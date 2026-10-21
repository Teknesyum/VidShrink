namespace VidShrink.Core.Editing;

public sealed record SlidePlan(IReadOnlyList<double> Seconds, double TransitionSeconds);

public static class Slideshow
{
    public const double DefaultSeconds = 3;

    public static double FitSeconds(double musicSeconds, int count)
        => count < 1 || !double.IsFinite(musicSeconds) || musicSeconds <= 0 ? DefaultSeconds : StillClip.ClampSeconds(musicSeconds / count);

    public static IReadOnlyList<string> Order(IEnumerable<string> paths)
        => paths.Where(ImageSequence.IsImage)
            .DistinctBy(StillClip.PathKey, StillClip.PathComparer)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

    public static SlidePlan Plan(int count, double slideSeconds, double transitionSeconds, double fps)
    {
        if (count < 1) return new SlidePlan(Array.Empty<double>(), 0);
        var rate = double.IsFinite(fps) && fps > 0 ? fps : StillClip.FallbackFps;
        var slide = Math.Max(1L, (long)Math.Round(StillClip.ClampSeconds(slideSeconds) * rate));
        var cross = 0L;
        if (count > 1 && double.IsFinite(transitionSeconds) && transitionSeconds > 0)
        {
            var wanted = Math.Min(Math.Min(transitionSeconds, ClipTransition.MaxSeconds), slide / rate / 2);
            cross = (long)Math.Floor(wanted * rate);
            if (cross / rate < ClipTransition.MinSeconds) cross = 0;
        }

        var half = cross / 2;
        var seconds = new double[count];
        for (var i = 0; i < count; i++)
        {
            var frames = count == 1 ? slide
                : i == 0 ? slide + cross - half
                : i == count - 1 ? slide + half
                : slide + cross;
            seconds[i] = frames / rate;
        }

        return new SlidePlan(seconds, cross / rate);
    }
}
