using System.Globalization;

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

/// <summary>
/// Kaydin yazildigi kap. Uzanti burdan cikar ve <see cref="RecorderArguments.Validate"/>
/// cikti yolunun uzantisiyla bu secimin ayrismasini reddeder.
/// <para>
/// <c>mkv</c> kolunun varlik sebebi <see cref="RecorderArguments"/> degil oturumun
/// olduruldugu yol: mp4/mov muxer'i <c>moov</c> atomunu kapanista yaziyor, surec
/// oldurulunce dosya oynatilamaz kaliyor. Matroska her kumeyi kendi basina kapatiyor,
/// yarim kalan dosya sona kadar oynuyor.
/// </para>
/// </summary>
public enum RecorderContainer
{
    /// <summary><c>.mp4</c> — en genis uyum, nazik olmayan durdurmada bozuluyor.</summary>
    Mp4,

    /// <summary><c>.mkv</c> — oldurulen kayit oynatilabilir kaliyor.</summary>
    Mkv,

    /// <summary><c>.mov</c> — mp4 ile ayni muxer ailesi, ayni kirilganlik.</summary>
    Mov
}

/// <summary>
/// Goruntu kolunun hangi olcege bagli oldugu. Iki kol da ayni istekten cikiyor; hangisinin
/// yazildigini <see cref="RecorderRequest.RateControl"/> soyler.
/// </summary>
public enum RecorderRateControl
{
    /// <summary>Kalite tabanli kol: <see cref="CodecModel.QualityArgs"/>.</summary>
    Quality,

    /// <summary>Hedef bit hizi kolu: <c>-b:v</c> ve <see cref="CodecModel.BitrateRateControlArgs"/>.</summary>
    Bitrate
}

/// <summary>Kaydedilecek dikdortgen. Olculer <c>yuv420p</c> icin cift olmak zorunda.</summary>
public sealed record RecorderRegion(int X, int Y, int Width, int Height);

/// <summary>
/// Ciktinin yeniden olceklenecegi boyut (<c>-vf scale=W:H</c>). macOS'ta var olan
/// <c>crop</c> filtresiyle ayni zincire giriyor, kirpmadan sonra.
/// </summary>
public sealed record RecorderScale(int Width, int Height);

/// <summary>
/// Tek bir monitorun masaustu koordinatlarindaki yeri. <c>gdigrab</c> ekran indeksi
/// almiyor; ekran secimi bu sinirin ofsetli bolgeye cevrilmesiyle oluyor. Liste isteme
/// disaridan veriliyor (arayuzde Avalonia'nin ekran listesi), boylece motor pencere
/// sistemine bagli kalmiyor ve secim ekransiz makinede de olculebiliyor.
/// </summary>
public sealed record ScreenBounds(int Index, int X, int Y, int Width, int Height);

