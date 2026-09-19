using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Çıktı dosyasının adını kuran desen. Ad bugüne kadar <c>{ad}_shrunk</c> olarak gömülüydü:
/// kullanıcı klasörü seçebiliyor ama adı seçemiyordu.
/// </summary>
/// <remarks>
/// Yer tutucu adları <b>dürüst</b>: <c>{kalite}</c> kodlayıcının kalite kolunu gösterir,
/// hedef boyutu değil. Boyut ayrı bir yer tutucudur (<c>{hedef}</c>); ikisini tek ada
/// yığmak adı yalancı yapardı.
/// </remarks>
public static class AdlandirmaDeseni
{
    /// <summary>Bugünkü davranış; ayar boş bırakılırsa bu kullanılır.</summary>
    public const string Varsayilan = "{ad}_shrunk";

    /// <summary>Tanınan yer tutucular. Küme kapalı: dışarıdan gelen ad buna göre elenir.</summary>
    public static readonly IReadOnlyList<string> YerTutucular =
        ["ad", "hedef", "kalite", "cozunurluk", "kodek", "tarih"];

    /// <summary>Dosya adında duramayan işaretler; işletim sisteminin listesine ek.</summary>
    private static readonly char[] YasakIsaretler = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Desenin kullanılabilirliği. Hata varsa dil dosyasının anahtarı döner; yoksa
    /// <c>null</c>.
    /// </summary>
    public static string? Hata(string? desen)
    {
        if (string.IsNullOrWhiteSpace(desen)) return "settings-tab.output-name.empty";
        if (desen.IndexOfAny(YasakIsaretler) >= 0) return "settings-tab.output-name.bad-char";
        if (desen.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "settings-tab.output-name.bad-char";

        var derinlik = 0;
        var ad = new StringBuilder();
        foreach (var isaret in desen)
        {
            if (isaret == '{')
            {
                if (derinlik > 0) return "settings-tab.output-name.bad-placeholder";
                derinlik++;
                ad.Clear();
                continue;
            }

            if (isaret == '}')
            {
                if (derinlik == 0) return "settings-tab.output-name.bad-placeholder";
                derinlik--;
                if (!YerTutucular.Contains(ad.ToString())) return "settings-tab.output-name.bad-placeholder";
                continue;
            }

            if (derinlik > 0) ad.Append(isaret);
        }

        if (derinlik != 0) return "settings-tab.output-name.bad-placeholder";
        return UygulaHam(desen, new AdBilgisi("x")).Length == 0 ? "settings-tab.output-name.empty" : null;
    }

    /// <summary>Kullanılabilir desen mi.</summary>
    public static bool Gecerli(string? desen) => Hata(desen) is null;

    /// <summary>
    /// Deseni uygular ve uzantısız dosya adını döndürür. Değeri olmayan yer tutucu boşa
    /// düşer, arda kalan ayırıcılar sadeleşir; bozuk desen verilirse varsayılana dönülür,
    /// çünkü ad kurulamadı diye iş durmaz.
    /// </summary>
    public static string Uygula(string? desen, AdBilgisi bilgi)
        => UygulaHam(Gecerli(desen) ? desen! : Varsayilan, bilgi);

    private static string UygulaHam(string desen, AdBilgisi bilgi)
    {
        var sonuc = new StringBuilder();
        var ad = new StringBuilder();
        var derinlik = 0;
        foreach (var isaret in desen)
        {
            if (isaret == '{') { derinlik = 1; ad.Clear(); continue; }
            if (isaret == '}' && derinlik == 1)
            {
                derinlik = 0;
                sonuc.Append(Deger(ad.ToString(), bilgi));
                continue;
            }

            if (derinlik == 1) ad.Append(isaret);
            else sonuc.Append(isaret);
        }

        return Sadelestir(sonuc.ToString());
    }

    private static string Deger(string yerTutucu, AdBilgisi bilgi) => yerTutucu switch
    {
        "ad" => bilgi.Ad,
        "hedef" => bilgi.HedefMb is { } mb && mb > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(mb, 2):0.##}mb").Replace(".", "_", StringComparison.Ordinal)
            : string.Empty,
        "kalite" => bilgi.Crf is { } crf
            ? string.Create(CultureInfo.InvariantCulture, $"crf{Math.Round(crf, 1):0.#}").Replace(".", "_", StringComparison.Ordinal)
            : bilgi.VideoBitrateK is { } k && k > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{k}k")
                : string.Empty,
        "cozunurluk" => bilgi.Yukseklik is { } y && y > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{y}p")
            : string.Empty,
        "kodek" => KisaKodek(bilgi.Kodek),
        "tarih" => bilgi.Tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => string.Empty
    };

    /// <summary>
    /// Kodlayıcı adı değil kodek adı yazılır: <c>libx265</c> ve <c>hevc_nvenc</c> aynı
    /// kodeği üretir, ad ikisinde de <c>hevc</c> olur.
    /// </summary>
    private static string KisaKodek(string? kodlayici)
    {
        if (string.IsNullOrWhiteSpace(kodlayici)) return string.Empty;
        var ad = kodlayici.ToLowerInvariant();
        if (ad.Contains("av1", StringComparison.Ordinal)) return "av1";
        if (ad.Contains("265", StringComparison.Ordinal) || ad.Contains("hevc", StringComparison.Ordinal)) return "hevc";
        if (ad.Contains("264", StringComparison.Ordinal) || ad.Contains("avc", StringComparison.Ordinal)) return "h264";
        if (ad.Contains("vp9", StringComparison.Ordinal)) return "vp9";
        return ad;
    }

    /// <summary>
    /// Boşa düşen yer tutucunun ardında kalan ayırıcıları toplar: <c>ad__720p</c> değil
    /// <c>ad_720p</c>, uçlarda ayırıcı kalmaz.
    /// </summary>
    private static string Sadelestir(string metin)
    {
        var sonuc = new StringBuilder(metin.Length);
        foreach (var isaret in metin)
        {
            var ayirici = isaret is '_' or '-' or '.' or ' ';
            if (ayirici && (sonuc.Length == 0 || sonuc[^1] == isaret)) continue;
            sonuc.Append(isaret);
        }

        while (sonuc.Length > 0 && (sonuc[^1] is '_' or '-' or '.' or ' ')) sonuc.Length--;
        return sonuc.ToString();
    }
}

/// <summary>
/// Desenin okuduğu değerler. Hepsi isteğe bağlı: plan kurulmadan da ad kurulabilmeli,
/// o yüzden değeri olmayan yer tutucu boşa düşer.
/// </summary>
/// <param name="Ad">Kaynağın uzantısız adı.</param>
public sealed record AdBilgisi(string Ad)
{
    public double? HedefMb { get; init; }

    public double? Crf { get; init; }

    public int? VideoBitrateK { get; init; }

    public int? Yukseklik { get; init; }

    public string? Kodek { get; init; }

    public DateTime Tarih { get; init; } = DateTime.Now;
}
