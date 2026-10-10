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
    private readonly JobPump<ShrinkRequest> _pump;
    private readonly List<AktifIs> _aktif = new();
    private readonly SemaphoreSlim _overshootGate = new(1, 1);
    private readonly List<string> _outputs = new();
    private DispatcherTimer? _closeTimer;
    private bool _started;
    private bool _closed;
    private int _accepted;
    private int _finished;
    private int _baslayan;
    private int? _parallelLimit;
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

    private readonly List<BitenIs> _bitenler = new();
    private Func<bool>? _pencereEtkin;

    /// <summary>İş bitti haberinin sistem yüzü; testler sahtesini verir.</summary>
    internal IIsBildirimYuzu Bildirim { get; set; } = IsBittiBildirimi.Varsayilan();

    /// <summary>Pencere şu an kullanıcının önünde mi; testler kendi cevabını verir.</summary>
    internal Func<bool> PencereEtkin
    {
        get => _pencereEtkin ??= () => IsActive;
        set => _pencereEtkin = value;
    }

    /// <summary>
    /// K6: aynı anda koşan iş sınırı. Ayardan okunur ve makinenin üst sınırına çekilir;
    /// testler kendi değerini verir.
    /// </summary>
    internal int ParallelLimit
    {
        get => _parallelLimit ?? ParallelJobs.Clamp(_appSettings.ParallelJobs, Environment.ProcessorCount);
        set => _parallelLimit = Math.Max(ParallelJobs.Default, value);
    }

    /// <summary>Bir isteği koşturan iş; testler sahtesini verir, gerçek ffmpeg çağrılmaz.</summary>
    internal Func<ShrinkRequest, CancellationToken, Task>? Kosturucu { get; set; }

    /// <summary>Şu an koşan istekler, başlama sırasıyla.</summary>
    internal IReadOnlyList<ShrinkRequest> RunningJobs => _pump.Running;

    /// <summary>K6: koşan tek işi iptal eder; ötekiler ve sıra sürer.</summary>
    internal bool CancelJob(ShrinkRequest request) => _pump.Cancel(request);

    /// <summary>Koşan bir işin ekrandaki durumu; satırı bir kez kurulur, ilerleme yerinde yazılır.</summary>
    private sealed class AktifIs
    {
        public AktifIs(ShrinkRequest request) => Request = request;
        public ShrinkRequest Request { get; }
        public string Target { get; set; } = "";
        public double Fraction { get; set; }
        public string Stage { get; set; } = "-";
        public string Remaining { get; set; } = "-";
        public Grid? Row { get; set; }
        public ProgressBar? Bar { get; set; }
        public TextBlock? Kalan { get; set; }
    }

    /// <summary>Bir işin sonucunu sıra sonundaki habere yazar.</summary>
    internal void IsBitti(string yol, IsSonucu sonuc) => _bitenler.Add(new BitenIs(yol, sonuc));

    /// <summary>
    /// Sıra boşaldığında, son haberden beri biten işler için tek haber. Karar
    /// <see cref="IsBittiBildirimi.Karar"/>'da; burası yalnız sonucu sistem yüzüne taşır.
    /// </summary>
    internal void HaberVer()
    {
        var karar = IsBittiBildirimi.Karar(_bitenler.ToList(), PencereEtkin(), _appSettings.NotifyWhenDone,
            (key, args) => string.Format(Strings.CultureOf(_language), Say(key), args));
        _bitenler.Clear();
        if (karar is not null) Bildirim.Bildir(this, karar);
    }

    public ShrinkJobWindow() : this(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null)
    {
    }

    internal ShrinkJobWindow(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _startup = startup;
        _queue = queue;
        _pump = new JobPump<ShrinkRequest>((request, ct) => (Kosturucu ?? RunOneAsync)(request, ct), () => ParallelLimit);
        _pump.Drained += OnDrained;

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
        Classes.Set("untracked", Strings.IsUntrackedLanguage(_language));

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
        BtnOvershootAccept.Click += (_, _) => AnswerOvershoot(true);
        BtnOvershootStop.Click += (_, _) => AnswerOvershoot(false);
        WireWatch();
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

    /// <summary>Bir dosyanın işi bitti: kaynak yolu ve teslim edilen çıktı; başarısız ya da iptal edilen işte çıktı <c>null</c>.</summary>
    internal event Action<string, string?>? JobFinished;

    /// <summary>Bu pencerenin teslim ettigi cikti yollari, kabul sirasiyla.</summary>
    internal IReadOnlyList<string> Outputs => _outputs;

    /// <summary>Kuyruga girmis istek sayisi; ikinci surecin boruyla verdikleri dahil.</summary>
    internal int AcceptedCount => _accepted;

    internal string MessageText => TxtMessage.Text ?? "";

    /// <summary>Henüz başlamamış istekler, koşacakları sırayla. Koşan dosya burada değil.</summary>
    internal IReadOnlyList<ShrinkRequest> Pending => _pump.Pending;

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
        RestoreWatch();

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
        _pump.Enqueue(request);
        RefreshPending();
    }

    /// <summary>C1-2: bekleyen isteği sıradan çıkarır. Koşan dosyaya dokunmaz.</summary>
    internal void RemovePending(int index)
    {
        if (!_pump.RemovePending(index)) return;
        _accepted--;
        RefreshPending();
    }

    /// <summary>C1-2: bekleyen isteği <paramref name="delta"/> kadar kaydırır; sınırın dışına taşımaz.</summary>
    internal void MovePending(int index, int delta)
    {
        if (!_pump.MovePending(index, delta)) return;
        RefreshPending();
    }

    /// <summary>
    /// C1-2: duraklatılan kuyrukta koşan dosyalar biter, sıradaki başlamaz. Sürdürünce pompa yeniden açılır.
    /// </summary>
    internal void SetPaused(bool paused)
    {
        _paused = paused;
        BtnPause.Content = Say(paused ? "main.shrink-job.resume" : "main.shrink-job.pause");
        TxtPaused.IsVisible = paused;
        _pump.Paused = paused;
        RefreshPending();
    }

    private void RefreshPending()
    {
        var pending = _pump.Pending;
        PendingPanel.IsVisible = pending.Count > 0;
        TxtPending.Text = Say("main.shrink-job.pending", pending.Count);
        PendingList.Children.Clear();
        for (var i = 0; i < pending.Count; i++) PendingList.Children.Add(PendingRow(pending, i));
    }

    private Grid PendingRow(IReadOnlyList<ShrinkRequest> pending, int index)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = Token("SpaceXs") };
        var name = new TextBlock
        {
            Text = Path.GetFileName(pending[index].Path),
            FontSize = Token("FontSizeSm"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(name, pending[index].Path);
        row.Children.Add(name);
        AddRowButton(row, 1, "IconChevronUp", "main.shrink-job.move-up", index > 0, () => MovePending(index, -1));
        AddRowButton(row, 2, "IconChevronDown", "main.shrink-job.move-down", index < pending.Count - 1, () => MovePending(index, 1));
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

    /// <summary>
    /// K6: pompanın "boşaldı" olayı. Kaç iş aynı anda koşmuş olursa olsun haber ve kuyruk sonu
    /// eylemi bütün işler bitince bir kez gelir. Kapanan pencere eylem başlatmaz.
    /// </summary>
    private void OnDrained()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnDrained);
            return;
        }
        if (_closed) return;
        HaberVer();
        QueueDrained();
    }

    /// <summary>K6: koşan işin satırı — ad, kalan süre, iptal düğmesi ve altında kendi çubuğu.</summary>
    private Grid ActiveRow(AktifIs job)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = Token("SpaceXs"),
            RowSpacing = Token("SpaceXs")
        };
        var name = new TextBlock
        {
            Text = Path.GetFileName(job.Request.Path),
            FontSize = Token("FontSizeSm"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(name, job.Request.Path);
        row.Children.Add(name);
        job.Kalan = new TextBlock { Text = job.Remaining, FontSize = Token("FontSizeSm"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(job.Kalan, 1);
        row.Children.Add(job.Kalan);
        AddRowButton(row, 2, "IconClose", "main.action.cancel", true, () => CancelJob(job.Request));
        job.Bar = new ProgressBar { Minimum = 0, Maximum = 1, Value = job.Fraction, Height = Token("ProgressBarHeight") };
        Grid.SetRow(job.Bar, 1);
        Grid.SetColumnSpan(job.Bar, 3);
        row.Children.Add(job.Bar);
        return row;
    }

    /// <summary>
    /// Koşan işleri ekrana yazar. Tek iş koşarken pencere bugünkü gibidir; birden çoğunda başlık
    /// sayıyı, çubuk ortalamayı gösterir ve her işin kendi satırı olur. Satırlar yalnız sınır
    /// 1'den büyükken görünür.
    /// </summary>
    private void RefreshActive(bool rebuild)
    {
        var rows = ParallelLimit > ParallelJobs.Default && _aktif.Count > 0;
        ActivePanel.IsVisible = rows;
        if (rebuild)
        {
            ActiveList.Children.Clear();
            if (rows)
                foreach (var job in _aktif) ActiveList.Children.Add(job.Row ??= ActiveRow(job));
        }
        if (_aktif.Count == 0) return;

        foreach (var job in _aktif)
        {
            if (job.Bar is { } bar) bar.Value = job.Fraction;
            if (job.Kalan is { } kalan) kalan.Text = job.Remaining;
        }

        var many = _aktif.Count > 1;
        Progress.IsVisible = true;
        RowFacts.IsVisible = !many;
        if (many)
        {
            TxtHeadline.Text = Say("main.shrink-job.running", _aktif.Count);
            TxtTarget.Text = _aktif[^1].Target;
            Progress.Value = _aktif.Average(job => job.Fraction);
            return;
        }

        var tek = _aktif[0];
        TxtHeadline.Text = Path.GetFileName(tek.Request.Path);
        TxtTarget.Text = tek.Target;
        Progress.Value = tek.Fraction;
        TxtStage.Text = tek.Stage;
        TxtRemaining.Text = tek.Remaining;
    }

    /// <summary>
    /// C1-4: sıra boşaldığında seçilen eylem. Uyut ve kapat geri sayımla gelir ve pencere bu
    /// sırada kapanmaz; klasör açma son çıktının klasörünü açar.
    /// </summary>
    internal void QueueDrained()
    {
        if (_pump.PendingCount > 0 || _paused || _pump.RunningCount > 0) return;
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
        TxtCountdown.Text = Core.Bicim.Satir.Bagla(Say(_whenDone == QueueEndChoice.Sleep ? "main.shrink-job.countdown.sleep" : "main.shrink-job.countdown.power-off", _countdownLeft));
    }

    private async Task RunOneAsync(ShrinkRequest request, CancellationToken ct)
    {
        var job = new AktifIs(request);
        var sira = ++_baslayan;
        _aktif.Add(job);
        State = ShrinkJobState.Kosuyor;
        BtnReveal.IsVisible = false;
        ResetShare(false);
        TxtMessage.Text = "";
        RefreshPending();
        RefreshActive(true);

        var sonuc = IsSonucu.Hatali;
        var son = ShrinkJobState.Hata;
        string? teslim = null;
        string? ayrilan = null;
        string? mesaj = null;
        var uygulamadaAc = false;
        IDisposable? yuva = null;
        try
        {
            var info = await FfprobeClient.ProbeAsync(request.Path, ct);
            var (options, targetMb) = OptionsFor(request, info);
            options = await KirpmaylaAsync(options, info, ct);
            job.Target = Say("main.shrink-job.target", TargetLabel(targetMb), sira, _accepted);
            RefreshActive(false);
            var plan = PlanCalculator.Build(info, options);
            var output = OutputReservations.Shared.Reserve(taken =>
                UniqueOutputPath(request.Path, _appSettings, plan, targetMb, ExtensionFor(plan), taken));
            ayrilan = output;
            QueueWatch.MarkOwn(output);
            yuva = await EncoderSlots.Shared.EnterAsync(plan.Codec, ct);

            var progress = new Progress<EncodeProgress>(step => ShowProgress(job, step));
            var soruAdi = ParallelLimit > ParallelJobs.Default ? Path.GetFileName(request.Path) : null;

            var result = await new EncodeRunner().RunAsync(
                info, plan, output, targetMb, progress, ct, options.FillPolicy,
                askBeforeRetry: (prompt, token) => AskOvershootAsync(soruAdi, prompt, token));

            if (result.Success)
            {
                QueueWatch.MarkOwn(result.OutputPath);
                _outputs.Add(result.OutputPath);
                teslim = result.OutputPath;
                son = ShrinkJobState.Bitti;
                sonuc = IsSonucu.Basarili;
                mesaj = BittiSatiri(result, targetMb, plan);
            }
            else
            {
                mesaj = HataSatiri(result, targetMb, plan);
                uygulamadaAc = true;
            }
        }
        catch (OperationCanceledException)
        {
            sonuc = IsSonucu.Iptal;
            mesaj = Say("main.run.cancelled");
        }
        catch (Exception ex)
        {
            mesaj = ex.Message;
            uygulamadaAc = true;
        }
        finally
        {
            yuva?.Dispose();
            if (ayrilan is not null) OutputReservations.Shared.Release(ayrilan);
            _finished++;
            IsBitti(request.Path, sonuc);
            _aktif.Remove(job);
            RefreshActive(true);
            if (_aktif.Count == 0)
            {
                RowFacts.IsVisible = true;
                TxtHeadline.Text = Path.GetFileName(request.Path);
                if (teslim is not null) Progress.Value = 1;
            }
            State = _aktif.Count > 0 ? ShrinkJobState.Kosuyor : son;
            TxtMessage.Text = mesaj ?? "";
            if (uygulamadaAc) BtnOpenInApp.IsVisible = true;
            if (teslim is not null)
            {
                BtnReveal.IsVisible = true;
                ResetShare(true);
            }
            JobFinished?.Invoke(request.Path, teslim);
        }
    }

    /// <summary>
    /// Motorun ilerleme satırı. Aşama adı motorun İngilizce belirteçleriyle gelir; ana pencereyle
    /// aynı sözlükten ve bu pencerenin dilinde yazılır.
    /// </summary>
    internal void ShowProgress(EncodeProgress step)
    {
        Progress.Value = step.Fraction;
        TxtStage.Text = MainWindow.LocalizeStageIn(step.Stage, _language);
        TxtRemaining.Text = Saat.Kalan(step.Remaining);
    }

    /// <summary>K6: ilerleme işin kendi kaydına yazılır; biten işin geç gelen satırı ekrana dokunmaz.</summary>
    private void ShowProgress(AktifIs job, EncodeProgress step)
    {
        if (!_aktif.Contains(job)) return;
        job.Fraction = step.Fraction;
        job.Stage = MainWindow.LocalizeStageIn(step.Stage, _language);
        job.Remaining = Saat.Kalan(step.Remaining);
        RefreshActive(false);
    }

    internal string StageText => TxtStage.Text ?? "";

    /// <summary>
    /// Kullanıcıya sorulmadan verilebilecek cevap. Deneme hakkı varken motor ana pencerede de
    /// yeniden dener; hedefin altında kalmış bir sonuç varsa "bırak" onu teslim eder. Geriye
    /// yalnız hedefi aşan dosyayı teslim etmek kalıyorsa <c>null</c>: o karar kullanıcının.
    /// </summary>
    internal static OvershootChoice? UnaskedChoice(RetryPrompt prompt)
        => prompt.CanRetry ? OvershootChoice.Retry
            : prompt.HasUnderBandFallback ? OvershootChoice.Leave
            : null;

    private TaskCompletionSource<OvershootChoice>? _overshootDecision;

    internal bool OvershootAsked => OvershootPanel.IsVisible;

    /// <summary>
    /// Ana penceredeki aşım sorusunun bu penceredeki karşılığı. Eskiden motor burada sorusuz
    /// koşuyordu ve hedefi aşan en küçük dosyayı teslim ediyordu; "hedeften büyük dosya asla
    /// verilmez" diyen satırla çelişiyordu.
    /// </summary>
    internal Task<OvershootChoice> AskOvershootAsync(RetryPrompt prompt, CancellationToken ct)
        => AskOvershootAsync(null, prompt, ct);

    /// <summary>
    /// K6: aynı anda iki iş sorabilir; panel tek, bu yüzden sorular sıraya girer ve her biri
    /// <paramref name="dosya"/> verilmişse hangi dosyayı sorduğunu ilk satırda söyler.
    /// </summary>
    internal async Task<OvershootChoice> AskOvershootAsync(string? dosya, RetryPrompt prompt, CancellationToken ct)
    {
        if (UnaskedChoice(prompt) is { } unasked) return unasked;

        await _overshootGate.WaitAsync(ct);
        try
        {
            var decision = new TaskCompletionSource<OvershootChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
            _overshootDecision = decision;
            await Dispatcher.UIThread.InvokeAsync(() => ShowOvershoot(prompt, dosya));
            using var cancellation = ct.Register(() => decision.TrySetCanceled(ct));
            try
            {
                return await decision.Task;
            }
            finally
            {
                _overshootDecision = null;
                await Dispatcher.UIThread.InvokeAsync(HideOvershoot);
            }
        }
        finally
        {
            _overshootGate.Release();
        }
    }

    private void ShowOvershoot(RetryPrompt prompt, string? dosya)
    {
        var kultur = Strings.CultureOf(_language);
        TxtOvershoot.Text = (dosya is null ? "" : dosya + Environment.NewLine) + Say("main.retry.outcome",
            prompt.Attempt,
            prompt.MaxAttempts,
            Bicim.Boyut.Mb(prompt.ActualMb, kultur),
            Bicim.Boyut.Hedef(prompt.TargetMb, kultur),
            Bicim.Boyut.Mb(prompt.OverMb, kultur),
            Bicim.Yuzde.Hazir(prompt.OverPercent, kultur),
            Saat.Ekran(prompt.AttemptDuration));
        TxtOvershootMeaning.Text = Say("main.retry.meaning-without-fallback");
        var kabul = Say("main.retry.accept", Bicim.Boyut.Mb(prompt.ActualMb, kultur));
        BtnOvershootAccept.Content = kabul;
        AutomationProperties.SetName(BtnOvershootAccept, kabul);
        TxtStage.Text = Say("main.output.waiting");
        OvershootPanel.IsVisible = true;
    }

    private void HideOvershoot() => OvershootPanel.IsVisible = false;

    internal void AnswerOvershoot(bool accept)
        => _overshootDecision?.TrySetResult(accept ? OvershootChoice.AcceptLarger : OvershootChoice.Leave);

    private void ArmClose()
    {
        if (_closeTimer is not null) return;
        _closeTimer = new DispatcherTimer { Interval = Linger };
        _closeTimer.Tick += (_, _) =>
        {
            if ((_shareFlow?.Running ?? false) || _countdownLeft is not null || Watching) return;
            _closeTimer?.Stop();
            if (_pump.PendingCount == 0 && _pump.RunningCount == 0) Close();
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
        StopWatch();
        _closed = true;
        _pump.CancelAll();
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

    internal static string UniqueOutputPath(string inputPath, AppSettings settings, EncodePlan? plan, double targetMb, string extension = "mp4",
        Func<string, bool>? taken = null)
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
            }),
            taken);

    /// <summary>
    /// İş bittiğinde yazılan satır. Arayüzden ayrı duruyor ki ölçülebilsin: hedefi aşan
    /// teslimde sapma <see cref="Bicim.Boyut.Sapma"/> ile, pencerenin <b>kendi</b>
    /// dilinin kültüründe yazılıyor. Eskiden burada <c>InvariantCulture</c> vardı ve
    /// Türkçe arayüz sapmayı <c>1.23</c> diye noktayla okuyordu. Aşım, motorun "hedefin
    /// üzerinde" dediği etkin hedefe göre (<see cref="EncodeRunner.EffectiveTargetMb"/>).
    /// </summary>
    internal string BittiSatiri(EncodeResult result, double hedefMb, EncodePlan plan)
    {
        var kultur = Strings.CultureOf(_language);
        if (result.OverTarget)
        {
            var etkinMb = EncodeRunner.EffectiveTargetMb(hedefMb, plan);
            return result.OutputPath + " " + Say("main.run.accepted-larger",
                Bicim.Boyut.Sapma(result.OutputMb - etkinMb, kultur), Bicim.Boyut.Hedef(etkinMb, kultur));
        }
        return MainWindow.ShowsSaturated(result)
            ? result.OutputPath + " " + Say("main.run.saturated", Bicim.Boyut.Mb(result.OutputMb, kultur), Bicim.Boyut.Hedef(hedefMb, kultur))
            : result.OutputPath;
    }

    /// <summary>
    /// İş düştüğünde yazılan satır. Boyutlar ailenin yazımıyla (<see cref="Bicim.Boyut"/>) ve
    /// pencerenin dilinin kültüründe; hedef, motorun altına inemediği etkin hedef.
    /// </summary>
    internal string HataSatiri(EncodeResult result, double hedefMb, EncodePlan plan)
    {
        var kultur = Strings.CultureOf(_language);
        return result.CeilingExceeded
            ? Say("main.run.over-ceiling", Bicim.Boyut.Hedef(EncodeRunner.EffectiveTargetMb(hedefMb, plan), kultur), result.Attempts, Bicim.Boyut.Mb(result.OutputMb, kultur))
            : Say("main.run.ended");
    }

    private string Say(string key)
        => LanguageCatalog.Title(Strings.GetIn(_language, key), _language);

    private string Say(string key, params object?[] args)
        => LanguageCatalog.Title(Strings.GetIn(_language, key, args), _language);
}
