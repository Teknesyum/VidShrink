namespace VidShrink.Core;

/// <summary>
/// Kisa ornek kodlamaya uygulanan hiz kontrolunun ne kadar guvenilir oldugu. Arayuz
/// "yaklasik onizleme" rozetini bu alandan surer; rozet kosulunu kendisi uydurmaz.
/// </summary>
public enum PreviewQuality
{
    /// <summary>Plan zaten bir kalite degeri tasiyordu; parca ayni degerle kodlanir.</summary>
    Kesin,

    /// <summary>Planin hedef bitrate'i kalite degerine cevrildi. Sonuc bir tahmindir.</summary>
    Yaklasik,

    /// <summary>Kodlayicinin kalite olcegi modellenmiyor; parca planin kendi hiz kontrolunu tasir.</summary>
    Desteklenmiyor
}

/// <summary>
/// Bir plandaki hiz kontrolunun kisa parca karsiligi: hangi kalite degeri uygulanacak ve
/// bu deger olculmus mu, tahmin mi.
/// </summary>
/// <param name="Kind">Degerin guvenilirligi.</param>
/// <param name="Crf">
/// Kodlayicinin kalite olceginde uygulanacak deger. <see cref="PreviewQuality.Desteklenmiyor"/>
/// oldugunda <c>null</c>.
/// </param>
public sealed record PreviewQualityChoice(PreviewQuality Kind, double? Crf);

/// <summary>
/// Bir plandan ve bir zaman noktasindan uretilen kisa ornek kodlama. Zaman penceresini
/// kaynaga gore kirpar, planin ortalama bitrate'i yerine onun kalite karsiligini uygular ve
/// ffmpeg argumanlarini verir. Surec acmaz, dosya okumaz.
/// </summary>
public sealed record PreviewSegment
{
    /// <summary>
    /// Parcanin sure tavani. Prob pencerelerinden uzun: 5 sn'lik pencere kullaniciya sahnenin
    /// devamini gosterir, 2 sn'lik pencere karsilastirma icin yeterli gelmiyordu.
    /// </summary>
    public const double WindowSeconds = 5.0;

