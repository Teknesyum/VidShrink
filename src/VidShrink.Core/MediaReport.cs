using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Döküm satırı. <paramref name="Key"/> etiketin dil anahtarının son parçası; <paramref name="ValueIsKey"/>
/// açıksa değer de çevrilecek bir anahtardır (evet/hayır), değilse olduğu gibi yazılır.
/// </summary>
public sealed record ReportField(string Key, string Value, bool ValueIsKey = false);

/// <summary>
/// Dökümün bir bölümü: genel bilgi, bir iz ya da bir bölüm işareti. <paramref name="Number"/> izde
/// ffprobe'un akış indeksi, bölümde kullanıcının gördüğü numara, genel bölümde 0.
/// </summary>
public sealed record ReportSection(string Key, int Number, IReadOnlyList<ReportField> Fields);

/// <summary>
/// ARC-01: ffprobe'un zaten okuduğu <see cref="MediaInfo"/>'dan tam döküm. Her iz ve her bölüm
/// kendi bölümünü alır; yeni bir yoklama yapılmaz. Metin biçimi etiketleri dışarıdan alır, böylece
/// arayüz dili döküme girer ama sayıların yazımı ölçülebilir kalır.
/// </summary>
public static class MediaReport
{
    /// <summary>
    /// Dökümün üretebildiği her alan adı ve evet/hayır değeri. Etiket anahtarı bu adlardan kurulur;
    /// ölü çeviri sayımı anahtarı derlemede göremediği için kümeyi buradan okur.
    /// </summary>
    internal static readonly IReadOnlyList<string> FieldKeys = new[]
    {
        "file", "size", "duration", "format", "bitrate", "streams", "chapters", "titles",
        "codec", "profile", "tag", "resolution", "par", "fps", "pixel-format", "bit-depth", "color",
        "hdr", "dolby-vision", "hdr10plus", "field-order", "channels", "sample-rate", "language",
        "title", "default", "forced", "cover", "start", "end", "yes", "no",
    };

    internal static IEnumerable<string> SectionKeys
        => new[] { "general", "chapter" }.Concat(Enum.GetValues<StreamKind>().Select(k => k.ToString().ToLowerInvariant()));

    public static IReadOnlyList<ReportSection> Build(MediaInfo info, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(culture);

        var sections = new List<ReportSection> { General(info, culture) };
        var main = info.Streams.FirstOrDefault(s => s.Kind == StreamKind.Video && !s.IsAttachedPicture);
        foreach (var stream in info.Streams)
            sections.Add(Stream(info, stream, ReferenceEquals(stream, main), culture));
        foreach (var chapter in info.Chapters)
            sections.Add(new ReportSection("chapter", chapter.Number, new[]
            {
                new ReportField("start", Saat.Ffmpeg(TimeSpan.FromSeconds(chapter.StartSeconds))),
                new ReportField("end", Saat.Ffmpeg(TimeSpan.FromSeconds(chapter.EndSeconds))),
                new ReportField("title", chapter.Title ?? "-"),
            }));
        return sections;
    }

