using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>
/// Akilli kesimde kenar GOP'u kodlayan esleme: kaynagin kodegi, profili ve piksel bicimi tek bir
/// kodlayici argumanina iner. <see cref="Container"/> ara parcanin kabi, <see cref="OutputTag"/>
/// birlesimde korunacak kap etiketi (yalniz <c>hvc1</c>).
/// </summary>
public sealed record SmartCutEncoding(
    string Encoder, IReadOnlyList<string> VideoArgs, string Container, string Extension, string? OutputTag);

/// <summary>Kodlama sirasindaki bir paket: gosterim zamani ve anahtar kare olup olmadigi.</summary>
public readonly record struct SmartCutPacket(double Time, bool Key);

/// <summary>
/// Govdenin baslayip bitebilecegi anahtar kare. <see cref="Packet"/> kodlama sirasindaki paket
/// numarasidir; iki sinirin farki govdeye kopyalanacak kare sayisini verir.
/// </summary>
public readonly record struct SmartCutPoint(double Time, int Packet);

/// <summary>
/// Akilli kesimin kapisi. Bir kodek ancak olcumde dort olcutu de gectiyse buraya girer
/// (<c>docs/olcumler/akilli-kesme-kodekler.md</c>); girmeyen kodek, eslenemeyen profil ya da
/// piksel bicimi ve makinede bulunmayan kodlayici <c>null</c> doner, teslim Tam kipe duser.
/// </summary>
public static class SmartCutCodec
{
    public const string TransportStream = "mpegts";
    public const string Mp4 = "mp4";

    /// <summary>mp4 ara parcalarin ortak zaman olcegi; farkli olceklerde concat damgayi yeniden olceklemiyor.</summary>
    public const string IntermediateTimescale = "90000";

    /// <summary>ffmpeg, PTS ile aramayan kapta B kareli akista arama noktasini 3/23 sn geri ceker.</summary>
    public const double DtsHeuristicSeconds = 3.0 / 23.0;

    private const double SeekNudgeSeconds = 0.001;

    private const string EightBit = "yuv420p";
    private const string TenBit = "yuv420p10le";

    public static IReadOnlyList<string> Codecs { get; } = new[] { "h264", "hevc", "av1", "vp9" };

    public static SmartCutEncoding? Resolve(MediaInfo info, Func<string, bool>? hasEncoder = null)
    {
        if (info.IsInterlaced) return null;
        var pixel = info.PixelFormat;
        if (pixel is not (EightBit or TenBit)) return null;
        if ((pixel == TenBit) != (info.BitDepth > 8)) return null;

        var encoding = info.VideoCodec.ToLowerInvariant() switch
        {
            "h264" => H264(info, pixel),
            "hevc" => Hevc(info, pixel),
            "av1" => Av1(info, pixel),
            "vp9" => Vp9(info, pixel),
            _ => null
        };
        if (encoding is null) return null;
        return hasEncoder is null || hasEncoder(encoding.Encoder) ? encoding : null;
    }

    /// <summary>
    /// Govde kopyasinin arama payi. PTS ile arayan kapta (mp4 ailesi) ve B karesiz akista 1 ms;
    /// otekilerde ffmpeg'in geri cektigi 3/23 sn'nin 1 ms ustu, yoksa kopya bir onceki anahtar
    /// kareden baslar.
    /// </summary>
    public static double CopySeekNudge(MediaInfo info)
        => SeeksByPts(info.FormatName) || info.VideoDelayFrames == 0
            ? SeekNudgeSeconds
            : DtsHeuristicSeconds + SeekNudgeSeconds;

    public static bool SeeksByPts(string? formatName)
        => formatName is not null && formatName.Split(',').Any(n => n.Trim() is "mov" or "mp4");

