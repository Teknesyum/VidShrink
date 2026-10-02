namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal uint? BorderColorRequest => Kip.BorderColorRequest;

    internal int BorderColorWrites => Kip.BorderColorWrites;

    internal double VideoAspect
        => _zoom.SourceWidth > 0 && _zoom.SourceHeight > 0 ? _zoom.SourceWidth / _zoom.SourceHeight : 0;
}
