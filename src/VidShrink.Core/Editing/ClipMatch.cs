using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Core.Editing;

public sealed record ColorStats(double MeanR, double MeanG, double MeanB, double StdR, double StdG, double StdB, int Frames)
{
    public double Mean(int channel) => channel switch { 0 => MeanR, 1 => MeanG, _ => MeanB };

    public double Std(int channel) => channel switch { 0 => StdR, 1 => StdG, _ => StdB };
}

public sealed partial record ClipEffects
{
    public const double MinMatchGain = 0.25;
    public const double MaxMatchGain = 4;

    public double MatchGainR { get; init; } = 1;

    public double MatchGainG { get; init; } = 1;

    public double MatchGainB { get; init; } = 1;

    public double MatchLiftR { get; init; }

    public double MatchLiftG { get; init; }

    public double MatchLiftB { get; init; }

    public bool HasMatch => MatchGainR != 1 || MatchGainG != 1 || MatchGainB != 1 || MatchLiftR != 0 || MatchLiftG != 0 || MatchLiftB != 0;

    public ClipEffects WithoutMatch() => this with { MatchGainR = 1, MatchGainG = 1, MatchGainB = 1, MatchLiftR = 0, MatchLiftG = 0, MatchLiftB = 0 };

    private ClipEffects MatchNormalized()
    {
        static double Gain(double v) => double.IsFinite(v) ? Math.Round(Math.Clamp(v, MinMatchGain, MaxMatchGain), 4) : 1;
        static double Lift(double v) => double.IsFinite(v) ? Math.Round(Math.Clamp(v, -1, 1), 4) : 0;
        return this with
        {
            MatchGainR = Gain(MatchGainR),
            MatchGainG = Gain(MatchGainG),
            MatchGainB = Gain(MatchGainB),
            MatchLiftR = Lift(MatchLiftR),
            MatchLiftG = Lift(MatchLiftG),
            MatchLiftB = Lift(MatchLiftB),
        };
    }
}

public static partial class ClipMatch
{
    public const int SampleFrames = 8;
    public const int SampleWidth = 160;

