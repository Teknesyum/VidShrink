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
    /// <summary><c>gdigrab</c> ya da <c>ddagrab</c> (<see cref="RecorderCapture"/>): butun masaustu, pencere ya da ofsetli bolge.</summary>
    Windows,

    /// <summary><c>avfoundation</c>: ekran adi (<c>Capture screen N</c>); bolge girdi degil kirpma filtresi.</summary>
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
    Region,

    /// <summary>Goruntusuz kayit: yalniz secilen ses girdileri yazilir.</summary>
    AudioOnly
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

/// <summary>
/// Cikisin kare zamanlamasi. <see cref="Auto"/> hicbir sey yazmaz ve muxer'in karari kalir;
/// oteki iki kip <c>-fps_mode cfr</c> ya da <c>-fps_mode vfr</c> yazar.
/// </summary>
public enum RecorderFrameRateMode
{
    /// <summary>Kip yazilmaz.</summary>
    Auto,

    /// <summary>Sabit kare hizi: eksik kare kopyalanir, fazlasi atilir.</summary>
    Constant,

    /// <summary>Degisken kare hizi: yakalanan zaman damgasi korunur.</summary>
    Variable
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
/// Windows'ta kareyi hangi yakalama yolunun verdigi. <c>gdigrab</c> her kareyi masaustunden
/// <c>BitBlt</c> ile okuyor ve 1280x720'de 60 istenince 41 kare veriyor; <c>ddagrab</c>
/// (Desktop Duplication) ayni bolgede 59,3 veriyor (<c>docs/olcumler/kaydedici-gdigrab-kare-tavani.md</c>).
/// Varsayilan <see cref="Gdigrab"/>: <see cref="Ddagrab"/>'i yalniz yoklamasi gecen istek alir.
/// </summary>
public enum RecorderCapture
{
    /// <summary><c>-f gdigrab</c>: butun masaustu, ofsetli bolge ya da pencere basligi.</summary>
    Gdigrab,

    /// <summary><c>-f lavfi -i ddagrab=...</c>: tek bir DXGI cikisi, bolge o cikisa gore ofsetli.</summary>
    Ddagrab
}

/// <summary>
/// ffmpeg'in varsayilan D3D11 aygitinin (ilk ekran karti) bir DXGI cikisi. <c>ddagrab</c>
/// ekrani <see cref="Index"/> ile seciyor (<c>output_idx</c>); bu sira Avalonia'nin ekran
/// sirasiyla ayni degil, bu yuzden eslesme masaustu koordinatindan yapiliyor. Dondurulmus
/// cikis listeye girmez. <see cref="Hdr"/>: Windows bu cikisi HDR10 renk uzayinda
/// (<c>DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020</c>) suruyor. <see cref="SdrWhiteNits"/>:
/// Windows'un SDR icerigi koydugu parlaklik (<c>DISPLAYCONFIG_SDR_WHITE_LEVEL</c>); okunamadiysa <c>null</c>.
/// </summary>
public sealed record DdaOutput(int Index, int X, int Y, int Width, int Height, bool Hdr = false, DdaDisplayInfo? Display = null, double? SdrWhiteNits = null);

/// <summary>
/// Cikisin <c>DXGI_OUTPUT_DESC1</c>'den okunan renk ve parlaklik bilgisi: renk koordinatlari
/// CIE 1931 xy, parlaklik nit. Surucu bos birakabilir; sifir ya da aralik disi deger
/// "bilinmiyor" sayilir.
/// </summary>
public sealed record DdaDisplayInfo(
    double RedX, double RedY, double GreenX, double GreenY, double BlueX, double BlueY,
    double WhiteX, double WhiteY, double MinNits, double MaxNits, double MaxFullFrameNits);

/// <summary>HDR10 statik verisi: x265 bicimli <c>master-display</c> ve <c>MaxCLL,MaxFALL</c>.</summary>
public sealed record HdrStaticMetadata(string MasterDisplay, string ContentLight);

/// <summary>HDR korunurken kodlayici seciminin sonucu (<see cref="RecorderArguments.WithHdrCodec"/>).</summary>
public enum HdrCodecOutcome
{
    /// <summary>Kodlayici degismedi.</summary>
    Unchanged,

    /// <summary>H.264 yerine HEVC 10 bit secildi.</summary>
    SwitchedToHevc,

    /// <summary>HEVC yok; kayit SDR'ye cevrildi.</summary>
    NoHevc
}

/// <summary>
/// <c>ddagrab</c> yerine <c>gdigrab</c>'a dusulmesinin sebebi. <see cref="None"/> disindaki
/// her deger kullaniciya bir satir olarak gosterilir; sessiz dusus yok.
/// </summary>
public enum DdagrabFallback
{
    /// <summary>Dusus yok: istek <c>ddagrab</c> ile yakalanabilir.</summary>
    None,

    /// <summary>Yoklama gecmedi: eski surucu, uzak masaustu ya da <c>ddagrab</c>'siz ffmpeg derlemesi.</summary>
    Unavailable,

    /// <summary>Ilk ekran kartinin masaustune bagli bir cikisi okunamadi.</summary>
    NoOutputs,

    /// <summary>Pencere hedefinin dikdortgeni okunamadi; <c>ddagrab</c>'da pencere bolge kirpmasidir.</summary>
    NoWindowRect,

    /// <summary>Yakalanacak alan tek bir cikisin icinde degil (iki monitore yayiliyor ya da ikinci kartta).</summary>
    OutsideOneOutput
}

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

    /// <summary>Tıklamalar kayda işaretlensin mi; yalnız macOS'ta ffmpeg'in <c>-capture_mouse_clicks</c> seçeneğine gider.</summary>
    public bool ShowClicks { get; init; }

    /// <summary>
    /// macOS'ta <c>avfoundation</c> ekran adinin numarasi (<c>Capture screen N</c>), Linux'ta <see cref="Display"/> ile
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
    /// Windows'ta <c>ddagrab</c> kolunda pencerenin istemci alani, masaustu pikselinde.
    /// </summary>
    public RecorderRegion? WindowRegion { get; init; }

    /// <summary>
    /// Gizlilik maskeleri, masaustu pikselinde. Yakalama alanina kirpilip kayda bulanik yakilir
    /// (<see cref="RecorderArguments.MaskBoxes"/>); canli onizleme maskesizdir.
    /// </summary>
    public IReadOnlyList<RecorderRegion> PrivacyMasks { get; init; } = Array.Empty<RecorderRegion>();

    /// <summary>
    /// Windows'ta yakalama yolu. <see cref="RecorderCapture.Ddagrab"/>'da pencere hedefi
    /// <see cref="WindowRegion"/>'dan (pencerenin istemci alani, masaustu pikselinde) kirpilir.
    /// </summary>
    public RecorderCapture Capture { get; init; } = RecorderCapture.Gdigrab;

    /// <summary><c>ddagrab</c>'in secebildigi cikislar. Bossa <see cref="RecorderCapture.Ddagrab"/> kabul edilmez.</summary>
    public IReadOnlyList<DdaOutput> DdaOutputs { get; init; } = Array.Empty<DdaOutput>();

    /// <summary>
    /// HDR ekranda kaydi SDR'ye cevir. Varsayilan <c>false</c>: HDR cikista kayit 10 bit HDR10
    /// kalir (<see cref="RecorderArguments.HdrCapture"/>). Acikken kare 10 bit iner ve
    /// <c>zscale</c> ile ton eslenir (<see cref="RecorderArguments.HdrTonemapCapture"/>);
    /// <see cref="CanTonemap"/> kapaliysa eski yol: 8 bit <c>bgra</c>, donusumu Windows yapar.
    /// </summary>
    public bool HdrToSdr { get; init; }

    /// <summary>
    /// ffmpeg'de <c>zscale</c> var mi. Varsayilan <c>true</c>; arayuz yetenek listesinden kurar.
    /// Kapaliyken <see cref="HdrToSdr"/> ton eslemesiz eski yola duser ve
    /// <see cref="RecorderArguments.HdrTonemapMissing"/> bunu soyler.
    /// </summary>
    public bool CanTonemap { get; init; } = true;

    /// <summary>
    /// Istek bir GIF kaydinin ara Matroska yakalamasi (<see cref="RecorderArguments.CaptureRequest"/>).
    /// GIF yolu renk etiketi ve BT.709 donusumu almaz: palet RGB'den kurulur.
    /// </summary>
    public bool GifCapture { get; init; }

    /// <summary>`gdigrab`'a dusulmenin sebebi, <see cref="RecorderArguments.ChooseCapture"/> kurar; tani gunlugune yazilir.</summary>
    public DdagrabFallback CaptureFallback { get; init; }

    /// <summary>Bolge kaydinda dikdortgen.</summary>
    public RecorderRegion? Region { get; init; }

    /// <summary>Kaydin yazildigi kap; cikti uzantisi bununla ayni olmak zorunda.</summary>
    public RecorderContainer Container { get; init; } = RecorderContainer.Mp4;

    /// <summary>Goruntu kodlayicisi.</summary>
    public string VideoCodec { get; init; } = RecorderArguments.DefaultVideoCodec;

    /// <summary>Ses izi basina yazilan kodlayici; kabin tasidigi listeden (<see cref="RecorderArguments.AudioCodecsFor"/>).</summary>
    public string AudioCodec { get; init; } = RecorderArguments.DefaultAudioCodec;

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

