using System.Globalization;
using System.Text;
using VidShrink.Core;

namespace VidShrink.SahneButcesi;

public static class Butce
{
    public const double ZoneFloor = 0.25;
    public const double ZoneCeiling = 4.0;

    public const double DefaultQcomp = 0.60;

    public static double Gamma(double qcomp) => 1.0 - qcomp;

    public static double[] HaritaPaylari(SceneMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var total = (double)map.Scenes.Sum(s => s.Bits);
        if (total <= 0) throw new InvalidOperationException("Harita bit uretmedi; pay hesaplanamaz.");
        return map.Scenes.Select(s => s.Bits / total).ToArray();
    }

    public static double[] ZamanPaylari(SceneMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var total = map.Scenes.Sum(s => s.Duration);
        if (total <= 0) throw new InvalidOperationException("Harita sure uretmedi.");
        return map.Scenes.Select(s => s.Duration / total).ToArray();
    }

    public static double[] ZoneCarpanlari(SceneMap map, double gamma)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!double.IsFinite(gamma)) throw new ArgumentOutOfRangeException(nameof(gamma));
        var scenes = map.Scenes;
        if (scenes.Count == 0) throw new InvalidOperationException("Harita sahne icermiyor.");

        var raw = new double[scenes.Count];
        for (var i = 0; i < scenes.Count; i++)
        {
            var c = scenes[i].Complexity;
            raw[i] = c > 0 ? Math.Pow(c, gamma) : 1.0;
        }

        var totalDuration = scenes.Sum(s => s.Duration);
        var mean = 0.0;
        for (var i = 0; i < scenes.Count; i++) mean += raw[i] * scenes[i].Duration / totalDuration;
        if (mean <= 0) throw new InvalidOperationException("Zone carpanlarinin ortalamasi sifir.");

        var result = new double[scenes.Count];
        for (var i = 0; i < scenes.Count; i++)
            result[i] = Math.Clamp(raw[i] / mean, ZoneFloor, ZoneCeiling);
        return result;
    }

    public static string ZonesArg(SceneMap map, double[] factors, double fps)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(factors);
        if (factors.Length != map.Scenes.Count)
            throw new ArgumentException("Carpan sayisi sahne sayisina esit degil.", nameof(factors));
        if (!double.IsFinite(fps) || fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));

        var parts = new List<string>(map.Scenes.Count);
        var lastEnd = -1;
        for (var i = 0; i < map.Scenes.Count; i++)
        {
            var scene = map.Scenes[i];
            var start = Math.Max(lastEnd + 1, (int)Math.Round(scene.Start * fps));
            var end = (int)Math.Round(scene.End * fps) - 1;
            if (end < start) continue;
            lastEnd = end;
            parts.Add(FormattableString.Invariant($"{start},{end},b={factors[i].ToString("0.###", CultureInfo.InvariantCulture)}"));
        }
        return string.Join('/', parts);
    }

    public static SceneMap KesimDusur(SceneMap map, int every)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (every < 2) throw new ArgumentOutOfRangeException(nameof(every));
        var bounds = new List<double> { 0.0 };
        for (var i = 1; i < map.Scenes.Count; i++)
            if (i % every != 0) bounds.Add(map.Scenes[i].Start);
        bounds.Add(map.Duration);
        return YenidenBolustur(map, bounds);
    }

    public static SceneMap KesimEkle(SceneMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var bounds = new List<double> { 0.0 };
        foreach (var scene in map.Scenes)
        {
            bounds.Add((scene.Start + scene.End) / 2.0);
            if (scene.End < map.Duration) bounds.Add(scene.End);
        }
        bounds.Add(map.Duration);
        return YenidenBolustur(map, bounds.Distinct().OrderBy(b => b).ToList());
    }

    private static SceneMap YenidenBolustur(SceneMap map, IReadOnlyList<double> bounds)
    {
        var scenes = new List<Scene>(bounds.Count - 1);
        var totalBits = 0.0;
        var raw = new double[bounds.Count - 1];
        for (var i = 0; i < bounds.Count - 1; i++)
        {
            raw[i] = BitsIn(map, bounds[i], bounds[i + 1]);
            totalBits += raw[i];
        }
        var meanBps = totalBits / map.Duration;
        for (var i = 0; i < raw.Length; i++)
        {
            var start = bounds[i];
            var end = bounds[i + 1];
            var bps = end > start ? raw[i] / (end - start) : 0.0;
            scenes.Add(new Scene
            {
                Index = i,
                Start = start,
                End = end,
                Bits = (long)Math.Round(raw[i]),
                Complexity = meanBps > 0 ? bps / meanBps : 0.0
            });
        }
        return new SceneMap { Threshold = map.Threshold, Duration = map.Duration, Scenes = scenes, Rule = map.Rule };
    }

    private static double BitsIn(SceneMap map, double start, double end)
    {
        var total = 0.0;
        foreach (var scene in map.Scenes)
        {
            var lo = Math.Max(start, scene.Start);
            var hi = Math.Min(end, scene.End);
            if (hi <= lo || scene.Duration <= 0) continue;
            total += scene.Bits * (hi - lo) / scene.Duration;
        }
        return total;
    }

    public static double MeanAbsoluteError(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count) throw new ArgumentException("Dizi boylari esit degil.", nameof(b));
        if (a.Count == 0) return 0.0;
        var sum = 0.0;
        for (var i = 0; i < a.Count; i++) sum += Math.Abs(a[i] - b[i]);
        return sum / a.Count;
    }

    public static int TersDusenler(
        IReadOnlyList<double> reference,
        IReadOnlyList<double> first,
        IReadOnlyList<double> second)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var count = 0;
        for (var i = 0; i < reference.Count; i++)
        {
            var da = first[i] - reference[i];
            var db = second[i] - reference[i];
            if (da * db < 0) count++;
        }
        return count;
    }

    public static string Csv(SceneMap map, double[] deserved, double[] given, double[] mapShare)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sahne;bas;son;sure;karmasiklik;hak_edilen;verilen;harita;verilen_fark;harita_fark");
        for (var i = 0; i < map.Scenes.Count; i++)
        {
            var s = map.Scenes[i];
            sb.AppendLine(string.Join(';', new[]
            {
                s.Index.ToString(CultureInfo.InvariantCulture),
                Kabuk.Inv(s.Start, "0.###"),
                Kabuk.Inv(s.End, "0.###"),
                Kabuk.Inv(s.Duration, "0.###"),
                Kabuk.Inv(s.Complexity, "0.####"),
                Kabuk.Inv(deserved[i] * 100, "0.####"),
                Kabuk.Inv(given[i] * 100, "0.####"),
                Kabuk.Inv(mapShare[i] * 100, "0.####"),
                Kabuk.Inv((given[i] - deserved[i]) * 100, "0.####"),
                Kabuk.Inv((mapShare[i] - deserved[i]) * 100, "0.####")
            }));
        }
        return sb.ToString();
    }
}
