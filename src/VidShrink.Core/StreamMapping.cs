using System.Globalization;

namespace VidShrink.Core;

public enum StreamKind { Video, Audio, Subtitle, Attachment, Data }

public enum OutputContainer { Mp4, Mkv, WebM, Mov }

public enum TrackAction { Encode, Copy }

public enum StreamNote
{
    AudioPassthrough,
    AudioDownmixedToStereo,
    ExtraAudioDropped,
    TextSubtitleConverted,
    ImageSubtitleDropped,
    SubtitleDroppedForPlatform,
    KeepAllTracksOverriddenByPlatform,
    LosslessAudioNotPassedThrough,
    AudioCodecNotInContainer,
    DolbyCodecNotInContainer,
    DolbyCodecBelowChannelFloor,
    FlacFellBack,
    AudioFilterSkippedOnCopy
}

public sealed record SourceStream(
    int Index,
    StreamKind Kind,
    string Codec,
    string? Language = null,
    bool IsDefault = false,
    bool IsForced = false,
    int Channels = 0,
    long BitrateBps = 0,
    long Bytes = 0,
    bool IsAttachedPicture = false,
    string? Title = null,
    int SampleRate = 0);

/// <summary>
/// Kaynagin yanindan ya da <c>--altyazi</c> ile gelen metin altyazi dosyasi. Cikti ona ek bir
/// <c>-i</c> girdisi olarak baglanir; dil adindan okunur (<c>film.tr.srt</c> → <c>tur</c>).
/// </summary>
public sealed record ExternalSubtitle(string Path, string Codec, string? Language, long Bytes)
{
    public static readonly string[] Extensions = { ".srt", ".ass", ".ssa", ".vtt" };

    public static ExternalSubtitle? FromFile(string path)
    {
        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var codec = extension switch
        {
            ".srt" => "subrip",
            ".ass" or ".ssa" => "ass",
            ".vtt" => "webvtt",
            _ => null
        };
        if (codec is null || !File.Exists(path)) return null;
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        var dot = stem.LastIndexOf('.');
        var language = dot > 0 ? StreamMapping.LanguageTag(stem[(dot + 1)..]) : null;
        return new ExternalSubtitle(System.IO.Path.GetFullPath(path), codec, language, new FileInfo(path).Length);
    }

    /// <summary>
    /// Girdinin yanindaki altyazi dosyalari: ayni govdeyle baslayan (<c>film.srt</c>,
    /// <c>film.tr.srt</c>) ve desteklenen uzantili olanlar, ada gore sirali.
    /// </summary>
    public static IReadOnlyList<ExternalSubtitle> Sidecars(string inputPath)
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(inputPath));
        if (folder is null || !Directory.Exists(folder)) return Array.Empty<ExternalSubtitle>();
        var stem = System.IO.Path.GetFileNameWithoutExtension(inputPath);
        return Directory.EnumerateFiles(folder)
            .Where(file => Extensions.Contains(System.IO.Path.GetExtension(file).ToLowerInvariant()))
            .Where(file =>
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                return name.Equals(stem, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(FromFile)
            .OfType<ExternalSubtitle>()
            .ToList();
    }
}

/// <summary>
/// Akis kararinin girdisi. Ses suzgeci (<see cref="AudioLoudnorm"/>, <see cref="AudioGainDb"/>),
/// dis altyazilar ve yakilan altyazi da burada tasinir: <see cref="StreamMapping.ForOutput"/>
/// kap degisince karari bu istekten yeniden kurar, alan burada durmazsa orada kaybolur.
/// <see cref="BurnedSubtitle"/> kaynagin altyazi sirasi (0 tabanli, ffmpeg'in <c>si</c>'si).
/// </summary>
public sealed record StreamRequest(
    bool KeepAllTracks = false,
    bool PlatformDelivery = false,
    string? PreferredLanguage = null,
    bool AudioLoudnorm = false,
    double? AudioGainDb = null,
    IReadOnlyList<ExternalSubtitle>? ExternalSubtitles = null,
    int? BurnedSubtitle = null)
{
    public static StreamRequest Default { get; } = new();

    public bool FiltersAudio => AudioLoudnorm || AudioGainDb is { } gain && gain != 0;
}

