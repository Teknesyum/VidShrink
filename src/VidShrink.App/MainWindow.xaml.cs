using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
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
    private string? _lastOutput;
    private bool _syncing;

    private EncodePlan? ActivePlan => _aiPlan ?? _autoPlan;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CheckTools();
    }

    private void CheckTools()
    {
        if (ToolLocator.IsAvailable(out var missing))
        {
            TxtStatusBar.Text = $"ffmpeg: ready — {ToolLocator.Ffmpeg}";
            TxtSystemStatus.Text = $"ffmpeg: {ToolLocator.Ffmpeg}\nversion: {ToolLocator.GetFfmpegVersion()}\n.NET: {Environment.Version}\nVidShrink: {Assembly.GetExecutingAssembly().GetName().Version}";
        }
        else
        {
            TxtStatusBar.Text = $"{missing} not found. Put ffmpeg.exe and ffprobe.exe in tools\\ffmpeg next to VidShrink.exe, or install them on PATH.";
            TxtSystemStatus.Text = TxtStatusBar.Text;
        }
    }

    private PlanOptions CurrentOptions() => new() { TargetMb = ParseTargetMb(), Intent = (Intent)Math.Max(0, CmbIntent.SelectedIndex), Codec = (CodecPreference)Math.Max(0, CmbCodec.SelectedIndex), AllowResolutionDrop = ChkResolution.IsChecked == true, AllowFpsDrop = ChkFps.IsChecked == true };
    private double ParseTargetMb() => double.TryParse(TxtTarget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) && mb > 0 ? mb : 25;

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
        TxtStatusBar.Text = "Probing with ffprobe...";
        try { _info = await FfprobeClient.ProbeAsync(path); }
        catch (Exception ex)
        {
            _info = null;
            BtnStart.IsEnabled = BtnConvert.IsEnabled = false;
            InfoGrid.Visibility = Visibility.Collapsed;
            TxtStatusBar.Text = $"This file cannot be used: {ex.Message}";
            return;
        }

        _aiPlan = null;
        BtnRevert.Visibility = Visibility.Collapsed;
        TxtAiStatus.Text = "";
        TxtConvertSource.Text = Path.GetFileName(path);
        ShowInfo(_info);
        _syncing = true;
        var suggested = Math.Max(1, Math.Round(_info.FileSizeMb / 2));
        SliderTarget.Maximum = Math.Max(500, Math.Ceiling(suggested));
        SliderTarget.Value = suggested;
        TxtTarget.Text = suggested.ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;
        TxtStatusBar.Text = $"ffmpeg: ready — {ToolLocator.Ffmpeg}";
        Recalculate();
        RefreshConversion();
    }

    private void ShowInfo(MediaInfo info)
    {
        InfoGrid.Visibility = Visibility.Visible;
        TxtDuration.Text = TimeSpan.FromSeconds(info.DurationSeconds).ToString(@"hh\:mm\:ss");
        TxtSize.Text = $"{info.FileSizeMb:0.0} MB";
        TxtResolution.Text = $"{info.Width}x{info.Height}";
        TxtFps.Text = info.Fps.ToString("0.##", CultureInfo.InvariantCulture);
        TxtVideoCodec.Text = info.VideoCodec;
        TxtAudio.Text = info.HasAudio ? $"{info.AudioCodec} {info.AudioBitrateBps / 1000}k" : "none";
        TxtBitrate.Text = $"{info.TotalBitrateBps / 1000} kbps";
        TxtHdr.Text = info.IsHdr ? "yes" : "no";
    }

    private void Recalculate()
    {
        if (_info is null) return;
        _autoPlan = PlanCalculator.Build(_info, CurrentOptions());
        if (_aiPlan is not null)
        {
            var validation = PlanParser.Parse(TxtAiJson.Text, _info, CurrentOptions());
            if (validation.Ok) _aiPlan = validation.Plan;
            else
            {
                _aiPlan = null;
                BtnRevert.Visibility = Visibility.Collapsed;
                TxtAiStatus.Text = "AI plan no longer matches the current options; using the automatic plan.";
            }
        }
        RefreshPlanView();
        BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
    }

    private void RefreshPlanView()
    {
        if (ActivePlan is not { } plan || _info is null) return;
        var quality = plan.ModeEnum == EncodeMode.Crf ? $"CRF {plan.Crf}" : $"{plan.VideoBitrateK}k two-pass";
        var estimate = PlanCalculator.EstimatedMb(plan, _info.DurationSeconds);
        var size = estimate is null ? $"≤ {ParseTargetMb():0.##} MB target (quality mode; corrected if needed)" : $"estimated {estimate:0.0} MB";
        TxtPlanSummary.Text = $"[{(_aiPlan is null ? "automatic" : "AI")}] {plan.Codec} · {quality} · {plan.Width}x{plan.Height} @ {plan.Fps:0.##} fps · {(plan.AudioCodec is null ? "no audio" : $"{plan.AudioCodec} {plan.AudioBitrateK}k")} · preset {plan.Preset} → {size}";
        TxtPlanReason.Text = plan.Reason;
        TxtCommand.Text = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(_info, plan, BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4"), plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null));
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
        if (_info is null || _autoPlan is null) { TxtAiStatus.Text = "Load a video first."; return; }
        try { Clipboard.SetText(PromptBuilder.Build(_info, CurrentOptions(), _autoPlan)); TxtAiStatus.Text = "Prompt copied. Paste its JSON answer below."; }
        catch (Exception ex) { TxtAiStatus.Text = $"The clipboard is unavailable: {ex.Message}"; }
    }

    private void OnApplyJson(object sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null) { TxtAiStatus.Text = "Load a video first."; return; }
        var result = PlanParser.Parse(TxtAiJson.Text, _info, CurrentOptions());
        if (!result.Ok) { _aiPlan = null; TxtAiStatus.Text = "Rejected; using automatic:\n• " + string.Join("\n• ", result.Errors); RefreshPlanView(); return; }
        _aiPlan = result.Plan;
        BtnRevert.Visibility = Visibility.Visible;
        var differences = _autoPlan.DescribeDifferences(_aiPlan!).ToList();
        TxtAiStatus.Text = differences.Count == 0 ? "The AI agreed with the automatic plan." : "Changes vs automatic:\n• " + string.Join("\n• ", differences);
        if (result.Warnings.Count > 0) TxtAiStatus.Text += "\nWarnings:\n• " + string.Join("\n• ", result.Warnings);
        RefreshPlanView();
    }

    private void OnRevertAuto(object sender, RoutedEventArgs e) { _aiPlan = null; BtnRevert.Visibility = Visibility.Collapsed; TxtAiStatus.Text = "Back on the automatic plan."; RefreshPlanView(); }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_info is null || ActivePlan is null) return;
        var output = BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4");
        _cts = new CancellationTokenSource(); SetRunning(true); TxtResult.Text = ""; BtnReveal.Visibility = Visibility.Collapsed;
        var progress = new Progress<EncodeProgress>(p => { Progress.Value = p.Fraction; TxtStage.Text = p.Stage; TxtRemaining.Text = p.Remaining?.ToString(@"mm\:ss") ?? "-"; if (p.OutputMb > 0) TxtOutSize.Text = $"{p.OutputMb:0.0} MB"; });
        try { var result = await new EncodeRunner().RunAsync(_info, ActivePlan, output, ParseTargetMb(), progress, _cts.Token); _lastOutput = result.OutputPath; TxtOutSize.Text = $"{result.OutputMb:0.0} MB"; TxtResult.Text = result.Success ? $"Done in {result.Attempts} attempt(s). {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB ({100 - result.OutputMb / _info.FileSizeMb * 100:0.#}% smaller)." : result.Error; BtnReveal.Visibility = Visibility.Visible; }
        catch (OperationCanceledException) { TxtResult.Text = "Cancelled."; } catch (Exception ex) { TxtResult.Text = ex.Message; } finally { _cts.Dispose(); _cts = null; SetRunning(false); }
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
        TxtConvertValidation.Text = errors.Count == 0 ? "Ready." : string.Join("\n", errors);
        BtnConvert.IsEnabled = errors.Count == 0 && _cts is null;
        try { TxtConvertCommand.Text = errors.Count == 0 ? FfmpegArguments.ToCommandLine(ConversionArguments.Build(_info, plan, output, plan.Gif ? "palette.png" : null)) : ""; } catch (Exception ex) { TxtConvertValidation.Text = ex.Message; BtnConvert.IsEnabled = false; }
    }

    private async void OnConvert(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        var plan = ReadConversionPlan(); var output = BuildUniqueOutputPath(_info.FilePath, "converted", plan.Container);
        _cts = new CancellationTokenSource(); SetRunning(true); TxtConvertResult.Text = ""; BtnConvertReveal.Visibility = Visibility.Collapsed;
        var progress = new Progress<EncodeProgress>(p => { ConvertProgress.Value = p.Fraction; TxtConvertStage.Text = p.Stage; });
        try { var result = await new EncodeRunner().ConvertAsync(_info, plan, output, progress, _cts.Token); _lastOutput = result.OutputPath; TxtConvertResult.Text = $"Done. {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB."; BtnConvertReveal.Visibility = Visibility.Visible; }
        catch (OperationCanceledException) { TxtConvertResult.Text = "Cancelled."; } catch (Exception ex) { TxtConvertResult.Text = ex.Message; } finally { _cts.Dispose(); _cts = null; SetRunning(false); RefreshConversion(); }
    }

    private void SetRunning(bool running) { BtnStart.IsEnabled = !running && _info is not null; BtnConvert.IsEnabled = !running && _info is not null; BtnCancel.IsEnabled = BtnConvertCancel.IsEnabled = running; if (!running) { TxtStage.Text = TxtConvertStage.Text = "idle"; TxtRemaining.Text = "-"; } }
    private void OnCancel(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void OnReveal(object sender, RoutedEventArgs e) { if (_lastOutput is not null && File.Exists(_lastOutput)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastOutput}\"") { UseShellExecute = true }); }
    private void OnSignatureNavigate(object sender, RequestNavigateEventArgs e) { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
}
