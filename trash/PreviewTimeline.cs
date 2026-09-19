namespace VidShrink.Core;

/// <summary>
/// Panelin durumlari. Sag tarafin etiketi bu durumdan turer; arayuzde dogaclanmaz.
/// </summary>
public enum PreviewState
{
    /// <summary>Henuz dosya yok.</summary>
    KaynakYok,

    /// <summary>Dosya var, plan yok. Sagda gosterilecek bir sey yok.</summary>
    YalnizKaynak,

    /// <summary>Kisa ornek kodlaniyor. Sag yari henuz yok; son onbellekli cift gosterilir.</summary>
    OrnekKodlaniyor,

    /// <summary>Sagdaki kare plandan uretilmis kisa bir ornek kodlamadan geliyor.</summary>
    OrnekKodlama,

    /// <summary>Tam kodlama suruyor. Cikti dosyasi bitmeden sag yari gosterilemez.</summary>
    TamKodlama,

    /// <summary>Sagdaki kare tamamlanmis gercek ciktidan geliyor.</summary>
    GercekCikti,

    /// <summary>Kare cekilemedi: bozuk dosya, sure disi arama ya da ffmpeg hatasi.</summary>
    Olculemedi
}

/// <summary>
/// Durumdan turetilen kurallar. Etiket metnini arayuz kendi dilinde yazar; **hangi** etiketi
/// yazacagina buradan karar verir.
/// </summary>
public static class PreviewStatus
{
    /// <summary>
    /// Bu durumda yeni kare cekilebilir mi? Kodlama surerken cekilemez: T30/O2 kodlama
    /// yavaslamasini %17,8-28,4 olctu, konseyin %5 kurali asildi. O sirada son onbellekli
    /// cift gosterilir.
    ///
    /// <see cref="PreviewState.OrnekKodlama"/> burada yok ama
    /// <see cref="HasRightHalf"/> icinde var; bu bir celiski degil, iki ayri soru.
    /// O durumda sag yaride gosterilecek bir kare <b>vardir</b> — kisa ornek parcadan
    /// gelir — ama <b>yeni</b> bir kare cekilecek bir tam cikti dosyasi yoktur.
    /// </summary>
    public static bool AllowsFrameGrab(PreviewState state)
        => state is PreviewState.YalnizKaynak or PreviewState.GercekCikti;

    /// <summary>Sag yarida gosterilecek bir kare var mi?</summary>
    public static bool HasRightHalf(PreviewState state)
        => state is PreviewState.OrnekKodlama or PreviewState.GercekCikti;

    /// <summary>
    /// Durumu tek yerden turetir. Arayuz kendi kosullarindan durum uydurmaz, bunu cagirir.
    /// </summary>
    /// <param name="sampleEncoding">Kisa ornek parca su anda kodlaniyor.</param>
    /// <param name="hasSample">Kisa ornek parca bitti ve sag yari ondan besleniyor.</param>
    public static PreviewState Derive(
        bool hasSource,
        bool hasPlan,
        bool isEncoding,
        bool hasRealOutput,
        bool grabFailed,
        bool sampleEncoding = false,
        bool hasSample = false)
    {
        if (!hasSource) return PreviewState.KaynakYok;
        if (grabFailed) return PreviewState.Olculemedi;
        if (isEncoding) return PreviewState.TamKodlama;
        if (hasRealOutput) return PreviewState.GercekCikti;
        if (!hasPlan) return PreviewState.YalnizKaynak;
        if (sampleEncoding) return PreviewState.OrnekKodlaniyor;
        if (hasSample) return PreviewState.OrnekKodlama;
        return PreviewState.YalnizKaynak;
    }
}

/// <summary>
/// Karsilastirma panelinin zaman noktasi. Eksen **cikti**nin kare izgarasina kilitli;
/// kaynak zamani ondan turetilir.
/// </summary>
/// <param name="OutputFrame">Cikti kare numarasi, 0 tabanli.</param>
/// <param name="OutputSeconds">Ciktinin kendi zaman ekseninde o karenin ani.</param>
/// <param name="SourceSeconds">Ayni karenin kaynaktaki karsiligi.</param>
/// <param name="DriftSeconds">
/// Istenen an ile teslim edilen karenin ani arasindaki fark. Fps dusurulmusse ciktida
/// kaynagin damgasinda kare yoktur; en yakin kare **baska bir andir**.
/// </param>
/// <param name="DriftExceedsSourceFrame">
/// Sapma bir kaynak karesinden buyuk. Hareketli sahnede iki yari arasindaki dikis kopuk
/// gorunur; panel bunu gostermek zorunda.
/// </param>
public sealed record TimelinePoint(
    int OutputFrame,
    double OutputSeconds,
    double SourceSeconds,
    double DriftSeconds,
    bool DriftExceedsSourceFrame);