    /// <summary>Cikisin kare zamanlamasi; <see cref="RecorderFrameRateMode.Auto"/> <c>-fps_mode</c> yazmaz.</summary>
    public RecorderFrameRateMode FrameRateMode { get; init; } = RecorderFrameRateMode.Auto;

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

public enum WebcamShape
{
    Rectangle,
    Rounded,
    Circle
}

/// <summary>
/// Kamera kutusunun karedeki yeri, kenar payindan arta kalan bosluga oranla: <c>X</c> ve <c>Y</c>
/// 0 sol/ust, 1 sag/alt kenar; <c>Size</c> kutu genisliginin kare genisligine orani.
/// </summary>
public sealed record WebcamPlacement(double X, double Y, double Size);

/// <summary>
/// <see cref="Placement"/> verilince <c>Width</c> ve <c>Corner</c> kullanilmaz; kutu karenin
/// oraniyla olceklenip serbest konuma oturur.
/// </summary>
public sealed record RecorderWebcam(
    string Device,
    int Width,
    WebcamCorner Corner = WebcamCorner.BottomRight,
    WebcamBackground Background = WebcamBackground.Keep)
{
    public WebcamShape Shape { get; init; }

    public bool Mirror { get; init; }

    public WebcamPlacement? Placement { get; init; }
}

/// <summary>
/// Kamera kutusunun bolge uzerindeki dikdortgeni ve surukleme hesabi; bolge duzenleyicisi ile
/// <see cref="RecorderArguments.WebcamGraph"/>'in <c>overlay</c> ifadesi ayni formulu kullanir.
/// Kameranin gercek en-boy orani kayittan once bilinmiyor; kutu 4:3 (dairede 1:1) cizilir.
/// </summary>
public static class WebcamLayout
{
    public static double Aspect(WebcamShape shape) => shape == WebcamShape.Circle ? 1 : 4d / 3;

    public static RecorderRegion Rect(RecorderRegion area, WebcamPlacement placement, WebcamShape shape)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(placement);
        var margin = RecorderArguments.WebcamMargin;
        var width = (int)Math.Round(area.Width * placement.Size);
        var height = (int)Math.Round(width / Aspect(shape));
        var x = area.X + margin + (int)Math.Round(Math.Max(0, area.Width - width - 2 * margin) * placement.X);
        var y = area.Y + margin + (int)Math.Round(Math.Max(0, area.Height - height - 2 * margin) * placement.Y);
        return new RecorderRegion(x, y, width, height);
    }

    public static WebcamPlacement Move(RecorderRegion area, WebcamPlacement placement, WebcamShape shape, int dx, int dy)
    {
        var rect = Rect(area, placement, shape);
        return At(area, rect.X + dx, rect.Y + dy, rect.Width, rect.Height, placement.Size);
    }

    public static WebcamPlacement Resize(RecorderRegion area, WebcamPlacement placement, WebcamShape shape, int dx)
    {
        var rect = Rect(area, placement, shape);
        var size = Math.Clamp((double)(rect.Width + dx) / area.Width, RecorderArguments.MinWebcamSize, RecorderArguments.MaxWebcamSize);
        var width = (int)Math.Round(area.Width * size);
        return At(area, rect.X, rect.Y, width, (int)Math.Round(width / Aspect(shape)), size);
    }

