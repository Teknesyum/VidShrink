using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

public enum ExportMode
{
    Fast,
    Smart,
    Full
}

public sealed record ExportStep(IReadOnlyList<string> Args, double DurationSeconds, string? ListPath = null, string? ListContent = null)
{
    /// <summary>Akilli kesimin birlesiminde sesin ayri concat listesi; kosucu video listesiyle birlikte yazar.</summary>
    public string? AudioListPath { get; init; }

    public string? AudioListContent { get; init; }

    /// <summary>Ikinci ve sonraki ses izlerinin ve altyazinin concat listeleri; kosucu adimdan once yazar.</summary>
    public IReadOnlyList<ExportList> ExtraLists { get; init; } = Array.Empty<ExportList>();
}

public sealed record ExportList(string Path, string Content);

/// <summary>Bir kaynagin teslim icin okunmus hali: akis bilgisi, anahtar kareler ve akilli kesim sinirlari.</summary>
public sealed record ExportSource(MediaInfo Info, IReadOnlyList<double> Keyframes, double StartTime, IReadOnlyList<SmartCutPoint>? Cuts = null);

public sealed record ExportPlan(
    ExportMode Requested,
    ExportMode Effective,
    IReadOnlyList<ExportStep> Steps,
    IReadOnlyList<int> ReverseOverLimit,
    double ReverseLimitSeconds,
    string OutputPath,
    string WorkDirectory)
{
    public IReadOnlyList<int> MotionClips { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Hizli kipte her klibin onceki anahtar kareye cekilen basinin kaymasi, saniye, cizelge sirasinda;
    /// yeniden kodlanan klipte 0. Hizli disindaki kiplerde bos.
    /// </summary>
    public IReadOnlyList<double> KeyframeLeads { get; init; } = Array.Empty<double>();

    /// <summary>Cizelgede metin oldugu icin <c>-c copy</c> yolu birakilip Tam'a gecildi.</summary>
    public bool TextForcedFull { get; init; }

    /// <summary>Klip ayarlari (kirpma, dondurme, ses, solma) yeniden kodlama istedigi icin Tam'a gecildi.</summary>
    public bool EffectsForcedFull { get; init; }

    /// <summary>Goruntu Akilli kipe uyuyordu, ses izi aynen kopyalanamadigi icin Tam'a gecildi.</summary>
    public bool AudioForcedFull { get; init; }

    /// <summary>Cizelge birden cok kaynaktan parca tasidigi icin kopyalama yolu birakilip Tam'a gecildi.</summary>
    public bool MergeForcedFull { get; init; }

    /// <summary>Teslim ayarlari (cozunurluk, kare hizi, kodek, kalite, yalniz ses) varsayilandan farkli oldugu icin Tam'a gecildi.</summary>
    public bool SettingsForcedFull { get; init; }

    /// <summary>Kliplerin arasinda capraz gecis oldugu icin <c>-c copy</c> yolu birakilip Tam'a gecildi.</summary>
    public bool TransitionsForcedFull { get; init; }

    /// <summary>Cizelgede muzik izi oldugu icin <c>-c copy</c> yolu birakilip Tam'a gecildi.</summary>
    public bool MusicForcedFull { get; init; }

    /// <summary>Metin varsa <c>ass=</c> suzgecinin okudugu belge; kosucu calismadan once BOM'lu yazar.</summary>
    public string? SubtitlePath { get; init; }

    public string? SubtitleContent { get; init; }

    /// <summary><c>fontsdir</c>; kosucu kurar ve secilen ailelerin dosyalarini koyar.</summary>
    public string? FontsDirectory { get; init; }

    public IReadOnlyList<string> FontFamilies { get; init; } = Array.Empty<string>();

    public bool FellBackToFull => Requested != ExportMode.Full && Effective == ExportMode.Full;

    public double TotalWorkSeconds => Steps.Sum(s => s.DurationSeconds);
}

public static class EditExport
{
    public const double MeasuredReverseMbPerSecond1080p30 = 99;

    public const double ReverseOverhead = 1.08;

    public const string FullVideoCodec = "libx264";
    public const string FullCrf = "18";
    public const string AudioCodec = "aac";

    /// <summary>Birlestirmede butun parcalarin sesinin getirildigi ortak bicim.</summary>
    public const string MergeAudioFormat = "aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo";

    public const string MergeSilence = "anullsrc=r=48000:cl=stereo";

    /// <summary>Bildirilecek en kucuk kayma, saniye; altindaki fark notta 0 olarak yazilirdi.</summary>
    public const double KeyframeLeadNotice = 0.005;

    /// <summary>
    /// Planlarin kaymalarini cizelge sirasiyla duzler; bildirim esigini gecen kliplerin sifirdan sira
    /// numaralari ve en buyuk kayma.
    /// </summary>
    public static (IReadOnlyList<int> Clips, double Longest) KeyframeLeadSummary(IReadOnlyList<ExportPlan> plans)
    {
        var leads = plans.SelectMany(p => p.KeyframeLeads).ToArray();
        var clips = Enumerable.Range(0, leads.Length).Where(i => leads[i] >= KeyframeLeadNotice).ToArray();
        return (clips, clips.Length == 0 ? 0 : clips.Max(i => leads[i]));
    }

    /// <summary>
    /// Akilli kesimde yalniz aynen kopyalanan ses kodekleri ve onlari tasiyan cikti kaplari. Opus
    /// <c>docs/olcumler/akilli-kesme-opus.md</c>; AC-3, E-AC-3 ve MP3 <c>docs/olcumler/akilli-kesme-ses-kodekleri.md</c>
    /// (uc kesimde ses-goruntu kaymasi bir kareden kucuk, kap listesi muxer'in kabul ettigi kadar). Bunlarin
    /// kodlanan parcasi AAC olurdu ve kopyalanan parcayla ayni akista birlesmez; hiz ya da ters klip varsa Tam'a dusulur.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CopyOnlyAudio =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["opus"] = new[] { ".mkv", ".mp4" },
            ["ac3"] = new[] { ".mkv", ".mp4", ".mov", ".m4v" },
            ["eac3"] = new[] { ".mkv", ".mp4", ".mov" },
            ["mp3"] = new[] { ".mkv", ".mp4", ".mov" }
        };

    public const string SubtitleFileName = "text.ass";

    public const string VideoListName = "list.ffconcat";
    public const string AudioListName = "audio.ffconcat";
    public const string SubtitleListName = "subtitle.ffconcat";

    private const double SeekNudgeSeconds = 0.001;

    /// <summary>
    /// Akilli kesimde bas kenarin kodlamasi anahtar kareden yarim kare once biter. Goruntu akisi
    /// kabin baslangicindan gec baslayan TS kaynakta (bench kaynaginda 23,2 ms) <c>-t</c> o kadar gec kesiyor,
    /// anahtar kareyi de kapsiyor ve parca bir kare uzuyordu. TS disinda girdi <c>-t</c> sureyi ilk tutulan
    /// kareden sayar; kesim kare arasina dusunce pay listedeki tam kare suresinden dusulur, yoksa kenar govdenin
    /// ilk karesini de kodluyor, birlesimde kare yineleniyordu (0,52 sn kesim: 95 yerine 96 kare).
    /// </summary>
    private static double HeadMarginSeconds(MediaInfo info) => 0.5 / (info.Fps > 0 ? info.Fps : 30);

    /// <summary>
    /// Bas kenarin listedeki suresi tam kare sayisidir. Kesim kare arasindan baslayinca kenarin ilk karesi
    /// sifira cekilir; liste kesimden anahtar kareye kadar yazilirsa aradaki bosluk son kareyi uzatir ve
    /// birlesik akista kare hizi 48/1 okunuyordu (TS kaynakta goruntu kabin basindan 23,2 ms gec).
    /// </summary>
    private static double HeadListSeconds(MediaInfo info, double span)
    {
        if (info.Fps <= 0) return span;
        var frames = Math.Floor(span * info.Fps + 1e-3);
        return frames > 0 ? frames / info.Fps : span;
    }

    /// <summary>
    /// Kuyruk kenari anahtar karede baslar ve kesimden onceki her kareyi kodlar; listedeki suresi bu karelerin
    /// sayisidir. Kesim kare arasina dusunce liste kesime kadar yazilirsa sonraki parca son karenin ustune biner
    /// (TS kaynakta birlesimde yinelenen dts, cikti 48/1 okunuyordu).
    /// </summary>
    private static double TailListSeconds(MediaInfo info, double span)
    {
        if (info.Fps <= 0) return span;
        var frames = Math.Ceiling(span * info.Fps - 1e-3);
        return frames > 0 ? frames / info.Fps : span;
    }

    /// <summary>
    /// Hizi degismis ya da ters klip butunuyle kodlanir; <c>fps=</c> suzgeci cizelge suresini en yakin tam kareye
    /// yuvarlar (1 sn kaynak 1,3x hizda 30 kare/sn'de 23 kare). Listedeki suresi bu karelerin sayisidir.
    /// </summary>
    private static double MotionListSeconds(MediaInfo info, double span)
    {
        if (info.Fps <= 0) return span;
        var frames = Math.Round(span * info.Fps, MidpointRounding.AwayFromZero);
        return frames > 0 ? frames / info.Fps : span;
    }

    private static readonly IReadOnlyDictionary<string, string> MatchingEncoders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["h264"] = "libx264",
        ["hevc"] = "libx265"
    };

    public static bool NeedsReencode(EditClip clip) => clip.Speed != 1m || clip.Reversed;

    public static bool SupportsSegments(MediaInfo info)
        => MatchingEncoders.ContainsKey(info.VideoCodec) && AudioCopies(info);

    /// <summary>
    /// Akilli kipin goruntu kapisi: kesim noktalari okunmus olmali ve <see cref="SmartCutCodec.Resolve"/>
    /// kaynaga bir kodlayici esleyebilmeli. Biri tutmazsa <c>null</c>. Ses ayri sorulur
    /// (<see cref="SmartAudioCopies"/>).
    /// </summary>
    public static SmartCutEncoding? SmartEncoding(
        MediaInfo info, IReadOnlyList<SmartCutPoint>? cuts, Func<string, bool>? hasEncoder = null)
        => cuts is { Count: > 0 } ? SmartCutCodec.Resolve(info, hasEncoder) : null;

    /// <summary>
    /// Akilli kipin ses kapisi. Sessiz kaynak ve AAC her zaman gecer. <see cref="CopyOnlyAudio"/> kodekleri
    /// yalniz butun klipler duz kopyalaniyorsa (hiz ya da ters klip yok) ve cikti kabi onu tasiyorsa gecer.
    /// </summary>
    public static bool SmartAudioCopies(MediaInfo info, bool motion, string outputPath)
    {
        if (AudioCopies(info)) return true;
        if (info.AudioCodec is not { } codec || !CopyOnlyAudio.TryGetValue(codec, out var containers)) return false;
        return !motion && containers.Contains(Path.GetExtension(outputPath), StringComparer.OrdinalIgnoreCase);
    }

    private static bool AudioCopies(MediaInfo info)
        => !info.HasAudio || string.Equals(info.AudioCodec, AudioCodec, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<double> AtempoChain(decimal speed)
    {
        if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Hiz pozitif olmalidir");
        var value = (double)speed;
        if (value == 1) return Array.Empty<double>();

        var count = 1;
        while (Math.Pow(2, count) < value || Math.Pow(0.5, count) > value) count++;
        var factor = Math.Pow(value, 1.0 / count);
        return Enumerable.Repeat(factor, count).ToArray();
    }

    public static string AtempoFilter(decimal speed)
        => string.Join(",", AtempoChain(speed).Select(f => "atempo=" + Number(f)));

    public static double ReverseBytesPerSecond(MediaInfo info)
    {
        var bytesPerPixel = info.BitDepth > 8 ? 3.0 : 1.5;
        return info.Width * (double)info.Height * bytesPerPixel * Math.Max(1, info.Fps) * ReverseOverhead;
    }

    public static double ReverseLimitSeconds(MediaInfo info, long memoryBudgetBytes)
    {
        var rate = ReverseBytesPerSecond(info);
        return rate <= 0 ? double.PositiveInfinity : memoryBudgetBytes / rate;
    }

    public static IReadOnlyList<int> ReverseOverLimit(EditTimeline timeline, MediaInfo info, long memoryBudgetBytes)
    {
        var limit = ReverseLimitSeconds(info, memoryBudgetBytes);
        var over = new List<int>();
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            if (clip.Reversed && EditTime.ToSeconds(clip.SourceLength) > limit) over.Add(i);
        }

        return over;
    }

    public static (IReadOnlyList<double> Keyframes, double StartTime) ParseKeyframes(string csv)
    {
        var keyframes = new List<double>();
        double startTime = 0;
        foreach (var raw in csv.Split('\n'))
        {
            var fields = raw.Trim().Split(',');
            if (fields.Length == 1)
            {
                if (double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start) && double.IsFinite(start))
                    startTime = start;
                continue;
            }

            if (fields.Length < 2 || !fields[1].StartsWith('K')) continue;
            if (double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts) && double.IsFinite(pts))
                keyframes.Add(pts);
        }

        var relative = keyframes.Select(k => Math.Max(0, k - startTime)).Distinct().OrderBy(k => k).ToArray();
        return (relative, startTime);
    }

    public static IReadOnlyList<string> KeyframeProbeArgs(string source)
        => new[]
        {
            "-v", "error", "-select_streams", "v:0", "-show_packets",
            "-show_entries", "packet=pts_time,flags:format=start_time", "-of", "csv=p=0", source
        };

    public static ExportPlan Build(
        EditTimeline timeline, MediaInfo info, IReadOnlyList<double> keyframes, double startTime,
        ExportMode mode, string outputPath, string workDirectory, long memoryBudgetBytes,
        IReadOnlyList<SmartCutPoint>? cuts = null, Func<string, bool>? hasEncoder = null, bool dropMetadata = false,
        EditExportSettings? settings = null, int audioTrack = EditAudioTrack.Default)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));
        settings ??= EditExportSettings.Default;
        var whole = info;
        var tracks = EditAudioTrack.Tracks(info, audioTrack);
        var languages = tracks.Select(t => EditAudioTrack.Streams(whole).ElementAtOrDefault(t)?.Language).ToArray();
        info = EditAudioTrack.Select(info, audioTrack);
        bool EachTrack(Func<MediaInfo, bool> test) => test(info) && tracks.All(t => test(EditAudioTrack.Select(whole, t)));

        var effective = mode;
        var anySpecial = timeline.Clips.Any(NeedsReencode);
        var smart = mode == ExportMode.Smart ? SmartEncoding(info, cuts, hasEncoder) : null;
        var audioForced = smart is not null && keyframes.Count > 0 && !EachTrack(t => SmartAudioCopies(t, anySpecial, outputPath));
        if (audioForced) smart = null;
        if (mode == ExportMode.Smart && smart is null) effective = ExportMode.Full;
        if (mode == ExportMode.Fast && anySpecial && !EachTrack(SupportsSegments)) effective = ExportMode.Full;
        if (mode != ExportMode.Full && keyframes.Count == 0) effective = ExportMode.Full;
        var textForced = mode != ExportMode.Full && effective != ExportMode.Full && timeline.HasText;
        var effectsForced = mode != ExportMode.Full && effective != ExportMode.Full && timeline.HasEffects;
        var settingsForced = mode != ExportMode.Full && effective != ExportMode.Full && !timeline.HasText && !timeline.HasEffects && !settings.IsDefault;
        var transitionsForced = mode != ExportMode.Full && effective != ExportMode.Full && !timeline.HasText && !timeline.HasEffects && settings.IsDefault
            && timeline.HasTransitions;
        var musicForced = mode != ExportMode.Full && effective != ExportMode.Full && !timeline.HasText && !timeline.HasEffects && settings.IsDefault
            && !timeline.HasTransitions && timeline.HasMusic;
        if (timeline.HasText || timeline.HasEffects || !settings.IsDefault || timeline.HasTransitions || timeline.HasMusic) effective = ExportMode.Full;

        var limit = ReverseLimitSeconds(info, memoryBudgetBytes);
        var over = ReverseOverLimit(timeline, info, memoryBudgetBytes);

        var motion = Enumerable.Range(0, timeline.Clips.Count).Where(i => NeedsReencode(timeline.Clips[i])).ToArray();
        var plan = effective switch
        {
            ExportMode.Full when settings.AudioOnly => AudioOnly(timeline, info, mode, outputPath, workDirectory, limit, over, settings,
                tracks.Count > 0 ? tracks[0] : EditAudioTrack.First),
            ExportMode.Full => Full(timeline, info, mode, outputPath, workDirectory, limit, over, settings, tracks),
            ExportMode.Smart => SmartSegmented(timeline, info, smart!, cuts!, keyframes, outputPath, workDirectory, limit, over, tracks),
            ExportMode.Fast when !anySpecial => FastDirect(timeline, info, keyframes, startTime, outputPath, workDirectory, limit, over, tracks),
            _ => Segmented(timeline, info, keyframes, outputPath, workDirectory, limit, over, tracks)
        };
        var subtitles = audioTrack == EditAudioTrack.All && motion.Length == 0 && !timeline.HasTransitions && !(effective == ExportMode.Full && settings.AudioOnly)
            ? CarriedSubtitles(whole, outputPath)
            : Array.Empty<(int Position, SourceStream Stream)>();
        if (subtitles.Count > 0) plan = WithSubtitles(plan, timeline, whole, subtitles, outputPath, workDirectory);
        if (timeline.Music is { } music) plan = WithMusic(plan, music, settings, outputPath);
        if (settings.AudioOnly && effective == ExportMode.Full) languages = languages.Take(1).ToArray();
        if (dropMetadata)
            plan = plan with { Steps = WithoutMetadata(plan.Steps, info, languages, subtitles.Select(s => s.Stream.Language).ToArray()) };
        return plan with
        {
            MotionClips = motion,
            TextForcedFull = textForced,
            EffectsForcedFull = effectsForced,
            AudioForcedFull = audioForced,
            SettingsForcedFull = settingsForced,
            TransitionsForcedFull = transitionsForced,
            MusicForcedFull = musicForced
        };
    }

    /// <summary>
    /// Muzik izini son adima ekler: dosya son girdinin ardina <c>-i</c> olarak girer, grafige
    /// <c>volume</c> + ortak bicim + <c>adelay</c> + <c>apad</c> + <c>atrim</c> zinciri baglanir. Kaynak sesi varsa ilk ses cikisi
    /// (<c>[aout]</c>) muzikle <c>amix</c> (<c>duration=first</c>, <c>normalize=0</c>; iki duzey oldugu gibi toplanir) ile karisip
    /// <c>[amix]</c> olarak eslenir; yoksa muzik tek basina ses izi olur. Ikinci ve sonraki ses izlerine dokunulmaz.
    /// </summary>
    private static ExportPlan WithMusic(ExportPlan plan, EditMusic music, EditExportSettings settings, string output)
    {
        var last = plan.Steps[^1];
        var args = last.Args.ToList();
        var index = args.Count(a => a == "-i");
        var input = args.LastIndexOf("-i") + 2;
        args.InsertRange(input, new[] { "-i", music.Path });
        var graphAt = args.IndexOf("-filter_complex") + 1;
        if (graphAt <= 0) throw new InvalidOperationException("Muzik yalniz suzgec grafigi kuran adima eklenir");
        var duration = Number(last.DurationSeconds);
        var chain = new StringBuilder(";[").Append(index.ToString(CultureInfo.InvariantCulture)).Append(":a:0]volume=")
            .Append(music.LevelDb.ToString("0.0", CultureInfo.InvariantCulture)).Append("dB,").Append(MergeAudioFormat)
            .Append(",adelay=").Append(Math.Round(EditTime.ToSeconds(music.Start) * 1000).ToString(CultureInfo.InvariantCulture)).Append(":all=1")
            .Append(",apad,atrim=duration=").Append(duration).Append(",asetpts=PTS-STARTPTS[mus]");
        var mapped = MapIndex(args, "[aout]");
        if (mapped >= 0)
        {
            chain.Append(";[aout]").Append(MergeAudioFormat).Append("[asrc];[asrc][mus]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[amix]");
            args[mapped] = "[amix]";
        }
        else
        {
            var video = MapIndex(args, "[vout]");
            args.InsertRange(video >= 0 ? video + 1 : graphAt + 1, new[] { "-map", "[mus]" });
            args.InsertRange(args.Count - 1, new[] { "-c:a", settings.AudioOnly ? EditExportSettings.AudioOnlyCodec(output) : AudioCodec, "-b:a", settings.AudioBitrate });
        }

        args[graphAt] += chain.ToString();
        return plan with { Steps = plan.Steps.Take(plan.Steps.Count - 1).Append(last with { Args = args }).ToArray() };
    }

    private static int MapIndex(IReadOnlyList<string> args, string label)
    {
        for (var i = 1; i < args.Count; i++)
            if (args[i] == label && args[i - 1] == "-map") return i;
        return -1;
    }

    /// <summary>Cizelgedeki parcalarin geldigi kaynaklarin siralari, kucukten buyuge.</summary>
    public static IReadOnlyList<int> UsedSources(EditTimeline timeline)
        => timeline.Clips.Select(c => c.Source).Distinct().OrderBy(s => s).ToArray();

    /// <summary>
    /// Cok kaynakli proje icin plan. Parcalarin hepsi tek kaynaktansa o kaynagin bilgisiyle tek kaynakli
    /// yol aynen kosar. Birden cok kaynak varsa kip ne olursa olsun tek adimda yeniden kodlanir: her parca
    /// ilk kaynagin gorunen boyutuna sigdirilir, onun kare hizina ve ortak ses bicimine getirilir.
    /// </summary>
    public static ExportPlan Build(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, ExportMode mode,
        string outputPath, string workDirectory, long memoryBudgetBytes,
        Func<string, bool>? hasEncoder = null, bool dropMetadata = false, EditExportSettings? settings = null,
        int audioTrack = EditAudioTrack.Default, bool clipChapters = false, string chapterTitle = EditChapters.DefaultTitle)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));
        var used = UsedSources(timeline);
        ExportPlan plan;
        if (used.Count > 1)
        {
            var merged = Merge(timeline, WithTrack(sources, audioTrack, out var track), used, mode, outputPath, workDirectory, memoryBudgetBytes, dropMetadata,
                settings ?? EditExportSettings.Default, track);
            plan = timeline.Music is { } music ? WithMusic(merged, music, settings ?? EditExportSettings.Default, outputPath) : merged;
        }
        else
        {
            var only = SourceOf(sources, used[0]);
            plan = Build(timeline, only.Info, only.Keyframes, only.StartTime, mode, outputPath, workDirectory, memoryBudgetBytes,
                only.Cuts, hasEncoder, dropMetadata, settings, used[0] == 0 ? audioTrack : EditAudioTrack.Default);
        }

        return WithChapters(plan, timeline, sources, clipChapters, chapterTitle);
    }

    /// <summary>
    /// Cok kaynakli projede parcalari ayri yazan planlar. Her parca tek kaynaklidir; kendi kaynaginin
    /// bilgisiyle ve istenen kiple planlanir.
    /// </summary>
    public static IReadOnlyList<ExportPlan> BuildSegments(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, ExportMode mode,
        IReadOnlyList<string> outputPaths, IReadOnlyList<string> workDirectories, long memoryBudgetBytes,
        Func<string, bool>? hasEncoder = null, bool dropMetadata = false, EditExportSettings? settings = null,
        int audioTrack = EditAudioTrack.Default, bool clipChapters = false, string chapterTitle = EditChapters.DefaultTitle)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));
        var used = UsedSources(timeline);
        if (used.Count == 1)
        {
            var only = SourceOf(sources, used[0]);
            var single = BuildSegments(timeline, only.Info, only.Keyframes, only.StartTime, mode, outputPaths, workDirectories, memoryBudgetBytes,
                only.Cuts, hasEncoder, dropMetadata, settings, used[0] == 0 ? audioTrack : EditAudioTrack.Default);
            return single.Select((plan, i) => WithChapters(plan, new EditTimeline(new[] { timeline.Clips[i] }), sources, clipChapters, chapterTitle)).ToArray();
        }

        if (outputPaths.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir cikti yolu gerekir", nameof(outputPaths));
        if (workDirectories.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir is klasoru gerekir", nameof(workDirectories));

        var plans = new List<ExportPlan>(timeline.Clips.Count);
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var index = i;
            var source = SourceOf(sources, timeline.Clips[i].Source);
            var piece = new EditTimeline(new[] { timeline.Clips[i] }, texts: SegmentTexts(timeline, i));
            var plan = Build(piece, source.Info, source.Keyframes, source.StartTime, mode, outputPaths[i], workDirectories[i], memoryBudgetBytes,
                source.Cuts, hasEncoder, dropMetadata, settings, timeline.Clips[i].Source == 0 ? audioTrack : EditAudioTrack.Default);
            plans.Add(WithChapters(plan with
            {
                MotionClips = plan.MotionClips.Select(_ => index).ToArray(),
                ReverseOverLimit = plan.ReverseOverLimit.Select(_ => index).ToArray()
            }, new EditTimeline(new[] { timeline.Clips[i] }), sources, clipChapters, chapterTitle));
        }

        return plans;
    }

    /// <summary>
    /// Teslimin bolumleri: <paramref name="clipChapters"/> acik ve iki ya da daha cok parca varsa parca
    /// sinirlari (<see cref="EditChapters.FromClips"/>), yoksa kaynak bolumlerinin kesimle kaymis hali
    /// (<see cref="EditChapters.Shifted"/>). Hizli kipte planin anahtar kare kaymasi hesaba girer.
    /// </summary>
    public static IReadOnlyList<RecorderChapter> ChaptersFor(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, bool clipChapters, string chapterTitle,
        IReadOnlyList<double>? leads = null)
    {
        if (clipChapters && timeline.Clips.Count > 1) return EditChapters.FromClips(timeline, chapterTitle, leads);
        var chapters = new Dictionary<int, IReadOnlyList<ChapterMark>>();
        foreach (var index in UsedSources(timeline))
            if (sources.TryGetValue(index, out var source) && source.Info.Chapters.Count > 0)
                chapters[index] = EditChapters.Relative(source.Info.Chapters, source.StartTime);
        return EditChapters.Shifted(timeline, chapters, chapterTitle, leads);
    }

    /// <summary>
    /// Bolum listesini teslimin son adimina ekler: liste <see cref="RecorderChapters.Metadata"/> ile
    /// is klasorundeki <c>chapters.ffmeta</c>'ya yazilir (adimin <see cref="ExportStep.ExtraLists"/>'i),
    /// son girdinin ardina <c>-f ffmetadata -i</c> olarak girer ve <c>-map_chapters</c> ile eslenir; Kaydedici'nin
    /// yeniden paketlemesiyle ayni yol (<see cref="RecorderChapters.BuildRemux"/>). Liste bossa ama kaynakta bolum
    /// varsa <c>-map_chapters -1</c> yazilir: ffmpeg kendiliginden ilk girdinin bolumlerini kaymamis kopyalardi.
    /// Bolum tasimayan kapta (<see cref="RecorderChapters.Carries"/>) liste yazilmaz, kaynak bolumu yine dusurulur.
    /// Bolumsuz kaynakta arguman degismez. Ara adimlara dokunulmaz.
    /// </summary>
    private static ExportPlan WithChapters(
        ExportPlan plan, EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, bool clipChapters, string chapterTitle)
    {
        var hadChapters = UsedSources(timeline).Any(i => sources.TryGetValue(i, out var s) && s.Info.Chapters.Count > 0);
        var carries = RecorderChapters.Carries(RecorderArguments.ContainerOf(plan.OutputPath));
        var chapters = carries
            ? ChaptersFor(timeline, sources, clipChapters, chapterTitle, plan.KeyframeLeads)
            : Array.Empty<RecorderChapter>();
        if (chapters.Count == 0 && !hadChapters) return plan;

        var last = plan.Steps[^1];
        var args = last.Args.ToList();
        var lists = last.ExtraLists;
        string map;
        if (chapters.Count > 0)
        {
            var path = Path.Combine(plan.WorkDirectory, "chapters.ffmeta");
            map = args.Count(a => a == "-i").ToString(CultureInfo.InvariantCulture);
            args.InsertRange(args.LastIndexOf("-i") + 2, new[] { "-f", "ffmetadata", "-i", path });
            lists = lists.Append(new ExportList(path, RecorderChapters.Metadata(chapters))).ToArray();
        }
        else
        {
            map = "-1";
        }

        args.InsertRange(args.Count - 1, new[] { "-map_chapters", map });
        return plan with { Steps = plan.Steps.Take(plan.Steps.Count - 1).Append(last with { Args = args, ExtraLists = lists }).ToArray() };
    }

    private static ExportSource SourceOf(IReadOnlyDictionary<int, ExportSource> sources, int index)
        => sources.TryGetValue(index, out var source)
            ? source
            : throw new ArgumentException("Parcanin kaynagi okunmamis: " + index.ToString(CultureInfo.InvariantCulture), nameof(sources));

    private static IReadOnlyDictionary<int, ExportSource> WithTrack(IReadOnlyDictionary<int, ExportSource> sources, int audioTrack, out int track)
    {
        track = EditAudioTrack.First;
        if (audioTrack == EditAudioTrack.All) audioTrack = EditAudioTrack.First;
        if (!sources.TryGetValue(0, out var first)) return sources;
        track = EditAudioTrack.Resolve(first.Info, audioTrack);
        var copy = sources.ToDictionary(pair => pair.Key, pair => pair.Value);
        copy[0] = first with { Info = EditAudioTrack.Select(first.Info, audioTrack) };
        return copy;
    }

    private static string AudioInput(string input, int track)
        => track > 0 ? "[" + input + ":a:" + track.ToString(CultureInfo.InvariantCulture) + "]" : "[" + input + ":a]";

    private static string AudioStream(int track) => "0:a:" + Math.Max(track, 0).ToString(CultureInfo.InvariantCulture);

    private static string AudioOutput(int position) => position == 0 ? "[aout]" : "[aout" + position.ToString(CultureInfo.InvariantCulture) + "]";

    private static IEnumerable<string> AudioMaps(MediaInfo info, IReadOnlyList<int> tracks)
        => info.HasAudio ? tracks.SelectMany(t => new[] { "-map", AudioStream(t) }) : Array.Empty<string>();

    /// <summary>
    /// Cikti kabinin tasiyabildigi altyazi izleri, kaynaktaki altyazi sirasiyla (<c>0:s:N</c>): MKV metin ve goruntu
    /// altyazisini, MP4 ailesi yalniz metni (<c>mov_text</c>'e cevrilerek) tasir; WebM ve digerleri hicbirini.
    /// </summary>
    private static IReadOnlyList<(int Position, SourceStream Stream)> CarriedSubtitles(MediaInfo info, string output)
    {
        var mkv = string.Equals(Path.GetExtension(output), ".mkv", StringComparison.OrdinalIgnoreCase);
        var mp4 = IsMp4Family(output);
        return info.Streams.Where(s => s.Kind == StreamKind.Subtitle)
            .Select((stream, position) => (Position: position, Stream: stream))
            .Where(s => StreamMapping.IsTextSubtitle(s.Stream.Codec) ? mkv || mp4 : mkv && StreamMapping.IsImageSubtitle(s.Stream.Codec))
            .ToArray();
    }

    /// <summary>
    /// Altyazi izlerini teslime ekler. Her klip icin altyazilar ayri bir MKV parcasina kesilir; parcanin basi
    /// teslimdeki klip basidir (Hizli kipte anahtar kareye cekilen bas), suresi teslimdeki klip suresidir. Parcalar
    /// sureli bir concat listesiyle son adima ek girdi olarak verilir. <c>-copypriorss 0</c> kesimden once baslayan
    /// olayi birakir. <c>mov_text</c> parcada SRT'ye cevrilir; MP4 ailesine <c>mov_text</c> olarak yazilir.
    /// </summary>
    private static ExportPlan WithSubtitles(
        ExportPlan plan, EditTimeline timeline, MediaInfo info, IReadOnlyList<(int Position, SourceStream Stream)> subtitles,
        string output, string work)
    {
        var steps = new List<ExportStep>();
        var list = new StringBuilder("ffconcat version 1.0\n");
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var lead = i < plan.KeyframeLeads.Count ? plan.KeyframeLeads[i] : 0;
            var start = Math.Max(0, EditTime.ToSeconds(clip.SourceStart) - lead);
            var length = EditTime.ToSeconds(clip.SourceEnd) - start;
            var part = Path.Combine(work, "s" + i.ToString("D4", CultureInfo.InvariantCulture) + ".mkv");
            var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
            if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
            args.AddRange(new[] { "-t", Number(length), "-i", info.FilePath });
            foreach (var (position, _) in subtitles) args.AddRange(new[] { "-map", "0:s:" + position.ToString(CultureInfo.InvariantCulture) });
            args.AddRange(new[] { "-c:s", "copy" });
            for (var k = 0; k < subtitles.Count; k++)
                if (string.Equals(subtitles[k].Stream.Codec, "mov_text", StringComparison.OrdinalIgnoreCase))
                    args.AddRange(new[] { "-c:s:" + k.ToString(CultureInfo.InvariantCulture), "srt" });
            args.AddRange(new[] { "-copypriorss", "0", "-f", "matroska", part });
            steps.Add(new ExportStep(args, 0));
            list.Append("file ").Append(Quote(part)).Append("\nduration ").Append(Number(length)).Append('\n');
        }

        var listPath = Path.Combine(work, SubtitleListName);
        var last = plan.Steps[^1];
        var input = last.Args.ToList().LastIndexOf("-i") + 2;
        var index = last.Args.Count(a => a == "-i");
        var joined = last.Args.Take(input)
            .Concat(new[] { "-f", "concat", "-safe", "0", "-i", listPath })
            .Concat(last.Args.Skip(input).Take(last.Args.Count - input - 1))
            .Concat(new[] { "-map", index.ToString(CultureInfo.InvariantCulture) + ":s", "-c:s", IsMp4Family(output) ? "mov_text" : "copy" })
            .Append(last.Args[^1])
            .ToArray();
        steps.InsertRange(0, plan.Steps.Take(plan.Steps.Count - 1));
        steps.Add(last with { Args = joined, ExtraLists = last.ExtraLists.Append(new ExportList(listPath, list.ToString())).ToArray() });
        return plan with { Steps = steps };
    }

    private static ExportPlan Merge(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, IReadOnlyList<int> used, ExportMode requested,
        string output, string work, long memoryBudgetBytes, bool dropMetadata, EditExportSettings settings, int track)
    {
        var target = SourceOf(sources, sources.ContainsKey(0) ? 0 : used[0]).Info;
        var canvas = settings.HasSize
            ? (Width: settings.Width, Height: settings.Height)
            : (Width: Math.Max(2, ClipFilters.DisplayWidth(target.Width, target.ParNum, target.ParDen) / 2 * 2),
                Height: Math.Max(2, target.Height / 2 * 2));
        var fps = Number(settings.Fps > 0 ? settings.Fps : target.Fps > 0 ? target.Fps : 30);
        var audio = used.Any(s => SourceOf(sources, s).Info.HasAudio);
        if (settings.AudioOnly) return MergeAudioOnly(timeline, sources, used, requested, output, work, memoryBudgetBytes, dropMetadata, settings, audio, track);
        var inputs = new Dictionary<int, int>();
        foreach (var s in used) inputs[s] = inputs.Count;

        var graph = new StringBuilder();
        var joins = new StringBuilder();
        var over = new List<int>();
        var limit = used.Min(s => ReverseLimitSeconds(SourceOf(sources, s).Info, memoryBudgetBytes));
        var transitions = timeline.HasTransitions;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var info = SourceOf(sources, clip.Source).Info;
            var input = inputs[clip.Source].ToString(CultureInfo.InvariantCulture);
            var start = Number(EditTime.ToSeconds(clip.SourceStart));
            var end = Number(EditTime.ToSeconds(clip.SourceEnd));
            if (clip.Reversed && EditTime.ToSeconds(clip.SourceLength) > ReverseLimitSeconds(info, memoryBudgetBytes)) over.Add(i);

            graph.Append('[').Append(input).Append(":v]trim=start=").Append(start).Append(":end=").Append(end).Append(",setpts=PTS-STARTPTS")
                .Append(VideoMotion(clip, info))
                .Append(Link(ClipFilters.Geometry(clip.Effects, info.Width, info.Height, info.ParNum, info.ParDen, canvas)))
                .Append(Link(ClipFilters.Color(clip.Effects)))
                .Append(",fps=").Append(fps).Append(",format=yuv420p")
                .Append(Link(ClipFilters.VideoFades(clip.Effects, clip.TimelineLength)))
                .Append(transitions ? ",settb=AVTB" : string.Empty)
                .Append("[v").Append(i).Append("];");
            joins.Append("[v").Append(i).Append(']');
            if (!audio) continue;

            if (info.HasAudio)
                graph.Append(AudioInput(input, clip.Source == 0 ? track : EditAudioTrack.First))
                    .Append("atrim=start=").Append(start).Append(":end=").Append(end).Append(",asetpts=PTS-STARTPTS")
                    .Append(AudioMotion(clip)).Append(Link(ClipFilters.Audio(clip.Effects, clip.TimelineLength)))
                    .Append(',').Append(MergeAudioFormat);
            else
                graph.Append(MergeSilence).Append(",atrim=duration=").Append(Number(EditTime.ToSeconds(clip.TimelineLength)));
            graph.Append("[a").Append(i).Append("];");
            joins.Append("[a").Append(i).Append(']');
        }

        if (transitions)
            graph.Append(TransitionChain(timeline, audio ? 1 : 0, timeline.HasText ? "[vcat]" : "[vout]"));
        else
            graph.Append(joins).Append("concat=n=").Append(timeline.Clips.Count).Append(":v=1:a=").Append(audio ? 1 : 0)
                .Append(timeline.HasText ? "[vcat]" : "[vout]").Append(audio ? "[aout]" : string.Empty);

        string? subtitle = null;
        string? fonts = null;
        if (timeline.HasText)
        {
            subtitle = Path.Combine(work, SubtitleFileName);
            fonts = Path.Combine(work, TextFonts.FolderName);
            graph.Append(";[vcat]").Append(AssFilter(subtitle, fonts)).Append("[vout]");
        }

        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        foreach (var s in used) args.AddRange(new[] { "-i", SourceOf(sources, s).Info.FilePath });
        args.AddRange(new[] { "-filter_complex", graph.ToString(), "-map", "[vout]" });
        if (audio) args.AddRange(new[] { "-map", "[aout]" });
        args.AddRange(VideoEncode(settings));
        if (audio) args.AddRange(new[] { "-c:a", AudioCodec, "-b:a", settings.AudioBitrate });
        args.AddRange(Faststart(output));
        if (dropMetadata) args.AddRange(StreamMapping.MetadataArguments(true, Array.Empty<string?>(), Array.Empty<string?>()));
        args.Add(output);

        return new ExportPlan(requested, ExportMode.Full, new[] { new ExportStep(args, EditTime.ToSeconds(timeline.Duration - timeline.TransitionOverlap)) },
            over, limit, output, work)
        {
            MotionClips = Enumerable.Range(0, timeline.Clips.Count).Where(i => NeedsReencode(timeline.Clips[i])).ToArray(),
            MergeForcedFull = requested != ExportMode.Full,
            SubtitlePath = subtitle,
            SubtitleContent = subtitle is null ? null : AssWriter.Write(timeline.Texts, canvas.Width, canvas.Height),
            FontsDirectory = fonts,
            FontFamilies = timeline.Texts.Select(t => t.FontName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static ExportPlan MergeAudioOnly(
        EditTimeline timeline, IReadOnlyDictionary<int, ExportSource> sources, IReadOnlyList<int> used, ExportMode requested,
        string output, string work, long memoryBudgetBytes, bool dropMetadata, EditExportSettings settings, bool audio, int track)
    {
        if (!audio) throw new EditExportNoAudioException();
        var inputs = new Dictionary<int, int>();
        foreach (var s in used) inputs[s] = inputs.Count;

        var graph = new StringBuilder();
        var joins = new StringBuilder();
        var over = new List<int>();
        var limit = used.Min(s => ReverseLimitSeconds(SourceOf(sources, s).Info, memoryBudgetBytes));
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var info = SourceOf(sources, clip.Source).Info;
            if (clip.Reversed && EditTime.ToSeconds(clip.SourceLength) > ReverseLimitSeconds(info, memoryBudgetBytes)) over.Add(i);
            if (info.HasAudio)
                graph.Append(AudioInput(inputs[clip.Source].ToString(CultureInfo.InvariantCulture), clip.Source == 0 ? track : EditAudioTrack.First))
                    .Append(AudioTrim(clip)).Append(',').Append(MergeAudioFormat);
            else
                graph.Append(MergeSilence).Append(",atrim=duration=").Append(Number(EditTime.ToSeconds(clip.TimelineLength)));
            graph.Append("[a").Append(i).Append("];");
            joins.Append("[a").Append(i).Append(']');
        }

        graph.Append(joins).Append("concat=n=").Append(timeline.Clips.Count).Append(":v=0:a=1[aout]");
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        foreach (var s in used) args.AddRange(new[] { "-i", SourceOf(sources, s).Info.FilePath });
        args.AddRange(AudioOnlyTail(graph.ToString(), output, settings));
        if (dropMetadata) args.AddRange(StreamMapping.MetadataArguments(true, Array.Empty<string?>(), Array.Empty<string?>()));
        args.Add(output);

        return new ExportPlan(requested, ExportMode.Full, new[] { new ExportStep(args, EditTime.ToSeconds(timeline.Duration)) },
            over, limit, output, work)
        {
            MotionClips = Enumerable.Range(0, timeline.Clips.Count).Where(i => NeedsReencode(timeline.Clips[i])).ToArray(),
            MergeForcedFull = requested != ExportMode.Full
        };
    }

    /// <summary>
    /// Yalniz ses teslimi: grafikte yalniz ses zincirleri, <c>concat=...:v=0:a=1</c>, <c>-vn</c>. Goruntu zinciri kurulmaz,
    /// etiketli ama eslenmeyen bir cikis ffmpeg'de hata olurdu. Metin katmani ve goruntu ayarlari yok sayilir.
    /// </summary>
    private static ExportPlan AudioOnly(
        EditTimeline timeline, MediaInfo info, ExportMode requested, string output, string work,
        double limit, IReadOnlyList<int> over, EditExportSettings settings, int track)
    {
        if (!info.HasAudio) throw new EditExportNoAudioException();
        var graph = new StringBuilder();
        var joins = new StringBuilder();
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            graph.Append(AudioInput("0", track)).Append(AudioTrim(timeline.Clips[i])).Append("[a").Append(i).Append("];");
            joins.Append("[a").Append(i).Append(']');
        }

        graph.Append(joins).Append("concat=n=").Append(timeline.Clips.Count).Append(":v=0:a=1[aout]");
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", info.FilePath };
        args.AddRange(AudioOnlyTail(graph.ToString(), output, settings));
        args.Add(output);
        return new ExportPlan(requested, ExportMode.Full, new[] { new ExportStep(args, EditTime.ToSeconds(timeline.Duration)) },
            over, limit, output, work);
    }

    private static string AudioTrim(EditClip clip)
        => "atrim=start=" + Number(EditTime.ToSeconds(clip.SourceStart)) + ":end=" + Number(EditTime.ToSeconds(clip.SourceEnd))
            + ",asetpts=PTS-STARTPTS" + AudioMotion(clip) + Link(ClipFilters.Audio(clip.Effects, clip.TimelineLength));

    private static IEnumerable<string> AudioOnlyTail(string graph, string output, EditExportSettings settings)
    {
        var args = new List<string>
        {
            "-filter_complex", graph, "-map", "[aout]", "-vn",
            "-c:a", EditExportSettings.AudioOnlyCodec(output), "-b:a", settings.AudioBitrate
        };
        if (IsMp4Family(output) || string.Equals(Path.GetExtension(output), ".m4a", StringComparison.OrdinalIgnoreCase))
            args.AddRange(new[] { "-movflags", "+faststart" });
        return args;
    }

    private static IEnumerable<string> VideoEncode(EditExportSettings settings)
        => new[]
        {
            "-c:v", settings.VideoCodec, "-preset", settings.Preset, "-crf", settings.Crf.ToString(CultureInfo.InvariantCulture),
            "-pix_fmt", "yuv420p"
        };

    /// <summary>
    /// Teslim dosyasini yazan son adima meta silme argumanlarini ekler; ara parcalara dokunmaz. Dizi
    /// kucultmeyle ayni kaynaktan (<see cref="StreamMapping.MetadataArguments"/>). Diller teslime giden izlerden
    /// (<see cref="EditAudioTrack"/>) cikti sirasiyla yazilir. Bolum adlarini da ffmpeg duser; bolum
    /// isaretleri (<see cref="WithChapters"/>) kalir.
    /// </summary>
    private static IReadOnlyList<ExportStep> WithoutMetadata(
        IReadOnlyList<ExportStep> steps, MediaInfo info, IReadOnlyList<string?> languages, IReadOnlyList<string?> subtitleLanguages)
    {
        var last = steps[^1];
        var audio = info.HasAudio ? languages : Array.Empty<string?>();
        var args = last.Args.Take(last.Args.Count - 1)
            .Concat(StreamMapping.MetadataArguments(true, audio, subtitleLanguages))
            .Append(last.Args[^1])
            .ToArray();
        return steps.Take(steps.Count - 1).Append(last with { Args = args }).ToArray();
    }

    /// <summary>
    /// Her parcayi ayri dosyaya yazan planlar; parca basina bir plan, cizelge sirasinda. Kol butun
    /// cizelge icin secilen koldur (<see cref="Build"/>), yani tek dosyada ne kosacaksa parcada da o
    /// kosar. Metin katmani parcanin araligina kirpilip parcanin basina gore kaydirilir. Plandaki
    /// parca numaralari (<see cref="ExportPlan.MotionClips"/>, <see cref="ExportPlan.ReverseOverLimit"/>)
    /// cizelgedeki siradir.
    /// </summary>
    public static IReadOnlyList<ExportPlan> BuildSegments(
        EditTimeline timeline, MediaInfo info, IReadOnlyList<double> keyframes, double startTime,
        ExportMode mode, IReadOnlyList<string> outputPaths, IReadOnlyList<string> workDirectories, long memoryBudgetBytes,
        IReadOnlyList<SmartCutPoint>? cuts = null, Func<string, bool>? hasEncoder = null, bool dropMetadata = false,
        EditExportSettings? settings = null, int audioTrack = EditAudioTrack.Default)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));
        if (outputPaths.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir cikti yolu gerekir", nameof(outputPaths));
        if (workDirectories.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir is klasoru gerekir", nameof(workDirectories));

        var whole = Build(timeline, info, keyframes, startTime, mode, outputPaths[0], workDirectories[0], memoryBudgetBytes, cuts, hasEncoder,
            settings: settings, audioTrack: audioTrack);
        var plans = new List<ExportPlan>(timeline.Clips.Count);
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var index = i;
            var piece = new EditTimeline(new[] { timeline.Clips[i] }, texts: SegmentTexts(timeline, i));
            var plan = Build(piece, info, keyframes, startTime, whole.Effective, outputPaths[i], workDirectories[i], memoryBudgetBytes, cuts, hasEncoder, dropMetadata, settings,
                audioTrack);
            plans.Add(plan with
            {
                Requested = mode,
                TextForcedFull = whole.TextForcedFull,
                EffectsForcedFull = whole.EffectsForcedFull,
                AudioForcedFull = whole.AudioForcedFull,
                SettingsForcedFull = whole.SettingsForcedFull,
                TransitionsForcedFull = whole.TransitionsForcedFull,
                MotionClips = plan.MotionClips.Select(_ => index).ToArray(),
                ReverseOverLimit = plan.ReverseOverLimit.Select(_ => index).ToArray()
            });
        }

        return plans;
    }

    private static IReadOnlyList<TextLayer> SegmentTexts(EditTimeline timeline, int index)
    {
        var from = timeline.ClipStart(index);
        var to = timeline.ClipStart(index + 1);
        var texts = new List<TextLayer>();
        foreach (var text in timeline.Texts)
        {
            var start = Math.Max(text.Start, from);
            var end = Math.Min(text.End, to);
            if (end <= start) continue;
            var cut = start == text.Start && end == text.End ? text : text.TrimmedTo(start, end);
            texts.Add(cut.MovedTo(start - from));
        }

        return texts;
    }

    private static ExportPlan Full(
        EditTimeline timeline, MediaInfo info, ExportMode requested, string output, string work,
        double limit, IReadOnlyList<int> over, EditExportSettings settings, IReadOnlyList<int> tracks)
    {
        var graph = new StringBuilder();
        var joins = new StringBuilder();
        var audio = info.HasAudio ? tracks.Count : 0;
        var canvas = settings.HasSize ? (settings.Width, settings.Height) : ClipFilters.Canvas(timeline, info);
        var rate = settings.Fps > 0 ? ",fps=" + Number(settings.Fps) : string.Empty;
        var transitions = timeline.HasTransitions;
        var blend = transitions ? TransitionInputTail(settings, info) : string.Empty;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var start = Number(EditTime.ToSeconds(clip.SourceStart));
            var end = Number(EditTime.ToSeconds(clip.SourceEnd));
            graph.Append("[0:v]trim=start=").Append(start).Append(":end=").Append(end).Append(",setpts=PTS-STARTPTS")
                .Append(VideoMotion(clip, info))
                .Append(Link(ClipFilters.Geometry(clip.Effects, info.Width, info.Height, info.ParNum, info.ParDen, canvas)))
                .Append(Link(ClipFilters.Color(clip.Effects)))
                .Append(rate)
                .Append(Link(ClipFilters.VideoFades(clip.Effects, clip.TimelineLength)))
                .Append(blend)
                .Append("[v").Append(i).Append("];");
            joins.Append("[v").Append(i).Append(']');
            for (var p = 0; p < audio; p++)
            {
                var label = "[a" + i.ToString(CultureInfo.InvariantCulture) + (p == 0 ? string.Empty : "_" + p.ToString(CultureInfo.InvariantCulture)) + "]";
                graph.Append(AudioInput("0", tracks[p])).Append("atrim=start=").Append(start).Append(":end=").Append(end).Append(",asetpts=PTS-STARTPTS")
                    .Append(AudioMotion(clip)).Append(Link(ClipFilters.Audio(clip.Effects, clip.TimelineLength)))
                    .Append(label).Append(';');
                joins.Append(label);
            }
        }

        if (transitions)
        {
            graph.Append(TransitionChain(timeline, audio, timeline.HasText ? "[vcat]" : "[vout]"));
        }
        else
        {
            graph.Append(joins).Append("concat=n=").Append(timeline.Clips.Count).Append(":v=1:a=").Append(audio)
                .Append(timeline.HasText ? "[vcat]" : "[vout]");
            for (var p = 0; p < audio; p++) graph.Append(AudioOutput(p));
        }

        string? subtitle = null;
        string? fonts = null;
        if (timeline.HasText)
        {
            subtitle = Path.Combine(work, SubtitleFileName);
            fonts = Path.Combine(work, TextFonts.FolderName);
            graph.Append(";[vcat]").Append(AssFilter(subtitle, fonts)).Append("[vout]");
        }

        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", info.FilePath, "-filter_complex", graph.ToString(), "-map", "[vout]" };
        for (var p = 0; p < audio; p++) args.AddRange(new[] { "-map", AudioOutput(p) });
        args.AddRange(VideoEncode(settings));
        if (audio > 0) args.AddRange(new[] { "-c:a", AudioCodec, "-b:a", settings.AudioBitrate });
        args.AddRange(Faststart(output));
        args.Add(output);

        var expected = EditTime.ToSeconds(timeline.Duration - timeline.TransitionOverlap);
        return new ExportPlan(requested, ExportMode.Full, new[] { new ExportStep(args, expected) },
            over, limit, output, work)
        {
            SubtitlePath = subtitle,
            SubtitleContent = subtitle is null ? null : AssWriter.Write(timeline.Texts, canvas?.Width ?? info.Width, canvas?.Height ?? info.Height),
            FontsDirectory = fonts,
            FontFamilies = timeline.Texts.Select(t => t.FontName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    /// <summary>
    /// Gecisli cizelgede her klip zincirinin sonu: <c>xfade</c> iki girdinin kare hizini, piksel bicimini ve zaman tabanini ayni
    /// ister. Kare hizi teslim ayarindan, yoksa kaynaktan (bilinmiyorsa 30).
    /// </summary>
    private static string TransitionInputTail(EditExportSettings settings, MediaInfo info)
        => (settings.Fps > 0 ? string.Empty : ",fps=" + Number(info.Fps > 0 ? info.Fps : 30)) + ",format=yuv420p,settb=AVTB";

    /// <summary>
    /// Klipleri soldan saga ikiser birlestiren zincir. Gecisli komsuda goruntu <c>xfade</c>, ses <c>acrossfade</c> ile
    /// <see cref="EditTimeline.TransitionLength"/> kadar ust uste biner; <c>offset</c> o ana dek birlesen uzunluk eksi gecis
    /// suresidir. Gecissiz komsu <c>concat=n=2</c> ile kesme olarak eklenir.
    /// </summary>
    private static string TransitionChain(EditTimeline timeline, int audio, string videoOut)
    {
        var text = new StringBuilder();
        var last = timeline.Clips.Count - 1;
        var video = "[v0]";
        var sounds = Enumerable.Range(0, audio).Select(p => AudioLabel(0, p)).ToArray();
        var length = timeline.Clips[0].TimelineLength;
        for (var k = 1; k <= last; k++)
        {
            var overlap = timeline.TransitionLength(k);
            var nextVideo = k == last ? videoOut : "[x" + k.ToString(CultureInfo.InvariantCulture) + "]";
            text.Append(video).Append("[v").Append(k).Append(']');
            if (overlap > 0)
            {
                text.Append("xfade=transition=").Append(ClipTransition.XfadeName(timeline.Clips[k].Transition!.Kind))
                    .Append(":duration=").Append(Number(EditTime.ToSeconds(overlap)))
                    .Append(":offset=").Append(Number(EditTime.ToSeconds(length - overlap)));
            }
            else
            {
                text.Append("concat=n=2:v=1:a=0");
            }

            text.Append(nextVideo).Append(';');
            video = nextVideo;
            for (var p = 0; p < audio; p++)
            {
                var nextSound = k == last ? AudioOutput(p) : "[y" + k.ToString(CultureInfo.InvariantCulture) + "_" + p.ToString(CultureInfo.InvariantCulture) + "]";
                text.Append(sounds[p]).Append(AudioLabel(k, p))
                    .Append(overlap > 0 ? "acrossfade=d=" + Number(EditTime.ToSeconds(overlap)) : "concat=n=2:v=0:a=1")
                    .Append(nextSound).Append(';');
                sounds[p] = nextSound;
            }

            length += timeline.Clips[k].TimelineLength - overlap;
        }

        return text.ToString(0, text.Length - 1);
    }

    private static string AudioLabel(int clip, int position)
        => "[a" + clip.ToString(CultureInfo.InvariantCulture) + (position == 0 ? string.Empty : "_" + position.ToString(CultureInfo.InvariantCulture)) + "]";

    /// <summary>
    /// <c>ass=filename='...':fontsdir='...'</c>. Yollar <see cref="VideoFilterChain.FilterPath"/> ile
    /// kacar: ters bolu duz bolu olur, iki nokta <c>\:</c> olur (<c>C\:/...</c>).
    /// </summary>
    public static string AssFilter(string subtitlePath, string? fontsDirectory)
    {
        var filter = "ass=filename='" + VideoFilterChain.FilterPath(subtitlePath) + "'";
        if (fontsDirectory is not null) filter += ":fontsdir='" + VideoFilterChain.FilterPath(fontsDirectory) + "'";
        return filter;
    }

    private static ExportPlan FastDirect(
        EditTimeline timeline, MediaInfo info, IReadOnlyList<double> keyframes, double startTime, string output, string work,
        double limit, IReadOnlyList<int> over, IReadOnlyList<int> tracks)
    {
        var list = new StringBuilder("ffconcat version 1.0\n");
        var leads = new List<double>(timeline.Clips.Count);
        double expected = 0;
        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);
            var inpoint = KeyframeAtOrBefore(keyframes, start);
            leads.Add(start - inpoint);
            expected += end - inpoint;
            list.Append("file ").Append(Quote(info.FilePath)).Append('\n');
            if (inpoint > 0) list.Append("inpoint ").Append(Number(inpoint + startTime)).Append('\n');
            list.Append("outpoint ").Append(Number(end + startTime)).Append('\n');
        }

        var listPath = Path.Combine(work, VideoListName);
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-map", "0:v:0" };
        args.AddRange(AudioMaps(info, tracks));
        args.AddRange(new[] { "-c", "copy" });
        args.AddRange(Faststart(output));
        args.Add(output);

        return new ExportPlan(ExportMode.Fast, ExportMode.Fast, new[] { new ExportStep(args, expected, listPath, list.ToString()) },
            over, limit, output, work) { KeyframeLeads = leads };
    }

    private static ExportPlan Segmented(
        EditTimeline timeline, MediaInfo info, IReadOnlyList<double> keyframes,
        string output, string work, double limit, IReadOnlyList<int> over, IReadOnlyList<int> tracks)
    {
        var steps = new List<ExportStep>();
        var segments = new List<string>();
        var leads = new List<double>(timeline.Clips.Count);
        double expected = 0;

        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);

            if (NeedsReencode(clip))
            {
                var length = EditTime.ToSeconds(clip.TimelineLength);
                steps.Add(Encode(info, start, end - start, VideoMotion(clip, info), AudioMotion(clip), Segment(work, segments), tracks));
                expected += length;
                leads.Add(0);
                continue;
            }

            var inpoint = KeyframeAtOrBefore(keyframes, start);
            leads.Add(start - inpoint);
            steps.Add(Copy(info, inpoint, end - inpoint, Segment(work, segments), tracks));
            expected += end - inpoint;
        }

        var list = new StringBuilder("ffconcat version 1.0\n");
        foreach (var segment in segments) list.Append("file ").Append(Quote(segment)).Append('\n');
        var listPath = Path.Combine(work, VideoListName);
        var join = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-map", "0:v:0" };
        if (info.HasAudio)
            for (var p = 0; p < tracks.Count; p++) join.AddRange(new[] { "-map", "0:a:" + p.ToString(CultureInfo.InvariantCulture) });
        join.AddRange(new[] { "-c", "copy" });
        join.AddRange(Faststart(output));
        join.Add(output);
        steps.Add(new ExportStep(join, expected, listPath, list.ToString()));

        return new ExportPlan(ExportMode.Fast, ExportMode.Fast, steps, over, limit, output, work) { KeyframeLeads = leads };
    }

    /// <summary>
    /// Akilli kesim. Goruntu ve ses ayri parcalara yazilir, iki concat listesiyle birlestirilir:
    /// govde <c>-c copy</c> ile kare sayisina gore kesilir (<c>-t</c> kopyada DTS'e bakar ve B kareli
    /// akista fazladan paket birakir), kenarlar kaynagin kodegiyle kodlanir, ses klip basina tek
    /// parca kopyalanir. Her parcanin suresi listede yazilir; yoksa concat suresi paketten tahmin eder.
    /// Temiz sinir bulunamayan klip butunuyle kodlanir.
    /// </summary>
    private static ExportPlan SmartSegmented(
        EditTimeline timeline, MediaInfo info, SmartCutEncoding encoding, IReadOnlyList<SmartCutPoint> cuts,
        IReadOnlyList<double> keyframes, string output, string work, double limit, IReadOnlyList<int> over, IReadOnlyList<int> tracks)
    {
        var steps = new List<ExportStep>();
        var video = new StringBuilder("ffconcat version 1.0\n");
        var count = info.HasAudio ? tracks.Count : 0;
        var audio = Enumerable.Range(0, Math.Max(count, 1)).Select(_ => new StringBuilder("ffconcat version 1.0\n")).ToArray();
        var videoParts = 0;
        var audioParts = new int[audio.Length];
        var nudge = SmartCutCodec.CopySeekNudge(info);
        double expected = 0;

        string VideoPart(double length)
        {
            var path = Path.Combine(work, "v" + (videoParts++).ToString("D4", CultureInfo.InvariantCulture) + encoding.Extension);
            video.Append("file ").Append(Quote(path)).Append("\nduration ").Append(Number(length)).Append('\n');
            return path;
        }

        string AudioPart(int p, double length)
        {
            var prefix = p == 0 ? "a" : "a" + p.ToString(CultureInfo.InvariantCulture) + "_";
            var path = Path.Combine(work, prefix + (audioParts[p]++).ToString("D4", CultureInfo.InvariantCulture) + ".ts");
            audio[p].Append("file ").Append(Quote(path)).Append("\nduration ").Append(Number(length)).Append('\n');
            return path;
        }

        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);

            if (NeedsReencode(clip))
            {
                var length = EditTime.ToSeconds(clip.TimelineLength);
                steps.Add(SmartEncode(info, encoding, keyframes, start, end - start, VideoMotion(clip, info), VideoPart(MotionListSeconds(info, length))));
                for (var p = 0; p < count; p++) steps.Add(AudioEncode(info, start, end - start, AudioMotion(clip), AudioPart(p, length), tracks[p]));
                expected += length;
                continue;
            }

            if (SmartBody(cuts, keyframes, start, end, nudge) is var (a, b))
            {
                if (a.Time > start + 1e-6)
                {
                    var head = HeadListSeconds(info, a.Time - start);
                    var span = SmartCutCodec.SeekOvershoots(info.FormatName) ? a.Time - start : head;
                    steps.Add(SmartEncode(info, encoding, keyframes, start, span - HeadMarginSeconds(info), string.Empty, VideoPart(head)));
                }
                steps.Add(SmartCopy(info, encoding, a.Time + nudge, b.Time - a.Time, b.Packet - a.Packet, VideoPart(b.Time - a.Time)));
                if (end > b.Time + 1e-6) steps.Add(SmartEncode(info, encoding, keyframes, b.Time, end - b.Time, string.Empty, VideoPart(TailListSeconds(info, end - b.Time))));
            }
            else
            {
                steps.Add(SmartEncode(info, encoding, keyframes, start, end - start, string.Empty, VideoPart(end - start)));
            }

            for (var p = 0; p < count; p++) steps.Add(AudioCopy(info, start, end - start, AudioPart(p, end - start), tracks[p]));
            expected += end - start;
        }

        var videoList = Path.Combine(work, VideoListName);
        var audioLists = Enumerable.Range(0, count)
            .Select(p => Path.Combine(work, p == 0 ? AudioListName : "audio" + p.ToString(CultureInfo.InvariantCulture) + ".ffconcat"))
            .ToArray();
        var join = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", videoList };
        foreach (var list in audioLists) join.AddRange(new[] { "-f", "concat", "-safe", "0", "-i", list });
        join.AddRange(new[] { "-map", "0:v:0" });
        for (var p = 0; p < count; p++) join.AddRange(new[] { "-map", (p + 1).ToString(CultureInfo.InvariantCulture) + ":a:0" });
        join.AddRange(new[] { "-c", "copy" });
        if (info.FieldOrder is "progressive" or "tt" or "bb" or "tb" or "bt") join.AddRange(new[] { "-field_order:v", info.FieldOrder });
        if (encoding.OutputTag is { } tag && IsMp4Family(output)) join.AddRange(new[] { "-tag:v", tag });
        join.AddRange(Faststart(output));
        join.Add(output);
        steps.Add(new ExportStep(join, expected, videoList, video.ToString())
        {
            AudioListPath = count > 0 ? audioLists[0] : null,
            AudioListContent = count > 0 ? audio[0].ToString() : null,
            ExtraLists = Enumerable.Range(1, Math.Max(count - 1, 0)).Select(p => new ExportList(audioLists[p], audio[p].ToString())).ToArray()
        });

        return new ExportPlan(ExportMode.Smart, ExportMode.Smart, steps, over, limit, output, work);
    }

    /// <summary>
    /// Klibin icine dusen ilk ve son temiz sinir. Arama payi bir sonraki anahtar kareyi asiyorsa
    /// kopya yanlis GOP'tan baslar; o klipte govde yok sayilir.
    /// </summary>
    private static (SmartCutPoint Start, SmartCutPoint End)? SmartBody(
        IReadOnlyList<SmartCutPoint> cuts, IReadOnlyList<double> keyframes, double start, double end, double nudge)
    {
        SmartCutPoint? first = null;
        SmartCutPoint? last = null;
        foreach (var cut in cuts)
        {
            if (cut.Time < start - 1e-6 || cut.Time > end + 1e-6) continue;
            first ??= cut;
            last = cut;
        }

        if (first is not { } a || last is not { } b || b.Time <= a.Time + 1e-6 || b.Packet <= a.Packet) return null;
        if (keyframes.Any(k => k > a.Time + 1e-6 && k <= a.Time + nudge + SeekNudgeSeconds)) return null;
        return (a, b);
    }

    /// <summary>
    /// mpegts'te girdi aramasi hedefin ardindaki anahtar kareye iniyor (olculen: 0,5 sn hedef 0,92 sn'deki
    /// anahtar kareden basladi, on kare kayboldu). Orada arama hedefin onundeki anahtar karenin hemen
    /// onune yapilir, kenar <c>trim</c> suzgecinde kare zamanina gore kesilir.
    /// </summary>
    private static ExportStep SmartEncode(MediaInfo info, SmartCutEncoding encoding, IReadOnlyList<double> keyframes, double start, double length, string motion, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        var trim = string.Empty;
        if (SmartCutCodec.SeekOvershoots(info.FormatName))
        {
            var anchor = keyframes.Where(k => k <= start + 1e-6).DefaultIfEmpty(0).Max();
            if (anchor - SeekNudgeSeconds > 0) args.AddRange(new[] { "-ss", Number(anchor - SeekNudgeSeconds) });
            var offset = Math.Max(0, start - anchor);
            args.AddRange(new[] { "-t", Number(offset + length + SeekNudgeSeconds), "-i", info.FilePath });
            trim = $",trim=start={Number(offset)}:duration={Number(length)},setpts=PTS-STARTPTS";
        }
        else
        {
            if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
            args.AddRange(new[] { "-t", Number(length), "-i", info.FilePath });
        }

        args.AddRange(new[] { "-map", "0:v:0", "-an", "-vf", "setpts=PTS-STARTPTS" + trim + motion });
        args.AddRange(encoding.VideoArgs);
        args.AddRange(Intermediate(encoding, target));
        return new ExportStep(args, length);
    }

    private static ExportStep SmartCopy(MediaInfo info, SmartCutEncoding encoding, double seek, double length, int frames, string target)
    {
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-ss", Number(seek), "-i", info.FilePath, "-t", Number(length),
            "-map", "0:v:0", "-an", "-c", "copy", "-frames:v", frames.ToString(CultureInfo.InvariantCulture),
            "-avoid_negative_ts", "make_zero"
        };
        args.AddRange(Intermediate(encoding, target));
        return new ExportStep(args, length);
    }

    private static IEnumerable<string> Intermediate(SmartCutEncoding encoding, string target)
        => encoding.Container == SmartCutCodec.Mp4
            ? new[] { "-video_track_timescale", SmartCutCodec.IntermediateTimescale, "-f", encoding.Container, target }
            : new[] { "-f", encoding.Container, target };

    private static ExportStep AudioCopy(MediaInfo info, double start, double length, string target, int track)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[]
        {
            "-i", info.FilePath, "-ss", "0", "-t", Number(length), "-map", AudioStream(track), "-vn", "-c", "copy",
            "-avoid_negative_ts", "make_zero", "-f", SmartCutCodec.TransportStream, target
        });
        return new ExportStep(args, length);
    }

    private static ExportStep AudioEncode(MediaInfo info, double start, double length, string motion, string target, int track)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[]
        {
            "-t", Number(length), "-i", info.FilePath, "-map", AudioStream(track), "-vn", "-af", "asetpts=PTS-STARTPTS" + motion,
            "-c:a", AudioCodec, "-b:a", "192k", "-f", SmartCutCodec.TransportStream, target
        });
        return new ExportStep(args, length);
    }

    private static ExportStep Copy(MediaInfo info, double start, double length, string target, IReadOnlyList<int> tracks)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start + SeekNudgeSeconds) });
        args.AddRange(new[] { "-i", info.FilePath, "-t", Number(length), "-map", "0:v:0" });
        args.AddRange(AudioMaps(info, tracks));
        args.AddRange(new[] { "-c", "copy", "-avoid_negative_ts", "make_zero", "-f", "mpegts", target });
        return new ExportStep(args, length);
    }

    private static ExportStep Encode(MediaInfo info, double start, double length, string videoMotion, string audioMotion, string target, IReadOnlyList<int> tracks)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[] { "-t", Number(length), "-i", info.FilePath, "-map", "0:v:0" });
        args.AddRange(AudioMaps(info, tracks));
        args.AddRange(new[] { "-vf", "setpts=PTS-STARTPTS" + videoMotion });
        if (info.HasAudio) args.AddRange(new[] { "-af", "asetpts=PTS-STARTPTS" + audioMotion });
        args.AddRange(new[] { "-c:v", MatchingEncoders[info.VideoCodec], "-preset", "medium", "-crf", FullCrf });
        args.AddRange(new[] { "-pix_fmt", string.IsNullOrEmpty(info.PixelFormat) ? "yuv420p" : info.PixelFormat });
        if (info.HasAudio) args.AddRange(new[] { "-c:a", AudioCodec, "-b:a", "192k" });
        args.AddRange(new[] { "-f", "mpegts", target });
        return new ExportStep(args, length);
    }

    private static string VideoMotion(EditClip clip, MediaInfo info)
    {
        var text = new StringBuilder();
        if (clip.Reversed) text.Append(",reverse");
        if (clip.Speed != 1m)
            text.Append(",setpts=PTS/").Append(clip.Speed.ToString(CultureInfo.InvariantCulture))
                .Append(",fps=").Append(Number(info.Fps > 0 ? info.Fps : 30));
        return text.ToString();
    }

    private static string AudioMotion(EditClip clip)
    {
        var text = new StringBuilder();
        if (clip.Reversed) text.Append(",areverse");
        if (clip.Speed != 1m) text.Append(',').Append(AtempoFilter(clip.Speed));
        return text.ToString();
    }

    private static string Link(string chain) => chain.Length == 0 ? string.Empty : "," + chain;

    private static string Segment(string work, List<string> segments)
    {
        var path = Path.Combine(work, "part" + segments.Count.ToString("D4", CultureInfo.InvariantCulture) + ".ts");
        segments.Add(path);
        return path;
    }

    private static double KeyframeAtOrBefore(IReadOnlyList<double> keyframes, double at)
    {
        double best = 0;
        foreach (var k in keyframes)
        {
            if (k > at + 1e-6) break;
            best = k;
        }

        return best;
    }

    private static IEnumerable<string> Faststart(string output)
        => IsMp4Family(output) ? new[] { "-movflags", "+faststart" } : Array.Empty<string>();

    private static bool IsMp4Family(string output)
        => Path.GetExtension(output).ToLowerInvariant() is ".mp4" or ".m4v" or ".mov";

    private static string Quote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''") + "'";

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
