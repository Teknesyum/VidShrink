using System;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private int _endCueSira;
    private int _endCueCount;

    internal int EndCueCount => _endCueCount;

    private double EndCueOpacity => this.FindResource("EndCueOpacity") is double d ? d : 0.8;

    private TimeSpan EndCueDuration => this.FindResource("EndCueDuration") is TimeSpan t ? t : TimeSpan.FromMilliseconds(400);

    private void PlayEndCue()
    {
        if (Settings.Repeat == RepeatMode.One) return;
        if (double.IsFinite(_loopStart) || double.IsFinite(_loopEnd)) return;

        _endCueCount++;
        var sira = ++_endCueSira;
        var half = TimeSpan.FromTicks(EndCueDuration.Ticks / 2);
        EndCue.Transitions = HoverZone.MotionReduced
            ? null
            : new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = half } };
        EndCue.IsVisible = true;
        EndCue.Opacity = EndCueOpacity;
        DispatcherTimer.RunOnce(() =>
        {
            if (sira != _endCueSira) return;
            EndCue.Opacity = 0;
            DispatcherTimer.RunOnce(() =>
            {
                if (sira != _endCueSira) return;
                EndCue.IsVisible = false;
            }, half);
        }, half);
    }
}
