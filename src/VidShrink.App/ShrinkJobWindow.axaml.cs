using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>Ilerleme penceresinin gorunur durumu; olcunun sordugu tek alan.</summary>
internal enum ShrinkJobState
{
    Bekliyor,
    Kosuyor,
    Bitti,
    Gerekce,
    Hata
}

/// <summary>
/// Bes ret gerekcesini kullanicinin gordugu cumlenin anahtarina ceviren tek kapi. Bes uye
/// de burada adiyla gecer: bu switch olmadan gerekceler uretilir ama hicbiri ekrana ulasmaz.
/// </summary>
internal static class ShrinkProblemText
{
    internal const string NoTarget = "main.shrink-job.reason.no-target";
    internal const string TargetNotANumber = "main.shrink-job.reason.not-a-number";
    internal const string TargetNotPositive = "main.shrink-job.reason.not-positive";
    internal const string TargetNotInQuickList = "main.shrink-job.reason.not-in-quick-list";
    internal const string NoPath = "main.shrink-job.reason.no-path";

    internal static IReadOnlyList<ShrinkArgumentProblem> All { get; } = new[]
    {
        ShrinkArgumentProblem.NoTarget,
        ShrinkArgumentProblem.TargetNotANumber,
        ShrinkArgumentProblem.TargetNotPositive,
        ShrinkArgumentProblem.TargetNotInQuickList,
        ShrinkArgumentProblem.NoPath
    };

    internal static string Key(ShrinkArgumentProblem problem) => problem switch
    {
        ShrinkArgumentProblem.NoTarget => NoTarget,
        ShrinkArgumentProblem.TargetNotANumber => TargetNotANumber,
        ShrinkArgumentProblem.TargetNotPositive => TargetNotPositive,
        ShrinkArgumentProblem.TargetNotInQuickList => TargetNotInQuickList,
        ShrinkArgumentProblem.NoPath => NoPath,
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };

    internal static string QuickList()
        => string.Join(", ", ShellIntegration.QuickShrinkTargetsMegabytes.Select(ShellIntegration.FormatQuickShrinkLabel));
}

/// <summary>
/// Kabuk menusunden gelen kucultme isteginin tuketicisi. Ana pencere acilmaz: gorev
/// cubugunda duran bu kucuk pencere kodlamayi kosar, <see cref="EncodeProgress"/>
/// degerlerini gosterir ve is bitince kendini kapatir. Istek cozulemediyse pencere kalir,
/// tek satir gerekceyi ve ana pencereye gecis dugmesini gosterir.
/// </summary>
public partial class ShrinkJobWindow : Window
{
    internal static readonly TimeSpan Linger = TimeSpan.FromSeconds(6);

    private readonly ShellShrinkStartup _startup;
    private readonly ShrinkRequestQueue? _queue;
    private readonly string _language;
    private readonly AppSettings _appSettings = LoadAppSettings();
    private readonly List<ShrinkRequest> _pending = new();
    private readonly List<string> _outputs = new();
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _closeTimer;
    private bool _busy;
    private bool _started;
    private int _accepted;
    private int _finished;
    private PlanOptions? _template;
    private bool _ceilingTarget;
    private string? _extension;
    private bool _paused;
    private QueueEndChoice _whenDone;
    private DispatcherTimer? _countdown;
    private int? _countdownLeft;

    /// <summary>Uyut ve kapat bu kadar saniye geri sayar; "Vazgeç" arada durdurur.</summary>
    internal const int CountdownSeconds = 60;

    /// <summary>Kuyruk sonu eyleminin sistem yüzü; testler sahtesini verir.</summary>
    internal IQueueEndActions Actions { get; set; } = SystemQueueEndActions.Instance;

    public ShrinkJobWindow() : this(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null)
    {
    }

