using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Cli;

/// <summary>
/// Kaydedicinin <c>recorder-settings.json</c> dosyasından komut satırının okuduğu alanlar. Anahtarlar
/// ve kabul kuralları <c>App/Recorder/RecorderSettings.Load</c> ile aynıdır; ikisinin aynı dosyayı aynı
/// okuduğunu <c>CliKayitTests</c> pencerenin yazdığı dosyadan ölçer.
/// </summary>
public sealed record CliRecordSettings
{
    public const string FileName = "recorder-settings.json";

    public string? OutputFolder { get; init; }
    public bool ManualMode { get; init; }
    public int? TargetSeconds { get; init; }
    public double? TargetMegabytes { get; init; }
    public int Fps { get; init; } = RecorderArguments.DefaultFps;
    public string Codec { get; init; } = RecorderArguments.DefaultVideoCodec;
    public string Preset { get; init; } = RecorderArguments.DefaultPreset;
    public double Quality { get; init; } = RecorderArguments.DefaultQuality;
    public bool ShowCursor { get; init; } = true;
    public bool ShowClicks { get; init; }
    public RecorderTargetKind Target { get; init; } = RecorderTargetKind.Screen;
    public string? WindowTitle { get; init; }
    public int RegionX { get; init; }
    public int RegionY { get; init; }
    public int RegionWidth { get; init; } = 1280;
    public int RegionHeight { get; init; } = 720;
    public string? MicrophoneName { get; init; }
    public string? SystemAudioName { get; init; }
    public string? WebcamName { get; init; }
    public WebcamShape WebcamShape { get; init; } = WebcamShape.Rectangle;
    public bool WebcamMirror { get; init; }
    public double WebcamX { get; init; } = 1;
    public double WebcamY { get; init; } = 1;
    public double WebcamSize { get; init; } = 0.2;
    public WebcamBackground WebcamBackground { get; init; } = WebcamBackground.Keep;
    public RecorderContainer Container { get; init; } = RecorderContainer.Mp4;
    public int ScreenIndex { get; init; }
    public int ScaleWidth { get; init; }
    public int ScaleHeight { get; init; }
    public int KeyframeSeconds { get; init; } = RecorderArguments.DefaultKeyframeSeconds;
    public string? Profile { get; init; }
    public string? Tune { get; init; }
    public RecorderRateControl RateControl { get; init; } = RecorderRateControl.Quality;
    public RecorderFrameRateMode FrameRateMode { get; init; } = RecorderFrameRateMode.Auto;
    public int BitrateKbps { get; init; }
    public int MaxBitrateKbps { get; init; }
    public int BufferKbits { get; init; }
    public string PixelFormat { get; init; } = RecorderArguments.DefaultPixelFormat;
    public string? ColorSpace { get; init; }
    public string? ColorRange { get; init; }
    public bool HdrToSdr { get; init; }
    public double MaxDurationSeconds { get; init; }
    public double SplitSeconds { get; init; }
    public double SplitMegabytes { get; init; }
    public AudioTrackLayout AudioLayout { get; init; } = AudioTrackLayout.MixedSingleTrack;
    public double AudioGainDb { get; init; }
    public bool AudioNoiseGate { get; init; }
    public bool AudioNoiseSuppression { get; init; }
    public bool AudioCompressor { get; init; }
    public bool AudioLimiter { get; init; }
    public string AudioCodec { get; init; } = RecorderArguments.DefaultAudioCodec;
    public IReadOnlyList<RecorderRegion> PrivacyMasks { get; init; } = Array.Empty<RecorderRegion>();

    /// <summary>Pencerenin ayar klasöründeki dosya; <c>VIDSHRINK_SETTINGS_PATH</c> ana ayarı nereye alıyorsa oraya.</summary>
    public static string? DefaultPath
    {
        get
        {
            var main = UpdateSettings.DefaultPath;
            var folder = Path.GetDirectoryName(main);
            return string.IsNullOrEmpty(folder) || !Path.IsPathRooted(main) ? null : Path.Combine(folder, FileName);
        }
    }

