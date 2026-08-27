using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.Core;
using VidShrink.Core.Playback;

namespace VidShrink.App.Playback;

/// <summary>
/// Paneli gerçek kare kaynağına bağlayan taraf. Panel kaynağı bilmez, kaynak arayüz
/// çizmez; ikisini birbirine bu sınıf takar.
///
/// Akış: <see cref="IComparisonFrameSource.TryTake"/> ile kare alınır, panonun havuzundan
/// kiralanan tampona kopyalanır, <see cref="ComparisonSurface.Submit"/> ile sunuma verilir
/// ve kare aynı turda <see cref="IComparisonFrameSource.Return"/> ile kaynağın havuzuna
/// döner. İade edilmeyen kare havuzu kurutur, bu yüzden iade <c>finally</c> içindedir.
///
/// Sağ yarının üç durumu vardır ve sırası şudur: tam çıktı varsa o gösterilir; yoksa
/// <see cref="SegmentEncoder"/>'ın ürettiği kısa parça çifti gösterilir; o da yoksa perde
/// iner. Parça modunda <b>iki girdi de</b> aynı pencereye kesilmiş dosyalardır — hizanın
/// nasıl kurulduğu <see cref="PreviewClip"/> belgesinde.
/// </summary>
internal sealed class PanelHost : IDisposable
{
    /// <summary>Bunun altındaki panoya akış kurulmaz — ffmpeg'e verilecek anlamlı bir ölçü değil.</summary>
    private const int MinPanelEdge = 64;

    /// <summary>Bu kadarlık bir ölçü değişimi akışı yeniden kurmaya değmez.</summary>
    private const double ResizeTolerance = 8;

    private const int MaxFps = 60;

    /// <summary>Bir sunum turunda en çok kaç kare atlanabileceği — yetişme sınırlı kalsın.</summary>
    private const int CatchUpCeiling = 8;

    /// <summary>Pencerenin bu kadarı oynadığında sonraki pencere kodlanmaya başlar (K3).</summary>
    private const double PrefetchAtFraction = 0.5;

    /// <summary>Pencerenin sonuna bu kadar kalınca sonraki pencereye geçilir.</summary>
    private const double AdvanceTailSeconds = 0.08;

    private readonly ComparisonPanel _panel;
    private readonly Func<IComparisonFrameSource> _factory;
    private readonly DispatcherTimer _settle;
    private readonly DispatcherTimer _segmentDelay;
    private readonly SegmentEncoder _segments;

    private IComparisonFrameSource? _source;
    private string? _left;
    private string? _right;
    private double _aspect = 16.0 / 9.0;
    private TimeSpan _duration;
    private int _fps = 30;
    private PixelSize _panelSize = PixelSize.Empty;

    private Task _restart = Task.CompletedTask;
    private bool _open;
    private bool _awaitingFirst;
    private bool _turkish;
    private bool _disposed;
    private int _generation;

    private long _submitted;
    private long _windowFrames;
    private long _windowStart;
    private double _screenFps;

    private MediaInfo? _info;
    private EncodePlan? _plan;
    private ComplexityProfile? _profile;
    private PreviewClip? _clip;
    private PreviewClip? _ahead;
    private double _clipStart;
    private bool _clipRunning;
    private bool _aheadRunning;
    private string? _clipError;

