using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

/// <summary>
/// CMX3600 EDL. Her parca bir <c>AA/V</c> kesme olayidir: kaynak sutunu parcanin kaynak araligi, kayit sutunu parca
/// uzunluklarinin birikimi (gecis kesme sayilir). Hiz ve yon <c>M2</c> satirinda kare/sn olarak (ters parcada eksi),
/// etiket <c>* COMMENT</c>, dosya adi <c>* FROM CLIP NAME</c> satirindadir. 29,97 ve 59,94'te dusen kare yazilir.
/// Okurken goruntu olayi varsa yalniz goruntu olaylari alinir; sesin ayri olayi ayni araligi ikiler.
/// </summary>
internal static class CutListEdl
{
    private const string Reel = "AX";

    public static string Write(EditTimeline timeline, CutListContext context)
    {
        var fps = context.Fps;
        var title = context.Title ?? (context.SourcePaths.Count > 0 ? Path.GetFileNameWithoutExtension(CutList.FileName(context.SourcePaths[0])) : AppIdentity.Name);
        var text = new StringBuilder();
        text.Append("TITLE: ").Append(title).Append("\r\n");
        var drop = EditTimecode.Format(0, fps).Contains(';');
        text.Append(drop ? "FCM: DROP FRAME" : "FCM: NON-DROP FRAME").Append("\r\n\r\n");
        long record = 0;
        for (var i = 0; i < timeline.Clips.Count; i++)
        {
            var clip = timeline.Clips[i];
            var srcIn = EditTimecode.Format(clip.SourceStart, fps);
            var recIn = EditTimecode.Format(record, fps);
            record += clip.TimelineLength;
            text.Append(string.Create(CultureInfo.InvariantCulture,
                $"{i + 1:000}  {Reel,-8} {"AA/V",-5} C        {srcIn} {EditTimecode.Format(clip.SourceEnd, fps)} {recIn} {EditTimecode.Format(record, fps)}")).Append("\r\n");
            if (clip.Speed != 1m || clip.Reversed)
            {
                var rate = ((decimal)fps * clip.Speed * (clip.Reversed ? -1 : 1)).ToString("0.###", CultureInfo.InvariantCulture);
                text.Append(string.Create(CultureInfo.InvariantCulture, $"M2   {Reel,-8} {rate,-8} {srcIn}")).Append("\r\n");
            }

            if (clip.Source < context.SourcePaths.Count)
                text.Append("* FROM CLIP NAME: ").Append(CutList.FileName(context.SourcePaths[clip.Source])).Append("\r\n");
            if (clip.Label is { } label)
                text.Append("* COMMENT: ").Append(label.Replace('\r', ' ').Replace('\n', ' ')).Append("\r\n");
            text.Append("\r\n");
        }

        return text.ToString();
    }

    public static IReadOnlyList<CutListEntry>? Read(string text, double fps)
    {
        var events = new List<(bool Video, CutListEntry Entry)>();
        foreach (var raw in CutListText.Lines(text))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("* COMMENT:", StringComparison.OrdinalIgnoreCase))
            {
                if (events.Count > 0)
                    events[^1] = (events[^1].Video, events[^1].Entry with { Label = line["* COMMENT:".Length..].Trim() });
                continue;
            }

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens[0].Equals("M2", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length < 4 || !double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) || rate == 0 || fps <= 0)
                    return null;
                if (events.Count > 0)
                {
                    var speed = (decimal)Math.Round(Math.Abs(rate) / fps, 2, MidpointRounding.AwayFromZero);
                    events[^1] = (events[^1].Video, events[^1].Entry with { Speed = speed, Reversed = rate < 0 });
                }

                continue;
            }

            if (!char.IsAsciiDigit(tokens[0][0])) continue;
            if (tokens.Length < 8 || !tokens[0].All(char.IsAsciiDigit)) return null;
            if (!EditTimecode.TryParse(tokens[^4], fps, out var srcIn) || !EditTimecode.TryParse(tokens[^3], fps, out var srcOut))
                return null;
            events.Add((tokens[2].Contains('V', StringComparison.OrdinalIgnoreCase) || tokens[2].Equals("B", StringComparison.OrdinalIgnoreCase), new CutListEntry(srcIn, srcOut)));
        }

        if (events.Count == 0) return null;
        var video = events.Any(e => e.Video);
        return events.Where(e => e.Video || !video).Select(e => e.Entry).ToArray();
    }
}
