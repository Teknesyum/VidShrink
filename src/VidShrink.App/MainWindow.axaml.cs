using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
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
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public enum DropVisual { Idle, Accept, Reject }

public partial class MainWindow : Window
{
    private const double WhatsAppTargetMb = 16;
    private const double MacTrafficLightInset = 80;
    private const int CalibrationRounds = 2;
    private const uint ClientAreaAnimationQuery = 0x1042;

    private const string HardwareTipEnglish = "The graphics card encodes many times faster than the processor. VidShrink picks the best encoder your card offers, and on a modern card the AV1 encoder reaches nearly the same quality as the software encoder at about seven times the speed. On older cards the speed still arrives, but it costs some quality per megabyte.";
    private const string NoHardwareTipEnglish = "No usable hardware encoder was found on this computer, so fast shrink is unavailable. The graphics card would normally encode many times faster than the processor.";

    private static readonly string[] MediaExtensions =
    {
        "mp4", "mkv", "mov", "avi", "webm", "wmv", "flv", "m4v", "mpg", "mpeg", "ts", "m2ts",
        "3gp", "ogv", "vob", "asf", "rm", "rmvb", "divx", "mxf", "f4v", "mts", "dav", "gif"
    };

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
    private CancellationTokenSource? _probeCts;
    private SizeEstimate? _estimate;
    private IEncoderAvailability? _encoders;
    private double _predictedQuality;
    private StrategyAdvice? _advice;
    private string? _lastOutput;
    private string? _ffmpegVersion;
    private bool _syncing;
    private bool _turkish;
    private bool _languageApplied;
    private bool _hardwareProbed;
    private bool _hardwareEncoderAvailable;
    private bool _motionReduced;
    private DropVisual _dropVisual = DropVisual.Idle;
    private DispatcherTimer? _recalculateTimer;
    private DateTime _lastEstimatePulse = DateTime.MinValue;
    private bool _controlsReady;

    private EncodePlan? ActivePlan => _aiPlan ?? _autoPlan;

    public MainWindow()
    {
        InitializeComponent();
        _controlsReady = true;

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
        foreach (var box in new[] { CmbIntent, CmbCodec, CmbFillPolicy, CmbHdrPolicy })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnOptionChanged);
        foreach (var check in new[] { ChkResolution, ChkFps, ChkFastGpu })
            Watch(check, ToggleButton.IsCheckedProperty, OnOptionChanged);

