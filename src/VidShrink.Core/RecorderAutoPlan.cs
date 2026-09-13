namespace VidShrink.Core;

/// <summary>
/// Otomatik kipin bir adayi neden sectigi. Sayi tasimaz: esikler bu dosyada
/// uydurulmuyor, kazanan <c>RecorderAutoProbe</c>'un olctugu dusen kareden cikiyor.
/// </summary>
public enum RecorderAutoNote
{
    /// <summary>Yoklamanin calistigini gordugu bir donanim kodlayicisi secildi.</summary>
    HardwareEncoderChosen,

    /// <summary>Calisan donanim kodlayicisi yok; yazilim koluna dusuldu.</summary>
    SoftwareEncoderFallback,

    /// <summary>Kare hizi ekranin yenileme hizindan asagi yuvarlandi.</summary>
    FpsFollowsRefreshRate,

    /// <summary>Kare hizi merdivende bir basamak asagi alindi.</summary>
    FpsSteppedDown,

    /// <summary>Yakalama boyutu korundu.</summary>
    ResolutionKept,

    /// <summary>Yakalama boyutu yariya indirildi.</summary>
    ResolutionHalved,

    /// <summary>Kap Matroska secildi: oldurulen kayit oynatilabilir kaliyor.</summary>
    ContainerSurvivesKill,

    /// <summary>On ayar kodlayicinin kendi sozlugunden secildi.</summary>
    PresetFollowsEncoder
}

/// <summary>
/// Otomatik kipin girdisi: kosan makinenin olculebilen yani. <paramref name="WorkingEncoders"/>
/// <b>yoklanmis</b> adlarin listesi — <c>EncoderCapabilities.WorksAsEncoder</c> gercekten bir
/// ffmpeg kosumu yapiyor, burada varsayim yok. Bos liste "hicbiri olculmedi" demektir ve
/// yazilim koluna dusulur.
/// </summary>
public readonly record struct RecorderMachine(
    int CaptureWidth,
    int CaptureHeight,
    double RefreshHz,
    int CpuCores,
    IReadOnlyList<string> WorkingEncoders);

/// <summary>
/// Otomatik kipin urettigi tek aday. <see cref="RecorderAutoPlan.Apply"/> bunu bir
/// <see cref="RecorderRequest"/>'e yaziyor; alanlarin hicbiri arayuzden okunmuyor.
/// </summary>
public sealed record RecorderAutoChoice(
    RecorderContainer Container,
    string VideoCodec,
    string Preset,
    int Fps,
    RecorderScale? Scale,
    double Quality,
    int KeyframeSeconds,
    string PixelFormat,
    IReadOnlyList<RecorderAutoNote> Notes);

/// <summary>
/// Kaydin otomatik kipi: makineden bir <b>aday merdiveni</b> uretir, kazanani secmez.
/// Secim olcen tarafta (<c>VidShrink.Ffmpeg.RecorderAutoProbe</c>) yapiliyor, cunku hangi
/// adayin kare dusurdugu tabloyla degil kisa gercek bir kayitla anlasiliyor.
/// <para>
/// Desen <see cref="CompressionStrategy"/> ile ayni: saf, statik, ffmpeg'e bagli degil,
/// kapali kumelerden kuruluyor.
/// </para>
/// </summary>
public static class RecorderAutoPlan
{
    /// <summary>
    /// Kabul edilen kare hizlari. Kapali kume: ekranin yenileme hizi bu merdivene asagi
    /// yuvarlanir, aradaki bir sayi (ornegin 75) uretilmez.
    /// </summary>
    private static readonly int[] FpsLadder = { 24, 30, 60, 120 };

    /// <summary>
    /// Donanim kodlayicilarinin yeglenme sirasi. Yalniz h264 kollari: kayit dosyasinin her
    /// yerde acilmasi kaydin kendisi kadar onemli, hevc/av1 secimi kullanicinin.
    /// </summary>
    private static readonly string[] HardwarePreference =
    {
        "h264_nvenc", "h264_qsv", "h264_amf"
    };

