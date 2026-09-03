using System.Globalization;

namespace VidShrink.Core;

public enum EncoderVendor { Software, Nvenc, Qsv, Amf, VideoToolbox }

public static class CodecModel
{
    public const double PriorQualityAtReference = 93.0;
    public const double PriorQualityPerHalving = 6.0;
    public const double DetailConcentrationExponent = 0.25;
    public const double ScalePenaltyScale = 10.0;
    public const double ScalePenaltyExponent = 1.1;
    public const double FpsPenaltyPerHalving = 5.0;
    public const double SoftwareQualityCeiling = 99.0;
    public const double HardwareQualityCeiling = 96.0;
    public const double HardwareAv1QualityCeiling = 98.0;
    public const double HardwareBitrateYield = 0.877;
    public const double HardwareFloorFactor = 1.52;

    // A hardware encoder refuses to go below a floor of its own and ignores -b:v and -maxrate
    // under it. Measured on av1_nvenc (-rc vbr -multipass fullres) with -b:v 32k -maxrate 33k,
    // 20 s of a 1080p60 screen recording, nine layouts:
    //   1920x1080@60 690,9k  1920x1080@30 423,9k  1280x720@60 341,3k  1280x720@30 211,3k
    //    854x480@60  145,0k   854x480@30   95,3k   640x360@60  71,8k   640x360@15  37,5k
    //    426x240@30   38,9k
    // Per megapixel the floor is a straight line in fps: kbit/s / Mpx = 4,29 * fps + 75,6.
    // That is 4,29e-3 bits per pixel per frame plus 0,0756 bits per pixel per second of
    // stream overhead that does not shrink with the frame rate.
    public const double HardwareMinBitratePerPixelFrame = 4.29e-3;
    public const double HardwareMinBitratePerPixelSecond = 0.0756;

    // The worst residual of that fit is 1280x720@60, where the encoder delivered 11% above the
    // line. The floor has to be an upper bound - a layout the encoder cannot actually reach
    // overshoots the target - so the fit is carried 15% high.
    public const double HardwareMinBitrateMargin = 1.15;

    // The line runs out at the small end: at 640x360@15 and 426x240@30 the encoder delivered
    // 37,5k and 38,9k where the line predicts 32k and 21k. Below roughly a tenth of a megapixel
    // the stream carries a cost that no longer shrinks with the picture, so the floor stops
    // falling at the lowest rate that was ever measured.
    public const int HardwareMinBitrateFloorK = 39;

    // At the floor itself the encoder does not merely refuse to go lower, it stops following the
    // request at all. av1_nvenc on a 400 s 1080p60 clip, delivered / requested against the
    // headroom (request divided by the floor above):
    //   0,38x -> 5,7x   1,01x -> 1,091   1,04x -> 1,171   1,30x -> 1,171   1,45x -> 1,091
    //   1,95x -> 0,994  2,17x -> 1,071   2,61x -> 0,995   2,85x -> 1,050   6,52x -> 1,013
    // Under 1,5x the floor the encoder overspends by 9% to 17%, and at 0,38x by a factor of five.
    // From there up the request is followed within a few per cent, but not cleanly: 1,95x and
    // 2,61x land on it (0,994 and 0,995) while 2,17x and 2,85x still spend 5% to 7% past it. The
    // scatter above the line is small enough for the retry to absorb, the overspend below it is
    // not, so the usable line is drawn at twice the floor - past the last point that overspends
    // systematically, and conservative rather than exact.
    public const double HardwareMinBitrateHeadroom = 2.0;

    public static double FloorBppf(string codec)
    {
        var baseFloor = Family(codec) switch
        {
            "av1" => 0.0095,
            "hevc" => 0.025,
            _ => 0.035
        };
        return IsHardware(codec) ? baseFloor * HardwareFloorFactor : baseFloor;
    }

