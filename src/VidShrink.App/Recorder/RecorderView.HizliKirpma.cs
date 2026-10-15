using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>Hızlı kırpmanın okuduğu kaynak: süre ve başlangıca göre anahtar kareler.</summary>
internal sealed record QuickTrimSource(double Duration, IReadOnlyList<double> Keyframes);

/// <summary>
/// Sonuç panelindeki hızlı kırpma (later 41). Açılırda küçük resim şeridi ve iki tutamak durur; başlangıç
/// tutamağı anahtar karelere oturur, kesim <see cref="ClipExport"/>'un kopya yolundan geçer, yazılan dosyanın
/// süresi ffprobe ile ölçülüp bildirimde söylenir. Panele görünür satır eklemez: düğme var olan satıra girer.
/// Yoklama, kesim ve ölçü testte sahtesiyle verilir.
/// </summary>
internal partial class RecorderView
{
    internal const int QuickTrimFrames = 6;

    private QuickTrimSource? _quickTrimSource;
    private CancellationTokenSource? _quickTrimReading;

    internal Func<string, CancellationToken, Task<QuickTrimSource>> QuickTrimProbe { get; set; } = ProbeQuickTrimSourceAsync;

    internal Func<ClipRequest, CancellationToken, Task<ClipResult>> QuickTrimCut { get; set; } = ClipExport.RunAsync;

    internal Func<string, CancellationToken, Task<double>> QuickTrimMeasure { get; set; } = MeasureQuickTrimAsync;

    internal bool QuickTrimVisible => BtnQuickTrim.IsVisible;

    internal int QuickTrimFrameCount => QuickTrimStrip.Children.Count;

    internal string QuickTrimRangeText => TxtTrimRange.Text ?? string.Empty;

    internal static async Task<QuickTrimSource> ProbeQuickTrimSourceAsync(string path, CancellationToken ct)
    {
        var info = await FfprobeClient.ProbeAsync(path, ct).ConfigureAwait(false);
        var keyframes = EditExport.ParseKeyframes(await EditExportRunner.ProbeKeyframesAsync(path, ct).ConfigureAwait(false)).Keyframes;
        return new QuickTrimSource(info.DurationSeconds, keyframes);
    }

    internal static async Task<double> MeasureQuickTrimAsync(string path, CancellationToken ct)
        => (await FfprobeClient.ProbeAsync(path, ct).ConfigureAwait(false)).DurationSeconds;

    private void InitHizliKirpma()
    {
        if (BtnQuickTrim.Flyout is Flyout flyout)
        {
            flyout.Opened += async (_, _) => await OpenQuickTrimAsync();
            flyout.Closed += (_, _) => _quickTrimReading?.Cancel();
        }

        SldTrimStart.ValueChanged += (_, _) => RefreshTrimRange();
        SldTrimEnd.ValueChanged += (_, _) => RefreshTrimRange();
        BtnTrimCut.Click += async (_, _) => await QuickTrimAsync();
    }

    private void ResetQuickTrim(RecordResult result)
    {
        _quickTrimReading?.Cancel();
        _quickTrimSource = null;
        QuickTrimStrip.Children.Clear();
        BtnQuickTrim.IsVisible = result.Ok && !result.Partial
                                 && RecorderArguments.ContainerOf(result.OutputPath) != RecorderContainer.Gif;
    }

    internal async Task OpenQuickTrimAsync()
    {
        if (Delivered() is not { } path) return;
        _quickTrimReading?.Cancel();
        var reading = new CancellationTokenSource();
        _quickTrimReading = reading;
        BtnTrimCut.IsEnabled = false;

        QuickTrimSource source;
        try { source = _quickTrimSource ?? await QuickTrimProbe(path, reading.Token); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            TxtTrimRange.Text = Say("recorder.output.quick-trim-failed", ex.Message);
            return;
        }

        if (reading.IsCancellationRequested || !double.IsFinite(source.Duration) || source.Duration <= 0) return;
        _quickTrimSource = source;
        SldTrimStart.Maximum = source.Duration;
        SldTrimEnd.Maximum = source.Duration;
        SldTrimStart.Ticks = new AvaloniaList<double>(source.Keyframes.Prepend(0).Distinct());
        SldTrimStart.Value = 0;
        SldTrimEnd.Value = source.Duration;
        RefreshTrimRange();
        await LoadQuickTrimStripAsync(path, source.Duration, reading.Token);
    }

    private async Task LoadQuickTrimStripAsync(string path, double duration, CancellationToken token)
    {
        QuickTrimStrip.Children.Clear();
        var height = Math.Max(1, (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderQuickTrimThumbHeight") * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1)));
        var images = Enumerable.Range(0, QuickTrimFrames).Select(_ => new Image { Height = OverlayPlace.Size(this, "RecorderQuickTrimThumbHeight") }).ToList();
        foreach (var image in images) QuickTrimStrip.Children.Add(image);

        for (var index = 0; index < QuickTrimFrames; index++)
        {
            if (token.IsCancellationRequested) return;
            byte[]? bytes;
            try { bytes = await ResultThumbnails.ReadAsync(path, EditTime.FromSeconds(duration * (index + 0.5) / QuickTrimFrames), height, token); }
            catch (Exception) { return; }
            if (bytes is null) continue;
            try { images[index].Source = new Bitmap(new MemoryStream(bytes)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NullReferenceException) { }
        }
    }

    internal void SetQuickTrimRange(double start, double end)
    {
        SldTrimStart.Value = start;
        SldTrimEnd.Value = end;
        RefreshTrimRange();
    }

    private ClipRequest? PendingQuickTrim()
    {
        if (Delivered() is not { } path || _quickTrimSource is not { } source) return null;
        return RecorderQuickTrim.Request(path, RecorderQuickTrim.TargetFor(path, File.Exists), source.Keyframes, SldTrimStart.Value, SldTrimEnd.Value);
    }

    private void RefreshTrimRange()
    {
        if (_quickTrimSource is not { } source) return;
        var start = RecorderQuickTrim.SnapStart(source.Keyframes, SldTrimStart.Value);
        TxtTrimRange.Text = Say("recorder.output.quick-trim-range",
            Saat.Kesit(TimeSpan.FromSeconds(start), Strings.Culture),
            Saat.Kesit(TimeSpan.FromSeconds(SldTrimEnd.Value), Strings.Culture));
        BtnTrimCut.IsEnabled = PendingQuickTrim() is not null;
    }

    internal async Task<ClipResult?> QuickTrimAsync()
    {
        if (PendingQuickTrim() is not { } request) return null;

        BtnTrimCut.IsEnabled = false;
        ClearNoticeAndError();
        ShowNotice(Say("recorder.output.quick-trim-running"));
        ClipResult result;
        double measured = 0;
        try
        {
            result = await QuickTrimCut(request, CancellationToken.None);
            if (result.Ok) measured = await QuickTrimMeasure(result.Target, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            result = new ClipResult(false, request.Target, ex.Message, TimeSpan.Zero);
        }
        finally
        {
            BtnTrimCut.IsEnabled = true;
        }

        ClearNoticeAndError();
        if (!result.Ok)
            ShowError(Say("recorder.output.quick-trim-failed", result.Error));
        else
            ShowNotice(Say("recorder.output.quick-trim-done", Saat.Sure(measured, Strings.Culture), result.Target));
        return result;
    }
}