    internal PanelHost(ComparisonPanel panel, Func<IComparisonFrameSource> factory, SegmentEncoder? segments = null)
    {
        _panel = panel;
        _factory = factory;
        _segments = segments ?? new SegmentEncoder();

        // K3: pencere sürüklenirken her pikselde akış kurulmaz; yerleşme beklenir.
        _settle = new DispatcherTimer { Interval = Motion("MotionSlow", 360) };
        _settle.Tick += (_, _) => { _settle.Stop(); Restart(); };

        // K1: ayar değişimi ile kodlamanın başlaması arasındaki gecikme. Recalculate'in kendi
        // 160 ms'i kodlamadan kısa; kaydırıcı sürüklenirken her ara değer bir ffmpeg açardı.
        _segmentDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SegmentEncoder.DebounceMilliseconds) };
        _segmentDelay.Tick += (_, _) => { _segmentDelay.Stop(); BeginClip(_clipStart); };

        _panel.Frames.SizeChanged += (_, _) => OnResized();
        _panel.Controls.PlayPauseRequested += (_, _) => ApplyPlayState();
        _panel.Controls.RestartRequested += (_, _) => Seek(TimeSpan.Zero);
        _panel.Controls.SeekRequested += (_, position) => Seek(position);

        _panel.SetCompact(true);
    }

    /// <summary>Panel açık mı. Kapalıyken ayakta hiçbir ffmpeg süreci yoktur.</summary>
    internal bool IsOpen => _open;

    /// <summary>Ekrana konan kare sayısı — kaynak bağlandığından beri.</summary>
    internal long PresentedFrames => _submitted;

    /// <summary>Son saniyede ekrana konan kare.</summary>
    internal double ScreenFps => _screenFps;

    internal ComparisonSourceStatus? SourceStatus => _source?.Status;

    /// <summary>Sağ yarıyı besleyen kısa parça çifti; tam çıktı gösterilirken <c>null</c>.</summary>
    internal PreviewClip? ActiveClip => _right is null ? _clip : null;

    /// <summary>Önden hazırlanmış bir sonraki pencere (K3).</summary>
    internal PreviewClip? PreparedClip => _ahead;

    /// <summary>Kısa parça kodlayıcısı — ölçüm iptal ve geçici dosya sayımını buradan okur.</summary>
    internal SegmentEncoder Segments => _segments;

    /// <summary>
    /// K4 rozetinin metni; rozet gerekmiyorsa <c>null</c>. Koşulu T47'nin döndürdüğü
    /// <see cref="PreviewClip.IsApproximate"/> sürer, panel kendi kararını vermez.
    ///
    /// K7: basılan sayı ffmpeg'e <b>gerçekten geçen</b> tam sayıdır
    /// (<c>PreviewSegment.Plan.Crf</c>), ham ondalık değer değil. Ondalık değeri basmak
    /// kullanıcıya kodlanandan farklı bir sayı gösterirdi. Kodlayıcının kalite ölçeği
    /// modellenmiyorsa sayı hiç gösterilmez.
    /// </summary>
    internal string? ApproximateBadge
    {
        get
        {
            var clip = ActiveClip;
            if (clip is null || !clip.IsApproximate) return null;
            var text = LanguageCatalog.Localize("Approximate preview", _turkish);
            return clip.Crf is { } crf ? $"{text} · CRF {crf}" : text;
        }
    }

    /// <summary>
    /// Ayar değiştiğinde çağrılır. Planın kendisi kodlama başlatmaz; gecikme dolduğunda
    /// oynatma konumundan başlayan pencere kodlanır (K1).
    /// </summary>
    internal void SetPlan(MediaInfo? info, EncodePlan? plan, ComplexityProfile? profile)
    {
        if (_disposed) return;
        _info = info;
        _plan = plan;
        _profile = profile;

        if (info is null || plan is null || _left is null)
        {
            _segmentDelay.Stop();
            _segments.Cancel();
            return;
        }

        // Tam çıktı varken parça üretilmez: sağ yarı zaten gerçek çıktıyı gösteriyor.
        if (_right is not null) { _segmentDelay.Stop(); _segments.Cancel(); return; }

        ScheduleClip(_clipStart);
    }

    /// <summary>
    /// Panelin süreceği dosyalar. Sağ taraf için işlenmiş dosya yoksa <c>null</c> geçilir —
    /// o zaman sağ taraf sahte çıktı sunmaz, perde arkasında sebebini söyler.
    /// </summary>
    internal void SetFiles(string? source, string? processed, double aspect, TimeSpan duration, double sourceFps)
    {
        var left = string.IsNullOrWhiteSpace(source) || !File.Exists(source) ? null : source;
        var right = string.IsNullOrWhiteSpace(processed) || !File.Exists(processed) ? null : processed;

        // Aynı dosyalar yeniden verilirse akış kurulmaz: her hedef tuşu Recalculate
        // üzerinden buraya düşer ve akışı her tuşta yeniden kurmak boru israfıdır.
        if (left == _left && right == _right) return;

        // Kaynak değiştiyse eldeki parçalar başka bir dosyanın parçalarıdır; tam çıktı
        // geldiyse parçaya gerek kalmaz. İki durumda da defter sıfırlanır.
        if (left != _left || right is not null)
        {
            _segmentDelay.Stop();
            _segments.Cancel();
            _clip = null;
            _ahead = null;
            _clipError = null;
            if (left != _left) _clipStart = 0;
        }

        _left = left;
        _right = right;
        if (aspect > 0) _aspect = aspect;
        _duration = duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
        _fps = (int)Math.Clamp(Math.Round(sourceFps <= 0 ? 30 : sourceFps), 1, MaxFps);

        if (_left is null) { Close(); return; }
        if (_open) Restart();
        if (_right is null) ScheduleClip(_clipStart);
    }

    internal void Open()
    {
        if (_disposed || _open || _left is null) return;
        _open = true;
        _panel.SetCompact(false);
        Restart();
    }

    internal void Close()
    {
        _settle.Stop();
        _segmentDelay.Stop();
        _segments.Cancel();
        _ = Teardown();
        _open = false;
        _panel.SetCompact(true);
        _panel.SetRightNotice(null);
        _panel.SetNotice(null);
        // Configure panoyu boşaltır: kapalı panel son kareyi donuk göstermez, boş duruma döner.
        if (_panelSize.Width > 0) _panel.Frames.Configure(new PixelSize(_panelSize.Width * 2, _panelSize.Height));
        _panel.Controls.IsPlaying = false;
        _panel.RefreshEmptyState();
    }

    internal void SetLanguage(bool turkish)
    {
        _turkish = turkish;
        _panel.SetLanguage(turkish);
    }

    /// <summary>
    /// Akışı panelin o anki ölçüsüyle kurar. Kare ölçüsü sabit bir sayıdan değil, panonun
    /// kendi genişliğinden gelir; kaynağın en boy oranı korunur, yoksa ffmpeg görüntüyü
    /// panoya doğru esnetirdi.
    /// </summary>
    /// <summary>
    /// Kurulumlar zincire dizilir: yerleşme ve dosya değişimi aynı anda düşerse ikisi
    /// sırayla koşar. Yarışan iki kurulum, kaybedenin kaynağını öksüz bırakabilirdi.
    /// </summary>
    private void Restart()
        => _restart = Chain(_restart);

    private async Task Chain(Task previous)
    {
        try { await previous; } catch { }
        await RestartCore();
    }

    private async Task RestartCore()
    {
        if (_disposed || !_open || _left is null) return;

        var size = Measure();
        if (size.Width < MinPanelEdge || size.Height < MinPanelEdge) return;

        await Teardown();
        if (_disposed || !_open) return;
        var generation = ++_generation;
        _panelSize = size;

        _panel.Frames.Configure(new PixelSize(size.Width * 2, size.Height));
        _panel.SetRightNotice(RightCurtain());
        _panel.SetNotice("The first frame is on its way");
        _panel.Controls.Duration = _duration > TimeSpan.Zero ? _duration : null;
        _panel.Controls.IsPlaying = true;

        var source = _factory();
        _source = source;
        source.StatusChanged += OnStatusChanged;

        // Parça modunda iki girdi de aynı pencereye kesilmiş dosyalardır: ikisi de kendi
        // ekseninde sıfırdan akar, hstack hizayı kendiliğinden tutturur. Pencere kısa olduğu
        // için tekrar kapalı — dolduğunda başa sarmak yerine sonraki pencereye geçilir.
        var clip = ActiveClip;
        var request = new ComparisonFrameRequest
        {
            LeftPath = clip?.SourcePath ?? _left,
            RightPath = clip?.EncodedPath ?? _right ?? _left,
            PanelWidth = size.Width,
            PanelHeight = size.Height,
            Fps = _fps,
            Realtime = true,
            Loop = clip is null
        };

        try
        {
            await source.StartAsync(request);
        }
        catch (Exception ex)
        {
            if (generation == _generation) _panel.SetNotice($"{LanguageCatalog.Localize("The comparison player could not start", _turkish)}: {ex.Message}");
            return;
        }

        if (generation != _generation) return;

        _submitted = 0;
        _awaitingFirst = true;
        _windowFrames = 0;
        _windowStart = Environment.TickCount64;
        Report(source.Status);
        Pump(generation);
    }

    /// <summary>
    /// Panonun ölçüsünden kare ölçüsü. Kaynağın oranı panoya sığdırılır (ZoomGesture de
    /// aynı sığdırmayı yapar), böylece boru zaten doğru orandaki kareyi verir.
    /// </summary>
    private PixelSize Measure()
    {
        var bounds = _panel.Frames.Bounds;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0) return PixelSize.Empty;

        if (width / height > _aspect) width = height * _aspect;
        else height = width / _aspect;

        // Tek panelin genişliği çift sayı olsun: bgra karesi de, birleşik genişlik de öyle.
        var w = Math.Max(MinPanelEdge, (int)Math.Round(width / 2) * 2);
        var h = Math.Max(MinPanelEdge, (int)Math.Round(height / 2) * 2);
        return new PixelSize(w, h);
    }

    private void OnResized()
    {
        if (!_open || _disposed) return;
        // K3: yakınlaştırma akışı yeniden kurmaz. Terfi de yakınlaştırmanın parçasıdır,
        // panel kök katmandayken ölçü değişimi kod çözmeye dokunmaz.
        if (_panel.IsPromoted) return;

        var size = Measure();
        if (size.Width < MinPanelEdge || size.Height < MinPanelEdge) return;
        if (Math.Abs(size.Width - _panelSize.Width) < ResizeTolerance &&
            Math.Abs(size.Height - _panelSize.Height) < ResizeTolerance) return;

        _settle.Stop();
        _settle.Start();
    }

    // ---- kısa parça: hazırlama, ilerleme, perde (K1, K2, K3, K6) ----------------------

    /// <summary>
    /// Perde metni. Tam çıktı da parça da yokken iner; kodlama hata verdiyse sebebini söyler,
    /// hata sessizce yutulmaz (K6).
    /// </summary>
    private string? RightCurtain()
    {
        if (_right is not null || ActiveClip is not null) return null;
        return _clipError is null
            ? "This part will be processed"
            : $"{LanguageCatalog.Localize("The preview sample could not be encoded", _turkish)}: {_clipError}";
    }

    /// <summary>Gecikmeyi kurar. Arka arkaya gelen istekler tek kodlamaya iner (K1).</summary>
    private void ScheduleClip(double startSeconds)
    {
        if (_disposed || _right is not null || _info is null || _plan is null || _left is null) return;
        _clipStart = Math.Max(0, startSeconds);
        _segmentDelay.Stop();
        _segmentDelay.Start();
    }

    /// <summary>Gösterilecek pencereyi kodlar; biterse akışı o çiftle yeniden kurar.</summary>
    private async void BeginClip(double startSeconds)
    {
        var info = _info;
        var plan = _plan;
        if (_disposed || info is null || plan is null || _left is null || _right is not null) return;

        _ahead = null;
        _clipRunning = true;
        try
        {
            var clip = await _segments.RequestAsync(info, plan, startSeconds, _profile);
            if (_disposed || _right is not null) return;
            if (clip is null)
            {
                // İptal edilen istek hata değildir; yerine yenisi zaten koşuyor.
                if (_segments.LastError is null) return;
                _clipError = _segments.LastError;
                _clip = null;
                _panel.SetRightNotice(RightCurtain());
                return;
            }

            _clipError = null;
            _clip = clip;
            _clipStart = clip.StartSeconds;
            if (_open) Restart();
        }
        catch (Exception ex)
        {
            _clipError = ex.Message;
            _panel.SetRightNotice(RightCurtain());
        }
        finally
        {
            _clipRunning = false;
        }
    }

    /// <summary>
    /// Bir sonraki pencereyi oynatma sürerken kodlar. En çok bir pencere ileriye bakılır;
    /// kuyruk kurulmaz.
    /// </summary>
    private async void PrepareAhead()
    {
        var info = _info;
        var plan = _plan;
        var clip = ActiveClip;
        if (_disposed || info is null || plan is null || clip is null) return;
        if (_aheadRunning || _clipRunning || _ahead is not null) return;

        var next = clip.EndSeconds;
        if (info.DurationSeconds > 0 && next >= info.DurationSeconds - 0.05) return;

        _aheadRunning = true;
        try
        {
            var prepared = await _segments.RequestAsync(info, plan, next, _profile);
            if (_disposed || _right is not null) return;
            // Kullanıcı bu sırada başka bir ana atladıysa hazırlanan pencere atılır.
            if (prepared is not null && ActiveClip is { } current && Math.Abs(prepared.StartSeconds - current.EndSeconds) < 0.05)
                _ahead = prepared;
        }
        catch (Exception ex)
        {
            _clipError = ex.Message;
        }
        finally
        {
            _aheadRunning = false;
        }
    }

    /// <summary>Pencere doldu: hazır olan sonrakine geçilir, yoksa o an kodlanır.</summary>
    private void AdvanceClip()
    {
        var clip = ActiveClip;
        if (clip is null) return;

        if (_ahead is { } ready)
        {
            _ahead = null;
            _clip = ready;
            _clipStart = ready.StartSeconds;
            if (_open) Restart();
            return;
        }

        if (_clipRunning) return;
        // Kaynağın sonundaki pencere: ilerlenecek yer yok, son kare donuk kalır.
        if (_info is { DurationSeconds: > 0 } info && clip.EndSeconds >= info.DurationSeconds - 0.05) return;
        BeginClip(clip.EndSeconds);
    }

    private void Pump(int generation)
    {
        var top = TopLevel.GetTopLevel(_panel);
        if (top is null) return;

        void Tick(TimeSpan _)
        {
            if (_disposed || !_open || generation != _generation) return;
            Drain();
            top.RequestAnimationFrame(Tick);
        }

        top.RequestAnimationFrame(Tick);
    }

    private void Drain()
    {
        var source = _source;
        if (source is null) return;
        if (!TakeNewestReady(source, out var frame)) return;

        var expected = _panel.Frames.FrameSize;
        var position = frame.Presentation;
        var fits = frame.Width == expected.Width && frame.Height == expected.Height;

        try
        {
            if (!fits) return;
            var buffer = _panel.Frames.Rent();
            Buffer.BlockCopy(frame.Buffer, 0, buffer, 0, Math.Min(frame.ByteLength, buffer.Length));
            _panel.Frames.Submit(buffer);
        }
        catch (InvalidOperationException)
        {
            // Pano bu turda yeniden yapılandırılıyor; kare iade edilir, tur atlanır.
            return;
        }
        finally
        {
            source.Return(frame);
        }

        _submitted++;
        var clip = ActiveClip;
        // Parça modunda boru penceresinin sıfırından akıyor; şerit kaynağın kendi eksenini
        // gösterir, yoksa 2 sn'lik pencere bütün videoymuş gibi görünürdü.
        _panel.Controls.Position = clip is null
            ? position
            : position + TimeSpan.FromSeconds(clip.StartSeconds);
        if (clip is not null) Follow(clip, position);
        // Boş durum kare panoya konduğunda kalkar, kare sıraya girdiğinde değil: sunum turu
        // bizim turumuzdan sonra çalışıyor, bir tur önce sorulursa cevap hâlâ "kare yok".
        if (_awaitingFirst && _panel.Frames.HasFrame)
        {
            _awaitingFirst = false;
            _panel.SetNotice(null);
            _panel.RefreshEmptyState();
        }
        SampleRate();
    }

    /// <summary>
    /// Pencerenin neresinde olduğumuza bakar: yarısı geçince sonraki pencere kodlanmaya
    /// başlar, sonuna gelince ona geçilir. Duraklatılmışken ileri hazırlık yapılmaz.
    /// </summary>
    private void Follow(PreviewClip clip, TimeSpan position)
    {
        if (!_panel.Controls.IsPlaying) return;
        var played = position.TotalSeconds;
        if (played >= clip.DurationSeconds - AdvanceTailSeconds) { AdvanceClip(); return; }
        if (played >= clip.DurationSeconds * PrefetchAtFraction) PrepareAhead();
    }

    /// <summary>
    /// Bir sunum turunda kaynaktan yalnız bir kare çekmek, tur hızı besleme hızının altına
    /// düştüğü anda kalıcı geri kalma üretiyordu: halka her turda taşıyor, ekrana konan kare
    /// giderek gerçek zamandan geriye kayıyordu. Burada tur başına kuyruk boşaltılıyor ve
    /// yalnız sonuncusu sunuluyor — atlananlar ekrana konmadan havuza iade ediliyor.
    ///
    /// Yetişiliyorken davranış değişmiyor: sırada tek kare varsa ikinci <c>TryTake</c> boş
    /// döner ve o tek kare sunulur. Atlama yalnız gerçekten geride kalınca oluyor.
    /// </summary>
    private static bool TakeNewestReady(IComparisonFrameSource source, out PlaybackFrame frame)
    {
        frame = null!;
        var skipped = 0;
        while (source.TryTake(out var next))
        {
            if (frame is not null)
            {
                source.Return(frame);
                skipped++;
            }
            frame = next;
            if (skipped >= CatchUpCeiling) break;
        }
        return frame is not null;
    }

    private void SampleRate()
    {
        _windowFrames++;
        var elapsed = Environment.TickCount64 - _windowStart;
        if (elapsed < 500) return;
        _screenFps = _windowFrames * 1000.0 / elapsed;
        _windowFrames = 0;
        _windowStart = Environment.TickCount64;
    }

    private void OnStatusChanged(object? sender, ComparisonSourceStatus status)
    {
        if (!ReferenceEquals(sender, _source)) return;
        Dispatcher.UIThread.Post(() => Report(status));
    }

    /// <summary>
    /// Kaynak kurulamadıysa panel sebebi söyler ve program çalışmaya devam eder — panelin
    /// yokluğu programı bozmaz (K2).
    /// </summary>
    private void Report(ComparisonSourceStatus status)
    {
        if (_disposed) return;
        if (status.State == ComparisonSourceState.Kullanilamiyor)
        {
            var reason = (_turkish ? status.MessageTr : status.MessageEn) ?? status.MessageEn ?? status.MessageTr;
            _panel.SetNotice(reason ?? "The comparison player could not start");
            _panel.Controls.IsPlaying = false;
        }
        else if (status.State == ComparisonSourceState.Durdu && _submitted == 0)
        {
            _panel.SetNotice("The comparison player could not start");
            _panel.Controls.IsPlaying = false;
        }
    }

    private void ApplyPlayState()
    {
        var source = _source;
        if (source is null) return;
        if (_panel.Controls.IsPlaying) source.Play();
        else
        {
            source.Pause();
            // Duraklatıldığında ileri hazırlık durur; duran oynatma için parça kodlanmaz.
            if (_aheadRunning) _segments.Cancel();
        }
    }

    private async void Seek(TimeSpan position)
    {
        // Parça modunda boru yalnız 2 sn'lik pencereyi tanır; atlamanın karşılığı o ana
        // yeni bir pencere kodlamaktır. Önden hazırlanan pencere atılır (K3).
        if (ActiveClip is not null || (_right is null && _info is not null && _plan is not null))
        {
            _ahead = null;
            ScheduleClip(position.TotalSeconds);
            return;
        }

        var source = _source;
        if (source is null) return;
        try { await source.SeekAsync(position); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { _panel.SetNotice(ex.Message); }
    }

    /// <summary>
    /// Kaynağı kapatır ve süreci öldürür.
    ///
    /// Kapatma <b>havuz kuyruğunda</b> yapılır, arayüz kuyruğunda değil: kaynağın kapatma
    /// yolu <c>await</c> ile yazılmış ve arayüz kuyruğundan çağrılınca sürdürmesi için o
    /// kuyruğu bekler — kapatmayı arayüz kuyruğunda beklemek program donduruyordu.
    /// </summary>
    private Task Teardown()
    {
        var source = _source;
        _source = null;
        _generation++;
        if (source is null) return Task.CompletedTask;
        source.StatusChanged -= OnStatusChanged;
        _screenFps = 0;
        return Task.Run(source.Dispose);
    }

    private TimeSpan Motion(string key, double fallbackMs)
        => _panel.TryFindResource(key, out var value) && value is TimeSpan span
            ? span
            : TimeSpan.FromMilliseconds(fallbackMs);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settle.Stop();
        _segmentDelay.Stop();
        // Uygulama kapanıyor: kalan parça dosyaları burada silinir (K5).
        _segments.Dispose();
        // Pencere kapanıyor: süreç gerçekten ölene kadar beklenir, öksüz ffmpeg kalmaz.
        // Bekleme havuz kuyruğunda olduğu için arayüz kuyruğunu kilitlemez.
        Teardown().Wait(TimeSpan.FromSeconds(3));
    }
}
