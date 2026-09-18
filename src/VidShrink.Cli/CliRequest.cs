using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Cli;

public enum CliCommand { Help, Version, Shrink, Plan, Watch }

public enum CliCodec { Auto, H264, Hevc, Av1 }

public static class ExitCodes
{
    public const int InBand = 0;
    public const int Error = 1;
    public const int UnderBand = 2;
    public const int CeilingExceeded = 3;
    public const int WatchFailures = 4;
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
    public string? OutputDirectory { get; init; }
    public double? PollSeconds { get; init; }
    public bool Once { get; init; }
    public bool Json { get; init; }
    public bool JsonLines { get; init; }
    public bool SkipMeasurement { get; init; }
    public bool MeasureVmaf { get; init; }
    public bool Fast { get; init; }
    public string? PreferredLanguage { get; init; }
    public double? TrimStartSeconds { get; init; }
    public double? TrimEndSeconds { get; init; }

    /// <summary>
    /// <paramref name="sourceDurationSeconds"/> kesitin acik ucunu kapatir (<c>--kes 10-</c>);
    /// 0 verilirse kesit yalnizca acikca verilen iki ucla kurulur.
    /// </summary>
    public PlanOptions ToPlanOptions(double targetMb, double sourceDurationSeconds = 0)
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
            SpeedMode = Fast ? SpeedMode.Fast : SpeedMode.Quality,
            PreferredLanguage = PreferredLanguage
        };
        if (Codec == CliCodec.Hevc) options.LockedCodec = "libx265";
        options.Trim = TrimWindow.Of(TrimStartSeconds, TrimEndSeconds, sourceDurationSeconds);
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
            "izle" or "watch" => CliCommand.Watch,
            _ => (CliCommand?)null
        };
        if (command is null) return Fail("error.unknown-command", head);

        var request = new CliRequest { Command = command.Value };
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--yardim" or "--help":
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
                case "--kes" or "--cut" when command != CliCommand.Watch:
                    if (!TryValue(args, ref i, out var cut)) return Fail("error.missing-value", arg);
                    if (!TryParseRange(cut, out var cutStart, out var cutEnd)) return Fail("error.bad-range", cut);
                    request = request with { TrimStartSeconds = cutStart, TrimEndSeconds = cutEnd };
                    break;
                case "--aralik" or "--interval" when command == CliCommand.Watch:
                    if (!TryValue(args, ref i, out var interval)) return Fail("error.missing-value", arg);
                    if (!TryParseNumber(interval, out var seconds) || seconds <= 0 || seconds > 86400) return Fail("error.bad-interval", interval);
                    request = request with { PollSeconds = seconds };
                    break;
                case "--bir-kez" or "--once" when command == CliCommand.Watch:
                    request = request with { Once = true };
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

        if (request.Input is null) return Fail(command == CliCommand.Watch ? "error.watch-no-folder" : "error.no-input", null);
        if (command == CliCommand.Watch && request.Output is null) return Fail("error.watch-no-output", null);
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

    /// <summary>
    /// <c>BAS-SON</c> kesiti. Uclar saniye (<c>12.5</c>) ya da saat gosterimi (<c>1:23</c>,
    /// <c>1:02:03</c>) olabilir; son uc bos birakilirsa (<c>10-</c>) kaynagin sonuna kadar.
    /// </summary>
    private static bool TryParseRange(string text, out double? start, out double? end)
    {
        start = null;
        end = null;
        var parts = text.Split('-');
        if (parts.Length != 2) return false;
        if (!TryParseClock(parts[0], out var from)) return false;
        if (parts[1].Length > 0)
        {
            if (!TryParseClock(parts[1], out var to)) return false;
            if (to <= from) return false;
            end = to;
        }
        if (from < 0) return false;
        start = from;
        return true;
    }

    private static bool TryParseClock(string text, out double seconds)
    {
        seconds = 0;
        if (text.Length == 0) return false;
        var fields = text.Split(':');
        if (fields.Length > 3) return false;
        var total = 0.0;
        for (var i = 0; i < fields.Length; i++)
        {
            if (!TryParseNumber(fields[i], out var field) || field < 0) return false;
            if (fields.Length > 1 && i > 0 && field >= 60) return false;
            total = total * 60 + field;
        }
        seconds = total;
        return true;
    }

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
