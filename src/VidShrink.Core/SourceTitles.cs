namespace VidShrink.Core;

/// <summary>Bir basligin nereden geldigi; girdi argumanini bu belirler.</summary>
public enum TitleSourceKind
{
    /// <summary>Duz dosya: tek baslik, girdi argumani bugunku gibi kalir.</summary>
    File,

    /// <summary>Cok programli yayin (MPEG-TS): baslik bir programa denk duser.</summary>
    Program,

    /// <summary>DVD-Video klasoru ya da ISO: baslik <c>dvdvideo</c> demuxer'inin title numarasi.</summary>
    Dvd,
}

/// <summary>
/// Kaynaktaki tek bir baslik. <paramref name="Number"/> kullanicinin yazdigi numaradir:
/// programda ffprobe'un <c>program_id</c>'si, DVD'de <c>-title</c> degeri, duz dosyada 1.
/// </summary>
public sealed record SourceTitle(
    int Number,
    double DurationSeconds,
    int Width,
    int Height,
    int StreamCount,
    int ChapterCount,
    string? Label)
{
    /// <summary>Basligin turu; girdi argumani bundan kurulur.</summary>
    public TitleSourceKind Kind { get; init; } = TitleSourceKind.File;

    /// <summary>
    /// Basligin tasidigi akislarin **mutlak** ffprobe indeksleri. Eslemeler zaten
    /// <c>0:&lt;indeks&gt;</c> yaziliyor, o yuzden program secmek eslemeyi degil envanteri
    /// daraltmak demek; <c>-map 0:p:N</c> sozdizimine hic gerek kalmiyor.
    /// </summary>
    public IReadOnlyList<int> StreamIndexes { get; init; } = Array.Empty<int>();
}

/// <summary>
/// DVD-Video kaynagi. ffmpeg 9.0'da <c>dvdvideo</c> demuxer'i var, Blu-ray icin demuxer
/// yok — BD bu turda kapsam disi (olcum <c>docs/olcumler/e4-baslik-tarama.md</c>).
/// </summary>
public sealed record DiscSource(int Title, int? Angle)
{
    /// <summary><c>-i</c>'den **once** yazilan girdi secenekleri.</summary>
    public IReadOnlyList<string> InputArguments()
    {
        var a = new List<string>
        {
            "-f", "dvdvideo",
            "-title", Title.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (Angle is { } aci)
            a.AddRange(new[] { "-angle", aci.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return a;
    }
}

/// <summary>
/// Baslik envanteri uzerindeki secim kurallari. Kurallar saf: dosya okumaz, surec acmaz,
/// bu yuzden hem CLI hem arayuz ayni karari veriyor. Olcum
/// <c>docs/olcumler/e4-baslik-tarama.md</c>.
/// </summary>
public static class SourceTitles
{
    /// <summary>
    /// Asgari sureyi uygulayarak envanteri daraltir. Sure verilmezse liste oldugu gibi doner;
    /// esik **dahil** degerlendirilir, tam esikteki baslik elenmez.
    /// </summary>
    public static IReadOnlyList<SourceTitle> Ele(IReadOnlyList<SourceTitle> basliklar, double? asgariSure)
    {
        if (basliklar is null || basliklar.Count == 0) return Array.Empty<SourceTitle>();
        if (asgariSure is not { } esik || esik <= 0) return basliklar;
        return basliklar.Where(b => b.DurationSeconds >= esik).ToArray();
    }

    /// <summary>
    /// En uzun baslik. Esitlikte kucuk numara kazanir: siralama girdi sirasina degil
    /// numaraya bagli olsun, ayni kaynak her kosumda ayni basligi versin.
    /// </summary>
    public static SourceTitle? AnaIcerik(IReadOnlyList<SourceTitle> basliklar)
    {
        if (basliklar is null || basliklar.Count == 0) return null;
        return basliklar
            .OrderByDescending(b => b.DurationSeconds)
            .ThenBy(b => b.Number)
            .First();
    }

    /// <summary>
    /// Envanterden tek baslik secer. Donen metin hata anahtaridir; <c>null</c> ise
    /// <paramref name="secilen"/> kullanilabilir. Asgari sure once uygulanir: elenmis bir
    /// basligin numarasini vermek "yok" sayilir, yoksa ayni kosum iki farkli cevap verirdi.
    /// </summary>
    public static string? Sec(
        IReadOnlyList<SourceTitle> basliklar,
        int? numara,
        bool anaIcerik,
        double? asgariSure,
        out SourceTitle? secilen)
    {
        secilen = null;
        if (basliklar is null || basliklar.Count == 0) return "error.no-titles";

        var kalan = Ele(basliklar, asgariSure);
        if (kalan.Count == 0) return "error.no-titles-after-min";

        if (numara is { } istenen)
        {
            secilen = kalan.FirstOrDefault(b => b.Number == istenen);
            return secilen is null ? "error.bad-title" : null;
        }

        secilen = anaIcerik || kalan.Count == 1 ? AnaIcerik(kalan) : kalan[0];
        return null;
    }

    /// <summary>
    /// Yoklamayi secilen basliga daraltir: envanter basligin akislarina iner, olcu ve sure
    /// baslikin kendisinden gelir. Baslik akis tasimiyorsa yoklama oldugu gibi doner —
    /// duz dosyada daraltacak bir sey yok.
    /// </summary>
    public static MediaInfo Uygula(MediaInfo bilgi, SourceTitle baslik)
    {
        if (baslik.StreamIndexes.Count == 0) return bilgi;
        var kume = baslik.StreamIndexes.ToHashSet();
        var akislar = bilgi.Streams.Where(s => kume.Contains(s.Index)).ToArray();
        if (akislar.Length == 0) return bilgi;

        return bilgi with
        {
            Streams = akislar,
            DurationSeconds = baslik.DurationSeconds > 0 ? baslik.DurationSeconds : bilgi.DurationSeconds,
            Width = baslik.Width > 0 ? baslik.Width : bilgi.Width,
            Height = baslik.Height > 0 ? baslik.Height : bilgi.Height,
        };
    }
}