public sealed record AudioTrack(string Map, TrackAction Action, string Codec, int BitrateK, int? Channels, string? Language, string? Title = null,
    string? Filter = null)
{
    public bool Copies => Action == TrackAction.Copy;
}

public sealed record SubtitleTrack(string Map, string Codec, bool Image, string? Language, long Bytes,
    string? Title = null, bool IsDefault = false, bool IsForced = false, string? InputPath = null)
{
    /// <summary>
    /// Kaynağın bayrakları <c>-disposition</c>'a açık yazılır: mp4 muxer'ı yazılmazsa ilk
    /// altyazıyı kendiliğinden varsayılan yapıyor, MKV yapmıyor
    /// (<c>docs/olcumler/e6-altyazi-bayragi-iz-adi.md</c>).
    /// </summary>
    public string Disposition => (IsDefault, IsForced) switch
    {
        (true, true) => "default+forced",
        (true, false) => "default",
        (false, true) => "forced",
        _ => "0"
    };
}

public sealed record StreamPlan(
    OutputContainer Container,
    string VideoMap,
    IReadOnlyList<AudioTrack> Audio,
    IReadOnlyList<SubtitleTrack> Subtitles,
    IReadOnlyList<string> Attachments,
    IReadOnlyList<StreamNote> Notes,
    double SideK,
    StreamRequest Request,
    string? CoverMap = null)
{
    public string Extension => StreamMapping.ExtensionOf(Container);

    /// <summary>
    /// Ana girdiden sonra eklenen <c>-i</c> girdileri, sirasiyla: dis altyazi dosyalari.
    /// Esleme <c>1:0</c>, <c>2:0</c>... bu siradan turer.
    /// </summary>
    public IReadOnlyList<string> ExtraInputs => Subtitles.Where(track => track.InputPath is not null).Select(track => track.InputPath!).ToList();

    public IReadOnlyList<string> OutputArguments(bool dropChapters = false)
    {
        var a = new List<string> { "-map", VideoMap };
        if (CoverMap is not null) a.AddRange(new[] { "-map", CoverMap });
        foreach (var track in Audio) a.AddRange(new[] { "-map", track.Map });
        foreach (var track in Subtitles) a.AddRange(new[] { "-map", track.Map });
        foreach (var map in Attachments) a.AddRange(new[] { "-map", map });

        if (Audio.Count == 0)
            a.Add("-an");
        else if (Audio.Count == 1)
            a.AddRange(AudioCodecArguments(Audio[0], ""));
        else
            for (var i = 0; i < Audio.Count; i++)
                a.AddRange(AudioCodecArguments(Audio[i], ":" + i.ToString(CultureInfo.InvariantCulture)));

        if (Subtitles.Count > 0)
        {
            if (Subtitles.Select(track => track.Codec).Distinct().Count() == 1)
                a.AddRange(new[] { "-c:s", Subtitles[0].Codec });
            else
                for (var i = 0; i < Subtitles.Count; i++)
                    a.AddRange(new[] { "-c:s:" + i.ToString(CultureInfo.InvariantCulture), Subtitles[i].Codec });
        }

        if (CoverMap is not null) a.AddRange(new[] { "-c:v:1", "copy", "-disposition:v:1", "attached_pic" });
        if (Attachments.Count > 0) a.AddRange(new[] { "-c:t", "copy" });
        if (!Request.KeepAllTracks && Audio.Count == 1) a.AddRange(new[] { "-disposition:a:0", "default" });

        for (var i = 0; i < Audio.Count; i++)
            if (!string.IsNullOrWhiteSpace(Audio[i].Title))
                a.AddRange(new[] { "-metadata:s:a:" + i.ToString(CultureInfo.InvariantCulture), "title=" + Audio[i].Title });

        for (var i = 0; i < Subtitles.Count; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(Subtitles[i].Title))
                a.AddRange(new[] { "-metadata:s:s:" + index, "title=" + Subtitles[i].Title });
            if (Subtitles[i].InputPath is not null && !string.IsNullOrWhiteSpace(Subtitles[i].Language))
                a.AddRange(new[] { "-metadata:s:s:" + index, "language=" + Subtitles[i].Language });
            a.AddRange(new[] { "-disposition:s:" + index, Subtitles[i].Disposition });
        }

        a.AddRange(new[] { "-map_metadata", "0", "-map_chapters", dropChapters ? "-1" : "0" });
        return a;
    }

    private static IEnumerable<string> AudioCodecArguments(AudioTrack track, string suffix)
    {
        if (track.Copies)
            return new[] { "-c:a" + suffix, "copy" };
        var a = new List<string> { "-c:a" + suffix, track.Codec };
        if (StreamMapping.IsFlac(track.Codec))
            a.AddRange(new[] { "-sample_fmt" + (suffix.Length == 0 ? "" : ":a" + suffix), "s16" });
        else
            a.AddRange(new[] { "-b:a" + suffix, track.BitrateK.ToString(CultureInfo.InvariantCulture) + "k" });
        a.AddRange(new[] { "-filter:a" + suffix, track.Filter ?? StreamMapping.SesHizalama });
        if (track.Channels is > 0) a.AddRange(new[] { suffix.Length == 0 ? "-ac" : "-ac:a" + suffix, track.Channels.Value.ToString(CultureInfo.InvariantCulture) });
        return a;
    }
}

