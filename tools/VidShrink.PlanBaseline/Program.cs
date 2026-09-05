using System.Globalization;
using VidShrink.Core;

namespace VidShrink.PlanBaseline;

internal sealed class AllWorking : IEncoderAvailability
{
    private static readonly string[] Codecs =
    {
        "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc"
    };

    public bool HasEncoder(string name) => Codecs.Contains(name, StringComparer.OrdinalIgnoreCase);
    public bool WorksAsEncoder(string codec) => HasEncoder(codec);
    public EncoderProbeState EncoderState(string codec) => HasEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
}

internal static class Program
{
    private static readonly (int Width, int Height, double Fps, double DurationSeconds, double TargetMb)[] Grid =
    {
        (1920, 1080, 30, 120, 25.0),
        (1280, 720, 24, 300, 8.0),
        (3840, 2160, 60, 45, 50.0),
        (1920, 1080, 30, 600, 6.0),
        (1280, 720, 30, 30, 100.0)
    };

    private static MediaInfo Source(int width, int height, double fps, double durationSeconds) => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = durationSeconds,
        Width = width,
        Height = height,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static int Main()
    {
        var ci = CultureInfo.InvariantCulture;
        Console.WriteLine("# VidShrink.PlanBaseline");
        Console.WriteLine("# kaynak|hedefMB -> kodek|kip|videoK|crf|WxH@fps|sesK/kanal|preset");

        foreach (var (width, height, fps, durationSeconds, targetMb) in Grid)
        {
            var info = Source(width, height, fps, durationSeconds);
            var options = new PlanOptions { TargetMb = targetMb, Codec = CodecPreference.Auto };
            var plan = PlanCalculator.BuildDetailed(info, options, null, new AllWorking()).Plan;

            var crf = plan.Crf?.ToString(ci) ?? "-";
            var channels = plan.AudioChannels?.ToString(ci) ?? "kaynak";
            Console.WriteLine(string.Format(ci,
                "{0}x{1}@{2}|{3} -> {4}|{5}|{6}k|crf={7}|{8}x{9}@{10:0.###}|ses {11}k/{12}|preset {13}",
                width, height, fps, targetMb,
                plan.Codec, plan.Mode, plan.VideoBitrateK, crf,
                plan.Width, plan.Height, plan.Fps, plan.AudioBitrateK, channels, plan.Preset));
        }

        return 0;
    }
}
