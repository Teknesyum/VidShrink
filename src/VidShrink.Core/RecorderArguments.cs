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
    Mov,

    Gif
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

    /// <summary>
    /// macOS'ta pencerenin yakalanan ekran karesindeki piksel dikdortgeni. <c>avfoundation</c>
    /// tek pencere vermiyor; pencere ekran karesinden kirpilir (<see cref="RecorderArguments.WindowCrop"/>).
    /// </summary>
    public RecorderRegion? WindowRegion { get; init; }

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

    public double? MaxMegabytes { get; init; }

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

    public RecorderWebcam? Webcam { get; init; }

    public string? PreviewPath { get; init; }
}

public enum WebcamCorner
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft
}

public enum WebcamBackground
{
    Keep,
    Static,
    Green
}

public sealed record RecorderWebcam(
    string Device,
    int Width,
    WebcamCorner Corner = WebcamCorner.BottomRight,
    WebcamBackground Background = WebcamBackground.Keep);

/// <summary>
/// Ekran kaydi argumanlarini uretir. Kosturmaz — surec surmeyi
/// <c>VidShrink.Ffmpeg.RecorderSession</c> biliyor, burasi yalnizca ne kosacagini soyluyor.
/// <para>
/// Uretilen listede <c>-nostdin</c> <b>yoktur</b>. Deponun oteki cagrilarinin nerdeyse hepsi
/// onu veriyor (<c>ClipExport</c>, <c>SegmentEncoder</c>, <c>ComplexityProbe</c>); kaydin nazik durdurma yolu ise tam olarak stdin'e yazilan
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

    /// <summary>
    /// Kabul edilen en uzun anahtar kare araligi, saniye. <b>Olculmus bir sayi degil</b>:
    /// ffmpeg'in boyle bir siniri yok, sayi yalniz yazim hatasini (kare sayisini saniye
    /// yerine yazmak gibi) yakalamak icin konan bir korkuluk. Bir dakikadan seyrek anahtar
    /// kare, ekran kaydinda aramayi bir dakikaya kadar bekletir; bundan uzununu isteyen bir
    /// kullanim bilinmiyor. Kanitlanmis bir alt ya da ust sinir gerekirse olculup
    /// degistirilmeli.
    /// </summary>
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
    /// <para>
    /// Kumede yalniz bit akisina ayni adla giren duzlemsel YUV bicimleri var. Eski kumedeki
    /// <c>nv12</c>, <c>p010le</c>, <c>rgb24</c>, <c>bgr0</c> ve <c>gbrp</c> cikarildi:
    /// libx264 onlari uyari vermeden <c>yuv420p</c>/<c>yuv444p</c>/<c>yuv420p10le</c>'ye
    /// ceviriyordu, yani secilen ad ciktida durmuyordu. Paketli ad (<c>nv12</c>,
    /// <c>p010le</c>) yalniz kodlayici duzlemsel adi hic almiyorsa motorca yaziliyor, bkz.
    /// <see cref="PixelFormatArgument"/>.
    /// </para>
    /// </summary>
    private static readonly string[] KnownPixelFormats =
    {
        "yuv420p", "yuv422p", "yuv444p",
        "yuv420p10le", "yuv422p10le", "yuv444p10le"
    };

    private static readonly string[] AllPlanarFormats = KnownPixelFormats;

    private static readonly string[] EightAndTenBit420 = { "yuv420p", "yuv420p10le" };

    private static readonly string[] Only420 = { "yuv420p" };

    private static readonly string[] NvencFormats = { "yuv420p", "yuv444p", "yuv420p10le" };

    private static readonly string[] KnownColorSpaces =
    {
        "bt709", "bt470bg", "smpte170m", "smpte240m", "bt2020nc", "bt2020c"
    };

    private static readonly string[] KnownColorRanges = { "tv", "pc", "limited", "full", "mpeg", "jpeg" };

    private static readonly string[] H264Profiles =
    {
        "baseline", "main", "high", "high10", "high422", "high444"
    };

    private static readonly string[] HevcProfiles = { "main", "main10", "rext" };

    private static readonly string[] Vp9Profiles = { "0", "1", "2", "3" };

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
        RecorderContainer.Gif => "gif",
        _ => "mp4"
    };

    public static IReadOnlyList<string> BuildRemuxToMp4(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source path is required.", nameof(source));
        if (ContainerOf(target ?? string.Empty) != RecorderContainer.Mp4)
            throw new ArgumentException("The remux target must be an .mp4 file.", nameof(target));
        return new[]
        {
            "-hide_banner", "-y", "-nostdin",
            "-i", source,
            "-map", "0",
            "-c", "copy",
            "-movflags", "+faststart",
            target!
        };
    }

    public static string RemuxTarget(string source, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var folder = Path.GetDirectoryName(source) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source);
        var candidate = Path.Combine(folder, stem + ".mp4");
        for (var index = 2; exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + ".mp4");
        return candidate;
    }

    public static RecorderContainer CaptureContainer(RecorderContainer container)
        => container == RecorderContainer.Gif ? RecorderContainer.Mkv : container;

    public static string CapturePath(string outputPath, RecorderContainer container)
    {
        if (container != RecorderContainer.Gif || string.IsNullOrWhiteSpace(outputPath)) return outputPath;
        return Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(outputPath) + ".gif-kayit.mkv");
    }

    public static bool SizeNeedsMatroska(RecorderRequest request)
        => request.Container is RecorderContainer.Mp4 or RecorderContainer.Mov
           && (request.MaxMegabytes is not null || request.Split?.Megabytes is not null);

    public static string SizeCapturePath(string outputPath)
        => Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outputPath) + ".boyut.mkv");

    public static string SizeDeliveryPath(string capturePath, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(capturePath);
        var mark = stem.LastIndexOf(".boyut", StringComparison.Ordinal);
        if (mark >= 0) stem = stem.Remove(mark, ".boyut".Length);
        return Path.Combine(Path.GetDirectoryName(capturePath) ?? string.Empty, stem + extension);
    }

    public static RecorderRequest CaptureRequest(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Container == RecorderContainer.Gif
            ? request with { Container = CaptureContainer(request.Container) }
            : request;
    }

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
            "gif" => RecorderContainer.Gif,
            _ => null
        };
    }

    /// <summary>
    /// Nazik olmayan durdurmada dosyayi oynatilabilir birakan kaplar. Bugun yalniz
    /// Matroska: mp4/mov muxer'i <c>moov</c> atomunu kapanista yaziyor. Matroska da ancak
    /// <c>-flush_packets 1</c> ile: bayraksiz 640x480 gdigrab kaydi 7 sn sonra olduruldugunde
    /// ffmpeg 79 KiB bildirirken dosya 0 bayt kaldi, bayrakla 80 KiB ve 76 okunur paket
    /// (<c>KayitBolmeTests</c>).
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
        if (c == "libvpx-vp9") return Vp9Profiles;
        if (c.Contains("vp9", StringComparison.Ordinal)) return Array.Empty<string>();
        if (c.Contains("265", StringComparison.Ordinal) || c.Contains("hevc", StringComparison.Ordinal)) return HevcProfiles;
        if (c.Contains("264", StringComparison.Ordinal)) return H264Profiles;
        return Array.Empty<string>();
    }

    /// <summary>
    /// libvpx-vp9'un piksel bicimine bagli profili: 0 sekiz bit 4:2:0, 1 sekiz bit 4:2:2/4:4:4,
    /// 2 on bit 4:2:0, 3 on bit 4:2:2/4:4:4. ffmpeg 9.0'da uyusmayan cift (profil 1 + yuv420p,
    /// profil 0 + yuv444p) "Error encoding frame: Invalid parameter", profil 2/3 + yuv420p acilista
    /// -22 ile duser; olcum <c>docs/olcumler/kaydedici-piksel.md</c>.
    /// </summary>
    public static string? Vp9ProfileFor(string? pixelFormat) => (pixelFormat ?? string.Empty).ToLowerInvariant() switch
    {
        "yuv420p" => "0",
        "yuv422p" or "yuv444p" => "1",
        "yuv420p10le" => "2",
        "yuv422p10le" or "yuv444p10le" => "3",
        _ => null
    };

    /// <summary>Kodegin tanidigi <c>-tune</c> degerleri; bos liste ayari reddeder.</summary>
    public static IReadOnlyList<string> TunesFor(string codec) => (codec ?? string.Empty).ToLowerInvariant() switch
    {
        "libx264" => X264Tunes,
        "libx265" => X265Tunes,
        var c when c.Contains("nvenc", StringComparison.Ordinal) => NvencTunes,
        _ => Array.Empty<string>()
    };

    /// <summary>Kabul edilen piksel bicimlerinin birlesimi; tek bir kodegin kumesi <see cref="PixelFormatsFor"/>.</summary>
    public static IReadOnlyList<string> PixelFormats => KnownPixelFormats;

    /// <summary>
    /// Kodegin kabul ettigi piksel bicimleri. Kaynak ffmpeg 9.0'in
    /// <c>ffmpeg -h encoder=&lt;ad&gt;</c> ciktisindaki "Supported pixel formats" satiri,
    /// <see cref="KnownPixelFormats"/> ile kesistirilmis; paketli ad (<c>nv12</c>,
    /// <c>p010le</c>) ayni ornekleme ve bit derinligindeki duzlemsel adin yerine sayiliyor.
    /// Ham satirlar <c>docs/olcumler/kaydedici-piksel-bicimleri.md</c>. Donanim kollarinin
    /// kumesi ffmpeg'in bildirdigi kume; kartin gercekten kodlayabildigi olculmedi.
    /// </summary>
    public static IReadOnlyList<string> PixelFormatsFor(string codec) => (codec ?? string.Empty).ToLowerInvariant() switch
    {
        "libx264" or "libx265" or "libvpx-vp9" => AllPlanarFormats,
        "libsvtav1" => EightAndTenBit420,
        "h264_nvenc" or "hevc_nvenc" or "av1_nvenc" => NvencFormats,
        "h264_qsv" => Only420,
        "hevc_qsv" or "h264_amf" or "hevc_amf" => EightAndTenBit420,
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// Eski ayar dosyasindaki piksel bicimi. Daraltmadan once yazilmis <c>nv12</c> ve
    /// <c>p010le</c> ayni ornekleme ve derinlikteki duzlemsel ada, kumeden cikan ya da
    /// uydurma ad varsayilana donuyor.
    /// </summary>
    public static string StoredPixelFormat(string? stored)
    {
        var f = (stored ?? string.Empty).Trim().ToLowerInvariant();
        if (f == "nv12") return "yuv420p";
        if (f == "p010le") return "yuv420p10le";
        return KnownPixelFormats.Contains(f, StringComparer.Ordinal) ? f : DefaultPixelFormat;
    }

    /// <summary>
    /// <c>-pix_fmt</c>'e yazilan ad. Kodlayici duzlemsel adi bildirmiyorsa ayni ornekleme ve
    /// derinlikteki paketli ad yaziliyor; boylece ffmpeg arada sessiz bir donusum kurmuyor.
    /// Quick Sync <c>yuv420p</c>'yi de, <c>yuv420p10le</c>'yi de bildirmiyor; NVENC ve AMF
    /// <c>yuv420p</c>'yi bildiriyor ama 10 bit icin yalniz <c>p010le</c>'yi.
    /// </summary>
    public static string PixelFormatArgument(string codec, string pixelFormat)
    {
        var c = (codec ?? string.Empty).ToLowerInvariant();
        var f = (pixelFormat ?? string.Empty).ToLowerInvariant();
        var qsv = c is "h264_qsv" or "hevc_qsv";
        var packed10 = qsv || c is "h264_nvenc" or "hevc_nvenc" or "av1_nvenc" or "h264_amf" or "hevc_amf";
        if (qsv && f == "yuv420p") return "nv12";
        if (packed10 && f == "yuv420p10le") return "p010le";
        return pixelFormat ?? string.Empty;
    }

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
    /// <c>gdigrab</c>'a yazilacak yakalama bolgesi, ekran hedefi icin. <c>-i desktop</c>
    /// butun sanal masaustunu veriyor: <b>tek monitor degil</b>. Bu yuzden secilen monitor
    /// masaustunun tamami degilse indeks sifir olsa da ofsete cevriliyor — aksi halde iki
    /// monitorlu bir makinede "Ekran 1" iki monitoru birden kaydediyor.
    /// <para>
    /// Monitor masaustunun tamamiysa <c>null</c> doner ve <c>-i desktop</c> ofsetsiz kalir;
    /// tek ekranli makinenin argumani degismiyor. Liste bossa da <c>null</c>: yakalanacak
    /// yerin ne oldugunu motor uydurmuyor.
    /// </para>
    /// </summary>
    public static RecorderRegion? ScreenCapture(IReadOnlyList<ScreenBounds> screens, int index)
    {
        if (screens is null || screens.Count == 0) return null;
        if (RegionForScreen(screens, index) is not { } region) return null;
        if (RecorderLayout.Union(screens) is { } union
            && union.X == region.X && union.Y == region.Y
            && union.Width == region.Width && union.Height == region.Height)
            return null;
        return region;
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
            errors.Add($"The {Path.GetExtension(outputPath)} output extension is not one of the recorder's containers (mp4, mkv, mov, gif).");
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
        errors.AddRange(GifErrors(request));
        errors.AddRange(WebcamErrors(request));
        errors.AddRange(PreviewErrors(request));

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
            foreach (var error in NegativeOffsetErrors(request)) yield return error;
    }

    /// <summary>
    /// Negatif bolge ofseti her zaman hata degil. Windows ve Linux'ta birincil monitorun
    /// soluna ya da ustune yerlestirilen ikinci monitor <b>negatif</b> masaustu
    /// koordinatlarinda oturuyor ve <c>gdigrab</c> ile <c>x11grab</c> oraya bakabiliyor;
    /// blanket bir "negatif olamaz" kurali o monitorde bolge kaydini tumden kapatiyor.
    /// <para>
    /// Kural sayiya degil <b>kapsamaya</b> bakiyor: monitor listesi dikdortgeni kapsiyorsa
    /// negatif ofset gecerli. Liste bossa masaustunun sola uzandigina dair kanit yok ve
    /// eski kural duruyor. macOS'ta bolge girdi degil kirpma filtresi: kare icinde negatif
    /// koordinat yok, orada kural her zaman geciyor.
    /// </para>
    /// </summary>
    private static IEnumerable<string> NegativeOffsetErrors(RecorderRequest request)
    {
        if (request.Platform != RecorderPlatform.MacOs
            && request.Screens.Count > 0
            && request.Region is { } region
            && RecorderLayout.Covered(request.Screens, region))
            yield break;

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
            else if (allowed == Vp9Profiles && Vp9ProfileFor(request.PixelFormat) is { } needed && needed != request.Profile)
                yield return $"The libvpx-vp9 profile {request.Profile} cannot carry the {request.PixelFormat} pixel format; that format needs profile {needed}.";
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
        else if (!PixelFormatsFor(request.VideoCodec).Contains(request.PixelFormat, StringComparer.OrdinalIgnoreCase))
            yield return $"The {request.VideoCodec} encoder does not take the {request.PixelFormat} pixel format ({string.Join(", ", PixelFormatsFor(request.VideoCodec))}).";

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
        if (request.MaxMegabytes is { } cap && (cap <= 0 || double.IsNaN(cap) || double.IsInfinity(cap)))
            yield return "Recording size limit must be greater than zero.";

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

    private static IEnumerable<string> GifErrors(RecorderRequest request)
    {
        if (request.Container != RecorderContainer.Gif) yield break;

        if (request.Audio is { InputCount: > 0 })
            yield return "A GIF has no audio track; turn the audio inputs off.";
        if (request.Split is not null)
            yield return "A GIF recording is converted as one file and cannot be split.";
        if (request.MaxMegabytes is not null)
            yield return "A GIF is converted after capture, so its size cannot be capped while recording.";
        if (request.Fps > GifPalette.MaxFps)
            yield return $"A GIF frame delay is counted in hundredths of a second; the frame rate must not exceed {GifPalette.MaxFps}.";
    }

    public const int WebcamMargin = 16;

    public const int MinWebcamWidth = 64;

    public const int MaxWebcamWidth = 1920;

    public const string WebcamBufferSize = "256M";

    public static IReadOnlyList<int> WebcamWidths { get; } = new[] { 160, 240, 320, 480 };

    private static IEnumerable<string> WebcamErrors(RecorderRequest request)
    {
        if (request.Webcam is not { } cam) yield break;
        if (request.Platform != RecorderPlatform.Windows)
            yield return "The webcam overlay reads the camera through DirectShow and is only available on Windows.";
        if (string.IsNullOrWhiteSpace(cam.Device))
            yield return "The webcam overlay needs a camera device.";
        else if (cam.Device.Contains('"', StringComparison.Ordinal))
            yield return "A camera device name cannot contain a quotation mark.";
        if (cam.Width < MinWebcamWidth || cam.Width > MaxWebcamWidth || cam.Width % 2 != 0)
            yield return $"The webcam width must be an even number between {MinWebcamWidth} and {MaxWebcamWidth} pixels.";
        if (!Enum.IsDefined(cam.Corner))
            yield return "The webcam corner is not one of the four corners.";
        if (!Enum.IsDefined(cam.Background))
            yield return "The webcam background mode is not one of keep, static or green.";
    }

    public static string WebcamPosition(WebcamCorner corner)
    {
        var margin = Number(WebcamMargin);
        var left = margin;
        var right = $"main_w-overlay_w-{margin}";
        var top = margin;
        var bottom = $"main_h-overlay_h-{margin}";
        return corner switch
        {
            WebcamCorner.TopLeft => $"{left}:{top}",
            WebcamCorner.TopRight => $"{right}:{top}",
            WebcamCorner.BottomLeft => $"{left}:{bottom}",
            _ => $"{right}:{bottom}"
        };
    }

    public const string WebcamOutputLabel = "vout";

    public const string StaticBackgroundKey = "backgroundkey=threshold=0.8:similarity=0.1:blend=0";

    public const string GreenScreenKey = "chromakey=color=0x00FF00:similarity=0.15:blend=0.05";

    public static string WebcamKey(WebcamBackground background) => background switch
    {
        WebcamBackground.Static => ",format=yuva420p," + StaticBackgroundKey,
        WebcamBackground.Green => ",format=yuva420p," + GreenScreenKey,
        _ => string.Empty
    };

    public static string? WebcamGraph(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Webcam is not { } cam) return null;
        var index = Number(AudioFirstInputIndex + (request.Audio?.InputCount ?? 0));
        return $"[0:v]{VideoFilter(request) ?? "null"}[base];"
               + $"[{index}:v]scale={Number(cam.Width)}:-2{WebcamKey(cam.Background)}[cam];"
               + $"[base][cam]overlay={WebcamPosition(cam.Corner)}:eof_action=repeat[{WebcamOutputLabel}]";
    }

    private static IEnumerable<string> ScreenSelectionErrors(RecorderRequest request)
    {
        if (request.Platform != RecorderPlatform.Windows) yield break;

        if (request.Target != RecorderTargetKind.Screen)
        {
            if (request.ScreenIndex != 0)
                yield return "A screen index only selects a monitor for whole-screen capture; window and region capture ignore it.";

            foreach (var error in RegionCoverageErrors(request)) yield return error;
            yield break;
        }

        if (request.Screens.Count == 0)
        {
            if (request.ScreenIndex != 0)
                yield return MissingMonitor(request.ScreenIndex);
            yield break;
        }

        if (RegionForScreen(request.Screens, request.ScreenIndex) is null)
            yield return MissingMonitor(request.ScreenIndex);
    }

    private static string MissingMonitor(int index)
        => "gdigrab captures the whole desktop and does not take a screen index; monitor "
           + index.ToString(CultureInfo.InvariantCulture)
           + " is not in the enumerated monitor bounds, so no region offset could be derived.";

    /// <summary>
    /// Bolgenin monitorlerin disina tasan taraflari. <c>gdigrab</c> masaustunde monitor
    /// olmayan yeri siyah veriyor; iki monitor arasindaki bosluga ya da masaustunun disina
    /// dusen dikdortgen sessizce siyah bant olarak kaydedilmesin diye burada duruyor.
    /// </summary>
    private static IEnumerable<string> RegionCoverageErrors(RecorderRequest request)
    {
        if (request.Target != RecorderTargetKind.Region) yield break;
        if (request.Screens.Count == 0 || request.Region is not { } region) yield break;
        if (RecorderLayout.Covered(request.Screens, region)) yield break;

        yield return "The capture region "
                     + region.Width.ToString(CultureInfo.InvariantCulture) + "x"
                     + region.Height.ToString(CultureInfo.InvariantCulture) + " at "
                     + region.X.ToString(CultureInfo.InvariantCulture) + ","
                     + region.Y.ToString(CultureInfo.InvariantCulture)
                     + " is not fully covered by the enumerated monitors; gdigrab records the uncovered part as black.";
    }

    private static IEnumerable<string> WindowErrors(RecorderRequest request) => request.Platform switch
    {
        RecorderPlatform.Windows => string.IsNullOrWhiteSpace(request.WindowTitle)
            ? new[] { "Window capture on gdigrab needs the window title." }
            : Array.Empty<string>(),
        RecorderPlatform.Linux => string.IsNullOrWhiteSpace(request.WindowId)
            ? new[] { "Window capture on x11grab needs the window id." }
            : Array.Empty<string>(),
        RecorderPlatform.MacOs => MacWindowErrors(request),
        _ => Array.Empty<string>()
    };

    private static IEnumerable<string> MacWindowErrors(RecorderRequest request)
    {
        if (request.WindowRegion is not { } region)
        {
            yield return "avfoundation exposes screens and devices, not single windows; the window rectangle must be resolved to a screen crop first.";
            yield break;
        }

        if (region.Width <= 0 || region.Height <= 0)
            yield return "Window crop dimensions must be positive.";
        else if (region.Width % 2 != 0 || region.Height % 2 != 0)
            yield return "Window crop dimensions must be even for the selected pixel format.";

        if (region.X < 0 || region.Y < 0)
            yield return "Window crop offsets cannot be negative.";
    }

    /// <summary>
    /// Pencerenin nokta cinsinden dikdortgenini, yakalanan ekranin piksel karesindeki kirpmaya cevirir:
    /// ekranla kesisim alinir, olcekle carpilir, boyut cift sayiya iner. Pencere ekranda degilse null.
    /// </summary>
    public static RecorderRegion? WindowCrop(
        double windowX, double windowY, double windowWidth, double windowHeight,
        double screenX, double screenY, double screenWidth, double screenHeight, double scale)
    {
        if (!(scale > 0)) return null;
        var left = Math.Max(windowX, screenX);
        var top = Math.Max(windowY, screenY);
        var right = Math.Min(windowX + windowWidth, screenX + screenWidth);
        var bottom = Math.Min(windowY + windowHeight, screenY + screenHeight);
        if (!(right > left && bottom > top)) return null;

        var x = (int)Math.Round((left - screenX) * scale);
        var y = (int)Math.Round((top - screenY) * scale);
        var w = Math.Min((int)Math.Floor((right - left) * scale), (int)Math.Floor(screenWidth * scale) - x);
        var h = Math.Min((int)Math.Floor((bottom - top) * scale), (int)Math.Floor(screenHeight * scale) - y);
        w -= w % 2;
        h -= h % 2;
        return w < 2 || h < 2 ? null : new RecorderRegion(x, y, w, h);
    }

    private static RecorderRegion? MacCrop(RecorderRequest request) => request switch
    {
        { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Region, Region: { } region } => region,
        { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Window, WindowRegion: { } window } => window,
        _ => null
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
        if (request.Container == RecorderContainer.Gif)
            return Build(CaptureRequest(request), CapturePath(outputPath, request.Container));

        var a = new List<string> { "-hide_banner", "-y" };
        a.AddRange(Input(request));

        var audio = request.Audio is { InputCount: > 0 } plan ? plan : null;
        if (audio is not null) a.AddRange(audio.Inputs);

        var webcam = WebcamGraph(request with { Audio = audio });
        if (request.Webcam is { } cam)
            a.AddRange(new[] { "-f", "dshow", "-rtbufsize", WebcamBufferSize, "-i", "video=" + cam.Device });

        if (webcam is null && VideoFilter(request) is { } filter) a.AddRange(new[] { "-vf", filter });

        var graphs = new[] { webcam, audio?.FilterComplex }.Where(g => !string.IsNullOrEmpty(g)).ToArray();
        if (graphs.Length > 0)
            a.AddRange(new[] { "-filter_complex", string.Join(";", graphs) });

        a.AddRange(new[] { "-c:v", request.VideoCodec, "-preset", request.Preset });
        a.AddRange(RateControlArgs(request));
        a.AddRange(StreamShapeArgs(request));
        a.AddRange(ColourArgs(request));
        a.AddRange(AudioOutputArgs(audio, webcam is null ? "0:v" : $"[{WebcamOutputLabel}]"));

        if (request.MaxDuration is { } limit)
            a.AddRange(new[] { "-t", limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) });

        if (request.MaxMegabytes is { } megabytes)
            a.AddRange(new[] { "-fs", LimitBytes(megabytes).ToString(CultureInfo.InvariantCulture) });

        if (request.Container is RecorderContainer.Mp4 or RecorderContainer.Mov)
            a.AddRange(new[] { "-movflags", "+faststart" });
        else if (request.Container == RecorderContainer.Mkv)
            a.AddRange(new[] { "-flush_packets", "1" });

        a.Add(outputPath);
        a.AddRange(PreviewArgs(request));
        return a;
    }

    public const int PreviewWidth = 320;

    public const int PreviewFps = 1;

    public static IReadOnlyList<string> PreviewArgs(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PreviewPath)) return Array.Empty<string>();

        var filter = $"fps={PreviewFps},scale={PreviewWidth}:-2";
        if (MacCrop(request) is { } region)
            filter =$"crop={Number(region.Width)}:{Number(region.Height)}:{Number(region.X)}:{Number(region.Y)}," + filter;
        var a = new List<string> { "-map", "0:v", "-vf", filter, "-an" };
        if (request.MaxDuration is { } limit)
            a.AddRange(new[] { "-t", limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) });
        a.AddRange(new[] { "-f", "image2", "-update", "1", "-q:v", "6", request.PreviewPath! });
        return a;
    }

    private static IEnumerable<string> PreviewErrors(RecorderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PreviewPath)) yield break;
        if (!string.Equals(Path.GetExtension(request.PreviewPath), ".jpg", StringComparison.OrdinalIgnoreCase))
            yield return "The live preview image must be a .jpg file.";
        if (request.MaxMegabytes is not null)
            yield return "The live preview cannot run with a size limit: ffmpeg keeps the preview output open after -fs closes the recording.";
    }

    /// <summary>
    /// Bir parcanin istegi. Sure siniri kaydin toplamina ait, parcaya degil: duraklatip
    /// surdurmek ve kendiliginden bolme yeni bir ffmpeg sureci aciyor ve her surec
    /// <c>-t</c>'yi sifirdan sayiyor. Ilk parcadan sonra <c>-t</c>'ye kalan sure yaziliyor.
    /// Bolme olcutu argumana girmedigi icin sonraki parcanin isteginde tasinmiyor; tasinsa
    /// kalan sure bolme suresinden kisa kaldiginda dogrulama parcayi reddederdi.
    /// Kalan sure <c>-t</c>'nin yazilabildigi en kucuk adimdan (1 ms) kisaysa <c>null</c>
    /// doner ve yeni parca acilmaz.
    /// </summary>
    public static RecorderRequest? ForSegment(RecorderRequest request, TimeSpan capturedBefore, double writtenMbBefore = 0)
    {
        ArgumentNullException.ThrowIfNull(request);
        var segment = request;

        if (capturedBefore > TimeSpan.Zero && request.MaxDuration is { } limit)
        {
            var remaining = limit - capturedBefore;
            if (remaining < TimeSpan.FromMilliseconds(1)) return null;
            segment = segment with { MaxDuration = remaining, Split = null };
        }

        if (writtenMbBefore > 0 && request.MaxMegabytes is { } cap)
        {
            var left = cap - writtenMbBefore;
            if (LimitBytes(left) < 1) return null;
            segment = segment with { MaxMegabytes = left, Split = null };
        }

        return segment;
    }

    public static long LimitBytes(double megabytes) => (long)Math.Floor(megabytes * 1024 * 1024);

    /// <summary>
    /// Kayit surerken alinan tek karelik ekran goruntusunun argumanlari. Ayni yakalama
    /// girdisinden okur: canli ekranin bir dosyasi yok, dosyadan kare kesen bir yol burada
    /// kullanilamaz. Kodlama kolu hic kurulmaz: bir kare, bir resim.
    /// </summary>
    public static IReadOnlyList<string> BuildSnapshot(RecorderRequest request, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("Image path is required.", nameof(imagePath));

        var probe = request with
        {
            Container = RecorderContainer.Mp4,
            Audio = null,
            Webcam = null,
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
        var a = new List<string> { "-pix_fmt", PixelFormatArgument(request.VideoCodec, request.PixelFormat) };
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
    private static IReadOnlyList<string> AudioOutputArgs(AudioCapturePlan? audio, string videoMap)
    {
        if (audio is null)
            return videoMap == "0:v" ? new[] { "-an" } : new[] { "-map", videoMap, "-an" };

        var a = new List<string> { "-map", videoMap };
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
            : request.Target == RecorderTargetKind.Screen
                ? ScreenCapture(request.Screens, request.ScreenIndex)
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

        if (MacCrop(request) is { } region)
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
