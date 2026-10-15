using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Cli;

/// <summary>
/// <c>donustur</c> komutu: Dönüştür panelinin seçimlerini panelin ön ayar biçiminde
/// (<see cref="ConversionPreset"/>) toplar, planı <see cref="ConversionPresetPlan.ToPlan"/> ile kurar,
/// argümanı <see cref="ConversionArguments.Build"/> ile üretir. Kap, video ve ses kodeği kümeleri
/// panelin listeleriyle aynıdır.
/// </summary>
public static class CliConvert
{
    public static IReadOnlyList<string> Containers { get; } = new[]
    {
        "mp4", "mkv", "webm", "mov", "avi", "gif", "mp3", "m4a", "wav", "flac", "webp", "avif", "mxf",
        "png", "jpg", "opus", "ogg", "aiff", "ac3", "apng"
    };

    public static IReadOnlyList<string> VideoCodecs { get; } =
        ConversionCodecChoice.LegacyOrder.Concat(ConversionCodecChoice.Hardware).Concat(ConversionCodecChoice.Archive).Distinct().ToList();

    public static IReadOnlyList<string> AudioCodecs { get; } = new[]
    {
        "aac", "libopus", "libmp3lame", "pcm_s16le", "flac", "copy", ConversionPreset.NoAudio, "ac3", "eac3"
    };

