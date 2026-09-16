using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Cli;

public enum CliCommand { Help, Version, Shrink, Plan }

public enum CliCodec { Auto, H264, Hevc, Av1 }

public static class ExitCodes
{
    public const int InBand = 0;
    public const int Error = 1;
    public const int UnderBand = 2;
    public const int CeilingExceeded = 3;
    public const int Usage = 64;
    public const int Cancelled = 130;
}

public sealed record CliRequest
{
    public CliCommand Command { get; init; } = CliCommand.Help;
    public string? Input { get; init; }
    public double? TargetMb { get; init; }
    public double? Quality { get; init; }
    public CliCodec Codec { get; init; } = CliCodec.Auto;
    public string? Output { get; init; }
    public bool Json { get; init; }
    public bool SkipMeasurement { get; init; }
    public bool MeasureVmaf { get; init; }
    public bool Fast { get; init; }

    public PlanOptions ToPlanOptions(double targetMb)
    {
        var options = new PlanOptions
        {
            TargetMb = targetMb,
            Intent = Intent.Sharing,
            Codec = Codec switch
            {
                CliCodec.H264 => CodecPreference.Compatible,
                CliCodec.Av1 => CodecPreference.MaxCompression,
                _ => CodecPreference.Auto
            },
            AllowResolutionDrop = true,
            AllowFpsDrop = true,
            HdrPolicy = HdrPolicy.Preserve,
            FillPolicy = FillPolicy.FillTarget,
            SpeedMode = Fast ? SpeedMode.Fast : SpeedMode.Quality
        };
        if (Codec == CliCodec.Hevc) options.LockedCodec = "libx265";
        return options;
    }
}

public sealed record CliParseResult(CliRequest? Request, string? ErrorKey, string? ErrorArgument)
{
    public bool Ok => Request is not null;
}

public static class CliParser
{
    public const double MinQuality = 1;
    public const double MaxQuality = 100;

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return Success(new CliRequest { Command = CliCommand.Help });

        var head = args[0];
        if (head is "-h" or "--help" or "help" or "yardim" or "--yardim")
            return Success(new CliRequest { Command = CliCommand.Help });
        if (head is "--version" or "-v" or "version" or "surum" or "--surum")
            return Success(new CliRequest { Command = CliCommand.Version });

        var command = head switch
        {
            "kucult" or "shrink" => CliCommand.Shrink,
            "plan" => CliCommand.Plan,
            _ => (CliCommand?)null
        };
        if (command is null) return Fail("error.unknown-command", head);

        var request = new CliRequest { Command = command.Value };
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "--yardim":
                    return Success(new CliRequest { Command = CliCommand.Help });
                case "--hedef" or "--target":
                    if (!TryValue(args, ref i, out var target)) return Fail("error.missing-value", arg);
                    if (!TryParseSize(target, out var mb)) return Fail("error.bad-size", target);
                    request = request with { TargetMb = mb };
                    break;
                case "--kalite" or "--quality":
                    if (!TryValue(args, ref i, out var quality)) return Fail("error.missing-value", arg);
                    if (!TryParseNumber(quality, out var score) || score < MinQuality || score > MaxQuality)
                        return Fail("error.bad-quality", quality);
                    request = request with { Quality = score };
                    break;
                case "--kodek" or "--codec":
                    if (!TryValue(args, ref i, out var codec)) return Fail("error.missing-value", arg);
                    if (!TryParseCodec(codec, out var parsed)) return Fail("error.bad-codec", codec);
                    request = request with { Codec = parsed };
                    break;
                case "--cikti" or "--output" or "-o":
                    if (!TryValue(args, ref i, out var output)) return Fail("error.missing-value", arg);
                    request = request with { Output = output };
                    break;
                case "--json":
                    request = request with { Json = true };
                    break;
                case "--olcumsuz" or "--no-measure":
                    request = request with { SkipMeasurement = true };
                    break;
                case "--vmaf":
                    request = request with { MeasureVmaf = true };
                    break;
                case "--hizli" or "--fast":
                    request = request with { Fast = true };
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1) return Fail("error.unknown-option", arg);
                    if (request.Input is not null) return Fail("error.extra-input", arg);
                    request = request with { Input = arg };
                    break;
            }
        }

        if (request.Input is null) return Fail("error.no-input", null);
        if (request.TargetMb is null == request.Quality is null) return Fail("error.target-or-quality", null);
        return Success(request);
    }

    public static bool TryParseSize(string text, out double mb)
    {
        mb = 0;
        var value = text.Trim();
        var factor = 1.0;
        if (value.EndsWith("GB", StringComparison.OrdinalIgnoreCase)) { factor = 1024; value = value[..^2]; }
        else if (value.EndsWith("MB", StringComparison.OrdinalIgnoreCase)) value = value[..^2];
        else if (value.EndsWith("M", StringComparison.OrdinalIgnoreCase)) value = value[..^1];
        if (!TryParseNumber(value.Trim(), out var number) || number <= 0) return false;
        mb = number * factor;
        return double.IsFinite(mb);
    }

    public static bool TryParseCodec(string text, out CliCodec codec)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "auto" or "otomatik": codec = CliCodec.Auto; return true;
            case "h264" or "avc" or "x264": codec = CliCodec.H264; return true;
            case "hevc" or "h265" or "x265": codec = CliCodec.Hevc; return true;
            case "av1": codec = CliCodec.Av1; return true;
            default: codec = CliCodec.Auto; return false;
        }
    }

    private static bool TryParseNumber(string text, out double value)
        => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
           && double.IsFinite(value);

    private static bool TryValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        value = "";
        if (index + 1 >= args.Count) return false;
        index++;
        value = args[index];
        return true;
    }

    private static CliParseResult Success(CliRequest request) => new(request, null, null);

    private static CliParseResult Fail(string key, string? argument) => new(null, key, argument);
}
