using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow : Window
{
    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".flv", ".m4v", ".mpg", ".mpeg", ".ts", ".m2ts" };

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
        }
        else
        {
            TxtStatusBar.Text = $"{missing} not found. Put ffmpeg.exe and ffprobe.exe in tools\\ffmpeg next to VidShrink.exe, or install them on PATH.";
            BtnStart.IsEnabled = false;
        }
    }

    private PlanOptions CurrentOptions() => new()
    {
        TargetMb = ParseTargetMb(),
        Intent = (Intent)Math.Max(0, CmbIntent.SelectedIndex),
        Codec = (CodecPreference)Math.Max(0, CmbCodec.SelectedIndex),
        AllowResolutionDrop = ChkResolution.IsChecked == true,
        AllowFpsDrop = ChkFps.IsChecked == true
    };

    private double ParseTargetMb()
        => double.TryParse(TxtTarget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) && mb > 0
            ? mb
            : 25;

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video files|*" + string.Join(";*", VideoExtensions) + "|All files|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            await LoadAsync(dialog.FileName);
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
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return null;
        var file = files[0];
        return VideoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) ? file : null;
    }

    private async Task LoadAsync(string path)
    {
        TxtFileName.Text = Path.GetFileName(path);
        TxtStatusBar.Text = "Probing...";
        try
        {
            _info = await FfprobeClient.ProbeAsync(path);
        }
        catch (Exception ex)
        {
            _info = null;
            BtnStart.IsEnabled = false;
            InfoGrid.Visibility = Visibility.Collapsed;
            TxtStatusBar.Text = ex.Message;
            return;
        }

        _aiPlan = null;
        BtnRevert.Visibility = Visibility.Collapsed;
        TxtAiStatus.Text = "";
        ShowInfo(_info);

        _syncing = true;
        var suggested = Math.Max(1, Math.Round(_info.FileSizeMb / 2));
        SliderTarget.Maximum = Math.Max(50, Math.Ceiling(_info.FileSizeMb));
        SliderTarget.Value = Math.Min(suggested, SliderTarget.Maximum);
        TxtTarget.Text = suggested.ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;

        TxtStatusBar.Text = $"ffmpeg: ready — {ToolLocator.Ffmpeg}";
        Recalculate();
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
        RefreshPlanView();
        BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
    }

    private void RefreshPlanView()
    {
        var plan = ActivePlan;
        if (plan is null || _info is null) return;

        var source = _aiPlan is null ? "automatic" : "AI";
        var quality = plan.ModeEnum == EncodeMode.Crf ? $"crf {plan.Crf}" : $"{plan.VideoBitrateK}k 2-pass";
        var audio = plan.AudioCodec is null ? "no audio" : $"{plan.AudioCodec} {plan.AudioBitrateK}k";
        TxtPlanSummary.Text =
            $"[{source}] {plan.Codec} · {quality} · {plan.Width}x{plan.Height} @ {plan.Fps:0.##} fps · {audio} · preset {plan.Preset}" +
            $"  →  estimated {PlanCalculator.EstimatedMb(plan, _info.DurationSeconds):0.0} MB";
        TxtPlanReason.Text = plan.Reason;

        var args = FfmpegArguments.Build(_info, plan, BuildOutputPath(_info), plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null);
        TxtCommand.Text = FfmpegArguments.ToCommandLine(args);
    }

    private string BuildOutputPath(MediaInfo info)
    {
        var dir = Path.GetDirectoryName(info.FilePath)!;
        var name = Path.GetFileNameWithoutExtension(info.FilePath);
        return Path.Combine(dir, $"{name}_shrunk.mp4");
    }

    private void OnTargetSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || TxtTarget is null) return;
        _syncing = true;
        TxtTarget.Text = Math.Round(e.NewValue, 1).ToString("0.##", CultureInfo.InvariantCulture);
        _syncing = false;
        Recalculate();
    }

    private void OnTargetTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || SliderTarget is null) return;
        _syncing = true;
        var mb = ParseTargetMb();
        SliderTarget.Value = Math.Clamp(mb, SliderTarget.Minimum, SliderTarget.Maximum);
        _syncing = false;
        Recalculate();
    }

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) TxtTarget.Text = tag;
    }

    private void OnPresetHalf(object sender, RoutedEventArgs e)
    {
        if (_info is null) return;
        TxtTarget.Text = Math.Round(_info.FileSizeMb / 2, 1).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e) => Recalculate();

    private void OnCopyPrompt(object sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null)
        {
            TxtAiStatus.Text = "Load a video first.";
            return;
        }
        Clipboard.SetText(PromptBuilder.Build(_info, CurrentOptions(), _autoPlan));
        TxtAiStatus.Text = "Prompt copied. Paste it into any chat AI, then paste the JSON answer below.";
    }

    private void OnApplyJson(object sender, RoutedEventArgs e)
    {
        if (_info is null || _autoPlan is null)
        {
            TxtAiStatus.Text = "Load a video first.";
            return;
        }

        var result = PlanParser.Parse(TxtAiJson.Text, _info, CurrentOptions());
        if (!result.Ok)
        {
            TxtAiStatus.Text = "Rejected, staying on the automatic plan:\n• " + string.Join("\n• ", result.Errors);
            return;
        }

        _aiPlan = result.Plan;
        BtnRevert.Visibility = Visibility.Visible;

        var diff = _autoPlan.DescribeDifferences(_aiPlan!).ToList();
        var lines = new List<string> { diff.Count == 0 ? "The AI agreed with the automatic plan." : "Changes vs automatic:\n• " + string.Join("\n• ", diff) };
        if (result.Warnings.Count > 0) lines.Add("Warnings:\n• " + string.Join("\n• ", result.Warnings));
        TxtAiStatus.Text = string.Join("\n", lines);

        RefreshPlanView();
    }

    private void OnRevertAuto(object sender, RoutedEventArgs e)
    {
        _aiPlan = null;
        BtnRevert.Visibility = Visibility.Collapsed;
        TxtAiStatus.Text = "Back on the automatic plan.";
        RefreshPlanView();
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_info is null || ActivePlan is null) return;

        var output = BuildOutputPath(_info);
        if (File.Exists(output) &&
            MessageBox.Show(this, $"{Path.GetFileName(output)} already exists. Overwrite it?", "VidShrink",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _cts = new CancellationTokenSource();
        SetRunning(true);
        TxtResult.Text = "";
        BtnReveal.Visibility = Visibility.Collapsed;

        var progress = new Progress<EncodeProgress>(p =>
        {
            Progress.Value = p.Fraction;
            TxtStage.Text = p.Stage;
            TxtRemaining.Text = p.Remaining is { } r ? r.ToString(@"mm\:ss") : "-";
            if (p.OutputMb > 0) TxtOutSize.Text = $"{p.OutputMb:0.0} MB";
        });

        try
        {
            var result = await new EncodeRunner().RunAsync(_info, ActivePlan, output, ParseTargetMb(), progress, _cts.Token);
            _lastOutput = result.OutputPath;
            TxtOutSize.Text = $"{result.OutputMb:0.0} MB";
            var saved = 100 - result.OutputMb / _info.FileSizeMb * 100;
            TxtResult.Text = result.Success
                ? $"Done in {result.Attempts} attempt(s). {_info.FileSizeMb:0.0} MB → {result.OutputMb:0.0} MB ({saved:0.#}% smaller)."
                : result.Error;
            BtnReveal.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            TxtResult.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            TxtResult.Text = ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    private void SetRunning(bool running)
    {
        BtnStart.IsEnabled = !running && _info is not null;
        BtnCancel.IsEnabled = running;
        if (!running)
        {
            TxtStage.Text = "idle";
            TxtRemaining.Text = "-";
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OnReveal(object sender, RoutedEventArgs e)
    {
        if (_lastOutput is null || !File.Exists(_lastOutput)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastOutput}\"") { UseShellExecute = true });
    }

    private void OnSignatureNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
