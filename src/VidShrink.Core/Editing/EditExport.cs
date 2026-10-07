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
}

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

    /// <summary>Cizelgede metin oldugu icin <c>-c copy</c> yolu birakilip Tam'a gecildi.</summary>
    public bool TextForcedFull { get; init; }

    /// <summary>Klip ayarlari (kirpma, dondurme, ses, solma) yeniden kodlama istedigi icin Tam'a gecildi.</summary>
    public bool EffectsForcedFull { get; init; }

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

    public const string SubtitleFileName = "text.ass";

    public const string VideoListName = "list.ffconcat";
    public const string AudioListName = "audio.ffconcat";

    private const double SeekNudgeSeconds = 0.001;

    private static readonly IReadOnlyDictionary<string, string> MatchingEncoders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["h264"] = "libx264",
        ["hevc"] = "libx265"
    };

    public static bool NeedsReencode(EditClip clip) => clip.Speed != 1m || clip.Reversed;

    public static bool SupportsSegments(MediaInfo info)
        => MatchingEncoders.ContainsKey(info.VideoCodec) && AudioCopies(info);

    /// <summary>
    /// Akilli kipin kapisi: ses kopyalanabilmeli, kesim noktalari okunmus olmali ve
    /// <see cref="SmartCutCodec.Resolve"/> kaynaga bir kodlayici esleyebilmeli. Biri tutmazsa <c>null</c>.
    /// </summary>
    public static SmartCutEncoding? SmartEncoding(
        MediaInfo info, IReadOnlyList<SmartCutPoint>? cuts, Func<string, bool>? hasEncoder = null)
        => cuts is { Count: > 0 } && AudioCopies(info) ? SmartCutCodec.Resolve(info, hasEncoder) : null;

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
        IReadOnlyList<SmartCutPoint>? cuts = null, Func<string, bool>? hasEncoder = null, bool dropMetadata = false)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));

        var effective = mode;
        var anySpecial = timeline.Clips.Any(NeedsReencode);
        var smart = mode == ExportMode.Smart ? SmartEncoding(info, cuts, hasEncoder) : null;
        if (mode == ExportMode.Smart && smart is null) effective = ExportMode.Full;
        if (mode == ExportMode.Fast && anySpecial && !SupportsSegments(info)) effective = ExportMode.Full;
        if (mode != ExportMode.Full && keyframes.Count == 0) effective = ExportMode.Full;
        var textForced = mode != ExportMode.Full && effective != ExportMode.Full && timeline.HasText;
        var effectsForced = mode != ExportMode.Full && effective != ExportMode.Full && timeline.HasEffects;
        if (timeline.HasText || timeline.HasEffects) effective = ExportMode.Full;

        var limit = ReverseLimitSeconds(info, memoryBudgetBytes);
        var over = ReverseOverLimit(timeline, info, memoryBudgetBytes);

        var motion = Enumerable.Range(0, timeline.Clips.Count).Where(i => NeedsReencode(timeline.Clips[i])).ToArray();
        var plan = effective switch
        {
            ExportMode.Full => Full(timeline, info, mode, outputPath, workDirectory, limit, over),
            ExportMode.Smart => SmartSegmented(timeline, info, smart!, cuts!, keyframes, outputPath, workDirectory, limit, over),
            ExportMode.Fast when !anySpecial => FastDirect(timeline, info, keyframes, startTime, outputPath, workDirectory, limit, over),
            _ => Segmented(timeline, info, keyframes, outputPath, workDirectory, limit, over)
        };
        if (dropMetadata) plan = plan with { Steps = WithoutMetadata(plan.Steps, info) };
        return plan with { MotionClips = motion, TextForcedFull = textForced, EffectsForcedFull = effectsForced };
    }

    /// <summary>
    /// Teslim dosyasini yazan son adima meta silme argumanlarini ekler; ara parcalara dokunmaz. Dizi
    /// kucultmeyle ayni kaynaktan (<see cref="StreamMapping.MetadataArguments"/>). Teslim hep kaynagin
    /// ilk ses izini tasidigi icin dil ondan yazilir. Duzenleyici kendi basina etiket ya da bolum yazmaz.
    /// </summary>
    private static IReadOnlyList<ExportStep> WithoutMetadata(IReadOnlyList<ExportStep> steps, MediaInfo info)
    {
        var last = steps[^1];
        var language = info.Streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Language;
        var audio = info.HasAudio ? new[] { language } : Array.Empty<string?>();
        var args = last.Args.Take(last.Args.Count - 1)
            .Concat(StreamMapping.MetadataArguments(true, audio, Array.Empty<string?>()))
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
        IReadOnlyList<SmartCutPoint>? cuts = null, Func<string, bool>? hasEncoder = null, bool dropMetadata = false)
    {
        if (timeline.Clips.Count == 0) throw new ArgumentException("Cizelge bos", nameof(timeline));
        if (outputPaths.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir cikti yolu gerekir", nameof(outputPaths));
        if (workDirectories.Count != timeline.Clips.Count)
            throw new ArgumentException("Her parcaya bir is klasoru gerekir", nameof(workDirectories));

        var whole = Build(timeline, info, keyframes, startTime, mode, outputPaths[0], workDirectories[0], memoryBudgetBytes, cuts, hasEncoder);
        var plans = new List<ExportPlan>(timeline.Clips.Count);
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var index = i;
            var piece = new EditTimeline(new[] { timeline.Clips[i] }, texts: SegmentTexts(timeline, i));
            var plan = Build(piece, info, keyframes, startTime, whole.Effective, outputPaths[i], workDirectories[i], memoryBudgetBytes, cuts, hasEncoder, dropMetadata);
            plans.Add(plan with
            {
                Requested = mode,
                TextForcedFull = whole.TextForcedFull,
                EffectsForcedFull = whole.EffectsForcedFull,
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
        double limit, IReadOnlyList<int> over)
    {
        var graph = new StringBuilder();
        var joins = new StringBuilder();
        var canvas = ClipFilters.Canvas(timeline, info);
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var start = Number(EditTime.ToSeconds(clip.SourceStart));
            var end = Number(EditTime.ToSeconds(clip.SourceEnd));
            graph.Append("[0:v]trim=start=").Append(start).Append(":end=").Append(end).Append(",setpts=PTS-STARTPTS")
                .Append(VideoMotion(clip, info))
                .Append(Link(ClipFilters.Geometry(clip.Effects, info.Width, info.Height, info.ParNum, info.ParDen, canvas)))
                .Append(Link(ClipFilters.VideoFades(clip.Effects, clip.TimelineLength)))
                .Append("[v").Append(i).Append("];");
            joins.Append("[v").Append(i).Append(']');
            if (!info.HasAudio) continue;
            graph.Append("[0:a]atrim=start=").Append(start).Append(":end=").Append(end).Append(",asetpts=PTS-STARTPTS")
                .Append(AudioMotion(clip)).Append(Link(ClipFilters.Audio(clip.Effects, clip.TimelineLength)))
                .Append("[a").Append(i).Append("];");
            joins.Append("[a").Append(i).Append(']');
        }

        graph.Append(joins).Append("concat=n=").Append(timeline.Clips.Count).Append(":v=1:a=").Append(info.HasAudio ? 1 : 0)
            .Append(timeline.HasText ? "[vcat]" : "[vout]").Append(info.HasAudio ? "[aout]" : string.Empty);

        string? subtitle = null;
        string? fonts = null;
        if (timeline.HasText)
        {
            subtitle = Path.Combine(work, SubtitleFileName);
            fonts = Path.Combine(work, TextFonts.FolderName);
            graph.Append(";[vcat]").Append(AssFilter(subtitle, fonts)).Append("[vout]");
        }

        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", info.FilePath, "-filter_complex", graph.ToString(), "-map", "[vout]" };
        if (info.HasAudio) args.AddRange(new[] { "-map", "[aout]" });
        args.AddRange(new[] { "-c:v", FullVideoCodec, "-preset", "medium", "-crf", FullCrf, "-pix_fmt", "yuv420p" });
        if (info.HasAudio) args.AddRange(new[] { "-c:a", AudioCodec, "-b:a", "192k" });
        args.AddRange(Faststart(output));
        args.Add(output);

        var expected = EditTime.ToSeconds(timeline.Duration);
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
        double limit, IReadOnlyList<int> over)
    {
        var list = new StringBuilder("ffconcat version 1.0\n");
        double expected = 0;
        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);
            var inpoint = KeyframeAtOrBefore(keyframes, start);
            expected += end - inpoint;
            list.Append("file ").Append(Quote(info.FilePath)).Append('\n');
            if (inpoint > 0) list.Append("inpoint ").Append(Number(inpoint + startTime)).Append('\n');
            list.Append("outpoint ").Append(Number(end + startTime)).Append('\n');
        }

        var listPath = Path.Combine(work, VideoListName);
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-map", "0:v:0" };
        if (info.HasAudio) args.AddRange(new[] { "-map", "0:a:0" });
        args.AddRange(new[] { "-c", "copy" });
        args.AddRange(Faststart(output));
        args.Add(output);

        return new ExportPlan(ExportMode.Fast, ExportMode.Fast, new[] { new ExportStep(args, expected, listPath, list.ToString()) },
            over, limit, output, work);
    }

    private static ExportPlan Segmented(
        EditTimeline timeline, MediaInfo info, IReadOnlyList<double> keyframes,
        string output, string work, double limit, IReadOnlyList<int> over)
    {
        var steps = new List<ExportStep>();
        var segments = new List<string>();
        double expected = 0;

        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);

            if (NeedsReencode(clip))
            {
                var length = EditTime.ToSeconds(clip.TimelineLength);
                steps.Add(Encode(info, start, end - start, VideoMotion(clip, info), AudioMotion(clip), Segment(work, segments)));
                expected += length;
                continue;
            }

            var inpoint = KeyframeAtOrBefore(keyframes, start);
            steps.Add(Copy(info, inpoint, end - inpoint, Segment(work, segments)));
            expected += end - inpoint;
        }

        var list = new StringBuilder("ffconcat version 1.0\n");
        foreach (var segment in segments) list.Append("file ").Append(Quote(segment)).Append('\n');
        var listPath = Path.Combine(work, VideoListName);
        var join = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-map", "0:v:0" };
        if (info.HasAudio) join.AddRange(new[] { "-map", "0:a:0" });
        join.AddRange(new[] { "-c", "copy" });
        join.AddRange(Faststart(output));
        join.Add(output);
        steps.Add(new ExportStep(join, expected, listPath, list.ToString()));

        return new ExportPlan(ExportMode.Fast, ExportMode.Fast, steps, over, limit, output, work);
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
        IReadOnlyList<double> keyframes, string output, string work, double limit, IReadOnlyList<int> over)
    {
        var steps = new List<ExportStep>();
        var video = new StringBuilder("ffconcat version 1.0\n");
        var audio = new StringBuilder("ffconcat version 1.0\n");
        var videoParts = 0;
        var audioParts = 0;
        var nudge = SmartCutCodec.CopySeekNudge(info);
        double expected = 0;

        string VideoPart(double length)
        {
            var path = Path.Combine(work, "v" + (videoParts++).ToString("D4", CultureInfo.InvariantCulture) + encoding.Extension);
            video.Append("file ").Append(Quote(path)).Append("\nduration ").Append(Number(length)).Append('\n');
            return path;
        }

        string AudioPart(double length)
        {
            var path = Path.Combine(work, "a" + (audioParts++).ToString("D4", CultureInfo.InvariantCulture) + ".ts");
            audio.Append("file ").Append(Quote(path)).Append("\nduration ").Append(Number(length)).Append('\n');
            return path;
        }

        foreach (var clip in timeline.Clips)
        {
            var start = EditTime.ToSeconds(clip.SourceStart);
            var end = EditTime.ToSeconds(clip.SourceEnd);

            if (NeedsReencode(clip))
            {
                var length = EditTime.ToSeconds(clip.TimelineLength);
                steps.Add(SmartEncode(info, encoding, start, end - start, VideoMotion(clip, info), VideoPart(length)));
                if (info.HasAudio) steps.Add(AudioEncode(info, start, end - start, AudioMotion(clip), AudioPart(length)));
                expected += length;
                continue;
            }

            if (SmartBody(cuts, keyframes, start, end, nudge) is var (a, b))
            {
                if (a.Time > start + 1e-6) steps.Add(SmartEncode(info, encoding, start, a.Time - start, string.Empty, VideoPart(a.Time - start)));
                steps.Add(SmartCopy(info, encoding, a.Time + nudge, b.Time - a.Time, b.Packet - a.Packet, VideoPart(b.Time - a.Time)));
                if (end > b.Time + 1e-6) steps.Add(SmartEncode(info, encoding, b.Time, end - b.Time, string.Empty, VideoPart(end - b.Time)));
            }
            else
            {
                steps.Add(SmartEncode(info, encoding, start, end - start, string.Empty, VideoPart(end - start)));
            }

            if (info.HasAudio) steps.Add(AudioCopy(info, start, end - start, AudioPart(end - start)));
            expected += end - start;
        }

        var videoList = Path.Combine(work, VideoListName);
        var audioList = Path.Combine(work, AudioListName);
        var join = new List<string> { "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", videoList };
        if (info.HasAudio) join.AddRange(new[] { "-f", "concat", "-safe", "0", "-i", audioList });
        join.AddRange(new[] { "-map", "0:v:0" });
        if (info.HasAudio) join.AddRange(new[] { "-map", "1:a:0" });
        join.AddRange(new[] { "-c", "copy" });
        if (encoding.OutputTag is { } tag && IsMp4Family(output)) join.AddRange(new[] { "-tag:v", tag });
        join.AddRange(Faststart(output));
        join.Add(output);
        steps.Add(new ExportStep(join, expected, videoList, video.ToString())
        {
            AudioListPath = info.HasAudio ? audioList : null,
            AudioListContent = info.HasAudio ? audio.ToString() : null
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

    private static ExportStep SmartEncode(MediaInfo info, SmartCutEncoding encoding, double start, double length, string motion, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[] { "-t", Number(length), "-i", info.FilePath, "-map", "0:v:0", "-an", "-vf", "setpts=PTS-STARTPTS" + motion });
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

    private static ExportStep AudioCopy(MediaInfo info, double start, double length, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[]
        {
            "-i", info.FilePath, "-ss", "0", "-t", Number(length), "-map", "0:a:0", "-vn", "-c", "copy",
            "-avoid_negative_ts", "make_zero", "-f", SmartCutCodec.TransportStream, target
        });
        return new ExportStep(args, length);
    }

    private static ExportStep AudioEncode(MediaInfo info, double start, double length, string motion, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[]
        {
            "-t", Number(length), "-i", info.FilePath, "-map", "0:a:0", "-vn", "-af", "asetpts=PTS-STARTPTS" + motion,
            "-c:a", AudioCodec, "-b:a", "192k", "-f", SmartCutCodec.TransportStream, target
        });
        return new ExportStep(args, length);
    }

    private static ExportStep Copy(MediaInfo info, double start, double length, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start + SeekNudgeSeconds) });
        args.AddRange(new[] { "-i", info.FilePath, "-t", Number(length), "-map", "0:v:0" });
        if (info.HasAudio) args.AddRange(new[] { "-map", "0:a:0" });
        args.AddRange(new[] { "-c", "copy", "-avoid_negative_ts", "make_zero", "-f", "mpegts", target });
        return new ExportStep(args, length);
    }

    private static ExportStep Encode(MediaInfo info, double start, double length, string videoMotion, string audioMotion, string target)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y" };
        if (start > 0) args.AddRange(new[] { "-ss", Number(start) });
        args.AddRange(new[] { "-t", Number(length), "-i", info.FilePath, "-map", "0:v:0" });
        if (info.HasAudio) args.AddRange(new[] { "-map", "0:a:0" });
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
