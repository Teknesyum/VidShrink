using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow : Window
{
    private const string MediaFilter = "Media files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.wmv;*.flv;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.3gp;*.ogv;*.vob;*.asf;*.rm;*.rmvb;*.divx;*.mxf;*.f4v;*.mts;*.dav;*.gif|All files|*.*";
    private MediaInfo? _info;
    private EncodePlan? _autoPlan;
    private EncodePlan? _aiPlan;
    private CancellationTokenSource? _cts;
    private ComplexityProfile? _profile;
    private CancellationTokenSource? _probeCts;
    private SizeEstimate? _estimate;
    private double _predictedQuality;
    private StrategyAdvice? _advice;
    private const double WhatsAppTargetMb = 16;
    private string? _lastOutput;
    private bool _syncing;
    private bool _turkish;

    private EncodePlan? ActivePlan => _aiPlan ?? _autoPlan;

    public MainWindow()
    {
        InitializeComponent();
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/VidShrink.App;component/Assets/VidShrink.ico"));
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Width = Math.Min(1440, SystemParameters.WorkArea.Width);
        Height = Math.Min(1000, SystemParameters.WorkArea.Height);
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        Loaded += async (_, _) =>
        {
            SetLanguage(true);
            CheckTools();
            var startupFile = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);
            if (startupFile is not null) await LoadAsync(startupFile);
        };
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximizeRestore();
        else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeRestore(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximizeRestore() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void UpdateMaximizeGlyph() => BtnMaximize.Content = WindowState == WindowState.Maximized ? "❐" : "□";

    private string T(string turkish, string english) => _turkish ? turkish : english;

    private void OnTurkish(object sender, RoutedEventArgs e) => SetLanguage(true);
    private void OnEnglish(object sender, RoutedEventArgs e) => SetLanguage(false);

    private void SetLanguage(bool turkish)
    {
        if (IsLoaded && _turkish == turkish) return;
        var translations = turkish ? LanguageCatalog.EnglishToTurkish : LanguageCatalog.TurkishToEnglish;
        TranslateTree(this, translations, new HashSet<DependencyObject>());
        TaglineLead.Text = turkish ? "Boyut Hedefli Media Sıkıştırma" : "Target Size Media Compression";
        TaglineSeparator.Text = " & ";
        TaglineConverter.Text = "Media Converter";
        _turkish = turkish;
        BtnTr.Opacity = turkish ? 1 : 0.45;
        BtnEn.Opacity = turkish ? 0.45 : 1;
        CheckTools();
        if (_info is not null) { ShowInfo(_info); Recalculate(); RefreshConversion(); }
    }

    private static void TranslateTree(DependencyObject node, IReadOnlyDictionary<string, string> translations, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;
        if (node is TextBlock text && translations.TryGetValue(text.Text, out var translatedText)) text.Text = translatedText;
        if (node is ContentControl content && content.Content is string value && translations.TryGetValue(value, out var translatedContent)) content.Content = translatedContent;
        if (node is HeaderedContentControl header && header.Header is string headerValue && translations.TryGetValue(headerValue, out var translatedHeader)) header.Header = translatedHeader;
        if (node is FrameworkElement element && element.ToolTip is string toolTipText && translations.TryGetValue(toolTipText, out var translatedToolTip)) element.ToolTip = translatedToolTip;
        else if (node is FrameworkElement { ToolTip: DependencyObject toolTip }) TranslateTree(toolTip, translations, visited);
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) TranslateTree(child, translations, visited);
        if (node is Visual)
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++) TranslateTree(VisualTreeHelper.GetChild(node, index), translations, visited);
    }

    private void CheckTools()
    {
        if (ToolLocator.IsAvailable(out var missing))
        {
            TxtStatusBar.Text = $"FFmpeg: {T("Hazır", "Ready")} — {ToolLocator.Ffmpeg}";
            TxtSystemStatus.Text = $"FFmpeg: {ToolLocator.Ffmpeg}\n{T("Sürüm", "Version")}: {ToolLocator.GetFfmpegVersion()}\n.NET: {Environment.Version}\nVidShrink: {Assembly.GetExecutingAssembly().GetName().Version}";
        }
        else
        {
            TxtStatusBar.Text = _turkish
                ? $"{missing} bulunamadı. VidShrink.exe yanındaki tools\\ffmpeg klasörüne koyun veya PATH'e kurun."
                : $"{missing} not found. Put it in tools\\ffmpeg next to VidShrink.exe, or install it on PATH.";
            TxtSystemStatus.Text = TxtStatusBar.Text;
        }
    }

    private PlanOptions CurrentOptions() => new() { TargetMb = ParseTargetMb(), Intent = (Intent)Math.Max(0, CmbIntent.SelectedIndex), Codec = CodecFromIndex(CmbCodec.SelectedIndex), AllowResolutionDrop = ChkResolution.IsChecked == true, AllowFpsDrop = ChkFps.IsChecked == true, HdrPolicy = CmbHdrPolicy.SelectedIndex == 1 ? HdrPolicy.TonemapToSdr : HdrPolicy.Preserve, FillPolicy = CmbFillPolicy.SelectedIndex == 1 ? FillPolicy.QualityCeiling : FillPolicy.FillTarget };
    private double ParseTargetMb() => double.TryParse(TxtTarget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) && mb > 0 ? mb : 16;

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = MediaFilter };
        if (dialog.ShowDialog(this) == true) await LoadAsync(dialog.FileName);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var file = TryGetDroppedFile(e);
        e.Handled = true;
        if (file is not null) await LoadAsync(file);
    }

    private static string? TryGetDroppedFile(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return null;
        return File.Exists(files[0]) ? files[0] : null;
    }

    private async Task LoadAsync(string path)
    {
        TxtFileName.Text = Path.GetFileName(path);
        TxtStatusBar.Text = T("FFprobe İle İnceleniyor...", "Probing With FFprobe...");
        try { _info = await FfprobeClient.ProbeAsync(path); }
        catch (Exception ex)
        {
            _info = null;
            BtnStart.IsEnabled = BtnConvert.IsEnabled = false;
            InfoGrid.Visibility = Visibility.Collapsed;
            TxtStatusBar.Text = $"{T("Bu dosya kullanılamıyor", "This file cannot be used")}: {ex.Message}";
            return;
        }

        _aiPlan = null;
        _profile = null;
        BtnRevert.Visibility = Visibility.Collapsed;
        TxtAiStatus.Text = "";
        TxtConvertSource.Text = Path.GetFileName(path);
        ShowInfo(_info);
        _syncing = true;
        var suggested = _info.FileSizeMb > WhatsAppTargetMb
            ? WhatsAppTargetMb
            : Math.Max(1, Math.Round(_info.FileSizeMb / 2));
        SliderTarget.Maximum = Math.Max(500, Math.Ceiling(suggested));
        SliderTarget.Value = suggested;
        TxtTarget.Text = suggested.ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;
        TxtStatusBar.Text = $"FFmpeg: {T("Hazır", "Ready")} — {ToolLocator.Ffmpeg}";
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
            var profile = await ComplexityProbe.RunAsync(info, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
            _profile = profile;
            Recalculate();

            var draft = PlanCalculator.BuildDetailed(info, CurrentOptions(), profile, EncoderCapabilities.Instance).Plan;
            TxtEstimateNote.Text = T("Plan ayarlarıyla kalibre ediliyor...", "Calibrating with the planned settings...");
            var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_info, info)) return;
            _profile = calibrated;
            Recalculate();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_info, info)) TxtEstimateNote.Text = $"{T("Ölçüm yapılamadı", "Measurement failed")}: {ex.Message}";
        }
    }

    private void ShowInfo(MediaInfo info)
    {
        InfoGrid.Visibility = Visibility.Visible;
        TxtDuration.Text = TimeSpan.FromSeconds(info.DurationSeconds).ToString(@"hh\:mm\:ss");
        TxtSize.Text = $"{info.FileSizeMb:0.0} MB";
        TxtResolution.Text = $"{info.Width}x{info.Height}";
        TxtFps.Text = info.Fps.ToString("0.##", CultureInfo.InvariantCulture);
        TxtVideoCodec.Text = info.VideoCodec;
        TxtAudio.Text = info.HasAudio ? $"{info.AudioCodec} {info.AudioBitrateBps / 1000}k" : T("Yok", "None");
        TxtBitrate.Text = $"{info.TotalBitrateBps / 1000} kbps";
        TxtHdr.Text = info.IsHdr ? T("Evet", "Yes") : T("Hayır", "No");
        HdrPolicyPanel.Visibility = info.IsHdr ? Visibility.Visible : Visibility.Collapsed;
    }

    private static CodecPreference CodecFromIndex(int index) => index switch
    {
        1 => CodecPreference.Compatible,
        2 => CodecPreference.MaxCompression,
        3 => CodecPreference.Fast,
        _ => CodecPreference.Auto
    };

    private void Recalculate()
    {
        if (_info is null) return;
        var detailed = PlanCalculator.BuildDetailed(_info, CurrentOptions(), _profile, EncoderCapabilities.Instance);
        _autoPlan = detailed.Plan;
        _predictedQuality = detailed.PredictedQuality;
        _advice = detailed.Advice;
        _profile ??= detailed.Profile;
        if (_aiPlan is not null)
        {
            var validation = PlanParser.Parse(TxtAiJson.Text, _info, CurrentOptions());
            if (validation.Ok) _aiPlan = validation.Plan;
            else
            {
                _aiPlan = null;
                BtnRevert.Visibility = Visibility.Collapsed;
                TxtAiStatus.Text = T("AI planı güncel seçeneklerle artık eşleşmiyor; otomatik plan kullanılıyor.", "AI plan no longer matches the current options; using the automatic plan.");
            }
        }
        RefreshPlanView();
        BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
    }

    private void RefreshPlanView()
    {
        if (ActivePlan is not { } plan || _info is null) return;
        var quality = plan.ModeEnum == EncodeMode.Crf ? $"CRF {plan.Crf}" : $"{plan.VideoBitrateK}k two-pass";
        _estimate = PlanCalculator.Estimate(plan, _info, _profile);
        TxtPlanSummary.Text = $"[{(_aiPlan is null ? T("Otomatik", "Automatic") : "AI")}] {plan.Codec} · {quality} · {plan.Width}x{plan.Height} @ {plan.Fps:0.##} FPS · {(plan.AudioCodec is null ? T("Ses Yok", "No Audio") : $"{plan.AudioCodec} {plan.AudioBitrateK}k")} · Preset {plan.Preset}";
        TxtPlanReason.Text = DescribeReason(plan);
        RefreshEstimateView();
        TxtCommand.Text = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(_info, plan, BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4"), plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null));
    }

    private void RefreshEstimateView()
    {
        if (_estimate is not { } estimate || _info is null)
        {
            TxtEstimateValue.Text = "-";
            TxtEstimateRange.Text = "";
            TxtEstimateNote.Text = "";
            TxtStrategyNote.Text = "";
            return;
        }

        TxtEstimateValue.Text = $"{estimate.ExpectedMb:0.0} MB";
        TxtEstimateRange.Text = $"{estimate.LowMb:0.0} - {estimate.HighMb:0.0} MB · {T("Kaynağın", "Of source")} %{estimate.ExpectedMb / Math.Max(_info.FileSizeMb, 0.01) * 100:0.#}";

        var basis = estimate.Measured
            ? T("kaynaktan ölçülen karmaşıklık", "measured source complexity")
            : T("kaynak bit hızından tahmin", "estimated from source bitrate");
        var mode = estimate.Enforced
            ? T("iki geçişli mod boyutu zorlar", "two-pass enforces this size")
            : T("kalite modu; hedefin altında kalır", "quality mode; stays under the target");
        TxtEstimateNote.Text = $"{basis} · {mode} · {T("öngörülen kalite", "predicted quality")} {_predictedQuality:0.#}/100";
        TxtStrategyNote.Text = DescribeStrategy();
    }

    private string DescribeReason(EncodePlan plan)
    {
        if (plan.ReasonCodes.Count == 0) return plan.Reason;

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
                _ => null
            };
            if (text is not null) parts.Add(text);
        }

        return string.Join("; ", parts);
    }

    private string DescribeStrategy()
    {
        if (_advice is not { } advice) return "";

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
                AdviceCode.CodecUpgradeRecommended => T("Bu sıkışıklıkta H.265 aynı boyutta gözle görülür şekilde daha iyi sonuç verir; sıkıştırma algoritmasını Otomatik veya H.265 yapmayı düşün.", "At this pressure H.265 gives a visibly better result at the same size; consider switching the compression algorithm to Automatic or H.265."),
                AdviceCode.HardwareCodecCostsQuality => T("NVENC hızlıdır ama bu kadar sıkışık bir hedefte megabayt başına belirgin kalite kaybettirir; yazılım kodeği daha iyi görünür.", "NVENC is fast but loses noticeable quality per megabyte at a target this tight; a software codec will look better."),
                AdviceCode.ExtremeRatioWarning => T("Uç sıkıştırma: kayıp kaçınılmaz, motor kaybı en az hissedilecek yere yığıyor.", "Extreme compression: loss is unavoidable, so the engine pushes it where it is least noticeable."),
                AdviceCode.ContentIsSimple => T("İçerik sade ölçüldü; hedefin altında rahat kalınıyor.", "The content measured as simple, so the target is met with room to spare."),
                AdviceCode.ContentIsComplex => T("İçerik yoğun ölçüldü; bit bütçesi bu yüzden zorlanıyor.", "The content measured as detail-heavy, which is why the bit budget is tight."),
                AdviceCode.ScaleSavesMuch => T("Bu klipte çözünürlük düşürmek çok bit kazandırıyor.", "Scaling down frees a lot of bits on this particular clip."),
                AdviceCode.ScaleSavesLittle => T("Bu klipte çözünürlük düşürmek az kazandırıyor; çözünürlük korunuyor.", "Scaling down frees little on this clip, so resolution is preserved."),
                AdviceCode.QualityCeilingReached => T("Kalite tavanına ulaşıldı; kalan bütçe gözle görülür bir şey satın almayacağı için harcanmıyor.", "The quality ceiling was reached; the remaining budget is left unspent because it would buy nothing you could see."),
                AdviceCode.AudioMono => T("Ses tek kanala indirildi — telefon hoparlöründe fark edilmez, görüntüye bit kazandırır.", "Audio was folded to mono — inaudible on a phone speaker, and it buys bits for the picture."),
                AdviceCode.AudioDropped => T("Bu boyutta ses tutulamıyor, çıkarıldı.", "Audio cannot fit at this size and was removed."),
                AdviceCode.EncoderFallback => T("Tercih edilen kodlayıcı bu ffmpeg sürümünde yok; yazılım karşılığına düşüldü.", "The preferred encoder is not available on this ffmpeg build; falling back to a software encoder."),
                AdviceCode.HdrTonemapped => T("Kaynak HDR ama seçili kodlayıcı 10-bit'i koruyamıyor; BT.709 SDR'ye tone-map edildi.", "The source is HDR but the selected encoder cannot preserve 10-bit, so it was tone-mapped to SDR BT.709."),
                _ => null
            };
            if (text is not null) lines.Add(text);
        }

        return string.Join("  ", lines);
    }

    private static string BuildUniqueOutputPath(string inputPath, string suffix, string extension)
    {
        var dir = Path.GetDirectoryName(inputPath)!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var firstIndex = 2;
        if (suffix == "shrunk" && name.EndsWith("_shrunk", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^"_shrunk".Length];
            firstIndex = 2;
        }
        var candidate = Path.Combine(dir, $"{name}_{suffix}.{extension}");
        for (var index = firstIndex; PathEquals(candidate, inputPath) || File.Exists(candidate); index++) candidate = Path.Combine(dir, $"{name}_{suffix}_{index}.{extension}");
        return candidate;
    }

    private static bool PathEquals(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void OnTargetSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_syncing || TxtTarget is null) return; _syncing = true; TxtTarget.Text = Math.Round(e.NewValue, 1).ToString("0.##", CultureInfo.InvariantCulture); _syncing = false; Recalculate(); }
    private void OnTargetTextChanged(object sender, TextChangedEventArgs e) { if (_syncing || SliderTarget is null) return; _syncing = true; var mb = ParseTargetMb(); if (mb > SliderTarget.Maximum) SliderTarget.Maximum = Math.Ceiling(mb); SliderTarget.Value = mb; _syncing = false; Recalculate(); }
    private void OnPreset(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string tag } && double.TryParse(tag, CultureInfo.InvariantCulture, out var value)) { if (value > SliderTarget.Maximum) SliderTarget.Maximum = value; TxtTarget.Text = tag; } }
    private void OnPresetHalf(object sender, RoutedEventArgs e) { if (_info is not null) TxtTarget.Text = Math.Round(_info.FileSizeMb / 2, 1).ToString("0.##", CultureInfo.InvariantCulture); }
    private void OnOptionChanged(object sender, RoutedEventArgs e) => Recalculate();

    private void OnCopyPrompt(object sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null) { TxtAiStatus.Text = T("Önce bir video yükleyin.", "Load a video first."); return; }
        try { Clipboard.SetText(PromptBuilder.Build(_info, CurrentOptions(), _autoPlan)); TxtAiStatus.Text = T("İstem kopyalandı. JSON yanıtını aşağıya yapıştırın.", "Prompt copied. Paste its JSON answer below."); }
        catch (Exception ex) { TxtAiStatus.Text = $"{T("Pano kullanılamıyor", "The clipboard is unavailable")}: {ex.Message}"; }
    }

    private void OnApplyJson(object sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null) { TxtAiStatus.Text = T("Önce bir video yükleyin.", "Load a video first."); return; }
        var result = PlanParser.Parse(TxtAiJson.Text, _info, CurrentOptions());
        if (!result.Ok) { _aiPlan = null; TxtAiStatus.Text = T("Reddedildi; otomatik plan kullanılıyor:\n• ", "Rejected; using automatic:\n• ") + string.Join("\n• ", result.Errors); RefreshPlanView(); return; }
        _aiPlan = result.Plan;
        BtnRevert.Visibility = Visibility.Visible;
        var differences = _autoPlan.DescribeDifferences(_aiPlan!).ToList();
        TxtAiStatus.Text = differences.Count == 0 ? T("AI otomatik planla aynı kararı verdi.", "The AI agreed with the automatic plan.") : T("Otomatik plana göre değişiklikler:\n• ", "Changes vs automatic:\n• ") + string.Join("\n• ", differences);
        if (result.Warnings.Count > 0) TxtAiStatus.Text += T("\nUyarılar:\n• ", "\nWarnings:\n• ") + string.Join("\n• ", result.Warnings);
        RefreshPlanView();
    }

    private void OnRevertAuto(object sender, RoutedEventArgs e) { _aiPlan = null; BtnRevert.Visibility = Visibility.Collapsed; TxtAiStatus.Text = T("Otomatik plana dönüldü.", "Back on the automatic plan."); RefreshPlanView(); }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_info is null || ActivePlan is null) return;
        var output = BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4");
        var targetMb = ParseTargetMb();
        if (DiskSpaceGuard.TryGetFreeBytes(output, out var freeBytes) && !DiskSpaceGuard.HasEnoughSpace(freeBytes, targetMb))
        {
            var neededMb = DiskSpaceGuard.RequiredBytes(targetMb) / 1024.0 / 1024.0;
            TxtResult.Text = T($"Hedef sürücüde yeterli boş alan yok; en az {neededMb:0} MB gerekiyor.", $"Not enough free space on the target drive; at least {neededMb:0} MB is needed.");
            return;
        }
        _probeCts?.Cancel();
        _cts = new CancellationTokenSource(); SetRunning(true); TxtResult.Text = ""; BtnReveal.Visibility = Visibility.Collapsed;
        var progress = new Progress<EncodeProgress>(p => { Progress.Value = p.Fraction; TxtStage.Text = LocalizeStage(p.Stage); TxtRemaining.Text = p.Remaining?.ToString(@"mm\:ss") ?? "-"; if (p.OutputMb > 0) TxtOutSize.Text = $"{p.OutputMb:0.0} MB"; });
        try
        {
            var result = await new EncodeRunner().RunAsync(_info, ActivePlan, output, targetMb, progress, _cts.Token, CurrentOptions().FillPolicy, _profile);
            _lastOutput = result.OutputPath;
            if (result.Success)
            {
                TxtOutSize.Text = $"{result.OutputMb:0.0} MB";
                TxtResult.Text = _turkish
                    ? $"{result.Attempts} denemede tamamlandı. {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB (%{100 - result.OutputMb / _info.FileSizeMb * 100:0.#} daha küçük)."
                    : $"Done in {result.Attempts} attempt(s). {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB ({100 - result.OutputMb / _info.FileSizeMb * 100:0.#}% smaller).";
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
                TxtResult.Text = result.Error;
            }
            BtnReveal.Visibility = result.Success ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { TxtResult.Text = T("İptal edildi.", "Cancelled."); } catch (Exception ex) { TxtResult.Text = ex.Message; } finally { _cts.Dispose(); _cts = null; SetRunning(false); }
    }

    private ConversionPlan ReadConversionPlan()
    {
        var container = ((ComboBoxItem)CmbContainer.SelectedItem).Content.ToString()!.ToLowerInvariant();
        var codec = CmbConvertCodec.SelectedIndex switch { 0 => "libx264", 1 => "libx265", 2 => "libvpx-vp9", 3 => "libsvtav1", _ => "copy" };
        int.TryParse(TxtQuality.Text, out var quality); int.TryParse(TxtAudioBitrate.Text, out var audioK);
        int? width = null, height = null;
        if (CmbResolution.SelectedIndex is > 0 and < 6) height = new[] { 2160, 1440, 1080, 720, 480 }[CmbResolution.SelectedIndex - 1];
        if (CmbResolution.SelectedIndex == 6) { var parts = TxtCustomResolution.Text.ToLowerInvariant().Split('x'); if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)) (width, height) = (w, h); }
        double? fps = CmbConvertFps.SelectedIndex switch { 1 => 60, 2 => 30, 3 => 24, 4 when double.TryParse(TxtCustomFps.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var custom) => custom, _ => null };
        var audio = CmbConvertAudio.SelectedIndex switch { 0 => "aac", 1 => "libopus", 2 => "libmp3lame", 3 => "pcm_s16le", 4 => "copy", _ => null };
        if (container == "mp3") audio = "libmp3lame"; else if (container == "m4a") audio = "aac"; else if (container == "wav") audio = "pcm_s16le";
        return new ConversionPlan { Container = container, VideoCodec = codec, QualityMode = CmbQualityMode.SelectedIndex == 0 ? ConversionQualityMode.Crf : ConversionQualityMode.Bitrate, Crf = quality > 0 ? quality : 23, VideoBitrateK = quality > 0 ? quality : 2500, Width = width, Height = height, Fps = fps, AudioCodec = audio, AudioBitrateK = audioK > 0 ? audioK : 128, Start = ParseTime(TxtTrimStart.Text), End = ParseTime(TxtTrimEnd.Text) };
    }

    private static TimeSpan? ParseTime(string text) => string.IsNullOrWhiteSpace(text) ? null : TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : TimeSpan.MinValue;
    private void OnQualitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || TxtQuality is null) return;
        _syncing = true;
        TxtQuality.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        _syncing = false;
        RefreshConversion();
    }

    private void OnQualityTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || SliderQuality is null) return;
        _syncing = true;
        if (int.TryParse(TxtQuality.Text, out var value))
        {
            var crfMode = CmbQualityMode?.SelectedIndex != 1;
            SliderQuality.Maximum = crfMode ? CrfMaximum() : Math.Max(10000, value);
            SliderQuality.Minimum = crfMode ? CrfMinimum() : 50;
            SliderQuality.Value = Math.Clamp(value, SliderQuality.Minimum, SliderQuality.Maximum);
        }
        _syncing = false;
        RefreshConversion();
    }

    private int CrfMinimum() => CmbConvertCodec?.SelectedIndex switch { 2 => 4, 3 => 1, _ => 0 };
    private int CrfMaximum() => CmbConvertCodec?.SelectedIndex switch { 2 => 63, 3 => 63, _ => 51 };
    private void OnConvertChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        if (sender == CmbQualityMode || sender == CmbConvertCodec) OnQualityTextChanged(TxtQuality, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
        else RefreshConversion();
    }

    private void RefreshConversion()
    {
        if (_info is null || CmbContainer is null) return;
        var plan = ReadConversionPlan();
        var errors = ConversionArguments.Validate(_info, plan).ToList();
        if (plan.Start == TimeSpan.MinValue || plan.End == TimeSpan.MinValue) errors.Add("Trim times must use HH:MM:SS format.");
        var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        TxtConvertValidation.Text = errors.Count == 0 ? T("Hazır.", "Ready.") : string.Join("\n", errors);
        BtnConvert.IsEnabled = errors.Count == 0 && _cts is null;
        try { TxtConvertCommand.Text = errors.Count == 0 ? FfmpegArguments.ToCommandLine(ConversionArguments.Build(_info, plan, output, plan.Gif ? "palette.png" : null)) : ""; } catch (Exception ex) { TxtConvertValidation.Text = ex.Message; BtnConvert.IsEnabled = false; }
    }

    private async void OnConvert(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        var plan = ReadConversionPlan(); var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        _cts = new CancellationTokenSource(); SetRunning(true); TxtConvertResult.Text = ""; BtnConvertReveal.Visibility = Visibility.Collapsed;
        var progress = new Progress<EncodeProgress>(p => { ConvertProgress.Value = p.Fraction; TxtConvertStage.Text = LocalizeStage(p.Stage); });
        try { var result = await new EncodeRunner().ConvertAsync(_info, plan, output, progress, _cts.Token); _lastOutput = result.OutputPath; TxtConvertResult.Text = $"{T("Tamamlandı", "Done")}. {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB."; BtnConvertReveal.Visibility = Visibility.Visible; }
        catch (OperationCanceledException) { TxtConvertResult.Text = T("İptal edildi.", "Cancelled."); } catch (Exception ex) { TxtConvertResult.Text = ex.Message; } finally { _cts.Dispose(); _cts = null; SetRunning(false); RefreshConversion(); }
    }

    private void SetRunning(bool running) { BtnStart.IsEnabled = !running && _info is not null; BtnConvert.IsEnabled = !running && _info is not null; BtnCancel.IsEnabled = BtnConvertCancel.IsEnabled = running; if (!running) { TxtStage.Text = TxtConvertStage.Text = T("Boşta", "Idle"); TxtRemaining.Text = "-"; } }
    private string LocalizeStage(string stage)
    {
        if (!_turkish) return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stage);
        return stage.Replace("encoding", "Kodlama", StringComparison.OrdinalIgnoreCase)
            .Replace("converting", "Dönüştürme", StringComparison.OrdinalIgnoreCase)
            .Replace("pass", "Geçiş", StringComparison.OrdinalIgnoreCase)
            .Replace("attempt", "Deneme", StringComparison.OrdinalIgnoreCase)
            .Replace("GIF palette", "GIF Paleti", StringComparison.OrdinalIgnoreCase)
            .Replace("GIF encode", "GIF Kodlama", StringComparison.OrdinalIgnoreCase);
    }
    private void OnCancel(object sender, RoutedEventArgs e) { _probeCts?.Cancel(); _cts?.Cancel(); }
    private void OnReveal(object sender, RoutedEventArgs e) { if (_lastOutput is not null && File.Exists(_lastOutput)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastOutput}\"") { UseShellExecute = true }); }
    private void OnSignatureNavigate(object sender, RequestNavigateEventArgs e) { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
}
