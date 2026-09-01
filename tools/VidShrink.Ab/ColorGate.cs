using VidShrink.Core;

namespace VidShrink.Ab;

public enum ColorGateKind
{
    Direct,
    ReferenceTransformed,
    Rejected
}

public sealed record ColorSignature(
    string? Primaries,
    string? Transfer,
    string? Space,
    string? PixelFormat,
    bool IsHdr)
{
    public static ColorSignature From(MediaInfo info)
        => new(info.ColorPrimaries, info.ColorTransfer, info.ColorSpace, info.PixelFormat, info.IsHdr);

    public string Describe()
        => $"{Or(Primaries)}/{Or(Transfer)}/{Or(Space)}/{Or(PixelFormat)}";

    private static string Or(string? value)
        => string.IsNullOrWhiteSpace(value) ? "etiketsiz" : value;
}

public sealed record ColorGateDecision(ColorGateKind Kind, string Label, string Reason)
{
    public bool Measurable => Kind != ColorGateKind.Rejected;
}

public static class ColorGate
{
    public const string SdrLabel = "SDR uzayında karşılaştırma — HDR kaybı hariç";
    public const string DirectLabel = "aynı renk uzayında doğrudan karşılaştırma";

    public static ColorGateDecision Decide(ColorSignature reference, ColorSignature candidate)
    {
        var referenceGaps = MissingTags(reference);
        if (referenceGaps is not null)
            return Reject($"Referansın renk etiketleri eksik ({referenceGaps}); varsayım yapılmaz.");

        var candidateGaps = MissingTags(candidate);
        if (candidateGaps is not null)
            return Reject($"Çıktının renk etiketleri eksik ({candidateGaps}); etiketsiz çıktı etiketli referansla karşılaştırılmaz.");

        if (reference.PixelFormat is null || candidate.PixelFormat is null)
            return Reject("pix_fmt okunamadı.");

        if (reference.IsHdr && candidate.IsHdr)
        {
            if (!Same(reference.Transfer, candidate.Transfer) || !Same(reference.Primaries, candidate.Primaries) || !Same(reference.Space, candidate.Space))
                return Reject($"İki taraf da HDR ama uzaylar ayrı: referans {reference.Describe()}, çıktı {candidate.Describe()}.");
            return new ColorGateDecision(
                ColorGateKind.Direct,
                DirectLabel,
                $"Referans ve çıktı aynı {reference.Primaries}/{reference.Transfer}/{reference.Space} uzayında.");
        }

        if (!reference.IsHdr && !candidate.IsHdr)
        {
            if (!Same(reference.Transfer, candidate.Transfer) || !Same(reference.Primaries, candidate.Primaries) || !Same(reference.Space, candidate.Space))
                return Reject($"İki taraf da SDR ama uzaylar ayrı: referans {reference.Describe()}, çıktı {candidate.Describe()}.");
            return new ColorGateDecision(
                ColorGateKind.Direct,
                DirectLabel,
                $"Referans ve çıktı aynı {reference.Primaries}/{reference.Transfer}/{reference.Space} uzayında.");
        }

        if (reference.IsHdr && !candidate.IsHdr)
        {
            if (!Same(candidate.Transfer, "bt709") || !Same(candidate.Primaries, "bt709") || !Same(candidate.Space, "bt709"))
                return Reject($"HDR referans yalnız bt709 SDR çıktıya indirgenebilir; çıktı {candidate.Describe()}.");
            return new ColorGateDecision(
                ColorGateKind.ReferenceTransformed,
                SdrLabel,
                $"Çıktı SDR bt709; referans aynı dönüşümden ({HdrResolver.TonemapFilter}) geçirilip öyle ölçüldü.");
        }

        return Reject($"Referans SDR, çıktı HDR ({candidate.Describe()}); referans yükseltilemez.");
    }

    private static ColorGateDecision Reject(string reason)
        => new(ColorGateKind.Rejected, "ölçülmedi", reason);

    private static string? MissingTags(ColorSignature signature)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(signature.Primaries)) missing.Add("color_primaries");
        if (string.IsNullOrWhiteSpace(signature.Transfer)) missing.Add("color_transfer");
        if (string.IsNullOrWhiteSpace(signature.Space)) missing.Add("color_space");
        return missing.Count == 0 ? null : string.Join(", ", missing);
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed record RateGateDecision(bool Comparable, string Reason);

public static class RateGate
{
    public const double ToleranceFps = 0.01;

    public static RateGateDecision Check(double referenceFps, double candidateFps)
        => Math.Abs(referenceFps - candidateFps) <= ToleranceFps
            ? new RateGateDecision(true, $"kare hızı eşit ({referenceFps:0.###} fps)")
            : new RateGateDecision(false, $"Kare hızı ayrı: referans {referenceFps:0.###} fps, çıktı {candidateFps:0.###} fps; kare kare hizalama bozulur.");
}