    internal ShrinkJobWindow(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _startup = startup;
        _queue = queue;

        var language = MainWindow.ResolveLanguage(null, CultureInfo.CurrentUICulture.Name);
        try
        {
            var settings = UpdateSettings.Load();
            language = MainWindow.ResolveLanguage(settings.Language, CultureInfo.CurrentUICulture.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        _language = language;
        Strings.Use(_language);

        InitializeComponent();
        if (Playback.HoverZone.MotionReduced) Classes.Add("reduced-motion");

        FlowDirection = Strings.IsRightToLeftLanguage(_language)
            ? Avalonia.Media.FlowDirection.RightToLeft
            : Avalonia.Media.FlowDirection.LeftToRight;

        if (OperatingSystem.IsMacOS()) WindowDecorations = WindowDecorations.Full;
        JobShell.PointerPressed += OnShellPointerPressed;

        BtnClose.Click += (_, _) => Close();
        BtnOpenInApp.Click += OnOpenInApp;
        BtnReveal.Click += OnReveal;
        BtnShare.Click += OnShare;
        BtnShareCancel.Click += OnShareCancel;
        BtnShareCopy.Click += OnCopyShareLink;
        BtnPause.Click += (_, _) => SetPaused(!_paused);
        BtnCountdownCancel.Click += (_, _) => CancelCountdown();
        CmbWhenDone.ItemsSource = new[]
        {
            Say("main.shrink-job.done.nothing"),
            Say("main.shrink-job.done.open-folder"),
            Say("main.shrink-job.done.sleep"),
            Say("main.shrink-job.done.power-off")
        };
        CmbWhenDone.SelectedIndex = 0;
        CmbWhenDone.SelectionChanged += (_, _) => _whenDone = (QueueEndChoice)Math.Max(0, CmbWhenDone.SelectedIndex);

        TxtHeadline.Text = Say("main.shrink-job.waiting");
        TxtTarget.Text = "";
        TxtStage.Text = "-";
        TxtRemaining.Text = "-";
        TxtMessage.Text = "";
        TxtNotice.Text = "";
    }

    /// <summary>
    /// C1-3: ana pencereye birden çok video ya da klasör bırakıldığında açılan kuyruk. Her dosya
    /// pencerenin o anki seçeneklerinin kopyasıyla kurulur; boyut tavanı olmayan yongada hedef
    /// dosyanın kendi kalite tavanından hesaplanır. <paramref name="extension"/> ön ayar kabından gelir.
    /// </summary>
    internal ShrinkJobWindow(IReadOnlyList<string> paths, PlanOptions template, bool ceilingTarget, string? extension, bool autoCrop = false)
        : this(new ShellShrinkStartup(paths.Select(path => new ShrinkRequest(0, path)).ToList(), null, paths.FirstOrDefault()), null)
    {
        _template = template;
        _ceilingTarget = ceilingTarget;
        _extension = extension;
        _autoCrop = autoCrop;
        TxtHeadline.Text = Say("main.shrink-job.batch", paths.Count);
    }

    private bool _autoCrop;

    /// <summary>
    /// HB #36: kuyruk "Siyah bantları kırp" kutusunu bayrak olarak taşır, dikdörtgeni değil —
    /// bantlar dosyaya özgü. Her dosya kendi yoklamasıyla kırpılır; elle yazılmış <c>crop=</c> korunur.
    /// </summary>
    internal async Task<PlanOptions> KirpmaylaAsync(PlanOptions options, MediaInfo info, CancellationToken ct)
    {
        if (!_autoCrop || options.Filters.Crop is not null) return options;
        options.Filters = OtomatikKirpma.Uygula(options.Filters, true, await OtomatikKirpma.BulAsync(info, ct));
        return options;
    }

    /// <summary>İsteğin plan seçenekleri ve hedefi. Kabuk isteği yalnız hedefi taşır; bırakılan kuyruk pencerenin ayarlarını.</summary>
    internal (PlanOptions Options, double TargetMb) OptionsFor(ShrinkRequest request, MediaInfo info)
    {
        if (_template is null) return (new PlanOptions { TargetMb = request.TargetMegabytes }, request.TargetMegabytes);
        var targetMb = _ceilingTarget ? PlanCalculator.QualityCeilingTargetMb(info) : _template.TargetMb;
        return (PlanCalculator.WithTarget(_template, targetMb), targetMb);
    }

    internal string ExtensionFor(EncodePlan plan) => _extension ?? plan.Streams?.Extension ?? "mp4";

    private string TargetLabel(double targetMb)
        => _template is null
            ? ShellIntegration.FormatQuickShrinkLabel((int)targetMb)
            : Bicim.Boyut.Hedef(targetMb, Strings.CultureOf(_language)) + " MB";

    private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    /// <summary>
    /// Bu pencerenin metinlerini urettigi dil. Kurulusta bir kez cozulur ve bir daha
    /// degismez: <see cref="Say(string)"/> surec genelindeki <see cref="Strings.Language"/>
    /// degerini okumaz, yoksa baska bir is parcaciginda kosan bir olcu dili degistirdiginde
    /// pencerenin metni koşudan koşuya kayar.
    /// </summary>
    internal string Language => _language;

    /// <summary>Pencerenin gorunur durumu; olcu buna bakar, piksele degil.</summary>
    internal ShrinkJobState State { get; private set; } = ShrinkJobState.Bekliyor;

    /// <summary>Bu pencerenin teslim ettigi cikti yollari, kabul sirasiyla.</summary>
    internal IReadOnlyList<string> Outputs => _outputs;

    /// <summary>Kuyruga girmis istek sayisi; ikinci surecin boruyla verdikleri dahil.</summary>
    internal int AcceptedCount => _accepted;

    internal string MessageText => TxtMessage.Text ?? "";

    /// <summary>Henüz başlamamış istekler, koşacakları sırayla. Koşan dosya burada değil.</summary>
    internal IReadOnlyList<ShrinkRequest> Pending => _pending;

    internal bool Paused => _paused;

    internal QueueEndChoice WhenDone
    {
        get => _whenDone;
        set => CmbWhenDone.SelectedIndex = (int)value;
    }

    /// <summary>Geri sayımda kalan saniye; geri sayım yoksa <c>null</c>.</summary>
    internal int? CountdownLeft => _countdownLeft;

    internal string CountdownText => TxtCountdown.Text ?? "";

    /// <summary>Bulunamayan yollar icin basilan uyari satiri; bos ise satir gorunmez.</summary>
    internal string NoticeText => TxtNotice.Text ?? "";

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Begin();
    }

    /// <summary>
    /// Isteklerin tuketildigi yer. Gerekce varsa pencere kalir; ayni argv'den gelen her
    /// istek kuyruga girer ve kuyrugun sahibiysek boru da acilir, ikinci surecin istegi
    /// buraya duser.
    /// </summary>
    internal void Begin()
    {
        if (_started) return;
        _started = true;

        ShowMissing();

        if (_startup.Problem is { } problem)
        {
            QueueControls.IsVisible = false;
            ShowProblem(problem);
            return;
        }

        foreach (var request in _startup.Items) Accept(request);

        if (_queue is null) return;
        try
        {
            _queue.StartOwning(incoming => Dispatcher.UIThread.Post(() => Accept(incoming)));
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Diskte bulunamayan argv parcalari. Kodlama satirinin ustunde ayri bir satirda durur:
    /// <c>TxtMessage</c> her istekte silindigi icin uyari orada kaybolurdu.
    /// </summary>
    private void ShowMissing()
    {
        if (_startup.Missing.Count == 0) return;
        TxtNotice.Text = Say("main.shrink-job.missing-paths",
            _startup.Missing.Count,
            string.Join(", ", _startup.Missing));
        TxtNotice.IsVisible = true;
    }

    private void Accept(ShrinkRequest request)
    {
        _accepted++;
        _pending.Add(request);
        RefreshPending();
        _ = PumpAsync();
    }

    /// <summary>C1-2: bekleyen isteği sıradan çıkarır. Koşan dosyaya dokunmaz.</summary>
    internal void RemovePending(int index)
    {
        if (index < 0 || index >= _pending.Count) return;
        _pending.RemoveAt(index);
        _accepted--;
        RefreshPending();
    }

    /// <summary>C1-2: bekleyen isteği <paramref name="delta"/> kadar kaydırır; sınırın dışına taşımaz.</summary>
    internal void MovePending(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= _pending.Count || target < 0 || target >= _pending.Count) return;
        (_pending[index], _pending[target]) = (_pending[target], _pending[index]);
        RefreshPending();
    }

    /// <summary>
    /// C1-2: duraklatılan kuyrukta koşan dosya biter, sıradaki başlamaz. Sürdürünce pompa yeniden açılır.
    /// </summary>
    internal void SetPaused(bool paused)
    {
        _paused = paused;
        BtnPause.Content = Say(paused ? "main.shrink-job.resume" : "main.shrink-job.pause");
        TxtPaused.IsVisible = paused;
        if (!paused) _ = PumpAsync();
    }

    private void RefreshPending()
    {
        PendingPanel.IsVisible = _pending.Count > 0;
        TxtPending.Text = Say("main.shrink-job.pending", _pending.Count);
        PendingList.Children.Clear();
        for (var i = 0; i < _pending.Count; i++) PendingList.Children.Add(PendingRow(i));
    }

    private Grid PendingRow(int index)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = Token("SpaceXs") };
        var name = new TextBlock
        {
            Text = Path.GetFileName(_pending[index].Path),
            FontSize = Token("FontSizeSm"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(name, _pending[index].Path);
        row.Children.Add(name);
        AddRowButton(row, 1, "IconChevronUp", "main.shrink-job.move-up", index > 0, () => MovePending(index, -1));
        AddRowButton(row, 2, "IconChevronDown", "main.shrink-job.move-down", index < _pending.Count - 1, () => MovePending(index, 1));
        AddRowButton(row, 3, "IconClose", "main.shrink-job.remove", true, () => RemovePending(index));
        return row;
    }

    private void AddRowButton(Grid row, int column, string icon, string key, bool enabled, Action click)
    {
        var size = Token("IconSizeSm");
        var button = new Button
        {
            Content = new PathIcon { Data = this.FindResource(icon) as Geometry, Width = size, Height = size },
            Padding = this.FindResource("ChipPadding") is Avalonia.Thickness padding ? padding : default,
            IsEnabled = enabled,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(button, Say(key));
        ToolTip.SetTip(button, Say(key));
        button.Click += (_, _) => click();
        Grid.SetColumn(button, column);
        row.Children.Add(button);
    }

    private double Token(string key) => this.FindResource(key) is double value ? value : 0;

    private void ShowProblem(ShrinkArgumentProblem problem)
    {
        State = ShrinkJobState.Gerekce;
        TxtHeadline.Text = Say("main.shrink-job.rejected");
        TxtTarget.Text = "";
        RowFacts.IsVisible = false;
        Progress.IsVisible = false;
        TxtMessage.Text = problem == ShrinkArgumentProblem.TargetNotInQuickList
            ? Say(ShrinkProblemText.Key(problem), ShrinkProblemText.QuickList())
            : Say(ShrinkProblemText.Key(problem));
        BtnOpenInApp.IsVisible = true;
    }

    private async Task PumpAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            while (_pending.Count > 0 && !_paused)
            {
                var request = _pending[0];
                _pending.RemoveAt(0);
                RefreshPending();
                await RunOneAsync(request);
            }
        }
        finally
        {
            _busy = false;
        }

        if (_pending.Count == 0 && !_paused && _finished > 0) QueueDrained();
    }

    /// <summary>
    /// C1-4: sıra boşaldığında seçilen eylem. Uyut ve kapat geri sayımla gelir ve pencere bu
    /// sırada kapanmaz; klasör açma son çıktının klasörünü açar.
    /// </summary>
    internal void QueueDrained()
    {
        if (_pending.Count > 0 || _paused || _busy) return;
        switch (_whenDone)
        {
            case QueueEndChoice.Sleep:
            case QueueEndChoice.PowerOff:
                StartCountdown();
                return;
            case QueueEndChoice.OpenFolder when _outputs.Count > 0:
                Reveal(_outputs[^1]);
                break;
        }
        if (State == ShrinkJobState.Bitti) ArmClose();
    }

    private void StartCountdown()
    {
        _countdownLeft = CountdownSeconds;
        ShowCountdown();
        _countdown ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdown.Tick -= OnCountdownTick;
        _countdown.Tick += OnCountdownTick;
        _countdown.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e) => CountdownTick();

    /// <summary>Geri sayımın bir saniyesi. Sıfıra inince eylem bir kez çalışır.</summary>
    internal void CountdownTick()
    {
        if (_countdownLeft is not int left) return;
        left--;
        if (left > 0)
        {
            _countdownLeft = left;
            ShowCountdown();
            return;
        }
        var choice = _whenDone;
        CancelCountdown();
        if (choice == QueueEndChoice.Sleep) Actions.Sleep();
        else if (choice == QueueEndChoice.PowerOff) Actions.PowerOff();
    }

    internal void CancelCountdown()
    {
        _countdown?.Stop();
        _countdownLeft = null;
        CountdownRow.IsVisible = false;
    }

    private void ShowCountdown()
    {
        CountdownRow.IsVisible = true;
        TxtCountdown.Text = Say(_whenDone == QueueEndChoice.Sleep ? "main.shrink-job.countdown.sleep" : "main.shrink-job.countdown.power-off", _countdownLeft);
    }

    private async Task RunOneAsync(ShrinkRequest request)
    {
        State = ShrinkJobState.Kosuyor;
        Progress.IsVisible = true;
        RowFacts.IsVisible = true;
        BtnReveal.IsVisible = false;
        ResetShare(false);
        Progress.Value = 0;
        TxtHeadline.Text = Path.GetFileName(request.Path);
        TxtTarget.Text = "";
        TxtMessage.Text = "";

        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var info = await FfprobeClient.ProbeAsync(request.Path, cts.Token);
            var (options, targetMb) = OptionsFor(request, info);
            options = await KirpmaylaAsync(options, info, cts.Token);
            TxtTarget.Text = Say("main.shrink-job.target", TargetLabel(targetMb), _finished + 1, _accepted);
            var plan = PlanCalculator.Build(info, options);
            var output = UniqueOutputPath(request.Path, _appSettings, plan, targetMb, ExtensionFor(plan));

            var progress = new Progress<EncodeProgress>(step =>
            {
                Progress.Value = step.Fraction;
                TxtStage.Text = step.Stage;
                TxtRemaining.Text = Saat.Kalan(step.Remaining);
            });

            var result = await new EncodeRunner().RunAsync(
                info, plan, output, targetMb, progress, cts.Token, options.FillPolicy);

            if (result.Success)
            {
                _outputs.Add(result.OutputPath);
                State = ShrinkJobState.Bitti;
                Progress.Value = 1;
                TxtMessage.Text = BittiSatiri(result, targetMb);
                BtnReveal.IsVisible = true;
                ResetShare(true);
            }
            else
            {
                State = ShrinkJobState.Hata;
                TxtMessage.Text = HataSatiri(result, targetMb);
                BtnOpenInApp.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            State = ShrinkJobState.Hata;
            TxtMessage.Text = Say("main.run.cancelled");
        }
        catch (Exception ex)
        {
            State = ShrinkJobState.Hata;
            TxtMessage.Text = ex.Message;
            BtnOpenInApp.IsVisible = true;
        }
        finally
        {
            _finished++;
            _cts = null;
            cts.Dispose();
        }
    }

    private void ArmClose()
    {
        if (_closeTimer is not null) return;
        _closeTimer = new DispatcherTimer { Interval = Linger };
        _closeTimer.Tick += (_, _) =>
        {
            if ((_shareFlow?.Running ?? false) || _countdownLeft is not null) return;
            _closeTimer?.Stop();
            if (_pending.Count == 0 && !_busy) Close();
        };
        _closeTimer.Start();
    }

    private void OnOpenInApp(object? sender, RoutedEventArgs e)
    {
        var window = new MainWindow(_startup.FallbackPath ?? _startup.Request?.Path);
        window.Show();
        Close();
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        if (_outputs.Count > 0) Reveal(_outputs[^1]);
    }

    private void Reveal(string path)
    {
        try
        {
            Actions.Reveal(path);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            TxtMessage.Text = Say("main.error.folder");
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _closeTimer?.Stop();
        _countdown?.Stop();
        _cts?.Cancel();
        _shareFlow?.Cancel();
        _queue?.Dispose();
        base.OnClosing(e);
    }

    /// <summary>
    /// Kuyruk da ana pencereyle aynı yoldan geçer: sabit çıktı klasörü ve adlandırma deseni
    /// burada da okunur. Eskiden bu pencerenin kendi kopyası vardı; ikisi de kullanıcının
    /// ayarını görmüyordu.
    /// </summary>
    private static AppSettings LoadAppSettings()
    {
        try { return AppSettings.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new AppSettings(); }
    }

    internal static string UniqueOutputPath(string inputPath, AppSettings settings, EncodePlan? plan, double targetMb, string extension = "mp4")
        => ShrinkEngine.UniqueOutputPath(
            inputPath,
            "shrunk",
            extension,
            MainWindow.UsableFixedFolder(settings.OutputFolderMode, settings.OutputFolder),
            AdlandirmaDeseni.Uygula(settings.OutputNamePattern, new AdBilgisi(ShrinkEngine.KaynakAdi(inputPath))
            {
                HedefMb = targetMb,
                Crf = plan?.Crf,
                VideoBitrateK = plan?.VideoBitrateK,
                Yukseklik = plan?.Height,
                Kodek = plan?.Codec
            }));

    /// <summary>
    /// İş bittiğinde yazılan satır. Arayüzden ayrı duruyor ki ölçülebilsin: hedefi aşan
    /// teslimde sapma <see cref="Bicim.Boyut.Sapma"/> ile, pencerenin <b>kendi</b>
    /// dilinin kültüründe yazılıyor. Eskiden burada <c>InvariantCulture</c> vardı ve
    /// Türkçe arayüz sapmayı <c>1.23</c> diye noktayla okuyordu.
    /// </summary>
    internal string BittiSatiri(EncodeResult result, double hedefMb)
        => result.OverTarget
            ? result.OutputPath + " " + Say("main.run.accepted-larger",
                Bicim.Boyut.Sapma(result.OutputMb - hedefMb, Strings.CultureOf(_language)), hedefMb)
            : MainWindow.ShowsSaturated(result)
                ? result.OutputPath + " " + Say("main.run.saturated",
                    Bicim.Boyut.Mb(result.OutputMb, Strings.CultureOf(_language)), Bicim.Boyut.Hedef(hedefMb, Strings.CultureOf(_language)))
                : result.OutputPath;

    /// <summary>
    /// İş düştüğünde yazılan satır. Tavanı aşan teslimde boyut ailenin baskın yazımıyla
    /// (<see cref="Bicim.Boyut.Mb"/>) ve pencerenin dilinin kültüründe yazılıyor.
    /// </summary>
    internal string HataSatiri(EncodeResult result, double hedefMb)
        => result.CeilingExceeded
            ? Say("main.run.over-ceiling", hedefMb, result.Attempts, Bicim.Boyut.Mb(result.OutputMb, Strings.CultureOf(_language)))
            : Say("main.run.ended");

    private string Say(string key)
        => LanguageCatalog.Title(Strings.GetIn(_language, key), _language);

    private string Say(string key, params object?[] args)
        => LanguageCatalog.Title(Strings.GetIn(_language, key, args), _language);
}
