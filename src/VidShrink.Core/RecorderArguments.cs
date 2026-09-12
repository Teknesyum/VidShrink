namespace VidShrink.Core;

/// <summary>
/// Yakalama girdisini hangi platformun verdigi. ffmpeg'in ekran girdisi platform basina
/// ayri bir demuxer: Windows <c>gdigrab</c>, macOS <c>avfoundation</c>, Linux <c>x11grab</c>.
/// Kosan makineden degil <b>istekten</b> okunuyor: boylece uc platformun argumani tek bir
/// makinede olculebiliyor.
/// </summary>
public enum RecorderPlatform
{
    /// <summary><c>gdigrab</c>: butun masaustu, pencere basligi ya da ofsetli bolge.</summary>
    Windows,

    /// <summary><c>avfoundation</c>: ekran ve cihaz indeksi; bolge girdi degil kirpma filtresi.</summary>
    MacOs,

    /// <summary><c>x11grab</c>: ekran adresi, <c>+x,y</c> ofseti ya da <c>-window_id</c>.</summary>
    Linux
}

/// <summary>Kullanicinin ne kaydettigi. Uc secim de ayni ureticiden cikar.</summary>
public enum RecorderTargetKind
{
    /// <summary>Ekranin tamami.</summary>
    Screen,

    /// <summary>Tek bir pencere: Windows'ta baslik, Linux'ta pencere kimligi.</summary>
    Window,

    /// <summary>Ekranin bir dikdortgeni.</summary>
    Region
}

/// <summary>Kaydedilecek dikdortgen. Olculer <c>yuv420p</c> icin cift olmak zorunda.</summary>
public sealed record RecorderRegion(int X, int Y, int Width, int Height);

/// <summary>
/// Bir ekran kaydi istegi. Cikti yolu burada durmaz: onu
/// <see cref="RecorderArguments.Build"/> alir, cunku duraklatilan kayit birden fazla parca
/// dosyasi yaziyor ve her parca ayni istekten uretiliyor.
/// </summary>
public sealed record RecorderRequest
{
    /// <summary>Girdiyi hangi platformun demuxer'i verecek.</summary>
    public required RecorderPlatform Platform { get; init; }

    /// <summary>Ekran, pencere ya da bolge.</summary>
    public required RecorderTargetKind Target { get; init; }

    /// <summary>Yakalama kare hizi.</summary>
    public int Fps { get; init; } = RecorderArguments.DefaultFps;

    /// <summary>Fare imleci kayda girsin mi (<c>-draw_mouse</c> / <c>-capture_cursor</c>).</summary>
    public bool ShowCursor { get; init; } = true;

    /// <summary>
    /// macOS'ta <c>avfoundation</c> ekran indeksi, Linux'ta <see cref="Display"/> ile
    /// birlikte ekran numarasi. <c>gdigrab</c> ekran secmiyor: orada ekran secimi ofsetli
    /// bolgedir, bu yuzden Windows'ta sifirdan farkli indeks reddedilir.
    /// </summary>
    public int ScreenIndex { get; init; }

    /// <summary>Linux ekran adresi; verilmezse <see cref="RecorderArguments.DefaultDisplay"/>.</summary>
    public string? Display { get; init; }

    /// <summary>Windows'ta pencere basligi (<c>-i title=...</c>).</summary>
    public string? WindowTitle { get; init; }

    /// <summary>Linux'ta pencere kimligi (<c>-window_id</c>).</summary>
    public string? WindowId { get; init; }

    /// <summary>Bolge kaydinda dikdortgen.</summary>
    public RecorderRegion? Region { get; init; }

    /// <summary>Goruntu kodlayicisi.</summary>
    public string VideoCodec { get; init; } = RecorderArguments.DefaultVideoCodec;

    /// <summary>Kodlayici on ayari.</summary>
    public string Preset { get; init; } = RecorderArguments.DefaultPreset;

    /// <summary>Kalite degeri; <see cref="CodecModel.QualityArgs"/> onu saticinin olcegine cevirir.</summary>
    public double Quality { get; init; } = RecorderArguments.DefaultQuality;