/// <summary>
/// Kaynak zamani ile cikti zamani arasinda saf donusum. Kirpma (<c>-ss</c>), fps dususu ve
/// sure farki hesaba katilir. Surec acmaz, dosya okumaz.
/// </summary>
public sealed record PreviewTimeline
{
    /// <summary>Kirpmanin kaynaktaki baslangici. Cikti bu anda 0 saniyedir.</summary>
    public required double SourceStartSeconds { get; init; }

    /// <summary>Kirpilmis parcanin kaynak zaman ekseninde suresi.</summary>
    public required double SourceDurationSeconds { get; init; }

    public required double SourceFps { get; init; }

    public required double OutputFps { get; init; }

    /// <summary>Ciktinin kendi suresi. Fps dususu sureyi degistirmez, kirpma degistirir.</summary>
    public double OutputDurationSeconds => SourceDurationSeconds;

    /// <summary>Ciktinin kare sayisi; eksenin uzunlugu budur.</summary>
    public int OutputFrameCount => Math.Max(1, (int)Math.Round(OutputDurationSeconds * OutputFps));

    public bool FpsWasReduced => OutputFps < SourceFps - 0.001;

    public bool WasTrimmed => SourceStartSeconds > 0.001 || SourceDurationSeconds < SourceTotalSeconds - 0.001;

    /// <summary>Kaynagin kirpilmamis toplam suresi.</summary>
    public required double SourceTotalSeconds { get; init; }

    public static PreviewTimeline For(
        MediaInfo info,
        EncodePlan plan,
        double trimStartSeconds = 0,
        double? trimDurationSeconds = null)
    {
        var start = Math.Clamp(trimStartSeconds, 0, info.DurationSeconds);
        var remaining = info.DurationSeconds - start;
        var duration = trimDurationSeconds is { } d ? Math.Clamp(d, 0, remaining) : remaining;

        return new PreviewTimeline
        {
            SourceStartSeconds = start,
            SourceDurationSeconds = duration,
            SourceTotalSeconds = info.DurationSeconds,
            SourceFps = info.Fps > 0 ? info.Fps : 30,
            OutputFps = plan.Fps > 0 ? plan.Fps : (info.Fps > 0 ? info.Fps : 30)
        };
    }

    /// <summary>
    /// Ciktinin zaman ekseninde bir an. Eksen ciktinin kare izgarasina kilitli oldugu icin
    /// giris once en yakin cikti karesine oturtulur, kaynak ondan turetilir.
    /// </summary>
    public TimelinePoint FromOutput(double outputSeconds)
    {
        var clamped = Math.Clamp(outputSeconds, 0, OutputDurationSeconds);
        var frame = Math.Clamp((int)Math.Round(clamped * OutputFps), 0, OutputFrameCount - 1);
        return PointFor(frame, clamped);
    }

    /// <summary>
    /// Kaynak zaman ekseninde bir an. Kirpma cikarilir, sonra ayni izgaraya oturtulur.
    /// </summary>
    public TimelinePoint FromSource(double sourceSeconds)
    {
        var clamped = Math.Clamp(sourceSeconds, SourceStartSeconds, SourceStartSeconds + SourceDurationSeconds);
        var asked = clamped - SourceStartSeconds;
        var frame = Math.Clamp((int)Math.Round(asked * OutputFps), 0, OutputFrameCount - 1);
        return PointFor(frame, asked);
    }

    public TimelinePoint FromFrame(int outputFrame)
    {
        var frame = Math.Clamp(outputFrame, 0, OutputFrameCount - 1);
        return PointFor(frame, frame / OutputFps);
    }

    private TimelinePoint PointFor(int frame, double askedOutputSeconds)
    {
        var outputSeconds = frame / OutputFps;
        var sourceSeconds = SourceStartSeconds + outputSeconds;
        var drift = Math.Abs(outputSeconds - askedOutputSeconds);
        var sourceFrameSeconds = 1.0 / (SourceFps > 0 ? SourceFps : 30);
        return new TimelinePoint(frame, outputSeconds, sourceSeconds, drift, drift > sourceFrameSeconds + 1e-9);
    }
}