    [GeneratedRegex(@"mean:\[(\d+(?:\.\d+)?) (\d+(?:\.\d+)?) (\d+(?:\.\d+)?)\] stdev:\[(\d+(?:\.\d+)?) (\d+(?:\.\d+)?) (\d+(?:\.\d+)?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex StatsLine();

    public static IReadOnlyList<string> ProbeArguments(string path, double start, double length, string colorChain)
    {
        ArgumentNullException.ThrowIfNull(path);
        var span = double.IsFinite(length) && length > 0 ? length : 1;
        var rate = Number(SampleFrames / span);
        var chain = "fps=" + rate + ",scale=" + SampleWidth.ToString(CultureInfo.InvariantCulture) + ":-2"
            + (string.IsNullOrEmpty(colorChain) ? string.Empty : "," + colorChain) + ",format=gbrp,showinfo";
        return new[]
        {
            "-hide_banner", "-nostdin", "-nostats", "-threads", "2",
            "-ss", Number(Math.Max(0, start)), "-t", Number(span), "-i", path,
            "-map", "0:v:0", "-vf", chain, "-frames:v", SampleFrames.ToString(CultureInfo.InvariantCulture),
            "-an", "-sn", "-dn", "-f", "null", "-",
        };
    }

    public static ColorStats? Parse(string standardError)
    {
        var sum = new double[3];
        var square = new double[3];
        var frames = 0;
        foreach (Match m in StatsLine().Matches(standardError ?? string.Empty))
        {
            for (var plane = 0; plane < 3; plane++)
            {
                var channel = plane switch { 0 => 1, 1 => 2, _ => 0 };
                var mean = double.Parse(m.Groups[1 + plane].Value, CultureInfo.InvariantCulture);
                var std = double.Parse(m.Groups[4 + plane].Value, CultureInfo.InvariantCulture);
                sum[channel] += mean;
                square[channel] += std * std + mean * mean;
            }

            frames++;
        }

        if (frames == 0) return null;
        var means = sum.Select(s => s / frames).ToArray();
        var stds = Enumerable.Range(0, 3).Select(c => Math.Sqrt(Math.Max(0, square[c] / frames - means[c] * means[c]))).ToArray();
        return new ColorStats(means[0], means[1], means[2], stds[0], stds[1], stds[2], frames);
    }

    public static ClipEffects Match(ClipEffects target, ColorStats reference, ColorStats source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(source);
        var gain = new double[3];
        var lift = new double[3];
        for (var c = 0; c < 3; c++)
        {
            gain[c] = Math.Clamp(source.Std(c) > 0.5 ? reference.Std(c) / source.Std(c) : 1, ClipEffects.MinMatchGain, ClipEffects.MaxMatchGain);
            lift[c] = (reference.Mean(c) - gain[c] * source.Mean(c)) / 255;
        }

        return (target with
        {
            MatchGainR = gain[0], MatchGainG = gain[1], MatchGainB = gain[2],
            MatchLiftR = lift[0], MatchLiftG = lift[1], MatchLiftB = lift[2],
        }).Normalized();
    }

    public static string Filter(ClipEffects effects)
    {
        if (!effects.HasMatch) return string.Empty;
        var names = new[] { "r", "g", "b" };
        var gains = new[] { effects.MatchGainR, effects.MatchGainG, effects.MatchGainB };
        var lifts = new[] { effects.MatchLiftR, effects.MatchLiftG, effects.MatchLiftB };
        var parts = new List<string>();
        for (var c = 0; c < 3; c++)
        {
            var (inMin, inMax, outMin, outMax) = Levels(gains[c], lifts[c]);
            parts.Add(names[c] + "imin=" + Number(inMin) + ":" + names[c] + "imax=" + Number(inMax)
                + ":" + names[c] + "omin=" + Number(outMin) + ":" + names[c] + "omax=" + Number(outMax));
        }

        return "colorlevels=" + string.Join(":", parts);
    }

    public static (double InMin, double InMax, double OutMin, double OutMax) Levels(double gain, double lift)
    {
        var low = lift;
        var high = gain + lift;
        var inMin = 0.0;
        var inMax = 1.0;
        if (low < 0)
        {
            inMin = Math.Min(1, -lift / gain);
            low = 0;
        }

        if (high > 1)
        {
            inMax = Math.Max(inMin, (1 - lift) / gain);
            high = 1;
        }

        return (Round(inMin), Round(inMax), Round(Math.Clamp(low, 0, 1)), Round(Math.Clamp(high, 0, 1)));
    }

    internal static void Write(Utf8JsonWriter writer, ClipEffects effects)
    {
        if (!effects.HasMatch) return;
        writer.WriteStartObject("match");
        writer.WriteNumber("gainR", effects.MatchGainR);
        writer.WriteNumber("gainG", effects.MatchGainG);
        writer.WriteNumber("gainB", effects.MatchGainB);
        writer.WriteNumber("liftR", effects.MatchLiftR);
        writer.WriteNumber("liftG", effects.MatchLiftG);
        writer.WriteNumber("liftB", effects.MatchLiftB);
        writer.WriteEndObject();
    }

    internal static ClipEffects Read(JsonElement e, ClipEffects effects)
    {
        if (!e.TryGetProperty("match", out var match) || match.ValueKind != JsonValueKind.Object) return effects;
        return effects with
        {
            MatchGainR = Field(match, "gainR", 1),
            MatchGainG = Field(match, "gainG", 1),
            MatchGainB = Field(match, "gainB", 1),
            MatchLiftR = Field(match, "liftR", 0),
            MatchLiftG = Field(match, "liftG", 0),
            MatchLiftB = Field(match, "liftB", 0),
        };
    }

    private static double Field(JsonElement match, string name, double fallback)
        => match.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : fallback;

    private static double Round(double value) => Math.Round(value, 4);

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
