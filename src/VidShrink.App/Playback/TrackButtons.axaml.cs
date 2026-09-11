using Avalonia.Controls;

namespace VidShrink.App.Playback;

internal partial class TrackButtons : UserControl
{
    public TrackButtons()
    {
        InitializeComponent();
    }

    internal Button Subtitle => BtnSubtitle;

    internal Button Audio => BtnAudio;
}