    /// <summary>
    /// Ses girdisinin argumanlari. Bu kol ses cihazi <b>listelemez</b> ve arguman uretmez:
    /// onlari 8c kendi dosyasinda uretiyor, burasi yalnizca girdiden sonra araya koyuyor.
    /// Bos oldugunda kayit sessiz olur ve <c>-an</c> yazilir.
    /// </summary>
    public IReadOnlyList<string>? AudioInputArgs { get; init; }
}

/// <summary>
/// Ekran kaydi argumanlarini uretir. Kosturmaz — surec surmeyi
/// <c>VidShrink.Ffmpeg.RecorderSession</c> biliyor, burasi yalnizca ne kosacagini soyluyor.
/// <para>
/// Uretilen listede <c>-nostdin</c> <b>yoktur</b>. Deponun oteki cagrilarinin nerdeyse hepsi
/// onu veriyor (<c>ClipExport</c>, <c>SegmentEncoder</c>, <c>FrameGrabber</c>,
/// <c>ComplexityProbe</c>); kaydin nazik durdurma yolu ise tam olarak stdin'e yazilan
/// <c>q</c>'dur, o yuzden bu kolda stdin kapatilmaz. <c>-progress</c> bayraklari da burada
/// degil: onlari okuyan taraf ekliyor.
/// </para>
/// </summary>
public static class RecorderArguments
{
    /// <summary>Varsayilan yakalama kare hizi.</summary>
    public const int DefaultFps = 30;

    /// <summary>Varsayilan goruntu kodlayicisi.</summary>
    public const string DefaultVideoCodec = "libx264";

    /// <summary>Varsayilan on ayar: kayit gercek zamanli, kodlama kareyi bekletemez.</summary>
    public const string DefaultPreset = "veryfast";

    /// <summary>Varsayilan kalite degeri.</summary>
    public const double DefaultQuality = 23;

    /// <summary>Linux'ta ekran adresi verilmediginde kullanilan adres.</summary>
    public const string DefaultDisplay = ":0.0";

    /// <summary>Kabul edilen en yuksek kare hizi.</summary>
    public const int MaxFps = 240;

    private static readonly string[] KnownVideoCodecs =
    {
        "libx264", "libx265", "libsvtav1", "libvpx-vp9",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv",
        "h264_amf", "hevc_amf"
    };

    /// <summary>
    /// Istegin kabul edilemez taraflari. Bos liste "kosturulabilir" demektir; uydurma bir
    /// kodek adi ya da eksik pencere secimi sessizce yutulmaz.
    /// </summary>
    public static IReadOnlyList<string> Validate(RecorderRequest request, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(outputPath)) errors.Add("Output path is required.");
        if (request.Fps <= 0) errors.Add("Frame rate must be greater than zero.");
        if (request.Fps > MaxFps) errors.Add($"Frame rate must not exceed {MaxFps}.");
        if (request.ScreenIndex < 0) errors.Add("Screen index cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Preset)) errors.Add("Encoder preset is required.");
        if (!KnownVideoCodecs.Contains(request.VideoCodec, StringComparer.OrdinalIgnoreCase))
            errors.Add($"The {request.VideoCodec} video encoder is not one of the recorder's measured encoders.");

        if (request.Target == RecorderTargetKind.Region)
        {
            if (request.Region is null) errors.Add("Region capture needs a rectangle.");
            else
            {
                if (request.Region.Width <= 0 || request.Region.Height <= 0)
                    errors.Add("Region dimensions must be positive.");
                else if (request.Region.Width % 2 != 0 || request.Region.Height % 2 != 0)
                    errors.Add("Region dimensions must be even for the selected pixel format.");
                if (request.Region.X < 0 || request.Region.Y < 0)
                    errors.Add("Region offsets cannot be negative.");
            }
        }

        if (request.Target == RecorderTargetKind.Window)
            errors.AddRange(WindowErrors(request));

        if (request.Platform == RecorderPlatform.Windows && request.ScreenIndex != 0)
            errors.Add("gdigrab captures the whole desktop and does not take a screen index; select a screen with a region offset instead.");

        return errors;
    }