    /// <summary>
    /// Kalite olcegi modellenen kodlayicilar. <see cref="CodecModel.ReferenceCrf"/> ve
    /// <see cref="CodecModel.CrfRange"/> yalnizca h264/hevc/av1 ailelerini tanir; listede
    /// olmayan bir kodlayici icin bitrate karsiligi <b>baska bir olcekten</b> cikardi.
    /// </summary>
    private static readonly HashSet<string> ModelledCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "libx264", "libx265", "libsvtav1",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    };

    /// <summary>Parcanin kaynaktaki baslangici.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>Kodlanacak gercek sure. Kaynagin sonunda bu, istenenden kisadir.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Cagiranin istedigi sure. Kirpma bu ikisinin farkinda gorunur.</summary>
    public required double RequestedDurationSeconds { get; init; }

    /// <summary>Parca kaynagin sonuna dayandi ve kisaldi.</summary>
    public bool WasClamped => DurationSeconds < RequestedDurationSeconds - 1e-6;

    /// <summary>Uygulanan hiz kontrolunun guvenilirligi ve degeri.</summary>
    public required PreviewQualityChoice Quality { get; init; }

    /// <summary>Plan iki gecisliydi; parca tek gecise dusuruldu.</summary>
    public required bool DroppedSecondPass { get; init; }

    /// <summary>
    /// Parcanin gordugu, tam kodlamadan sapmis bir yani var: ya kalite degeri tahmin, ya da
    /// ikinci gecis dusuruldu. Rozetin kosulu budur.
    /// </summary>
    public bool IsApproximate => Quality.Kind != PreviewQuality.Kesin || DroppedSecondPass;

    /// <summary>Parcanin kodlanmasi icin ffmpeg argumanlari.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Parcanin kodlamada kullandigi plan; tam plandan yalnizca hiz kontrolunde ayrilir.</summary>
    public required EncodePlan Plan { get; init; }

    /// <summary>
    /// Planin ortalama bitrate'inin kalite olcegindeki karsiligi. Bagintiyi projenin kendi
    /// karmasiklik modeli verir: bitrate once bit/piksel/kare'ye cevrilir
    /// (<see cref="PlanCalculator.BitsPerPixel"/>), sonra
    /// <see cref="ComplexityProfile.CrfForBppf"/> ile kaliteye. Tam kodlamanin CRF secimi de
    /// ayni iki adimdan gecer, boylece parca ile tam kodlama ayni olcekte kalir.
    /// </summary>
    public static PreviewQualityChoice QualityFor(MediaInfo info, EncodePlan plan, ComplexityProfile? complexity = null)
    {
        if (!ModelledCodecs.Contains(plan.Codec))
            return new PreviewQualityChoice(PreviewQuality.Desteklenmiyor, null);

        var (min, max) = CodecModel.CrfRange(plan.Codec);

        if (plan.ModeEnum == EncodeMode.Crf && plan.Crf is { } planned)
            return new PreviewQualityChoice(PreviewQuality.Kesin, Math.Clamp((double)planned, min, max));

        var profile = complexity ?? ComplexityProfile.FromSourceBitrate(info);
        var scale = plan.Width > 0 && info.Width > 0 ? (double)plan.Width / info.Width : 1.0;
        var sourceFps = info.Fps > 0 ? info.Fps : plan.Fps;
        var bppf = PlanCalculator.BitsPerPixel(plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
        var crf = profile.CrfForBppf(plan.Codec, bppf, scale, plan.Fps, sourceFps);
        if (!double.IsFinite(crf)) return new PreviewQualityChoice(PreviewQuality.Desteklenmiyor, null);

        return new PreviewQualityChoice(PreviewQuality.Yaklasik, Math.Clamp(crf, min, max));
    }

    /// <summary>
    /// Verilen an icin parcayi hesaplar. <paramref name="startSeconds"/> negatif olamaz,
    /// <paramref name="durationSeconds"/> pozitif olmalidir; parca sonu kaynagi asarsa hata
    /// atilmaz, sure kirpilir ve kirpma <see cref="WasClamped"/> ile gorunur.
    /// </summary>
    public static PreviewSegment For(
        MediaInfo info,
        EncodePlan plan,
        double startSeconds,
        string outputPath,
        double? durationSeconds = null,
        ComplexityProfile? complexity = null)
    {
        if (startSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(startSeconds), startSeconds, "Parca baslangici negatif olamaz.");

        var requested = durationSeconds ?? WindowSeconds;
        if (requested <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), requested, "Parca suresi pozitif olmalidir.");

        var duration = requested;
        if (info.DurationSeconds > 0)
        {
            var remaining = info.DurationSeconds - startSeconds;
            if (remaining <= 0)
                throw new ArgumentOutOfRangeException(nameof(startSeconds), startSeconds, "Parca baslangici kaynagin suresini asiyor.");
            duration = Math.Min(requested, remaining);
        }

        var quality = QualityFor(info, plan, complexity);
        var segmentPlan = plan.Clone();
        if (quality.Crf is { } crf)
        {
            segmentPlan.Mode = "crf";
            segmentPlan.Crf = (int)Math.Round(crf);
        }

        return new PreviewSegment
        {
            StartSeconds = startSeconds,
            DurationSeconds = duration,
            RequestedDurationSeconds = requested,
            Quality = quality,
            DroppedSecondPass = plan.ModeEnum == EncodeMode.TwoPass && FfmpegArguments.NeedsTwoPasses(plan.Codec),
            Plan = segmentPlan,
            Arguments = FfmpegArguments.BuildSegment(info, segmentPlan, startSeconds, duration, outputPath)
        };
    }
}
