using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Core.Editing;

public enum SubtitleFormat
{
    Srt,
    Vtt,
    Ass
}

/// <summary>Altyazi dosyasindaki tek satir: cizelge tick'i ve satir sonlari <c>\n</c> olan duz metin.</summary>
public sealed record SubtitleCue(long Start, long End, string Text);

/// <summary>
/// SRT, WebVTT ve ASS/SSA altyazisini ffmpeg'siz okur, metin izini SRT ya da ASS olarak yazar. BOM atilir,
/// CRLF ve CR <c>\n</c> olur; bicim etiketleri (SRT/VTT <c>&lt;i&gt;</c>, ASS <c>{\...}</c>) atilir, metin kalir.
/// ASS'te ayni stil ve metinle ardisik gelen olaylar (AssWriter'in hareket ve daktilo bolmesi) tek satira birlesir.
/// Surec acmaz, dosya yazmaz.
/// </summary>
public static partial class SubtitleFile
{
    /// <summary>Iceri alinan altyazinin dikey yeri, kare yuksekliginin kesri.</summary>
    public const double LineY = 0.9;

    private static readonly string[] DefaultAssFields = { "layer", "start", "end", "style", "name", "marginl", "marginr", "marginv", "effect", "text" };

    public static SubtitleFormat? FormatOf(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".srt" => SubtitleFormat.Srt,
            ".vtt" => SubtitleFormat.Vtt,
            ".ass" or ".ssa" => SubtitleFormat.Ass,
            _ => null
        };

    public static bool CanWrite(SubtitleFormat format) => format is SubtitleFormat.Srt or SubtitleFormat.Ass;

    /// <summary>Okunan satirlar; gecerli tek satir yoksa <c>null</c>.</summary>
    public static IReadOnlyList<SubtitleCue>? Read(SubtitleFormat format, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = content.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var cues = format == SubtitleFormat.Ass ? ReadAss(text) : ReadTimed(text, format == SubtitleFormat.Vtt);
        return cues.Count == 0 ? null : cues;
    }

    /// <summary>
    /// Satirlari metin katmanina cevirir: bos metin atilir, cizelgenin sonundan sonra baslayan atilir, tasan sona
    /// kirpilir. Metin alt ortada (<see cref="LineY"/>) durur.
    /// </summary>
    public static IReadOnlyList<TextLayer> ToLayers(IEnumerable<SubtitleCue> cues, long duration)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var layers = new List<TextLayer>();
        foreach (var cue in cues)
        {
            if (string.IsNullOrWhiteSpace(cue.Text) || cue.Start < 0) continue;
            var end = Math.Min(cue.End, duration);
            if (end <= cue.Start) continue;
            layers.Add(new TextLayer(cue.Text, cue.Start, end) { Keyframes = new[] { new TextKeyframe(0, 0.5, LineY) } });
        }