    /// <summary>
    /// Kodlama sirasindaki paket dokumunden govde sinirlari. Anahtar kareden sonra cozulup ondan
    /// once gosterilen paket varsa (acik GOP) o anahtar kare sinir olamaz: oncu kareler kesilen
    /// GOP'a basvurur. Zamani okunamayan paket dokumu guvenilmez kilar, sinir donmez.
    /// </summary>
    public static IReadOnlyList<SmartCutPoint> CleanCuts(IReadOnlyList<SmartCutPacket> packets, double startTime)
    {
        var points = new List<SmartCutPoint>();
        if (packets.Any(p => !double.IsFinite(p.Time))) return points;

        var earliestAfter = new double[packets.Count + 1];
        earliestAfter[packets.Count] = double.PositiveInfinity;
        for (var i = packets.Count - 1; i >= 0; i--) earliestAfter[i] = Math.Min(earliestAfter[i + 1], packets[i].Time);

        var latestBefore = double.NegativeInfinity;
        for (var i = 0; i < packets.Count; i++)
        {
            var time = packets[i].Time;
            if (packets[i].Key && latestBefore < time - 1e-6 && earliestAfter[i + 1] > time + 1e-6)
                points.Add(new SmartCutPoint(Math.Max(0, time - startTime), i));
            latestBefore = Math.Max(latestBefore, time);
        }

        return points;
    }

    public static IReadOnlyList<SmartCutPacket> ParsePackets(string csv)
    {
        var packets = new List<SmartCutPacket>();
        foreach (var raw in csv.Split('\n'))
        {
            var fields = raw.Trim().Split(',');
            if (fields.Length < 2) continue;
            var time = double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts) ? pts : double.NaN;
            packets.Add(new SmartCutPacket(time, fields[1].StartsWith('K')));
        }

        return packets;
    }

    private static SmartCutEncoding? H264(MediaInfo info, string pixel)
    {
        var profile = info.VideoProfile switch
        {
            null or "" => string.Empty,
            "Constrained Baseline" or "Baseline" => pixel == EightBit ? "baseline" : null,
            "Main" => pixel == EightBit ? "main" : null,
            "High" => pixel == EightBit ? "high" : null,
            "High 10" => pixel == TenBit ? "high10" : null,
            _ => null
        };
        if (profile is null) return null;
        var args = new List<string> { "-c:v", "libx264", "-preset", "medium", "-crf", EditExport.FullCrf, "-pix_fmt", pixel };
        if (profile.Length > 0) args.AddRange(new[] { "-profile:v", profile });
        return new SmartCutEncoding("libx264", args, TransportStream, ".ts", null);
    }

    private static SmartCutEncoding? Hevc(MediaInfo info, string pixel)
    {
        var expected = pixel == TenBit ? "Main 10" : "Main";
        if (!string.IsNullOrEmpty(info.VideoProfile) && info.VideoProfile != expected) return null;
        var args = new[] { "-c:v", "libx265", "-preset", "medium", "-crf", EditExport.FullCrf, "-pix_fmt", pixel };
        var tag = string.Equals(info.VideoCodecTag, "hvc1", StringComparison.OrdinalIgnoreCase) ? "hvc1" : null;
        return new SmartCutEncoding("libx265", args, TransportStream, ".ts", tag);
    }

    private static SmartCutEncoding? Av1(MediaInfo info, string pixel)
    {
        if (!string.IsNullOrEmpty(info.VideoProfile) && info.VideoProfile != "Main") return null;
        var args = new[] { "-c:v", "libsvtav1", "-preset", "10", "-crf", EditExport.FullCrf, "-pix_fmt", pixel };
        return new SmartCutEncoding("libsvtav1", args, Mp4, ".mp4", null);
    }

    private static SmartCutEncoding? Vp9(MediaInfo info, string pixel)
    {
        var expected = pixel == TenBit ? "Profile 2" : "Profile 0";
        if (!string.IsNullOrEmpty(info.VideoProfile) && info.VideoProfile != expected) return null;
        var args = new[]
        {
            "-c:v", "libvpx-vp9", "-crf", EditExport.FullCrf, "-b:v", "0", "-deadline", "good", "-cpu-used", "4", "-row-mt", "1",
            "-pix_fmt", pixel
        };
        return new SmartCutEncoding("libvpx-vp9", args, Mp4, ".mp4", null);
    }
}