    /// <summary>Dosya yoksa ya da okunamıyorsa pencerenin ilk açılışındaki varsayılanlar.</summary>
    public static CliRecordSettings Load(string? file)
    {
        var settings = new CliRecordSettings();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return settings;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return settings;
            var masks = new List<RecorderRegion>();
            if (root["privacyMasks"] is JsonArray maskArray)
                foreach (var mask in maskArray.OfType<JsonObject>())
                    if ((int?)mask["x"] is { } mx && (int?)mask["y"] is { } my
                        && (int?)mask["width"] is { } mw && mw > 0 && (int?)mask["height"] is { } mh && mh > 0)
                        masks.Add(new RecorderRegion(mx, my, mw, mh));

            var container = settings.Container;
            if (Enum.TryParse<RecorderContainer>((string?)root["containerFormat"], true, out var format) && Enum.IsDefined(format)) container = format;
            else if (Enum.TryParse<RecorderContainer>((string?)root["containerChoice"], true, out var choice) && Enum.IsDefined(choice))
                container = choice == RecorderContainer.Mkv ? RecorderContainer.Mp4 : choice;
            else if (Enum.TryParse<RecorderContainer>((string?)root["container"], true, out var old) && Enum.IsDefined(old))
                container = old;

            var camX = settings.WebcamX;
            var camY = settings.WebcamY;
            if (Enum.TryParse<WebcamCorner>((string?)root["webcamCorner"], true, out var corner) && Enum.IsDefined(corner))
            {
                camX = corner is WebcamCorner.TopLeft or WebcamCorner.BottomLeft ? 0 : 1;
                camY = corner is WebcamCorner.TopLeft or WebcamCorner.TopRight ? 0 : 1;
            }
            if ((double?)root["webcamX"] is { } x && x is >= 0 and <= 1) camX = x;
            if ((double?)root["webcamY"] is { } y && y is >= 0 and <= 1) camY = y;

            var manual = (bool?)root["manualMode"] ?? !((bool?)root["autoMode"] ?? true);
            return settings with
            {
                OutputFolder = (string?)root["outputFolder"],
                ManualMode = manual,
                TargetSeconds = (int?)root["targetSeconds"] is { } ts && ts > 0 ? ts : null,
                TargetMegabytes = (double?)root["targetMegabytes"] is { } tm && tm > 0 ? tm : null,
                Fps = (int?)root["fps"] is { } fps && fps > 0 ? fps : settings.Fps,
                Codec = (string?)root["codec"] is { Length: > 0 } codec ? codec : settings.Codec,
                Preset = (string?)root["preset"] is { Length: > 0 } preset ? preset : settings.Preset,
                Quality = (double?)root["quality"] ?? settings.Quality,
                ShowCursor = (bool?)root["showCursor"] ?? true,
                ShowClicks = (bool?)root["showClicks"] ?? false,
                Target = Enum.TryParse<RecorderTargetKind>((string?)root["target"], true, out var target) ? target : settings.Target,
                WindowTitle = (string?)root["windowTitle"],
                RegionX = (int?)root["regionX"] ?? 0,
                RegionY = (int?)root["regionY"] ?? 0,
                RegionWidth = (int?)root["regionWidth"] is { } width && width > 0 ? width : settings.RegionWidth,
                RegionHeight = (int?)root["regionHeight"] is { } height && height > 0 ? height : settings.RegionHeight,
                MicrophoneName = (string?)root["microphoneName"],
                SystemAudioName = (string?)root["systemAudioName"],
                WebcamName = (string?)root["webcamName"],
                WebcamShape = Enum.TryParse<WebcamShape>((string?)root["webcamShape"], true, out var shape) && Enum.IsDefined(shape) ? shape : settings.WebcamShape,
                WebcamMirror = (bool?)root["webcamMirror"] ?? settings.WebcamMirror,
                WebcamX = camX,
                WebcamY = camY,
                WebcamSize = (double?)root["webcamSize"] is { } size && size is >= RecorderArguments.MinWebcamSize and <= RecorderArguments.MaxWebcamSize ? size : settings.WebcamSize,
                WebcamBackground = Enum.TryParse<WebcamBackground>((string?)root["webcamBackground"], true, out var background) && Enum.IsDefined(background) ? background : settings.WebcamBackground,
                Container = container,
                ScreenIndex = (int?)root["screenIndex"] ?? 0,
                ScaleWidth = (int?)root["scaleWidth"] ?? 0,
                ScaleHeight = (int?)root["scaleHeight"] ?? 0,
                KeyframeSeconds = (int?)root["keyframeSeconds"] ?? RecorderArguments.DefaultKeyframeSeconds,
                Profile = (string?)root["profile"],
                Tune = (string?)root["tune"],
                RateControl = Enum.TryParse<RecorderRateControl>((string?)root["rateControl"], true, out var rate) ? rate : settings.RateControl,
                FrameRateMode = Enum.TryParse<RecorderFrameRateMode>((string?)root["frameRateMode"], true, out var fpsMode) && Enum.IsDefined(fpsMode) ? fpsMode : settings.FrameRateMode,
                BitrateKbps = (int?)root["bitrateKbps"] ?? 0,
                MaxBitrateKbps = (int?)root["maxBitrateKbps"] ?? 0,
                BufferKbits = (int?)root["bufferKbits"] ?? 0,
                PixelFormat = (string?)root["pixelFormat"] is { Length: > 0 } pixel ? RecorderArguments.StoredPixelFormat(pixel) : settings.PixelFormat,
                ColorSpace = (string?)root["colorSpace"],
                ColorRange = (string?)root["colorRange"],
                HdrToSdr = (bool?)root["hdrToSdr"] ?? false,
                MaxDurationSeconds = (double?)root["maxDurationSeconds"] ?? 0,
                SplitSeconds = (double?)root["splitSeconds"] ?? 0,
                SplitMegabytes = (double?)root["splitMegabytes"] ?? 0,
                AudioLayout = Enum.TryParse<AudioTrackLayout>((string?)root["audioLayout"], true, out var layout) ? layout : settings.AudioLayout,
                AudioGainDb = (double?)root["audioGainDb"] ?? 0,
                AudioNoiseGate = (bool?)root["audioNoiseGate"] ?? false,
                AudioNoiseSuppression = (bool?)root["audioNoiseSuppression"] ?? false,
                AudioCompressor = (bool?)root["audioCompressor"] ?? false,
                AudioLimiter = (bool?)root["audioLimiter"] ?? false,
                AudioCodec = (string?)root["audioCodec"] == RecorderArguments.OpusAudioCodec ? RecorderArguments.OpusAudioCodec : RecorderArguments.DefaultAudioCodec,
                PrivacyMasks = masks
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new CliRecordSettings();
        }
    }

