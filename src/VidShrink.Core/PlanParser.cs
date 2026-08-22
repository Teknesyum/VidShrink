using System.Text.Json;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public sealed record PlanParseResult(EncodePlan? Plan, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool Ok => Plan is not null && Errors.Count == 0;
}

public static class PlanParser
{
    private static readonly string[] AllowedCodecs = { "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv", "av1_nvenc", "av1_qsv", "h264_amf", "hevc_amf", "av1_amf" };
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

        if (!options.AllowResolutionDrop && (plan.Width != info.Width || plan.Height != info.Height))
        {
            plan.Width = info.Width % 2 == 0 ? info.Width : info.Width - 1;
            plan.Height = info.Height % 2 == 0 ? info.Height : info.Height - 1;
            warnings.Add("Resolution reduction is disabled, so the source resolution was restored.");
        }
        else
        {
            if (plan.Width > info.Width || plan.Height > info.Height)
                errors.Add("AI plans cannot upscale beyond the source resolution.");

            var sourceAspect = (double)info.Width / Math.Max(info.Height, 1);
            var planAspect = (double)plan.Width / Math.Max(plan.Height, 1);
            if (Math.Abs(planAspect / sourceAspect - 1.0) > 0.015)
                errors.Add("AI plan resolution must preserve the source aspect ratio.");
        }

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


        if (!options.AllowFpsDrop && plan.Fps < info.Fps - 0.01)
        {
            plan.Fps = info.Fps;
            warnings.Add("Frame-rate reduction is disabled, so the source frame rate was restored.");
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

        plan.ExtraArgs = SanitizeExtraArgs(plan.ExtraArgs, warnings);

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

    private static List<string> SanitizeExtraArgs(IReadOnlyList<string>? raw, List<string> warnings)
    {
        if (raw is null || raw.Count == 0) return new List<string>();

        var allowed = new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["-tune"] = value => new[] { "film", "animation", "grain", "stillimage", "fastdecode", "zerolatency", "psnr", "ssim" }.Contains(value, StringComparer.OrdinalIgnoreCase),
            ["-profile:v"] = value => new[] { "baseline", "main", "high", "high10", "main10" }.Contains(value, StringComparer.OrdinalIgnoreCase),
            ["-level:v"] = value => Regex.IsMatch(value, @"^\d(?:\.\d)?$"),
            ["-aq-mode"] = value => int.TryParse(value, out var number) && number is >= 0 and <= 4,
            ["-aq-strength"] = value => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && number is >= 0 and <= 3
        };

        var result = new List<string>();
        for (var index = 0; index < raw.Count; index++)
        {
            var flag = raw[index];
            if (!allowed.TryGetValue(flag, out var validator) || index + 1 >= raw.Count || !validator(raw[index + 1]))
            {
                warnings.Add($"Unsafe or unsupported extra argument was removed: {flag}");
                continue;
            }

            result.Add(flag);
            result.Add(raw[++index]);
        }
        return result;
    }
}