public static class StreamMapping
{
    /// <summary>
    /// Sesi videoyla ayni anda baslatan suzgec: kaynakta ses videodan gec basliyorsa
    /// (kap icinde gecikme olarak duruyorsa) basina sessizlik doldurulur. HandBrake'in
    /// <c>--align-av</c>'sinin karsiligi. Hizali kaynakta cikti <b>bayt bayt ayni</b> kalir
    /// (<c>docs/olcumler/e8-kap-uyumlulugu.md</c>), yani bedeli yalniz bozuk kaynakta odenir.
    /// Yalniz sesin yeniden kodlandigi izde islenir; kopyalanan izde suzgec kurulamaz.
    /// </summary>
    public const string SesHizalama = "aresample=async=1:first_pts=0";

    /// <summary>
    /// ffmpeg'in <c>loudnorm</c> varsayilanlari acik yazili: I=-24 LUFS, LRA=7, TP=-2 dBTP.
    /// Suzgec ciktisini 192 kHz'e cikariyor; zincirin sonundaki <c>aresample</c> kaynak hizina dondurur.
    /// </summary>
    public const string LoudnormFilter = "loudnorm=I=-24:LRA=7:TP=-2";

    public const double MinGainDb = -20;
    public const double MaxGainDb = 20;
    public const int DefaultSampleRate = 48000;

    /// <summary>flac'in butceye giren tavani: 16 bit PCM. <c>-sample_fmt s16</c> ile yazilir.</summary>
    public const int FlacBitsPerSample = 16;

    private static readonly string[] CoverCodecs = { "mjpeg", "png", "bmp" };

    public const double PassthroughTargetShare = 0.15;
    public const int MinimumTrackK = 24;

    private static readonly string[] TextSubtitleCodecs = { "subrip", "srt", "ass", "ssa", "mov_text", "webvtt", "text" };
    private static readonly string[] ImageSubtitleCodecs = { "hdmv_pgs_subtitle", "dvd_subtitle", "dvb_subtitle", "xsub" };
    private static readonly string[] NeverPassedThrough = { "truehd", "mlp", "dts" };

    private static readonly IReadOnlyDictionary<OutputContainer, string[]> CopyableAudio = new Dictionary<OutputContainer, string[]>
    {
        [OutputContainer.Mp4] = new[] { "aac", "ac3", "eac3", "opus", "mp3", "flac" },
        [OutputContainer.Mkv] = new[] { "aac", "ac3", "eac3", "opus", "mp3", "flac", "vorbis" },
        [OutputContainer.WebM] = new[] { "opus", "vorbis" },
        [OutputContainer.Mov] = new[] { "aac", "ac3", "eac3", "mp3", "alac" }
    };