    /// <summary>Kaydın yazılacağı klasör: ayardaki klasör, yoksa video klasörünün altında VidShrink, o da yoksa çalışma klasörü.</summary>
    public string ResolveFolder()
    {
        if (!string.IsNullOrWhiteSpace(OutputFolder)) return OutputFolder!;
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (!string.IsNullOrEmpty(videos)) return Path.Combine(videos, "VidShrink");
        return Environment.CurrentDirectory;
    }

    /// <summary>Pencerenin adlandırması: <c>kayit_&lt;damga&gt;</c>, aynı saniyede ikinci kayıt sayı alır.</summary>
    public string OutputPath(DateTime now, RecorderContainer container)
    {
        var folder = ResolveFolder();
        var extension = "." + RecorderArguments.Extension(container);
        var stem = "kayit_" + Bicim.DosyaDamgasi(now);
        var candidate = Path.Combine(folder, stem + extension);
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }
}

/// <summary>Kayıt isteğinin kurulduğu makine: platform, listelenen cihazlar ve çalışan donanım kodlayıcıları.</summary>
public sealed record CliRecordMachine(
    RecorderPlatform Platform,
    IReadOnlyList<AudioCaptureDevice> AudioDevices,
    IReadOnlyList<string> Cameras,
    IReadOnlyList<string> WorkingEncoders,
    int ProcessorCount)
{
    public static RecorderPlatform HostPlatform =>
        OperatingSystem.IsWindows() ? RecorderPlatform.Windows
        : OperatingSystem.IsMacOS() ? RecorderPlatform.MacOs
        : RecorderPlatform.Linux;
}

