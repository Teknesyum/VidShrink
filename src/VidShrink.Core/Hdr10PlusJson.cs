using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core;

/// <summary>
/// ffprobe kare dokumunun ozeti. <see cref="Json"/> x265'in <c>dhdr10-info</c> girdisi;
/// <c>null</c> ise donusum reddedildi (bir karede HDR10+ yok, pencere sayisi birden farkli ya
/// da tanimadigimiz bir alan var) ve kopru o is icin veri tasimaz.
/// </summary>
public sealed record Hdr10PlusExtraction(string? Json, int Frames, int Hdr10PlusFrames);

/// <summary>
/// ffprobe'un kare basina SMPTE 2094-40 yan verisini x265'in <c>dhdr10-info</c> JSON'una
/// cevirir. Girdi <see cref="FfprobeArguments"/> ile uretilen <c>json=compact=1</c> metnidir:
/// orada her kare <c>{ "key_frame"</c> ile baslayan satirda acilir ve her yan veri nesnesi tek
/// satirdir; toplayici satir satir okudugu icin uzun bir kaynagin dokumu bellege sigmak zorunda
/// degil. Kare sirasi ffprobe'un cikis sirasidir, yani gosterim sirasi; x265 JSON'daki
/// girdileri de gosterim sirasiyla karelere dagitiyor (<c>docs/olcumler/hdr10plus-tasima.md</c>).
/// </summary>
public static class Hdr10PlusJson
{
    public const string SideDataType = "HDR Dynamic Metadata SMPTE2094-40 (HDR10+)";