/// <summary>
/// Kaydin kendiliginden bolunme olcutu. Ikisi birden verilebilir; once dolan boler.
/// Bolme acikken parcalar <b>birlestirilmez</b>, ayri dosyalar olarak teslim edilir.
/// </summary>
public sealed record RecorderSplit(TimeSpan? Duration = null, double? Megabytes = null)
{
    /// <summary>En az bir olcut verilmis mi.</summary>
    public bool IsSet => Duration is not null || Megabytes is not null;
}

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
    /// bolgedir. Windows'ta sifirdan farkli indeks yalniz <see cref="Screens"/> o indeksi
    /// tasiyorsa kabul edilir, o zaman bolgeye cevrilir.
    /// </summary>
    public int ScreenIndex { get; init; }

    /// <summary>
    /// Masaustundeki monitorlerin sinirlari. Windows'ta ikinci monitoru secmenin tek yolu;
    /// bos birakilirsa <see cref="ScreenIndex"/> sifirdan farkli olamaz.
    /// </summary>
    public IReadOnlyList<ScreenBounds> Screens { get; init; } = Array.Empty<ScreenBounds>();

    /// <summary>Linux ekran adresi; verilmezse <see cref="RecorderArguments.DefaultDisplay"/>.</summary>
    public string? Display { get; init; }

    /// <summary>Windows'ta pencere basligi (<c>-i title=...</c>).</summary>
    public string? WindowTitle { get; init; }

    /// <summary>Linux'ta pencere kimligi (<c>-window_id</c>).</summary>
    public string? WindowId { get; init; }

    /// <summary>Bolge kaydinda dikdortgen.</summary>
    public RecorderRegion? Region { get; init; }

    /// <summary>Kaydin yazildigi kap; cikti uzantisi bununla ayni olmak zorunda.</summary>
    public RecorderContainer Container { get; init; } = RecorderContainer.Mp4;

    /// <summary>Goruntu kodlayicisi.</summary>
    public string VideoCodec { get; init; } = RecorderArguments.DefaultVideoCodec;

    /// <summary>Kodlayici on ayari.</summary>
    public string Preset { get; init; } = RecorderArguments.DefaultPreset;

    /// <summary>Hangi hiz kontrolu kolunun yazilacagi.</summary>
    public RecorderRateControl RateControl { get; init; } = RecorderRateControl.Quality;

    /// <summary>Kalite degeri; <see cref="CodecModel.QualityArgs"/> onu saticinin olcegine cevirir.</summary>
    public double Quality { get; init; } = RecorderArguments.DefaultQuality;

    /// <summary>
    /// Hedef goruntu bit hizi, kbit/s. <see cref="RecorderRateControl.Bitrate"/> kolunda
    /// <b>zorunlu</b>: varsayilani yok, cunku bu depoda kayit icin olculmus bir hedef bit
    /// hizi yok. Kullanici sayiyi verir, motor uydurmaz.
    /// </summary>
    public int? BitrateKbps { get; init; }

    /// <summary>Tavan bit hizi (<c>-maxrate</c>); verilirse <see cref="BitrateKbps"/>'den kucuk olamaz.</summary>
    public int? MaxBitrateKbps { get; init; }

    /// <summary>Tampon boyu (<c>-bufsize</c>), kbit.</summary>
    public int? BufferKbits { get; init; }

    /// <summary>
    /// Iki anahtar kare arasindaki sure, saniye. <c>-g</c> degeri bundan ve
    /// <see cref="Fps"/>'ten cikar. Sifir yazilirsa <c>-g</c> hic yazilmaz ve kodlayici
    /// kendi araligini secer.
    /// </summary>
    public int KeyframeSeconds { get; init; } = RecorderArguments.DefaultKeyframeSeconds;

    /// <summary>Kodlayici profili (<c>-profile:v</c>); kodegin tanidiklarindan biri olmak zorunda.</summary>
    public string? Profile { get; init; }

    /// <summary>Kodlayici ayari (<c>-tune</c>); kodegin tanidiklarindan biri olmak zorunda.</summary>
    public string? Tune { get; init; }

    /// <summary>Ciktinin yeniden olceklenecegi boyut; bos birakilirsa yakalama boyutu korunur.</summary>
    public RecorderScale? Scale { get; init; }

    /// <summary>Piksel bicimi (<c>-pix_fmt</c>).</summary>
    public string PixelFormat { get; init; } = RecorderArguments.DefaultPixelFormat;

    /// <summary>Renk uzayi (<c>-colorspace</c>); bos birakilirsa yazilmaz.</summary>
    public string? ColorSpace { get; init; }

    /// <summary>Renk araligi (<c>-color_range</c>); bos birakilirsa yazilmaz.</summary>
    public string? ColorRange { get; init; }

    /// <summary>Kaydin kendiliginden duracagi sure (<c>-t</c>); bos birakilirsa sinir yok.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>
    /// Kendiliginden bolme olcutu. Arguman uretimine girmez — parcalari
    /// <c>VidShrink.Ffmpeg.RecorderSession</c> aciyor; burasi yalnizca olcutu dogruluyor.
    /// </summary>
    public RecorderSplit? Split { get; init; }

    /// <summary>
    /// Ses kolu. Bu dosya ses cihazi <b>listelemez</b> ve arguman uretmez: plani
    /// <see cref="AudioCaptureArguments"/> uretiyor, burasi yalnizca girdiden sonra araya
    /// koyuyor, grafigini ve eslemelerini yaziyor. Bos oldugunda kayit sessiz olur ve
    /// <c>-an</c> yazilir. Birden fazla esleme varsa her ize kendi <c>-c:a:N</c>'i yazilir.
    /// </summary>
    public AudioCapturePlan? Audio { get; init; }
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

    /// <summary>
    /// Varsayilan anahtar kare araligi, saniye. Olculmus bir sayi degil, kayit kolunun
    /// yaygin degeri: iki saniye hem aramayi iki saniyeden uzun bekletmiyor hem de her
    /// kareyi anahtar yapmanin boyutunu odemiyor. Kullanici degistirebiliyor.
    /// </summary>
    public const int DefaultKeyframeSeconds = 2;

    /// <summary>Kabul edilen en uzun anahtar kare araligi, saniye.</summary>
    public const int MaxKeyframeSeconds = 60;

    /// <summary>Varsayilan piksel bicimi.</summary>
    public const string DefaultPixelFormat = "yuv420p";

    /// <summary>Ses izi basina yazilan kodlayici.</summary>
    public const string AudioCodec = "aac";

    /// <summary>Ses izi basina yazilan bit hizi.</summary>
    public const string AudioBitrate = "160k";

    /// <summary>Linux'ta ekran adresi verilmediginde kullanilan adres.</summary>
    public const string DefaultDisplay = ":0.0";

    /// <summary>Kabul edilen en yuksek kare hizi.</summary>
    public const int MaxFps = 240;

    /// <summary>
    /// Ses girdilerinin ffmpeg girdi sirasindaki ilk numarasi. Yakalama girdisi her zaman
    /// 0 oldugu icin 1; <see cref="AudioCaptureArguments.Build"/>in ucuncu argumani bu
    /// olmak zorunda, yoksa <c>amix</c> grafigi var olmayan bir girdiye bakar. Sayi
    /// arayuzde uydurulmuyor, motordan okunuyor.
    /// </summary>
    public const int AudioFirstInputIndex = 1;

    private static readonly string[] KnownVideoCodecs =
    {
        "libx264", "libx265", "libsvtav1", "libvpx-vp9",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv",
        "h264_amf", "hevc_amf"
    };

    /// <summary>
    /// Kabul edilen piksel bicimleri. Kapali kume: uydurma bir bicim ffmpeg'e hic
    /// gitmiyor, cunku SVT-AV1'de olculdugu gibi tanimadigi anahtari sessizce yutan bir
    /// kodlayici "kabul" donduruyor ve hata ancak ciktida goruluyor.
    /// </summary>
    private static readonly string[] KnownPixelFormats =
    {
        "yuv420p", "yuv422p", "yuv444p",
        "yuv420p10le", "yuv422p10le", "yuv444p10le",
        "nv12", "p010le", "rgb24", "bgr0", "gbrp"
    };

    private static readonly string[] KnownColorSpaces =
    {
        "bt709", "bt470bg", "smpte170m", "smpte240m", "bt2020nc", "bt2020c"
    };

    private static readonly string[] KnownColorRanges = { "tv", "pc", "limited", "full" };

    private static readonly string[] H264Profiles =
    {
        "baseline", "main", "high", "high10", "high422", "high444"
    };

    private static readonly string[] HevcProfiles = { "main", "main10", "rext" };

    private static readonly string[] X264Tunes =
    {
        "film", "animation", "grain", "stillimage", "fastdecode", "zerolatency", "psnr", "ssim"
    };

    private static readonly string[] X265Tunes =
    {
        "psnr", "ssim", "grain", "zerolatency", "fastdecode", "animation"
    };

    private static readonly string[] NvencTunes = { "hq", "ll", "ull", "lossless" };

    /// <summary>Kabin dosya uzantisi, noktasiz.</summary>
    public static string Extension(RecorderContainer container) => container switch
    {
        RecorderContainer.Mkv => "mkv",
        RecorderContainer.Mov => "mov",
        _ => "mp4"
    };

    /// <summary>
    /// Kabin adindan kap. Tanimadigi uzantida <c>null</c> doner; cagiran sessizce mp4'e
    /// dusmez.
    /// </summary>
    public static RecorderContainer? ContainerOf(string extensionOrPath)
    {
        if (string.IsNullOrWhiteSpace(extensionOrPath)) return null;
        var extension = extensionOrPath.Contains('.', StringComparison.Ordinal)
            ? Path.GetExtension(extensionOrPath)
            : extensionOrPath;
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "mp4" => RecorderContainer.Mp4,
            "mkv" => RecorderContainer.Mkv,
            "mov" => RecorderContainer.Mov,
            _ => null
        };
    }

    /// <summary>
    /// Nazik olmayan durdurmada dosyayi oynatilabilir birakan kaplar. Bugun yalniz
    /// Matroska: mp4/mov muxer'i <c>moov</c> atomunu kapanista yaziyor.
    /// </summary>
    public static bool SurvivesKill(RecorderContainer container) => container == RecorderContainer.Mkv;

    /// <summary>
    /// Kodegin tanidigi profiller. Bos liste "bu kodege <c>-profile:v</c> verilmez"
    /// demektir ve verilen profil reddedilir.
    /// </summary>
    public static IReadOnlyList<string> ProfilesFor(string codec)
    {
        var c = (codec ?? string.Empty).ToLowerInvariant();
        if (c.Contains("av1", StringComparison.Ordinal)) return Array.Empty<string>();
        if (c.Contains("vp9", StringComparison.Ordinal)) return Array.Empty<string>();
        if (c.Contains("265", StringComparison.Ordinal) || c.Contains("hevc", StringComparison.Ordinal)) return HevcProfiles;
        if (c.Contains("264", StringComparison.Ordinal)) return H264Profiles;
        return Array.Empty<string>();
    }

    /// <summary>Kodegin tanidigi <c>-tune</c> degerleri; bos liste ayari reddeder.</summary>
    public static IReadOnlyList<string> TunesFor(string codec) => (codec ?? string.Empty).ToLowerInvariant() switch
    {
        "libx264" => X264Tunes,
        "libx265" => X265Tunes,
        var c when c.Contains("nvenc", StringComparison.Ordinal) => NvencTunes,
        _ => Array.Empty<string>()
    };

    /// <summary>Kabul edilen piksel bicimleri.</summary>
    public static IReadOnlyList<string> PixelFormats => KnownPixelFormats;

    /// <summary>Kabul edilen renk uzaylari.</summary>
    public static IReadOnlyList<string> ColorSpaces => KnownColorSpaces;

    /// <summary>Kabul edilen renk araliklari.</summary>
    public static IReadOnlyList<string> ColorRanges => KnownColorRanges;

    /// <summary>
    /// Windows'ta bir monitorun <c>gdigrab</c> bolgesi. Ekran indeksi girdi secenegi
    /// olmadigi icin secim ofsete ve boyuta cevriliyor. Monitor listede yoksa <c>null</c>
    /// doner; cagiran sessizce ilk ekrana dusmez.
    /// <para>
    /// Genislik ve yukseklik <c>yuv420p</c> icin asagi yuvarlanarak ciftlenir: tek sayili
    /// bir monitor genisligi (ornegin 1366x768 degil ama 1365 genislikte olceklenmis bir
    /// masaustu) kodlayiciyi kirar.
    /// </para>
    /// </summary>
    public static RecorderRegion? RegionForScreen(IReadOnlyList<ScreenBounds> screens, int index)
    {
        if (screens is null) return null;
        foreach (var screen in screens)
        {
            if (screen.Index != index) continue;
            if (screen.Width <= 1 || screen.Height <= 1) return null;
            return new RecorderRegion(screen.X, screen.Y, screen.Width - (screen.Width % 2), screen.Height - (screen.Height % 2));
        }

        return null;
    }

    /// <summary>
    /// Istegin kabul edilemez taraflari. Bos liste "kosturulabilir" demektir; uydurma bir
    /// kodek adi ya da eksik pencere secimi sessizce yutulmaz.
    /// </summary>
    public static IReadOnlyList<string> Validate(RecorderRequest request, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(outputPath)) errors.Add("Output path is required.");
        else if (ContainerOf(outputPath) is not { } written)
            errors.Add($"The {Path.GetExtension(outputPath)} output extension is not one of the recorder's containers (mp4, mkv, mov).");
        else if (written != request.Container)
            errors.Add($"The output extension says {Extension(written)} but the selected container is {Extension(request.Container)}.");

        if (request.Fps <= 0) errors.Add("Frame rate must be greater than zero.");
        if (request.Fps > MaxFps) errors.Add($"Frame rate must not exceed {MaxFps}.");
        if (request.ScreenIndex < 0) errors.Add("Screen index cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Preset)) errors.Add("Encoder preset is required.");
        if (!KnownVideoCodecs.Contains(request.VideoCodec, StringComparer.OrdinalIgnoreCase))
            errors.Add($"The {request.VideoCodec} video encoder is not one of the recorder's measured encoders.");

        errors.AddRange(RegionErrors(request));
        errors.AddRange(ScaleErrors(request));
        errors.AddRange(RateControlErrors(request));
        errors.AddRange(StreamShapeErrors(request));
        errors.AddRange(ColourErrors(request));
        errors.AddRange(LimitErrors(request));

        if (request.Target == RecorderTargetKind.Window)
            errors.AddRange(WindowErrors(request));

        errors.AddRange(ScreenSelectionErrors(request));

        return errors;
    }

    private static IEnumerable<string> RegionErrors(RecorderRequest request)
    {
        if (request.Target != RecorderTargetKind.Region) yield break;

        if (request.Region is null)
        {
            yield return "Region capture needs a rectangle.";
            yield break;
        }

        if (request.Region.Width <= 0 || request.Region.Height <= 0)
            yield return "Region dimensions must be positive.";
        else if (request.Region.Width % 2 != 0 || request.Region.Height % 2 != 0)
            yield return "Region dimensions must be even for the selected pixel format.";

        if (request.Region.X < 0 || request.Region.Y < 0)
            yield return "Region offsets cannot be negative.";
    }

    private static IEnumerable<string> ScaleErrors(RecorderRequest request)
    {
        if (request.Scale is not { } scale) yield break;

        if (scale.Width <= 0 || scale.Height <= 0)
            yield return "Output scale dimensions must be positive.";
        else if (scale.Width % 2 != 0 || scale.Height % 2 != 0)
            yield return "Output scale dimensions must be even for the selected pixel format.";
    }

    private static IEnumerable<string> RateControlErrors(RecorderRequest request)
    {
        if (request.RateControl != RecorderRateControl.Bitrate)
        {
            if (request.BitrateKbps is not null || request.MaxBitrateKbps is not null || request.BufferKbits is not null)
                yield return "Bitrate values only belong to the bitrate rate-control arm; the quality arm ignores them.";
            yield break;
        }

        if (request.BitrateKbps is not { } bitrate)
        {
            yield return "The bitrate arm needs a target video bitrate; the recorder has no measured default to fall back on.";
            yield break;
        }

        if (bitrate <= 0) yield return "Target video bitrate must be greater than zero.";
        if (request.MaxBitrateKbps is { } max)
        {
            if (max <= 0) yield return "Peak video bitrate must be greater than zero.";
            else if (max < bitrate) yield return "Peak video bitrate cannot be below the target video bitrate.";
        }

        if (request.BufferKbits is { } buffer && buffer <= 0)
            yield return "Rate-control buffer must be greater than zero.";
    }

    private static IEnumerable<string> StreamShapeErrors(RecorderRequest request)
    {
        if (request.KeyframeSeconds < 0) yield return "Keyframe interval cannot be negative.";
        else if (request.KeyframeSeconds > MaxKeyframeSeconds)
            yield return $"Keyframe interval must not exceed {MaxKeyframeSeconds} seconds.";

        if (!string.IsNullOrWhiteSpace(request.Profile))
        {
            var allowed = ProfilesFor(request.VideoCodec);
            if (allowed.Count == 0)
                yield return $"The {request.VideoCodec} encoder takes no -profile:v in the recorder arm.";
            else if (!allowed.Contains(request.Profile, StringComparer.OrdinalIgnoreCase))
                yield return $"The {request.Profile} profile is not one of the {request.VideoCodec} profiles ({string.Join(", ", allowed)}).";
        }

        if (string.IsNullOrWhiteSpace(request.Tune)) yield break;

        var tunes = TunesFor(request.VideoCodec);
        if (tunes.Count == 0)
            yield return $"The {request.VideoCodec} encoder takes no -tune in the recorder arm.";
        else if (!tunes.Contains(request.Tune, StringComparer.OrdinalIgnoreCase))
            yield return $"The {request.Tune} tune is not one of the {request.VideoCodec} tunes ({string.Join(", ", tunes)}).";
    }

    private static IEnumerable<string> ColourErrors(RecorderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PixelFormat))
            yield return "Pixel format is required.";
        else if (!KnownPixelFormats.Contains(request.PixelFormat, StringComparer.OrdinalIgnoreCase))
            yield return $"The {request.PixelFormat} pixel format is not one of the recorder's pixel formats.";

        if (!string.IsNullOrWhiteSpace(request.ColorSpace)
            && !KnownColorSpaces.Contains(request.ColorSpace, StringComparer.OrdinalIgnoreCase))
            yield return $"The {request.ColorSpace} colour space is not one of the recorder's colour spaces.";

        if (!string.IsNullOrWhiteSpace(request.ColorRange)
            && !KnownColorRanges.Contains(request.ColorRange, StringComparer.OrdinalIgnoreCase))
            yield return $"The {request.ColorRange} colour range is not one of the recorder's colour ranges.";
    }

    private static IEnumerable<string> LimitErrors(RecorderRequest request)
    {
        if (request.MaxDuration is { } limit && limit <= TimeSpan.Zero)
            yield return "Recording time limit must be greater than zero.";

        if (request.Split is not { } split) yield break;

        if (!split.IsSet)
            yield return "Automatic splitting needs a duration or a size to split on.";
        if (split.Duration is { } duration && duration <= TimeSpan.Zero)
            yield return "Split duration must be greater than zero.";
        if (split.Megabytes is { } megabytes && megabytes <= 0)
            yield return "Split size must be greater than zero.";
        if (request.MaxDuration is { } max && split.Duration is { } every && every > max)
            yield return "Split duration cannot be longer than the recording time limit.";
    }

    private static IEnumerable<string> ScreenSelectionErrors(RecorderRequest request)
    {
        if (request.Platform != RecorderPlatform.Windows || request.ScreenIndex == 0) yield break;

        if (request.Target != RecorderTargetKind.Screen)
            yield return "A screen index only selects a monitor for whole-screen capture; window and region capture ignore it.";
        else if (RegionForScreen(request.Screens, request.ScreenIndex) is null)
            yield return "gdigrab captures the whole desktop and does not take a screen index; monitor "
                         + request.ScreenIndex.ToString(CultureInfo.InvariantCulture)
                         + " is not in the enumerated monitor bounds, so no region offset could be derived.";
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

        var audio = request.Audio is { InputCount: > 0 } plan ? plan : null;
        if (audio is not null) a.AddRange(audio.Inputs);

        if (VideoFilter(request) is { } filter) a.AddRange(new[] { "-vf", filter });

        if (audio?.FilterComplex is { Length: > 0 } graph)
            a.AddRange(new[] { "-filter_complex", graph });

        a.AddRange(new[] { "-c:v", request.VideoCodec, "-preset", request.Preset });
        a.AddRange(RateControlArgs(request));
        a.AddRange(StreamShapeArgs(request));
        a.AddRange(ColourArgs(request));
        a.AddRange(AudioOutputArgs(audio));

        if (request.MaxDuration is { } limit)
            a.AddRange(new[] { "-t", limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) });

        if (request.Container is RecorderContainer.Mp4 or RecorderContainer.Mov)
            a.AddRange(new[] { "-movflags", "+faststart" });

        a.Add(outputPath);
        return a;
    }

    /// <summary>
    /// Kayit surerken alinan tek karelik ekran goruntusunun argumanlari. Ayni yakalama
    /// girdisinden okur — dosyadan kare kesen <c>FrameGrabber</c> burada kullanilamaz,
    /// cunku canli ekranin bir dosyasi yok. Kodlama kolu hic kurulmaz: bir kare, bir resim.
    /// </summary>
    public static IReadOnlyList<string> BuildSnapshot(RecorderRequest request, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("Image path is required.", nameof(imagePath));

        var probe = request with
        {
            Container = RecorderContainer.Mp4,
            Audio = null,
            MaxDuration = null,
            Split = null
        };
        var errors = Validate(probe, "snapshot.mp4");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var a = new List<string> { "-hide_banner", "-nostdin", "-y" };
        a.AddRange(Input(request));
        if (VideoFilter(request) is { } filter) a.AddRange(new[] { "-vf", filter });
        a.AddRange(new[] { "-frames:v", "1", "-an", imagePath });
        return a;
    }

    private static IReadOnlyList<string> RateControlArgs(RecorderRequest request)
    {
        if (request.RateControl == RecorderRateControl.Quality)
            return CodecModel.QualityArgs(request.VideoCodec, request.Quality);

        var a = new List<string>(CodecModel.BitrateRateControlArgs(request.VideoCodec));
        a.AddRange(new[] { "-b:v", Rate(request.BitrateKbps!.Value) });
        if (request.MaxBitrateKbps is { } max) a.AddRange(new[] { "-maxrate", Rate(max) });
        if (request.BufferKbits is { } buffer) a.AddRange(new[] { "-bufsize", Rate(buffer) });
        return a;
    }

    private static IReadOnlyList<string> StreamShapeArgs(RecorderRequest request)
    {
        var a = new List<string>();
        if (request.KeyframeSeconds > 0)
            a.AddRange(new[] { "-g", Number(Math.Max(1, request.KeyframeSeconds * request.Fps)) });
        if (!string.IsNullOrWhiteSpace(request.Profile)) a.AddRange(new[] { "-profile:v", request.Profile! });
        if (!string.IsNullOrWhiteSpace(request.Tune)) a.AddRange(new[] { "-tune", request.Tune! });
        return a;
    }

    private static IReadOnlyList<string> ColourArgs(RecorderRequest request)
    {
        var a = new List<string> { "-pix_fmt", request.PixelFormat };
        if (!string.IsNullOrWhiteSpace(request.ColorSpace)) a.AddRange(new[] { "-colorspace", request.ColorSpace! });
        if (!string.IsNullOrWhiteSpace(request.ColorRange)) a.AddRange(new[] { "-color_range", request.ColorRange! });
        return a;
    }

    /// <summary>
    /// Ses cikisinin eslemeleri ve kodlayicilari. Tek iz tek <c>-c:a</c> aliyor; birden
    /// fazla iz (ayri mikrofon ve sistem sesi) her biri kendi <c>-c:a:N</c>'ini aliyor,
    /// cunku izler ayri kaldiginda hangisinin hangi kodlayiciya gittigi argumandan
    /// okunabilmeli.
    /// </summary>
    private static IReadOnlyList<string> AudioOutputArgs(AudioCapturePlan? audio)
    {
        if (audio is null) return new[] { "-an" };

        var a = new List<string> { "-map", "0:v" };
        foreach (var map in audio.Maps) a.AddRange(new[] { "-map", map });

        if (audio.Maps.Count <= 1)
        {
            a.AddRange(new[] { "-c:a", AudioCodec, "-b:a", AudioBitrate });
            return a;
        }

        for (var track = 0; track < audio.Maps.Count; track++)
        {
            var suffix = Number(track);
            a.AddRange(new[] { $"-c:a:{suffix}", AudioCodec, $"-b:a:{suffix}", AudioBitrate });
        }

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

        var region = request.Target == RecorderTargetKind.Region
            ? request.Region
            : request.Target == RecorderTargetKind.Screen && request.ScreenIndex != 0
                ? RegionForScreen(request.Screens, request.ScreenIndex)
                : null;

        if (region is { } rectangle)
            a.AddRange(new[]
            {
                "-offset_x", Number(rectangle.X),
                "-offset_y", Number(rectangle.Y),
                "-video_size", Size(rectangle)
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
    /// <c>-vf</c> zinciri. Iki halka var ve sirasi onemli: once kirpma, sonra olcekleme —
    /// ters sirada dikdortgenin koordinatlari olceklenmis kareye dusuyor ve kullanicinin
    /// sectigi yer kayiyor.
    /// <para>
    /// macOS'ta bolge girdi secenegi degil: <c>avfoundation</c> ekranin tamamini veriyor,
    /// dikdortgen kirpma filtresiyle aliniyor. Oteki iki platformda bolge zaten girdide.
    /// </para>
    /// </summary>
    private static string? VideoFilter(RecorderRequest request)
    {
        var links = new List<string>();

        if (request is { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Region, Region: { } region })
            links.Add($"crop={Number(region.Width)}:{Number(region.Height)}:{Number(region.X)}:{Number(region.Y)}");

        if (request.Scale is { } scale)
            links.Add($"scale={Number(scale.Width)}:{Number(scale.Height)}");

        return links.Count == 0 ? null : string.Join(',', links);
    }

    private static string Size(RecorderRegion region) => $"{Number(region.Width)}x{Number(region.Height)}";

    private static string Rate(int kbps) => Number(kbps) + "k";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "1" : "0";
}
