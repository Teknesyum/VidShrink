using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Interactivity;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IReplayBuffer? _replay;
    private bool _replayBusy;

    internal Func<RecorderRequest, string, int, Task<IReplayBuffer>> ReplayStarter { get; set; }
        = async (request, folder, seconds) => await ReplayRecorder.StartAsync(request, folder, seconds);

    internal Func<string> ReplayFolder { get; set; } = () => Path.Combine(
        Path.GetTempPath(), "VidShrink", $"replay-{Environment.ProcessId}");

    internal bool ReplayRunning => _replay is not null;

    internal bool ReplayStartVisible => BtnReplay.IsVisible;

    internal string ReplayToggleText => BtnReplay.Content as string ?? string.Empty;

    internal bool ReplaySaveVisible => BtnReplaySave.IsVisible;

    internal string ReplaySaveText => BtnReplaySave.Content as string ?? string.Empty;

    internal int SelectedReplaySeconds
    {
        get
        {
            var index = CmbReplaySeconds.SelectedIndex;
            return index >= 0 && index < ReplayBuffer.SecondsChoices.Length ? ReplayBuffer.SecondsChoices[index] : _settings.ReplaySeconds;
        }
    }

    private void InitTampon()
    {
        CmbReplaySeconds.ItemsSource = ReplayLabels();
        CmbReplaySeconds.SelectedIndex = Math.Max(0, Array.IndexOf(ReplayBuffer.SecondsChoices, _settings.ReplaySeconds));
        CmbReplaySeconds.SelectionChanged += (_, _) =>
        {
            if (CmbReplaySeconds.SelectedIndex < 0 || _quiet > 0) return;
            _settings.ReplaySeconds = SelectedReplaySeconds;
            _settings.Save(_settingsPath);
            SyncReplayButtons(_session is null && !CountingDown);
        };
        SyncReplayButtons(true);
    }

    private static List<string> ReplayLabels()
        => ReplayBuffer.SecondsChoices.Select(seconds => Say("recorder.countdown.seconds", seconds)).ToList();

    private void RefreshReplayLabels()
    {
        var selected = CmbReplaySeconds.SelectedIndex;
        Quietly(() =>
        {
            CmbReplaySeconds.ItemsSource = ReplayLabels();
            CmbReplaySeconds.SelectedIndex = selected >= 0 ? selected : 0;
        });
        SyncReplayButtons(_session is null && !CountingDown);
    }

    private void SyncReplayButtons(bool idle)
    {
        var running = _replay is not null;
        BtnReplay.IsVisible = idle;
        var toggle = Say(running ? "recorder.replay.stop" : "recorder.replay.start");
        BtnReplay.Content = toggle;
        AutomationProperties.SetName(BtnReplay, toggle);
        BtnReplay.IsEnabled = !_replayBusy;
        BtnReplaySave.IsVisible = idle && running;
        var save = Say("recorder.replay.save", _replay?.Seconds ?? SelectedReplaySeconds);
        BtnReplaySave.Content = save;
        AutomationProperties.SetName(BtnReplaySave, save);
        BtnReplaySave.IsEnabled = !_replayBusy;
        CmbReplaySeconds.IsEnabled = !running;
    }

    private async void OnReplayToggle(object? sender, RoutedEventArgs e)
    {
        if (_replay is null) await StartReplayAsync();
        else await StopReplayAsync();
    }

    private async void OnReplaySave(object? sender, RoutedEventArgs e) => await SaveReplayAsync();

    internal async Task<bool> StartReplayAsync()
    {
        if (_replay is not null || _session is not null || CountingDown || _replayBusy) return false;
        ClearMessages();

        if (!ToolLocator.IsAvailable(out var missing))
        {
            ShowError(Say("recorder.error.no-ffmpeg", missing ?? string.Empty));
            return false;
        }

        if (BuildRequest() is not { } request) return false;
        StoreChoices();
        var seconds = SelectedReplaySeconds;
        var replayRequest = request with { Container = RecorderContainer.Mkv, MaxDuration = null, MaxMegabytes = null, Split = null, PreviewPath = null };
        var errors = RecorderArguments.Validate(replayRequest, "replay.mkv");
        if (errors.Count > 0)
        {
            ShowError(Say("recorder.error.invalid", string.Join(" ", errors)));
            return false;
        }

        _replayBusy = true;
        RefreshSerit();
        try
        {
            _replay = await ReplayStarter(replayRequest, ReplayFolder(), seconds);
            ShowNotice(Say("recorder.replay.running", seconds));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _replay = null;
            ShowError(Say("recorder.error.start", ex.Message));
            return false;
        }
        finally
        {
            _replayBusy = false;
            RefreshSerit();
        }
    }

    internal async Task StopReplayAsync()
    {
        if (_replay is not { } replay || _replayBusy) return;
        _replayBusy = true;
        RefreshSerit();
        try { await replay.StopAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { }
        finally
        {
            _replay = null;
            _replayBusy = false;
            ClearMessages();
            RefreshSerit();
        }
    }

    internal async Task<ReplaySaveResult?> SaveReplayAsync()
    {
        if (_replay is not { } replay || _replayBusy) return null;
        _replayBusy = true;
        RefreshSerit();
        var container = _settings.Container == RecorderContainer.Gif ? RecorderContainer.Mkv : _settings.Container;
        var target = Path.ChangeExtension(_settings.OutputPath(DateTime.Now), "." + RecorderArguments.Extension(container));
        ReplaySaveResult result;
        try { result = await replay.SaveAsync(target); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            result = new ReplaySaveResult(false, null, 0, ex.Message);
        }
        finally
        {
            _replayBusy = false;
        }

        ClearMessages();
        if (result is { Ok: true, Target: { } saved }) ShowNotice(Say("recorder.replay.saved", saved));
        else ShowError(Say("recorder.replay.failed", result.Error));

        RefreshSerit();
        return result;
    }
}
