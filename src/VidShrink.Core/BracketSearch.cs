using System.Globalization;

namespace VidShrink.Core;

public sealed record SearchProbe(double TargetMb, double Value);

public enum TargetStepKind
{
    Multiplicative,
    Bisect,
    Exhausted
}

public sealed record TargetStep(TargetStepKind Kind, double TargetMb, string Reason);

public static class BracketSearch
{
    public const double MinimumBracketRatio = 0.002;

    public static TargetStep Next(
        IReadOnlyList<SearchProbe> probes,
        Func<double, int> side,
        Func<SearchProbe, double> extrapolate)
    {
        if (probes is null) throw new ArgumentNullException(nameof(probes));
        if (side is null) throw new ArgumentNullException(nameof(side));
        if (extrapolate is null) throw new ArgumentNullException(nameof(extrapolate));
        if (probes.Count == 0) throw new ArgumentException("En az bir yoklama gerekir.", nameof(probes));

        var judged = probes
            .Where(p => double.IsFinite(p.Value) && p.Value > 0)
            .Select(p => (Probe: p, Side: side(p.Value)))
            .ToList();

        var under = judged
            .Where(x => x.Side < 0)
            .OrderByDescending(x => x.Probe.TargetMb)
            .Select(x => x.Probe)
            .FirstOrDefault();

        var over = judged
            .Where(x => x.Side > 0)
            .OrderBy(x => x.Probe.TargetMb)
            .Select(x => x.Probe)
            .FirstOrDefault();

        if (under is not null && over is not null && under.TargetMb < over.TargetMb)
        {
            var span = over.TargetMb - under.TargetMb;
            if (span / over.TargetMb <= MinimumBracketRatio)
                return new TargetStep(TargetStepKind.Exhausted, under.TargetMb,
                    $"kıskaç {Mb(under.TargetMb)}–{Mb(over.TargetMb)} MB'a indi; aradaki aralık yoklandı, " +
                    "iki komşu sonuç da bandın dışında.");

            var middle = under.TargetMb + span / 2.0;
            return new TargetStep(TargetStepKind.Bisect, middle,
                $"kıskaç kuruldu: {Mb(under.TargetMb)} MB → {Value(under.Value)} (altta), " +
                $"{Mb(over.TargetMb)} MB → {Value(over.Value)} (üstte); orta nokta {Mb(middle)} MB.");
        }

        var last = probes[probes.Count - 1];
        if (!double.IsFinite(last.Value) || last.Value <= 0)
            return new TargetStep(TargetStepKind.Exhausted, last.TargetMb, "son yoklama değer vermedi.");

        var scaled = extrapolate(last);
        if (!double.IsFinite(scaled) || scaled <= 0)
            return new TargetStep(TargetStepKind.Exhausted, last.TargetMb, "kestirim geçerli bir hedef vermedi.");

        return new TargetStep(TargetStepKind.Multiplicative, scaled,
            $"kıskaç yok; {Mb(last.TargetMb)} MB → {Value(last.Value)} üzerinden {Mb(scaled)} MB deneniyor.");
    }

    private static string Mb(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Value(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