        return layers;
    }

    /// <summary>SRT metni: baslangica gore sirali, 1'den numarali, <c>HH:MM:SS,mmm</c>, CRLF; bos ic satirlar atilir.</summary>
    public static string WriteSrt(IEnumerable<TextLayer> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var doc = new StringBuilder();
        var index = 0;
        foreach (var t in texts.OrderBy(t => t.Start))
        {
            var lines = t.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(l => l.Trim().Length > 0).ToArray();
            if (lines.Length == 0) continue;
            if (index > 0) doc.Append("\r\n");
            index++;
            doc.Append(index.ToString(CultureInfo.InvariantCulture)).Append("\r\n")
                .Append(SrtTime(t.Start)).Append(" --> ").Append(SrtTime(t.End)).Append("\r\n");
            foreach (var line in lines) doc.Append(line).Append("\r\n");
        }

        return doc.ToString();
    }

    /// <summary>Dosyaya yazilacak baytlar: SRT BOM'suz UTF-8, ASS <see cref="AssWriter"/> belgesi BOM'lu.</summary>
    public static byte[] Bytes(SubtitleFormat format, IReadOnlyList<TextLayer> texts, int width, int height) => format switch
    {
        SubtitleFormat.Srt => new UTF8Encoding(false).GetBytes(WriteSrt(texts)),
        SubtitleFormat.Ass => AssWriter.Bytes(AssWriter.Write(texts, width, height)),
        _ => throw new ArgumentException("Bu bicim yazilmaz", nameof(format))
    };

    public static string SrtTime(long ticks)
    {
        var ms = (Math.Max(0, ticks) + AssWriter.TicksPerMillisecond / 2) / AssWriter.TicksPerMillisecond;
        return string.Create(CultureInfo.InvariantCulture, $"{ms / 3600000:00}:{ms / 60000 % 60:00}:{ms / 1000 % 60:00},{ms % 1000:000}");
    }

    private static List<SubtitleCue> ReadTimed(string text, bool vtt)
    {
        var cues = new List<SubtitleCue>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = TimingLine().Match(lines[i]);
            if (!match.Success) continue;
            var start = Ticks(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
            var end = Ticks(match.Groups[5].Value, match.Groups[6].Value, match.Groups[7].Value, match.Groups[8].Value);
            var body = new List<string>();
            while (i + 1 < lines.Length && lines[i + 1].Trim().Length > 0 && !lines[i + 1].Contains("-->", StringComparison.Ordinal))
                body.Add(lines[++i]);
            if (end <= start) continue;
            var cleaned = string.Join("\n", body.Select(l => CleanTimed(l, vtt)));
            cues.Add(new SubtitleCue(start, end, cleaned));
        }

        return cues;
    }

    private static string CleanTimed(string line, bool vtt)
    {
        var text = MarkupTag().Replace(line, string.Empty);
        text = vtt ? TimestampTag().Replace(text, string.Empty) : AssBlock().Replace(text, string.Empty);
        if (vtt)
            text = text.Replace("&lt;", "<", StringComparison.Ordinal).Replace("&gt;", ">", StringComparison.Ordinal)
                .Replace("&nbsp;", "\u00A0", StringComparison.Ordinal).Replace("&lrm;", "\u200E", StringComparison.Ordinal)
                .Replace("&rlm;", "\u200F", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
        return text.TrimEnd();
    }

    private static long Ticks(string hours, string minutes, string seconds, string fraction)
    {
        var h = hours.Length == 0 ? 0 : long.Parse(hours, CultureInfo.InvariantCulture);
        var m = long.Parse(minutes, CultureInfo.InvariantCulture);
        var s = long.Parse(seconds, CultureInfo.InvariantCulture);
        var ms = long.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return (((h * 60 + m) * 60 + s) * 1000 + ms) * AssWriter.TicksPerMillisecond;
    }

    private static List<SubtitleCue> ReadAss(string text)
    {
        var cues = new List<SubtitleCue>();
        var fields = DefaultAssFields;
        string? lastStyle = null;
        var inEvents = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inEvents = line.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inEvents) continue;
            if (line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            {
                fields = line["Format:".Length..].Split(',').Select(f => f.Trim().ToLowerInvariant()).ToArray();
                continue;
            }

            if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase)) continue;
            var values = raw.TrimStart()["Dialogue:".Length..].TrimStart().Split(',', fields.Length);
            if (values.Length != fields.Length) continue;
            var startAt = Array.IndexOf(fields, "start");
            var endAt = Array.IndexOf(fields, "end");
            var textAt = Array.IndexOf(fields, "text");
            if (startAt < 0 || endAt < 0 || textAt != fields.Length - 1) continue;
            if (AssTime(values[startAt]) is not { } start || AssTime(values[endAt]) is not { } end || end <= start) continue;
            var styleAt = Array.IndexOf(fields, "style");
            var style = styleAt < 0 ? string.Empty : values[styleAt].Trim();
            var body = AssText(values[textAt].TrimEnd('\r'));

            if (cues.Count > 0 && lastStyle == style && cues[^1].Text == body && cues[^1].End == start)
                cues[^1] = cues[^1] with { End = end };
            else
                cues.Add(new SubtitleCue(start, end, body));
            lastStyle = style;
        }

        return cues;
    }

    private static long? AssTime(string value)
    {
        var match = AssTimestamp().Match(value.Trim());
        if (!match.Success) return null;
        var cs = match.Groups[4].Value.PadRight(2, '0')[..2];
        return Ticks(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, cs + "0");
    }

    private static string AssText(string value)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '{')
            {
                var close = value.IndexOf('}', i + 1);
                if (close < 0) break;
                i = close;
                continue;
            }

            if (ch == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                switch (next)
                {
                    case 'N':
                    case 'n':
                        result.Append('\n');
                        i++;
                        continue;
                    case 'h':
                        result.Append('\u00A0');
                        i++;
                        continue;
                    case '{':
                    case '}':
                        result.Append(next);
                        i++;
                        continue;
                    case '\u2060':
                        result.Append('\\');
                        i++;
                        continue;
                }
            }

            result.Append(ch);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"^\s*(?:(\d+):)?(\d{1,2}):(\d{1,2})[,.](\d{1,3})\s*-->\s*(?:(\d+):)?(\d{1,2}):(\d{1,2})[,.](\d{1,3})")]
    private static partial Regex TimingLine();

    [GeneratedRegex(@"</?(?:b|i|u|s|c|v|lang|ruby|rt|font)(?:[.\s][^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex MarkupTag();

    [GeneratedRegex(@"<\d{1,2}:\d{2}(?::\d{2})?\.\d{3}>")]
    private static partial Regex TimestampTag();

    [GeneratedRegex(@"\{\\[^}]*\}")]
    private static partial Regex AssBlock();

    [GeneratedRegex(@"^(\d+):(\d{1,2}):(\d{1,2})\.(\d{1,2})$")]
    private static partial Regex AssTimestamp();
}
