using Avalonia;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private PixelRect? _frameRegion;

    internal IRecorderFrameHost FrameHost { get; set; } = new RecorderFrameHost();

    internal bool FrameHiddenByUser { get; private set; }

    internal bool FrameShown { get; private set; }

    internal System.Func<string, PixelRect?> WindowRect { get; set; } = RecorderFrame.WindowBounds;

    internal PixelRect? RegionOf(RecorderRequest request) => request.Target switch
    {
        RecorderTargetKind.Region => request.Region is { } r ? new PixelRect(r.X, r.Y, r.Width, r.Height) : null,
        RecorderTargetKind.Window => string.IsNullOrWhiteSpace(request.WindowTitle) ? null : WindowRect(request.WindowTitle),
        _ => RecorderArguments.RegionForScreen(request.Screens, request.ScreenIndex) is { } s
            ? new PixelRect(s.X, s.Y, s.Width, s.Height)
            : null
    };

    internal void ToggleFrame()
    {
        FrameHiddenByUser = !FrameHiddenByUser;
        SyncFrame();
    }

    private void SyncFrame()
    {
        if (RecorderFrame.Wanted(_session is not null, _frameRegion, FrameHiddenByUser) is { } region)
        {
            FrameHost.Show(region);
            FrameShown = true;
        }
        else if (FrameShown)
        {
            FrameHost.Hide();
            FrameShown = false;
        }
    }
}
