using System.Globalization;

namespace VidShrink.Core;

/// <summary>
/// Kullanicinin kucultmek istedigi kesit. Donusturucunun <see cref="ConversionPlan.Start"/>
/// alanindan ayri durur: bu pencere hedef boyut hesabina girer, cunku hedef MB kesitin
/// <b>ciktisina</b> uygulanir (fable 2026-09-18, S2).
///
/// Arama melez bolunur (S1): <see cref="LeadSeconds"/> girdiden <b>once</b> gelen
/// <c>-ss</c>, <see cref="RemainderSeconds"/> girdiden <b>sonra</b> gelen <c>-ss</c>.
/// Ikisinin toplami her zaman <see cref="StartSeconds"/>'dir; girdi-oncesi arama anahtar
/// kareye yuvarlanabildigi icin (docs/olcumler/sahne-butcesi.md:755) kalan fark
/// girdi-sonrasi aramayla kare hassasiyetinde alinir, girdi-oncesi arama olmadan da
/// dosya bastan cozulmez (FfmpegArguments'in BuildSegment gerekcesi).
/// </summary>
public sealed record TrimWindow(double StartSeconds, double EndSeconds)
{
    /// <summary>
    /// Girdi-oncesi aramanin istenen baslangictan ne kadar geri oturdugu. Ust sinir
    /// uretimin <c>-g</c> tavanindan geliyor (docs/olcumler/anahtar-kare-tavani.md:
    /// erisim alani [5,0 ; 10,0] sn). Kaynagin gercek I-kare araligi olculmedi; fable
    /// bunu olculecek borc olarak isaretledi.
    /// </summary>
    public const double SeekLeadSeconds = 10.0;

    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);

    public double LeadSeconds => Math.Max(0, StartSeconds - Math.Min(StartSeconds, SeekLeadSeconds));

    public double RemainderSeconds => StartSeconds - LeadSeconds;

    public static TrimWindow? Of(double? startSeconds, double? endSeconds, double sourceDurationSeconds)
    {
        if (startSeconds is null && endSeconds is null) return null;
        var start = Math.Max(0, startSeconds ?? 0);
        if (endSeconds is null && sourceDurationSeconds <= 0)
            throw new ArgumentException("kesitin acik ucu kaynak suresi bilinmeden kapatilamaz");
        var end = endSeconds ?? sourceDurationSeconds;
        if (sourceDurationSeconds > 0) end = Math.Min(end, sourceDurationSeconds);
        if (end <= start) throw new ArgumentException($"kesit sonu ({end.ToString("0.###", CultureInfo.InvariantCulture)} sn) baslangicindan sonra olmali ({start.ToString("0.###", CultureInfo.InvariantCulture)} sn)");
        if (startSeconds is null && Math.Abs(end - sourceDurationSeconds) < 0.001) return null;
        return new TrimWindow(start, end);
    }

    /// <summary>
    /// Plan hesabina giren kaynak kunyesini kesite indirir: sure kesit suresi olur, kaynak
    /// bayti sure oraniyla olceklenir. Oran ikisine birlikte uygulandigi icin kaynak bit
    /// hizi degismez; rejim, sikistirma orani ve bit/piksel tabani kesitten turer.
    /// Gercek kesit bayti okunmaz (fable S2: ek ffprobe yerine sure orani).
    /// </summary>
    public MediaInfo Apply(MediaInfo info)
    {
        if (info.DurationSeconds <= 0) return info;
        var kept = Math.Min(DurationSeconds, info.DurationSeconds);
        if (kept <= 0) return info;
        var share = kept / info.DurationSeconds;
        return info with
        {
            DurationSeconds = kept,
            FileSizeBytes = Math.Max(1, (long)Math.Round(info.FileSizeBytes * share)),
        };
    }
}
