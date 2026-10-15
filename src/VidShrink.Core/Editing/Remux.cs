using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>
/// Yeniden paketlenecek tek iz. <see cref="Language"/> ve <see cref="Title"/> <c>null</c> ise kaynaktaki deger kalir; bos ad
/// izin adini temizler. <see cref="OffsetMs"/> izi zaman ekseninde kaydirir. <see cref="Rotation"/> yalniz goruntu izinde,
/// saat yonunde 0, 90, 180 ya da 270 derecedir; <c>null</c> kaynaktaki donusu birakir.
/// </summary>
public sealed record RemuxTrack(
    int Index,
    StreamKind Kind,
    string? Language = null,
    string? Title = null,
    bool IsDefault = false,
    long OffsetMs = 0,
    int? Rotation = null);

/// <summary>Kaynagin izleri yeniden kodlanmadan <see cref="Output"/>'a yazilir; <see cref="FileTitle"/> <c>null</c> ise dosya basligi kalir.</summary>
public sealed record RemuxRequest(string Source, string Output, IReadOnlyList<RemuxTrack> Tracks, string? FileTitle = null);

/// <summary>
/// Kap ici iz isleri icin <c>-c copy</c> komutu. Cikti izleri <see cref="RemuxRequest.Tracks"/> sirasindadir ve iz basina
/// ust veri cikti sirasiyla yazilir. Kaydirilan iz, ayni kaynagin <c>-itsoffset</c>'li ek girdisinden eslenir; ayni
/// kaydirmayi tasiyan izler tek girdiyi paylasir. Donus <c>-display_rotation</c> girdi secenegiyle yazilir ve ffmpeg saat
/// yonunun tersini bekledigi icin cevrilir.
/// </summary>
public static class Remux
{
    public static bool IsLanguage(string? code)
        => code is { Length: 3 } && code.All(c => c is >= 'a' and <= 'z');

    public static IReadOnlyList<RemuxTrack> TracksOf(MediaInfo info)
        => info.Streams
            .Where(stream => stream.Kind != StreamKind.Data)
            .Select(stream => new RemuxTrack(stream.Index, stream.Kind, stream.Language, stream.Title, stream.IsDefault))
            .ToArray();

    public static IReadOnlyList<string> Arguments(RemuxRequest request)
    {
        if (request.Tracks.Count == 0) throw new ArgumentException("At least one track is required.", nameof(request));
        foreach (var track in request.Tracks)
        {
            if (track.Language is { } language && !IsLanguage(Language(language)))
                throw new ArgumentException($"'{language}' is not a three-letter language code.", nameof(request));
            if (track.Rotation is { } rotation && (track.Kind != StreamKind.Video || rotation is not (0 or 90 or 180 or 270)))
                throw new ArgumentException($"Rotation {rotation} is not allowed on track {track.Index}.", nameof(request));
        }

        var offsets = new List<long> { 0 };
        foreach (var track in request.Tracks)
            if (!offsets.Contains(track.OffsetMs)) offsets.Add(track.OffsetMs);

        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        for (var input = 0; input < offsets.Count; input++)
        {
            if (offsets[input] != 0) args.AddRange(new[] { "-itsoffset", Seconds(offsets[input]) });
            foreach (var track in request.Tracks.Where(t => t.OffsetMs == offsets[input] && t.Rotation is not null))
                args.AddRange(new[] { "-display_rotation:" + Number(track.Index), Number((360 - track.Rotation!.Value) % 360) });
            args.AddRange(new[] { "-i", request.Source });
        }

        foreach (var track in request.Tracks)
            args.AddRange(new[] { "-map", Number(offsets.IndexOf(track.OffsetMs)) + ":" + Number(track.Index) });

        args.AddRange(new[] { "-c", "copy" });
        if (request.FileTitle is { } title) args.AddRange(new[] { "-metadata", "title=" + title });
        for (var output = 0; output < request.Tracks.Count; output++)
        {
            var track = request.Tracks[output];
            var spec = Number(output);
            if (track.Language is { } language) args.AddRange(new[] { "-metadata:s:" + spec, "language=" + Language(language) });
            if (track.Title is { } name) args.AddRange(new[] { "-metadata:s:" + spec, "title=" + name });
            args.AddRange(new[] { "-disposition:" + spec, track.IsDefault ? "+default" : "-default" });
        }

        args.Add(request.Output);
        return args;
    }

    private static string Language(string code) => code.Trim().ToLowerInvariant();

    private static string Seconds(long milliseconds) => (milliseconds / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
