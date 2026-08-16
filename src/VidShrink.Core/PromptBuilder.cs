using System.Text;

namespace VidShrink.Core;

public static class PromptBuilder
{
    public static string Build(MediaInfo info, PlanOptions options, EncodePlan localPlan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a video encoding expert. Pick ffmpeg settings that shrink the source below to the target size with the least possible visual quality loss.");
        sb.AppendLine();
        sb.AppendLine("SOURCE");
        sb.AppendLine($"- duration: {info.DurationSeconds:0.##} s");
        sb.AppendLine($"- resolution: {info.Width}x{info.Height} @ {info.Fps:0.##} fps");
        sb.AppendLine($"- video codec: {info.VideoCodec}, pixel format: {info.PixelFormat ?? "unknown"}, HDR: {(info.IsHdr ? "yes" : "no")}");
        sb.AppendLine($"- current size: {info.FileSizeMb:0.0} MB, total bitrate: {info.TotalBitrateBps / 1000} kbps");
        sb.AppendLine(info.HasAudio
            ? $"- audio: {info.AudioCodec}, {info.AudioBitrateBps / 1000} kbps, {info.AudioChannels} channel(s)"
            : "- audio: none");
        sb.AppendLine();
        sb.AppendLine("GOAL");
        sb.AppendLine($"- target size: {options.TargetMb:0.##} MB (hard ceiling)");
        sb.AppendLine($"- intent: {options.Intent}");
        sb.AppendLine($"- codec preference: {options.Codec}");
        sb.AppendLine($"- resolution may be reduced: {(options.AllowResolutionDrop ? "yes" : "no")}");
        sb.AppendLine($"- frame rate may be reduced: {(options.AllowFpsDrop ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("BASELINE (what this program would do on its own — beat it or confirm it)");
        sb.AppendLine($"- {localPlan.Codec} / {localPlan.Mode} / {localPlan.VideoBitrateK}k video / {localPlan.AudioBitrateK}k audio / {localPlan.Width}x{localPlan.Height} @ {localPlan.Fps:0.##} / preset {localPlan.Preset}");
        sb.AppendLine();
        sb.AppendLine("ANSWER FORMAT");
        sb.AppendLine("Reply with a single JSON object and nothing else. No markdown fences, no commentary.");
        sb.AppendLine("""
        {
          "codec": "libx264 | libx265 | libsvtav1",
          "mode": "crf | 2pass",
          "videoBitrateK": 0,
          "crf": null,
          "audioCodec": "aac | libopus | null",
          "audioBitrateK": 0,
          "width": 0,
          "height": 0,
          "fps": 0,
          "preset": "slow",
          "extraArgs": [],
          "reason": "one or two sentences"
        }
        """);
        sb.AppendLine("Rules: width and height must be even numbers. If mode is \"2pass\", videoBitrateK must be set and crf must be null. If mode is \"crf\", crf must be set. videoBitrateK + audioBitrateK must not exceed the target size for the given duration.");
        return sb.ToString();
    }
}
