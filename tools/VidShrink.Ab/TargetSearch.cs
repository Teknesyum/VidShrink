using System;
using System.Collections.Generic;
using System.Linq;

namespace VidShrink.Ab;

public sealed record TargetProbe(double TargetMb, long Bytes);

public enum TargetStepKind
{
    Multiplicative,
    Bisect,
    Exhausted
}

public sealed record TargetStep(TargetStepKind Kind, double TargetMb, string Reason);

public static class TargetSearch
{
    public const double MinimumBracketRatio = 0.002;

    public static TargetStep Next(
        long baselineBytes,
        IReadOnlyList<TargetProbe> probes,
        double tolerancePercent = SizeParityCheck.DefaultTolerancePercent)
    {
        if (baselineBytes <= 0) throw new ArgumentOutOfRangeException(nameof(baselineBytes));
        if (probes is null) throw new ArgumentNullException(nameof(probes));
        if (probes.Count == 0) throw new ArgumentException("En az bir yoklama gerekir.", nameof(probes));

        var judged = probes
            .Where(p => p.Bytes > 0)
            .Select(p => (Probe: p, Parity: SizeParityCheck.Evaluate(baselineBytes, p.Bytes, tolerancePercent)))
            .ToList();

        var under = judged
            .Where(x => !x.Parity.Equal && x.Parity.DeltaPercent < 0)
            .OrderByDescending(x => x.Probe.TargetMb)
            .Select(x => x.Probe)
            .FirstOrDefault();

        var over = judged
            .Where(x => !x.Parity.Equal && x.Parity.DeltaPercent > 0)
            .OrderBy(x => x.Probe.TargetMb)
            .Select(x => x.Probe)
            .FirstOrDefault();

        if (under is not null && over is not null && under.TargetMb < over.TargetMb)
        {
            var span = over.TargetMb - under.TargetMb;
            if (span / over.TargetMb <= MinimumBracketRatio)
                return new TargetStep(TargetStepKind.Exhausted, under.TargetMb,
                    $"kıskaç {under.TargetMb:0.####}–{over.TargetMb:0.####} MB'a indi; aradaki aralık yoklandı, " +
                    "iki komşu teslim de bandın dışında.");

            var middle = under.TargetMb + span / 2.0;
            return new TargetStep(TargetStepKind.Bisect, middle,
                $"kıskaç kuruldu: {under.TargetMb:0.####} MB → {under.Bytes} bayt (altta), " +
                $"{over.TargetMb:0.####} MB → {over.Bytes} bayt (üstte); orta nokta {middle:0.####} MB.");
        }

        var last = probes[probes.Count - 1];
        if (last.Bytes <= 0)
            return new TargetStep(TargetStepKind.Exhausted, last.TargetMb, "son yoklama sıfır bayt verdi.");

        var scaled = last.TargetMb * (baselineBytes / (double)last.Bytes);
        return new TargetStep(TargetStepKind.Multiplicative, scaled,
            $"kıskaç yok; {last.TargetMb:0.####} MB → {last.Bytes} bayt oranıyla {scaled:0.####} MB deneniyor.");
    }
}
