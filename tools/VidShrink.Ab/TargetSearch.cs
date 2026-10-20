using System;
using System.Collections.Generic;
using System.Linq;
using VidShrink.Core;

namespace VidShrink.Ab;

public sealed record TargetProbe(double TargetMb, long Bytes);

public static class TargetSearch
{
    public const double MinimumBracketRatio = BracketSearch.MinimumBracketRatio;

    public static TargetStep Next(
        long baselineBytes,
        IReadOnlyList<TargetProbe> probes,
        double tolerancePercent = SizeParityCheck.DefaultTolerancePercent)
    {
        if (baselineBytes <= 0) throw new ArgumentOutOfRangeException(nameof(baselineBytes));
        if (probes is null) throw new ArgumentNullException(nameof(probes));
        if (probes.Count == 0) throw new ArgumentException("En az bir yoklama gerekir.", nameof(probes));

        return BracketSearch.Next(
            probes.Select(p => new SearchProbe(p.TargetMb, p.Bytes)).ToList(),
            bytes =>
            {
                var parity = SizeParityCheck.Evaluate(baselineBytes, (long)bytes, tolerancePercent);
                return parity.Equal ? 0 : Math.Sign(parity.DeltaPercent);
            },
            last => last.TargetMb * (baselineBytes / last.Value));
    }
}
