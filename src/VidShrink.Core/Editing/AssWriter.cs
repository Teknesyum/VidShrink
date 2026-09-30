using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

/// <summary>
/// Metin katmanlarindan tek <c>.ass</c> belgesi. Disa aktarma bu metni ffmpeg <c>ass=</c>
/// suzgecine, onizleme libmpv <c>sub-add</c>'e verir. PlayRes kaynak boyutudur; konum
/// kesirden piksele, boyut kare yuksekliginin yuzdesinden piksele cevrilir. Zaman damgasi
/// santisaniyedir ve en yakina yuvarlanir (yarim yukari). Surec acmaz, dosya yazmaz.
/// </summary>
public static class AssWriter
{
    public const long TicksPerCentisecond = EditTime.TicksPerSecond / 100;

    public const long TicksPerMillisecond = EditTime.TicksPerSecond / 1000;

    public static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Disa aktarma belgesi: olay zamanlari cizelge zamanidir.</summary>
    public static string Write(IReadOnlyList<TextLayer> texts, int width, int height)
        => Document(texts, width, height, preview: null);

    /// <summary>
    /// Onizleme belgesi: olay zamanlari EDL zamanidir. Butun parcalar 1x ve ileriyse metin
    /// <see cref="Write"/> ile birebir aynidir; degilse satirlar parca sinirinda bolunur ve
    /// hizli/yavas/geri parcaya gore esnetilir.
    /// </summary>
    public static string WritePreview(IReadOnlyList<TextLayer> texts, int width, int height, EdlPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var identity = preview.Parts.All(p => p.Clip.Speed == 1m && !p.Clip.Reversed);
        return Document(texts, width, height, identity ? null : preview);
    }

    public static byte[] Bytes(string document) => FileEncoding.GetPreamble().Concat(FileEncoding.GetBytes(document)).ToArray();

    /// <summary><c>H:MM:SS.cc</c>; tick santisaniyeye en yakina yuvarlanir, yarim yukari gider.</summary>
    public static string Timestamp(long ticks)
    {
        if (ticks < 0) ticks = 0;
        var cs = (ticks + TicksPerCentisecond / 2) / TicksPerCentisecond;
        var h = cs / 360000;
        var m = cs / 6000 % 60;
        var s = cs / 100 % 60;
        var c = cs % 100;
        return string.Create(CultureInfo.InvariantCulture, $"{h}:{m:00}:{s:00}.{c:00}");
    }