    private static IEnumerable<string> WindowErrors(RecorderRequest request) => request.Platform switch
    {
        RecorderPlatform.Windows => string.IsNullOrWhiteSpace(request.WindowTitle)
            ? new[] { "Window capture on gdigrab needs the window title." }
            : Array.Empty<string>(),
        RecorderPlatform.Linux => string.IsNullOrWhiteSpace(request.WindowId)
            ? new[] { "Window capture on x11grab needs the window id." }
            : Array.Empty<string>(),
        RecorderPlatform.MacOs => new[]
        {
            "avfoundation exposes screens and devices, not single windows; capture a region of the screen instead."
        },
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// Kosturulacak arguman listesi. Istek kabul edilemezse
    /// <see cref="InvalidOperationException"/> atilir; sebepler
    /// <see cref="Validate"/>'in dondurdugu satirlardir.
    /// </summary>
    public static IReadOnlyList<string> Build(RecorderRequest request, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = Validate(request, outputPath);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var a = new List<string> { "-hide_banner", "-y" };
        a.AddRange(Input(request));

        var hasAudio = request.AudioInputArgs is { Count: > 0 };
        if (hasAudio) a.AddRange(request.AudioInputArgs!);

        if (Crop(request) is { } crop) a.AddRange(new[] { "-vf", crop });

        a.AddRange(new[] { "-c:v", request.VideoCodec, "-preset", request.Preset });
        a.AddRange(CodecModel.QualityArgs(request.VideoCodec, request.Quality));
        a.AddRange(new[] { "-pix_fmt", "yuv420p" });

        if (hasAudio) a.AddRange(new[] { "-c:a", "aac", "-b:a", "160k" });
        else a.Add("-an");

        if (Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant() is "mp4" or "mov")
            a.AddRange(new[] { "-movflags", "+faststart" });

        a.Add(outputPath);
        return a;
    }

    private static IReadOnlyList<string> Input(RecorderRequest request) => request.Platform switch
    {
        RecorderPlatform.Windows => WindowsInput(request),
        RecorderPlatform.MacOs => MacInput(request),
        RecorderPlatform.Linux => LinuxInput(request),
        _ => throw new InvalidOperationException($"Unknown recorder platform: {request.Platform}.")
    };

    private static IReadOnlyList<string> WindowsInput(RecorderRequest request)
    {
        var a = new List<string> { "-f", "gdigrab", "-framerate", Number(request.Fps), "-draw_mouse", Flag(request.ShowCursor) };

        if (request.Target == RecorderTargetKind.Region && request.Region is { } region)
            a.AddRange(new[]
            {
                "-offset_x", Number(region.X),
                "-offset_y", Number(region.Y),
                "-video_size", Size(region)
            });

        a.AddRange(new[] { "-i", request.Target switch
        {
            RecorderTargetKind.Window => $"title={request.WindowTitle}",
            RecorderTargetKind.Screen => "desktop",
            RecorderTargetKind.Region => "desktop",
            _ => "desktop"
        } });

        return a;
    }

    private static IReadOnlyList<string> MacInput(RecorderRequest request) => new[]
    {
        "-f", "avfoundation",
        "-framerate", Number(request.Fps),
        "-capture_cursor", Flag(request.ShowCursor),
        "-i", $"{request.ScreenIndex}:none"
    };

    private static IReadOnlyList<string> LinuxInput(RecorderRequest request)
    {
        var a = new List<string> { "-f", "x11grab", "-framerate", Number(request.Fps), "-draw_mouse", Flag(request.ShowCursor) };
        var display = string.IsNullOrWhiteSpace(request.Display) ? DefaultDisplay : request.Display!;

        if (request.Target == RecorderTargetKind.Region && request.Region is { } region)
        {
            a.AddRange(new[] { "-video_size", Size(region) });
            a.AddRange(new[] { "-i", $"{display}+{Number(region.X)},{Number(region.Y)}" });
            return a;
        }

        if (request.Target == RecorderTargetKind.Window)
            a.AddRange(new[] { "-window_id", request.WindowId! });

        a.AddRange(new[] { "-i", display });
        return a;
    }

    /// <summary>
    /// macOS'ta bolge girdi secenegi degil: <c>avfoundation</c> ekranin tamamini veriyor,
    /// dikdortgen kirpma filtresiyle aliniyor. Oteki iki platformda bolge zaten girdide.
    /// </summary>
    private static string? Crop(RecorderRequest request)
        => request is { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Region, Region: { } region }
            ? $"crop={Number(region.Width)}:{Number(region.Height)}:{Number(region.X)}:{Number(region.Y)}"
            : null;

    private static string Size(RecorderRegion region) => $"{Number(region.Width)}x{Number(region.Height)}";

    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "1" : "0";
}
