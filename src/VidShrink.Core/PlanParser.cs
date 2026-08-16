using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public sealed record PlanParseResult(EncodePlan? Plan, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool Ok => Plan is not null && Errors.Count == 0;
}

public static class PlanParser
{
    private static readonly string[] AllowedCodecs = { "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv" };
    private static readonly string[] AllowedAudioCodecs = { "aac", "libopus", "libmp3lame", "copy" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PlanParseResult Parse(string raw, MediaInfo info, PlanOptions options)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var json = ExtractJson(raw);
        if (json is null)
            return new PlanParseResult(null, new[] { "No JSON object found in the pasted text." }, warnings);

        EncodePlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<EncodePlan>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return new PlanParseResult(null, new[] { $"JSON could not be parsed: {ex.Message}" }, warnings);
        }

        if (plan is null)
            return new PlanParseResult(null, new[] { "JSON parsed to nothing." }, warnings);

        if (!AllowedCodecs.Contains(plan.Codec, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Unsupported codec: {plan.Codec}");

        if (!plan.Mode.Equals("crf", StringComparison.OrdinalIgnoreCase) && !plan.Mode.Equals("2pass", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Unsupported mode: {plan.Mode}");

        if (plan.AudioCodec is not null && !AllowedAudioCodecs.Contains(plan.AudioCodec, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Unsupported audio codec: {plan.AudioCodec}");

        if (plan.Width <= 0 || plan.Height <= 0)
        {
            plan.Width = info.Width;
            plan.Height = info.Height;
            warnings.Add("Resolution was missing, fell back to the source resolution.");
        }

        if (plan.Width % 2 != 0) { plan.Width++; warnings.Add("Width was odd, rounded up by one pixel."); }
        if (plan.Height % 2 != 0) { plan.Height++; warnings.Add("Height was odd, rounded up by one pixel."); }

        if (plan.Fps <= 0)
        {
            plan.Fps = info.Fps;
            warnings.Add("Frame rate was missing, fell back to the source frame rate.");
        }

        if (plan.Fps > info.Fps + 0.01)
        {
            plan.Fps = info.Fps;
            warnings.Add("Frame rate above the source was requested, clamped to the source.");
        }

        if (plan.ModeEnum == EncodeMode.TwoPass && plan.VideoBitrateK <= 0)
            errors.Add("2pass mode requires a positive videoBitrateK.");

        if (plan.ModeEnum == EncodeMode.Crf && plan.Crf is null)
            errors.Add("crf mode requires a crf value.");

        if (plan.Crf is { } crf && (crf < 0 || crf > 51))
            errors.Add($"crf out of range: {crf}");

        if (!info.HasAudio && plan.AudioCodec is not null)
        {
            plan.AudioCodec = null;
            plan.AudioBitrateK = 0;
            warnings.Add("The source has no audio track, audio settings were dropped.");
        }

        if (string.IsNullOrWhiteSpace(plan.Preset)) plan.Preset = FfmpegArguments.DefaultPreset(plan.Codec);
        if (!FfmpegArguments.IsValidPreset(plan.Codec, plan.Preset))
            errors.Add($"Preset '{plan.Preset}' is invalid for codec '{plan.Codec}'.");

        if (plan.ModeEnum == EncodeMode.TwoPass && errors.Count == 0)
        {
            var estimated = PlanCalculator.EstimatedMb(plan, info.DurationSeconds);
            if (estimated > options.TargetMb * 1.05)
                warnings.Add($"These settings estimate to {estimated:0.0} MB, above the {options.TargetMb:0.##} MB target. The size-correction retry will fix it if it overshoots.");
        }

        plan.ExtraArgs ??= new List<string>();
        plan.ExtraArgs.RemoveAll(a => string.IsNullOrWhiteSpace(a) || a.Contains("..") || a.Contains('&') || a.Contains('|'));

        return new PlanParseResult(errors.Count == 0 ? plan : null, errors, warnings);
    }

    private static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var fence = Regex.Match(raw, "```(?:json)?\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline);
        if (fence.Success) return fence.Groups[1].Value;
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }
}
