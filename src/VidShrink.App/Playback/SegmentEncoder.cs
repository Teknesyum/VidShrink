using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Playback;

/// <summary>
/// Panelin bir zaman penceresi için ürettiği <b>dosya çifti</b>. İki dosya da kaynağın
/// aynı anında başlar ve ikisi de kendi zaman ekseninde sıfırdan akar — hizayı bu kurar.
/// </summary>
/// <remarks>
/// Hiza neden böyle: birleştirilmiş kare tek ffmpeg sürecinde <c>hstack</c> ile üretiliyor
/// ve <see cref="VidShrink.Ffmpeg.Playback.ComparisonGraph"/> <c>-ss</c>'i <b>iki girdiye de
/// aynı değerle</b> veriyor. Sağ yarıya 2 sn'lik bir parça, sol yarıya bütün kaynak
/// konursa aynı <c>-ss</c> iki girdide iki ayrı ana düşer. Bu yüzden sol yarı da aynı
/// pencereye kesiliyor: iki dosya aynı anda başlayınca <c>hstack</c> hizayı kendiliğinden
/// tutturuyor. Sol kesit <c>-qp 0</c> ile kayıpsızdır; "orijinal" yarı yeniden
/// sıkıştırılmış görünmez.
/// </remarks>
internal sealed record PreviewClip
{
    /// <summary>Sol yarı: kaynağın penceresi, kayıpsız.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Sağ yarı: aynı pencerenin plana göre kodlanmış hâli.</summary>
    public required string EncodedPath { get; init; }

    /// <summary>Pencerenin kaynaktaki başlangıcı.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>Pencerenin gerçek süresi; kaynağın sonunda istenenden kısadır.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>T47'nin döndürdüğü alan. Rozetin koşulu budur, panel kendi kararını vermez.</summary>
    public required bool IsApproximate { get; init; }

    /// <summary>ffmpeg'e gerçekten geçen tam sayı kalite değeri; kodlayıcı ölçeği modellenmiyorsa yok.</summary>
    public required int? Crf { get; init; }

    /// <summary>İki dosyanın birlikte kodlanma süresi.</summary>
    public required TimeSpan Elapsed { get; init; }

    public double EndSeconds => StartSeconds + DurationSeconds;
}

/// <summary>
/// Başarısız bir kodlamanın sebebi. Kodlayan taraf metin üretmez, <b>anahtar</b> üretir:
/// dizgeyi dile çeviren taraf paneli barındıran <see cref="PanelHost"/>'tur.
/// </summary>
/// <param name="Key">Sebebin dil anahtarı.</param>
/// <param name="Detail">
/// Anahtarın biçim argümanı. ffmpeg'in kendi <c>StandardError</c>'ı buraya düşer ve
/// <b>ham</b> kalır: motorun ürettiği tanı metni çevrilmez, teşhis değerini yitirmesin diye
/// olduğu gibi taşınır. Ekranda etiketli görünür — <c>playback.error.engine</c> anahtarı
/// "Motor iletisi: {0}" yazar, yani ham metin sessizce arayüz metnine karışmaz. Aynı karar
/// T90'ın 7. kriterinde <c>LocalizeStage</c> için verilmişti.
/// </param>
internal readonly record struct EncodeFailure(string Key, string? Detail = null);

/// <summary>
/// Kısa önizleme parçalarını kodlayan taraf. Aynı anda <b>en çok bir</b> kodlama koşar;
/// yeni istek gelince koşan iptal edilir, birikmez.
/// </summary>
/// <remarks>
/// Geçici dosyalar <c>%TEMP%/vidshrink_*</c> kalıbındadır, yani çökme durumunda
/// <see cref="TempCleanup"/> onları toplar. Ayakta en çok
/// <see cref="KeepClips"/> çift tutulur: biri gösterilen, biri önden hazırlanan; daha
/// eskisi silinir. Silme başarısız olabilir (dosyayı okuyan boru hâlâ açıkken Windows
/// tutar), bu yüzden başarısız silmeler kuyruğa alınır ve bir sonraki turda yeniden
/// denenir.
/// </remarks>
internal sealed class SegmentEncoder : IDisposable
{
    /// <summary>
    /// Ayar değişimi ile kodlamanın başlaması arasındaki gecikme. <c>ScheduleRecalculate</c>'in
    /// 160 ms'i yetmiyor: ölçülen parça kodlaması bunun katı sürüyor ve kaydırıcı sürüklenirken
    /// her ara değer için bir ffmpeg açılırdı. Sözleşmenin verdiği 300-500 ms aralığının ortası.
    /// </summary>
    internal const int DebounceMilliseconds = 400;

    /// <summary>Ayakta tutulan çift sayısı: gösterilen ve önden hazırlanan.</summary>
    internal const int KeepClips = 2;

    /// <summary>Geçici dosya kalıbı. <see cref="TempCleanup"/> bunu tanır.</summary>
    internal const string TempPrefix = "vidshrink_preview";

    private readonly string _tempDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _bookGate = new();
    private readonly List<PreviewClip> _live = new();
    private readonly List<string> _pendingDelete = new();