    /// <summary>Donanim yokken dusulen kol.</summary>
    public const string SoftwareCodec = RecorderArguments.DefaultVideoCodec;

    /// <summary>Uretilen en fazla aday sayisi; deneme kaydi aday basina bir kosum.</summary>
    public const int MaxCandidates = 4;

    /// <summary>Yenileme hizi okunamadiginda kullanilan kare hizi.</summary>
    public const int FallbackFps = RecorderArguments.DefaultFps;

    /// <summary>Kabul edilen kare hizlari.</summary>
    public static IReadOnlyList<int> FrameRates => FpsLadder;

    /// <summary>Yeglenen donanim kodlayicilari, sirasiyla.</summary>
    public static IReadOnlyList<string> HardwareCodecs => HardwarePreference;

    /// <summary>
    /// Kodlayicinin kendi on ayar sozlugunden secilen deger. Liste degil tek deger
    /// donuyor: otomatik kip kullaniciya on ayar sormuyor. nvenc'in <c>p1..p7</c> olcegi,
    /// AMF'nin <c>speed/balanced/quality</c> olcegi ve x264 ailesinin adlari ayri
    /// sozlukler; tek bir ad hepsine yazilamaz.
    /// </summary>
    public static string PresetFor(string codec)
    {
        var c = (codec ?? string.Empty).ToLowerInvariant();
        if (c.Contains("nvenc", StringComparison.Ordinal)) return "p4";
        if (c.Contains("amf", StringComparison.Ordinal)) return "speed";
        if (c.Contains("qsv", StringComparison.Ordinal)) return "veryfast";
        return RecorderArguments.DefaultPreset;
    }

    /// <summary>
    /// Yenileme hizinin merdivendeki karsiligi: hizi <b>gecmeyen</b> en buyuk basamak.
    /// Okunamayan hizda <see cref="FallbackFps"/>.
    /// </summary>
    public static int FpsFor(double refreshHz)
    {
        if (double.IsNaN(refreshHz) || refreshHz <= 0) return FallbackFps;

        var chosen = FpsLadder[0];
        foreach (var step in FpsLadder)
            if (step <= refreshHz + 0.5) chosen = step;

        return chosen;
    }

    /// <summary>Merdivende bir asagi basamak; en alttakinin altinda basamak yok.</summary>
    public static int? StepDown(int fps)
    {
        int? previous = null;
        foreach (var step in FpsLadder)
        {
            if (step >= fps) break;
            previous = step;
        }

        return previous;
    }

    /// <summary>
    /// Yakalama boyutunun yarisi, <c>yuv420p</c> icin cifte yuvarlanmis. Yarisi iki
    /// pikselin altina dusuyorsa <c>null</c> doner: olceklenemeyecek kadar kucuk bir
    /// yakalamaya ikinci aday uretilmiyor.
    /// </summary>
    public static RecorderScale? Halved(int width, int height)
    {
        var w = width / 2 / 2 * 2;
        var h = height / 2 / 2 * 2;
        return w >= 2 && h >= 2 ? new RecorderScale(w, h) : null;
    }