    /// <summary>
    /// The lowest video bitrate the encoder will actually deliver at this layout, in kbit/s.
    /// Zero for every encoder off the hardware path: libx264 and its siblings follow -b:v all the
    /// way down, and VideoToolbox reads zero too because <see cref="IsHardware"/> keeps it out,
    /// not because its floor was measured.
    /// Only av1_nvenc was measured; QSV and AMF carry the same line, which is not measured.
    /// </summary>
    public static int MinBitrateK(string codec, int width, int height, double fps)
    {
        if (!IsHardware(codec)) return 0;
        var pixels = (double)Math.Max(width, 2) * Math.Max(height, 2);
        var bps = pixels * (HardwareMinBitratePerPixelFrame * Math.Max(fps, 1.0) + HardwareMinBitratePerPixelSecond);
        return Math.Max(HardwareMinBitrateFloorK, (int)Math.Ceiling(bps * HardwareMinBitrateMargin / 1000.0));
    }

    /// <summary>
    /// The lowest video bitrate at which the encoder still follows the request at this layout.
    /// Above it the delivered bitrate tracks what was asked for; below it the encoder overspends.
    /// </summary>
    public static int UsableBitrateK(string codec, int width, int height, double fps)
        => (int)Math.Ceiling(MinBitrateK(codec, width, height, fps) * HardwareMinBitrateHeadroom);

    public static double ReferenceCrf(string codec) => Family(codec) switch
    {
        "hevc" => 28,
        "av1" => 35,
        _ => 23
    };

    public static double RelativeBitrateNeed(string codec) => codec.ToLowerInvariant() switch
    {
        "libx265" => 0.68,
        "libsvtav1" => 0.55,
        "h264_nvenc" => 1.28,
        "hevc_nvenc" => 0.88,
        "h264_qsv" => 1.25,
        "hevc_qsv" => 0.90,
        "av1_nvenc" => 0.60,
        "av1_qsv" => 0.62,
        "av1_amf" => 0.66,
        "hevc_amf" => 0.95,
        "h264_amf" => 1.30,
        _ => 1.0
    };

    public static double CrfHalvingStep(string codec) => Family(codec) switch
    {
        "av1" => 7.0,
        _ => 6.0
    };