    /// <summary>
    /// Kullanici metnini olay metnine cevirir: <c>\</c> ardina gorunmez kelime birlestirici
    /// alir ki <c>\N</c>, <c>\h</c> ya da etiket olmasin; <c>{</c> ve <c>}</c> <c>\{</c> ve
    /// <c>\}</c> olur; her satir sonu <c>\N</c> olur.
    /// </summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '\\':
                    result.Append('\\').Append('\u2060');
                    break;
                case '{':
                    result.Append("\\{");
                    break;
                case '}':
                    result.Append("\\}");
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    result.Append("\\N");
                    break;
                case '\n':
                    result.Append("\\N");
                    break;
                default:
                    result.Append(ch);
                    break;
            }
        }

        return result.ToString();
    }

    /// <summary><c>&amp;H00BBGGRR</c>; ASS rengi mavi-yesil-kirmizi sirasindadir.</summary>
    public static string Color(uint rgb)
    {
        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return string.Create(CultureInfo.InvariantCulture, $"&H00{b:X2}{g:X2}{r:X2}");
    }

    public static int FontPixels(TextLayer text, int height)
        => Math.Max(1, (int)Math.Round(text.Size / 100 * height, MidpointRounding.AwayFromZero));

    /// <summary>Metinde guclu sagdan sola karakter var mi; varsa stil taban yonu otomatige birakilir.</summary>
    public static bool HasRightToLeft(string text)
        => text.Any(c => c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF');

    private static string Document(IReadOnlyList<TextLayer> texts, int width, int height, EdlPreview? preview)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Kare boyutu pozitif olmalidir");

        var doc = new StringBuilder();
        doc.Append("[Script Info]\n")
            .Append("ScriptType: v4.00+\n")
            .Append("WrapStyle: 0\n")
            .Append("ScaledBorderAndShadow: yes\n")
            .Append("YCbCr Matrix: None\n")
            .Append("PlayResX: ").Append(width.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("PlayResY: ").Append(height.ToString(CultureInfo.InvariantCulture)).Append("\n\n");

        doc.Append("[V4+ Styles]\n")
            .Append("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n");
        for (var i = 0; i < texts.Count; i++)
        {
            var t = texts[i];
            var px = FontPixels(t, height);
            var outline = Math.Max(1, (int)Math.Round(px / 16.0, MidpointRounding.AwayFromZero));
            doc.Append("Style: T").Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(t.FontName.Replace(",", string.Empty, StringComparison.Ordinal).Trim()).Append(',')
                .Append(px.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Color(t.Color)).Append(',').Append(Color(t.Color)).Append(",&H00000000,&H00000000,")
                .Append(t.Bold ? "-1" : "0").Append(',').Append(t.Italic ? "-1" : "0")
                .Append(",0,0,100,100,0,0,1,").Append(outline.ToString(CultureInfo.InvariantCulture))
                .Append(",0,5,0,0,0,").Append(HasRightToLeft(t.Text) ? "-1" : "1").Append('\n');
        }

        doc.Append("\n[Events]\n")
            .Append("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");
        for (var i = 0; i < texts.Count; i++)
        {
            var t = texts[i];
            foreach (var line in Lines(t, preview))
                doc.Append(Dialogue(i, t, line, width, height));
        }

        return doc.ToString();
    }

    private readonly record struct Line(long Start, long End, long From, long To, bool Reversed);

    private static IEnumerable<Line> Lines(TextLayer text, EdlPreview? preview)
    {
        var cuts = new List<long> { 0 };
        foreach (var k in text.Keyframes)
            if (k.Offset > 0 && k.Offset < text.Length) cuts.Add(k.Offset);
        cuts.Add(text.Length);

        for (var i = 0; i + 1 < cuts.Count; i++)
        {
            var from = cuts[i];
            var to = cuts[i + 1];
            if (preview is null)
            {
                yield return new Line(text.Start + from, text.Start + to, from, to, false);
                continue;
            }

            foreach (var part in preview.Parts)
            {
                var a = Math.Max(text.Start + from, part.TimelineStart);
                var b = Math.Min(text.Start + to, part.TimelineEnd);
                if (b <= a) continue;
                var ea = EdlAt(part, a);
                var eb = EdlAt(part, b);
                var reversed = part.Clip.Reversed;
                yield return new Line(Math.Min(ea, eb), Math.Max(ea, eb), a - text.Start, b - text.Start, reversed);
            }
        }
    }

    private static long EdlAt(EdlPart part, long time)
    {
        var fraction = (double)(time - part.TimelineStart) / part.Clip.TimelineLength;
        var offset = (long)Math.Round(fraction * part.EdlLength, MidpointRounding.AwayFromZero);
        return part.Clip.Reversed ? part.EdlEnd - offset : part.EdlStart + offset;
    }

    private static string Dialogue(int index, TextLayer text, Line line, int width, int height)
    {
        var begin = line.Reversed ? line.To : line.From;
        var end = line.Reversed ? line.From : line.To;
        var (x0, y0) = text.PositionAt(begin);
        var (x1, y1) = text.PositionAt(end);
        var p0 = (Px(x0, width), Px(y0, height));
        var p1 = (Px(x1, width), Px(y1, height));

        var tags = new StringBuilder("{\\an5");
        if (p0 == p1) tags.Append("\\pos(").Append(p0.Item1).Append(',').Append(p0.Item2).Append(')');
        else tags.Append("\\move(").Append(p0.Item1).Append(',').Append(p0.Item2).Append(',').Append(p1.Item1).Append(',').Append(p1.Item2).Append(')');
        tags.Append(Fade(text, line));
        tags.Append('}');

        return "Dialogue: " + index.ToString(CultureInfo.InvariantCulture) + "," + Timestamp(line.Start) + "," + Timestamp(line.End)
               + ",T" + index.ToString(CultureInfo.InvariantCulture) + ",,0,0,0,," + tags + Escape(text.Text) + "\n";
    }

    private static string Fade(TextLayer text, Line line)
    {
        if (text.FadeIn == 0 && text.FadeOut == 0) return string.Empty;

        var span = line.To - line.From;
        var duration = line.End - line.Start;
        var scale = span == 0 ? 0 : (double)duration / span;
        var inEnd = text.FadeIn;
        var outStart = text.Length - text.FadeOut;

        long rise;
        long fall;
        int aBegin;
        int aEnd;
        if (!line.Reversed)
        {
            aBegin = Alpha(text, line.From);
            aEnd = Alpha(text, line.To);
            rise = Math.Clamp(inEnd - line.From, 0, span);
            fall = Math.Clamp(outStart - line.From, 0, span);
        }
        else
        {
            aBegin = Alpha(text, line.To);
            aEnd = Alpha(text, line.From);
            rise = Math.Clamp(line.To - outStart, 0, span);
            fall = Math.Clamp(line.To - inEnd, 0, span);
        }

        if (aBegin == 0 && aEnd == 0 && rise == 0 && fall == span) return string.Empty;

        var t2 = Ms((long)Math.Round(rise * scale));
        var t3 = Ms((long)Math.Round(fall * scale));
        var total = Ms(duration);
        if (rise == span && aEnd != 0)
            return string.Create(CultureInfo.InvariantCulture, $"\\fade({aBegin},{aEnd},{aEnd},0,{total},{total},{total})");
        if (fall == 0 && aBegin != 0)
            return string.Create(CultureInfo.InvariantCulture, $"\\fade({aBegin},{aBegin},{aEnd},0,0,0,{total})");

        var simpleHead = aBegin == 255 || (aBegin == 0 && rise == 0);
        var simpleTail = aEnd == 255 || (aEnd == 0 && fall == span);
        if (simpleHead && simpleTail)
        {
            var fin = aBegin == 255 ? t2 : 0;
            var fout = aEnd == 255 ? total - t3 : 0;
            return "\\fad(" + fin.ToString(CultureInfo.InvariantCulture) + "," + fout.ToString(CultureInfo.InvariantCulture) + ")";
        }

        return string.Create(CultureInfo.InvariantCulture, $"\\fade({aBegin},0,{aEnd},0,{t2},{t3},{total})");
    }

    private static int Alpha(TextLayer text, long offset)
    {
        if (text.FadeIn > 0 && offset < text.FadeIn)
            return (int)Math.Round(255.0 * (text.FadeIn - offset) / text.FadeIn, MidpointRounding.AwayFromZero);
        var outStart = text.Length - text.FadeOut;
        if (text.FadeOut > 0 && offset > outStart)
            return (int)Math.Round(255.0 * (offset - outStart) / text.FadeOut, MidpointRounding.AwayFromZero);
        return 0;
    }

    private static long Ms(long ticks) => (ticks + TicksPerMillisecond / 2) / TicksPerMillisecond;

    private static string Px(double fraction, int size)
        => ((int)Math.Round(fraction * size, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
}