        Watch(SliderQuality, RangeBase.ValueProperty, OnQualitySliderChanged);
        Watch(TxtQuality, TextBox.TextProperty, OnQualityTextChanged);
        foreach (var box in new[] { CmbQualityMode, CmbConvertCodec })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnQualityRangeChanged);
        foreach (var box in new[] { CmbContainer, CmbResolution, CmbConvertFps, CmbConvertAudio })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnConvertChanged);
        foreach (var field in new[] { TxtCustomResolution, TxtCustomFps, TxtAudioBitrate, TxtTrimStart, TxtTrimEnd })
            Watch(field, TextBox.TextProperty, OnConvertChanged);

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
            if (control.IsVisible && control.Opacity > 0.99) return;
            control.Opacity = 0;
            control.IsVisible = true;
            Dispatcher.UIThread.Post(() => control.Opacity = 1, DispatcherPriority.Loaded);
            return;
        }

        if (!control.IsVisible) return;
        control.Opacity = 0;
        DispatcherTimer.RunOnce(() => { if (control.Opacity < 0.01) control.IsVisible = false; }, Motion("MotionBase", 240));
    }

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
            WindowShell.Margin = OffScreenMargin;
            SetLanguage(true);
            PlayPanelEntrance();
            var startupFile = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);
            if (startupFile is not null) await LoadAsync(startupFile);
            await LoadFfmpegVersionAsync();
            await ProbeHardwareEncodersAsync();
        }
        catch (Exception ex)
        {
            ReportSourceError($"{T("Açılış tamamlanamadı", "Startup did not finish")}: {ex.Message}");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_controlsReady) return;
        if (change.Property == WindowStateProperty) UpdateMaximizeGlyph();
        else if (change.Property == OffScreenMarginProperty) WindowShell.Margin = OffScreenMargin;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _probeCts?.Cancel();
        _cts?.Cancel();
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
        catch (Exception ex) { TxtSystemStatus.Text = $"{T("Bağlantı açılamadı", "The link could not be opened")}: {ex.Message}"; }
    }

    private string T(string turkish, string english) => _turkish ? turkish : english;

    private string Localize(string english)
        => _turkish && LanguageCatalog.EnglishToTurkish.TryGetValue(english, out var turkish) ? turkish : english;

    private void OnTurkish(object? sender, RoutedEventArgs e) => SetLanguage(true);
    private void OnEnglish(object? sender, RoutedEventArgs e) => SetLanguage(false);

    private void SetLanguage(bool turkish)
    {
        if (_languageApplied && _turkish == turkish) return;
        var translations = turkish ? LanguageCatalog.EnglishToTurkish : LanguageCatalog.TurkishToEnglish;
        var wasSyncing = _syncing;
        _syncing = true;
        TranslateTree(this, translations, new HashSet<StyledElement>());
        _syncing = wasSyncing;
        _turkish = turkish;
        _languageApplied = true;
        BtnTr.IsEnabled = !turkish;
        BtnEn.IsEnabled = turkish;
        ApplyFastGpuTip();
        UpdateToolStatus();
        if (_info is not null) { ShowInfo(_info); Recalculate(); RefreshConversion(); }
    }

    private void TranslateTree(StyledElement node, IReadOnlyDictionary<string, string> translations, HashSet<StyledElement> visited)
    {
        if (!visited.Add(node)) return;

        switch (node)
        {
            case TextBlock text when text.Text is { } value && translations.TryGetValue(value, out var translatedText):
                text.Text = translatedText;
                break;
            case HeaderedContentControl header when header.Header is string headerValue && translations.TryGetValue(headerValue, out var translatedHeader):
                header.Header = translatedHeader;
                break;
            case ContentControl content when content.Content is string contentValue && translations.TryGetValue(contentValue, out var translatedContent):
                content.Content = translatedContent;
                break;
        }

        if (node is Control control)
        {
            var tip = ToolTip.GetTip(control);
            if (tip is string tipText && translations.TryGetValue(tipText, out var translatedTip)) ToolTip.SetTip(control, translatedTip);
            else if (tip is StyledElement tipElement) TranslateTree(tipElement, translations, visited);
        }

        if (node is ItemsControl items)
            foreach (var item in items.Items.OfType<StyledElement>()) TranslateTree(item, translations, visited);

        if (node is ContentControl owner && owner.Content is StyledElement contentElement) TranslateTree(contentElement, translations, visited);

        foreach (var child in node.GetLogicalChildren().OfType<StyledElement>()) TranslateTree(child, translations, visited);

        if (node is ComboBox combo && combo.SelectedIndex >= 0)
        {
            var index = combo.SelectedIndex;
            combo.SelectedIndex = -1;
            combo.SelectedIndex = index;
        }
    }

    private void ApplyFastGpuTip()
        => TipFastGpu.Text = Localize(_hardwareProbed && !_hardwareEncoderAvailable ? NoHardwareTipEnglish : HardwareTipEnglish);

    private async Task LoadFfmpegVersionAsync()
    {
        if (_ffmpegVersion is not null) return;
        try { _ffmpegVersion = await Task.Run(ToolLocator.GetFfmpegVersion); }
        catch (Exception ex) { _ffmpegVersion = $"{T("okunamadı", "unavailable")} ({ex.Message})"; }
        UpdateToolStatus();
    }

    private void UpdateToolStatus()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            var message = T(
                $"{missing} bulunamadı. VidShrink'in yanındaki tools/ffmpeg klasörüne koyun veya PATH'e kurun.",
                $"{missing} not found. Put it in tools/ffmpeg next to VidShrink, or install it on PATH.");
            TxtStatusBar.Text = message;
            TxtSystemStatus.Text = message;
            return;
        }

        TxtStatusBar.Text = $"FFmpeg: {T("Hazır", "Ready")} — {ToolLocator.Ffmpeg}";
        TxtSystemStatus.Text = string.Join("\n",
            $"FFmpeg: {ToolLocator.Ffmpeg}",
            $"{T("Sürüm", "Version")}: {_ffmpegVersion ?? T("okunuyor...", "reading...")}",
            $".NET: {Environment.Version}",
            $"VidShrink: {AppVersion()}");
    }

    // AssemblyVersion is only ever "1.0.0.0" — the SDK pads it and drops everything after
    // the patch number. AssemblyInformationalVersion is the one Directory.Build.props feeds,
    // and it carries the commit CI appends, so a user reading the About box can tell two
    // builds of the same version apart.
    private static string AppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "?"
            : informational;
    }

    private async Task ProbeHardwareEncodersAsync()
    {
        var available = false;
        IEncoderAvailability? encoders = null;

        try
        {
            (encoders, available) = await Task.Run(() =>
            {
                var capabilities = EncoderCapabilities.Instance;
                var options = new PlanOptions { TargetMb = WhatsAppTargetMb, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast };
                var plan = PlanCalculator.Build(HardwareProbeSource, options, capabilities);
                return ((IEncoderAvailability?)capabilities, CodecModel.IsHardware(plan.Codec));
            });
        }
        catch (Exception ex)
        {
            encoders = null;
            available = false;
            TxtStatusBar.Text = $"{T("Donanım kodlayıcı yoklaması başarısız", "The hardware encoder probe failed")}: {ex.Message}";
        }

        _encoders = encoders;
        _hardwareProbed = true;
        _hardwareEncoderAvailable = available;
        ChkFastGpu.IsEnabled = available;
        if (!available) ChkFastGpu.IsChecked = false;
        ApplyFastGpuTip();
        Recalculate();
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
                    new FilePickerFileType("Media") { Patterns = MediaExtensions.Select(extension => "*." + extension).ToArray() },
                    FilePickerFileTypes.All
                }
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is not null) await LoadAsync(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{T("Dosya seçilemedi", "The file could not be chosen")}: {ex.Message}");
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

        var (title, hint) = state switch
        {
            DropVisual.Accept => ("Release to load this file", "Any format ffmpeg can open"),
            DropVisual.Reject => ("Only one file at a time", "Folders cannot be dropped"),
            _ => ("Drop a media file here", "Any format ffmpeg can open")
        };

        TxtDropTitle.Text = Localize(title);
        TxtDropHint.Text = Localize(hint);
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
        TxtStatusBar.Text = message;
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
        TxtStatusBar.Text = T("Ffprobe ile inceleniyor...", "Probing with ffprobe...");

        try { _info = await FfprobeClient.ProbeAsync(path); }
        catch (Exception ex)
        {
            _info = null;
            BtnStart.IsEnabled = BtnConvert.IsEnabled = false;
            Fade(InfoGrid, false);
            Fade(DropZone, true);
            ResetPlanView();
            ReportSourceError($"{T("Bu dosya kullanılamıyor", "This file cannot be used")}: {DescribeFailure(ex)}");
            return;
        }

        Fade(DropZone, false);

        _aiPlan = null;
        _profile = null;
        BtnRevert.IsVisible = false;
        TxtAiStatus.Text = "";
        SetAiDetails(false);
        TxtConvertSource.Text = Path.GetFileName(path);
        ShowInfo(_info);

        _syncing = true;
        var suggested = _info.FileSizeMb > WhatsAppTargetMb
            ? WhatsAppTargetMb
            : Math.Max(1, Math.Round(_info.FileSizeMb / 2));
        SliderTarget.Maximum = Math.Max(SliderTarget.Maximum, Math.Ceiling(suggested));
        SliderTarget.Value = suggested;
        TxtTarget.Text = suggested.ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;

        UpdateToolStatus();
        Recalculate();
        RefreshConversion();
        await MeasureComplexityAsync(_info);
    }

    private async Task MeasureComplexityAsync(MediaInfo info)
    {
        _probeCts?.Cancel();
        _probeCts?.Dispose();
        var cts = new CancellationTokenSource();
        _probeCts = cts;

        TxtEstimateNote.Text = T("Karmaşıklık ölçülüyor...", "Measuring complexity...");
        try
        {
            var speed = CurrentOptions().SpeedMode;
            var profile = await ComplexityProbe.RunAsync(info, speed, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
            _profile = profile;
            Recalculate();

            TxtEstimateNote.Text = T("Plan ayarlarıyla kalibre ediliyor...", "Calibrating with the planned settings...");
            var draft = PlanCalculator.BuildDetailed(info, CurrentOptions(), profile, _encoders).Plan;

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

                var settled = PlanCalculator.BuildDetailed(info, CurrentOptions(), calibrated, _encoders).Plan;
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
            if (ReferenceEquals(_info, info)) TxtEstimateNote.Text = $"{T("Ölçüm yapılamadı", "Measurement failed")}: {ex.Message}";
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
        TxtAudio.Text = info.HasAudio ? $"{info.AudioCodec} {info.AudioBitrateBps / 1000}k" : T("Yok", "None");
        TxtBitrate.Text = $"{info.TotalBitrateBps / 1000} kbps";
        TxtHdr.Text = info.IsHdr ? T("Evet", "Yes") : T("Hayır", "No");
        Fade(HdrPolicyPanel, info.IsHdr);
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
        var detailed = PlanCalculator.BuildDetailed(_info, CurrentOptions(), _profile, _encoders);
        _autoPlan = detailed.Plan;
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
                TxtAiStatus.Text = T("AI planı güncel seçeneklerle artık eşleşmiyor; otomatik plan kullanılıyor.", "The AI plan no longer matches the current options; the automatic plan is in use.");
            }
        }

        RefreshPlanView();
        BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
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

        var key = new TextBlock { Text = label, Theme = Look("PlanFactLabel") };
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

        var body = new TextBlock { Text = text, Theme = Look("PlanReasonText") };
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

        var channels = plan.AudioChannels == 1 ? $" {T("tek kanal", "mono")}" : "";
        AddPlanFact("Plan", _aiPlan is null ? T("Otomatik", "Automatic") : "AI");
        AddPlanFact(Localize("Encoder"), plan.Codec);
        AddPlanFact(Localize("Mode"), plan.ModeEnum switch
        {
            EncodeMode.Crf => $"CRF {plan.Crf}",
            EncodeMode.PassThrough => T("kopyala", "stream copy"),
            _ => $"{plan.VideoBitrateK}k · {T("iki geçiş", "two-pass")}"
        });
        AddPlanFact(Localize("Resolution"), $"{plan.Width}x{plan.Height}");
        AddPlanFact(Localize("Frame rate"), $"{plan.Fps:0.##} FPS");
        AddPlanFact(Localize("Audio"), plan.AudioCodec is null ? T("Yok", "None") : $"{plan.AudioCodec} {plan.AudioBitrateK}k{channels}");
        AddPlanFact(Localize("Preset"), plan.Preset);
        AddPlanFact(Localize("Estimated size"), _estimate is { } size ? $"{size.ExpectedMb:0.0} MB" : "-");

        foreach (var line in StrategyLines()) AddPlanReason(line);
        foreach (var line in ReasonLines(plan)) AddPlanReason(line);
        PlanRule.IsVisible = PlanReasons.Children.Count > 0;

        RefreshEstimateView();
        RefreshDurationView();
        TxtCommand.Text = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(_info, plan, BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4"), plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null));
    }

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
        TxtEstimateRange.Text = $"{estimate.LowMb:0.0} - {estimate.HighMb:0.0} MB · {T("Kaynağın", "Of source")} %{estimate.ExpectedMb / Math.Max(_info.FileSizeMb, 0.01) * 100:0.#}";

        var basis = estimate.Measured
            ? T("kaynaktan ölçülen karmaşıklık", "measured source complexity")
            : T("kaynak bit hızından tahmin", "estimated from source bitrate");
        var mode = estimate.Enforced
            ? T("iki geçişli mod boyutu zorlar", "two-pass enforces this size")
            : T("kalite modu; hedefin altında kalır", "quality mode; stays under the target");
        TxtEstimateNote.Text = $"{basis} · {mode} · {T("öngörülen kalite", "predicted quality")} {_predictedQuality:0.#}/100";
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
                ? T("Kodlama hızı bu makinede henüz ölçülmedi.", "The encoding speed has not been measured on this machine yet.")
                : T("Örnekler bu plan ayarlarıyla kodlanmadı; süre tahmin edilmiyor.", "The samples were not encoded with these settings, so no time is shown.");
            return;
        }

        if (duration.StreamCopy)
        {
            TxtDurationValue.Text = T("kopyalanacak", "copied");
            TxtDurationRange.Text = T("Yeniden kodlama yok, dosya olduğu gibi aktarılıyor.", "Nothing is re-encoded; the file is carried over as it is.");
            return;
        }

        var reading = $"~{HumanDuration(duration.ExpectedSeconds)}";
        if (TxtDurationValue.Text != reading) Pulse(TxtDurationValue, true);
        TxtDurationValue.Text = reading;

        var rate = profile.Speed!.FramesPerSecond;
        TxtDurationRange.Text = $"{HumanDuration(duration.LowSeconds)} - {HumanDuration(duration.HighSeconds)} · {T("ölçülen", "measured")} {rate:0} {T("kare/sn", "frames/s")}";
    }

    private string HumanDuration(double seconds)
    {
        if (seconds < 60) return $"{Math.Max(5, Math.Round(seconds / 5.0) * 5.0):0} {T("sn", "s")}";

        if (seconds < 3600)
        {
            var minutes = seconds / 60.0;
            if (minutes < 10) return $"{Math.Max(1.0, Math.Round(minutes * 2.0) / 2.0):0.#} {T("dk", "min")}";
            return $"{Math.Round(minutes):0} {T("dk", "min")}";
        }

        var hours = (int)(seconds / 3600);
        var rest = (int)Math.Round((seconds - hours * 3600.0) / 60.0);
        if (rest >= 60)
        {
            hours++;
            rest = 0;
        }
        return rest == 0 ? $"{hours} {T("sa", "h")}" : $"{hours} {T("sa", "h")} {rest} {T("dk", "min")}";
    }

    private List<string> ReasonLines(EncodePlan plan)
    {
        var parts = new List<string>();
        foreach (var note in plan.ReasonCodes)
        {
            var text = note.Code switch
            {
                ReasonCode.ResolutionScaled => T($"{note.Width}x{note.Height}'e ölçeklendi (kaynağın %{note.ScalePercent:0.#}'i); bu klibin ölçülen ayrıntı düşüşünde bu, öngörülen kaliteyi yükseltecek kadar bit kazandırıyor", $"scaled to {note.Width}x{note.Height} ({note.ScalePercent:0.#}% of source); at this title's measured detail falloff that frees enough bits to raise predicted quality"),
                ReasonCode.FrameRateReduced => T($"kare başına ayrıntıyı korumak için kare hızı {note.Fps:0.##}'e düşürüldü", $"frame rate reduced to {note.Fps:0.##} to keep per-frame detail"),
                ReasonCode.ResolutionRestoredAtCeiling => T($"tavan bütçeyi boşta bıraktığı için çözünürlük {note.Width}x{note.Height}@{note.Fps:0.##}'e geri getirildi — CRF {note.Crf:0}'te hedefe hâlâ sığan en büyük düzen", $"the ceiling left budget unused, so resolution was restored to {note.Width}x{note.Height}@{note.Fps:0.##} — the largest layout that still fits the target at CRF {note.Crf:0}"),
                ReasonCode.BudgetExceedsCeiling => T($"bütçe CRF {note.BudgetCrf:0.#}'e imkan tanıyor, bu amaç için CRF {note.Crf:0} şeffaflık tavanından daha iyi; bu yüzden kodlayıcı tavanda durup dosyayı {note.TargetMb:0.##} MB'a şişirmek yerine yaklaşık {note.Mb:0.0} MB verir", $"the budget affords CRF {note.BudgetCrf:0.#}, better than the CRF {note.Crf:0} transparency ceiling for this intent, so the encoder stops at the ceiling and delivers about {note.Mb:0.0} MB instead of padding the file to {note.TargetMb:0.##} MB"),
                ReasonCode.BudgetBelowCeilingTwoPass => T($"bütçe CRF {note.BudgetCrf:0.#} civarında kalıyor, CRF {note.Crf:0} tavanının altında; bu yüzden iki geçişli VBR {note.TargetMb:0.##} MB'ın tamamını harcıyor", $"the budget lands near CRF {note.BudgetCrf:0.#}, short of the CRF {note.Crf:0} ceiling, so two-pass VBR spends the whole {note.TargetMb:0.##} MB"),
                ReasonCode.PredictedQualityMeasured => T($"öngörülen kalite {note.Score:0.#}/100, ölçülen bir örnekten (bppf {note.Bppf:0.0000}, ayrıntı düşüşü {note.DetailExponent:0.00})", $"predicted quality {note.Score:0.#}/100 from a measured sample (bppf {note.Bppf:0.0000}, detail falloff {note.DetailExponent:0.00})"),
                ReasonCode.PredictedQualityEstimated => T($"öngörülen kalite {note.Score:0.#}/100, kaynak bit hızından tahmin", $"predicted quality {note.Score:0.#}/100 estimated from the source bitrate"),
                ReasonCode.RetryScaled => T($"yeniden deneme: önceki girişim {note.TargetMb:0.##} MB hedefe karşı {note.Mb:0.0} MB üretti; ses için {note.AudioMb:0.00} MB ayrıldıktan sonra video bit hızı {note.Factor:0.###} ile ölçeklendi ve kalan bütçeyle sınırlandı", $"retry: the previous attempt produced {note.Mb:0.0} MB against a {note.TargetMb:0.##} MB target; after reserving {note.AudioMb:0.00} MB for audio, video bitrate was scaled by {note.Factor:0.###} and capped to the remaining budget"),
                ReasonCode.EncoderFallback => T($"{note.RequestedCodec} kodlayıcısı bu ffmpeg sürümünde yok, bu yüzden {note.FallbackCodec}'e düşüldü", $"the {note.RequestedCodec} encoder is not available on this ffmpeg build, so encoding falls back to {note.FallbackCodec}"),
                ReasonCode.HdrTonemapped => T("kaynak HDR ama seçili kodlayıcı 10-bit'i koruyamıyor, bu yüzden BT.709 SDR'ye tone-map edildi", "the source is HDR but the selected encoder cannot preserve 10-bit, so it was tone-mapped to SDR BT.709"),
                ReasonCode.FillCrfLowered => T($"hedefi doldur politikası CRF'yi {note.Crf:0.#}'e düşürdü; şeffaflık tavanında durmak yerine {note.BandLowerMb:0.0}-{note.TargetMb:0.0} MB bandına, {note.Mb:0.0} MB bant merkezine yaklaşıldı", $"the fill target policy lowered CRF to {note.Crf:0.#} instead of stopping at the transparency ceiling, landing near the {note.Mb:0.0} MB band center inside the {note.BandLowerMb:0.0}-{note.TargetMb:0.0} MB band"),
                ReasonCode.FillTwoPassBandCenter => T($"CRF {note.Crf:0} tabanına bandı doldurmadan ulaşıldığı için iki geçişli VBR doğrudan {note.Mb:0.0} MB bant merkezini hedefliyor", $"CRF floor {note.Crf:0} was reached before the fill band, so two-pass VBR targets the {note.Mb:0.0} MB band center directly"),
                ReasonCode.FillTwoPassBandTooNarrowForCrf => T($"tek CRF adımı dosyayı %{note.Factor * 100:0.#} kaydırıyor, bu da %{(note.TargetMb - note.BandLowerMb) / Math.Max(note.TargetMb, 0.01) * 100:0.#}'lik doldurma bandından geniş; CRF banda giremediği için iki geçişli VBR doğrudan {note.Mb:0.0} MB hedefliyor", $"one CRF step moves the file by {note.Factor * 100:0.#}%, wider than the {(note.TargetMb - note.BandLowerMb) / Math.Max(note.TargetMb, 0.01) * 100:0.#}% fill band, so single-pass CRF cannot land inside it and two-pass VBR targets {note.Mb:0.0} MB directly"),
                ReasonCode.HardwareBitrateBias => T($"{note.FallbackCodec} donanım kodlayıcısı bu klip için henüz kalibre edilmedi, bu yüzden hedefin altında kalmak adına bit hızına {note.Factor:0.###} güvenlik payı eklendi", $"the {note.FallbackCodec} hardware encoder is not calibrated for this clip yet, so the bitrate carries a {note.Factor:0.###} safety margin to stay under the target"),
                ReasonCode.SourceAlreadyUnderTarget => T($"kaynak zaten {note.Mb:0.0} MB, {note.TargetMb:0.##} MB hedefin altında; bu yüzden yeniden kodlanmadan olduğu gibi kopyalanıyor", $"the source is already {note.Mb:0.0} MB, under the {note.TargetMb:0.##} MB target, so it is copied as it is instead of being re-encoded"),
                ReasonCode.TargetCappedToSource => T($"hedef {note.Mb:0.##} MB'a çekildi çünkü çıktı yapıldığı dosyadan büyük olamaz ({note.TargetMb:0.##} MB istenmişti)", $"the target was capped to {note.Mb:0.##} MB because the output is never larger than what it was made from ({note.TargetMb:0.##} MB was asked for)"),
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
            CompressionRegime.Light => T("hafif", "light"),
            CompressionRegime.Balanced => T("dengeli", "balanced"),
            CompressionRegime.Aggressive => T("agresif", "aggressive"),
            _ => T("uç", "extreme")
        };

        var lines = new List<string>
        {
            T($"{advice.Ratio:0.#}x küçültme — {regime} senaryo.", $"{advice.Ratio:0.#}x reduction — {regime} scenario.")
        };

        foreach (var note in advice.Notes.Distinct())
        {
            var text = note switch
            {
                AdviceCode.BudgetIsGenerous => T("Hedef bu kaynak için bol; motor bütçeyi zorlanmadan karşılıyor.", "The target is generous for this source, so the engine meets it without strain."),
                AdviceCode.CodecUpgradeRecommended => T("Bu sıkışıklıkta H.265 aynı boyutta gözle görülür şekilde daha iyi sonuç verir; sıkıştırma algoritmasını otomatik veya H.265 yapmayı düşün.", "At this pressure H.265 gives a visibly better result at the same size; consider switching the compression algorithm to automatic or H.265."),
                AdviceCode.HardwareCodecCostsQuality => T("Donanım kodlayıcısı hızlıdır ama bu kadar sıkışık bir hedefte megabayt başına belirgin kalite kaybettirir; hızlı düşür (GPU) kapalıyken sonuç daha iyi görünür.", "The hardware encoder is fast but loses noticeable quality per megabyte at a target this tight; the result looks better with fast shrink (GPU) turned off."),
                AdviceCode.ExtremeRatioWarning => T("Uç sıkıştırma: kayıp kaçınılmaz, motor kaybı en az hissedilecek yere yığıyor.", "Extreme compression: loss is unavoidable, so the engine pushes it where it is least noticeable."),
                AdviceCode.ContentIsSimple => T("İçerik sade ölçüldü; hedefin altında rahat kalınıyor.", "The content measured as simple, so the target is met with room to spare."),
                AdviceCode.ContentIsComplex => T("İçerik yoğun ölçüldü; bit bütçesi bu yüzden zorlanıyor.", "The content measured as detail-heavy, which is why the bit budget is tight."),
                AdviceCode.ScaleSavesMuch => T("Bu klipte çözünürlük düşürmek çok bit kazandırıyor.", "Scaling down frees a lot of bits on this particular clip."),
                AdviceCode.ScaleSavesLittle => T("Bu klipte çözünürlük düşürmek az kazandırıyor; çözünürlük korunuyor.", "Scaling down frees little on this clip, so resolution is preserved."),
                AdviceCode.ResolutionReduced => T("Bütçeye sığmak için çözünürlük düşürüldü.", "Resolution was lowered so the picture fits the budget."),
                AdviceCode.FrameRateReduced => T("Kalan karelere bit kazandırmak için kare hızı düşürüldü.", "Frame rate was lowered to free bits for the frames that remain."),
                AdviceCode.TargetEnforcedTwoPass => T("Hedef boyutu tutturmak için iki geçişli kodlama kullanılıyor.", "Two-pass encoding is used so the target size is actually hit."),
                AdviceCode.QualityCeilingReached => T("Kalite tavanına ulaşıldı; kalan bütçe gözle görülür bir şey satın almayacağı için harcanmıyor.", "The quality ceiling was reached; the remaining budget is left unspent because it would buy nothing you could see."),
                AdviceCode.AudioReduced => T("Görüntüye bit kalsın diye ses bit hızı düşürüldü.", "The audio bitrate was lowered so more bits are left for the picture."),
                AdviceCode.AudioMono => T("Ses tek kanala indirildi — telefon hoparlöründe fark edilmez, görüntüye bit kazandırır.", "Audio was folded to mono — inaudible on a phone speaker, and it buys bits for the picture."),
                AdviceCode.AudioDropped => T("Bu boyutta ses tutulamıyor, çıkarıldı.", "Audio cannot fit at this size and was removed."),
                AdviceCode.EncoderFallback => ChkFastGpu.IsChecked == true
                    ? T("Hızlı düşür (GPU) açık ama çalışan bir donanım kodlayıcı bulunamadı; kodlama yazılım kodlayıcısına düştü ve hız kazancı yok.", "Fast shrink (GPU) is on but no working hardware encoder was found, so encoding fell back to a software encoder and there is no speed gain.")
                    : T("Tercih edilen kodlayıcı bu ffmpeg sürümünde yok; yazılım karşılığına düşüldü.", "The preferred encoder is not available on this ffmpeg build; falling back to a software encoder."),
                AdviceCode.HdrTonemapped => T("Kaynak HDR ama seçili kodlayıcı 10-bit'i koruyamıyor; BT.709 SDR'ye tone-map edildi.", "The source is HDR but the selected encoder cannot preserve 10-bit, so it was tone-mapped to SDR BT.709."),
                _ => null
            };
            if (text is not null) lines.Add(text);
        }

        return lines;
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
        ScheduleRecalculate();
    }

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
            TxtAiStatus.Text = T("Önce bir video yükleyin.", "Load a video first.");
            return;
        }

        try
        {
            if (Clipboard is null) throw new InvalidOperationException(T("Pano sağlayıcısı yok.", "There is no clipboard provider."));
            await Clipboard.SetTextAsync(PromptBuilder.Build(_info, CurrentOptions(), _autoPlan));
            TxtAiStatus.Text = T("İstem kopyalandı. JSON yanıtını aşağıya yapıştırın.", "The prompt was copied. Paste its JSON answer below.");
        }
        catch (Exception ex)
        {
            TxtAiStatus.Text = $"{T("Pano kullanılamıyor", "The clipboard is unavailable")}: {ex.Message}";
        }
    }

    private void OnApplyJson(object? sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null)
        {
            TxtAiStatus.Text = T("Önce bir video yükleyin.", "Load a video first.");
            return;
        }

        var result = PlanParser.Parse(TxtAiJson.Text ?? "", _info, CurrentOptions());
        if (!result.Ok)
        {
            _aiPlan = null;
            TxtAiStatus.Text = T("Reddedildi; otomatik plan kullanılıyor:\n• ", "Rejected; the automatic plan is in use:\n• ") + string.Join("\n• ", result.Errors);
            RefreshPlanView();
            return;
        }

        _aiPlan = result.Plan;
        BtnRevert.IsVisible = true;
        var differences = _autoPlan.DescribeDifferences(_aiPlan!).ToList();
        TxtAiStatus.Text = differences.Count == 0
            ? T("AI otomatik planla aynı kararı verdi.", "The AI agreed with the automatic plan.")
            : T("Otomatik plana göre değişiklikler:\n• ", "Changes vs automatic:\n• ") + string.Join("\n• ", differences);
        if (result.Warnings.Count > 0)
            TxtAiStatus.Text += T("\nUyarılar:\n• ", "\nWarnings:\n• ") + string.Join("\n• ", result.Warnings);
        RefreshPlanView();
    }

    private void OnRevertAuto(object? sender, RoutedEventArgs e)
    {
        _aiPlan = null;
        BtnRevert.IsVisible = false;
        TxtAiStatus.Text = T("Otomatik plana dönüldü.", "Back on the automatic plan.");
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
            TxtResult.Text = T($"Hedef sürücüde yeterli boş alan yok; en az {neededMb:0} MB gerekiyor.", $"Not enough free space on the target drive; at least {neededMb:0} MB is needed.");
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
            var progress = new Progress<EncodeProgress>(p =>
            {
                Progress.Value = p.Fraction;
                SetStage(TxtStage, LocalizeStage(p.Stage));
                TxtRemaining.Text = p.Remaining?.ToString(@"mm\:ss") ?? "-";
                if (p.OutputMb > 0) TxtOutSize.Text = $"{p.OutputMb:0.0} MB";
            });

            var result = await new EncodeRunner().RunAsync(_info, ActivePlan, output, targetMb, progress, cts.Token, CurrentOptions().FillPolicy, _profile);
            _lastOutput = result.OutputPath;

            if (result.Success)
            {
                TxtOutSize.Text = $"{result.OutputMb:0.0} MB";
                var saved = 100 - result.OutputMb / _info.FileSizeMb * 100;
                TxtResult.Text = T(
                    $"{result.Attempts} denemede tamamlandı. {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB (%{saved:0.#} daha küçük).",
                    $"Done in {result.Attempts} attempt(s). {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB ({saved:0.#}% smaller).");
            }
            else if (result.CeilingExceeded)
            {
                TxtOutSize.Text = "-";
                TxtResult.Text = T(
                    $"{result.Attempts} denemede {targetMb:0.##} MB hedefinin altına inilemedi (son sonuç {result.OutputMb:0.0} MB). Dosya teslim edilmedi, çünkü hedeften büyük dosya asla verilmez. Hedefi büyütüp yeniden deneyin.",
                    $"Could not get under the {targetMb:0.##} MB target after {result.Attempts} attempt(s) (last result was {result.OutputMb:0.0} MB). No file was written, because a file larger than the target is never handed back. Raise the target and try again.");
            }
            else
            {
                TxtOutSize.Text = "-";
                TxtResult.Text = T("Kodlama beklenmedik biçimde sonlandı, dosya yazılmadı.", "Encoding ended unexpectedly and no file was written.");
            }

            BtnReveal.IsVisible = result.Success;
        }
        catch (OperationCanceledException)
        {
            TxtResult.Text = T("İptal edildi.", "Cancelled.");
        }
        catch (Exception ex)
        {
            TxtResult.Text = DescribeFailure(ex);
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            SetRunning(false);
            RefreshConversion();
        }
    }

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
        var errors = ConversionArguments.Validate(_info, plan).Select(message => LanguageCatalog.Validation(message, _turkish)).ToList();
        if (plan.Start == TimeSpan.MinValue || plan.End == TimeSpan.MinValue)
            errors.Add(LanguageCatalog.Validation(LanguageCatalog.TrimFormatError, _turkish));

        var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        TxtConvertValidation.Text = errors.Count == 0 ? T("Hazır.", "Ready.") : string.Join("\n", errors);
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
            TxtConvertResult.Text = $"{T("Tamamlandı", "Done")}. {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB.";
            BtnConvertReveal.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            TxtConvertResult.Text = T("İptal edildi.", "Cancelled.");
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
        var lead = ClassifyFailure(raw) ?? T(
            "İşlem tamamlanamadı. Ffmpeg'in bildirdiği ayrıntı aşağıda.",
            "The operation could not be completed. The detail ffmpeg reported is below.");
        return raw.Length == 0 ? lead : $"{lead}\n{raw}";
    }

    private string? ClassifyFailure(string raw)
    {
        if (Mentions(raw, "no space left", "not enough space", "enospc", "disk full", "insufficient disk space"))
            return T(
                "Hedef sürücüde yer kalmadı. Yer açın ya da çıktıyı başka bir sürücüye alın.",
                "The target drive ran out of space. Free some room, or write the output to another drive.");

        if (Mentions(raw, "unknown encoder", "encoder not found", "does not support", "could not write header",
                "error initializing output stream", "automatic encoder selection failed", "incorrect codec parameters",
                "invalid argument", "muxer does not support"))
            return T(
                "Seçilen kodlayıcı ile kapsayıcı birbirine uymuyor. Kapsayıcıyı MP4, video kodeğini H.264 yapıp yeniden deneyin.",
                "The chosen encoder and container do not fit together. Set the container to MP4 and the video codec to H.264, then try again.");

        if (Mentions(raw, "invalid data found", "moov atom not found", "could not find codec parameters",
                "decoder not found", "no such file or directory", "end of file", "unknown format"))
            return T(
                "Kaynak dosya çözülemedi; bozuk olabilir ya da bu ffmpeg sürümü bu biçimi tanımıyor.",
                "The source file could not be decoded; it may be damaged, or this ffmpeg build does not know the format.");

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
        SetStage(TxtStage, T("Boşta", "Idle"));
        SetStage(TxtConvertStage, T("Boşta", "Idle"));
        TxtRemaining.Text = "-";
    }

    private string LocalizeStage(string stage)
    {
        if (!_turkish) return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stage);
        return stage.Replace("encoding", "Kodlama", StringComparison.OrdinalIgnoreCase)
            .Replace("converting", "Dönüştürme", StringComparison.OrdinalIgnoreCase)
            .Replace("pass", "Geçiş", StringComparison.OrdinalIgnoreCase)
            .Replace("attempt", "Deneme", StringComparison.OrdinalIgnoreCase)
            .Replace("GIF palette", "GIF paleti", StringComparison.OrdinalIgnoreCase)
            .Replace("GIF encode", "GIF kodlama", StringComparison.OrdinalIgnoreCase);
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
            TxtSystemStatus.Text = $"{T("Klasör açılamadı", "The folder could not be opened")}: {ex.Message}";
        }
    }
}
