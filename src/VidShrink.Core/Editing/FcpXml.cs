using System.Globalization;
using System.Text;
using System.Xml;

namespace VidShrink.Core.Editing;

/// <summary>
/// Kesilmis cizelgenin Final Cut Pro 7 XML'i (xmeml 4): Premiere Pro ve DaVinci Resolve'un ice aldigi tek dizi, bir
/// goruntu ve (sesliyse) bir ses izi. Parcalar kesmeyle yan yana dizilir (gecis yazilmaz); her sinir tick'ten kareye
/// ayri yuvarlanir, boylece bitisik parcalar arasinda bosluk kalmaz. NTSC hizinda kare suresi <c>timebase*1000/1001</c>
/// uzerinden sayilir. Hiz ve yon <c>timeremap</c> etkisiyle tasinir. Dosya ilk geciste tam, sonra kimligiyle anilir;
/// yolu <c>file://localhost/</c> adresidir. Hedef programda acildigi olculmedi.
/// </summary>
public static class FcpXml
{
    public static string Write(EditTimeline timeline, CutListContext context)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(context);
        if (!double.IsFinite(context.Fps) || context.Fps < 1 || context.Fps > 240)
            throw new ArgumentOutOfRangeException(nameof(context), context.Fps, "Gecersiz kare hizi");
        var timebase = (int)Math.Round(context.Fps);
        var ntsc = Math.Abs(context.Fps - timebase * 1000.0 / 1001.0) < 0.01;
        var frameRate = ntsc ? timebase * 1000.0 / 1001.0 : timebase;
        long Frames(long ticks) => (long)Math.Round(ticks * frameRate / EditTime.TicksPerSecond, MidpointRounding.AwayFromZero);

        var bounds = new List<(long Start, long End)>(timeline.Clips.Count);
        long at = 0;
        foreach (var clip in timeline.Clips)
        {
            bounds.Add((Frames(at), Frames(at + clip.TimelineLength)));
            at += clip.TimelineLength;
        }

        var title = context.Title ?? (context.SourcePaths.Count > 0 ? Path.GetFileNameWithoutExtension(CutList.FileName(context.SourcePaths[0])) : AppIdentity.Name);
        var output = new Utf8Writer();
        using (var xml = XmlWriter.Create(output, new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = Encoding.UTF8 }))
        {
            void Rate()
            {
                xml.WriteStartElement("rate");
                xml.WriteElementString("timebase", timebase.ToString(CultureInfo.InvariantCulture));
                xml.WriteElementString("ntsc", ntsc ? "TRUE" : "FALSE");
                xml.WriteEndElement();
            }

            void Number(string name, long value) => xml.WriteElementString(name, value.ToString(CultureInfo.InvariantCulture));

            var written = new HashSet<int>();

            void File(int source)
            {
                xml.WriteStartElement("file");
                xml.WriteAttributeString("id", "file-" + (source + 1).ToString(CultureInfo.InvariantCulture));
                if (written.Add(source))
                {
                    var path = source < context.SourcePaths.Count ? context.SourcePaths[source] : string.Empty;
                    xml.WriteElementString("name", CutList.FileName(path));
                    xml.WriteElementString("pathurl", PathUrl(path));
                    Rate();
                    if (timeline.DurationOf(source) is { } duration) Number("duration", Frames(duration));
                    xml.WriteStartElement("media");
                    xml.WriteStartElement("video");
                    Characteristics();
                    xml.WriteEndElement();
                    if (context.HasAudio)
                    {
                        xml.WriteStartElement("audio");
                        Number("channelcount", 2);
                        xml.WriteEndElement();
                    }

                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            void Characteristics()
            {
                xml.WriteStartElement("samplecharacteristics");
                Rate();
                if (context.Width > 0) Number("width", context.Width);
                if (context.Height > 0) Number("height", context.Height);
                xml.WriteElementString("pixelaspectratio", "square");
                xml.WriteEndElement();
            }

            void Speed(EditClip clip)
            {
                if (clip.Speed == 1m && !clip.Reversed) return;
                xml.WriteStartElement("filter");
                xml.WriteStartElement("effect");
                xml.WriteElementString("name", "Time Remap");
                xml.WriteElementString("effectid", "timeremap");
                xml.WriteElementString("effectcategory", "motion");
                xml.WriteElementString("effecttype", "motion");
                xml.WriteElementString("mediatype", "video");
                Parameter("variablespeed", "0");
                Parameter("speed", (clip.Speed * 100).ToString("0.##", CultureInfo.InvariantCulture));
                Parameter("reverse", clip.Reversed ? "TRUE" : "FALSE");
                Parameter("frameblending", "FALSE");
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            void Parameter(string id, string value)
            {
                xml.WriteStartElement("parameter");
                xml.WriteElementString("parameterid", id);
                xml.WriteElementString("name", id);
                xml.WriteElementString("value", value);
                xml.WriteEndElement();
            }

            void Track(string kind)
            {
                xml.WriteStartElement("track");
                for (var i = 0; i < timeline.Clips.Count; i++)
                {
                    var clip = timeline.Clips[i];
                    xml.WriteStartElement("clipitem");
                    xml.WriteAttributeString("id", string.Create(CultureInfo.InvariantCulture, $"clipitem-{kind}-{i + 1}"));
                    xml.WriteElementString("name", clip.Label ?? CutList.FileName(clip.Source < context.SourcePaths.Count ? context.SourcePaths[clip.Source] : string.Empty));
                    xml.WriteElementString("enabled", "TRUE");
                    if (timeline.DurationOf(clip.Source) is { } duration) Number("duration", Frames(duration));
                    Rate();
                    Number("start", bounds[i].Start);
                    Number("end", bounds[i].End);
                    Number("in", Frames(clip.SourceStart));
                    Number("out", Frames(clip.SourceEnd));
                    File(clip.Source);
                    xml.WriteStartElement("sourcetrack");
                    xml.WriteElementString("mediatype", kind);
                    Number("trackindex", 1);
                    xml.WriteEndElement();
                    Speed(clip);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            xml.WriteStartDocument();
            xml.WriteStartElement("xmeml");
            xml.WriteAttributeString("version", "4");
            xml.WriteStartElement("sequence");
            xml.WriteAttributeString("id", "sequence-1");
            xml.WriteElementString("name", title);
            Number("duration", bounds.Count == 0 ? 0 : bounds[^1].End);
            Rate();
            xml.WriteStartElement("media");
            xml.WriteStartElement("video");
            xml.WriteStartElement("format");
            Characteristics();
            xml.WriteEndElement();
            Track("video");
            xml.WriteEndElement();
            if (context.HasAudio)
            {
                xml.WriteStartElement("audio");
                Track("audio");
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return output.ToString();
    }

    internal static string PathUrl(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        var escaped = string.Join('/', parts.Select(p => p.Length == 2 && char.IsAsciiLetter(p[0]) && p[1] == ':' ? p : Uri.EscapeDataString(p)));
        return "file://localhost" + (escaped.StartsWith('/') ? string.Empty : "/") + escaped;
    }

    private sealed class Utf8Writer : StringWriter
    {
        public Utf8Writer() : base(CultureInfo.InvariantCulture)
        {
        }

        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