    public static double QualityLimit(string codec)
    {
        if (!IsHardware(codec)) return SoftwareQualityCeiling;
        return codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase)
            ? HardwareAv1QualityCeiling
            : HardwareQualityCeiling;
    }

    public static EncoderVendor Vendor(string codec)
    {
        var c = codec.ToLowerInvariant();
        if (c.Contains("nvenc")) return EncoderVendor.Nvenc;
        if (c.Contains("qsv")) return EncoderVendor.Qsv;
        if (c.Contains("amf")) return EncoderVendor.Amf;
        if (c.Contains("videotoolbox")) return EncoderVendor.VideoToolbox;
        return EncoderVendor.Software;
    }

    /// <summary>
    /// Whether the encoder is sent down the hardware path. Everything behind this gate - the
    /// floor factor, the delivered-bitrate yield, the delivery reserve, the peak ceiling and the
    /// lower quality ceiling - was measured on NVENC, so the gate names the vendors those numbers
    /// are carried to instead of asking whether the vendor is a chip.
    /// VideoToolbox is a chip and is still false here: nothing behind this gate has been measured
    /// on it. docs/olcumler/videotoolbox.md gives one bitrate per arm on one Apple M1, which is not
    /// enough for any of them. Opening this gate for VideoToolbox is a measurement, not an edit.
    /// </summary>
    public static bool IsHardware(string codec) => Vendor(codec) switch
    {
        EncoderVendor.Nvenc or EncoderVendor.Qsv or EncoderVendor.Amf => true,
        _ => false
    };

    private readonly record struct TurboFirstPassEntry(string Ceiling, bool Safe);

    /// <summary>
    /// Ilk gecisin inebilecegi en hizli on ayar, kodek basina. Tavanin varligi turbonun
    /// acilabilecegi anlamina gelmez; onu <see cref="TurboFirstPassIsSafe"/> soyler.
    /// <para>
    /// <c>libx264</c> tavani <c>veryfast</c>, guvenli degil: <c>veryfast</c> ilk gecis
    /// <c>weightp=1</c>, <c>slow</c> ikinci gecis <c>weightp=2</c> kosar ve x264 ikinci gecisi
    /// <c>different weightp setting than first pass (2 vs 1)</c> diyerek hic acmaz — kullanici
    /// sifir bayt cikti alir. Iki kaynak parcasinda olculdu, ikisinde de ikinci gecis
    /// <c>exit=127</c> ve cikti 0 bayt; ayni girdide <c>libx265</c> turbo 745,2K ve 677,3K
    /// uretti. Olcum: <c>docs/olcumler/turbo-x264-mayini.md</c>.
    /// </para>
    /// <para>
    /// Satir tablodan cikarilmiyor cunku olculen sey tavanin yoklugu degil, tavanin
    /// <b>kullanilamazligi</b>: iki gecise ayni <c>weightp</c> verilirse x264 turbosu calisiyor.
    /// O engel olculdu ve asilabilir oldugu goruldu; <c>Safe</c> yine de <c>false</c>, artik
    /// baska bir sebeple: esitleme yapildiginda bile turbo uretim borusunda toplam sureyi
    /// yalniz %0,58 - %4,44 kisaltiyor ve VMAF'tan 0,35 - 0,83 puan goturuyor; ayni olcumde
    /// <c>libx265</c> turbosu %29,6 - %33,5 kazandirip VMAF'i dusurmuyor. x264'te ikinci
    /// gecis toplamin buyuk yarisidir (2,38 - 2,80 sn); klip 35'te ilk gecisin suresini
    /// agirlikli olarak cozme ve olcekleme belirliyor, on ayar farki yalniz 39,7 ms / toplamin
    /// %1,0'i (<c>docs/olcumler/x264-turbo-acilis.md:127-131</c>). Bu oran yalniz olculen
    /// parca icin gecerli; kazanc parca basina %0,58 - %4,44 arasinda degisiyor, tek yonlu
    /// bir genelleme kurulamaz. Olcum: <c>docs/olcumler/x264-turbo-acilis.md</c>.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, TurboFirstPassEntry> TurboFirstPassCeilings =
        new Dictionary<string, TurboFirstPassEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["libx264"] = new("veryfast", Safe: false),
            ["libx265"] = new("veryfast", Safe: true)
        };

    /// <summary>
    /// Tabloda tavani olan kodekler. Hepsi turboya acilabilir demek <b>degildir</b> —
    /// acilabilenler icin <see cref="TurboFirstPassIsSafe"/>.
    /// </summary>
    public static IReadOnlyCollection<string> TurboFirstPassCodecs
        => (IReadOnlyCollection<string>)TurboFirstPassCeilings.Keys;

    /// <summary>
    /// Kodegin tabloda olculmus bir ilk gecis tavani var mi. Turbo acilacaksa ilk gecisin
    /// nereye kadar hizlanacagini bu belirler; <b>acilip acilmayacagini</b> belirlemez.
    /// </summary>
    public static bool SupportsTurboFirstPass(string codec)
        => TurboFirstPassCeilings.ContainsKey(codec);

    public static string? TurboFirstPassCeiling(string codec)
        => TurboFirstPassCeilings.TryGetValue(codec, out var entry) ? entry.Ceiling : null;

    /// <summary>
    /// Kodegin tavani uretim yolunda acilabilir mi. <see cref="SupportsTurboFirstPass"/>
    /// tavanin varligini, bu tavanin <b>kullanilabilirligini</b> soyler; ikisi <c>libx264</c>
    /// icin ayrisir.
    /// </summary>
    public static bool TurboFirstPassIsSafe(string codec)
        => TurboFirstPassCeilings.TryGetValue(codec, out var entry) && entry.Safe;

    public static bool CostsQualityInHardware(string codec)
        => IsHardware(codec)
           && !codec.Equals("av1_nvenc", StringComparison.OrdinalIgnoreCase)
           && !codec.Equals("av1_qsv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Kalite hedefinin kodlayici olcegindeki karsiligi. Son kol <c>-crf</c> uretir, cunku
    /// yazilim kodlayicilarinin hepsi onu kabul eder.
    /// <para>
    /// VideoToolbox o kola dusemez: <c>-crf</c> kabul etmiyor, kendi olcegi <c>-q:v</c> ise bu
    /// depoda olculmedi — <c>docs/olcumler/videotoolbox.md</c> bir Apple M1'de kol basina tek
    /// bir bit hizi veriyor, bir olcek cikarmaya yetmiyor. Olculmemis bir olcek yazmak yerine
    /// kol acikca patliyor: bugun <c>PlanParser.AllowedCodecs</c> videotoolbox kodeklerini
    /// gecirmedigi icin buraya ulasan yok, ama kapiyi acan sozlesme sessiz bir gecersiz bayrak
    /// yerine bu istisnayi gorur.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> QualityArgs(string codec, double quality)
    {
        var exact = quality.ToString("0.#", CultureInfo.InvariantCulture);
        var whole = Math.Round(quality).ToString("0", CultureInfo.InvariantCulture);
        return Vendor(codec) switch
        {
            EncoderVendor.VideoToolbox => throw new NotSupportedException(
                $"VideoToolbox hiz kontrolu olculmedi ({codec}): -crf kabul edilmiyor ve -q:v olceginin "
                + "bu depoda dayanagi yok. Kapiyi acan sozlesme olcegi olcup bu kolu yazar."),
            EncoderVendor.Nvenc => new[] { "-rc", "vbr", "-multipass", "fullres", "-cq", exact },
            EncoderVendor.Qsv => codec.Equals("h264_qsv", StringComparison.OrdinalIgnoreCase)
                ? new[] { "-global_quality", whole, "-look_ahead", "1" }
                : new[] { "-global_quality", whole },
            EncoderVendor.Amf => new[] { "-rc", "cqp", "-qp_i", whole, "-qp_p", whole, "-qp_b", whole },
            _ => new[] { "-crf", exact }
        };
    }

    public static IReadOnlyList<string> BitrateRateControlArgs(string codec) => Vendor(codec) switch
    {
        EncoderVendor.Nvenc => new[] { "-rc", "vbr", "-multipass", "fullres" },
        EncoderVendor.Amf => new[] { "-rc", "vbr_peak" },
        EncoderVendor.Qsv => codec.Equals("h264_qsv", StringComparison.OrdinalIgnoreCase)
            ? new[] { "-look_ahead", "1" }
            : Array.Empty<string>(),
        _ => Array.Empty<string>()
    };

    public static (int Min, int Max) CrfRange(string codec) => Family(codec) switch
    {
        "av1" => (18, 55),
        _ => (10, 45)
    };

    public static string SourceFamily(string sourceCodec) => Family(sourceCodec);

    public static double SourceBitrateNeed(string sourceCodec) => Family(sourceCodec) switch
    {
        "av1" => 0.55,
        "hevc" => 0.68,
        "vp9" => 0.72,
        "h264" => 1.0,
        "vp8" => 1.35,
        "mpeg4" => 1.8,
        "mpeg2video" => 2.4,
        "wmv3" or "vc1" => 1.5,
        _ => 1.15
    };

    private static string Family(string codec)
    {
        var c = codec.ToLowerInvariant();
        if (c.Contains("av1")) return "av1";
        if (c.Contains("265") || c.Contains("hevc")) return "hevc";
        if (c.Contains("vp9")) return "vp9";
        if (c.Contains("vp8")) return "vp8";
        if (c.Contains("264") || c.Contains("avc")) return "h264";
        return c;
    }
}
