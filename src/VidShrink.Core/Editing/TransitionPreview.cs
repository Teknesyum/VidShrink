using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VidShrink.Core.Editing;

public static class TransitionPreview
{
    public const string Extension = ".mkv";

    public const string Crf = "12";

    public static string CacheRoot => Path.Combine(Path.GetTempPath(), AppIdentity.FolderName, "gecis");

    public static string ProcessFolder(string root) => Path.Combine(root, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

    public static string FileName(string key) => key + Extension;

    public static int SweepStale(string root)
    {
        string[] folders;
        try
        {
            folders = Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        var removed = 0;
        foreach (var folder in folders)
        {
            if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == Environment.ProcessId || Alive(id)) continue;
            if (Remove(folder)) removed++;
        }

        return removed;
    }

    public static bool Remove(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Alive(int id)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static IReadOnlyList<int> Pending(EditTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        return Enumerable.Range(1, Math.Max(0, timeline.Clips.Count - 1)).Where(k => timeline.TransitionLength(k) > 0).ToList();
    }

    public static string Key(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var text = string.Join("\n", arguments.Take(arguments.Count - 1));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).Substring(0, 24).ToLowerInvariant();
    }

    public static IReadOnlyList<string> Arguments(
        EditTimeline timeline, int index, IReadOnlyList<string> sourcePaths, IReadOnlyList<MediaInfo> sources,
        (int Width, int Height) canvas, double fps, bool audio, int track, string output)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(output);
        if (index < 1 || index >= timeline.Clips.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Gecis klibi cizelgede yok");
        var overlap = timeline.TransitionLength(index);
        if (overlap <= 0)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Klibin gecisi yok");

        var outgoing = timeline.Clips[index - 1];
        var incoming = timeline.Clips[index];
        var rate = Number(fps > 0 ? fps : 30);
        var length = Number(EditTime.ToSeconds(overlap));
        var position = Enumerable.Range(0, index - 1).Sum(j => timeline.Clips[j].TimelineLength) - Enumerable.Range(1, index - 1).Sum(timeline.TransitionLength);
        var offset = Number(EditTime.ToSeconds(position + outgoing.TimelineLength - overlap));
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-threads", "2", "-filter_complex_threads", "2" };
        var graph = new StringBuilder();
        var sides = new[]
        {
            (Clip: outgoing, Part: new EdlPart(0, outgoing, 0, 0) { Head = outgoing.TimelineLength - overlap }, From: outgoing.TimelineLength - overlap),
            (Clip: incoming, Part: new EdlPart(0, incoming, 0, 0) { Tail = incoming.TimelineLength - overlap }, From: 0L),
        };
        for (var n = 0; n < sides.Length; n++)
        {
            var (clip, part, from) = sides[n];
            var info = sources[clip.Source];
            args.AddRange(new[]
            {
                "-threads", "2",
                "-ss", Number(EditTime.ToSeconds(part.SourceFrom)),
                "-t", Number(EditTime.ToSeconds(part.EdlLength)),
                "-i", sourcePaths[clip.Source],
            });
            var input = n.ToString(CultureInfo.InvariantCulture);
            var shift = Number(EditTime.ToSeconds(part.HeadConsumed) / (double)clip.Speed);
            var start = Number(EditTime.ToSeconds(from));
            graph.Append('[').Append(input).Append(":v]setpts=PTS-STARTPTS")
                .Append(EditExport.VideoMotion(clip, info))
                .Append(",setpts=PTS+").Append(shift).Append("/TB")
                .Append(Link(ClipFilters.Geometry(clip.Effects, info.Width, info.Height, info.ParNum, info.ParDen, canvas)))
                .Append(Link(EditExport.BlurChain(clip.Effects, info, canvas, n)))
                .Append(Link(ClipFilters.Color(clip.Effects)))
                .Append(",fps=").Append(rate).Append(",format=yuv420p")
                .Append(Link(ClipFilters.VideoFades(clip.Effects, clip.TimelineLength)))
                .Append(",trim=start=").Append(start)
                .Append(n == 0 ? ",setpts=PTS-STARTPTS+" + offset + "/TB" : ",setpts=PTS-STARTPTS")
                .Append(",tpad=stop_mode=clone:stop_duration=").Append(length)
                .Append(",trim=duration=").Append(length).Append(",settb=AVTB[v").Append(input).Append("];");
            if (!audio) continue;
            if (info.HasAudio)
            {
                var stream = clip.Source == 0 ? Math.Max(track, 0) : EditAudioTrack.First;
                graph.Append('[').Append(input).Append(":a:").Append(stream.ToString(CultureInfo.InvariantCulture)).Append("]asetpts=PTS-STARTPTS")
                    .Append(EditExport.AudioMotion(clip))
                    .Append(",asetpts=PTS+").Append(shift).Append("/TB")
                    .Append(Link(ClipFilters.Audio(clip.Effects, clip.TimelineLength)))
                    .Append(",atrim=start=").Append(start).Append(",asetpts=PTS-STARTPTS,").Append(EditExport.MergeAudioFormat);
            }
            else
            {
                graph.Append(EditExport.MergeSilence);
            }

            graph.Append(",apad,atrim=duration=").Append(length).Append("[a").Append(input).Append("];");
        }

        graph.Append("[v0][v1]xfade=transition=").Append(ClipTransition.XfadeName(incoming.Transition!.Kind))
            .Append(":duration=").Append(length).Append(":offset=0,setpts=PTS-STARTPTS,trim=duration=").Append(length).Append("[vout]");
        if (audio) graph.Append(";[a0][a1]acrossfade=d=").Append(length).Append("[aout]");

        args.AddRange(new[] { "-filter_complex", graph.ToString(), "-map", "[vout]" });
        if (audio) args.AddRange(new[] { "-map", "[aout]", "-c:a", "pcm_s16le" });
        else args.Add("-an");
        args.AddRange(new[]
        {
            "-fps_mode", "cfr", "-r", rate, "-c:v", "libx264", "-preset", "ultrafast", "-crf", Crf, "-g", rate.Split('.')[0], "-pix_fmt", "yuv420p",
            "-threads", "2", "-sn", "-dn", "-map_metadata", "-1", output,
        });
        return args;
    }

    private static string Link(string chain) => chain.Length == 0 ? string.Empty : "," + chain;

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