    private CancellationTokenSource? _inflight;
    private int _running;
    private int _peakRunning;
    private int _started;
    private int _completed;
    private long _counter;
    private bool _disposed;

    internal SegmentEncoder(string? tempDirectory = null)
        => _tempDirectory = tempDirectory ?? Path.GetTempPath();

    /// <summary>
    /// Ölçülmüş kodlayıcı yeteneği. Önizleme parçası psy/AQ bayraklarını buradan alır;
    /// <c>null</c> kalırsa parça bayraksız kodlanır ve tam kodlamadan ayrışır. Yoklamayı
    /// arayüz iş parçacığında doğurmamak için dışarıdan, arka planda ölçülmüş hâliyle verilir.
    /// </summary>
    internal IEncoderAvailability? Availability { get; set; }

    /// <summary>
    /// Bir pencerenin parçasını hesaplar; kodlayan yol da imza hesabı da buradan geçer,
    /// böylece ikisi aynı <see cref="Availability"/> değerini görür. Pencere kaynağın
    /// dışına düşerse <see cref="ArgumentOutOfRangeException"/> atar.
    /// </summary>
    internal PreviewSegment Describe(
        MediaInfo info, EncodePlan plan, double startSeconds, string outputPath, ComplexityProfile? complexity)
        => PreviewSegment.For(info, plan, startSeconds, outputPath,
            complexity: complexity, availability: Availability);

    /// <summary>Son başarısız kodlamanın sebebi, anahtar hâlinde. Başarıda temizlenir.</summary>
    internal EncodeFailure? LastFailure { get; private set; }

    /// <summary>
    /// Son başarısız kodlamanın <b>anahtarı</b>. Ekrana çıkan metin değil, sebebin kimliğidir;
    /// dile çeviren taraf <see cref="PanelHost"/>'tur.
    /// </summary>
    internal string? LastError => LastFailure?.Key;

    /// <summary>Başlatılan kodlama sayısı — iptal ölçümü bunu okur.</summary>
    internal int StartedEncodes => Volatile.Read(ref _started);

    /// <summary>Sonuna kadar koşan kodlama sayısı.</summary>
    internal int CompletedEncodes => Volatile.Read(ref _completed);

    /// <summary>Aynı anda koşan en yüksek kodlama sayısı. Sözleşmenin "tek kodlama" kuralı bu.</summary>
    internal int PeakConcurrentEncodes => Volatile.Read(ref _peakRunning);

    /// <summary>Diskte tutulan çift sayısı.</summary>
    internal int LiveClipCount { get { lock (_bookGate) return _live.Count; } }

    /// <summary>Bu kodlayıcının bıraktığı bütün dosyalar. Ölçüm bunu sayar.</summary>
    internal IReadOnlyList<string> LiveFiles
    {
        get
        {
            lock (_bookGate)
                return _live.SelectMany(clip => new[] { clip.SourcePath, clip.EncodedPath }).ToList();
        }
    }

