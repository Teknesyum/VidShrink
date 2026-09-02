using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.App.Performance;
using VidShrink.App.Playback;
using VidShrink.Core;
using CoreShare = VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;

namespace VidShrink.App;

public enum DropVisual { Idle, Accept, Reject }

public partial class MainWindow : Window
{
    private const double WhatsAppTargetMb = 16;
    private const double MacTrafficLightInset = 80;
    private const int CalibrationRounds = 2;
    private const uint ClientAreaAnimationQuery = 0x1042;

    // Ayar kapalıyken açılışta bir kez sorulur. Ağ yoksa bekleyen tek şey bu arka plan işi;
    // pencere zaten açılmış olur.
    private static readonly TimeSpan UpdateProbeTimeout = TimeSpan.FromSeconds(8);

    // Şeridin kapatıldığı sürüm. Ayar dosyası değil, yanına konan bir
    // işaret dosyası; UpdateSettings'e ait olduğu için oraya yazılmaz.
    private const string DismissedNoticeFileName = "dismissed-update.txt";

    private static readonly ConversionPlan ConversionDefaults = new();

    private static readonly MediaInfo HardwareProbeSource = new()
    {
        FilePath = "hardware-probe.mp4",
        FileSizeBytes = 200L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 14_000_000
    };

    private MediaInfo? _info;
    private EncodePlan? _autoPlan;
    private EncodePlan? _aiPlan;
    private CancellationTokenSource? _cts;
    private ComplexityProfile? _profile;
    private SceneMapAttempt? _sceneMap;
    private CancellationTokenSource? _probeCts;
    private SizeEstimate? _estimate;
    private IEncoderAvailability? _encoders;
    private DeferredEncoderAvailability? _planEncoders;
    private bool _probeStatusShown;
    private double _predictedQuality;
    private StrategyAdvice? _advice;
    private string? _lastOutput;
    private string? _ffmpegVersion;
    private bool _syncing;

    // T61/K1: iki denetim birbirini sürüyor. Bayrak "bu değeri kullanıcı değil program
    // yazıyor" demektir; yazılan tarafın işleyicisi o turda hiçbir şey türetmez, böylece
    // döngü tek turda kapanır. İki bayrak var çünkü iki yön ayrı ayrı bastırılıyor.
    private bool _targetIsDerived;
    private bool _qualityIsDerived;
    private TaskCompletionSource<bool>? _retryDecision;
    private RetryPrompt? _activeRetryPrompt;
    private bool _hardwareProbed;
    private bool _hardwareEncoderAvailable;
    private HardwareVerdict _hardwareVerdict = HardwareVerdict.NotProbed;
    private bool _motionReduced;
    private DropVisual _dropVisual = DropVisual.Idle;
    private DispatcherTimer? _recalculateTimer;
    private DateTime _lastEstimatePulse = DateTime.MinValue;
    private bool _controlsReady;
    private bool _updateUiSyncing;
    private bool _settingsSyncing;
    private string? _noticeVersion;
    private AppliedUpdateNotice? _appliedNotice;
    private ShareTargetTable _shareTargets = ShareTargetTable.Fallback;
    private CoreShare.ShareTargetTable? _shareEndpoints;
    private CoreShare.IHttpTransport? _shareTransport;
    private ShareFlow? _shareFlow;
    private PanelHost? _preview;

    private readonly string? _startupFile;