/// <summary>Kurulan kayıt: istek, çıktı yolu ve kayda girmeyen kayıtlı cihazların notları (anahtar, ad).</summary>
public sealed record CliRecordPlan(RecorderRequest Request, string OutputPath, IReadOnlyList<(string Key, string Argument)> Notes);

/// <summary>
/// <c>kaydet</c> komutu: kaydedicinin kayıtlı ayarlarından pencere açmadan kayıt. İstek pencerenin
/// <c>RecorderView.BuildRequest</c> yoluyla aynı sırayla kurulur (elle kip, otomatik kip ve bütçe, kodeğe
/// uydurma); süre <c>--sure</c> ile verilirse kaydın <c>-t</c> sınırı olur, verilmezse kayıt Ctrl+C ile biter.
/// Yakalama Windows'ta <c>gdigrab</c>'dır: <c>ddagrab</c> yoklaması ve monitör listesi pencerenin işidir.
/// </summary>
public static class CliRecord
{
    public const double MaxSeconds = 24 * 3600;

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var request = new CliRequest { Command = CliCommand.Record };
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--yardim" or "--help":
                    return CliParser.Success(new CliRequest { Command = CliCommand.Help });
                case "--sure" or "--duration":
                    if (!CliParser.TryValue(args, ref i, out var sure)) return CliParser.Fail("error.missing-value", arg);
                    if (!CliParser.TryParseClock(sure, out var seconds) || seconds <= 0 || seconds > MaxSeconds)
                        return CliParser.Fail("error.record-duration", sure);
                    request = request with { RecordSeconds = seconds };
                    break;
                case "--cikti" or "--output" or "-o":
                    if (!CliParser.TryValue(args, ref i, out var output)) return CliParser.Fail("error.missing-value", arg);
                    if (RecorderArguments.ContainerOf(output) is null) return CliParser.Fail("error.record-output-extension", output);
                    request = request with { Output = output };
                    break;
                case "--ayarlar" or "--settings":
                    if (!CliParser.TryValue(args, ref i, out var ayarlar)) return CliParser.Fail("error.missing-value", arg);
                    request = request with { RecordSettings = ayarlar };
                    break;
                case "--json":
                    request = request with { Json = true };
                    break;
                default:
                    return CliParser.Fail(arg.StartsWith('-') && arg.Length > 1 ? "error.unknown-option" : "error.record-extra", arg);
            }
        }

        return CliParser.Success(request);
    }

    /// <summary>
    /// Kayıtlı ayardan istek. Hata dönerse <paramref name="errorKey"/> doludur. Kayıtlı ses ya da kamera
    /// cihazı listede yoksa pencere gibi o kol sessize düşer; fark notlarda söylenir.
    /// </summary>
    public static CliRecordPlan? Build(CliRequest cli, CliRecordSettings saved, CliRecordMachine machine, DateTime now,
        out string? errorKey, out string? errorArgument)
    {
        errorKey = null;
        errorArgument = null;
        var notes = new List<(string, string)>();

        if (saved.Target == RecorderTargetKind.Window && machine.Platform != RecorderPlatform.Windows)
        {
            errorKey = "error.record-window-platform";
            return null;
        }

        AudioCaptureDevice? Pick(AudioSourceRole role, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var found = machine.AudioDevices.FirstOrDefault(d => d.Role == role && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found is null) notes.Add(("record.device-missing", name));
            return found;
        }

        var selection = new AudioCaptureSelection(
            Pick(AudioSourceRole.Microphone, saved.MicrophoneName),
            Pick(AudioSourceRole.SystemAudio, saved.SystemAudioName));
        var audio = AudioCapturePlan.Silent;
        if (selection.Count > 0
            && !AudioCaptureArguments.TryBuild(selection, machine.AudioDevices, RecorderArguments.AudioFirstInputIndexFor(saved.Target),
                saved.AudioLayout, new AudioFilterOptions(saved.AudioGainDb, saved.AudioNoiseGate, saved.AudioNoiseSuppression, saved.AudioCompressor, saved.AudioLimiter),
                out audio, out var reason))
        {
            errorKey = "error.record-audio";
            errorArgument = reason ?? string.Empty;
            return null;
        }

        RecorderWebcam? webcam = null;
        if (!string.IsNullOrEmpty(saved.WebcamName))
        {
            var camera = machine.Cameras.FirstOrDefault(c => string.Equals(c, saved.WebcamName, StringComparison.OrdinalIgnoreCase));
            if (camera is null) notes.Add(("record.device-missing", saved.WebcamName!));
            else
                webcam = new RecorderWebcam(camera, RecorderArguments.DefaultWebcamWidth, WebcamCorner.BottomRight, saved.WebcamBackground)
                {
                    Shape = saved.WebcamShape,
                    Mirror = saved.WebcamMirror,
                    Placement = new WebcamPlacement(saved.WebcamX, saved.WebcamY, saved.WebcamSize)
                };
        }

        var region = saved.Target == RecorderTargetKind.Region
            ? new RecorderRegion(saved.RegionX, saved.RegionY, saved.RegionWidth, saved.RegionHeight)
            : null;
        var bitrate = saved.RateControl == RecorderRateControl.Bitrate;
        var request = new RecorderRequest
        {
            Platform = machine.Platform,
            Target = saved.Target,
            Fps = saved.Fps,
            Quality = saved.Quality,
            ShowCursor = saved.ShowCursor,
            ShowClicks = machine.Platform == RecorderPlatform.MacOs && saved.ShowClicks,
            VideoCodec = saved.Codec,
            Preset = saved.Preset,
            WindowTitle = string.IsNullOrWhiteSpace(saved.WindowTitle) ? null : saved.WindowTitle,
            Region = region,
            Audio = audio,
            Webcam = webcam,
            Container = saved.Container,
            ScreenIndex = saved.Target == RecorderTargetKind.Screen ? saved.ScreenIndex : 0,
            PrivacyMasks = saved.PrivacyMasks,
            Scale = saved.ScaleWidth > 0 && saved.ScaleHeight > 0 ? new RecorderScale(saved.ScaleWidth, saved.ScaleHeight) : null,
            KeyframeSeconds = saved.KeyframeSeconds,
            Profile = string.IsNullOrWhiteSpace(saved.Profile) ? null : saved.Profile,
            Tune = string.IsNullOrWhiteSpace(saved.Tune) ? null : saved.Tune,
            RateControl = saved.RateControl,
            FrameRateMode = saved.FrameRateMode,
            BitrateKbps = bitrate && saved.BitrateKbps > 0 ? saved.BitrateKbps : null,
            MaxBitrateKbps = bitrate && saved.MaxBitrateKbps > 0 ? saved.MaxBitrateKbps : null,
            BufferKbits = bitrate && saved.BufferKbits > 0 ? saved.BufferKbits : null,
            PixelFormat = saved.PixelFormat,
            ColorSpace = string.IsNullOrWhiteSpace(saved.ColorSpace) ? null : saved.ColorSpace,
            ColorRange = string.IsNullOrWhiteSpace(saved.ColorRange) ? null : saved.ColorRange,
            HdrToSdr = saved.HdrToSdr,
            MaxDuration = saved.MaxDurationSeconds > 0 ? TimeSpan.FromSeconds(saved.MaxDurationSeconds) : null,
            Split = saved.SplitSeconds > 0 || saved.SplitMegabytes > 0
                ? new RecorderSplit(saved.SplitSeconds > 0 ? TimeSpan.FromSeconds(saved.SplitSeconds) : null, saved.SplitMegabytes > 0 ? saved.SplitMegabytes : null)
                : null
        };

        if (!saved.ManualMode) request = Auto(request, saved, machine, selection.Count);

        var container = cli.Output is { } named ? RecorderArguments.ContainerOf(named)!.Value : request.Container;
        request = FitToCodec(request) with
        {
            Container = container,
            AudioCodec = RecorderArguments.AudioCodecsFor(container).Contains(saved.AudioCodec, StringComparer.Ordinal) ? saved.AudioCodec : RecorderArguments.DefaultAudioCodec
        };
        if (cli.RecordSeconds is { } limit) request = request with { MaxDuration = TimeSpan.FromSeconds(limit) };

        var output = cli.Output is { } path ? Path.GetFullPath(path) : saved.OutputPath(now, container);
        var errors = RecorderArguments.Validate(request, output);
        if (errors.Count > 0)
        {
            errorKey = "error.record-invalid";
            errorArgument = string.Join("; ", errors);
            return null;
        }

        return new CliRecordPlan(request, output, notes);
    }

    /// <summary>Pencerenin otomatik kipi: merdivenin ilk adayı, kayıtlı kap, sonra hedef bütçe ya da hedef süre.</summary>
    private static RecorderRequest Auto(RecorderRequest request, CliRecordSettings saved, CliRecordMachine machine, int audioTracks)
    {
        var width = request.Region?.Width ?? 0;
        var height = request.Region?.Height ?? 0;
        var candidates = RecorderAutoPlan.Candidates(new RecorderMachine(
            width, height, 0, machine.ProcessorCount, machine.WorkingEncoders,
            machine.Platform == RecorderPlatform.Windows ? RecorderAutoPlan.GdigrabMaxFps : 0));
        var planned = RecorderAutoPlan.Apply(request, candidates[0]);
        request = planned with
        {
            Container = saved.Container,
            Fps = saved.Container == RecorderContainer.Gif ? Math.Min(planned.Fps, GifPalette.MaxFps) : planned.Fps
        };

        var cap = saved.Container == RecorderContainer.Gif ? null : saved.TargetMegabytes;
        var budget = RecorderBudget.From(saved.TargetMegabytes, saved.TargetSeconds, audioTracks);
        if (budget.Verdict == RecorderBudgetVerdict.Usable)
            return RecorderAutoPlan.ApplyBudget(request, budget) with
            {
                MaxDuration = TimeSpan.FromSeconds(saved.TargetSeconds ?? 0),
                MaxMegabytes = cap
            };

        if (budget.Verdict != RecorderBudgetVerdict.NotRequested) return request;

        return request with
        {
            MaxDuration = saved.TargetSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : request.MaxDuration,
            MaxMegabytes = cap ?? request.MaxMegabytes
        };
    }

    private static RecorderRequest FitToCodec(RecorderRequest request)
    {
        static bool Has(IReadOnlyList<string> values, string? value)
            => value is not null && values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));

        var formats = RecorderArguments.PixelFormatsFor(request.VideoCodec);
        var pixel = formats.Count == 0 || Has(formats, request.PixelFormat)
            ? request.PixelFormat
            : Has(formats, RecorderArguments.DefaultPixelFormat) ? RecorderArguments.DefaultPixelFormat : formats[0];
        return request with
        {
            Profile = Has(RecorderArguments.ProfilesFor(request.VideoCodec), request.Profile) ? request.Profile : null,
            Tune = Has(RecorderArguments.TunesFor(request.VideoCodec), request.Tune) ? request.Tune : null,
            PixelFormat = pixel
        };
    }

    internal static async Task<int> RunAsync(CliRequest request, CliServices services, TextWriter stdout, TextWriter stderr,
        CliText text, CancellationToken ct)
    {
        var settingsPath = request.RecordSettings is { } given ? Path.GetFullPath(given) : services.RecorderSettingsPath();
        if (request.RecordSettings is not null && !File.Exists(settingsPath))
        {
            stderr.WriteLine(text.Format("error.record-settings-missing", settingsPath));
            return ExitCodes.Error;
        }

        var saved = CliRecordSettings.Load(settingsPath);
        var plan = Build(request, saved, services.RecordMachine(), services.Now(), out var errorKey, out var errorArgument);
        if (plan is null)
        {
            stderr.WriteLine(text.Format(errorKey!, errorArgument));
            stderr.WriteLine(text["usage.hint"]);
            return ExitCodes.Usage;
        }

        foreach (var (key, argument) in plan.Notes) stderr.WriteLine(text.Format(key, argument));
        var folder = Path.GetDirectoryName(plan.OutputPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        stderr.WriteLine(request.Json
            ? CliProgress.StageLine("record")
            : text.Format("record.started", plan.OutputPath));
        var started = DateTime.UtcNow;
        RecordResult? result = null;
        string? failure = null;
        try
        {
            result = await services.Record(plan.Request, plan.OutputPath, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            failure = ex.Message;
        }
        var elapsed = DateTime.UtcNow - started;

        var exit = result is { Ok: true } ? ExitCodes.InBand : ExitCodes.Error;
        if (request.Json)
            stdout.Write(ResultJson(plan, result, exit, elapsed));
        else if (result is not null)
        {
            foreach (var file in result.Files is { Count: > 0 } files ? files : new[] { result.OutputPath })
                stdout.WriteLine(text.Format("result.output", file));
            stdout.WriteLine(text.Format("convert.size", result.OutputMb.ToString("0.0", text.Culture)));
            stdout.WriteLine(text.Format("result.duration", elapsed.TotalSeconds < 60 ? elapsed.TotalSeconds.ToString("0.0", text.Culture) + " s" : Saat.Ekran(elapsed)));
            if (result.Partial) stderr.WriteLine(text["record.partial"]);
        }
        if (failure is not null) stderr.WriteLine(text.Format("error.failed", failure));
        else if (result is { Ok: false }) stderr.WriteLine(text.Format("error.failed", result.DeliveryError ?? result.StandardError));
        return exit;
    }

    /// <summary>
    /// Varsayılan kayıt: oturum açılır, kendi sınırıyla biter ya da Ctrl+C gelince nazikçe durdurulur.
    /// Durdurma iptal belirtecini almaz; iptal edilen komutun da dosyası kapanır ve teslim edilir.
    /// </summary>
    public static async Task<RecordResult> RecordAsync(RecorderRequest request, string outputPath, CancellationToken ct)
    {
        await using var session = await RecorderSession.StartAsync(request, outputPath, null, ct);
        var cancelled = new TaskCompletionSource();
        await using (ct.Register(() => cancelled.TrySetResult()))
            await Task.WhenAny(session.Ended, cancelled.Task);
        return await session.StopAsync();
    }

    private static string ResultJson(CliRecordPlan plan, RecordResult? result, int exit, TimeSpan elapsed)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("command", "kaydet");
            writer.WriteString("output", result?.OutputPath ?? plan.OutputPath);
            writer.WriteString("container", RecorderArguments.Extension(plan.Request.Container));
            writer.WriteString("target", plan.Request.Target.ToString());
            writer.WriteString("videoCodec", plan.Request.VideoCodec);
            if (plan.Request.MaxDuration is { } limit) writer.WriteNumber("durationLimitSeconds", limit.TotalSeconds);
            else writer.WriteNull("durationLimitSeconds");
            writer.WriteStartArray("files");
            foreach (var file in result?.Files ?? (result is null ? Array.Empty<string>() : new[] { result.OutputPath })) writer.WriteStringValue(file);
            writer.WriteEndArray();
            writer.WriteStartObject("result");
            writer.WriteNumber("exitCode", exit);
            writer.WriteBoolean("success", result is { Ok: true });
            writer.WriteBoolean("partial", result?.Partial ?? false);
            if (result is null) writer.WriteNull("outputMb"); else writer.WriteNumber("outputMb", Math.Round(result.OutputMb, 3));
            writer.WriteNumber("elapsedSeconds", Math.Round(elapsed.TotalSeconds, 1));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }
}