    /// <summary>
    /// Düz metin: her bölüm başlığıyla, alanlar altında girintili <c>etiket: değer</c> satırları,
    /// bölümler arasında boş satır.
    /// </summary>
    public static string Text(IReadOnlyList<ReportSection> sections, Func<ReportSection, string> heading, Func<string, string> label)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var text = new StringBuilder();
        foreach (var section in sections)
        {
            if (text.Length > 0) text.AppendLine();
            text.AppendLine(heading(section));
            foreach (var field in section.Fields)
                text.Append("  ").Append(label(field.Key)).Append(": ").AppendLine(field.ValueIsKey ? label(field.Value) : field.Value);
        }
        return text.ToString();
    }

    private static ReportSection General(MediaInfo info, CultureInfo culture)
    {
        var fields = new List<ReportField>
        {
            new("file", info.FilePath),
            new("size", Bytes(info.FileSizeBytes, culture)),
            new("duration", Saat.Ffmpeg(TimeSpan.FromSeconds(info.DurationSeconds))),
        };
        if (!string.IsNullOrEmpty(info.FormatName)) fields.Add(new("format", info.FormatName));
        if (info.TotalBitrateBps > 0) fields.Add(new("bitrate", Kbps(info.TotalBitrateBps, culture)));
        fields.Add(new("streams", info.Streams.Count.ToString(culture)));
        fields.Add(new("chapters", info.ChapterCount.ToString(culture)));
        if (info.HasMultipleTitles) fields.Add(new("titles", info.Titles.Count.ToString(culture)));
        return new ReportSection("general", 0, fields);
    }

    private static ReportSection Stream(MediaInfo info, SourceStream stream, bool main, CultureInfo culture)
    {
        var fields = new List<ReportField> { new("codec", stream.Codec) };
        if (main)
        {
            if (!string.IsNullOrEmpty(info.VideoProfile)) fields.Add(new("profile", info.VideoProfile));
            if (!string.IsNullOrEmpty(info.VideoCodecTag)) fields.Add(new("tag", info.VideoCodecTag));
            fields.Add(new("resolution", info.Width.ToString(CultureInfo.InvariantCulture) + "x" + info.Height.ToString(CultureInfo.InvariantCulture)));
            if (info.IsAnamorphic) fields.Add(new("par", info.ParNum.ToString(CultureInfo.InvariantCulture) + ":" + info.ParDen.ToString(CultureInfo.InvariantCulture)));
            fields.Add(new("fps", info.Fps.ToString("0.###", culture)));
            if (!string.IsNullOrEmpty(info.PixelFormat)) fields.Add(new("pixel-format", info.PixelFormat));
            fields.Add(new("bit-depth", info.BitDepth.ToString(culture)));
            var color = new[] { info.ColorPrimaries, info.ColorTransfer, info.ColorSpace, info.ColorRange }.Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (color.Count > 0) fields.Add(new("color", string.Join(" / ", color)));
            fields.Add(new("hdr", info.IsHdr ? "yes" : "no", true));
            if (info.HasDolbyVision) fields.Add(new("dolby-vision", info.DolbyVisionProfile!.Value.ToString(CultureInfo.InvariantCulture)));
            if (info.HasHdr10Plus) fields.Add(new("hdr10plus", "yes", true));
            if (info.IsInterlaced) fields.Add(new("field-order", info.FieldOrder ?? "-"));
        }

        if (stream.Kind == StreamKind.Audio)
        {
            if (stream.Channels > 0) fields.Add(new("channels", stream.Channels.ToString(culture)));
            if (stream.SampleRate > 0) fields.Add(new("sample-rate", stream.SampleRate.ToString(culture) + " Hz"));
        }

        if (!string.IsNullOrEmpty(stream.Language)) fields.Add(new("language", stream.Language));
        if (!string.IsNullOrEmpty(stream.Title)) fields.Add(new("title", stream.Title));
        if (stream.BitrateBps > 0) fields.Add(new("bitrate", Kbps(stream.BitrateBps, culture)));
        if (stream.Bytes > 0) fields.Add(new("size", Bytes(stream.Bytes, culture)));
        fields.Add(new("default", stream.IsDefault ? "yes" : "no", true));
        if (stream.IsForced) fields.Add(new("forced", "yes", true));
        if (stream.IsAttachedPicture) fields.Add(new("cover", "yes", true));
        return new ReportSection(stream.Kind.ToString().ToLowerInvariant(), stream.Index, fields);
    }

    private static string Kbps(long bps, CultureInfo culture)
        => (bps / 1000d).ToString("N0", culture) + " kb/s";

    private static string Bytes(long bytes, CultureInfo culture)
        => bytes.ToString("N0", culture) + " B (" + (bytes / 1024d / 1024d).ToString("0.##", culture) + " MiB)";
}