    private static readonly IReadOnlyDictionary<string, string[]> LanguageCodes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = new[] { "ara" }, ["bg"] = new[] { "bul" }, ["bn"] = new[] { "ben" }, ["cs"] = new[] { "ces", "cze" },
        ["da"] = new[] { "dan" }, ["de"] = new[] { "deu", "ger" }, ["el"] = new[] { "ell", "gre" }, ["en"] = new[] { "eng" },
        ["es"] = new[] { "spa" }, ["et"] = new[] { "est" }, ["fa"] = new[] { "fas", "per" }, ["fi"] = new[] { "fin" },
        ["fr"] = new[] { "fra", "fre" }, ["he"] = new[] { "heb" }, ["hi"] = new[] { "hin" }, ["hr"] = new[] { "hrv" },
        ["hu"] = new[] { "hun" }, ["id"] = new[] { "ind" }, ["it"] = new[] { "ita" }, ["ja"] = new[] { "jpn" },
        ["ko"] = new[] { "kor" }, ["lt"] = new[] { "lit" }, ["lv"] = new[] { "lav" }, ["ms"] = new[] { "msa", "may" },
        ["nb"] = new[] { "nob", "nor" }, ["nl"] = new[] { "nld", "dut" }, ["pl"] = new[] { "pol" }, ["pt"] = new[] { "por" },
        ["ro"] = new[] { "ron", "rum" }, ["ru"] = new[] { "rus" }, ["sk"] = new[] { "slk", "slo" }, ["sl"] = new[] { "slv" },
        ["sr"] = new[] { "srp" }, ["sv"] = new[] { "swe" }, ["sw"] = new[] { "swa" }, ["ta"] = new[] { "tam" },
        ["th"] = new[] { "tha" }, ["tr"] = new[] { "tur" }, ["uk"] = new[] { "ukr" }, ["ur"] = new[] { "urd" },
        ["vi"] = new[] { "vie" }, ["zh"] = new[] { "zho", "chi" }
    };

    /// <summary>
    /// MOV, MP4'un muxer soyundan: <c>+faststart</c>, <c>mov_text</c> altyazi ve tek iz
    /// kurali ikisinde de ayni. Ayrildiklari tek yer ses kopyalama listesi
    /// (<c>docs/netlestirme/022-mov-kabi-kucultmede.md</c>).
    /// </summary>
    public static bool IsMp4Family(OutputContainer container)
        => container is OutputContainer.Mp4 or OutputContainer.Mov;

    public static OutputContainer ContainerFor(StreamRequest request)
        => request.KeepAllTracks && !request.PlatformDelivery ? OutputContainer.Mkv : OutputContainer.Mp4;

    public static OutputContainer ContainerOf(string outputPath) => Path.GetExtension(outputPath).ToLowerInvariant() switch
    {
        ".mkv" => OutputContainer.Mkv,
        ".webm" => OutputContainer.WebM,
        ".mov" => OutputContainer.Mov,
        _ => OutputContainer.Mp4
    };

    public static string ExtensionOf(OutputContainer container) => container switch
    {
        OutputContainer.Mkv => "mkv",
        OutputContainer.WebM => "webm",
        OutputContainer.Mov => "mov",
        _ => "mp4"
    };

    /// <summary>
    /// ac3'un kapali bit hizi merdiveni. Olculdu, uydurulmadi:
    /// <c>docs/olcumler/b1a-ac3-merdiveni.md</c>. ffmpeg istegi reddetmiyor, sessizce bu
    /// merdivene oturtuyor; butceye giren sayi ile teslim edilen sayi ayrisirsa hedef boyut
    /// hesabi yanlis olacagi icin oturtmayi urun kendisi yapiyor.
    /// </summary>
    public static readonly int[] Ac3LadderK =
        { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 512, 576, 640 };

    /// <summary>
    /// Kanal sayisinin dayattigi alt sinir. Bu kol ffmpeg'de <b>gercekten hata veriyor</b>
    /// (kodlayici acilmiyor), sessiz oturtma yok — o yuzden urun tabanin altina hic inmiyor.
    /// Olcum <c>docs/olcumler/b1a-ac3-merdiveni.md</c>: dort kanalda ac3 40k, eac3 48k; bes ve
    /// alti kanalda ikisi de 48k; ucun altinda ikisi de 32k.
    /// </summary>
    public static int ChannelFloorK(string codec, int channels) => channels switch
    {
        >= 5 => 48,
        4 => codec.Equals("eac3", StringComparison.OrdinalIgnoreCase) ? 48 : 40,
        _ => 32
    };

    public static bool IsFlac(string? codec) => codec is not null && codec.Equals("flac", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// flac izinin butce tavani, kbit/sn: ornekleme hizi × kanal × 16 bit. flac kayipsiz ve
    /// degisken; sikistirma bu tavanin altinda kalir, ustune cikmaz.
    /// </summary>
    public static int FlacCeilingK(int sampleRate, int channels)
        => (int)Math.Ceiling((sampleRate > 0 ? sampleRate : DefaultSampleRate) * (double)Math.Max(channels, 1) * FlacBitsPerSample / 1000.0);

    /// <summary>
    /// Yeniden kodlanan izin ses suzgeci. Istek yoksa yalniz hizalama; kazanc ve normallestirme
    /// hizalamadan sonra, <c>loudnorm</c>'un 192 kHz'i ise sondaki <c>aresample</c> ile kaynak hizina doner.
    /// </summary>
    public static string AudioFilter(StreamRequest request, int sampleRate)
    {
        if (!request.FiltersAudio) return SesHizalama;
        var chain = new List<string> { SesHizalama };
        if (request.AudioGainDb is { } gain && gain != 0)
            chain.Add("volume=" + gain.ToString("0.##", CultureInfo.InvariantCulture) + "dB");
        if (request.AudioLoudnorm)
        {
            chain.Add(LoudnormFilter);
            chain.Add("aresample=" + (sampleRate > 0 ? sampleRate : DefaultSampleRate).ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(',', chain);
    }

    /// <summary>
    /// Dosya adindaki dil etiketini ffmpeg'in uc harfli koduna cevirir: <c>tr</c> → <c>tur</c>.
    /// Zaten uc harfli bilinen kod oldugu gibi doner; tanimayan etiket <c>null</c>.
    /// </summary>
    public static string? LanguageTag(string tag)
    {
        if (LanguageCodes.TryGetValue(tag, out var codes)) return codes[0];
        var known = LanguageCodes.Values.SelectMany(list => list).FirstOrDefault(code => code.Equals(tag, StringComparison.OrdinalIgnoreCase));
        return known?.ToLowerInvariant();
    }

    /// <summary>
    /// Kapak resmi (<c>attached_pic</c>) yalniz MP4'te tasinir. Olculdu
    /// (<c>docs/olcumler/b1-kalan-mutasyonlar.md</c>): ffmpeg 9.0'da MOV muxer'i resmi sessizce
    /// dusuruyor, Matroska muxer'i onu kapak degil tek kareli duz bir video izi olarak yaziyor.
    /// </summary>
    public static bool CarriesCover(OutputContainer container) => container == OutputContainer.Mp4;

    public static bool IsDolby(string? codec)
        => codec is not null
            && (codec.Equals("ac3", StringComparison.OrdinalIgnoreCase) || codec.Equals("eac3", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ac3 merdivene oturur, eac3 istegi neredeyse birebir teslim eder; tek ortak kisit
    /// kanal tabani. Olcum <c>docs/olcumler/b1a-ac3-merdiveni.md</c>.
    /// </summary>
    public static int FitDolbyBitrateK(string codec, int requestedK)
    {
        if (codec.Equals("eac3", StringComparison.OrdinalIgnoreCase)) return requestedK;
        var fitting = Ac3LadderK.Where(step => step <= requestedK).ToList();
        return fitting.Count > 0 ? fitting[^1] : Ac3LadderK[0];
    }

    /// <summary>ac3/eac3 kurulamadiginda donulen kodek; kabin kendi varsayilani.</summary>
    public static string FallbackAudioCodec(OutputContainer container) => IsMp4Family(container) ? "aac" : "libopus";

    public static bool IsTextSubtitle(string codec) => TextSubtitleCodecs.Contains(codec, StringComparer.OrdinalIgnoreCase);

    public static bool IsImageSubtitle(string codec) => ImageSubtitleCodecs.Contains(codec, StringComparer.OrdinalIgnoreCase);

    public static bool LanguageMatches(string? streamLanguage, string? preferred)
    {
        if (string.IsNullOrWhiteSpace(streamLanguage) || string.IsNullOrWhiteSpace(preferred)) return false;
        var primary = preferred.Split('-', '_')[0];
        if (streamLanguage.Equals(primary, StringComparison.OrdinalIgnoreCase)) return true;
        return LanguageCodes.TryGetValue(primary, out var codes) && codes.Contains(streamLanguage, StringComparer.OrdinalIgnoreCase);
    }

    public static SourceStream? PrimaryAudio(IReadOnlyList<SourceStream> streams, string? preferredLanguage)
    {
        var audio = streams.Where(stream => stream.Kind == StreamKind.Audio).ToList();
        if (audio.Count == 0) return null;
        var inLanguage = audio.Where(stream => LanguageMatches(stream.Language, preferredLanguage)).ToList();
        if (inLanguage.Count > 0) return inLanguage.FirstOrDefault(stream => stream.IsDefault) ?? inLanguage[0];
        return audio.FirstOrDefault(stream => stream.IsDefault) ?? audio[0];
    }

    public static StreamPlan Decide(
        MediaInfo info,
        StreamRequest request,
        OutputContainer container,
        int audioK,
        int? audioChannels,
        string? audioCodec,
        bool allowPassthrough,
        double targetMb,
        bool preserveSourceChannels = false)
    {
        var notes = new List<StreamNote>();
        var duration = Math.Max(info.DurationSeconds, 0.1);
        var inventory = info.Streams.Count > 0;

        if (request.KeepAllTracks && request.PlatformDelivery)
            notes.Add(StreamNote.KeepAllTracksOverriddenByPlatform);
        var keepAll = request.KeepAllTracks && !request.PlatformDelivery && !IsMp4Family(container);

        var video = info.Streams.FirstOrDefault(stream => stream.Kind == StreamKind.Video && !stream.IsAttachedPicture);
        var videoMap = video is null ? "0:v:0" : Map(video);

        var audio = new List<AudioTrack>();
        if (audioCodec is not null && audioK > 0 && (info.HasAudio || !inventory))
        {
            var sources = inventory
                ? (keepAll
                    ? info.Streams.Where(stream => stream.Kind == StreamKind.Audio).ToList()
                    : new List<SourceStream> { PrimaryAudio(info.Streams, request.PreferredLanguage)! })
                : new List<SourceStream> { new(-1, StreamKind.Audio, info.AudioCodec ?? "", Channels: info.AudioChannels, BitrateBps: info.AudioBitrateBps) };

            if (!keepAll && inventory && info.Streams.Count(stream => stream.Kind == StreamKind.Audio) > 1)
                notes.Add(StreamNote.ExtraAudioDropped);

            var passthroughBudgetK = targetMb > 0 ? targetMb * PassthroughTargetShare * 8388.608 / duration : 0;
            var passedK = 0.0;
            foreach (var source in sources)
            {
                var map = source.Index < 0 ? "0:a:0?" : Map(source);
                var sourceK = source.BitrateBps / 1000.0;
                if (audioCodec == "copy")
                {
                    audio.Add(new AudioTrack(map, TrackAction.Copy, "copy", (int)Math.Round(sourceK > 0 ? sourceK : audioK), null, source.Language, source.Title));
                    if (request.FiltersAudio) notes.Add(StreamNote.AudioFilterSkippedOnCopy);
                    continue;
                }

                var lossless = NeverPassedThrough.Contains(source.Codec, StringComparer.OrdinalIgnoreCase);
                if (lossless && allowPassthrough) notes.Add(StreamNote.LosslessAudioNotPassedThrough);
                var copyable = allowPassthrough
                    && !request.FiltersAudio
                    && inventory
                    && !lossless
                    && CopyableAudio[container].Contains(source.Codec, StringComparer.OrdinalIgnoreCase)
                    && sourceK > 0
                    && sourceK <= audioK
                    && passedK + sourceK <= passthroughBudgetK;

                if (copyable)
                {
                    passedK += sourceK;
                    audio.Add(new AudioTrack(map, TrackAction.Copy, "copy", (int)Math.Round(sourceK), null, source.Language, source.Title));
                    notes.Add(StreamNote.AudioPassthrough);
                    continue;
                }

                if (allowPassthrough && inventory && !lossless
                    && !CopyableAudio[container].Contains(source.Codec, StringComparer.OrdinalIgnoreCase)
                    && CopyableAudio.Values.Any(list => list.Contains(source.Codec, StringComparer.OrdinalIgnoreCase)))
                    notes.Add(StreamNote.AudioCodecNotInContainer);

                var channels = audioChannels;
                if (channels is null && source.Channels > 2)
                {
                    if (preserveSourceChannels) channels = source.Channels;
                    else
                    {
                        channels = 2;
                        notes.Add(StreamNote.AudioDownmixedToStereo);
                    }
                }

                var codec = audioCodec;
                var trackK = audioK;
                if (IsDolby(codec))
                {
                    var wide = channels ?? (source.Channels > 0 ? source.Channels : 2);
                    if (!CopyableAudio[container].Contains(codec, StringComparer.OrdinalIgnoreCase))
                    {
                        codec = FallbackAudioCodec(container);
                        notes.Add(StreamNote.DolbyCodecNotInContainer);
                    }
                    else if (audioK < ChannelFloorK(codec, wide))
                    {
                        codec = FallbackAudioCodec(container);
                        notes.Add(StreamNote.DolbyCodecBelowChannelFloor);
                    }
                    else trackK = FitDolbyBitrateK(codec, audioK);
                }
                else if (IsFlac(codec))
                {
                    var ceiling = FlacCeilingK(source.SampleRate, channels ?? (source.Channels > 0 ? source.Channels : 2));
                    if (!CopyableAudio[container].Contains(codec, StringComparer.OrdinalIgnoreCase)
                        || passedK + ceiling > passthroughBudgetK)
                    {
                        codec = FallbackAudioCodec(container);
                        notes.Add(StreamNote.FlacFellBack);
                    }
                    else
                    {
                        passedK += ceiling;
                        trackK = ceiling;
                    }
                }
                else if (!IsMp4Family(container) && codec == "aac") codec = "libopus";

                audio.Add(new AudioTrack(map, TrackAction.Encode, codec, trackK, channels, source.Language, source.Title,
                    request.FiltersAudio ? AudioFilter(request, source.SampleRate) : null));
            }
        }

        var subtitles = new List<SubtitleTrack>();
        var subtitleOrdinal = -1;
        foreach (var source in info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle))
        {
            subtitleOrdinal++;
            if (request.BurnedSubtitle == subtitleOrdinal) continue;
            if (request.PlatformDelivery)
            {
                notes.Add(StreamNote.SubtitleDroppedForPlatform);
                continue;
            }

            var text = IsTextSubtitle(source.Codec);
            var image = IsImageSubtitle(source.Codec);
            if (IsMp4Family(container))
            {
                if (text)
                {
                    subtitles.Add(new SubtitleTrack(Map(source), "mov_text", false, source.Language, source.Bytes, source.Title, source.IsDefault, source.IsForced));
                    if (!source.Codec.Equals("mov_text", StringComparison.OrdinalIgnoreCase)) notes.Add(StreamNote.TextSubtitleConverted);
                }
                else if (image) notes.Add(StreamNote.ImageSubtitleDropped);
                continue;
            }

            if (container == OutputContainer.WebM)
            {
                if (text) subtitles.Add(new SubtitleTrack(Map(source), "webvtt", false, source.Language, source.Bytes, source.Title, source.IsDefault, source.IsForced));
                else if (image) notes.Add(StreamNote.ImageSubtitleDropped);
                continue;
            }

            if (text)
            {
                var mov = source.Codec.Equals("mov_text", StringComparison.OrdinalIgnoreCase);
                subtitles.Add(new SubtitleTrack(Map(source), mov ? "srt" : "copy", false, source.Language, source.Bytes, source.Title, source.IsDefault, source.IsForced));
                if (mov) notes.Add(StreamNote.TextSubtitleConverted);
            }
            else if (image)
                subtitles.Add(new SubtitleTrack(Map(source), "copy", true, source.Language, source.Bytes, source.Title, source.IsDefault, source.IsForced));
        }

        foreach (var external in request.ExternalSubtitles ?? Array.Empty<ExternalSubtitle>())
        {
            if (request.PlatformDelivery)
            {
                notes.Add(StreamNote.SubtitleDroppedForPlatform);
                continue;
            }
            var codec = IsMp4Family(container) ? "mov_text" : container == OutputContainer.WebM ? "webvtt" : "copy";
            var input = subtitles.Count(track => track.InputPath is not null) + 1;
            subtitles.Add(new SubtitleTrack(input.ToString(CultureInfo.InvariantCulture) + ":0", codec, false, external.Language, external.Bytes,
                InputPath: external.Path));
        }

        subtitles = DefaultForced(subtitles, request.PreferredLanguage, audio.FirstOrDefault()?.Language);

        string? coverMap = null;
        var coverBytes = 0L;
        if (CarriesCover(container) && !request.PlatformDelivery
            && info.Streams.FirstOrDefault(stream => stream.IsAttachedPicture && CoverCodecs.Contains(stream.Codec, StringComparer.OrdinalIgnoreCase)) is { } cover)
        {
            coverMap = Map(cover);
            coverBytes = cover.Bytes;
        }

        var attachments = new List<string>();
        var attachmentBytes = 0L;
        if (keepAll && container == OutputContainer.Mkv)
            foreach (var source in info.Streams.Where(stream => stream.Kind == StreamKind.Attachment))
            {
                attachments.Add(Map(source));
                attachmentBytes += source.Bytes;
            }

        var subtitleBytes = subtitles.Sum(track => track.Bytes);
        var sideK = audio.Sum(track => (double)track.BitrateK) + (subtitleBytes + attachmentBytes + coverBytes) * 8.0 / 1000.0 / duration;

        return new StreamPlan(container, videoMap, audio, subtitles, attachments, notes.Distinct().ToList(), sideK, request, coverMap);
    }

    /// <summary>
    /// Ciktida hic varsayilan altyazi yoksa zorunlu (forced) bayrakli iz varsayilan olur:
    /// once tercih edilen dilde, sonra tutulan sesin dilinde, yoksa ilki. Varsayilan zaten
    /// varsa ya da zorunlu iz yoksa liste degismez. HandBrake'in "Foreign Audio Search"'u degil;
    /// yalniz kaynagin kendi bayragini okur.
    /// </summary>
    public static List<SubtitleTrack> DefaultForced(List<SubtitleTrack> subtitles, string? preferredLanguage, string? audioLanguage)
    {
        if (subtitles.Any(track => track.IsDefault)) return subtitles;
        var forced = subtitles.Where(track => track.IsForced).ToList();
        if (forced.Count == 0) return subtitles;
        var pick = forced.FirstOrDefault(track => LanguageMatches(track.Language, preferredLanguage))
            ?? forced.FirstOrDefault(track => audioLanguage is not null && string.Equals(track.Language, audioLanguage, StringComparison.OrdinalIgnoreCase))
            ?? forced[0];
        return subtitles.Select(track => ReferenceEquals(track, pick) ? track with { IsDefault = true } : track).ToList();
    }

    public static StreamPlan ForOutput(MediaInfo info, EncodePlan plan, string outputPath)
    {
        var container = ContainerOf(outputPath);
        if (plan.Streams is { } streams && streams.Container == container) return streams;
        var request = plan.Streams?.Request ?? StreamRequest.Default;
        var planned = plan.Streams is not null;
        var codec = plan.AudioCodec;
        if (planned && codec is "copy" or "libopus") codec = "aac";
        return Decide(info, request, container, plan.AudioBitrateK, plan.AudioChannels, codec, planned, plan.EffectiveTargetMb ?? 0);
    }

    private static string Map(SourceStream stream) => "0:" + stream.Index.ToString(CultureInfo.InvariantCulture);
}
