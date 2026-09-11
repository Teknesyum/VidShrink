using System.Collections.Generic;
using System.Linq;

namespace VidShrink.App.Playback;

internal readonly record struct SeekMark(double Seconds, bool Chapter);

internal static class SeekMarks
{
    internal static IReadOnlyList<SeekMark> Build(IEnumerable<double> chapters, IEnumerable<double> bookmarks, double duration)
    {
        if (!double.IsFinite(duration) || duration <= 0) return new List<SeekMark>();
        return chapters.Where(at => double.IsFinite(at) && at > 0 && at < duration).Select(at => new SeekMark(at, true))
            .Concat(bookmarks.Where(at => double.IsFinite(at) && at >= 0 && at <= duration).Select(at => new SeekMark(at, false)))
            .OrderBy(mark => mark.Seconds)
            .ThenBy(mark => mark.Chapter ? 0 : 1)
            .ToList();
    }

    internal static double Offset(double seconds, double duration, double width)
        => duration > 0 && width > 0 ? System.Math.Clamp(seconds / duration, 0, 1) * width : 0;

    internal static double SecondsAt(double x, double width, double duration)
        => duration > 0 && width > 0 ? System.Math.Clamp(x / width, 0, 1) * duration : 0;
}
