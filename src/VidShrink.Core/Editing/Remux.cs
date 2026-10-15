using System.Globalization;

namespace VidShrink.Core.Editing;

/// <summary>
/// Yeniden paketlenecek tek iz. <see cref="Language"/> ve <see cref="Title"/> <c>null</c> ise kaynaktaki deger kalir; bos ad
/// izin adini temizler. <see cref="OffsetMs"/> izi zaman ekseninde kaydirir. <see cref="Rotation"/> yalniz goruntu izinde,
/// saat yonunde 0, 90, 180 ya da 270 derecedir; <c>null</c> kaynaktaki donusu birakir. <see cref="Source"/> doluysa iz o
/// dosyanin <see cref="Index"/>'li akisidir; <c>null</c> istegin kaynagidir.
/// </summary>
public sealed record RemuxTrack(
    int Index,
    StreamKind Kind,
    string? Language = null,
    string? Title = null,
    bool IsDefault = false,
    long OffsetMs = 0,
    int? Rotation = null,
    string? Source = null);

/// <summary>
/// Izler yeniden kodlanmadan <see cref="Output"/>'a yazilir; <see cref="FileTitle"/> <c>null</c> ise dosya basligi kalir.
/// <see cref="Cover"/> PNG ya da JPEG kapak resmidir; yalniz <see cref="Remux.CarriesCover"/> kaplarinda gecerlidir.
/// </summary>
public sealed record RemuxRequest(string Source, string Output, IReadOnlyList<RemuxTrack> Tracks, string? FileTitle = null, string? Cover = null);

/// <summary>
/// Kap ici iz isleri icin <c>-c copy</c> komutu. Cikti izleri <see cref="RemuxRequest.Tracks"/> sirasindadir ve iz basina
/// ust veri cikti sirasiyla yazilir. Her dosya ve kaydirma cifti bir girdidir: kaydirilan iz, dosyasinin <c>-itsoffset</c>'li
/// ek girdisinden eslenir, ayni dosya ve kaydirmayi tasiyan izler tek girdiyi paylasir. Donus <c>-display_rotation</c>
/// girdi secenegiyle yazilir ve ffmpeg saat yonunun tersini bekledigi icin cevrilir. Kapak kap kuralina uyar
/// (<c>docs/olcumler/kapak-mkv-mov.md</c>): MP4 onu goruntu izi ve <c>attached_pic</c> ile, MKV <c>-attach</c> eki ve
/// mimetype ile tasir; MOV eslemeyi sessizce attigi, WebM resim tasimadigi icin reddedilir.
/// </summary>
public static class Remux
{
    private static readonly string[] Mp4Extensions = { ".mp4", ".m4v" };

    public static bool IsLanguage(string? code)
        => code is { Length: 3 } && code.All(c => c is >= 'a' and <= 'z');

    public static bool CarriesCover(string output)
        => Mp4Extensions.Contains(Extension(output)) || Extension(output) == ".mkv";

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

        var cover = request.Cover is { } coverPath ? CoverOf(coverPath, request.Output) : default;
        var inputs = new List<(string File, long OffsetMs)> { (request.Source, 0) };
        foreach (var track in request.Tracks)
            if (InputIndex(inputs, request, track) < 0) inputs.Add(InputOf(request, track));

        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        foreach (var input in inputs)
        {
            if (input.OffsetMs != 0) args.AddRange(new[] { "-itsoffset", Seconds(input.OffsetMs) });
            foreach (var track in request.Tracks.Where(t => InputIndex(inputs, request, t) == inputs.IndexOf(input) && t.Rotation is not null))
                args.AddRange(new[] { "-display_rotation:" + Number(track.Index), Number((360 - track.Rotation!.Value) % 360) });
            args.AddRange(new[] { "-i", input.File });
        }

        if (cover.AsVideo) args.AddRange(new[] { "-i", request.Cover! });

        foreach (var track in request.Tracks)
            args.AddRange(new[] { "-map", Number(InputIndex(inputs, request, track)) + ":" + Number(track.Index) });
        if (cover.AsVideo) args.AddRange(new[] { "-map", Number(inputs.Count) + ":0" });

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

        if (cover.AsVideo)
            args.AddRange(new[] { "-disposition:v:" + Number(request.Tracks.Count(t => t.Kind == StreamKind.Video)), "attached_pic" });
        else if (cover.MimeType is { } mime)
        {
            var spec = "-metadata:s:t:" + Number(request.Tracks.Count(t => t.Kind == StreamKind.Attachment));
            args.AddRange(new[] { "-attach", request.Cover!, spec, "mimetype=" + mime, spec, "filename=cover." + cover.Extension });
        }

        args.Add(request.Output);
        return args;
    }

    /// <summary>Tek izi yeniden kodlamadan kendi dosyasina yazan komut; izin ust verisi ffmpeg'in esleme kuraliyla gecer.</summary>
    public static IReadOnlyList<string> ExtractArguments(string source, RemuxTrack track, string output)
        => new[] { "-hide_banner", "-nostdin", "-y", "-i", track.Source ?? source, "-map", "0:" + Number(track.Index), "-c", "copy", output };

    /// <summary>Kodegi kendi tek iz dosyasina kopyalanabilen izler icin uzanti; bilinmeyen kodek her seyi tasiyan MKV'ye gider.</summary>
    public static string ExtractExtension(string? codec) => codec?.ToLowerInvariant() switch
    {
        "aac" or "alac" => ".m4a",
        "mp3" => ".mp3",
        "opus" => ".opus",
        "vorbis" => ".ogg",
        "flac" => ".flac",
        "ac3" => ".ac3",
        "eac3" => ".eac3",
        { } pcm when pcm.StartsWith("pcm_", StringComparison.Ordinal) => ".wav",
        "subrip" => ".srt",
        "ass" or "ssa" => ".ass",
        "webvtt" => ".vtt",
        "hdmv_pgs_subtitle" => ".sup",
        "mov_text" => ".mp4",
        _ => ".mkv"
    };

    private static (string File, long OffsetMs) InputOf(RemuxRequest request, RemuxTrack track) => (track.Source ?? request.Source, track.OffsetMs);

    private static int InputIndex(List<(string File, long OffsetMs)> inputs, RemuxRequest request, RemuxTrack track)
        => inputs.FindIndex(input => input.OffsetMs == track.OffsetMs && PathEquality.Same(input.File, track.Source ?? request.Source));

    private static (bool AsVideo, string? MimeType, string? Extension) CoverOf(string cover, string output)
    {
        var (mime, extension) = Extension(cover) switch
        {
            ".png" => ("image/png", "png"),
            ".jpg" or ".jpeg" => ("image/jpeg", "jpg"),
            _ => throw new ArgumentException($"'{cover}' is not a PNG or JPEG image.", nameof(cover))
        };
        if (Mp4Extensions.Contains(Extension(output))) return (true, null, null);
        if (Extension(output) == ".mkv") return (false, mime, extension);
        throw new ArgumentException($"'{Path.GetExtension(output)}' cannot carry a cover.", nameof(output));
    }

    private static string Extension(string path) => Path.GetExtension(path).ToLowerInvariant();

    private static string Language(string code) => code.Trim().ToLowerInvariant();

    private static string Seconds(long milliseconds) => (milliseconds / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