    private EncodePlan? ActivePlan => _aiPlan ?? _autoPlan;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? startupFile)
    {
        _startupFile = startupFile;
        InitializeComponent();
        _controlsReady = true;

        // T43: panel ana pencereye burada bağlanıyor. Kaynağı üreten çağrı tek yerde durur;
        // panel hangi motorun kare ürettiğini bilmez.
        _preview = new PanelHost(Preview, () => new PipeComparisonFrameSource());

        BuildLanguageSwitch();
        Strings.Changed += OnLanguageChanged;

        ShowScrollOnlyOnHover(TxtCommand, TxtAiJson, TxtConvertCommand);

        _motionReduced = !AnimationsAllowed();
        if (_motionReduced) Classes.Add("reduced-motion");
        ApplyStartupSize();
        PreparePanelEntrance();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        TitleBar.PointerPressed += OnTitleBarPointerPressed;
        LoadTitleBarLogo();

        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
            WindowButtons.IsVisible = false;
            TitleBarContent.Margin = new Thickness(MacTrafficLightInset, 0, TitleBarContent.Margin.Right, 0);
        }

        Watch(SliderTarget, RangeBase.ValueProperty, OnTargetSliderChanged);
        Watch(TxtTarget, TextBox.TextProperty, OnTargetTextChanged);
        Watch(SliderQualityTarget, RangeBase.ValueProperty, OnQualityTargetSliderChanged);
        Watch(TxtQualityTarget, TextBox.TextProperty, OnQualityTargetTextChanged);
        foreach (var box in new[] { CmbIntent, CmbCodec, CmbFillPolicy, CmbHdrPolicy })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnOptionChanged);
        foreach (var check in new[] { ChkResolution, ChkFps, ChkFastGpu })
            Watch(check, ToggleButton.IsCheckedProperty, OnOptionChanged);
        Watch(ChkFastGpu, ToggleButton.IsCheckedProperty, OnFastGpuChanged);

        Watch(SliderQuality, RangeBase.ValueProperty, OnQualitySliderChanged);
        Watch(TxtQuality, TextBox.TextProperty, OnQualityTextChanged);
        foreach (var box in new[] { CmbQualityMode, CmbConvertCodec })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnQualityRangeChanged);
        foreach (var box in new[] { CmbContainer, CmbResolution, CmbConvertFps, CmbConvertAudio })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnConvertChanged);
        foreach (var field in new[] { TxtCustomResolution, TxtCustomFps, TxtAudioBitrate, TxtTrimStart, TxtTrimEnd })
            Watch(field, TextBox.TextProperty, OnConvertChanged);

        Watch(ChkAutoUpdate, ToggleButton.IsCheckedProperty, OnAutoUpdateChanged);
        Watch(CmbShareTarget, SelectingItemsControl.SelectedIndexProperty, OnShareTargetChanged);
        Watch(CmbShareRetention, SelectingItemsControl.SelectedIndexProperty, SaveSettings);
        foreach (var control in new SelectingItemsControl[]
                 {
                     CmbIntent, CmbCodec, CmbFillPolicy, CmbHdrPolicy, CmbQualityMode,
                     CmbContainer, CmbConvertCodec, CmbResolution, CmbConvertFps, CmbConvertAudio
                 })
            Watch(control, SelectingItemsControl.SelectedIndexProperty, SaveSettings);
        foreach (var control in new ToggleButton[] { ChkResolution, ChkFps })
            Watch(control, ToggleButton.IsCheckedProperty, SaveSettings);
        foreach (var control in new TextBox[]
                 {
                     TxtTarget, TxtQualityTarget, TxtQuality, TxtCustomResolution, TxtCustomFps,
                     TxtAudioBitrate, TxtTrimStart, TxtTrimEnd
                 })
            Watch(control, TextBox.TextProperty, SaveSettings);

        RefreshQualityTargetAvailability();
        // Sınır cümlesi ölçüm koşmadan da ekranda durur; sonda burada çağrılmıyor.
        ShowPerformanceResult(PerformanceCheckResult.NotMeasured);
        Loaded += OnWindowLoaded;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint action, uint param, ref int value, uint update);

    private static bool AnimationsAllowed()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            var enabled = 1;
            return !SystemParametersInfoW(ClientAreaAnimationQuery, 0, ref enabled, 0) || enabled != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private TimeSpan Motion(string key, double fallbackMs)
        => this.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.FromMilliseconds(fallbackMs);

    private double Scalar(string key, double fallback)
        => this.TryFindResource(key, out var value) && value is double number ? number : fallback;

    private IBrush? Paint(string key)
        => this.TryFindResource(key, out var value) ? value as IBrush : null;

    private ControlTheme? Look(string key)
        => this.TryFindResource(key, out var value) ? value as ControlTheme : null;

    private void ApplyStartupSize()
    {
        try
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen is null) return;

            var scaling = screen.Scaling <= 0 ? 1 : screen.Scaling;
            var share = Scalar("WindowWorkAreaShare", 0.9);
            var roomWidth = screen.WorkingArea.Width / scaling * share;
            var roomHeight = screen.WorkingArea.Height / scaling * share;

            Width = Math.Max(MinWidth, Math.Min(Scalar("WindowPreferredWidth", Width), roomWidth));
            Height = Math.Max(MinHeight, Math.Min(Scalar("WindowPreferredHeight", Height), roomHeight));
        }
        catch (Exception)
        {
            // the declared Width/Height in markup stay in force
        }
    }

    private Control[] EntrancePanels() => new Control[] { SourcePanel, TargetPanel, PlanPanel, OutputPanel, AiPanel };

    private void PreparePanelEntrance()
    {
        var entering = _motionReduced ? "enter-flat" : "enter";
        foreach (var panel in EntrancePanels()) panel.Classes.Add(entering);
    }

    private void PlayPanelEntrance()
    {
        var entering = _motionReduced ? "enter-flat" : "enter";
        var step = Scalar("MotionStaggerMs", 40);
        var panels = EntrancePanels();
        for (var index = 0; index < panels.Length; index++)
        {
            var panel = panels[index];
            if (index == 0) panel.Classes.Remove(entering);
            else DispatcherTimer.RunOnce(() => panel.Classes.Remove(entering), TimeSpan.FromMilliseconds(step * index));
        }
    }

    private readonly Dictionary<Control, int> _fadeGeneration = new();
    private readonly HashSet<Control> _fadingOut = new();

    /// <summary>
    /// Uygulama açılışta Türkçe koşuyor (<c>OnWindowLoaded</c>). Ölçüm başsız koştuğu için
    /// o olay ateşlenmiyor ve pencere İngilizce ölçülüyordu; Türkçe karşılıkların
    /// kutulara sığıp sığmadığı bu kapıdan geçmeden ölçülemez.
    /// </summary>
    internal void UseTurkish() => UseLanguage("tr");

    internal void SettleFades()
    {
        foreach (var control in _fadingOut) control.IsVisible = false;
        _fadingOut.Clear();
    }

    private void ShowScrollOnlyOnHover(params TextBox[] boxes)
    {
        foreach (var box in boxes)
        {
            ApplyScrollAffordance(box, false);
            box.PointerEntered += (_, _) => ApplyScrollAffordance(box, true);
            box.PointerExited += (_, _) => ApplyScrollAffordance(box, false);
        }
    }

    private static void ApplyScrollAffordance(TextBox box, bool hovered)
    {
        var shown = hovered ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
        ScrollViewer.SetVerticalScrollBarVisibility(box, shown);
        ScrollViewer.SetHorizontalScrollBarVisibility(
            box,
            box.TextWrapping == TextWrapping.Wrap ? ScrollBarVisibility.Disabled : shown);
    }


    private void EnsureFade(Control control)
    {
        if (control.Transitions is not null) return;
        control.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = Motion("MotionBase", 240), Easing = new CubicEaseOut() }
        };
    }

    private void Fade(Control control, bool visible)
    {
        EnsureFade(control);
        if (visible)
        {
            _fadeGeneration[control] = NextFadeGeneration(control);
            _fadingOut.Remove(control);
            if (control.IsVisible && control.Opacity > 0.99) return;
            control.Opacity = 0;
            control.IsVisible = true;
            Dispatcher.UIThread.Post(() => control.Opacity = 1, DispatcherPriority.Loaded);
            return;
        }

        if (!control.IsVisible) return;
        var generation = _fadeGeneration[control] = NextFadeGeneration(control);
        _fadingOut.Add(control);
        control.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (_fadeGeneration.TryGetValue(control, out var current) && current != generation) return;
            _fadingOut.Remove(control);
            control.IsVisible = false;
        }, Motion("MotionBase", 240));
    }

    private int NextFadeGeneration(Control control)
        => _fadeGeneration.TryGetValue(control, out var current) ? current + 1 : 1;

    private void Pulse(Control control, bool throttled)
    {
        if (_motionReduced) return;
        var now = DateTime.UtcNow;
        if (throttled && now - _lastEstimatePulse < Motion("MotionSlow", 360) + Motion("MotionBase", 240)) return;
        _lastEstimatePulse = now;
        control.Opacity = 0.35;
        DispatcherTimer.RunOnce(() => control.Opacity = 1, Motion("MotionFast", 160));
    }

    private void SetStage(TextBlock target, string text)
    {
        if (target.Text == text) return;
        target.Text = text;
        Pulse(target, false);
    }

    private void ScheduleRecalculate()
    {
        _recalculateTimer ??= new DispatcherTimer { Interval = Motion("MotionFast", 160) };
        _recalculateTimer.Stop();
        _recalculateTimer.Tick -= OnRecalculateTick;
        _recalculateTimer.Tick += OnRecalculateTick;
        _recalculateTimer.Start();
    }

    private void OnRecalculateTick(object? sender, EventArgs e)
    {
        _recalculateTimer?.Stop();
        Recalculate();
    }

    private void LoadTitleBarLogo()
    {
        try
        {
            if (!this.TryFindResource("AppIconUri", out var uri) || uri is not string source) return;
            using var stream = AssetLoader.Open(new Uri(source));
            AppLogo.Source = new Bitmap(stream);
            AppLogo.IsVisible = true;
        }
        catch (Exception)
        {
            AppLogo.IsVisible = false;
        }
    }

    private static void Watch(AvaloniaObject target, AvaloniaProperty property, Action handler)
        => target.PropertyChanged += (_, args) => { if (args.Property == property) handler(); };

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            UpdateMaximizeGlyph();
            ApplyWindowFrame();
            WindowShell.Margin = OffScreenMargin;
            var settings = UpdateSettings.Load(SettingsPathOverride);
            _settingsSyncing = true;
            UseLanguage(ResolveLanguage(settings.Language, CultureInfo.CurrentUICulture.Name));
            InitializeShareUi();
            RestoreSettings(settings);
            InitializeUpdateUi(settings);
            _ = CheckForUpdateAsync();
            PlayPanelEntrance();
            await LoadStartupFileAsync();
            await LoadFfmpegVersionAsync();
            await ProbeHardwareEncodersAsync();
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.startup")}: {ex.Message}");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_controlsReady) return;
        if (change.Property == WindowStateProperty) { UpdateMaximizeGlyph(); ApplyWindowFrame(); }
        else if (change.Property == OffScreenMarginProperty) WindowShell.Margin = OffScreenMargin;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        _probeCts?.Cancel();
        _cts?.Cancel();
        // Kaynak burada kapanır: pencere kapanırken öksüz ffmpeg kalmaz.
        _preview?.Dispose();
        base.OnClosing(e);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            e.Handled = true;
            return;
        }
        BeginMoveDrag(e);
    }

    /// <summary>
    /// Tam ekranda kabuk kenarlığını ve köşe yuvarlamasını kaldırır. Windows kendi pencerelerinde
    /// de böyle yapar ve sebebi süs değil: 1 piksellik kenarlık kalırsa ekranın sağ üst pikseli
    /// kapatma düğmesinin değil kenarlığın üstüne düşer, köşenin kolay hedef olma kazancı gider.
    /// Normal boyutta kenarlık ve yuvarlama belirteçlerden geri gelir.
    /// </summary>
    private void ApplyWindowFrame()
    {
        var maximized = WindowState == WindowState.Maximized;
        WindowShell.BorderThickness = maximized
            ? new Thickness(0)
            : this.TryFindResource("BorderThin", out var border) && border is Thickness thickness
                ? thickness
                : new Thickness(1);
        WindowShell.CornerRadius = maximized
            ? new CornerRadius(0)
            : this.TryFindResource("RadiusControl", out var radius) && radius is CornerRadius corner
                ? corner
                : default;
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeRestore(object? sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
    private void ToggleMaximizeRestore() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void UpdateMaximizeGlyph() => BtnMaximize.Content = WindowState == WindowState.Maximized ? "❐" : "□";

    private void OnOpenGitHub(object? sender, RoutedEventArgs e) => OpenExternal(this.TryFindResource("LinkGitHub", out var url) ? url as string : null);
    private void OnOpenSponsor(object? sender, RoutedEventArgs e) => OpenExternal(this.TryFindResource("LinkSponsor", out var url) ? url as string : null);

    private void OpenExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { TxtSystemStatus.Text = $"{Say("main.error.link")}: {ex.Message}"; }
    }

    /// <summary>Yürürlükteki dilin Türkçe olup olmadığı; yalnız büyük harf kuralı için.</summary>
    private static bool IsTurkish
        => Strings.Language.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Koddan yazılan her metnin geçtiği kapı: karşılık sözlükten anahtarla okunur, sonra
    /// yürürlükteki dilin büyük harf kuralından geçer. Biçimlemedeki <c>{loc:Text}</c> bağı
    /// da tam olarak aynı iki adımı uyguluyor, böylece iki yol aynı metni veriyor.
    /// </summary>
    private static string Say(string key) => LanguageCatalog.Display(Strings.Get(key));

    private static string Say(string key, params object?[] args)
        => LanguageCatalog.Display(Strings.Get(key, args));

    /// <summary>
    /// Aynı geçit, ama dili çağıran seçiyor. Ölçüler iki dilin satırını yan yana koyabilsin
    /// diye var; arayüz her zaman yürürlükteki dili geçiriyor.
    /// </summary>
    /// <summary>Sayı biçimlemesi dile göre kaymasın diye tek yerden geçiyor.</summary>
    private static string Num(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Speak(string language, string key, params object?[] args)
        => LanguageCatalog.Title(
            Strings.GetIn(language, key, args),
            language.StartsWith("tr", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Dil düğmeleri <c>Locales</c> altındaki klasörlerden kuruluyor; kodda hiçbir dil adı
    /// yazılı değil. Üçüncü bir dil klasörü kopyalandığında düğmesi kendiliğinden belirir
    /// ve her dil kendi adını kendi dosyasında taşır.
    /// </summary>
    private void BuildLanguageSwitch()
    {
        LangSwitch.Children.Clear();

        foreach (var language in Strings.Languages)
        {
            var button = new Button
            {
                Content = Strings.GetIn(language, "main.language.name"),
                Theme = Look("LanguageButton"),
                Tag = language
            };

            AutomationProperties.SetName(button, Strings.GetIn(language, "main.language.name"));
            button.Click += (_, _) => UseLanguage(language);
            LangSwitch.Children.Add(button);
        }

        MarkChosenLanguage();
    }

    /// <summary>
    /// Açılır listede <b>seçili</b> satırın metni bir kopyadır: Avalonia onu seçim anında
    /// alıp saklıyor, öğenin bağı sonradan tazelenince kopya eskide kalıyor. Seçim aynı
    /// yere geri konarak kopya yenileniyor; olay işleyicileri bu sırada susturuluyor.
    /// </summary>
    private void RefreshChoiceLabels()
    {
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            foreach (var box in this.GetVisualDescendants().OfType<ComboBox>())
            {
                var chosen = box.SelectedIndex;
                if (chosen < 0) continue;
                box.SelectedIndex = -1;
                box.SelectedIndex = chosen;
            }
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }

    private void MarkChosenLanguage()
    {
        foreach (var button in LangSwitch.Children.OfType<Button>())
            button.Classes.Set(
                "selected",
                string.Equals(button.Tag as string, Strings.Language, StringComparison.OrdinalIgnoreCase));
    }

    private void UseLanguage(string language) => Strings.Use(language);

    /// <summary>
    /// Dil değişti. Biçimlemeden gelen metni bağlar kendisi tazeliyor; burada yalnız koddan
    /// yazılan metinler yeniden kuruluyor.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Dil, arayüz iş parçacığının dışından da değiştirilebiliyor (ölçümler böyle
        // yapıyor). Görsel ağaca oradan dokunmak yasak; iş kuyruğa alınır.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnLanguageChanged(sender, e));
            return;
        }

        MarkChosenLanguage();
        RefreshChoiceLabels();
        ApplyFastGpuTip();
        _preview?.SetLanguage(IsTurkish);
        if (_activeRetryPrompt is { } pendingPrompt) ShowRetryAsk(pendingPrompt);
        RefreshUpdateTexts();
        RefreshSettingsTexts();
        RefreshShareTarget();
        UpdateToolStatus();
        if (!_performanceRunning) ShowPerformanceResult(_performanceShown);
        ApplyDropText();
        if (_info is not null) { ShowInfo(_info); Recalculate(); RefreshConversion(); }
        else RefreshQualityPanels();
        RefreshQualityTargetAvailability();
        SaveSettings();
    }

    private void RefreshSettingsTexts()
    {
        BtnResetSettings.Content = Strings.Get("settings.reset-all");
        TxtResetSettingsConfirm.Text = Strings.Get("settings.reset-confirm");
        BtnConfirmResetSettings.Content = Strings.Get("settings.reset-confirm-button");
        BtnCancelResetSettings.Content = Strings.Get("settings.reset-cancel");
    }

    internal static string ResolveLanguage(string? saved, string? operatingSystem)
    {
        var known = Strings.Languages;
        var stored = known.FirstOrDefault(language => string.Equals(language, saved, StringComparison.OrdinalIgnoreCase));
        if (stored is not null) return stored;
        var system = known.FirstOrDefault(language => operatingSystem?.StartsWith(language, StringComparison.OrdinalIgnoreCase) == true);
        return system ?? Strings.FallbackLanguage;
    }

    /// <summary>
    /// K6: madde işaretini gövde metninden ayırır. Yuvarlak işaret neon mavisi bir koşu olur,
    /// cümle gövde renginde kalır; göz maddenin nerede başladığını sarma satırından ayırt eder.
    /// Düz metin <see cref="StyledElement.Tag"/> içinde saklanır, çünkü koşulardan kurulmuş bir
    /// <see cref="TextBlock"/> artık <c>Text</c> üzerinden okunup yazılamaz ve dil geçidi ile
    /// büyük harf geçidi kaynak metni oradan alır.
    /// </summary>
    public static void PaintBullets(TextBlock block, string plain)
    {
        block.Tag = plain;
        if (block.Inlines is not { } inlines) { block.Text = plain; return; }

        inlines.Clear();
        var bullet = block.TryFindResource("NeonBlue", out var value) ? value as IBrush : null;
        var lines = plain.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0) inlines.Add(new LineBreak());
            var line = lines[index];
            if (line.StartsWith("• ", StringComparison.Ordinal))
            {
                inlines.Add(new Run("• ") { Foreground = bullet });
                line = line[2..];
            }
            inlines.Add(new Run(line));
        }
    }


    private void ApplyFastGpuTip()
    {
        // Text yazmak koşuları silerdi: ipucu gövdesi aynı boyayıcıdan geçmeli.
        var body = Say(_hardwareProbed && !_hardwareEncoderAvailable ? "main.fast-gpu.tip-missing" : "main.fast-gpu.tip");
        var verdict = FastGpuVerdictLine(_hardwareVerdict, ChkFastGpu.IsChecked == true, Strings.Language);
        PaintBullets(TipFastGpu, verdict is null ? body : $"{body}\n{verdict}");
    }

    /// <summary>
    /// Kutunun neden açıldığı ya da neden kapalı kaldığı. Ayrı bir pencere açılmaz; ölçüm
    /// ipucunun son satırı olarak durur.
    ///
    /// Kutunun gerçek durumu <paramref name="fastGpuOn"/> ile geliyor: ölçüm kapalı
    /// önerdiği hâlde kullanıcı kutuyu elle açmış olabilir ve satır o zaman "kapalı kaldı"
    /// diyemez.
    /// </summary>
    internal static string? FastGpuVerdictLine(HardwareVerdict verdict, bool fastGpuOn, string language)
    {
        string Line(string key, params object?[] args) => Speak(language, key, args);

        var v = verdict;

        // Gövdenin kendisi zaten donanım bulunamadığını yazıyor.
        if (v.Reason is HardwareVerdictReason.NotProbed or HardwareVerdictReason.NoHardwareEncoder)
            return null;

        if (v.Reason == HardwareVerdictReason.Usable)
            return fastGpuOn
                ? Line("main.fast-gpu.on-usable", v.Codec, v.ElapsedMs, v.RequestedBitrateK, v.UsableBitrateK)
                : Line("main.fast-gpu.off-usable", v.Codec);

        var measurement = v.Reason switch
        {
            HardwareVerdictReason.ProbeFailed => Line("main.fast-gpu.probe-failed", v.Codec),
            HardwareVerdictReason.ProbeSlow => Line("main.fast-gpu.probe-slow", v.Codec, v.ElapsedMs, HardwareVerdict.ProbeBudgetMs),
            _ => Line("main.fast-gpu.bitrate-floor", v.Codec, v.UsableBitrateK, v.RequestedBitrateK)
        };

        return fastGpuOn
            ? Line("main.fast-gpu.on-against-advice", measurement)
            : Line("main.fast-gpu.off-as-advised", measurement);
    }

    private async Task LoadFfmpegVersionAsync()
    {
        if (_ffmpegVersion is not null) return;
        try { _ffmpegVersion = await Task.Run(ToolLocator.GetFfmpegVersion); }
        catch (Exception ex) { _ffmpegVersion = $"{Say("main.about.unavailable")} ({ex.Message})"; }
        UpdateToolStatus();
    }

    private void UpdateToolStatus()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            TxtSystemStatus.Text = Say("main.about.tool-missing", missing);
            return;
        }

        TxtSystemStatus.Text = string.Join("\n",
            $"FFmpeg: {ToolLocator.Ffmpeg}",
            $"{Say("main.about.version")}: {_ffmpegVersion ?? Say("main.about.reading")}",
            $".NET: {Environment.Version}",
            $"VidShrink: {AppVersion()}");
    }

    /// <summary>
    /// Uygulamanın kendi sürümü. Hakkında kutusu da bildirim şeridi de burayı okur;
    /// AssemblyInformationalVersion'a giden ikinci bir yol açılmaz.
    /// </summary>
    private static string AppVersion() => DisplayVersion(UpdateCheck.CurrentVersion(Assembly.GetExecutingAssembly()));

    internal static string DisplayVersion(string informationalVersion)
    {
        var build = informationalVersion.IndexOf('+');
        return build < 0 ? informationalVersion : informationalVersion[..build];
    }

    private void RestoreSettings(UpdateSettings settings)
    {
        _settingsSyncing = _syncing = _updateUiSyncing = true;
        try
        {
            TxtTarget.Text = settings.TargetMb.ToString("0.##", CultureInfo.InvariantCulture);
            TxtQualityTarget.Text = settings.QualityTarget.ToString("0.##", CultureInfo.InvariantCulture);
            CmbIntent.SelectedIndex = settings.Intent;
            CmbCodec.SelectedIndex = settings.Codec;
            ChkResolution.IsChecked = settings.MayLowerResolution;
            ChkFps.IsChecked = settings.MayLowerFps;
            ChkFastGpu.IsChecked = settings.FastGpu ?? false;
            CmbFillPolicy.SelectedIndex = settings.FillPolicy;
            CmbHdrPolicy.SelectedIndex = settings.HdrPolicy;
            CmbQualityMode.SelectedIndex = settings.QualityMode;
            TxtQuality.Text = settings.QualityValue.ToString(CultureInfo.InvariantCulture);
            CmbContainer.SelectedIndex = settings.Container;
            CmbConvertCodec.SelectedIndex = settings.ConvertCodec;
            CmbResolution.SelectedIndex = settings.Resolution;
            TxtCustomResolution.Text = settings.CustomResolution;
            CmbConvertFps.SelectedIndex = settings.ConvertFps;
            TxtCustomFps.Text = settings.CustomFps;
            CmbConvertAudio.SelectedIndex = settings.ConvertAudio;
            TxtAudioBitrate.Text = settings.AudioBitrate;
            TxtTrimStart.Text = settings.TrimStart;
            TxtTrimEnd.Text = settings.TrimEnd;
            CmbShareTarget.SelectedIndex = settings.ShareTarget;
            RefreshShareTarget();
            if (CmbShareRetention.ItemCount > 0)
                CmbShareRetention.SelectedIndex = Math.Clamp(settings.ShareRetention, 0, CmbShareRetention.ItemCount - 1);
            ChkAutoUpdate.IsChecked = settings.AutoUpdate;
        }
        finally
        {
            _updateUiSyncing = _syncing = _settingsSyncing = false;
        }
    }

    private UpdateSettings CaptureSettings() => new()
    {
        Language = Strings.Language,
        AutoUpdate = ChkAutoUpdate.IsChecked == true,
        FastGpu = ChkFastGpu.IsChecked == true,
        TargetMb = ParseTargetMb(),
        QualityTarget = ParseQualityTarget(),
        Intent = CmbIntent.SelectedIndex,
        Codec = CmbCodec.SelectedIndex,
        MayLowerResolution = ChkResolution.IsChecked == true,
        MayLowerFps = ChkFps.IsChecked == true,
        FillPolicy = CmbFillPolicy.SelectedIndex,
        HdrPolicy = CmbHdrPolicy.SelectedIndex,
        QualityMode = CmbQualityMode.SelectedIndex,
        QualityValue = int.TryParse(TxtQuality.Text, out var quality) ? quality : 23,
        Container = CmbContainer.SelectedIndex,
        ConvertCodec = CmbConvertCodec.SelectedIndex,
        Resolution = CmbResolution.SelectedIndex,
        CustomResolution = TxtCustomResolution.Text ?? "",
        ConvertFps = CmbConvertFps.SelectedIndex,
        CustomFps = TxtCustomFps.Text ?? "",
        ConvertAudio = CmbConvertAudio.SelectedIndex,
        AudioBitrate = TxtAudioBitrate.Text ?? "",
        TrimStart = TxtTrimStart.Text ?? "",
        TrimEnd = TxtTrimEnd.Text ?? "",
        ShareTarget = CmbShareTarget.SelectedIndex,
        ShareRetention = CmbShareRetention.SelectedIndex
    };

    private void SaveSettings()
    {
        if (_settingsSyncing || _syncing) return;
        try { CaptureSettings().Save(SettingsPathOverride); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("settings.error.save")}: {ex.Message}";
        }
    }

    private void OnResetSettings(object? sender, RoutedEventArgs e)
        => ResetSettingsConfirm.IsVisible = true;

    private void OnCancelResetSettings(object? sender, RoutedEventArgs e)
        => ResetSettingsConfirm.IsVisible = false;

    private void OnConfirmResetSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            UpdateSettings.Delete(SettingsPathOverride);
            var defaults = new UpdateSettings();
            _settingsSyncing = true;
            UseLanguage(ResolveLanguage(null, CultureInfo.CurrentUICulture.Name));
            RestoreSettings(defaults);
            ResetSettingsConfirm.IsVisible = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("settings.error.reset")}: {ex.Message}";
        }
    }

    internal void RestoreSettingsForTest(UpdateSettings settings) => RestoreSettings(settings);
    internal void ConfirmResetSettingsForTest() => OnConfirmResetSettings(null, new RoutedEventArgs());

    private void InitializeUpdateUi(UpdateSettings? settings = null)
    {
        AutoUpdateRow.IsVisible = UpdateCheck.CanSelfUpdate;

        _updateUiSyncing = true;
        try { ChkAutoUpdate.IsChecked = (settings ?? UpdateSettings.Load(SettingsPathOverride)).AutoUpdate; }
        finally { _updateUiSyncing = false; }

        RefreshUpdateTexts();
        ReportAppliedUpdate();
    }

    /// <summary>
    /// Kendini güncelleyen uygulama yeniden başlar, bu yüzden "geçildi" bilgisi bellekte
    /// tutulamaz: başlatıcının bıraktığı işaret dosyası okunur. İşaret <b>yalnız kullanıcı
    /// şeridi kapatınca</b> silinir; şerit görüldüğü anda silinseydi, kullanıcı okumadan
    /// pencereyi kapattığında bilgi bir daha hiç görünmezdi.
    /// </summary>
    private void ReportAppliedUpdate()
    {
        _appliedNotice = new AppliedUpdateNotice(AppContext.BaseDirectory);
        if (!_appliedNotice.Load()) return;

        TxtAppliedVersion.Text = _appliedNotice.Version!;
        AppliedNotice.IsVisible = true;
    }

    private void OnDismissAppliedNotice(object? sender, RoutedEventArgs e)
    {
        _appliedNotice?.Shown();
        AppliedNotice.IsVisible = false;
    }

    private void RefreshUpdateTexts()
        => TxtAutoUpdateEffect.Text = Say(UpdateCheck.CanSelfUpdate ? "settings.update.auto-effect" : "settings.update.no-self-effect");

    /// <summary>
    /// Hedef listesi <c>paylasim-hedefleri.json</c>'dan gelir. Dosya yoksa şema
    /// varsayılanları kullanılır; ne liste ne de tavanlar XAML'de sabit durur, bu yüzden
    /// JSON'a eklenen üçüncü bir hedef burada kendiliğinden belirir.
    /// </summary>
    private void InitializeShareUi()
    {
        _shareTargets = ShareTargetTable.Load();

        var wasSyncing = _syncing;
        _syncing = true;
        CmbShareTarget.ItemsSource = _shareTargets.Targets.Select(target => target.DisplayName).ToList();
        CmbShareTarget.SelectedIndex = Math.Max(0, IndexOfTarget(_shareTargets.Default));
        _syncing = wasSyncing;

        RefreshShareTarget();
    }

    private int IndexOfTarget(ShareTarget target)
    {
        for (var index = 0; index < _shareTargets.Targets.Count; index++)
            if (ReferenceEquals(_shareTargets.Targets[index], target)) return index;
        return -1;
    }

    private void OnShareTargetChanged()
    {
        if (_syncing) return;
        RefreshShareTarget();
        SaveSettings();
    }

    private ShareTarget SelectedShareTarget()
    {
        var index = CmbShareTarget.SelectedIndex;
        return index >= 0 && index < _shareTargets.Targets.Count
            ? _shareTargets.Targets[index]
            : _shareTargets.Default;
    }

    private void RefreshShareTarget()
    {
        var target = SelectedShareTarget();
        TxtShareCeiling.Text = DescribeBytes(target.MaxBytes);

        var wasSyncing = _syncing;
        _syncing = true;
        CmbShareRetention.IsVisible = target.RetentionDays.Count > 0;
        TxtShareRetentionFixed.IsVisible = target.RetentionDays.Count == 0;
        if (target.RetentionDays.Count > 0)
        {
            CmbShareRetention.ItemsSource = target.RetentionDays
                .Select(day => day == 1 ? Say("settings.share.day-one") : Say("settings.share.days", day))
                .ToList();
            var chosen = target.RetentionDays.ToList().IndexOf(target.DefaultRetentionDays);
            CmbShareRetention.SelectedIndex = chosen >= 0 ? chosen : 0;
        }
        else
        {
            TxtShareRetentionFixed.Text = target.FixedRetentionHours is { } hours
                ? hours == 1 ? Say("settings.share.hour-one-fixed") : Say("settings.share.hours-fixed", hours)
                : Say("settings.share.not-stated");
        }
        _syncing = wasSyncing;

        // Silme yeteneği hedefe göre değişir ve gizlenmez: uguu.se gönderene silme jetonu
        // vermiyor, kullanıcı bunu seçim anında bilmek zorunda.
        BtnShareDelete.IsVisible = target.CanDelete;
        TxtShareDeleteNote.Text = target.CanDelete
            ? Say("settings.share.can-delete", target.DisplayName)
            : target.FixedRetentionHours is { } window
                ? Say("settings.share.no-delete-window", target.DisplayName, window)
                : Say("settings.share.no-delete", target.DisplayName);
    }

    /// <summary>
    /// Yüklemenin konuştuğu hedef satırı. Ayarlardaki liste yalnız görüneni okuyor, uç
    /// noktalar motorun tablosunda duruyor; ikisi aynı dosyadan gelir ve kimlikle eşlenir.
    /// Tablo bulunamazsa paylaşım yapılamaz, çünkü adres koda gömülü değildir.
    /// </summary>
    private CoreShare.ShareTarget? SelectedShareEndpoint()
    {
        try { _shareEndpoints ??= CoreShare.ShareTargetTable.Load(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
        return _shareEndpoints.Find(SelectedShareTarget().Id);
    }

    private ShareFlow Share() => _shareFlow ??= new ShareFlow(target =>
        CoreShare.ShareProviderFactory.Create(
            target,
            _shareTransport ??= new CoreShare.HttpClientTransport(),
            _shareEndpoints));

    /// <summary>Seçili ömür, gün. Hedef ömür seçtirmiyorsa boş.</summary>
    private int? SelectedRetentionDays()
    {
        var target = SelectedShareTarget();
        if (target.RetentionDays.Count == 0) return null;
        var index = CmbShareRetention.SelectedIndex;
        return index >= 0 && index < target.RetentionDays.Count ? target.RetentionDays[index] : target.DefaultRetentionDays;
    }

    /// <summary>
    /// Paylaş düğmesi yalnız teslim edilmiş bir dosya varken görünür. Yeni bir kodlama
    /// başladığında önceki bağlantı da düşer: gösterilen adres artık yeni dosyayı göstermez.
    /// </summary>
    private void ResetShare(bool fileReady)
    {
        BtnShare.IsVisible = fileReady;
        BtnShare.IsEnabled = fileReady;
        BtnShareCancel.IsVisible = false;
        ShareProgress.IsVisible = false;
        ShareProgress.Value = 0;
        ShareLinkRow.IsVisible = false;
        TxtShareLink.Text = "";
        TxtShareStatus.Text = "";
        BtnShareDelete.IsEnabled = false;
    }

    private void SetSharing(bool sharing)
    {
        BtnShare.IsEnabled = !sharing;
        BtnShareCancel.IsVisible = sharing;
        ShareProgress.IsVisible = sharing;
        if (sharing) ShareProgress.Value = 0;
    }

    private async void OnShare(object? sender, RoutedEventArgs e)
    {
        if (_lastOutput is null || !File.Exists(_lastOutput))
        {
            TxtShareStatus.Text = Say("settings.share.nothing");
            return;
        }

        if (SelectedShareEndpoint() is not { } target)
        {
            TxtShareStatus.Text = Say("settings.share.targets-missing", CoreShare.ShareTargetTable.FileName);
            return;
        }

        var flow = Share();
        if (flow.Running) return;

        SetSharing(true);
        TxtShareStatus.Text = Say("settings.share.uploading");
        ShareLinkRow.IsVisible = false;

        var progress = new Progress<CoreShare.UploadProgress>(step => ShareProgress.Value = step.Fraction);
        var result = await flow.ShareAsync(target, _lastOutput, SelectedRetentionDays(), progress);

        SetSharing(false);
        ShowShareResult(result, flow);
    }

    private void ShowShareResult(CoreShare.ShareResult result, ShareFlow flow)
    {
        if (result.Ok && result.Link is { } link)
        {
            TxtShareLink.Text = link.Url;
            ShareLinkRow.IsVisible = true;
            BtnShareDelete.IsEnabled = flow.CanDelete;
            TxtShareStatus.Text = link.ExpiresAt is { } expires
                ? Say("settings.share.shared-until", expires.ToLocalTime().ToString("d MMMM HH:mm", Strings.Culture))
                : Say("settings.share.shared");
            return;
        }

        ShareLinkRow.IsVisible = false;
        BtnShareDelete.IsEnabled = flow.CanDelete;
        TxtShareStatus.Text = result.Failure == CoreShare.ShareFailure.Cancelled
            ? Say("settings.share.cancelled")
            : $"{Say("settings.share.failed")}: {result.Message}";
    }

    private void OnShareCancel(object? sender, RoutedEventArgs e) => _shareFlow?.Cancel();

    private async void OnCopyShareLink(object? sender, RoutedEventArgs e)
    {
        // Pano yoksa adres yine okunabilir ve seçilebilir kalır; kullanıcıya hata basılmaz.
        try
        {
            if (Clipboard is null) return;
            await Clipboard.SetTextAsync(TxtShareLink.Text ?? "");
        }
        catch (Exception)
        {
        }
    }

    private async void OnShareDelete(object? sender, RoutedEventArgs e)
    {
        var flow = Share();
        if (flow.Link is null) return;
        if (SelectedShareEndpoint() is not { } target) return;

        BtnShareDelete.IsEnabled = false;
        var result = await flow.DeleteAsync(target);
        if (result.Ok)
        {
            ShareLinkRow.IsVisible = false;
            TxtShareLink.Text = "";
            TxtShareStatus.Text = Say("settings.share.closed");
            return;
        }

        BtnShareDelete.IsEnabled = flow.CanDelete;
        TxtShareStatus.Text = $"{Say("settings.share.close-failed")}: {result.Message}";
    }

    /// <summary>
    /// Tavan JSON'da bayt olarak duruyor ve tam ikilik katlar: 128 MiB ve 25 GiB. Ondalık
    /// birime yuvarlamak sayıyı değiştirirdi, bu yüzden ikilik ad yazılır.
    /// </summary>
    internal static string DescribeBytes(long bytes)
    {
        if (bytes <= 0) return "-";
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private void OnAutoUpdateChanged()
    {
        if (_updateUiSyncing) return;

        var settings = UpdateSettings.Load(SettingsPathOverride);
        settings.AutoUpdate = ChkAutoUpdate.IsChecked == true;
        try
        {
            settings.Save(SettingsPathOverride);
        }
        catch (Exception ex)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
            return;
        }

        // Açıkken güncellemeyi başlatıcı sessizce yapıyor, söylenecek bir şey yok.
        // Kapatıldığı anda haber verme görevi uygulamaya geçer.
        if (UpdateCheck.AutoUpdateEnabled(settings)) UpdateNotice.IsVisible = false;
        else _ = CheckForUpdateAsync();
    }

    /// <summary>
    /// Yeni sürümü arka planda sorar. Açılışı geciktirmez ve hiçbir hatada kullanıcıya
    /// bir şey göstermez: haber verilecek bir şey yoksa şerit hiç belirmez.
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        if (UpdateCheck.AutoUpdateEnabled()) return;

        string version;
        try
        {
            using var http = new HttpClient { Timeout = UpdateProbeTimeout };
            var asset = UpdateCheck.ManifestAssetName(UpdateCheck.Rid);
            var json = await http.GetStringAsync(UpdateCheck.LatestAssetUrl(asset));
            version = UpdateCheck.ParseManifest(json).Version;
        }
        catch (Exception)
        {
            // Ağ yok, oran sınırı, bozuk manifest: sessizce vazgeçilir.
            return;
        }

        if (!UpdateCheck.IsNewer(version, AppVersion())) return;
        if (string.Equals(ReadDismissedVersion(), version, StringComparison.OrdinalIgnoreCase)) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _noticeVersion = version;
            TxtNoticeVersion.Text = version;
            TxtNoticeCommand.Text = UpdateCheck.UpdateInstruction();
            UpdateNotice.IsVisible = true;
        });
    }

    private async void OnCopyUpdateCommand(object? sender, RoutedEventArgs e)
    {
        // Pano yoksa komut yine de okunabilir ve seçilebilir kalır; kullanıcıya hata basılmaz.
        try
        {
            if (Clipboard is null) return;
            await Clipboard.SetTextAsync(TxtNoticeCommand.Text ?? "");
        }
        catch (Exception)
        {
        }
    }

    private void OnDismissUpdateNotice(object? sender, RoutedEventArgs e)
    {
        UpdateNotice.IsVisible = false;
        if (_noticeVersion is not null) WriteDismissedVersion(_noticeVersion);
    }

    private static string DismissedNoticePath
    {
        get
        {
            var folder = Path.GetDirectoryName(UpdateSettings.DefaultPath);
            return string.IsNullOrEmpty(folder) ? DismissedNoticeFileName : Path.Combine(folder, DismissedNoticeFileName);
        }
    }

    private static string? ReadDismissedVersion()
    {
        try
        {
            return File.Exists(DismissedNoticePath) ? File.ReadAllText(DismissedNoticePath).Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteDismissedVersion(string version)
    {
        try
        {
            var folder = Path.GetDirectoryName(DismissedNoticePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(DismissedNoticePath, version);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Yazılamazsa şerit bir sonraki açılışta yeniden belirir; başka bir sonucu yok.
        }
    }

    /// <summary>
    /// Psy/AQ seçenek yoklamasını arka planda bir kez tüketir. <c>SupportsEncoderOption</c>
    /// ilk çağrısında ffmpeg süreci doğuruyor ve sonucu önbelleğe alıyor; o ilk çağrı
    /// arayüz iş parçacığına düşerse plan görünümü kodlayıcı başına yoklamanın süresi kadar
    /// kilitleniyor. Burada koşturulunca sonraki bütün okumalar önbellekten geliyor.
    /// </summary>
    internal static void WarmPsychovisualProbe(IEncoderAvailability capabilities)
    {
        foreach (var codec in FfmpegArguments.KnownCodecs)
            FfmpegArguments.PsychovisualArgs(codec, capabilities);
    }

    /// <summary>
    /// Yoklamayı arayüz iş parçacığından ayıran geçit.
    ///
    /// Okuma tarafı süreç doğurmaz: yalnız ısıtılmış cevabı verir. Sorulan kodlayıcı henüz
    /// ölçülmemişse <see cref="IEncoderMeasurementState"/> üzerinden "ölçülmedi" der —
    /// "çalışmıyor" demez — ve ölçümü arka planda kuyruğa alır. Ölçüm bitince
    /// <c>onMeasured</c> çağrılır ve hesap yenilenir. Aynı kodlayıcı aynı anda iki kez
    /// kuyruğa girmez; N yeniden hesap N yoklama doğurmaz.
    ///
    /// <see cref="FfmpegArguments.CachedPsychovisualArgs"/> ile
    /// <see cref="FfmpegArguments.WarmPsychovisual"/> ayrımının aynısı: ölçen yol ayrı,
    /// okuyan yol saf.
    /// </summary>
    internal sealed class DeferredEncoderAvailability
        : IEncoderAvailability, IHdr10EncoderAvailability, IEncoderOptionAvailability, IEncoderMeasurementState
    {
        /// <summary>
        /// Bu süreden uzun süren yoklama yerleşmiş sayılmaz. Yükün altında aynı komut
        /// 3625-14855 ms sürebiliyor (docs/olcumler/handbrake-acigi.md); o anki cevabı
        /// kalıcı kabul etmek T94'ün kaldırdığı "geçici düşüş kalıcı donanım yok kararı"
        /// kusurunu geri getirirdi.
        /// </summary>
        internal const int UnsettledProbeMs = 2000;

        /// <summary>
        /// Yerleşmeyen bir yoklama en çok bir kez daha denenir. Sınır olmasa ölçüm ile
        /// yeniden hesap birbirini besleyip sonsuz yoklama üretirdi.
        /// </summary>
        internal const int MaxAttempts = 2;

        internal const int RetryAfterFailureMs = 5000;

        private sealed class Answer
        {
            internal bool Works;
            internal string? PixelFormat;
            internal bool Settled;
            internal int Attempts;
            internal string? Failure;
            internal long ElapsedMs = -1;
            internal long LastAttemptTicks;
        }

        /// <summary>
        /// Bir kodlayicinin yoklama cevabinin hangi durumda oldugu. <c>NotWorking</c> ile
        /// <c>Failed</c> ayri: birincisi olculmus bir cevap, ikincisi yoklamanin hic cevap
        /// uretememesi. Ikisini ayni yere yazmak ucuncu durumu yok ediyordu.
        /// </summary>
        internal enum ProbeAnswer
        {
            Unknown,
            Working,
            NotWorking,
            Unsettled,
            Failed
        }

        private readonly IEncoderAvailability _source;
        private readonly Action _onMeasured;
        private readonly object _gate = new();
        private readonly Dictionary<string, Answer> _answers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
        private int _probes;

        internal DeferredEncoderAvailability(IEncoderAvailability source, Action onMeasured)
        {
            _source = source;
            _onMeasured = onMeasured;
        }

        /// <summary>Ölçümün gerçekten koştuğu yetenek nesnesi. Kodlama yolu bunu kullanır.</summary>
        internal IEncoderAvailability Source => _source;

        /// <summary>Arka planda koşan yoklama var mı.</summary>
        internal bool Pending
        {
            get { lock (_gate) return _running.Count > 0; }
        }

        /// <summary>Bu geçidin bugüne kadar kaç yoklama başlattığı. Ölçü bunu pinler.</summary>
        internal int Probes
        {
            get { lock (_gate) return _probes; }
        }

        /// <summary>
        /// Denemesi bittiği hâlde yerleşmemiş bir yoklama var mı. Varsa hesap bilinmeyen
        /// bir cevapla çalışıyor ve bunun kullanıcıya görünmesi gerekiyor.
        /// </summary>
        internal bool Unsettled
        {
            get
            {
                lock (_gate)
                    return _answers.Values.Any(a => !a.Settled && a.Attempts >= MaxAttempts);
            }
        }

        public bool HasEncoder(string name) => _source.HasEncoder(name);

        public bool SupportsEncoderOption(string codec, string option, string value)
            => _source is IEncoderOptionAvailability options && options.SupportsEncoderOption(codec, option, value);

        public bool IsMeasured(string codec) => Ready(Key("works", codec), codec, hdr10: false);

        public bool IsHdr10Measured(string codec) => Ready(Key("hdr10", codec), codec, hdr10: true);

        public bool WorksAsEncoder(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) && answer.Works;
        }

        public EncoderProbeState EncoderState(string codec) => AnswerFor(codec) switch
        {
            ProbeAnswer.Working => EncoderProbeState.Working,
            ProbeAnswer.NotWorking => EncoderProbeState.NotWorking,
            _ => EncoderProbeState.Unmeasured
        };

        public string? Hdr10PixelFormat(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("hdr10", codec), out var answer) ? answer.PixelFormat : null;
        }

        /// <summary>Kodlayicinin bugunku yoklama durumu. Olcu bunu okur, arayuz de.</summary>
        internal ProbeAnswer AnswerFor(string codec)
        {
            lock (_gate)
            {
                if (!_answers.TryGetValue(Key("works", codec), out var answer)) return ProbeAnswer.Unknown;
                if (answer.Failure is not null) return ProbeAnswer.Failed;
                if (!answer.Settled) return ProbeAnswer.Unsettled;
                return answer.Works ? ProbeAnswer.Working : ProbeAnswer.NotWorking;
            }
        }

        /// <summary>
        /// Kodlayicinin son yoklamasinin gercekten kac ms surdugu, hic yoklanmadiysa -1.
        /// Yerlesme karari bu sureden turuyor; olcu ikisini yuzlestiriyor.
        /// </summary>
        internal long ElapsedMsFor(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) ? answer.ElapsedMs : -1;
        }

        /// <summary>Yoklama firlattiysa istisnanin metni, yoksa <c>null</c>.</summary>
        internal string? FailureFor(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) ? answer.Failure : null;
        }

        /// <summary>
        /// Yoklamasi istisnayla dusen ilk kodlayicinin istisna metni. Arayuz durum satiri
        /// bunu gosterir; istisna sessizce kaybolmaz.
        /// </summary>
        internal string? FirstFailure
        {
            get
            {
                lock (_gate) return _answers.Values.Select(a => a.Failure).FirstOrDefault(f => f is not null);
            }
        }

        private static string Key(string kind, string codec) => $"{kind}:{codec}";

        /// <summary>
        /// Yoklama yerleşmediyse cevap "ölçüldü" sayılmaz: bir kez daha denenir, o da
        /// yerleşmezse deneme durur ama cevap yine <b>bilinmeyen</b> kalır. Yerleşmeyen bir
        /// yoklamayı ölçüm gibi kabul etmek, öldürülmüş bir denemeyi "bu kodlayıcı 10 bit
        /// taşıyamıyor" cümlesine çevirmek demekti; <see cref="Unsettled"/> bunun yerine
        /// durumu arayüze taşır.
        /// </summary>
        private bool Ready(string key, string codec, bool hdr10)
        {
            lock (_gate)
            {
                if (_answers.TryGetValue(key, out var answer))
                {
                    if (answer.Settled) return true;
                    var stuck = answer.Attempts >= MaxAttempts;
                    var cooling = stuck && Environment.TickCount64 - answer.LastAttemptTicks < RetryAfterFailureMs;
                    if (cooling) return false;
                }
                if (!_running.Add(key)) return false;
                _probes++;
            }

            Measure(key, codec, hdr10);
            return false;
        }

        private void Measure(string key, string codec, bool hdr10)
        {
            Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                var works = false;
                string? pixelFormat = null;
                Exception? failure = null;
                try
                {
                    if (hdr10) pixelFormat = (_source as IHdr10EncoderAvailability)?.Hdr10PixelFormat(codec);
                    else works = _source.WorksAsEncoder(codec);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                clock.Stop();

                lock (_gate)
                {
                    if (!_answers.TryGetValue(key, out var answer)) _answers[key] = answer = new Answer();
                    answer.Attempts++;
                    answer.LastAttemptTicks = Environment.TickCount64;
                    answer.Failure = failure?.Message;
                    answer.ElapsedMs = clock.ElapsedMilliseconds;
                    if (failure is null)
                    {
                        answer.Works = works;
                        answer.PixelFormat = pixelFormat;
                        answer.Settled = clock.ElapsedMilliseconds < UnsettledProbeMs;
                    }
                    else
                    {
                        answer.Works = false;
                        answer.PixelFormat = null;
                        answer.Settled = false;
                    }
                    _running.Remove(key);
                }

                _onMeasured();
            });
        }
    }

    private async Task ProbeHardwareEncodersAsync()
    {
        var available = false;
        IEncoderAvailability? encoders = null;
        var verdict = HardwareVerdict.NotProbed;

        try
        {
            (encoders, available, verdict) = await Task.Run(() =>
            {
                var capabilities = EncoderCapabilities.Instance;
                WarmPsychovisualProbe(capabilities);
                var options = new PlanOptions { TargetMb = WhatsAppTargetMb, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast };
                var plan = PlanCalculator.Build(HardwareProbeSource, options, capabilities);
                var probe = capabilities.Probe(plan.Codec);
                var decision = HardwareVerdict.Decide(probe, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
                return ((IEncoderAvailability?)capabilities, CodecModel.IsHardware(plan.Codec), decision);
            });
        }
        catch (Exception ex)
        {
            encoders = null;
            available = false;
            verdict = HardwareVerdict.NotProbed;
            TxtSystemStatus.Text = $"{Say("main.error.probe")}: {ex.Message}";
        }

        ApplyHardwareVerdict(encoders, available, verdict);
    }

    /// <summary>
    /// Hızlı mod kararının yazıldığı ayar dosyası. Boşken <see cref="UpdateSettings.DefaultPath"/>
    /// kullanılır; ölçüm süreç genelindeki ortam değişkenine dokunmadan kendi dosyasını verir.
    /// </summary>
    internal string? SettingsPathOverride { get; set; }

    /// <summary>
    /// Yoklamanın sonucunu arayüze ve ayara bağlar. Yoklamadan ayrı durur ki açılış yolu
    /// ffmpeg çağrılmadan da sınanabilsin.
    /// </summary>
    internal void ApplyHardwareVerdict(IEncoderAvailability? encoders, bool available, HardwareVerdict verdict)
    {
        _encoders = encoders;
        _planEncoders = encoders is null
            ? null
            : new DeferredEncoderAvailability(encoders, () => Dispatcher.UIThread.Post(ScheduleRecalculate));
        if (_preview is not null) _preview.Availability = encoders;
        _hardwareProbed = true;
        _hardwareEncoderAvailable = available;
        _hardwareVerdict = verdict;

        var wasSyncing = _syncing;
        _syncing = true;
        ChkFastGpu.IsEnabled = available;
        ChkFastGpu.IsChecked = available && ResolveFastGpuSetting(verdict);
        _syncing = wasSyncing;

        ApplyFastGpuTip();
        Recalculate();
    }

    /// <summary>
    /// Kararı ayar dosyasıyla buluşturur. Dosyada değer varsa yoklama onu ezmez; yoksa
    /// bu açılışta bir kez yazılır ve bir daha yoklamaya sorulmaz.
    /// </summary>
    private bool ResolveFastGpuSetting(HardwareVerdict verdict)
    {
        try
        {
            var settings = UpdateSettings.Load(SettingsPathOverride);
            if (HardwareVerdict.ReprobeRequested()) settings.FastGpu = null;
            if (verdict.ApplyTo(settings)) settings.Save(SettingsPathOverride);
            return settings.FastGpu == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
            return false;
        }
    }

    private void OnFastGpuChanged()
    {
        if (_syncing) return;
        try
        {
            var settings = UpdateSettings.Load(SettingsPathOverride);
            var enabled = ChkFastGpu.IsChecked == true;
            if (settings.FastGpu == enabled) return;
            settings.FastGpu = enabled;
            settings.Save(SettingsPathOverride);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
        }
        ApplyFastGpuTip();
    }

    private PlanOptions CurrentOptions() => new()
    {
        TargetMb = ParseTargetMb(),
        Intent = (Intent)Math.Max(0, CmbIntent.SelectedIndex),
        Codec = CodecFromIndex(CmbCodec.SelectedIndex),
        AllowResolutionDrop = ChkResolution.IsChecked == true,
        AllowFpsDrop = ChkFps.IsChecked == true,
        HdrPolicy = CmbHdrPolicy.SelectedIndex == 1 ? HdrPolicy.TonemapToSdr : HdrPolicy.Preserve,
        FillPolicy = CmbFillPolicy.SelectedIndex == 1 ? FillPolicy.QualityCeiling : FillPolicy.FillTarget,
        SpeedMode = ChkFastGpu.IsChecked == true ? SpeedMode.Fast : SpeedMode.Quality
    };

    private double ParseTargetMb()
        => double.TryParse(TxtTarget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) && mb > 0 ? mb : WhatsAppTargetMb;

    private static string? SelectedTag(SelectingItemsControl box)
        => (box.SelectedItem as Control)?.Tag as string;

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Media") { Patterns = ShellIntegration.MediaExtensions.Select(extension => "*." + extension).ToArray() },
                    FilePickerFileTypes.All
                }
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is not null) await LoadAsync(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepted = _cts is null && TryGetDroppedFile(e) is not null;
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropVisual(accepted ? DropVisual.Accept : DropVisual.Reject);
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        SetDropVisual(DropVisual.Idle);
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var file = TryGetDroppedFile(e);
        e.Handled = true;
        SetDropVisual(DropVisual.Idle);
        if (_cts is not null || file is null) return;
        await LoadAsync(file);
    }

    private void SetDropVisual(DropVisual state)
    {
        if (_dropVisual == state) return;
        _dropVisual = state;

        var (outline, fill, icon, scale) = state switch
        {
            DropVisual.Accept => ("NeonBlue", "NeonBlueActive", "NeonBlue", "scale(1.08)"),
            DropVisual.Reject => ("NeonPink", "NeonPinkFill", "NeonPink", "scale(0.94)"),
            _ => ("NeonBlueBorderStrong", "NeonBlueFill", "NeonBlue", "none")
        };

        DropOutline.Stroke = Paint(outline);
        DropOutline.Fill = Paint(fill);
        DropIcon.Stroke = Paint(icon);
        DropIcon.RenderTransform = TransformOperations.Parse(_motionReduced ? "none" : scale);

        ApplyDropText();
    }

    private void ApplyDropText()
    {
        var (title, hint) = _dropVisual switch
        {
            DropVisual.Accept => ("main.drop.release", "main.drop.hint"),
            DropVisual.Reject => ("main.drop.single", "main.drop.no-folder"),
            _ => ("main.drop.title", "main.drop.hint")
        };

        TxtDropTitle.Text = Say(title);
        TxtDropHint.Text = Say(hint);
    }

    private static string? TryGetDroppedFile(DragEventArgs e)
    {
        var items = e.DataTransfer.TryGetFiles()?.ToList();
        if (items is null || items.Count != 1) return null;
        if (items[0] is IStorageFolder) return null;
        var path = items[0].TryGetLocalPath();
        if (path is null || Directory.Exists(path) || !File.Exists(path)) return null;
        return path;
    }

    private void ReportSourceError(string message)
    {
        TxtSourceStatus.Text = message;
        TxtSourceStatus.IsVisible = true;
        TxtSystemStatus.Text = message;
    }

    private void ClearSourceError()
    {
        TxtSourceStatus.Text = "";
        TxtSourceStatus.IsVisible = false;
    }

    private async Task LoadAsync(string path)
    {
        TxtFileName.Text = Path.GetFileName(path);
        Fade(SourceCard, true);
        ClearSourceError();

        MediaInfo probed;
        try { probed = await FfprobeClient.ProbeAsync(path); }
        catch (Exception ex)
        {
            _info = null;
            BtnStart.IsEnabled = BtnConvert.IsEnabled = false;
            Fade(InfoGrid, false);
            Fade(DropZone, true);
            ResetPlanView();
            RefreshQualityPanels();
            RefreshQualityTargetAvailability();
            ReportSourceError($"{Say("main.error.unusable")}: {DescribeFailure(ex)}");
            return;
        }

        ApplyLoaded(path, probed);
        await MeasureComplexityAsync(probed);
    }

    /// <summary>
    /// Komut satırından gelen yolu, sürükle-bırakın kullandığı yükleyiciden geçirir.
    /// Yol yoksa hiçbir şey yapmaz; kötü dosyanın hatasını o yükleyici bildirir.
    /// </summary>
    internal Task LoadStartupFileAsync()
        => _startupFile is null ? Task.CompletedTask : LoadAsync(_startupFile);

    /// <summary>
    /// Açılış ölçümü için yoklamayı elle başlatır. <c>OnWindowLoaded</c> yalnız gerçek
    /// pencere gösterildiğinde ateşlendiği için başsız ölçüm aynı yolu buradan çağırır.
    /// </summary>
    internal Task ProbeForMeasurement() => ProbeHardwareEncodersAsync();

    internal void LoadWithoutProbing(string path, MediaInfo info)
    {
        TxtFileName.Text = Path.GetFileName(path);
        Fade(SourceCard, true);
        ClearSourceError();
        ApplyLoaded(path, info);
    }

    private void ApplyLoaded(string path, MediaInfo info)
    {
        _info = info;

        Fade(DropZone, false);

        _aiPlan = null;
        _profile = null;
        _sceneMap = null;
        BtnRevert.IsVisible = false;
        TxtAiStatus.Text = "";
        SetAiDetails(false);
        TxtConvertSource.Text = Path.GetFileName(path);
        ShowInfo(info);
        RefreshPreviewSource();

        _syncing = true;
        var suggested = info.FileSizeMb > WhatsAppTargetMb
            ? WhatsAppTargetMb
            : Math.Max(1, Math.Round(info.FileSizeMb / 2));
        SliderTarget.Maximum = Math.Max(SliderTarget.Maximum, Math.Ceiling(suggested));
        SliderTarget.Value = suggested;
        TxtTarget.Text = suggested.ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;

        UpdateToolStatus();
        Recalculate();
        RefreshQualityTargetAvailability();
        DeriveQualityFromTarget();
        RefreshConversion();
    }

    public static async Task<ComplexityProfile> ProbeWithMeasuredQualityAsync(
        MediaInfo info, SpeedMode speed, IQualityMeasurement? meter, CancellationToken ct)
    {
        var probed = await ComplexityProbe.RunDetailedAsync(info, speed, measureQuality: true, meter, ct);
        var anchors = probed.QualityMeasurements
            .Where(q => q is { Comparable: true, VmafNegMean: not null })
            .Select(q => q.VmafNegMean!.Value)
            .ToArray();
        return anchors.Length > 0 ? probed.Profile.WithProbeQuality(anchors) : probed.Profile;
    }

    private async Task MeasureComplexityAsync(MediaInfo info)
    {
        _probeCts?.Cancel();
        _probeCts?.Dispose();
        var cts = new CancellationTokenSource();
        _probeCts = cts;

        TxtEstimateNote.Text = Say("main.estimate.measuring");
        try
        {
            var speed = CurrentOptions().SpeedMode;
            var profile = await ProbeWithMeasuredQualityAsync(info, speed, null, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
            _profile = profile;
            Recalculate();

            _sceneMap = await EncodeRunner.TryBuildSceneMapAsync(info, ct: cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
            Recalculate();

            TxtEstimateNote.Text = Say("main.estimate.calibrating");
            var draft = PlanCalculator.BuildDetailed(info, CurrentOptions(), profile, _planEncoders).Plan;

            // The calibration is keyed to the plan it measured, and feeding it back changes the plan,
            // which would throw the measurement away. Measure again on the plan the calibration
            // produced, until the two agree.
            for (var round = 0; round < CalibrationRounds; round++)
            {
                var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, cts.Token);
                if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
                _profile = calibrated;
                Recalculate();

                if (!calibrated.Calibrated) break;

                var settled = PlanCalculator.BuildDetailed(info, CurrentOptions(), calibrated, _planEncoders).Plan;
                if (calibrated.AppliesTo(settled.Codec, PlanScale(info, settled), settled.Fps)) break;

                draft = settled;
                profile = calibrated.WithoutCalibration();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_info, info)) TxtEstimateNote.Text = $"{Say("main.estimate.failed")}: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_probeCts, cts)) _probeCts = null;
            cts.Dispose();
        }
    }

    private static double PlanScale(MediaInfo info, EncodePlan plan)
        => info.Height <= 0 ? 1.0 : (double)plan.Height / info.Height;

    private void ShowInfo(MediaInfo info)
    {
        Fade(InfoGrid, true);
        TxtDuration.Text = TimeSpan.FromSeconds(info.DurationSeconds).ToString(@"hh\:mm\:ss");
        TxtSize.Text = $"{info.FileSizeMb:0.0} MB";
        TxtResolution.Text = $"{info.Width}x{info.Height}";
        TxtFps.Text = info.Fps.ToString("0.##", CultureInfo.InvariantCulture);
        TxtVideoCodec.Text = info.VideoCodec;
        TxtAudio.Text = info.HasAudio ? $"{info.AudioCodec} {info.AudioBitrateBps / 1000}k" : Say("main.info.none");
        TxtBitrate.Text = $"{info.TotalBitrateBps / 1000} kbps";
        TxtHdr.Text = info.IsHdr ? Say("main.info.yes") : Say("main.info.no");
        Fade(HdrPolicyPanel, info.IsHdr);
    }

    /// <summary>
    /// Panelin süreceği dosyaları tazeler. Sağ taraf yalnız gerçekten üretilmiş bir dosya
    /// varken doludur; yoksa panel o tarafta sahte çıktı sunmaz (PLAN §6).
    /// </summary>
    private void RefreshPreviewSource()
    {
        if (_preview is null || _info is null) return;
        var aspect = _info.Height > 0 ? _info.Width / (double)_info.Height : 16.0 / 9.0;
        _preview.SetFiles(
            _info.FilePath,
            _lastOutput,
            aspect,
            TimeSpan.FromSeconds(_info.DurationSeconds),
            _info.Fps);
        if (!_preview.IsOpen) _preview.Open();
    }

    private static CodecPreference CodecFromIndex(int index) => index switch
    {
        1 => CodecPreference.Compatible,
        2 => CodecPreference.MaxCompression,
        _ => CodecPreference.Auto
    };

    private void Recalculate()
    {
        if (_info is null) return;
        var detailed = PlanCalculator.BuildDetailed(_info, CurrentOptions(), _profile, _planEncoders);
        _autoPlan = detailed.Plan;
        PlanHardwareNotMeasured = detailed.HardwareNotMeasured;
        _predictedQuality = detailed.PredictedQuality;
        _advice = detailed.Advice;
        _profile ??= detailed.Profile;

        if (_aiPlan is not null)
        {
            var validation = PlanParser.Parse(TxtAiJson.Text ?? "", _info, CurrentOptions());
            if (validation.Ok) _aiPlan = validation.Plan;
            else
            {
                _aiPlan = null;
                BtnRevert.IsVisible = false;
                TxtAiStatus.Text = Say("main.ai.stale");
            }
        }

        RefreshPlanView();
        RefreshQualityPanels();
        BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
        ReportUnsettledProbe();

        // T48/K1: plan tazelendi. Panel gecikmesini kendi kurar; burası yalnız haber verir.
        if (_preview is not null) _preview.Scenes = _sceneMap?.Map;
        _preview?.SetPlan(_info, ActivePlan, _profile);
    }

    /// <summary>Ölçü için: arka planda koşan yoklama var mı.</summary>
    internal bool PlanProbePending => _planEncoders?.Pending ?? false;

    /// <summary>Ölçü için: geçidin bugüne kadar başlattığı yoklama sayısı.</summary>
    internal int PlanProbeCount => _planEncoders?.Probes ?? 0;

    /// <summary>Ölçü için: son hesabın donanım cevabını ölçülmemiş sayıp saymadığı.</summary>
    internal bool PlanHardwareNotMeasured { get; private set; }

    /// <summary>Ölçü için: geçitte denemesi bitmiş ama yerleşmemiş bir yoklama var mı.</summary>
    internal bool PlanProbeUnsettled => _planEncoders?.Unsettled ?? false;

    /// <summary>Ölçü için: geçitte istisnayla düşmüş bir yoklamanın metni.</summary>
    internal string? PlanProbeFailure => _planEncoders?.FirstFailure;

    /// <summary>Ölçü için: durum satırı raporlamasını tek başına koşturur.</summary>
    internal void ReportProbeStatusForMeasurement() => ReportUnsettledProbe();

    /// <summary>
    /// Yerleşmeyen yoklamayı kullanıcıya söyler. Yoklama bir cevap üretemediğinde bu bir
    /// sonuç değil bilinmeyendir; sessizce varsayılana düşmek — HDR kaynakta tonemap'e —
    /// aynı dosyanın iki koşumda iki farklı çıktı vermesi demekti ve kullanıcı nedenini
    /// hiçbir yerde göremiyordu.
    ///
    /// İki ayrı cümle, çünkü iki ayrı durum: yoklama koşup sonuca varamadıysa
    /// <c>main.status.probe-unsettled</c>, hiç koşamayıp istisna fırlattıysa
    /// <c>main.status.probe-failed</c> — istisnanın metniyle birlikte. İkisi de
    /// başarısızlık değil bilinmezlik bildiriyor; <c>main.error.probe</c> gerçek
    /// başarısızlık için açılışta duruyor (<see cref="ProbeHardwareEncodersAsync"/>).
    /// </summary>
    private void ReportUnsettledProbe()
    {
        if (_planEncoders is null) return;

        string? text = null;
        if (_planEncoders.FirstFailure is { } failure) text = Say("main.status.probe-failed", failure);
        else if (_planEncoders.Unsettled) text = Say("main.status.probe-unsettled");

        if (text is not null)
        {
            if (TxtSystemStatus.Text != text) TxtSystemStatus.Text = text;
            _probeStatusShown = true;
        }
        else if (_probeStatusShown)
        {
            TxtSystemStatus.Text = string.Empty;
            _probeStatusShown = false;
        }
    }

    /// <summary>
    /// Yongalar ve her birinin hedefi. <c>Half</c>'ın hedefi kaynağa bağlı olduğu için
    /// burada boş durur ve hesap anında kaynaktan türetilir.
    /// </summary>
    private IReadOnlyList<(Button Chip, double? TargetMb)> QualityChips() => new (Button, double?)[]
    {
        (ChipWhatsApp, 16), (Chip8, 8), (Chip25, 25), (Chip100, 100),
        (Chip128, 128), (Chip180, 180), (ChipHalf, null)
    };

    private double? ChipTargetMb(double? declared)
    {
        if (declared is { } fixedMb) return fixedMb;
        return _info is null ? null : Math.Max(1, Math.Round(_info.FileSizeMb / 2, 1));
    }

    /// <summary>
    /// Her yonganın balonuna tahmini kalite paneli koyar.
    ///
    /// Hesap <see cref="PlanCalculator.BuildDetailed"/>'dır. Karmaşıklık tarafı saf aritmetik:
    /// ölçülmüş profil varsa o kullanılır, yoksa <c>BuildDetailed</c> kendi içinde
    /// <see cref="ComplexityProfile.FromSourceBitrate"/>'a düşer.
    ///
    /// Kodlayıcı tarafı saf değildi ve T130'a kadar bu cümle yanlıştı: <c>BuildDetailed</c>
    /// donanım adaylarını ve HDR piksel biçimini yetenek nesnesine soruyor, o da ffmpeg
    /// süreci doğuruyordu. Artık arayüz yolu gerçek yetenek nesnesini değil
    /// <see cref="DeferredEncoderAvailability"/> geçidini görüyor; geçit yalnız ısıtılmış
    /// cevabı okur, süreç doğurmaz. Bu yüzden panel fare üstüne gelince beklemeden çıkar.
    /// Profilin ölçülmüş olup olmadığını panel ayrıca yazar; tahmin ölçüm gibi sunulmaz.
    /// </summary>
    private void RefreshQualityPanels()
    {
        foreach (var (chip, declared) in QualityChips())
        {
            if (ToolTip.GetTip(chip) is not StackPanel panel || panel.Children.Count == 0) continue;

            while (panel.Children.Count > 1) panel.Children.RemoveAt(panel.Children.Count - 1);
            panel.Children.Add(new Border { Theme = Look("PanelRule") });
            panel.Children.Add(QualityBody(ChipTargetMb(declared)));
        }
    }

    private Control QualityBody(double? targetMb)
    {
        var hint = QualityHint.For(_info, CurrentOptions(), targetMb, _profile, _planEncoders);

        // Video yüklü değilken skor hesaplanamaz. Sıfır ya da uydurma bir sayı yerine alan
        // boş kalır ve yonganın ne işe yaradığı yukarıdaki maddelerde durmaya devam eder.
        if (hint.Score is not { } score || hint.TargetMb is not { } target)
            return new TextBlock
            {
                Text = Say("main.quality.tip.empty"),
                Theme = Look("Hint"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = Scalar("TooltipMaxWidth", 460)
            };

        // Ölçülen ile tahmin edilen ayrı kelimelerle söylenir; tahmini ölçüm gibi sunmak
        // kullanıcının güvenerek yanlış hedef seçmesine yol açar.
        var basis = hint.Basis switch
        {
            QualityBasis.Measured => Say("main.quality.basis.measured"),
            QualityBasis.Estimated => Say("main.quality.basis.estimated"),
            _ => Say("main.quality.basis.under-target")
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = Scalar("SpaceMd", 12),
            RowSpacing = Scalar("SpaceSm", 8),
            MaxWidth = Scalar("TooltipMaxWidth", 460)
        };

        AddQualityRow(grid, Say("main.quality.target"), $"{Num(target, "0.##")} MB");
        AddQualityRow(grid, Say("main.quality.predicted"), $"{score:0.#}/100");
        AddQualityRow(grid, Say("main.quality.loss"), Say("main.quality.points", Num(hint.LossPoints, "0.#")));
        AddQualityRow(grid, Say("main.quality.basis"), basis);
        return grid;
    }

    private void AddQualityRow(Grid grid, string label, string value)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var key = new TextBlock { Text = LanguageCatalog.Display(label), Theme = Look("PlanFactLabel") };
        Grid.SetRow(key, row);
        Grid.SetColumn(key, 0);

        var read = new TextBlock { Text = LanguageCatalog.Display(value), Theme = Look("PlanFactValue") };
        Grid.SetRow(read, row);
        Grid.SetColumn(read, 1);

        grid.Children.Add(key);
        grid.Children.Add(read);
    }

    private void ResetPlanView()
    {
        PlanFacts.Children.Clear();
        PlanFacts.RowDefinitions.Clear();
        PlanReasons.Children.Clear();
        PlanRule.IsVisible = false;
        TxtPlanEmpty.IsVisible = true;
        TxtCommand.Text = "";
    }

    private void AddPlanFact(string label, string value)
    {
        var row = PlanFacts.RowDefinitions.Count;
        PlanFacts.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var key = new TextBlock { Text = LanguageCatalog.Display(label), Theme = Look("PlanFactLabel") };
        Grid.SetRow(key, row);
        Grid.SetColumn(key, 0);

        var read = new TextBlock { Text = value, Theme = Look("PlanFactValue") };
        Grid.SetRow(read, row);
        Grid.SetColumn(read, 1);

        PlanFacts.Children.Add(key);
        PlanFacts.Children.Add(read);
    }

    private void AddPlanReason(string text)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        row.Children.Add(new TextBlock { Text = "•", Theme = Look("PlanBullet") });

        var body = new TextBlock { Text = LanguageCatalog.Display(text), Theme = Look("PlanReasonText") };
        Grid.SetColumn(body, 1);
        row.Children.Add(body);

        PlanReasons.Children.Add(row);
    }

    private void RefreshPlanView()
    {
        if (ActivePlan is not { } plan || _info is null) return;
        _estimate = PlanCalculator.Estimate(plan, _info, _profile);

        PlanFacts.Children.Clear();
        PlanFacts.RowDefinitions.Clear();
        PlanReasons.Children.Clear();
        TxtPlanEmpty.IsVisible = false;

        var channels = plan.AudioChannels == 1 ? $" {Say("main.plan.audio.mono")}" : "";
        AddPlanFact(Say("main.plan.fact.plan"), _aiPlan is null ? Say("main.plan.automatic") : "AI");
        AddPlanFact(Say("main.plan.fact.encoder"), plan.Codec);
        AddPlanFact(Say("main.plan.fact.mode"), plan.ModeEnum switch
        {
            EncodeMode.Crf => $"CRF {plan.Crf}",
            EncodeMode.PassThrough => Say("main.plan.mode.copy"),
            _ => $"{plan.VideoBitrateK}k · {Say("main.plan.mode.two-pass")}"
        });
        AddPlanFact(Say("main.plan.fact.resolution"), $"{plan.Width}x{plan.Height}");
        AddPlanFact(Say("main.plan.fact.frame-rate"), $"{Num(plan.Fps, "0.##")} FPS");
        AddPlanFact(Say("main.plan.fact.audio"), plan.AudioCodec is null ? Say("main.info.none") : $"{plan.AudioCodec} {plan.AudioBitrateK}k{channels}");
        AddPlanFact(Say("main.plan.fact.preset"), plan.Preset);
        AddPlanFact(Say("main.plan.fact.estimate"), _estimate is { } size ? $"{Num(size.ExpectedMb, "0.0")} MB" : "-");

        foreach (var line in StrategyLines()) AddPlanReason(line);
        foreach (var line in ReasonLines(plan)) AddPlanReason(line);

        var reasons = PlanReasons.Children.Count;
        PlanRule.IsVisible = reasons > 0;
        PlanReasonsHead.IsVisible = reasons > 0;
        TxtPlanReasonsHead.Text = $"{Say("main.plan.reasons")} · {reasons}";
        SetPlanReasonsExpanded(_reasonsExpanded);

        RefreshEstimateView();
        RefreshDurationView();
        TxtCommand.Text = FfmpegArguments.ToCommandLine(DisplayedEncodeArguments(_info, plan,
            BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4"), _encoders, _sceneMap?.Map));
    }

    /// <summary>
    /// Ekranda gosterilen komut. <paramref name="scenes"/> kodlamaya giden haritanin ta kendisidir;
    /// ayri gecilmezse gosterilen komut kosan komuttan anahtar kare araliginda ayrisirdi.
    /// </summary>
    public static IReadOnlyList<string> DisplayedEncodeArguments(MediaInfo info, EncodePlan plan,
        string outputPath, IEncoderAvailability? availability, SceneMap? scenes = null)
        => FfmpegArguments.Build(info, plan, outputPath,
            plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null, availability, scenes);

    private void RefreshEstimateView()
    {
        if (_estimate is not { } estimate || _info is null)
        {
            TxtEstimateValue.Text = "-";
            TxtEstimateRange.Text = "";
            TxtEstimateNote.Text = "";
            return;
        }

        var reading = $"{estimate.ExpectedMb:0.0} MB";
        if (TxtEstimateValue.Text != reading) Pulse(TxtEstimateValue, true);
        TxtEstimateValue.Text = reading;
        TxtEstimateRange.Text = $"{estimate.LowMb:0.0} - {estimate.HighMb:0.0} MB · {Say("main.estimate.of-source")} %{estimate.ExpectedMb / Math.Max(_info.FileSizeMb, 0.01) * 100:0.#}";

        var basis = estimate.Measured
            ? Say("main.estimate.basis.measured")
            : Say("main.estimate.basis.estimated");
        var mode = estimate.Enforced
            ? Say("main.estimate.mode.enforced")
            : Say("main.estimate.mode.ceiling");
        TxtEstimateNote.Text = $"{basis} · {mode} · {Say("main.estimate.predicted-quality")} {_predictedQuality:0.#}/100";
    }

    private void RefreshDurationView()
    {
        if (ActivePlan is not { } plan || _info is null)
        {
            TxtDurationValue.Text = "-";
            TxtDurationRange.Text = "";
            return;
        }

        var profile = _profile ?? ComplexityProfile.FromSourceBitrate(_info);
        if (profile.EstimateTime(plan, _info.DurationSeconds) is not { } duration)
        {
            TxtDurationValue.Text = "-";
            TxtDurationRange.Text = profile.Speed is null
                ? Say("main.duration.not-measured")
                : Say("main.duration.other-settings");
            return;
        }

        if (duration.StreamCopy)
        {
            TxtDurationValue.Text = Say("main.duration.copied");
            TxtDurationRange.Text = Say("main.duration.copied-note");
            return;
        }

        var reading = $"~{HumanDuration(duration.ExpectedSeconds)}";
        if (TxtDurationValue.Text != reading) Pulse(TxtDurationValue, true);
        TxtDurationValue.Text = reading;

        var rate = profile.Speed!.FramesPerSecond;
        TxtDurationRange.Text = $"{HumanDuration(duration.LowSeconds)} - {HumanDuration(duration.HighSeconds)} · {Say("main.duration.measured")} {rate:0} {Say("main.duration.frames-per-second")}";
    }

    private string HumanDuration(double seconds)
    {
        if (seconds < 60) return $"{Math.Max(5, Math.Round(seconds / 5.0) * 5.0):0} {Say("main.duration.seconds")}";

        if (seconds < 3600)
        {
            var minutes = seconds / 60.0;
            if (minutes < 10) return $"{Math.Max(1.0, Math.Round(minutes * 2.0) / 2.0):0.#} {Say("main.duration.minutes")}";
            return $"{Math.Round(minutes):0} {Say("main.duration.minutes")}";
        }

        var hours = (int)(seconds / 3600);
        var rest = (int)Math.Round((seconds - hours * 3600.0) / 60.0);
        if (rest >= 60)
        {
            hours++;
            rest = 0;
        }
        return rest == 0 ? $"{hours} {Say("main.duration.hours")}" : $"{hours} {Say("main.duration.hours")} {rest} {Say("main.duration.minutes")}";
    }

    public static bool ShowsMeasuredQualityStop(EncodePlan plan, ReasonNote note, FillPolicy fillPolicy) =>
        plan.StopsShortOfBandOnPurpose
        && fillPolicy == FillPolicy.FillTarget
        && note.Mb >= FillBand.For(note.TargetMb).HardFloorMb;

    private List<string> ReasonLines(EncodePlan plan)
    {
        var parts = new List<string>();
        foreach (var note in plan.ReasonCodes)
        {
            var text = note.Code switch
            {
                ReasonCode.ResolutionScaled => Say("main.reason.resolution-scaled",
                    note.Width, note.Height, Num(note.ScalePercent, "0.#")),
                ReasonCode.FrameRateReduced => Say("main.reason.frame-rate-reduced", Num(note.Fps, "0.##")),
                ReasonCode.ResolutionRestoredAtCeiling => Say("main.reason.resolution-restored",
                    note.Width, note.Height, Num(note.Fps, "0.##"), Num(note.Crf, "0")),
                ReasonCode.BudgetExceedsCeiling => ShowsMeasuredQualityStop(plan, note, CurrentOptions().FillPolicy)
                    ? Say("main.reason.measured-quality-stop",
                        Num(note.Crf, "0"), Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##"))
                    : Say("main.reason.budget-exceeds-ceiling",
                        Num(note.BudgetCrf, "0.#"), Num(note.Crf, "0"), Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##")),
                ReasonCode.BudgetBelowCeilingTwoPass => Say("main.reason.budget-below-ceiling",
                    Num(note.BudgetCrf, "0.#"), Num(note.Crf, "0"), Num(note.TargetMb, "0.##")),
                ReasonCode.PredictedQualityMeasured => Say("main.reason.quality-measured",
                    Num(note.Score, "0.#"), Num(note.Bppf, "0.0000"), Num(note.DetailExponent, "0.00")),
                ReasonCode.PredictedQualityEstimated => Say("main.reason.quality-estimated", Num(note.Score, "0.#")),
                ReasonCode.RetryScaled => Say("main.reason.retry-scaled",
                    Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##"), Num(note.AudioMb, "0.00"), Num(note.Factor, "0.###")),
                ReasonCode.EncoderFallback => Say("main.reason.encoder-fallback", note.RequestedCodec, note.FallbackCodec),
                ReasonCode.HdrTonemapped => Say("main.reason.hdr-tonemapped"),
                ReasonCode.FillCrfLowered => Say("main.reason.fill-crf-lowered",
                    Num(note.Crf, "0.#"), Num(note.Mb, "0.0"), Num(note.BandLowerMb, "0.0"), Num(note.TargetMb, "0.0")),
                ReasonCode.FillTwoPassBandCenter => Say("main.reason.fill-band-center",
                    Num(note.Crf, "0"), Num(note.Mb, "0.0")),
                ReasonCode.FillTwoPassBandTooNarrowForCrf => Say("main.reason.fill-band-narrow",
                    Num(note.Factor * 100, "0.#"),
                    Num((note.TargetMb - note.BandLowerMb) / Math.Max(note.TargetMb, 0.01) * 100, "0.#"),
                    Num(note.Mb, "0.0")),
                ReasonCode.HardwareBitrateBias => Say("main.reason.hardware-bitrate-bias",
                    note.FallbackCodec, Num((1 - note.Factor) * 100, "0.#")),
                ReasonCode.SourceAlreadyUnderTarget => Say("main.reason.source-under-target",
                    Num(note.Mb, "0.0"), Num(note.TargetMb, "0.##")),
                ReasonCode.TargetCappedToSource => Say("main.reason.target-capped",
                    Num(note.Mb, "0.##"), Num(note.TargetMb, "0.##")),
                _ => null
            };
            if (text is not null) parts.Add(text);
        }

        return parts;
    }

    private List<string> StrategyLines()
    {
        if (_advice is not { } advice) return new List<string>();

        var regime = advice.Regime switch
        {
            CompressionRegime.Light => Say("main.reason.regime.light"),
            CompressionRegime.Balanced => Say("main.reason.regime.balanced"),
            CompressionRegime.Aggressive => Say("main.reason.regime.aggressive"),
            _ => Say("main.reason.regime.extreme")
        };

        var lines = new List<string>
        {
            Say("main.reason.ratio", Num(advice.Ratio, "0.#"), regime)
        };

        foreach (var note in advice.Notes.Distinct())
        {
            var text = AdviceLine(note, Strings.Language, ChkFastGpu.IsChecked == true);
            if (text is not null) lines.Add(text);
        }

        return lines;
    }

    internal static readonly AdviceCode[] AdviceCodesWithoutText = Array.Empty<AdviceCode>();

    internal static string? AdviceLine(AdviceCode note, string language, bool fastGpu)
    {

        return note switch
        {
            AdviceCode.BudgetIsGenerous => Speak(language, "main.advice.budget-generous"),
            AdviceCode.CodecUpgradeRecommended => Speak(language, "main.advice.codec-upgrade"),
            AdviceCode.HardwareCodecCostsQuality => Speak(language, "main.advice.hardware-costs-quality"),
            AdviceCode.ExtremeRatioWarning => Speak(language, "main.advice.extreme-ratio"),
            AdviceCode.TargetBelowCodecFloor => Speak(language, "main.advice.below-codec-floor"),
            AdviceCode.FrameRateCutForFloor => Speak(language, "main.advice.frame-rate-for-floor"),
            AdviceCode.MotionCutIsCheap => Speak(language, "main.advice.motion-cut-cheap"),
            AdviceCode.MotionCutIsExpensive => Speak(language, "main.advice.motion-cut-expensive"),
            AdviceCode.ContentIsSimple => Speak(language, "main.advice.content-simple"),
            AdviceCode.ContentIsComplex => Speak(language, "main.advice.content-complex"),
            AdviceCode.ScaleSavesMuch => Speak(language, "main.advice.scale-saves-much"),
            AdviceCode.ScaleSavesLittle => Speak(language, "main.advice.scale-saves-little"),
            AdviceCode.ResolutionReduced => Speak(language, "main.advice.resolution-reduced"),
            AdviceCode.FrameRateReduced => Speak(language, "main.advice.frame-rate-reduced"),
            AdviceCode.TargetEnforcedTwoPass => Speak(language, "main.advice.two-pass"),
            AdviceCode.QualityCeilingReached => Speak(language, "main.advice.quality-ceiling"),
            AdviceCode.AudioReduced => Speak(language, "main.advice.audio-reduced"),
            AdviceCode.AudioMono => Speak(language, "main.advice.audio-mono"),
            AdviceCode.AudioDropped => Speak(language, "main.advice.audio-dropped"),
            AdviceCode.EncoderFallback => fastGpu
                ? Speak(language, "main.advice.encoder-fallback-gpu")
                : Speak(language, "main.advice.encoder-fallback"),
            AdviceCode.HdrTonemapped => Speak(language, "main.advice.hdr-tonemapped"),
            _ => null
        };
    }

    private static string BuildUniqueOutputPath(string inputPath, string suffix, string extension)
    {
        var dir = Path.GetDirectoryName(inputPath)!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        const int firstIndex = 2;
        if (suffix == "shrunk" && name.EndsWith("_shrunk", StringComparison.OrdinalIgnoreCase))
            name = name[..^"_shrunk".Length];
        var candidate = Path.Combine(dir, $"{name}_{suffix}.{extension}");
        for (var index = firstIndex; PathEquals(candidate, inputPath) || File.Exists(candidate); index++)
            candidate = Path.Combine(dir, $"{name}_{suffix}_{index}.{extension}");
        return candidate;
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void OnTargetSliderChanged()
    {
        if (_syncing) return;
        _syncing = true;
        TxtTarget.Text = Math.Round(SliderTarget.Value, 1).ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;
        if (!_targetIsDerived) DeriveQualityFromTarget();
        ScheduleRecalculate();
    }

    private void OnTargetTextChanged()
    {
        if (_syncing) return;
        _syncing = true;
        var mb = ParseTargetMb();
        if (mb > SliderTarget.Maximum) SliderTarget.Maximum = Math.Ceiling(mb);
        SliderTarget.Value = mb;
        _syncing = false;
        if (!_targetIsDerived) DeriveQualityFromTarget();
        ScheduleRecalculate();
    }

    private void OnQualityTargetSliderChanged()
    {
        if (_syncing || _qualityIsDerived) return;
        _qualityIsDerived = true;
        TxtQualityTarget.Text = Math.Round(SliderQualityTarget.Value).ToString("0.##", CultureInfo.InvariantCulture);
        _qualityIsDerived = false;
        DeriveTargetFromQuality();
    }

    private void OnQualityTargetTextChanged()
    {
        if (_syncing || _qualityIsDerived) return;

        // T61/K2: kutuya yazılan sayı geri yazılmaz. Kaydırıcı yazılan değere gider ama
        // kutu olduğu gibi kalır; gidiş dönüş tam olarak aynı sayıya dönmediği için
        // kullanıcının 60'ı 59,2 diye düzeltilirdi.
        _qualityIsDerived = true;
        SliderQualityTarget.Value = ParseQualityTarget();
        _qualityIsDerived = false;
        DeriveTargetFromQuality();
    }

    /// <summary>Kutudaki kalite skoru. Okunamayan metin kaydırıcının o anki değeridir.</summary>
    private double ParseQualityTarget()
    {
        var value = double.TryParse(TxtQualityTarget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
            ? score
            : SliderQualityTarget.Value;
        return Math.Clamp(value, SliderQualityTarget.Minimum, SliderQualityTarget.Maximum);
    }

    /// <summary>
    /// Kaliteden hedef MB türetir. Kullanıcının ellediği taraf kalite olduğu için kalite
    /// kutusuna dokunulmaz; yalnız hedef yazılır ve sınır durumu ekrana basılır.
    /// </summary>
    private void DeriveTargetFromQuality()
    {
        if (_info is null) { RefreshQualityTargetAvailability(); return; }

        QualityDerivedTargets++;
        var result = PlanCalculator.TargetMbForQuality(_info, CurrentOptions(), ParseQualityTarget(), _profile, _planEncoders);
        var mb = Math.Round(result.TargetMb, 1);

        _targetIsDerived = true;
        if (mb > SliderTarget.Maximum) SliderTarget.Maximum = Math.Ceiling(mb);
        SliderTarget.Value = mb;
        TxtTarget.Text = mb.ToString("0.##", CultureInfo.InvariantCulture);
        _targetIsDerived = false;

        ShowQualityTargetBound(result, mb);
        ScheduleRecalculate();
    }

    /// <summary>
    /// Hedef MB'dan kalite türetir. Bu yön kullanıcının MB'ı ellediği yön olduğu için MB
    /// kutusuna dokunulmaz.
    /// </summary>
    private void DeriveQualityFromTarget()
    {
        if (_info is null) { RefreshQualityTargetAvailability(); return; }

        var hint = QualityHint.For(_info, CurrentOptions(), ParseTargetMb(), _profile, _planEncoders);
        if (hint.Score is not { } score) return;

        TargetDerivedQualities++;
        _qualityIsDerived = true;
        SliderQualityTarget.Value = Math.Clamp(score, SliderQualityTarget.Minimum, SliderQualityTarget.Maximum);
        TxtQualityTarget.Text = Math.Round(score, 1).ToString("0.##", CultureInfo.InvariantCulture);
        _qualityIsDerived = false;

        SetQualityTargetNotice("");
    }

    /// <summary>
    /// T61/K3: iki sınır sessizce kırpılmaz. Kullanıcı kaydırıcıyı sürüklerken ne olduğunu
    /// ve nedenini denetimin hemen altında okur; balonda saklanmaz.
    /// </summary>
    private void ShowQualityTargetBound(QualityTargetResult result, double mb)
    {
        var target = mb.ToString("0.##", CultureInfo.InvariantCulture);
        var reached = result.PredictedQuality.ToString("0.#", CultureInfo.InvariantCulture);

        SetQualityTargetNotice(result.Bound switch
        {
            QualityTargetBound.BelowFloor => Say("main.quality.below-floor", target, reached),
            QualityTargetBound.AboveSourceCeiling => Say("main.quality.above-ceiling", target, reached),
            _ => ""
        });
    }

    private void SetQualityTargetNotice(string text)
    {
        TxtQualityTargetNotice.Text = text;
        TxtQualityTargetNotice.IsVisible = text.Length > 0;
    }

    /// <summary>
    /// T61/K4: kaynak yokken kaliteden MB türetilemez. Denetim kapanır ve neden kapalı
    /// olduğu tek satırla yazılır; sayı uydurulmaz.
    /// </summary>
    private void RefreshQualityTargetAvailability()
    {
        var ready = _info is not null;
        SliderQualityTarget.IsEnabled = TxtQualityTarget.IsEnabled = ready;
        if (ready) SetQualityTargetNotice("");
        else SetQualityTargetNotice(Say("main.quality.no-source"));
    }

    /// <summary>Ölçüm için tur sayacı: her yönde kaç türetme yapıldı.</summary>
    internal int QualityDerivedTargets { get; private set; }

    internal int TargetDerivedQualities { get; private set; }

    private void OnPreset(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            if (value > SliderTarget.Maximum) SliderTarget.Maximum = value;
            TxtTarget.Text = tag;
        }
    }

    private void OnPresetHalf(object? sender, RoutedEventArgs e)
    {
        if (_info is not null) TxtTarget.Text = Math.Round(_info.FileSizeMb / 2, 1).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void OnOptionChanged()
    {
        if (_syncing) return;
        ScheduleRecalculate();
    }

    private bool _commandExpanded;

    private void OnToggleCommand(object? sender, RoutedEventArgs e) => SetCommandExpanded(!_commandExpanded);

    private void SetCommandExpanded(bool expanded)
    {
        _commandExpanded = expanded;
        TxtCommand.TextWrapping = expanded ? TextWrapping.Wrap : TextWrapping.NoWrap;
        TxtCommand.MaxLines = expanded ? 8 : 1;
        BtnCommandExpand.Content = expanded ? "▴" : "▾";
        ApplyScrollAffordance(TxtCommand, TxtCommand.IsPointerOver);
    }

    private bool _reasonsExpanded;

    private void OnTogglePlanReasons(object? sender, RoutedEventArgs e) => SetPlanReasonsExpanded(!_reasonsExpanded);

    /// <summary>
    /// Gerekçelerin açık olduğu hâli ölçüme açar. Plan panelinin tavanı yalnız bu hâlde
    /// bağlayıcı oluyor ve düğmeye basmadan o hâl kurulamıyor.
    /// </summary>
    internal void ExpandPlanReasons() => SetPlanReasonsExpanded(true);

    /// <summary>
    /// K6: gerekçeler katlanır. Kapalıyken plan paneli olguların net özetidir ve kaymaz;
    /// açıkken listenin tamamı görünür ve taşma <c>PlanScroll</c>'a düşer. Metin hiçbir
    /// durumda kısaltılmıyor.
    /// </summary>
    private void SetPlanReasonsExpanded(bool expanded)
    {
        _reasonsExpanded = expanded;
        PlanReasons.IsVisible = expanded && PlanReasons.Children.Count > 0;
        BtnPlanReasons.Content = expanded ? "▴" : "▾";
    }

    private void OnTogglePerformance(object? sender, RoutedEventArgs e) => SetPerformanceDetails(!PerformanceDetails.IsVisible);

    private void SetPerformanceDetails(bool visible)
    {
        PerformanceDetails.IsVisible = visible;
        BtnPerformanceExpand.Content = visible ? "▴" : "▾";
    }

    /// <summary>
    /// Açık hâli ölçüme açar. Panelin yerleşimi yalnız bu hâlde bağlayıcı ve düğmeye
    /// basmadan o hâl kurulamıyor.
    /// </summary>
    internal void ExpandPerformanceCheck() => SetPerformanceDetails(true);

    /// <summary>
    /// Ölçümü başlatan çağrı. Alan olarak duruyor ki ölçüm sondayı sahtesiyle
    /// değiştirebilsin; gerçek sonda yalnız buradan çağrılır ve kendiliğinden hiç
    /// koşmaz — ne açılışta, ne sekme açılırken, ne dosya yüklenince.
    /// </summary>
    internal Func<CancellationToken, Task<PerformanceCheckResult>> PerformanceProbeRunner { get; set; }
        = token => PerformanceProbe.RunAsync(ct: token);

    private bool _performanceRunning;

    /// <summary>
    /// Ekranda duran son ölçüm sonucu. Dil değişince panel bundan yeniden kuruluyor;
    /// aksi hâlde açılışta yazılan satırlar ilk dilde donup kalıyordu.
    /// </summary>
    private PerformanceCheckResult _performanceShown = PerformanceCheckResult.NotMeasured;

    private async void OnRunPerformanceCheck(object? sender, RoutedEventArgs e) => await RunPerformanceCheckAsync();

    internal async Task RunPerformanceCheckAsync()
    {
        if (_performanceRunning) return;

        _performanceRunning = true;
        BtnPerformanceRun.IsEnabled = false;
        TxtPerformanceStatus.Text = Say("performance.running");

        try
        {
            ShowPerformanceResult(await PerformanceProbeRunner(CancellationToken.None));
        }
        catch (Exception ex)
        {
            TxtPerformanceStatus.Text = $"{Say("performance.failed")}: {ex.Message}";
        }
        finally
        {
            _performanceRunning = false;
            BtnPerformanceRun.IsEnabled = true;
        }
    }

    /// <summary>
    /// Sonucu ekrana yazar. Manşet <see cref="PerformanceCheckResult.Impact"/> alanından
    /// değil bulgulardan kurulur; gerekçesi <see cref="PerformanceReportText"/> içinde.
    /// </summary>
    internal void ShowPerformanceResult(PerformanceCheckResult result)
    {
        _performanceShown = result;
        TxtPerformanceStatus.Text = string.Empty;

        PerformanceFacts.Children.Clear();
        foreach (var fact in PerformanceReportText.Facts(result))
        {
            var row = new TextBlock
            {
                Theme = Look("MonoValue"),
                FontWeight = FontWeight.Normal,
                TextWrapping = TextWrapping.Wrap
            };

            // Koşulardan kurulu bir TextBlock büyük harf geçidine uğramıyor. Kodlayıcı adı
            // ve birim ölçüldüğü yazımla kalsın diye satır burada kuruluyor: "libx264"
            // sözcük kuralından geçseydi "Libx264", "ms" ise "Ms" olurdu.
            if (row.Inlines is { } inlines)
            {
                inlines.Add(new Run(LanguageCatalog.Display(fact.Label) + ": "));
                inlines.Add(new Run(fact.Value));
            }
            else row.Text = $"{fact.Label}: {fact.Value}";

            PerformanceFacts.Children.Add(row);
        }

        PerformanceLines.Children.Clear();
        foreach (var line in PerformanceReportText.Describe(result))
            PerformanceLines.Children.Add(new TextBlock
            {
                Text = LanguageCatalog.Display(line),
                Theme = Look("Hint"),
                TextWrapping = TextWrapping.Wrap
            });
    }

    private void OnToggleAiDetails(object? sender, RoutedEventArgs e) => SetAiDetails(!AiDetails.IsVisible);

    private void SetAiDetails(bool visible)
    {
        AiDetails.IsVisible = visible;
        TxtAiHint.IsVisible = !visible;
        BtnAiDetails.Content = visible ? "▴" : "▾";
    }

    private async void OnCopyPrompt(object? sender, RoutedEventArgs e)
    {
        SetAiDetails(true);
        if (_info is null || _autoPlan is null)
        {
            TxtAiStatus.Text = Say("main.ai.load-first");
            return;
        }

        try
        {
            if (Clipboard is null) throw new InvalidOperationException(Say("main.ai.no-clipboard"));
            await Clipboard.SetTextAsync(PromptBuilder.Build(_info, CurrentOptions(), _autoPlan));
            TxtAiStatus.Text = Say("main.ai.prompt-copied");
        }
        catch (Exception ex)
        {
            TxtAiStatus.Text = $"{Say("main.ai.clipboard-failed")}: {ex.Message}";
        }
    }

    private void OnApplyJson(object? sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null)
        {
            TxtAiStatus.Text = Say("main.ai.load-first");
            return;
        }

        var result = PlanParser.Parse(TxtAiJson.Text ?? "", _info, CurrentOptions());
        if (!result.Ok)
        {
            _aiPlan = null;
            TxtAiStatus.Text = Say("main.ai.rejected") + string.Join("\n• ", result.Errors);
            RefreshPlanView();
            return;
        }

        _aiPlan = result.Plan;
        BtnRevert.IsVisible = true;
        var differences = _autoPlan.DescribeDifferences(_aiPlan!).ToList();
        TxtAiStatus.Text = differences.Count == 0
            ? Say("main.ai.same-decision")
            : Say("main.ai.changes") + string.Join("\n• ", differences);
        if (result.Warnings.Count > 0)
            TxtAiStatus.Text += Say("main.ai.warnings") + string.Join("\n• ", result.Warnings);
        RefreshPlanView();
    }

    private void OnRevertAuto(object? sender, RoutedEventArgs e)
    {
        _aiPlan = null;
        BtnRevert.IsVisible = false;
        TxtAiStatus.Text = Say("main.ai.reverted");
        RefreshPlanView();
    }

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (_info is null || ActivePlan is null || _cts is not null) return;

        var output = BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4");
        var targetMb = ParseTargetMb();
        if (DiskSpaceGuard.TryGetFreeBytes(output, out var freeBytes) && !DiskSpaceGuard.HasEnoughSpace(freeBytes, targetMb))
        {
            var neededMb = DiskSpaceGuard.RequiredBytes(targetMb) / 1024.0 / 1024.0;
            TxtResult.Text = Say("main.run.no-space", Num(neededMb, "0"));
            return;
        }

        _probeCts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            SetRunning(true);
            TxtResult.Text = "";
            BtnReveal.IsVisible = false;
            ResetShare(false);
            HideRetryAsk();
            var progress = new Progress<EncodeProgress>(p =>
            {
                Progress.Value = p.Fraction;
                SetStage(TxtStage, LocalizeStage(p.Stage));
                TxtRemaining.Text = p.Remaining?.ToString(@"mm\:ss") ?? "-";
                if (p.OutputMb > 0) TxtOutSize.Text = $"{p.OutputMb:0.0} MB";
            });

            var result = await new EncodeRunner().RunAsync(_info, ActivePlan, output, targetMb, progress, cts.Token, CurrentOptions().FillPolicy, _profile, AskBeforeRetryAsync, _sceneMap?.Map);
            _lastOutput = result.OutputPath;
            RefreshPreviewSource();

            if (result.Success)
            {
                TxtOutSize.Text = $"{result.OutputMb:0.0} MB";
                var saved = 100 - result.OutputMb / _info.FileSizeMb * 100;
                TxtResult.Text = Say("main.run.done",
                    result.Attempts, Num(_info.FileSizeMb, "0.0"), Num(result.OutputMb, "0.0"), Num(saved, "0.#"));
            }
            else if (result.CeilingExceeded)
            {
                TxtOutSize.Text = "-";
                TxtResult.Text = Say("main.run.over-ceiling",
                    Num(targetMb, "0.##"), result.Attempts, Num(result.OutputMb, "0.0"));
            }
            else
            {
                TxtOutSize.Text = "-";
                TxtResult.Text = Say("main.run.ended");
            }

            BtnReveal.IsVisible = result.Success;
            ResetShare(result.Success);
        }
        catch (OperationCanceledException)
        {
            TxtResult.Text = Say("main.run.cancelled");
        }
        catch (Exception ex)
        {
            TxtResult.Text = DescribeFailure(ex);
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            HideRetryAsk();
            SetRunning(false);
            RefreshConversion();
        }
    }

    // The engine stops after an attempt that lands over the target and hands the decision here.
    // Nothing blocks: the panel is shown, the awaited task completes when a button is pressed.
    private async Task<bool> AskBeforeRetryAsync(RetryPrompt prompt, CancellationToken ct)
    {
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _retryDecision = decision;

        await Dispatcher.UIThread.InvokeAsync(() => ShowRetryAsk(prompt));
        using var cancellation = ct.Register(() => decision.TrySetCanceled(ct));

        try
        {
            return await decision.Task;
        }
        finally
        {
            _retryDecision = null;
            await Dispatcher.UIThread.InvokeAsync(HideRetryAsk);
        }
    }

    private void ShowRetryAsk(RetryPrompt prompt)
    {
        _activeRetryPrompt = prompt;
        TxtOutSize.Text = $"{Num(prompt.ActualMb, "0.0")} MB";

        TxtRetryOutcome.Text = Say("main.retry.outcome",
            prompt.Attempt,
            prompt.MaxAttempts,
            Num(prompt.ActualMb, "0.0"),
            Num(prompt.TargetMb, "0.##"),
            Num(prompt.OverMb, "0.0"),
            Num(prompt.OverPercent, "0.#"),
            prompt.AttemptDuration.ToString(@"mm\:ss", CultureInfo.InvariantCulture));

        TxtRetryMeaning.Text = prompt.HasUnderBandFallback
            ? Say("main.retry.meaning-with-fallback", Num(prompt.FallbackMb, "0.0"))
            : Say("main.retry.meaning-without-fallback");
        RetryAskPanel.IsVisible = true;
        SetStage(TxtStage, Say("main.output.waiting"));
    }

    private void HideRetryAsk()
    {
        _activeRetryPrompt = null;
        RetryAskPanel.IsVisible = false;
    }

    private void OnRetryAgain(object? sender, RoutedEventArgs e) => _retryDecision?.TrySetResult(true);

    private void OnRetryStop(object? sender, RoutedEventArgs e) => _retryDecision?.TrySetResult(false);

    private ConversionPlan ReadConversionPlan()
    {
        var container = SelectedTag(CmbContainer) ?? ConversionDefaults.Container;
        var codec = SelectedTag(CmbConvertCodec) ?? ConversionDefaults.VideoCodec;
        int.TryParse(TxtQuality.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality);
        int.TryParse(TxtAudioBitrate.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var audioK);

        int? width = null, height = null;
        var resolutionTag = SelectedTag(CmbResolution);
        if (resolutionTag == "custom")
        {
            var parts = (TxtCustomResolution.Text ?? "").ToLowerInvariant().Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)) (width, height) = (w, h);
        }
        else if (resolutionTag is not null && int.TryParse(resolutionTag, out var fixedHeight))
        {
            height = fixedHeight;
        }

        double? fps = null;
        var fpsTag = SelectedTag(CmbConvertFps);
        if (fpsTag == "custom")
        {
            if (double.TryParse(TxtCustomFps.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var custom)) fps = custom;
        }
        else if (fpsTag is not null && double.TryParse(fpsTag, NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedFps))
        {
            fps = fixedFps;
        }

        var audio = SelectedTag(CmbConvertAudio);
        if (new ConversionPlan { Container = container }.AudioOnly) audio = AudioCodecForAudioContainer(container);

        return new ConversionPlan
        {
            Container = container,
            VideoCodec = codec,
            QualityMode = CmbQualityMode.SelectedIndex == 1 ? ConversionQualityMode.Bitrate : ConversionQualityMode.Crf,
            Crf = quality > 0 ? quality : ConversionDefaults.Crf,
            VideoBitrateK = quality > 0 ? quality : ConversionDefaults.VideoBitrateK,
            Width = width,
            Height = height,
            Fps = fps,
            AudioCodec = audio,
            AudioBitrateK = audioK > 0 ? audioK : ConversionDefaults.AudioBitrateK,
            Start = ParseTime(TxtTrimStart.Text),
            End = ParseTime(TxtTrimEnd.Text),
            HdrPolicy = CmbHdrPolicy.SelectedIndex == 1 ? HdrPolicy.TonemapToSdr : HdrPolicy.Preserve
        };
    }

    private static string AudioCodecForAudioContainer(string container) => container switch
    {
        "mp3" => "libmp3lame",
        "wav" => "pcm_s16le",
        _ => "aac"
    };

    private static TimeSpan? ParseTime(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : TimeSpan.MinValue;

    private (int Min, int Max) CrfLimits()
        => CodecModel.CrfRange(SelectedTag(CmbConvertCodec) ?? ConversionDefaults.VideoCodec);

    private void OnQualitySliderChanged()
    {
        if (_syncing) return;
        _syncing = true;
        TxtQuality.Text = Math.Round(SliderQuality.Value).ToString(CultureInfo.InvariantCulture);
        _syncing = false;
        RefreshConversion();
    }

    private void OnQualityTextChanged()
    {
        if (_syncing) return;
        ApplyQualityRange();
        RefreshConversion();
    }

    private void OnQualityRangeChanged()
    {
        if (_syncing) return;
        ApplyQualityRange();
        RefreshConversion();
    }

    private void ApplyQualityRange()
    {
        if (!int.TryParse(TxtQuality.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return;
        _syncing = true;
        var crfMode = CmbQualityMode.SelectedIndex != 1;
        var (min, max) = CrfLimits();
        SliderQuality.Maximum = crfMode ? max : Math.Max(10000, value);
        SliderQuality.Minimum = crfMode ? min : 50;
        SliderQuality.Value = Math.Clamp(value, SliderQuality.Minimum, SliderQuality.Maximum);
        _syncing = false;
    }

    private void OnConvertChanged()
    {
        if (_syncing) return;
        RefreshConversion();
    }

    private void RefreshConversion()
    {
        if (_info is null) return;
        var plan = ReadConversionPlan();
        var errors = ConversionArguments.Validate(_info, plan).Select(message => LanguageCatalog.Validation(message)).ToList();
        if (plan.Start == TimeSpan.MinValue || plan.End == TimeSpan.MinValue)
            errors.Add(LanguageCatalog.Validation(LanguageCatalog.TrimFormatError));

        var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        TxtConvertValidation.Text = errors.Count == 0 ? Say("main.convert.ready") : string.Join("\n", errors);
        BtnConvert.IsEnabled = errors.Count == 0 && _cts is null;

        try
        {
            TxtConvertCommand.Text = errors.Count == 0
                ? FfmpegArguments.ToCommandLine(ConversionArguments.Build(_info, plan, output, plan.Gif ? "palette.png" : null))
                : "";
        }
        catch (Exception ex)
        {
            TxtConvertValidation.Text = DescribeFailure(ex);
            BtnConvert.IsEnabled = false;
        }
    }

    private async void OnConvert(object? sender, RoutedEventArgs e)
    {
        if (_info is null || _cts is not null) return;

        var plan = ReadConversionPlan();
        var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            SetRunning(true);
            TxtConvertResult.Text = "";
            BtnConvertReveal.IsVisible = false;
            var progress = new Progress<EncodeProgress>(p =>
            {
                ConvertProgress.Value = p.Fraction;
                SetStage(TxtConvertStage, LocalizeStage(p.Stage));
            });

            var result = await new EncodeRunner().ConvertAsync(_info, plan, output, progress, cts.Token);
            _lastOutput = result.OutputPath;
            RefreshPreviewSource();
            TxtConvertResult.Text = Say("main.run.converted", Num(_info.FileSizeMb, "0.0"), Num(result.OutputMb, "0.0"));
            BtnConvertReveal.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            TxtConvertResult.Text = Say("main.run.cancelled");
        }
        catch (Exception ex)
        {
            TxtConvertResult.Text = DescribeFailure(ex);
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            SetRunning(false);
            RefreshConversion();
        }
    }

    private string DescribeFailure(Exception ex)
    {
        var raw = ex.Message ?? "";
        var lead = ClassifyFailure(raw) ?? Say("main.error.generic");
        return raw.Length == 0 ? lead : $"{lead}\n{raw}";
    }

    private string? ClassifyFailure(string raw)
    {
        if (Mentions(raw, "no space left", "not enough space", "enospc", "disk full", "insufficient disk space"))
            return Say("main.error.disk-full");

        if (Mentions(raw, "unknown encoder", "encoder not found", "does not support", "could not write header",
                "error initializing output stream", "automatic encoder selection failed", "incorrect codec parameters",
                "invalid argument", "muxer does not support"))
            return Say("main.error.encoder-container");

        if (Mentions(raw, "invalid data found", "moov atom not found", "could not find codec parameters",
                "decoder not found", "no such file or directory", "end of file", "unknown format"))
            return Say("main.error.undecodable");

        return null;
    }

    private static bool Mentions(string text, params string[] needles)
        => needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private void SetRunning(bool running)
    {
        BtnStart.IsEnabled = !running && _info is not null;
        BtnConvert.IsEnabled = !running && _info is not null;
        BtnCancel.IsEnabled = BtnConvertCancel.IsEnabled = running;
        if (running) return;
        SetStage(TxtStage, Say("main.output.idle"));
        SetStage(TxtConvertStage, Say("main.output.idle"));
        TxtRemaining.Text = "-";
    }

    /// <summary>
    /// Motorun aşama satırı ("Attempt 2 · encoding") sayı ve İngilizce sözcük karışımıdır.
    /// Sözcükler motorun kendi belirteçleri: ekrana çıkarken karşılıklarıyla değiştirilir,
    /// karşılık sözlükten gelir.
    /// </summary>
    private static readonly (string Token, string Key)[] StageWords =
    {
        ("GIF palette", "main.stage.gif-palette"),
        ("GIF encode", "main.stage.gif-encode"),
        ("encoding", "main.stage.encoding"),
        ("converting", "main.stage.converting"),
        ("pass", "main.stage.pass"),
        ("attempt", "main.stage.attempt")
    };

    private static string LocalizeStage(string stage)
    {
        foreach (var (token, key) in StageWords)
            stage = stage.Replace(token, Strings.Get(key), StringComparison.OrdinalIgnoreCase);

        return LanguageCatalog.Display(stage);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _probeCts?.Cancel();
        _cts?.Cancel();
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        if (_lastOutput is null || !File.Exists(_lastOutput)) return;
        try
        {
            Platform.Reveal(_lastOutput);
        }
        catch (Exception ex)
        {
            TxtSystemStatus.Text = $"{Say("main.error.folder")}: {ex.Message}";
        }
    }
}

/// <summary>Skorun neye dayandığı. Panel üçünü ayrı kelimeyle söyler.</summary>
internal enum QualityBasis
{
    /// <summary>Video yüklü değil; skor yok.</summary>
    NoSource,

    /// <summary>Kalibrasyon ölçümü çalıştı, skor ölçüme dayanıyor.</summary>
    Measured,

    /// <summary>Ölçüm yok; skor kaynak bit hızından türetilmiş tahmin.</summary>
    Estimated,

    /// <summary>Kaynak zaten hedefin altında, yeniden kodlama yok.</summary>
    SourceUnderTarget
}

/// <summary>
/// Bir yonganın kalite paneline giren veri. Denetim burada bitiyor: panel yalnız bu
/// kaydı çizer, karar vermez.
///
/// <see cref="For"/> ffprobe çağırmaz: ölçülmüş bir profil verilmişse o kullanılır,
/// verilmemişse <see cref="PlanCalculator.BuildDetailed"/> kendi içinde
/// <see cref="ComplexityProfile.FromSourceBitrate"/>'a düşer; ikisi de saf aritmetik.
///
/// "ffmpeg de çağırmaz" cümlesi T130'a kadar yanlıştı — <c>BuildDetailed</c> kodlayıcı
/// yeteneğini soruyor, o da süreç doğuruyordu. Artık arayüz bu çağrıya gerçek yetenek
/// nesnesini değil <see cref="MainWindow.DeferredEncoderAvailability"/> geçidini
/// veriyor; geçit yalnız ısıtılmış cevabı okur.
/// </summary>
internal readonly record struct QualityHint(double? TargetMb, double? Score, QualityBasis Basis)
{
    /// <summary>Kaynak kalitesi 100 sayılır; kayıp o tavana göre okunur.</summary>
    private const double SourceQualityScore = 100.0;

    internal double LossPoints => Score is { } score ? SourceQualityScore - score : 0;

    internal static QualityHint None { get; } = new(null, null, QualityBasis.NoSource);

    internal static QualityHint For(
        MediaInfo? info,
        PlanOptions options,
        double? targetMb,
        ComplexityProfile? profile,
        IEncoderAvailability? availability)
    {
        if (info is null || targetMb is not { } target || target <= 0) return None;

        options.TargetMb = target;
        var result = PlanCalculator.BuildDetailed(info, options, profile, availability);
        var codes = result.Plan.ReasonCodes.Select(note => note.Code).ToList();

        var basis = codes.Contains(ReasonCode.PredictedQualityMeasured)
            ? QualityBasis.Measured
            : codes.Contains(ReasonCode.PredictedQualityEstimated)
                ? QualityBasis.Estimated
                : QualityBasis.SourceUnderTarget;

        return new QualityHint(target, result.PredictedQuality, basis);
    }
}

/// <summary>
/// Tek bir paylaşım hedefi. Alan adları <c>paylasim-hedefleri.json</c> şemasıyla birebir
/// aynı; şema T35'te sabitlendi ve iki taraf da onu okuyor.
/// </summary>
internal sealed record ShareTarget(
    string Id,
    string DisplayName,
    long MaxBytes,
    IReadOnlyList<int> RetentionDays,
    int DefaultRetentionDays,
    int? FixedRetentionHours,
    bool CanDelete,
    bool PlaysInBrowser);

/// <summary>
/// Hedef listesi arayüze koddan değil dosyadan gelir: JSON'a üçüncü bir hedef eklenince
/// açılır kutuda kendiliğinden belirir ve tek satır C# değişmez.
///
/// Dosyayı T35 yazıyor. Henüz yoksa <see cref="Fallback"/> devreye girer — bunlar
/// sözleşmede yazılı şema varsayılanlarıdır, uydurma değer değil.
/// </summary>
internal sealed record ShareTargetTable(string DefaultId, IReadOnlyList<ShareTarget> Targets)
{
    internal const string FileName = "paylasim-hedefleri.json";

    /// <summary>Ölçülmüş tavan: uguu.se ana sayfası "Max upload size is 128 MiB" diyor.</summary>
    internal const long UguuMaxBytes = 134_217_728;

    /// <summary>storage.to'nun ilan ettiği 25 GB tavanı.</summary>
    internal const long StorageToMaxBytes = 26_843_545_600;

    internal static ShareTargetTable Fallback { get; } = new(
        "storage.to",
        new[]
        {
            new ShareTarget("storage.to", "storage.to", StorageToMaxBytes,
                new[] { 1, 2, 3, 4, 5, 6, 7 }, 3, null, true, true),
            new ShareTarget("uguu.se", "uguu.se", UguuMaxBytes,
                Array.Empty<int>(), 0, 3, false, true)
        });

    internal ShareTarget Default =>
        Targets.FirstOrDefault(target => string.Equals(target.Id, DefaultId, StringComparison.Ordinal))
        ?? Targets[0];

    /// <summary>
    /// Dosya çalışma dizininin yanında da olabilir, depo kökünde de. Yayında ikisi aynı yer;
    /// geliştirmede exe <c>bin/</c> altındadır, bu yüzden üst dizinler taranır.
    /// </summary>
    internal static string? Locate(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    internal static ShareTargetTable Load()
    {
        try
        {
            var path = Locate(AppContext.BaseDirectory);
            return path is null ? Fallback : Parse(File.ReadAllText(path));
        }
        catch (Exception)
        {
            // Dosya bozuksa arayüz açılmaya devam eder; şema varsayılanları gösterilir.
            return Fallback;
        }
    }

    internal static ShareTargetTable Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var targets = new List<ShareTarget>();
        if (root.TryGetProperty("targets", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
                if (ReadTarget(item) is { } target)
                    targets.Add(target);

        if (targets.Count == 0) return Fallback;

        var defaultId = root.TryGetProperty("default", out var chosen) && chosen.ValueKind == JsonValueKind.String
            ? chosen.GetString() ?? targets[0].Id
            : targets[0].Id;

        return new ShareTargetTable(defaultId, targets);
    }

    private static ShareTarget? ReadTarget(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        if (!item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) return null;

        var identifier = id.GetString();
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var days = new List<int>();
        if (item.TryGetProperty("retentionDays", out var retention) && retention.ValueKind == JsonValueKind.Array)
            foreach (var day in retention.EnumerateArray())
                if (day.TryGetInt32(out var value))
                    days.Add(value);

        int? fixedHours = item.TryGetProperty("fixedRetentionHours", out var hours) && hours.TryGetInt32(out var hoursValue)
            ? hoursValue
            : null;

        return new ShareTarget(
            identifier,
            item.TryGetProperty("displayName", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() ?? identifier
                : identifier,
            item.TryGetProperty("maxBytes", out var max) && max.TryGetInt64(out var maxValue) ? maxValue : 0,
            days,
            item.TryGetProperty("defaultRetentionDays", out var fallbackDay) && fallbackDay.TryGetInt32(out var dayValue)
                ? dayValue
                : days.FirstOrDefault(),
            fixedHours,
            item.TryGetProperty("canDelete", out var canDelete) && canDelete.ValueKind == JsonValueKind.True,
            item.TryGetProperty("playsInBrowser", out var plays) && plays.ValueKind == JsonValueKind.True);
    }
}

/// <summary>
/// Paylaş düğmesinin arkasındaki iş: yükle, kaydı tut, iptal et, yayını kapat. Pencere
/// yalnız ilerlemeyi ve sonucu çizer.
/// </summary>
/// <remarks>
/// Sağlayıcı dışarıdan veriliyor, çünkü ölçüm ağa çıkmadan koşmalı: sahte taşıyıcıya bağlı
/// gerçek sağlayıcı verilir ve tavan denetimi, iptal, hata sınıflandırması aynı kodla ölçülür.
/// Tavanı aşan dosyayı sağlayıcı zaten yüklemeye kalkışmadan reddeder; burada ikinci bir
/// denetim yok, tek karar yeri <c>ShareErrorClassifier.CheckSize</c>.
/// </remarks>
internal sealed class ShareFlow
{
    private readonly Func<CoreShare.ShareTarget, CoreShare.IShareProvider> _provider;
    private readonly CoreShare.ShareLedger _ledger;
    private CancellationTokenSource? _upload;

    internal ShareFlow(
        Func<CoreShare.ShareTarget, CoreShare.IShareProvider> provider,
        CoreShare.ShareLedger? ledger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _ledger = ledger ?? new CoreShare.ShareLedger();
    }

    /// <summary>Açık olan son paylaşım. Kapatıldığında ya da hiç yapılmadığında boştur.</summary>
    internal CoreShare.ShareLink? Link { get; private set; }

    internal bool Running => _upload is not null;

    /// <summary>Elde silme jetonu var mı; silme düğmesi buna bakar.</summary>
    internal bool CanDelete => Link is { CanDelete: true };

    internal void Cancel() => _upload?.Cancel();

    internal async Task<CoreShare.ShareResult> ShareAsync(
        CoreShare.ShareTarget target,
        string filePath,
        int? retentionDays = null,
        IProgress<CoreShare.UploadProgress>? progress = null)
    {
        if (_upload is not null)
            return CoreShare.ShareResult.Failed(new CoreShare.ShareDiagnosis(
                CoreShare.ShareFailure.Unknown,
                Strings.Get("settings.share.upload-busy")));

        var cts = new CancellationTokenSource();
        _upload = cts;
        try
        {
            var result = await _provider(target).UploadAsync(filePath, retentionDays, progress, cts.Token);
            if (result.Ok && result.Link is { } link)
            {
                Link = link;
                TryRecord(link);
            }

            return result;
        }
        finally
        {
            _upload = null;
            cts.Dispose();
        }
    }

    /// <summary>
    /// Yayını kapatır. Jeton geçersizse silinecek bir şey kalmamıştır; kayıt yine düşürülür,
    /// yoksa kapatılamayan bir satır orada kalırdı.
    /// </summary>
    internal async Task<CoreShare.ShareResult> DeleteAsync(
        CoreShare.ShareTarget target,
        CancellationToken cancellationToken = default)
    {
        if (Link is not { } link)
            return CoreShare.ShareResult.Failed(new CoreShare.ShareDiagnosis(
                CoreShare.ShareFailure.Unknown, Strings.Get("settings.share.nothing-to-close")));

        var result = await _provider(target).DeleteAsync(link, cancellationToken);
        if (!result.Ok && result.Failure != CoreShare.ShareFailure.TokenExpired) return result;

        TryForget(link.FileId);
        Link = null;
        return result;
    }

    // Kayıt defteri yazılamazsa paylaşım yine de başarılıdır; kaybedilen tek şey uygulama
    // kapanıp açıldıktan sonra yayını kapatabilme imkânı.
    private void TryRecord(CoreShare.ShareLink link)
    {
        try { _ledger.Add(link); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private void TryForget(string fileId)
    {
        try { _ledger.Remove(fileId); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