    /// <summary>
    /// Makinenin aday merdiveni, en iyisi basta. Sira <b>tahmin</b>: hangisinin kare
    /// dusurdugu olculmeden bilinmiyor, o yuzden liste bir sonuc degil bir deneme sirasi.
    /// Ilk aday donanim varsa donanimi, yoksa x264'u; sonraki adaylar once kare hizini,
    /// sonra cozunurlugu indiriyor.
    /// </summary>
    public static IReadOnlyList<RecorderAutoChoice> Candidates(RecorderMachine machine)
    {
        var codec = CodecFor(machine.WorkingEncoders);
        var hardware = !string.Equals(codec, SoftwareCodec, StringComparison.Ordinal);
        var fps = FpsFor(machine.RefreshHz);
        var slower = StepDown(fps);
        var half = Halved(machine.CaptureWidth, machine.CaptureHeight);

        var ladder = new List<(int Fps, RecorderScale? Scale)> { (fps, null) };
        if (slower is { } second) ladder.Add((second, null));
        if (half is not null) ladder.Add((fps, half));
        if (slower is { } third && half is not null) ladder.Add((third, half));

        var candidates = new List<RecorderAutoChoice>(MaxCandidates);
        foreach (var (rate, scale) in ladder)
        {
            if (candidates.Count == MaxCandidates) break;

            var notes = new List<RecorderAutoNote>
            {
                hardware ? RecorderAutoNote.HardwareEncoderChosen : RecorderAutoNote.SoftwareEncoderFallback,
                RecorderAutoNote.PresetFollowsEncoder,
                RecorderAutoNote.ContainerSurvivesKill,
                rate == fps ? RecorderAutoNote.FpsFollowsRefreshRate : RecorderAutoNote.FpsSteppedDown,
                scale is null ? RecorderAutoNote.ResolutionKept : RecorderAutoNote.ResolutionHalved
            };

            candidates.Add(new RecorderAutoChoice(
                RecorderContainer.Mkv,
                codec,
                PresetFor(codec),
                rate,
                scale,
                RecorderArguments.DefaultQuality,
                RecorderArguments.DefaultKeyframeSeconds,
                RecorderArguments.DefaultPixelFormat,
                notes));
        }

        return candidates;
    }

    /// <summary>
    /// Yoklamanin calistigini gordugu ilk yeglenen donanim kolu; yoksa
    /// <see cref="SoftwareCodec"/>. Listede olan ama yeglenenler arasinda bulunmayan bir ad
    /// secilmiyor: aday kumesi kapali.
    /// </summary>
    public static string CodecFor(IReadOnlyList<string>? workingEncoders)
    {
        if (workingEncoders is null || workingEncoders.Count == 0) return SoftwareCodec;

        foreach (var preferred in HardwarePreference)
            if (workingEncoders.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                return preferred;

        return SoftwareCodec;
    }

    /// <summary>
    /// Adayi istege yazar. Kullanicinin hedefi, bolgesi ve ses kolu korunuyor; otomatik kip
    /// yalniz kodlama kolunu yaziyor. Hiz kontrolu kalite koluna aliniyor, cunku bu depoda
    /// kayit icin olculmus bir hedef bit hizi yok.
    /// </summary>
    public static RecorderRequest Apply(RecorderRequest request, RecorderAutoChoice choice)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(choice);

        return request with
        {
            Container = choice.Container,
            VideoCodec = choice.VideoCodec,
            Preset = choice.Preset,
            Fps = choice.Fps,
            Scale = choice.Scale,
            RateControl = RecorderRateControl.Quality,
            Quality = choice.Quality,
            BitrateKbps = null,
            MaxBitrateKbps = null,
            BufferKbits = null,
            KeyframeSeconds = choice.KeyframeSeconds,
            PixelFormat = choice.PixelFormat,
            Profile = null,
            Tune = null
        };
    }

    /// <summary>
    /// Kullanicinin verdigi hedef boyutu isteğe yazar. Kalite kolu bit hizi koluna
    /// cevrilir ve tavan bit hizina esitlenir; tampon iki kati alinir. Butce
    /// kullanilabilir degilse istek degismeden doner.
    /// </summary>
    public static RecorderRequest ApplyBudget(RecorderRequest request, RecorderBudget budget)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (budget.Verdict != RecorderBudgetVerdict.Usable) return request;

        return request with
        {
            RateControl = RecorderRateControl.Bitrate,
            BitrateKbps = budget.VideoKbps,
            MaxBitrateKbps = budget.VideoKbps,
            BufferKbits = budget.VideoKbps * 2
        };
    }
}
