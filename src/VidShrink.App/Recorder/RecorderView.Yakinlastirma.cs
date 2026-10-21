using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private PointerTrackRecorder? _track;
    private PointerSampler? _sampler;
    private PointerTrack? _zoomTrack;
    private IReadOnlyList<ZoomRegion> _zoomRegions = Array.Empty<ZoomRegion>();
    private CancellationTokenSource? _zoomReading;

    internal Func<(int X, int Y)?> PointerRead { get; set; } = PointerSampler.ReadSystem;

    internal Func<long> TrackClock { get; set; } = () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    internal bool PointerSamplerEnabled { get; set; } = true;

    internal Func<string, CancellationToken, Task<double>> ZoomProbe { get; set; } = MeasureQuickTrimAsync;

    internal Func<string, IReadOnlyList<ZoomRegion>, double, Task<AutoZoomResult>> ZoomRun { get; set; } =
        (source, regions, scale) => AutoZoomer.RunAsync(source, regions, scale);

    internal PointerTrackRecorder? Track => _track;

    internal bool PointerSamplerRunning => _sampler?.Running == true;

    internal bool AutoZoomVisible => BtnAutoZoom.IsVisible;

    internal IReadOnlyList<ZoomRegion> ZoomRegions => _zoomRegions;

    internal int ZoomRegionRows => ZoomRegionList.Children.Count;

    internal bool ZoomEmptyVisible => TxtZoomEmpty.IsVisible;

    private bool ZoomTrackOn => HooksAvailable && _settings.ZoomTrack;

    private void InitYakinlastirma()
    {
        CmbZoomLevel.ItemsSource = AutoZoom.Scales.Select(scale => scale.ToString("0.#", Strings.Culture) + "×").ToList();
        CmbZoomLevel.SelectedIndex = Math.Max(0, AutoZoom.Scales.ToList().IndexOf(AutoZoom.DefaultScale));
        if (BtnAutoZoom.Flyout is Flyout flyout)
        {
            flyout.Opened += async (_, _) => await OpenAutoZoomAsync();
            flyout.Closed += (_, _) => _zoomReading?.Cancel();
        }

        BtnZoomApply.Click += async (_, _) => await ApplyAutoZoomAsync();
    }

    private void BeginTrack()
    {
        EndTrack(null);
        if (!ZoomTrackOn || _frameRegion is not { Width: > 0, Height: > 0 } region) return;
        var track = new PointerTrackRecorder(region.X, region.Y, region.Width, region.Height, TrackClock);
        _track = track;
        if (!PointerSamplerEnabled) return;
        _sampler = new PointerSampler(PointerRead, track.Move);
        _sampler.Start();
    }

    private void SyncTrack(bool running)
    {
        if (_track is not { } track) return;
        if (_session is not null && State == RecorderState.Paused) track.Pause();
        else if (running) track.Resume();
    }

    internal string? EndTrack(RecordResult? result)
    {
        _sampler?.Stop();
        _sampler = null;
        var track = _track;
        _track = null;
        if (track is null || result is null || !File.Exists(result.OutputPath)
            || RecorderArguments.ContainerOf(result.OutputPath) == RecorderContainer.Gif) return null;

        var path = PointerTrack.SidecarOf(result.OutputPath);
        try
        {
            track.Build().Save(path);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ResetAutoZoom(RecordResult result)
    {
        _zoomReading?.Cancel();
        _zoomRegions = Array.Empty<ZoomRegion>();
        ZoomRegionList.Children.Clear();
        TxtZoomEmpty.IsVisible = false;
        _zoomTrack = result.Ok && !result.Partial && RecorderArguments.ContainerOf(result.OutputPath) != RecorderContainer.Gif
            ? PointerTrack.Load(PointerTrack.SidecarOf(result.OutputPath))
            : null;
        BtnAutoZoom.IsVisible = _zoomTrack is not null;
    }

    internal async Task OpenAutoZoomAsync()
    {
        if (Delivered() is not { } path || _zoomTrack is not { } track) return;
        _zoomReading?.Cancel();
        var reading = new CancellationTokenSource();
        _zoomReading = reading;
        BtnZoomApply.IsEnabled = false;

        double duration;
        try { duration = await ZoomProbe(path, reading.Token); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            return;
        }

        if (reading.IsCancellationRequested) return;
        _zoomRegions = AutoZoom.Suggest(track, duration);
        ZoomRegionList.Children.Clear();
        this.TryFindResource("CheckStyle", out var theme);
        foreach (var region in _zoomRegions)
        {
            var label = Saat.Kesit(TimeSpan.FromSeconds(region.Start), Strings.Culture) + " – " + Saat.Kesit(TimeSpan.FromSeconds(region.End), Strings.Culture);
            var box = new CheckBox { Content = label, IsChecked = true };
            if (theme is ControlTheme check) box.Theme = check;
            Avalonia.Automation.AutomationProperties.SetName(box, label);
            box.IsCheckedChanged += (_, _) => BtnZoomApply.IsEnabled = SelectedZoomRegions().Count > 0;
            ZoomRegionList.Children.Add(box);
        }

        TxtZoomEmpty.IsVisible = _zoomRegions.Count == 0;
        BtnZoomApply.IsEnabled = _zoomRegions.Count > 0;
    }

    internal void SetZoomRegionChecked(int index, bool value)
    {
        if (ZoomRegionList.Children[index] is CheckBox box) box.IsChecked = value;
    }

    internal double SelectedZoomScale
    {
        get => CmbZoomLevel.SelectedIndex >= 0 ? AutoZoom.Scales[CmbZoomLevel.SelectedIndex] : AutoZoom.DefaultScale;
        set => CmbZoomLevel.SelectedIndex = AutoZoom.Scales.ToList().IndexOf(value);
    }

    private List<ZoomRegion> SelectedZoomRegions()
    {
        var chosen = new List<ZoomRegion>();
        for (var i = 0; i < _zoomRegions.Count && i < ZoomRegionList.Children.Count; i++)
            if (ZoomRegionList.Children[i] is CheckBox { IsChecked: true }) chosen.Add(_zoomRegions[i]);
        return chosen;
    }

    internal async Task<string?> ApplyAutoZoomAsync()
    {
        if (Delivered() is not { } source) return null;
        var regions = SelectedZoomRegions();
        if (regions.Count == 0) return null;

        BtnZoomApply.IsEnabled = false;
        ClearNoticeAndError();
        ShowNotice(Say("recorder.output.zoom-running"));
        AutoZoomResult result;
        try { result = await ZoomRun(source, regions, SelectedZoomScale); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            result = new AutoZoomResult(false, null, ex.Message);
        }
        finally
        {
            BtnZoomApply.IsEnabled = true;
        }

        ClearNoticeAndError();
        if (result.Ok && result.Target is { } target)
        {
            ShowNotice(Say("recorder.output.zoom-done", target));
            return target;
        }

        ShowError(Say("recorder.output.zoom-failed", result.Error));
        return null;
    }
}