    private static WebcamPlacement At(RecorderRegion area, int x, int y, int width, int height, double size)
    {
        var margin = RecorderArguments.WebcamMargin;
        static double Ratio(int offset, int room) => room <= 0 ? 0 : Math.Clamp((double)offset / room, 0, 1);
        return new WebcamPlacement(
            Ratio(x - area.X - margin, area.Width - width - 2 * margin),
            Ratio(y - area.Y - margin, area.Height - height - 2 * margin),
            size);
    }
}

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

    /// <summary>Ses izi basina varsayilan kodlayici.</summary>
    public const string DefaultAudioCodec = "aac";

    /// <summary>Opus kodlayicisi; mov kabi tasimiyor (ffmpeg 9.0'da baslik yazilamiyor).</summary>
    public const string OpusAudioCodec = "libopus";

    /// <summary>
    /// Kabin tasidigi ses kodlayicilari, varsayilan basta. GIF ses tasimiyor.
    /// </summary>
    public static IReadOnlyList<string> AudioCodecsFor(RecorderContainer container) => container switch
    {
        RecorderContainer.Mp4 or RecorderContainer.Mkv => new[] { DefaultAudioCodec, OpusAudioCodec },
        RecorderContainer.Mov => new[] { DefaultAudioCodec },
        _ => Array.Empty<string>()
    };

    /// <summary>Ses izi basina yazilan bit hizi.</summary>
    public const string AudioBitrate = "160k";

    /// <summary>Linux'ta ekran adresi verilmediginde kullanilan adres.</summary>
    public const string DefaultDisplay = ":0.0";

    /// <summary>Kabul edilen en yuksek kare hizi.</summary>
    public const int MaxFps = 240;

    /// <summary>
    /// Ses girdilerinin ffmpeg girdi sirasindaki ilk numarasi. Goruntulu hedefte yakalama
    /// girdisi 0 oldugu icin 1, yalniz seste goruntu girdisi olmadigi icin 0;
    /// <see cref="AudioCaptureArguments.Build"/>in ucuncu argumani bu olmak zorunda, yoksa
    /// <c>amix</c> grafigi var olmayan bir girdiye bakar. Sayi arayuzde uydurulmuyor,
    /// motordan okunuyor.
    /// </summary>
    public static int AudioFirstInputIndexFor(RecorderTargetKind target)
        => target == RecorderTargetKind.AudioOnly ? 0 : 1;

    private static readonly string[] KnownVideoCodecs =
    {
        "libx264", "libx265", "libsvtav1", "libvpx-vp9",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf",
        "h264_videotoolbox", "hevc_videotoolbox",
        "h264_vaapi", "hevc_vaapi"
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

    /// <summary>
    /// MP4 ve MOV teslimi Matroska'ya yakalanir, durunca <c>-c copy -movflags +faststart</c> ile
    /// teslim kabina cevrilir. mp4/mov muxer'i <c>moov</c> atomunu kapanista yaziyor: oldurulen
    /// surec oynatilamaz dosya birakiyordu (<see cref="SurvivesKill"/>). Parcali mp4
    /// (<c>empty_moov</c>) oldurulmeye dayanirdi ama paylasim yuzeylerinde (WhatsApp, bazi
    /// tarayici oynaticilari) aranamiyor ya da reddediliyor; one alinmis <c>moov</c>'lu
    /// duz mp4 hepsinde aciliyor.
    /// </summary>
    public static bool CapturesInMatroska(RecorderRequest request)
        => request.Container is RecorderContainer.Mp4 or RecorderContainer.Mov;

    private const string CaptureMark = ".yakalama";

    public static string MatroskaCapturePath(string outputPath)
        => Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outputPath) + CaptureMark + ".mkv");

    public static string MatroskaDeliveryPath(string capturePath, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(capturePath);
        var mark = stem.LastIndexOf(CaptureMark, StringComparison.Ordinal);
        if (mark >= 0) stem = stem.Remove(mark, CaptureMark.Length);
        return Path.Combine(Path.GetDirectoryName(capturePath) ?? string.Empty, stem + extension);
    }

    /// <summary>
    /// EKA-20: dosya adi Matroska yakalamasinin isaretini (<c>&lt;ad&gt;.yakalama.mkv</c>) tasiyor mu.
    /// Teslimden sonra bu ad diskte kalmaz; kalmissa kayit cokmus ya da kesilmistir.
    /// </summary>
    public static bool IsMatroskaCapture(string path)
        => !string.IsNullOrWhiteSpace(path)
           && Path.GetFileName(path).EndsWith(CaptureMark + ".mkv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Yarim kalan yakalamanin kurtarma hedefi: MP4 ya da MOV seciliyse o kap, degilse MP4
    /// (yakalama yalniz bu iki kap icin yazilir). Ad doluysa <c>_2</c>, <c>_3</c> eklenir.
    /// </summary>
    public static string RecoveryTarget(string capturePath, RecorderContainer container, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var extension = "." + Extension(container is RecorderContainer.Mov ? RecorderContainer.Mov : RecorderContainer.Mp4);
        var first = MatroskaDeliveryPath(capturePath, extension);
        var folder = Path.GetDirectoryName(first) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(first);
        var candidate = first;
        for (var index = 2; exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }

    /// <summary>Matroska yakalamasini teslim kabina <c>-c copy -movflags +faststart</c> ile aktarir.</summary>
    public static IReadOnlyList<string> BuildMatroskaDelivery(string source, string target)
        => new[] { "-hide_banner", "-y", "-nostdin", "-i", source, "-map", "0", "-c", "copy", "-movflags", "+faststart", target };

    public static RecorderRequest CaptureRequest(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Container == RecorderContainer.Gif
            ? request with { Container = CaptureContainer(request.Container), GifCapture = true }
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
    /// (<c>KayitBolmeTests</c>). Bayrak da yetmiyor: muxer kumeyi (cluster) 5 sn tutuyor,
    /// 3 sn'de oldurulen kayit 582 baytlik basliktan ibaretti; <c>-cluster_time_limit 500</c>
    /// ile ayni oldurme 12 okunur paket birakti (<c>KayitMotoruTests</c>).
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
        "hevc_qsv" or "h264_amf" or "hevc_amf" or "av1_qsv" or "av1_amf" => EightAndTenBit420,
        "h264_videotoolbox" or "hevc_videotoolbox" or "h264_vaapi" or "hevc_vaapi" => Only420,
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
        var qsv = c is "h264_qsv" or "hevc_qsv" or "av1_qsv";
        var packed10 = qsv || c is "h264_nvenc" or "hevc_nvenc" or "av1_nvenc" or "h264_amf" or "hevc_amf" or "av1_amf";
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
    /// Bolgenin en cok ortustugu monitorun indeksi. macOS'ta <c>avfoundation</c> ekrani adla
    /// aciyor ve bolge o ekranin karesinden kesiliyor; bolge ikinci ekrandayken ilk ekrani
    /// acmak bos ya da yanlis kare verir. Esitlikte listede once gelen kazanir; liste bos ya
    /// da hicbir monitorle ortusmuyorsa 0.
    /// </summary>
    public static int ScreenOf(IReadOnlyList<ScreenBounds> screens, RecorderRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (screens is null) return 0;

        var best = 0;
        long bestArea = 0;
        foreach (var screen in screens)
        {
            long width = Math.Min(region.X + region.Width, screen.X + screen.Width) - Math.Max(region.X, screen.X);
            long height = Math.Min(region.Y + region.Height, screen.Y + screen.Height) - Math.Max(region.Y, screen.Y);
            if (width <= 0 || height <= 0) continue;
            var area = width * height;
            if (area <= bestArea) continue;
            bestArea = area;
            best = screen.Index;
        }

        return best;
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
        if (request.Target == RecorderTargetKind.AudioOnly) return AudioOnlyErrors(request, outputPath);

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
        else
            errors.AddRange(CodecPlatformErrors(request));

        errors.AddRange(RegionErrors(request));
        errors.AddRange(ScaleErrors(request));
        errors.AddRange(RateControlErrors(request));
        errors.AddRange(StreamShapeErrors(request));
        errors.AddRange(ColourErrors(request));
        errors.AddRange(LimitErrors(request));
        errors.AddRange(GifErrors(request));
        errors.AddRange(WebcamErrors(request));
        errors.AddRange(PreviewErrors(request));
        errors.AddRange(MaskErrors(request));

        if (request.Target == RecorderTargetKind.Window)
            errors.AddRange(WindowErrors(request));

        errors.AddRange(ScreenSelectionErrors(request));
        errors.AddRange(CaptureErrors(request));

        if (request.Platform != RecorderPlatform.Windows && LoopbackAudio.Uses(request.Audio?.Inputs))
            errors.Add("System audio loopback capture is only available on Windows.");

        errors.AddRange(AudioCodecErrors(request));
        return errors;
    }

    private static List<string> AudioOnlyErrors(RecorderRequest request, string outputPath)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(outputPath)) errors.Add("Output path is required.");
        else if (ContainerOf(outputPath) is not { } written)
            errors.Add($"The {Path.GetExtension(outputPath)} output extension is not one of the recorder's containers (mp4, mkv, mov, gif).");
        else if (written != request.Container)
            errors.Add($"The output extension says {Extension(written)} but the selected container is {Extension(request.Container)}.");

        if (request.Audio is not { InputCount: > 0 })
            errors.Add("Audio-only recording needs at least one audio input.");
        if (request.Container == RecorderContainer.Gif)
            errors.Add("Audio-only recording cannot be written to gif.");
        if (request.Webcam is not null)
            errors.Add("Audio-only recording has no picture to place a webcam on.");
        if (!string.IsNullOrWhiteSpace(request.PreviewPath))
            errors.Add("Audio-only recording has no picture to preview.");
        if (request.Capture == RecorderCapture.Ddagrab)
            errors.Add("Audio-only recording does not capture the screen.");

        errors.AddRange(LimitErrors(request));
        if (request.Platform != RecorderPlatform.Windows && LoopbackAudio.Uses(request.Audio?.Inputs))
            errors.Add("System audio loopback capture is only available on Windows.");

        if (request.Container != RecorderContainer.Gif) errors.AddRange(AudioCodecErrors(request));
        return errors;
    }

    private static IEnumerable<string> AudioCodecErrors(RecorderRequest request)
    {
        if (request.Audio is not { InputCount: > 0 } && request.Target != RecorderTargetKind.AudioOnly) yield break;
        if (request.Container == RecorderContainer.Gif) yield break;
        if (!AudioCodecsFor(request.Container).Contains(request.AudioCodec, StringComparer.Ordinal))
            yield return $"The {request.AudioCodec} audio encoder cannot be written to {Extension(request.Container)}.";
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
    /// eski kural duruyor. macOS'ta bolge girdi degil secilen ekranin karesinde kirpma
    /// filtresi: kirpma o ekranin kokenine gore yerellesmis haliyle negatif degilse gecerli.
    /// </para>
    /// </summary>
    private static IEnumerable<string> NegativeOffsetErrors(RecorderRequest request)
    {
        if (request.Platform == RecorderPlatform.MacOs)
        {
            if (MacCrop(request) is { X: >= 0, Y: >= 0 }) yield break;
        }
        else if (request.Screens.Count > 0
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

    /// <summary>
    /// Kayit platformunun karsiligi; <see cref="CodecModel.OnlyPlatform"/> bununla karsilastirilir.
    /// </summary>
    public static HostPlatform HostOf(RecorderPlatform platform) => platform switch
    {
        RecorderPlatform.MacOs => HostPlatform.MacOS,
        RecorderPlatform.Linux => HostPlatform.Linux,
        _ => HostPlatform.Windows
    };

    private static IEnumerable<string> CodecPlatformErrors(RecorderRequest request)
    {
        if (CodecModel.OnlyPlatform(request.VideoCodec) is { } only && only != HostOf(request.Platform))
            yield return $"The {request.VideoCodec} video encoder only runs on {only}.";
        if (request.RateControl == RecorderRateControl.Quality && !CodecModel.HasQualityScale(request.VideoCodec))
            yield return $"The {request.VideoCodec} video encoder has no quality scale; use the bitrate arm.";
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

    public const int DefaultWebcamWidth = 240;

    public const double MinWebcamSize = 0.05;

    public const double MaxWebcamSize = 0.5;

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
        if (!Enum.IsDefined(cam.Shape))
            yield return "The webcam shape is not one of rectangle, rounded or circle.";
        if (cam.Placement is { } place
            && !(place.X is >= 0 and <= 1 && place.Y is >= 0 and <= 1 && place.Size is >= MinWebcamSize and <= MaxWebcamSize))
            yield return $"The webcam position must lie between 0 and 1 and its size between {MinWebcamSize.ToString(CultureInfo.InvariantCulture)} and {MaxWebcamSize.ToString(CultureInfo.InvariantCulture)} of the frame width.";
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

    /// <summary>
    /// Daire ve yuvarlak kose icin saydamlik maskesi: kosedeki ceyrek dairelerin disinda kalan
    /// piksel saydam olur, anahtarlanmis arka planin saydamligi korunur (<c>alpha(X,Y)</c> ile carpilir).
    /// </summary>
    public static string WebcamMask(WebcamShape shape)
    {
        var radius = shape switch
        {
            WebcamShape.Circle => "min(W,H)/2",
            WebcamShape.Rounded => "min(W,H)/5",
            _ => null
        };
        return radius is null
            ? string.Empty
            : $"geq=lum='lum(X,Y)':cb='cb(X,Y)':cr='cr(X,Y)':a='alpha(X,Y)*lte(hypot(max(abs(X-(W-1)/2)-W/2+{radius},0),max(abs(Y-(H-1)/2)-H/2+{radius},0)),{radius})'";
    }

    private static string Ratio(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static string? WebcamGraph(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Webcam is not { } cam) return null;
        var index = Number(AudioFirstInputIndexFor(request.Target) + (request.Audio?.InputCount ?? 0));

        var before = new List<string>();
        if (cam.Mirror) before.Add("hflip");
        if (cam.Shape == WebcamShape.Circle) before.Add("crop='min(iw,ih)':'min(iw,ih)'");

        var key = WebcamKey(cam.Background);
        var mask = WebcamMask(cam.Shape);
        var after = key + (mask.Length == 0 ? string.Empty : (key.Length == 0 ? ",format=yuva420p," : ",") + mask);
        var source = VideoFilter(request) ?? "null";
        var colour = SdrColour(request) ? "," + SdrColourLink(request) : string.Empty;
        var margin = Number(WebcamMargin);

        if (cam.Placement is not { } place)
        {
            var chain = string.Join(",", before.Append($"scale={Number(cam.Width)}:-2"));
            return $"[0:v]{source}[base];"
                   + $"[{index}:v]{chain}{after}[cam];"
                   + $"[base][cam]overlay={WebcamPosition(cam.Corner)}:eof_action=repeat{colour}[{WebcamOutputLabel}]";
        }

        var room = Number(2 * WebcamMargin);
        return $"[0:v]{source},split[base][wref];"
               + $"[{index}:v]{(before.Count == 0 ? "null" : string.Join(",", before))}[wsrc];"
               + $"[wsrc][wref]scale=w='trunc(rw*{Ratio(place.Size)}/2)*2':h=-2{after}[cam];"
               + $"[base][cam]overlay=x='{margin}+(main_w-overlay_w-{room})*{Ratio(place.X)}'"
               + $":y='{margin}+(main_h-overlay_h-{room})*{Ratio(place.Y)}':eof_action=repeat{colour}[{WebcamOutputLabel}]";
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

    private static IEnumerable<string> CaptureErrors(RecorderRequest request)
    {
        if (request.Capture != RecorderCapture.Ddagrab) yield break;

        if (request.Platform != RecorderPlatform.Windows)
        {
            yield return "ddagrab (Desktop Duplication) exists only on Windows.";
            yield break;
        }

        if (DdagrabBlocker(request) is var blocker and not DdagrabFallback.None)
            yield return "ddagrab cannot capture this request: " + blocker switch
            {
                DdagrabFallback.NoOutputs => "no DXGI output of the first graphics adapter was enumerated.",
                DdagrabFallback.NoWindowRect => "the window rectangle is unknown.",
                _ => "the capture area is not inside a single DXGI output."
            };
    }

    /// <summary>
    /// <c>ddagrab</c>'in yakalayacagi alan, masaustu pikselinde: bolge hedefinde bolge, pencere
    /// hedefinde pencerenin istemci alani, ekran hedefinde secilen monitor. Monitor listesi bos ve
    /// indeks sifirsa birincil cikis (masaustu koordinati 0,0 olan).
    /// </summary>
    public static RecorderRegion? CaptureArea(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Target switch
        {
            RecorderTargetKind.Region => request.Region,
            RecorderTargetKind.Window => request.WindowRegion,
            RecorderTargetKind.AudioOnly => null,
            _ when request.Screens.Count > 0 => RegionForScreen(request.Screens, request.ScreenIndex),
            _ when request.ScreenIndex == 0 => request.DdaOutputs.FirstOrDefault(o => o.X == 0 && o.Y == 0) is { } primary
                ? new RecorderRegion(primary.X, primary.Y, primary.Width - primary.Width % 2, primary.Height - primary.Height % 2)
                : null,
            _ => null
        };
    }

    /// <summary>
    /// <c>ddagrab</c>'in cikisi ve o cikisin sol ust kosesine gore alan. Alan cikisin tamamen
    /// icinde olmali: <c>ddagrab</c> tek cikis okuyor, iki monitore yayilan dikdortgeni
    /// kesmiyor, <c>gdigrab</c>'a dusuluyor. Boyut <c>yuv420p</c> icin asagi ciftlenir.
    /// </summary>
    public static (DdaOutput Output, RecorderRegion Local)? DdagrabTarget(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (CaptureArea(request) is not { } area) return null;

        var width = area.Width - area.Width % 2;
        var height = area.Height - area.Height % 2;
        if (width < 2 || height < 2) return null;

        foreach (var output in request.DdaOutputs)
        {
            var x = area.X - output.X;
            var y = area.Y - output.Y;
            if (x < 0 || y < 0 || x + width > output.Width || y + height > output.Height) continue;
            return (output, new RecorderRegion(x, y, width, height));
        }

        return request.Target == RecorderTargetKind.Window ? ClippedWindow(request.DdaOutputs, area) : null;
    }

    /// <summary>
    /// Ekrana tam sigmayan pencerenin <c>ddagrab</c> alani: pencere tek cikisa degiyorsa disari
    /// tasan kisim zaten gorunmuyor, kirpilir; birden cok cikisa degiyor ve biri HDR ise en buyuk
    /// payin cikisina kirpilir, cunku <c>gdigrab</c> HDR masaustunu yanlis renkle okuyor. HDR
    /// olmayan cok ekranli pencere <c>null</c> doner ve <c>gdigrab</c>'da butun kalir.
    /// </summary>
    private static (DdaOutput Output, RecorderRegion Local)? ClippedWindow(IReadOnlyList<DdaOutput> outputs, RecorderRegion area)
    {
        var touched = outputs
            .Select(o => (Output: o, Left: Math.Max(area.X, o.X), Top: Math.Max(area.Y, o.Y),
                Right: Math.Min(area.X + area.Width, o.X + o.Width), Bottom: Math.Min(area.Y + area.Height, o.Y + o.Height)))
            .Where(t => t.Right > t.Left && t.Bottom > t.Top)
            .ToList();
        if (touched.Count == 0 || (touched.Count > 1 && !touched.Any(t => t.Output.Hdr))) return null;

        var best = touched.OrderByDescending(t => (long)(t.Right - t.Left) * (t.Bottom - t.Top)).First();
        var width = best.Right - best.Left;
        var height = best.Bottom - best.Top;
        width -= width % 2;
        height -= height % 2;
        if (width < 2 || height < 2) return null;
        return (best.Output, new RecorderRegion(best.Left - best.Output.X, best.Top - best.Output.Y, width, height));
    }

    /// <summary>Pencere ekrana sigmadigi icin <c>ddagrab</c> alani pencereden kucuk; tani gunlugu soyler.</summary>
    public static bool WindowClipped(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Target == RecorderTargetKind.Window && request.WindowRegion is { } w && DdagrabTarget(request) is { } t
               && (t.Local.Width < w.Width - w.Width % 2 || t.Local.Height < w.Height - w.Height % 2);
    }

    /// <summary>
    /// Istegin <c>ddagrab</c> ile yakalanmasini engelleyen yerlesim sebebi. Yoklama sonucunu
    /// bilmez: ffmpeg'in <c>ddagrab</c>'i acip acamadigini <see cref="BuildDdagrabProbe"/> olcer.
    /// </summary>
    public static DdagrabFallback DdagrabBlocker(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Platform != RecorderPlatform.Windows) return DdagrabFallback.Unavailable;
        if (request.DdaOutputs.Count == 0) return DdagrabFallback.NoOutputs;
        if (request.Target == RecorderTargetKind.Window && request.WindowRegion is null) return DdagrabFallback.NoWindowRect;
        return DdagrabTarget(request) is null ? DdagrabFallback.OutsideOneOutput : DdagrabFallback.None;
    }

    /// <summary>
    /// Yakalama yolunun secimi. Yerlesim engeli ya da gecmeyen yoklama <c>gdigrab</c>'a
    /// dusurur ve sebebi <paramref name="reason"/>'da doner; cagiran bunu kullaniciya gosterir.
    /// Windows disinda istek degismez ve sebep <see cref="DdagrabFallback.None"/>'dir.
    /// </summary>
    public static RecorderRequest ChooseCapture(RecorderRequest request, bool ddagrabWorks, out DdagrabFallback reason)
    {
        ArgumentNullException.ThrowIfNull(request);
        reason = DdagrabFallback.None;
        if (request.Platform != RecorderPlatform.Windows || request.Target == RecorderTargetKind.AudioOnly)
            return request with { Capture = RecorderCapture.Gdigrab };

        reason = DdagrabBlocker(request);
        if (reason == DdagrabFallback.None && !ddagrabWorks) reason = DdagrabFallback.Unavailable;
        return request with { Capture = reason == DdagrabFallback.None ? RecorderCapture.Ddagrab : RecorderCapture.Gdigrab, CaptureFallback = reason };
    }

    /// <summary>
    /// <c>ddagrab</c>'in bu istekte acilip acilmadigini olcen kisa kosum: ayni kaynak grafigi,
    /// tek kare, <c>null</c> cikis. Uzak masaustunde ve eski surucude kaynak acilmiyor,
    /// <c>ddagrab</c>'siz derlemede filtre bulunmuyor; ucunde de kosum sifir disinda doner.
    /// </summary>
    public static IReadOnlyList<string> BuildDdagrabProbe(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (DdagrabBlocker(request) is var blocker and not DdagrabFallback.None)
            throw new InvalidOperationException($"ddagrab cannot capture this request ({blocker}).");

        var a = new List<string> { "-hide_banner", "-nostdin" };
        a.AddRange(DdagrabInput(request));
        a.AddRange(new[] { "-frames:v", "1", "-f", "null", "-" });
        return a;
    }

    /// <summary>
    /// Oz sinamanin tek karelik yakalamasi: kaydin kendi <c>ddagrab</c> girdisi, kodlayici yerine
    /// <paramref name="rawPath"/>'e ham <c>rgb24</c>. Renk karti boylece kaydin goruntu yolundan
    /// gecer, kodlayicinin kaybi olcuye karismaz.
    /// </summary>
    public static IReadOnlyList<string> BuildFrameGrab(RecorderRequest request, string rawPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPath);
        if (DdagrabBlocker(request) is var blocker and not DdagrabFallback.None)
            throw new InvalidOperationException($"ddagrab cannot capture this request ({blocker}).");

        var a = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-y" };
        a.AddRange(DdagrabInput(request));
        a.AddRange(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", rawPath });
        return a;
    }

    /// <summary>
    /// <c>ddagrab</c> girdisi. Kaynak D3D11 yuzeyi veriyor; <c>hwdownload,format=bgra</c> ayni
    /// grafikte kareyi bellege indiriyor, boylece <c>-vf</c>, kamera bindirmesi, onizleme ve
    /// her kodlayici <c>gdigrab</c>'daki gibi bgra kare goruyor. NVENC'e dogrudan D3D11 yuzeyi
    /// vermek olculdu ve birakildi: <c>-pix_fmt</c> ile acilmiyor, onsuz dosya <c>gbr</c>
    /// renk uzayiyla yanlis etiketleniyor (<c>docs/olcumler/kaydedici-ddagrab.md</c>).
    /// </summary>
    private static IReadOnlyList<string> DdagrabInput(RecorderRequest request)
    {
        var (output, local) = DdagrabTarget(request)!.Value;
        var source = $"ddagrab=output_idx={Number(output.Index)}:framerate={Number(request.Fps)}:draw_mouse={Flag(request.ShowCursor)}";
        if (local.X != 0 || local.Y != 0 || local.Width != output.Width || local.Height != output.Height)
            source += $":offset_x={Number(local.X)}:offset_y={Number(local.Y)}:video_size={Size(local)}";
        if (HdrCapture(request))
            return new[] { "-f", "lavfi", "-i", source + ":output_fmt=10bit,hwdownload,format=x2bgr10," + HdrFrameChain };
        if (HdrTonemapCapture(request))
            return new[] { "-f", "lavfi", "-i", source + ":output_fmt=10bit,hwdownload,format=x2bgr10," + HdrSdrChain(SdrWhite(request), PeakNits(request)) };
        return new[] { "-f", "lavfi", "-i", source + ",hwdownload,format=bgra" };
    }

    /// <summary>Windows'un SDR beyaz duzeyi okunamazsa varsayilan: Windows'un kendi varsayilani degil, HDR10 referans beyazi (BT.2408).</summary>
    public const double DefaultSdrWhiteNits = 203;

    /// <summary>Ekran tepe parlakligini bildirmiyorsa egrinin tavani.</summary>
    public const double DefaultPeakNits = 1000;

    /// <summary>Hedef cikisin SDR beyaz duzeyi (nit); okunamadiysa <see cref="DefaultSdrWhiteNits"/>.</summary>
    public static double SdrWhite(RecorderRequest request)
        => ReadSdrWhite(request) ?? DefaultSdrWhiteNits;

    /// <summary>HDR'den SDR'ye cevrilen kayitta SDR beyaz duzeyi okunamadi ve varsayilan kullanildi; cagiran soyler.</summary>
    public static bool SdrWhiteAssumed(RecorderRequest request)
        => HdrTonemapCapture(request) && ReadSdrWhite(request) is null;

    /// <summary>Varsayilan beyaz kullanildiginda gunluge yazilan makine satiri; dili sabit, ekrana cikmaz.</summary>
    public static string SdrWhiteAssumedLogLine
        => "recorder: SDR white level unreadable, assumed "
           + DefaultSdrWhiteNits.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " nits";

    private static double? ReadSdrWhite(RecorderRequest request)
        => DdagrabTarget(request) is { Output.SdrWhiteNits: { } w } && double.IsFinite(w) && w >= 80 ? w : null;

    /// <summary>Hedef ekranin bildirdigi tepe parlaklik (nit); bilinmiyorsa <see cref="DefaultPeakNits"/>.</summary>
    public static double PeakNits(RecorderRequest request)
        => DdagrabTarget(request) is { Output.Display.MaxNits: var m } && double.IsFinite(m) && m >= 1 ? m : DefaultPeakNits;

    /// <summary>
    /// HDR masaustunu SDR'ye Windows'un SDR'yi HDR'ye cevirmesinin tersiyle indirir. Windows SDR
    /// pikseli <paramref name="whiteNits"/> parlakliga koyar; PQ dogrusala <c>npl</c> ile acilinca
    /// o parlaklik 1.0 olur ve SDR piksel kendi degerine doner. 1.0'in ustu (Auto HDR, yerel HDR
    /// parlaklari) <c>mobius</c> ile kirilir: 0.99'un alti oldugu gibi kalir, ust ekranin tepe
    /// parlakligina kadar 1.0'a sikisir, renk tonu korunur (en parlak bilesene gore olceklenir).
    /// Sonra sRGB egrisi ve 8 bit <c>bgra</c>: kodlayiciya kadar yol 8 bit yakalamayla ayni.
    /// <para>ddagrab'in <c>rgbaf16</c> cikisi denendi ve birakildi: ffmpeg 9.0'da swscale yarim
    /// kayan noktayi 1.0'da kirpiyor, 1.0'in ustu kayboluyor (<c>docs/olcumler/kaydedici-hdr.md</c>).</para>
    /// </summary>
    public static string HdrSdrChain(double whiteNits, double peakNits)
    {
        var w = double.IsFinite(whiteNits) && whiteNits >= 80 ? whiteNits : DefaultSdrWhiteNits;
        var peak = Math.Max(double.IsFinite(peakNits) ? peakNits : DefaultPeakNits, w * 1.05) / w;
        return $"zscale=tin=smpte2084:pin=bt2020:rin=full:t=linear:p=bt709:npl={Decimal(w)},format=gbrpf32le,"
               + $"tonemap=mobius:param=0.99:desat=0:peak={Decimal(peak)},"
               + "zscale=tin=linear:pin=bt709:rin=full:t=iec61966-2-1:p=bt709:r=full,format=gbrp,format=bgra";
    }

    private static string Decimal(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// HDR karenin bellekteki yolu. <c>ddagrab</c>'in 10 bit cikisi BT.2020 + PQ'lu RGB; matris
    /// BT.2020 ile acikca YUV'a cevrilir, yoksa sonraki suzgecler ffmpeg'in varsayilan BT.601
    /// donusumunu kuruyor. <c>setparams</c> olmadan kodlayicinin <c>-color_trc</c>/<c>-color_primaries</c>
    /// secenekleri ffmpeg 9.0'da dosyaya "unknown" yaziliyordu (<c>docs/olcumler/kaydedici-hdr.md</c>).
    /// <c>limiter</c> TV araligini zorlar (Y 64-940, renk 64-960): yasal araligin ustundeki
    /// "super beyaz" oynaticida kirpiliyor ya da tepe parlakligi sasirtiyor.
    /// </summary>
    internal const string HdrFrameChain =
        "scale=out_color_matrix=bt2020:out_range=tv,format=yuv444p10le,"
        + "limiter=min=64:max=940:planes=1,limiter=min=64:max=960:planes=6,"
        + "setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv";

    /// <summary>
    /// Kayit HDR10 olarak mi yakalanacak: <c>ddagrab</c> kolu, hedef cikis HDR, SDR'ye cevirme
    /// kapali, kap GIF degil ve kodlayici 10 bit bir bicim aliyor. HDR olmayan cikista hep
    /// <c>false</c>: davranis degismez.
    /// </summary>
    public static bool HdrCapture(RecorderRequest request)
        => HdrDisplay(request) && !request.HdrToSdr && HdrPixelFormat(request) is not null;

    /// <summary>
    /// Ekran HDR, SDR'ye cevirme istenmedi ama kayit HDR kalamiyor (kodlayici 10 bit almiyor ya da
    /// kap GIF). Cagiran bunu kullaniciya soyler; sessiz dusus yok.
    /// </summary>
    public static bool HdrLost(RecorderRequest request)
        => HdrDisplay(request) && !request.HdrToSdr && HdrPixelFormat(request) is null;

    /// <summary>
    /// HDR cikista SDR'ye cevirme istendi ve <c>zscale</c> var: kare 10 bit iner ve
    /// <see cref="HdrSdrChain"/> ile Windows'un SDR beyaz duzeyinden geri cevrilir. Genel bir ton
    /// esleme (hable) SDR icerigin rengini de degistiriyordu; bu yol SDR pikseli yerinde birakir.
    /// GIF yakalamasi eski yolda kalir.
    /// </summary>
    public static bool HdrTonemapCapture(RecorderRequest request)
        => HdrDisplay(request) && request.HdrToSdr && request.CanTonemap;

    /// <summary>SDR'ye cevirme istendi ama <c>zscale</c> yok: kayit eski 8 bit yolda, cagiran soyler.</summary>
    public static bool HdrTonemapMissing(RecorderRequest request)
        => HdrDisplay(request) && request.HdrToSdr && !request.CanTonemap;

    /// <summary>
    /// Kayit <c>gdigrab</c>'a dustu ve yakalanan ekran HDR: GDI kareyi HDR masaustunden
    /// dogru okumuyor, renkler bozuk cikabilir. Hedef cikis <see cref="RecorderRequest.DdaOutputs"/>'tan
    /// bilinir; alan tek cikisa oturmuyorsa herhangi bir cikis HDR ise <c>true</c>.
    /// </summary>
    public static bool HdrOnGdigrab(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Platform != RecorderPlatform.Windows || request.Capture != RecorderCapture.Gdigrab
            || request.Target == RecorderTargetKind.AudioOnly)
            return false;
        return DdagrabTarget(request) is { } target ? target.Output.Hdr : request.DdaOutputs.Any(o => o.Hdr);
    }

    /// <summary>
    /// HDR korunurken H.264 kullanilmaz: High 10 profili donanim cozuculerinde ve paylasim
    /// uygulamalarinda yesil bant ya da ezik renk olarak cikiyor. Ayni ureticinin HEVC'si,
    /// sonra diger donanim HEVC'leri, sonra <c>libx265</c> denenir; <paramref name="works"/>
    /// kodlayicinin bu makinede acildigini soyler.
    /// <para>Kodlayici H.264 degilse <c>null</c> (degisiklik yok), HEVC bulunamazsa bos dize.</para>
    /// </summary>
    public static string? HdrHevcCodec(string codec, Func<string, bool> works)
    {
        ArgumentNullException.ThrowIfNull(works);
        if (!IsH264(codec)) return null;
        foreach (var candidate in HdrHevcOrder(codec))
            if (works(candidate)) return candidate;
        return string.Empty;
    }

    /// <summary>H.264 kodlayicisinin HDR icin denenecek HEVC sirasi; H.264 degilse bos.</summary>
    public static IReadOnlyList<string> HdrHevcOrder(string codec)
    {
        var c = (codec ?? string.Empty).ToLowerInvariant();
        if (!IsH264(c)) return Array.Empty<string>();
        var same = c switch
        {
            "h264_nvenc" => "hevc_nvenc",
            "h264_amf" => "hevc_amf",
            "h264_qsv" => "hevc_qsv",
            _ => null
        };
        return new[] { same, "hevc_nvenc", "hevc_amf", "hevc_qsv", "libx265" }
            .Where(n => n is not null).Select(n => n!).Distinct(StringComparer.Ordinal)
            .Where(n => PixelFormatsFor(n).Contains("yuv420p10le", StringComparer.Ordinal))
            .ToArray();
    }

    /// <summary>Kodlayici H.264 ailesinden mi.</summary>
    public static bool IsH264(string codec)
    {
        var c = (codec ?? string.Empty).ToLowerInvariant();
        return c == "libx264" || c.StartsWith("h264_", StringComparison.Ordinal);
    }

    /// <summary>
    /// HDR korunacaksa H.264'u HEVC 10 bit'e ceviren istek. Kodlayici zaten H.264 degilse ya
    /// da HDR korunmuyorsa istek aynen doner. HEVC yoksa kayit SDR'ye cevrilir
    /// (<see cref="RecorderRequest.HdrToSdr"/>) ve <paramref name="outcome"/> bunu soyler.
    /// </summary>
    public static RecorderRequest WithHdrCodec(RecorderRequest request, Func<string, bool> works, out HdrCodecOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(request);
        outcome = HdrCodecOutcome.Unchanged;
        if (!HdrCapture(request)) return request;
        var hevc = HdrHevcCodec(request.VideoCodec, works);
        if (hevc is null) return request;
        if (hevc.Length == 0)
        {
            outcome = HdrCodecOutcome.NoHevc;
            return request with { HdrToSdr = true };
        }
        outcome = HdrCodecOutcome.SwitchedToHevc;
        return request with { VideoCodec = hevc, Profile = null, Tune = null };
    }

    private static bool GifPath(RecorderRequest request)
        => request.GifCapture || request.Container == RecorderContainer.Gif;

    private static bool HdrDisplay(RecorderRequest request)
        => request.Platform == RecorderPlatform.Windows
           && request.Capture == RecorderCapture.Ddagrab
           && DdagrabTarget(request) is { Output.Hdr: true };

    /// <summary>
    /// Windows'ta SDR kaydin RGB'den YUV'a donusumu acikca yazilir. Yazilmazsa ffmpeg
    /// <c>bgra</c>'yi BT.601 ile ceviriyor ve dosya etiketsiz kaliyordu; oynatici etiketsizi
    /// BT.709 sayinca renkler kayiyordu. HDR10 kayit ve GIF yolu bunu almaz.
    /// </summary>
    private static bool SdrColour(RecorderRequest request)
        => request.Platform == RecorderPlatform.Windows
           && request.Target != RecorderTargetKind.AudioOnly
           && !HdrCapture(request)
           && !GifPath(request);

    private static string SdrColorSpace(RecorderRequest request)
        => string.IsNullOrWhiteSpace(request.ColorSpace) ? "bt709" : request.ColorSpace!;

    private static string SdrColorRange(RecorderRequest request)
        => string.IsNullOrWhiteSpace(request.ColorRange) ? "tv" : request.ColorRange!;

    /// <summary>
    /// Donusumun suzgeci: matris ve aralik etiketle ayni, bicim kodlayicinin bicimi. Bicim
    /// burada sabitlenmezse ffmpeg donusumu kodlayicinin onune kendi varsayilaniyla koyar.
    /// <c>setparams</c> karenin kendisini etiketler: ffmpeg kodlayiciya karenin renk bilgisini
    /// gecirir ve etiketsiz kare <c>-color_primaries</c>/<c>-color_trc</c>'yi dosyada "unknown" birakiyor.
    /// </summary>
    internal static string SdrColourLink(RecorderRequest request)
    {
        var matrix = SdrColorSpace(request).ToLowerInvariant() switch
        {
            "bt470bg" => "bt470",
            "smpte170m" => "smpte170m",
            "smpte240m" => "smpte240m",
            "bt2020nc" or "bt2020c" => "bt2020",
            _ => "bt709"
        };
        var range = SdrColorRange(request).ToLowerInvariant() is "pc" or "full" or "jpeg" ? "pc" : "tv";
        var space = matrix switch
        {
            "bt470" => "bt470bg",
            "bt2020" => "bt2020nc",
            _ => matrix
        };
        return $"scale=out_color_matrix={matrix}:out_range={range},format={OutputPixelFormat(request)},"
               + $"setparams=color_primaries=bt709:color_trc=bt709:colorspace={space}:range={range}";
    }

    private static string? HdrPixelFormat(RecorderRequest request)
    {
        if (request.Container == RecorderContainer.Gif) return null;
        var formats = PixelFormatsFor(request.VideoCodec);
        var wanted = (request.PixelFormat ?? string.Empty).ToLowerInvariant() switch
        {
            "yuv422p" or "yuv422p10le" => "yuv422p10le",
            "yuv444p" or "yuv444p10le" => "yuv444p10le",
            _ => "yuv420p10le"
        };
        if (formats.Contains(wanted, StringComparer.Ordinal)) return wanted;
        return formats.Contains("yuv420p10le", StringComparer.Ordinal) ? "yuv420p10le" : null;
    }

    /// <summary>
    /// HDR kaydin profili: kullanicinin 8 bit profili 10 bit karsiligina cekilir, yoksa
    /// kodlayici acilmiyor (libx264 "high" + 10 bit).
    /// </summary>
    private static string? HdrProfile(RecorderRequest request, string pixelFormat)
    {
        var profile = request.Profile;
        if (string.IsNullOrWhiteSpace(profile)) return profile;
        var allowed = ProfilesFor(request.VideoCodec);
        if (ReferenceEquals(allowed, Vp9Profiles)) return Vp9ProfileFor(pixelFormat);
        if (ReferenceEquals(allowed, HevcProfiles)) return profile == "main" ? "main10" : profile;
        if (!ReferenceEquals(allowed, H264Profiles)) return profile;
        return pixelFormat switch
        {
            "yuv420p10le" when profile is "baseline" or "main" or "high" => "high10",
            "yuv422p10le" when profile is not "high422" and not "high444" => "high422",
            "yuv444p10le" => "high444",
            _ => profile
        };
    }

    private static IEnumerable<string> WindowErrors(RecorderRequest request) => request.Platform switch
    {
        RecorderPlatform.Windows when request.Capture == RecorderCapture.Ddagrab => Array.Empty<string>(),
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
        { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Region, Region: { } region } => MacLocal(request, region),
        { Platform: RecorderPlatform.MacOs, Target: RecorderTargetKind.Window, WindowRegion: { } window } => window,
        _ => null
    };

    private static RecorderRegion MacLocal(RecorderRequest request, RecorderRegion region)
    {
        foreach (var screen in request.Screens)
            if (screen.Index == request.ScreenIndex)
                return region with { X = region.X - screen.X, Y = region.Y - screen.Y };
        return region;
    }

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
        if (request.Target == RecorderTargetKind.AudioOnly) return BuildAudioOnly(request, outputPath);

        var a = new List<string> { "-hide_banner", "-y" };
        a.AddRange(CodecModel.DeviceArgs(request.VideoCodec));
        a.AddRange(Input(request));

        var audio = request.Audio is { InputCount: > 0 } plan ? plan : null;
        if (audio is not null) a.AddRange(audio.Inputs);

        var webcam = WebcamGraph(request with { Audio = audio });
        if (request.Webcam is { } cam)
            a.AddRange(new[] { "-f", "dshow", "-rtbufsize", WebcamBufferSize, "-i", "video=" + cam.Device });

        if (webcam is null && EncoderFilter(request) is { } filter) a.AddRange(new[] { "-vf", filter });

        var graphs = new[] { webcam, audio?.FilterComplex }.Where(g => !string.IsNullOrEmpty(g)).ToArray();
        if (graphs.Length > 0)
            a.AddRange(new[] { "-filter_complex", string.Join(";", graphs) });

        a.AddRange(new[] { "-c:v", request.VideoCodec });
        if (CodecModel.TakesPreset(request.VideoCodec)) a.AddRange(new[] { "-preset", request.Preset });
        a.AddRange(RateControlArgs(request));
        a.AddRange(StreamShapeArgs(request));
        a.AddRange(ColourArgs(request));
        a.AddRange(AudioOutputArgs(audio, webcam is null ? "0:v" : $"[{WebcamOutputLabel}]", request.AudioCodec));

        a.AddRange(LimitAndContainerArgs(request));

        if (request.FrameRateMode is RecorderFrameRateMode.Constant or RecorderFrameRateMode.Variable)
            a.AddRange(new[] { "-fps_mode", request.FrameRateMode == RecorderFrameRateMode.Variable ? "vfr" : "cfr" });

        a.Add(outputPath);
        a.AddRange(PreviewArgs(request));
        return a;
    }

    private static IReadOnlyList<string> BuildAudioOnly(RecorderRequest request, string outputPath)
    {
        var audio = request.Audio!;
        var a = new List<string> { "-hide_banner", "-y" };
        a.AddRange(audio.Inputs);
        if (!string.IsNullOrEmpty(audio.FilterComplex)) a.AddRange(new[] { "-filter_complex", audio.FilterComplex });
        a.AddRange(AudioOutputArgs(audio, null, request.AudioCodec));
        a.Add("-vn");
        a.AddRange(LimitAndContainerArgs(request));
        a.Add(outputPath);
        return a;
    }

    private static IReadOnlyList<string> LimitAndContainerArgs(RecorderRequest request)
    {
        var a = new List<string>();
        if (request.MaxDuration is { } limit)
            a.AddRange(new[] { "-t", limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) });

        if (request.MaxMegabytes is { } megabytes)
            a.AddRange(new[] { "-fs", LimitBytes(megabytes).ToString(CultureInfo.InvariantCulture) });

        if (request.Container is RecorderContainer.Mp4 or RecorderContainer.Mov)
            a.AddRange(new[] { "-movflags", "+faststart" });
        else if (request.Container == RecorderContainer.Mkv)
            a.AddRange(new[] { "-flush_packets", "1", "-cluster_time_limit", "500" });
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
    /// Bir parcanin istegi. Bolme olcutu artik parcanin kendi <c>-t</c>/<c>-fs</c> sinirina
    /// katiliyor: her parca (ilki dahil) kalan toplam sure/boyut ile bolme olcutunun kucugunu
    /// (<c>min</c>) kendi sinir olarak aliyor, boylece ffmpeg parcayi icerik zamaninda kendisi
    /// kapatiyor — dis yoklamanin ya da nazik kapanisin gecikmesi parca suresine hic girmiyor
    /// (eskiden <c>RecorderSession</c> parcayi disaridan yoklayip nazikce kapatiyordu; bu yol
    /// CI yukunde 2 sn'lik bolmede 4,4 sn'lik parca uretiyordu, bkz.
    /// <c>docs/olcumler/kayit-bolme-parca-siniri.md</c>). Duraklatip surdurmek yine yeni bir
    /// ffmpeg sureci aciyor ve her surec <c>-t</c>'yi sifirdan sayiyor.
    /// Kalan sure <c>-t</c>'nin yazilabildigi en kucuk adimdan (1 ms) kisaysa, ya da kalan
    /// boyut bayta yuvarlaninca sifirlaniyorsa <c>null</c> doner ve yeni parca acilmaz.
    /// </summary>
    public static RecorderRequest? ForSegment(RecorderRequest request, TimeSpan capturedBefore, double writtenMbBefore = 0)
    {
        ArgumentNullException.ThrowIfNull(request);

        TimeSpan? newMaxDuration = request.MaxDuration;
        if (request.MaxDuration is { } limit)
        {
            var remaining = limit - capturedBefore;
            if (remaining < TimeSpan.FromMilliseconds(1)) return null;
            newMaxDuration = request.Split?.Duration is { } every && every < remaining ? every : remaining;
        }
        else if (request.Split?.Duration is { } soleEvery)
        {
            newMaxDuration = soleEvery;
        }

        double? newMaxMegabytes = request.MaxMegabytes;
        if (request.MaxMegabytes is { } cap)
        {
            var remainingBytes = cap - writtenMbBefore;
            if (LimitBytes(remainingBytes) < 1) return null;
            newMaxMegabytes = request.Split?.Megabytes is { } megabytes && megabytes < remainingBytes ? megabytes : remainingBytes;
        }
        else if (request.Split?.Megabytes is { } soleMegabytes)
        {
            newMaxMegabytes = soleMegabytes;
        }

        if (Equals(newMaxDuration, request.MaxDuration) && Equals(newMaxMegabytes, request.MaxMegabytes) && request.Split is null)
            return request;

        return request with { MaxDuration = newMaxDuration, MaxMegabytes = newMaxMegabytes, Split = null };
    }

    /// <summary>
    /// Kalan surenin "kayit bitti" sayildigi alt sinir. Ilerleme blogunun son <c>out_time</c>'i
    /// <c>-t</c>'nin birkac ms altinda kalabiliyor; bu fark yeni bir parca acmamali.
    /// </summary>
    public static readonly TimeSpan TailTolerance = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Kendiliginden biten bir parcanin ardindan yeni parca acilir mi. Yalniz parca bolme
    /// olcutuyle sinirlanmissa (bolme siniri kalan toplamdan kucukse) acilir; toplam sure ya da
    /// boyut sinirina gelen parca kaydi bitirir. Kalan sure <see cref="TailTolerance"/>'tan
    /// kisaysa yine biter: olcum gecikmesinden kalan birkac ms'lik kuyruk parca sayilmaz.
    /// </summary>
    public static bool NaturalExitContinues(RecorderRequest request, TimeSpan capturedAtStart, double writtenMbAtStart, TimeSpan capturedAtExit)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Split is not { IsSet: true } split) return false;
        if (request.MaxDuration is { } total && total - capturedAtExit < TailTolerance) return false;

        var byDuration = split.Duration is { } every && (request.MaxDuration is not { } limit || every < limit - capturedAtStart);
        var bySize = split.Megabytes is { } megabytes && (request.MaxMegabytes is not { } cap || megabytes < cap - writtenMbAtStart);
        return byDuration || bySize;
    }

    public static long LimitBytes(double megabytes) => Megabayt.Tavan(megabytes);

    /// <summary>
    /// Kayit surerken alinan tek karelik ekran goruntusunun argumanlari. Ayni yakalama
    /// girdisinden okur: canli ekranin bir dosyasi yok, dosyadan kare kesen bir yol burada
    /// kullanilamaz. Kodlama kolu hic kurulmaz: bir kare, bir resim. Windows'ta her zaman
    /// <c>gdigrab</c>: tek karede hiz sorun degil ve kayit suren cikisa ikinci bir Desktop
    /// Duplication acilmiyor.
    /// </summary>
    public static IReadOnlyList<string> BuildSnapshot(RecorderRequest request, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target == RecorderTargetKind.AudioOnly)
            throw new InvalidOperationException("Audio-only recording has no picture to snapshot.");
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("Image path is required.", nameof(imagePath));

        var probe = request with
        {
            Capture = RecorderCapture.Gdigrab,
            Container = RecorderContainer.Mp4,
            Audio = null,
            Webcam = null,
            MaxDuration = null,
            Split = null
        };
        var errors = Validate(probe, "snapshot.mp4");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var a = new List<string> { "-hide_banner", "-nostdin", "-y" };
        a.AddRange(Input(probe));
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
        var profile = HdrCapture(request) ? HdrProfile(request, HdrPixelFormat(request)!) : request.Profile;
        if (!string.IsNullOrWhiteSpace(profile)) a.AddRange(new[] { "-profile:v", profile! });
        if (!string.IsNullOrWhiteSpace(request.Tune)) a.AddRange(new[] { "-tune", request.Tune! });
        return a;
    }

    private static IReadOnlyList<string> ColourArgs(RecorderRequest request)
    {
        if (HdrCapture(request))
            return new[]
            {
                "-pix_fmt", PixelFormatArgument(request.VideoCodec, HdrPixelFormat(request)!),
                "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc", "-color_range", "tv"
            }.Concat(HdrStaticArgs(request)).ToArray();

        var a = new List<string>();
        var format = OutputPixelFormat(request);
        if (CodecModel.TakesPixelFormatFlag(request.VideoCodec)) a.AddRange(new[] { "-pix_fmt", format });
        if (string.IsNullOrWhiteSpace(request.Profile) && CodecModel.OutputProfile(request.VideoCodec, format) is { } profile)
            a.AddRange(new[] { "-profile:v", profile });
        if (SdrColour(request))
        {
            a.AddRange(new[]
            {
                "-color_primaries", "bt709", "-color_trc", "bt709",
                "-colorspace", SdrColorSpace(request), "-color_range", SdrColorRange(request)
            });
            return a;
        }

        if (!string.IsNullOrWhiteSpace(request.ColorSpace)) a.AddRange(new[] { "-colorspace", request.ColorSpace! });
        if (!string.IsNullOrWhiteSpace(request.ColorRange)) a.AddRange(new[] { "-color_range", request.ColorRange! });
        return a;
    }

    private static readonly DdaDisplayInfo Bt2020D65 = new(0.708, 0.292, 0.170, 0.797, 0.131, 0.046, 0.3127, 0.3290, 0, 0, 0);

    /// <summary>
    /// HDR10 kaydin statik verisi hedef ekranin DXGI bilgisinden: <c>master-display</c> ekranin
    /// renk koordinatlari ve en dusuk/en yuksek parlakligi, <c>MaxCLL</c> en yuksek parlaklik,
    /// <c>MaxFALL</c> tam kare parlakligi. Koordinatlar bos ya da aralik disiysa BT.2020 D65
    /// yazilir. HDR10 kayit degilse ya da ekran en yuksek parlakligi bildirmiyorsa <c>null</c>:
    /// oynatici o zaman tepe parlakligi tahmin eder.
    /// </summary>
    public static HdrStaticMetadata? HdrStatic(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HdrCapture(request) || DdagrabTarget(request) is not { } target || target.Output.Display is not { } d) return null;
        if (!double.IsFinite(d.MaxNits) || d.MaxNits < 1) return null;

        double[] xy = { d.GreenX, d.GreenY, d.BlueX, d.BlueY, d.RedX, d.RedY, d.WhiteX, d.WhiteY };
        if (xy.Any(v => !double.IsFinite(v) || v <= 0 || v >= 1))
            xy = new[] { Bt2020D65.GreenX, Bt2020D65.GreenY, Bt2020D65.BlueX, Bt2020D65.BlueY, Bt2020D65.RedX, Bt2020D65.RedY, Bt2020D65.WhiteX, Bt2020D65.WhiteY };
        string C(int i) => Math.Round(xy[i] * 50000).ToString(CultureInfo.InvariantCulture);
        var min = double.IsFinite(d.MinNits) ? Math.Clamp(d.MinNits, 0, d.MaxNits) : 0;
        var md = $"G({C(0)},{C(1)})B({C(2)},{C(3)})R({C(4)},{C(5)})WP({C(6)},{C(7)})"
                 + $"L({Math.Round(d.MaxNits * 10000).ToString(CultureInfo.InvariantCulture)},{Math.Round(min * 10000).ToString(CultureInfo.InvariantCulture)})";
        var cll = (int)Math.Round(d.MaxNits);
        var fall = double.IsFinite(d.MaxFullFrameNits) && d.MaxFullFrameNits >= 1 ? Math.Min(cll, (int)Math.Round(d.MaxFullFrameNits)) : cll;
        return new HdrStaticMetadata(md, Number(cll) + "," + Number(fall));
    }

    /// <summary>
    /// <see cref="HdrStatic"/>'in kodlayici parametresi: libx265 <c>master-display</c>/<c>max-cll</c>,
    /// libx264 <c>mastering-display</c>/<c>cll</c>, libsvtav1 kesirli <c>mastering-display</c> ve
    /// <c>content-light</c>. Donanim kodlayicilari ffmpeg'de bu veriyi parametreyle almiyor; bos doner.
    /// </summary>
    internal static IReadOnlyList<string> HdrStaticArgs(RecorderRequest request)
    {
        if (HdrStatic(request) is not { } m) return Array.Empty<string>();
        return request.VideoCodec.ToLowerInvariant() switch
        {
            "libx265" => new[] { "-x265-params", $"hdr10=1:master-display={m.MasterDisplay}:max-cll={m.ContentLight}" },
            "libx264" => new[] { "-x264-params", $"mastering-display={m.MasterDisplay}:cll={m.ContentLight}" },
            "libsvtav1" when HdrResolver.SvtAv1MasteringDisplay(m.MasterDisplay) is { } svt
                => new[] { "-svtav1-params", $"mastering-display={svt}:content-light={m.ContentLight}" },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Ses cikisinin eslemeleri ve kodlayicilari. Tek iz tek <c>-c:a</c> aliyor; birden
    /// fazla iz (ayri mikrofon ve sistem sesi) her biri kendi <c>-c:a:N</c>'ini aliyor,
    /// cunku izler ayri kaldiginda hangisinin hangi kodlayiciya gittigi argumandan
    /// okunabilmeli.
    /// </summary>
    private static IReadOnlyList<string> AudioOutputArgs(AudioCapturePlan? audio, string? videoMap, string codec)
    {
        if (audio is null)
            return videoMap is null or "0:v" ? new[] { "-an" } : new[] { "-map", videoMap, "-an" };

        var a = new List<string>();
        if (videoMap is not null) a.AddRange(new[] { "-map", videoMap });
        foreach (var map in audio.Maps) a.AddRange(new[] { "-map", map });

        if (audio.Maps.Count <= 1)
        {
            a.AddRange(new[] { "-c:a", codec, "-b:a", AudioBitrate });
            return a;
        }

        for (var track = 0; track < audio.Maps.Count; track++)
        {
            var suffix = Number(track);
            a.AddRange(new[] { $"-c:a:{suffix}", codec, $"-b:a:{suffix}", AudioBitrate });
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
        if (request.Capture == RecorderCapture.Ddagrab) return DdagrabInput(request);

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

    private static IReadOnlyList<string> MacInput(RecorderRequest request)
    {
        var a = new List<string> { "-f", "avfoundation", "-framerate", Number(request.Fps), "-capture_cursor", Flag(request.ShowCursor) };
        if (request.ShowClicks) a.AddRange(new[] { "-capture_mouse_clicks", "1" });
        a.AddRange(new[] { "-i", $"{MacScreenName(request.ScreenIndex)}:none" });
        return a;
    }

    /// <summary>
    /// avfoundation ekrani adla secilir: sira numarasinda ekranlar kameralardan sonra geliyor
    /// (<c>[kamera sayisi + i] Capture screen i</c>), kameralı Mac'te <c>0</c> kamerayi acar. Ad
    /// <c>Capture screen N</c> kamera sayisindan bagimsiz; ffmpeg <c>avfoundation.m</c> bunu
    /// <c>sscanf(..., "Capture screen %d", ...)</c> ile okuyor (n4.0'dan beri).
    /// </summary>
    private static string MacScreenName(int screenIndex) => "Capture screen " + Number(screenIndex);

    private static IReadOnlyList<string> LinuxInput(RecorderRequest request)
    {
        var a = new List<string> { "-f", "x11grab", "-framerate", Number(request.Fps), "-draw_mouse", Flag(request.ShowCursor) };
        var display = string.IsNullOrWhiteSpace(request.Display) ? DefaultDisplay : request.Display!;

        // Ekran secimi gdigrab'daki gibi o monitorun dikdortgenine iner: x11grab butun X
        // ekranini (tum monitorleri) tek kare olarak veriyor, secilen monitor yok sayiliyordu.
        // Tek monitorde ya da monitor listesi yokken ScreenCapture null doner, ekranin tamami kalir.
        var area = request.Target == RecorderTargetKind.Region
            ? request.Region
            : request.Target == RecorderTargetKind.Screen
                ? ScreenCapture(request.Screens, request.ScreenIndex)
                : null;

        if (area is { } region)
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
    private static string OutputPixelFormat(RecorderRequest request)
        => CodecModel.OutputPixelFormat(request.VideoCodec, PixelFormatArgument(request.VideoCodec, request.PixelFormat));

    /// <summary>
    /// Kodlayiciya giden <c>-vf</c>: yakalama suzgeclerinin ardina kodlayicinin yukleme
    /// adimlari (VAAPI'de <c>format=nv12,hwupload</c>) eklenir.
    /// </summary>
    private static string? EncoderFilter(RecorderRequest request)
    {
        var upload = CodecModel.UploadFilters(request.VideoCodec, OutputPixelFormat(request));
        var filter = ColouredFilter(request);
        if (upload.Count == 0) return filter;
        var tail = string.Join(",", upload);
        return filter is null ? tail : filter + "," + tail;
    }

    private static string? ColouredFilter(RecorderRequest request)
    {
        var filter = VideoFilter(request);
        if (!SdrColour(request)) return filter;
        var link = SdrColourLink(request);
        return filter is null ? link : filter + "," + link;
    }

    private static string? VideoFilter(RecorderRequest request)
    {
        var links = new List<string>();

        if (MacCrop(request) is { } region)
            links.Add($"crop={Number(region.Width)}:{Number(region.Height)}:{Number(region.X)}:{Number(region.Y)}");

        var boxes = MaskBoxes(request);
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var radius = Math.Clamp(Math.Min(box.Width, box.Height) / 4 - 1, 1, MaskBlurRadius);
            links.Add($"split[pm{i}][pb{i}];[pb{i}]crop={Number(box.Width)}:{Number(box.Height)}:{Number(box.X)}:{Number(box.Y)},"
                + $"boxblur=luma_radius={Number(radius)}:luma_power={Number(MaskBlurPower)}[pk{i}];"
                + $"[pm{i}][pk{i}]overlay={Number(box.X)}:{Number(box.Y)}:format=auto");
        }

        if (request.Scale is { } scale)
            links.Add($"scale={Number(scale.Width)}:{Number(scale.Height)}");

        return links.Count == 0 ? null : string.Join(',', links);
    }

    /// <summary>Maskenin en buyuk bulanma yaricapi. Kucuk kutuda kisa kenarin dortte birinin bir eksigi: 4:2:0 renk duzleminde <c>boxblur</c> yaricapi kenarin yarisindan kesin kucuk ister.</summary>
    public const int MaskBlurRadius = 20;

    /// <summary>Bulanmanin kac kez uygulandigi.</summary>
    public const int MaskBlurPower = 3;

    /// <summary>Maskenin en kucuk kenari; daha kucugu bu boya buyutulur.</summary>
    public const int MinMaskSize = 8;

    /// <summary>
    /// Maskelerin kare koordinati: yakalama alanina kirpilir, disa dogru ciftlenir, kenari
    /// <see cref="MinMaskSize"/>'dan kucukse kare icinde kalacak bicimde buyutulur. Alanin
    /// tamamen disindaki maske duser; alan bilinmiyorsa liste bostur.
    /// </summary>
    public static IReadOnlyList<RecorderRegion> MaskBoxes(RecorderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PrivacyMasks.Count == 0 || request.Target == RecorderTargetKind.Window
            || CaptureArea(request) is not { } area)
            return Array.Empty<RecorderRegion>();

        var frameWidth = area.Width - area.Width % 2;
        var frameHeight = area.Height - area.Height % 2;
        var boxes = new List<RecorderRegion>();
        foreach (var mask in request.PrivacyMasks)
        {
            if (mask.Width <= 0 || mask.Height <= 0) continue;
            if (MaskSpan(mask.X - area.X, mask.Width, frameWidth) is not { } x) continue;
            if (MaskSpan(mask.Y - area.Y, mask.Height, frameHeight) is not { } y) continue;
            boxes.Add(new RecorderRegion(x.Start, y.Start, x.Length, y.Length));
        }

        return boxes;
    }

    private static (int Start, int Length)? MaskSpan(int start, int length, int frame)
    {
        if (frame < 2) return null;
        var from = Math.Max(0, start);
        var to = Math.Min(frame, start + length);
        if (to <= from) return null;

        from -= from % 2;
        to = Math.Min(frame, to + to % 2);
        var size = Math.Min(frame, MinMaskSize - MinMaskSize % 2);
        if (to - from < size)
        {
            to = Math.Min(frame, from + size);
            from = to - size;
        }

        return (from, to - from);
    }

    private static IEnumerable<string> MaskErrors(RecorderRequest request)
    {
        if (request.PrivacyMasks.Count == 0) yield break;
        if (request.PrivacyMasks.Any(m => m.Width <= 0 || m.Height <= 0))
            yield return "Every privacy mask must have a positive width and height.";
        if (request.Target == RecorderTargetKind.Window)
            yield return "A privacy mask needs a screen or region target; the window frame does not follow desktop pixels.";
        else if (CaptureArea(request) is null)
            yield return "The privacy mask cannot be placed because the capture area is unknown.";
    }

    private static string Size(RecorderRegion region) => $"{Number(region.Width)}x{Number(region.Height)}";

    private static string Rate(int kbps) => Number(kbps) + "k";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "1" : "0";
}