    private static readonly IReadOnlyDictionary<string, string> VideoAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["h264"] = "libx264", ["avc"] = "libx264", ["x264"] = "libx264",
        ["hevc"] = "libx265", ["h265"] = "libx265", ["x265"] = "libx265",
        ["vp9"] = "libvpx-vp9", ["av1"] = "libsvtav1", ["prores"] = "prores_ks:standard", ["dnxhr"] = "dnxhd:dnxhr_sq"
    };

    private static readonly IReadOnlyDictionary<string, string> AudioAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["opus"] = "libopus", ["mp3"] = "libmp3lame", ["pcm"] = "pcm_s16le", ["yok"] = ConversionPreset.NoAudio
    };

    public const int MinVideoBitrateK = 50;
    public const int MaxVideoBitrateK = 200000;
    public const int MinAudioBitrateK = 8;
    public const int MaxAudioBitrateK = 1536;
    public const int MinHeight = 16;
    public const int MaxHeight = 8640;

    public static bool TryVideoCodec(string text, out string codec)
    {
        codec = VideoCodecs.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase))
            ?? (VideoAliases.TryGetValue(text, out var alias) ? alias : "");
        return codec.Length > 0;
    }

    public static bool TryAudioCodec(string text, out string codec)
    {
        codec = AudioCodecs.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase))
            ?? (AudioAliases.TryGetValue(text, out var alias) ? alias : "");
        return codec.Length > 0;
    }

    private static bool IsSequence(string container) => container is "png" or "jpg";

    private static string? ContainerOfPath(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (extension == "aif") extension = "aiff";
        if (extension == "jpeg") extension = "jpg";
        return Containers.Contains(extension) && !IsSequence(extension) ? extension : null;
    }

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var request = new CliRequest { Command = CliCommand.Convert };
        var preset = new ConversionPreset { Id = "cli", Name = "cli" };
        string? container = null;
        double? crf = null;
        int? bitrate = null;
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--yardim" or "--help":
                    return CliParser.Success(new CliRequest { Command = CliCommand.Help });
                case "--kap" or "--container":
                    if (!CliParser.TryValue(args, ref i, out var kap)) return CliParser.Fail("error.missing-value", arg);
                    container = Containers.FirstOrDefault(c => string.Equals(c, kap, StringComparison.OrdinalIgnoreCase));
                    if (container is null) return CliParser.Fail("error.bad-container", kap);
                    break;
                case "--kodek" or "--codec":
                    if (!CliParser.TryValue(args, ref i, out var kodek)) return CliParser.Fail("error.missing-value", arg);
                    if (!TryVideoCodec(kodek, out var video)) return CliParser.Fail("error.convert-codec", kodek);
                    preset = preset with { VideoCodec = video };
                    break;
                case "--crf":
                    if (!CliParser.TryValue(args, ref i, out var crfText)) return CliParser.Fail("error.missing-value", arg);
                    if (bitrate is not null) return CliParser.Fail("error.crf-and-bitrate", crfText);
                    if (!CliParser.TryParseNumber(crfText, out var crfValue) || crfValue != Math.Floor(crfValue)) return CliParser.Fail("error.bad-crf", crfText);
                    crf = crfValue;
                    break;
                case "--bit-hizi" or "--bitrate":
                    if (!CliParser.TryValue(args, ref i, out var bitText)) return CliParser.Fail("error.missing-value", arg);
                    if (crf is not null) return CliParser.Fail("error.crf-and-bitrate", bitText);
                    if (!int.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out var bitValue)
                        || bitValue < MinVideoBitrateK || bitValue > MaxVideoBitrateK)
                        return CliParser.Fail("error.bad-bitrate", bitText);
                    bitrate = bitValue;
                    break;
                case "--cozunurluk" or "--resolution":
                    if (!CliParser.TryValue(args, ref i, out var cozunurluk)) return CliParser.Fail("error.missing-value", arg);
                    if (ParseResolution(cozunurluk) is not { } resolved) return CliParser.Fail("error.bad-resolution", cozunurluk);
                    preset = resolved(preset);
                    break;
                case "--kare-hizi" or "--fps":
                    if (!CliParser.TryValue(args, ref i, out var kare)) return CliParser.Fail("error.missing-value", arg);
                    if (kare is "kaynak" or "source")
                    {
                        preset = preset with { Fps = "source", CustomFps = null };
                        break;
                    }
                    if (!CliParser.TryParseNumber(kare, out var fps) || fps < CliParser.MinFrameRate || fps > CliParser.MaxFrameRate)
                        return CliParser.Fail("error.bad-fps", kare);
                    preset = preset with { Fps = ConversionPreset.Custom, CustomFps = fps.ToString(CultureInfo.InvariantCulture) };
                    break;
                case "--ses-kodek" or "--audio-codec":
                    if (!CliParser.TryValue(args, ref i, out var ses)) return CliParser.Fail("error.missing-value", arg);
                    if (!TryAudioCodec(ses, out var audio)) return CliParser.Fail("error.convert-audio-codec", ses);
                    preset = preset with { AudioCodec = audio };
                    break;
                case "--ses-bit-hizi" or "--audio-bitrate":
                    if (!CliParser.TryValue(args, ref i, out var sesBit)) return CliParser.Fail("error.missing-value", arg);
                    if (!int.TryParse(sesBit, NumberStyles.None, CultureInfo.InvariantCulture, out var sesBitValue)
                        || sesBitValue < MinAudioBitrateK || sesBitValue > MaxAudioBitrateK)
                        return CliParser.Fail("error.bad-audio-bitrate", sesBit);
                    preset = preset with { AudioBitrateK = sesBitValue };
                    break;
                case "--kes" or "--cut":
                    if (!CliParser.TryValue(args, ref i, out var cut)) return CliParser.Fail("error.missing-value", arg);
                    if (!CliParser.TryParseRange(cut, out var start, out var end, out var startFrame, out var endFrame))
                        return CliParser.Fail("error.bad-range", cut);
                    request = request with { TrimStartSeconds = start, TrimEndSeconds = end, TrimStartFrame = startFrame, TrimEndFrame = endFrame };
                    break;
                case "--meta-yok" or "--no-metadata":
                    preset = preset with { DropMetadata = true };
                    break;
                case "--cikti" or "--output" or "-o":
                    if (!CliParser.TryValue(args, ref i, out var output)) return CliParser.Fail("error.missing-value", arg);
                    request = request with { Output = output };
                    break;
                case "--json":
                    request = request with { Json = true };
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1) return CliParser.Fail("error.unknown-option", arg);
                    if (request.Input is not null) return CliParser.Fail("error.extra-input", arg);
                    request = request with { Input = arg };
                    break;
            }
        }

        if (request.Input is null) return CliParser.Fail("error.no-input", null);

        var fromOutput = request.Output is { } outputPath ? ContainerOfPath(outputPath) : null;
        if (container is null)
        {
            if (request.Output is { } named && fromOutput is null) return CliParser.Fail("error.convert-output-extension", named);
            container = fromOutput ?? preset.Container;
        }
        else if (request.Output is { } named && !IsSequence(container) && fromOutput != container)
            return CliParser.Fail("error.convert-output-extension", named);

        var encoder = IntermediateCodecs.FromId(preset.VideoCodec)?.Encoder ?? preset.VideoCodec;
        if (crf is { } crfChosen)
        {
            var (min, max) = CodecModel.CrfRange(encoder);
            if (crfChosen < min || crfChosen > max) return CliParser.Fail("error.convert-crf", crfChosen.ToString(CultureInfo.InvariantCulture));
            preset = preset with { QualityMode = ConversionQualityMode.Crf, Quality = (int)crfChosen };
        }
        else if (bitrate is { } kbps)
            preset = preset with { QualityMode = ConversionQualityMode.Bitrate, Quality = kbps };

        return CliParser.Success(request with { Conversion = preset with { Container = container } });
    }

    private static Func<ConversionPreset, ConversionPreset>? ParseResolution(string text)
    {
        if (text is "kaynak" or "source") return p => p with { Resolution = "source", CustomResolution = null };
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var height))
            return height is >= MinHeight and <= MaxHeight
                ? p => p with { Resolution = height.ToString(CultureInfo.InvariantCulture), CustomResolution = null }
                : null;
        var parts = text.ToLowerInvariant().Split('x');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var w)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            && w is >= MinHeight and <= 2 * MaxHeight && h is >= MinHeight and <= MaxHeight)
        {
            var custom = w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture);
            return p => p with { Resolution = ConversionPreset.Custom, CustomResolution = custom };
        }
        return null;
    }

    /// <summary>İsteğin dönüşüm planı; kesit, <see cref="CliRequest.Resolved"/> sonrası saniyeden gelir.</summary>
    public static ConversionPlan Plan(CliRequest request)
        => request.Conversion!.ToPlan() with
        {
            Start = request.TrimStartSeconds is { } start ? TimeSpan.FromSeconds(start) : null,
            End = request.TrimEndSeconds is { } end ? TimeSpan.FromSeconds(end) : null
        };

    /// <summary>
    /// Çıktının yolu. Verilmemişse panelin yolu: dosya için kaynağın yanında <c>_converted</c>,
    /// resim dizisi için <see cref="EncodeRunner.SequenceOutputFolder"/>.
    /// </summary>
    public static string OutputPath(CliRequest request, MediaInfo info, ConversionPlan plan)
    {
        if (request.Output is { } output) return Path.GetFullPath(output);
        return plan.ImageSequenceOutput && !plan.SingleFrame
            ? EncodeRunner.SequenceOutputFolder(info.FilePath)
            : ShrinkEngine.UniqueOutputPath(info.FilePath, "converted", plan.Container, null, null);
    }

    /// <summary>Komut satırında gösterilen ffmpeg argümanı; paneldeki komut kutusuyla aynı çağrı.</summary>
    public static IReadOnlyList<string> Arguments(MediaInfo info, ConversionPlan plan, string outputPath, IEncoderAvailability? availability)
    {
        var target = plan.ImageSequenceOutput && !plan.SingleFrame
            ? ConversionArguments.SequenceOutputPattern(outputPath, Path.GetFileName(outputPath), plan.Container)
            : outputPath;
        return ConversionArguments.Build(info, plan, target, plan.Gif ? "palette.png" : null, availability);
    }

    internal static async Task<int> RunAsync(CliRequest request, CliServices services, TextWriter stdout, TextWriter stderr,
        CliText text, CancellationToken ct)
    {
        var input = Path.GetFullPath(request.Input!);
        if (!File.Exists(input))
        {
            stderr.WriteLine(text.Format("error.input-missing", input));
            return ExitCodes.Error;
        }

        CliProgress.Stage(stderr, text, "progress.probe", request.Json);
        var info = await services.Probe(input, ct);
        if (request.Resolved(info, out request) is { } trimError)
        {
            stderr.WriteLine(text.Format(trimError, "-"));
            return ExitCodes.Usage;
        }

        var plan = Plan(request);
        var availability = services.Availability();
        if (!ConversionArguments.ContainerAvailable(plan.Container, availability))
        {
            stderr.WriteLine(text.Format("error.container-unavailable", plan.Container, ConversionArguments.RequiredEncoder(plan.Container)));
            return ExitCodes.Error;
        }

        var errors = ConversionArguments.Validate(info, plan);
        if (errors.Count > 0)
        {
            foreach (var error in errors) stderr.WriteLine(text.Format("error.convert-invalid", error));
            stderr.WriteLine(text["usage.hint"]);
            return ExitCodes.Usage;
        }

        var output = OutputPath(request, info, plan);
        if (PathEquality.Same(output, info.FilePath))
        {
            stderr.WriteLine(text["error.same-output"]);
            return ExitCodes.Usage;
        }

        var arguments = Arguments(info, plan, output, availability);
        var clock = Stopwatch.StartNew();
        ConversionResult? result = null;
        string? failure = null;
        try
        {
            result = await services.Convert(info, plan, output, new CliProgress.Encode(stderr, text, request.Json), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = ex.Message;
        }
        clock.Stop();

        var exit = result is null ? ExitCodes.Error : ExitCodes.InBand;
        if (request.Json)
            stdout.Write(ResultJson(input, output, plan, arguments, result, exit, clock.Elapsed));
        else if (result is not null)
        {
            stdout.WriteLine(text.Format("result.output", result.OutputPath));
            stdout.WriteLine(text.Format("convert.size", result.OutputMb.ToString("0.0", text.Culture)));
            stdout.WriteLine(text.Format("result.duration", clock.Elapsed.TotalSeconds < 60 ? clock.Elapsed.TotalSeconds.ToString("0.0", text.Culture) + " s" : Saat.Ekran(clock.Elapsed)));
        }
        if (failure is not null) stderr.WriteLine(text.Format("error.failed", failure));
        return exit;
    }

    private static string ResultJson(string input, string output, ConversionPlan plan, IReadOnlyList<string> arguments,
        ConversionResult? result, int exit, TimeSpan elapsed)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("command", "donustur");
            writer.WriteString("input", input);
            writer.WriteString("output", result?.OutputPath ?? output);
            writer.WriteString("container", plan.Container);
            writer.WriteString("videoCodec", plan.VideoCodec);
            if (plan.AudioCodec is null) writer.WriteNull("audioCodec"); else writer.WriteString("audioCodec", plan.AudioCodec);
            writer.WriteStartArray("arguments");
            foreach (var argument in arguments) writer.WriteStringValue(argument);
            writer.WriteEndArray();
            writer.WriteString("commandLine", FfmpegArguments.ToCommandLine(arguments));
            writer.WriteStartObject("result");
            writer.WriteNumber("exitCode", exit);
            writer.WriteBoolean("success", result is not null);
            if (result is null) writer.WriteNull("outputMb"); else writer.WriteNumber("outputMb", Math.Round(result.OutputMb, 3));
            writer.WriteNumber("elapsedSeconds", Math.Round(elapsed.TotalSeconds, 1));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }
}