    /// <summary>
    /// Kare basina yalniz zaman damgasi ve yan veri; <c>key_frame</c> her karede yazildigi icin
    /// kare acilisinin isaretidir.
    /// </summary>
    public static IReadOnlyList<string> FfprobeArguments(string input) => new[]
    {
        "-v", "error", "-select_streams", "v:0", "-show_frames",
        "-show_entries", "frame=key_frame,pts:frame_side_data", "-of", "json=compact=1", input
    };

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "side_data_type", "application version", "num_windows", "targeted_system_display_maximum_luminance",
        "maxscl", "average_maxrgb", "num_distribution_maxrgb_percentiles", "distribution_maxrgb_percentage",
        "distribution_maxrgb_percentile", "fraction_bright_pixels", "knee_point_x", "knee_point_y",
        "num_bezier_curve_anchors", "bezier_curve_anchors"
    };

    public static Hdr10PlusExtraction FromFfprobe(string ffprobeJson)
    {
        var collector = new Collector();
        using var reader = new StringReader(ffprobeJson);
        while (reader.ReadLine() is { } line) collector.AddLine(line);
        return collector.Result();
    }

    /// <summary>
    /// ffmpeg <c>-x265-params</c> degerini <c>:</c> ile boler ve ters bolu ile kacisi tanir; ham
    /// Windows yolu (<c>C:\...</c>) "Error setting option x265-params" ile duser. Ters bolu
    /// once duz boluye cevrilir (Windows iki ayraci da tanir), sonra <c>:</c>, <c>=</c> ve
    /// <c>'</c> kacirilir.
    /// </summary>
    public static string EscapeForX265Params(string path)
    {
        var sb = new StringBuilder(path.Length + 8);
        foreach (var c in path)
        {
            if (c == '\\') sb.Append('/');
            else if (c is ':' or '=' or '\'') sb.Append('\\').Append(c);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>ffprobe dokumunu satir satir okuyan toplayici; bkz. <see cref="Hdr10PlusJson"/>.</summary>
    public sealed class Collector
    {
        private readonly StringBuilder _scenes = new();
        private int _frames;
        private int _hdr10PlusFrames;
        private bool _currentHasHdr10Plus = true;
        private bool _rejected;
        private bool _bezier;

        /// <summary>Simdiye kadar acilan kare sayisi; cozme gecisinin ilerlemesi bundan okunur.</summary>
        public int Frames => _frames;

        public void AddLine(string line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("{ \"key_frame\"", StringComparison.Ordinal))
            {
                _frames++;
                _currentHasHdr10Plus = false;
                return;
            }
            if (_frames == 0 || !trimmed.StartsWith("{ \"side_data_type\"", StringComparison.Ordinal) || !trimmed.Contains(SideDataType, StringComparison.Ordinal))
                return;

            var body = trimmed.TrimEnd().TrimEnd(',');
            if (_currentHasHdr10Plus)
            {
                _rejected = true;
                return;
            }
            _currentHasHdr10Plus = true;
            _hdr10PlusFrames++;
            if (_rejected) return;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!AppendScene(doc.RootElement, _hdr10PlusFrames - 1)) _rejected = true;
            }
            catch (JsonException)
            {
                _rejected = true;
            }
        }

        public Hdr10PlusExtraction Result()
        {
            var complete = !_rejected && _frames > 0 && _hdr10PlusFrames == _frames;
            string? json = null;
            if (complete)
            {
                json = new StringBuilder()
                    .Append("{\"JSONInfo\":{\"HDR10plusProfile\":\"").Append(_bezier ? "B" : "A").Append("\",\"Version\":\"1.0\"},\"SceneInfo\":[")
                    .Append(_scenes)
                    .Append("]}")
                    .ToString();
            }
            return new Hdr10PlusExtraction(json, _frames, _hdr10PlusFrames);
        }

        private bool AppendScene(JsonElement sideData, int index)
        {
            int? windows = null, luminance = null, average = null, kneeX = null, kneeY = null, bezierCount = null, percentileCount = null;
            var maxScl = new List<int>();
            var distributionIndex = new List<int>();
            var distributionValues = new List<int>();
            var anchors = new List<int>();

            foreach (var property in sideData.EnumerateObject())
            {
                if (!KnownKeys.Contains(property.Name)) return false;
                switch (property.Name)
                {
                    case "num_windows": windows = Integer(property.Value); break;
                    case "targeted_system_display_maximum_luminance": luminance = Scaled(property.Value, 1); break;
                    case "maxscl": if (Scaled(property.Value, 100000) is int m) maxScl.Add(m); else return false; break;
                    case "average_maxrgb": average = Scaled(property.Value, 100000); break;
                    case "num_distribution_maxrgb_percentiles": percentileCount = Integer(property.Value); break;
                    case "distribution_maxrgb_percentage": if (Integer(property.Value) is int p) distributionIndex.Add(p); else return false; break;
                    case "distribution_maxrgb_percentile": if (Scaled(property.Value, 100000) is int v) distributionValues.Add(v); else return false; break;
                    case "fraction_bright_pixels": if (Scaled(property.Value, 1000) is not 0) return false; break;
                    case "knee_point_x": kneeX = Scaled(property.Value, 4095); break;
                    case "knee_point_y": kneeY = Scaled(property.Value, 4095); break;
                    case "num_bezier_curve_anchors": bezierCount = Integer(property.Value); break;
                    case "bezier_curve_anchors": if (Scaled(property.Value, 1023) is int a) anchors.Add(a); else return false; break;
                }
            }

            if (windows != 1 || luminance is null || average is null || maxScl.Count != 3) return false;
            if (percentileCount is not int percentiles || distributionIndex.Count != percentiles || distributionValues.Count != percentiles) return false;
            var hasBezier = bezierCount is > 0;
            if (hasBezier && (kneeX is null || kneeY is null || anchors.Count != bezierCount)) return false;

            var sb = _scenes;
            if (index > 0) sb.Append(',');
            sb.Append('{');
            if (hasBezier)
            {
                _bezier = true;
                sb.Append("\"BezierCurveData\":{\"Anchors\":[").Append(Join(anchors))
                  .Append("],\"KneePointX\":").Append(Text(kneeX!.Value))
                  .Append(",\"KneePointY\":").Append(Text(kneeY!.Value)).Append("},");
            }
            sb.Append("\"LuminanceParameters\":{\"AverageRGB\":").Append(Text(average.Value))
              .Append(",\"LuminanceDistributions\":{\"DistributionIndex\":[").Append(Join(distributionIndex))
              .Append("],\"DistributionValues\":[").Append(Join(distributionValues))
              .Append("]},\"MaxScl\":[").Append(Join(maxScl))
              .Append("]},\"NumberOfWindows\":1,\"TargetedSystemDisplayMaximumLuminance\":").Append(Text(luminance.Value))
              .Append(",\"SceneFrameIndex\":").Append(Text(index))
              .Append(",\"SceneId\":0,\"SequenceFrameIndex\":").Append(Text(index))
              .Append('}');
            return true;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Join(List<int> values) => string.Join(",", values.Select(Text));

        private static int? Integer(JsonElement value)
            => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : null;

        /// <summary>
        /// ffprobe kesri <c>pay/payda</c> yazar; x265 JSON'u alanin kendi paydasindaki payi
        /// bekler. Paydalar tutarsa pay oldugu gibi gecer, tutmazsa en yakin tamsayiya olceklenir.
        /// </summary>
        private static int? Scaled(JsonElement value, long denominator)
        {
            if (value.ValueKind == JsonValueKind.Number) return value.TryGetInt32(out var whole) ? checked((int)(whole * denominator)) : null;
            if (value.ValueKind != JsonValueKind.String) return null;
            var text = value.GetString()!;
            var slash = text.IndexOf('/');
            if (slash <= 0) return null;
            if (!long.TryParse(text.AsSpan(0, slash), NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)) return null;
            if (!long.TryParse(text.AsSpan(slash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var den) || den <= 0) return null;
            if (den == denominator) return (int)num;
            return (int)Math.Round((double)num * denominator / den, MidpointRounding.AwayFromZero);
        }
    }
}