    /// <summary>Koşan kodlamayı iptal eder. Koşan yoksa hiçbir şey yapmaz.</summary>
    internal void Cancel()
    {
        CancellationTokenSource? inflight;
        lock (_bookGate) { inflight = _inflight; _inflight = null; }
        try { inflight?.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Sol kesitin argümanları. Kayıpsız (<c>-qp 0</c>) ve en hızlı ön ayarla: bu dosya
    /// karşılaştırmanın "orijinal" yarısı, yeniden sıkıştırılmış görünmemeli. Ses atılır,
    /// panel ses çalmaz.
    /// </summary>
    internal static IReadOnlyList<string> BuildSourceClipArguments(
        string sourcePath, double startSeconds, double durationSeconds, string outputPath)
        => new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-ss", Seconds(startSeconds),
            "-i", sourcePath,
            "-t", Seconds(durationSeconds),
            "-map", "0:v:0", "-an", "-sn",
            "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0",
            outputPath
        };

    /// <summary>
    /// Verilen an için parçayı kodlar. Koşan kodlama varsa iptal edilir. İptal edilen
    /// istek <c>null</c> döner ve <see cref="LastFailure"/>'a dokunmaz; ffmpeg hatası da
    /// <c>null</c> döner ama hatayı yazar.
    /// </summary>
    internal async Task<PreviewClip?> RequestAsync(
        MediaInfo info,
        EncodePlan plan,
        double startSeconds,
        ComplexityProfile? complexity = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(plan);
        if (_disposed) return null;

        Cancel();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_bookGate) _inflight = cts;

        try
        {
            await _gate.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cts.Dispose();
            return null;
        }

        try
        {
            cts.Token.ThrowIfCancellationRequested();
            return await EncodeAsync(info, plan, startSeconds, complexity, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
            lock (_bookGate) { if (ReferenceEquals(_inflight, cts)) _inflight = null; }
            cts.Dispose();
        }
    }

    private async Task<PreviewClip?> EncodeAsync(
        MediaInfo info, EncodePlan plan, double startSeconds, ComplexityProfile? complexity, CancellationToken ct)
    {
        var start = Clamp(info, startSeconds);
        var index = Interlocked.Increment(ref _counter);
        var sourcePath = Path.Combine(_tempDirectory, $"{TempPrefix}_{index}_src.mp4");
        var encodedPath = Path.Combine(_tempDirectory, $"{TempPrefix}_{index}_out.mp4");

        PreviewSegment segment;
        try
        {
            segment = Describe(info, plan, start, encodedPath, complexity);
        }
        catch (ArgumentOutOfRangeException)
        {
            LastFailure = new EncodeFailure(WindowOutOfRangeKey);
            return null;
        }

        Interlocked.Increment(ref _started);
        var running = Interlocked.Increment(ref _running);
        Bump(running);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var left = FfmpegRunner.RunAsync(
                BuildSourceClipArguments(info.FilePath, start, segment.DurationSeconds, sourcePath), ct);
            var right = FfmpegRunner.RunAsync(segment.Arguments, ct);
            var runs = await Task.WhenAll(left, right).ConfigureAwait(false);
            clock.Stop();

            if (!runs[0].Ok || !runs[1].Ok || !File.Exists(sourcePath) || !File.Exists(encodedPath))
            {
                LastFailure = FirstFailure(runs);
                Delete(sourcePath);
                Delete(encodedPath);
                return null;
            }

            Interlocked.Increment(ref _completed);
            LastFailure = null;

            var clip = new PreviewClip
            {
                SourcePath = sourcePath,
                EncodedPath = encodedPath,
                StartSeconds = start,
                DurationSeconds = segment.DurationSeconds,
                IsApproximate = segment.IsApproximate,
                Crf = segment.Plan.Crf,
                Elapsed = clock.Elapsed
            };

            Register(clip);
            return clip;
        }
        catch (OperationCanceledException)
        {
            Delete(sourcePath);
            Delete(encodedPath);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }

    /// <summary>
    /// Çifti deftere yazar ve <see cref="KeepClips"/>'ten eskileri siler. Silinemeyen dosya
    /// kuyrukta kalır; bir sonraki turda yeniden denenir.
    /// </summary>
    private void Register(PreviewClip clip)
    {
        List<string> drop = new();
        lock (_bookGate)
        {
            _live.Add(clip);
            while (_live.Count > KeepClips)
            {
                var old = _live[0];
                _live.RemoveAt(0);
                drop.Add(old.SourcePath);
                drop.Add(old.EncodedPath);
            }
            drop.AddRange(_pendingDelete);
            _pendingDelete.Clear();
        }

        foreach (var path in drop) Delete(path);
    }

    private void Delete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            lock (_bookGate) if (!_pendingDelete.Contains(path)) _pendingDelete.Add(path);
        }
    }

    private void Bump(int running)
    {
        int peak;
        while (running > (peak = Volatile.Read(ref _peakRunning)))
            Interlocked.CompareExchange(ref _peakRunning, running, peak);
    }

    /// <summary>ffmpeg tanı metni bırakmadan düştü; geriye yalnız çıkış kodu kaldı.</summary>
    internal const string ExitCodeKey = "playback.error.exit-code";

    /// <summary>
    /// ffmpeg'in kendi <c>StandardError</c>'ı. Metin motordan gelir ve İngilizcedir;
    /// çevrilmez, <b>etiketlenir</b>. Karar bilinçli: ham metin teşhis için değerlidir ama
    /// etiketsiz geçirilirse arayüz metnine karışır.
    /// </summary>
    internal const string EngineMessageKey = "playback.error.engine";

    /// <summary>İki koşum da tamam dedi ama dosya diskte yok.</summary>
    internal const string NoFileKey = "playback.error.no-file";

    /// <summary>
    /// İstenen pencere kaynağın dışına düşüyor. <see cref="Clamp"/> bu durumu önlediği için
    /// yol beklenmedik; kesme iletisi geliştiriciye aittir ve ekrana taşınmaz, kullanıcı
    /// anahtardan gelen sebebi görür.
    /// </summary>
    internal const string WindowOutOfRangeKey = "playback.error.window";

    internal static EncodeFailure FirstFailure(FfmpegRun[] runs)
    {
        foreach (var run in runs)
            if (!run.Ok)
                return string.IsNullOrWhiteSpace(run.StandardError)
                    ? new EncodeFailure(ExitCodeKey, run.ExitCode.ToString(CultureInfo.InvariantCulture))
                    : new EncodeFailure(EngineMessageKey, run.StandardError.Trim());
        return new EncodeFailure(NoFileKey);
    }

    private static double Clamp(MediaInfo info, double startSeconds)
    {
        if (startSeconds < 0) return 0;
        if (info.DurationSeconds <= 0) return startSeconds;
        var last = Math.Max(0, info.DurationSeconds - PreviewSegment.WindowSeconds);
        return Math.Min(startSeconds, last);
    }

    private static string Seconds(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();

        List<string> drop;
        lock (_bookGate)
        {
            drop = _live.SelectMany(clip => new[] { clip.SourcePath, clip.EncodedPath }).ToList();
            drop.AddRange(_pendingDelete);
            _live.Clear();
            _pendingDelete.Clear();
        }

        foreach (var path in drop)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        _gate.Dispose();
    }
}
