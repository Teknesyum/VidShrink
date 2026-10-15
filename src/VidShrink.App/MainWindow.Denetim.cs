using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VidShrink.App.Integration;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// Denetim kanalının ana penceredeki konağı (<c>docs/control-channel.md</c>). Komutlar düğmelerin
/// yürüdüğü yolun aynısından geçer: oynat ve duraklat <c>TogglePlay</c>, konum <c>Seek.GoTo</c>, dosya
/// açma <see cref="AcceptForwarded"/>, kayıt kaydedicinin <c>StartAsync</c>/<c>StopAsync</c>'i.
/// </summary>
public partial class MainWindow : IControlHost
{
    internal void AcceptControl(ControlBridge bridge)
    {
        Opened += (_, _) => bridge.Attach(this);
        Closed += (_, _) => bridge.Detach(this);
    }

    /// <summary>Küçültme isteğinin teslimi; varsayılanı kabuk komutunun aynısıyla yeni bir süreç doğurur.</summary>
    internal Func<ShrinkRequest, bool> ShrinkSubmitter { get; set; } = StartShrinkProcess;

    ControlStatus IControlHost.Status()
    {
        var recorder = _recorderPane;
        var state = recorder switch
        {
            { CountingDown: true } => ControlProtocol.RecorderCountdown,
            { HasSession: true, State: RecorderState.Paused } => ControlProtocol.RecorderPaused,
            { HasSession: true } => ControlProtocol.RecorderRunning,
            _ => ControlProtocol.RecorderIdle
        };
        var last = recorder?.ResultPathText;
        return new ControlStatus(state, string.IsNullOrWhiteSpace(last) ? null : last,
            Player.LoadedPath, Player.IsPlaying, Player.PositionSeconds, _cts is not null);
    }

    bool IControlHost.Play()
    {
        if (Player.LoadedPath is null) return false;
        if (!Player.IsPlaying) Player.TogglePlay();
        return true;
    }

    bool IControlHost.Pause()
    {
        if (Player.LoadedPath is null) return false;
        if (Player.IsPlaying) Player.TogglePlay();
        return true;
    }

    bool IControlHost.Seek(double seconds)
    {
        if (Player.LoadedPath is null) return false;
        Player.Seek.GoTo(seconds);
        return true;
    }

    async Task<ControlReply> IControlHost.OpenAsync(string path)
    {
        if (!AcceptForwarded(new ForwardedRequest(new[] { path }, false))) return ControlReply.Failure(ControlErrors.Busy);
        if (ForwardedLoad is { } load) await load;
        return ControlReply.Success(((IControlHost)this).Status());
    }

    async Task<ControlReply> IControlHost.StartRecordingAsync()
    {
        var recorder = RecorderPane;
        if (recorder.HasSession || recorder.CountingDown) return ControlReply.Failure(ControlErrors.Busy);
        Tabs.SelectedIndex = RecorderTabIndex;
        await recorder.StartAsync();
        if (recorder.HasSession) return ControlReply.Success(((IControlHost)this).Status());
        var reason = recorder.ErrorText;
        return ControlReply.Failure(ControlErrors.Failed, string.IsNullOrWhiteSpace(reason) ? null : reason);
    }

    async Task<ControlReply> IControlHost.StopRecordingAsync()
    {
        if (_recorderPane is not { } recorder || !(recorder.HasSession || recorder.CountingDown))
            return ControlReply.Failure(ControlErrors.NotReady, "no-recording");
        await recorder.StopAsync();
        return ControlReply.Success(((IControlHost)this).Status());
    }

    bool IControlHost.SubmitShrink(ShrinkRequest request) => ShrinkSubmitter(request);

    private static bool StartShrinkProcess(ShrinkRequest request)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable)) return false;
        var start = new ProcessStartInfo { FileName = executable, UseShellExecute = false };
        foreach (var argument in ShrinkRequestResolver.ShellArguments(request)) start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
