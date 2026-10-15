using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Hakkında ve tanılama kartlarının dış adresleri ve tanılama paketi. Her adres buradan
/// çıkar ki düğmenin açtığı yer ölçülebilsin. Hata bildirme yalnız GitHub issue şablonuna
/// gider; e-posta ya da telefon yolu yok.
///
/// <para><b>Gizlilik:</b> bildirime ve pakete giren günlük yolu kullanıcı klasörünü
/// <c>~</c>'ye indirir; kullanıcı adı adrese ve pakete girmez. Günlüğün kendisi zaten yolsuz
/// (<see cref="Gunluk"/>).</para>
/// </summary>
public static class Destek
{
    /// <summary>Deponun adresi.</summary>
    public const string DepoAdresi = "https://github.com/Teknesyum/VidShrink";

    /// <summary>Hata bildiriminin issue şablonu: <c>.github/ISSUE_TEMPLATE/</c> altındaki dosya adı.</summary>
    public const string HataSablonu = "bug_report.yml";

    /// <summary>Paketteki sistem özetinin adı.</summary>
    public const string SistemDosyasi = "sistem.txt";

    /// <summary>README'nin gizlilik bölümü.</summary>
    public const string GizlilikAdresi = DepoAdresi + "#privacy";

    /// <summary>Üçüncü taraf bildirimleri; uygulamanın kendi lisansına da oradan bağlanılır.</summary>
    public const string LisansAdresi = DepoAdresi + "/blob/main/THIRD-PARTY-NOTICES.md";

    /// <summary>Verilen sürümün yayın notları; derleme eki (<c>+...</c>) atılır.</summary>
    public static string SurumNotlari(string surum) => $"{DepoAdresi}/releases/tag/v{Etiket(surum)}";

    /// <summary>
    /// Doldurulmuş hata bildirimi: şablonun <c>version</c>, <c>os</c> ve <c>log-path</c>
    /// alanları sorgu dizgisinden dolar.
    /// </summary>
    public static string HataBildir(string surum, string isletim, string gunlukYolu) =>
        $"{DepoAdresi}/issues/new?template={HataSablonu}" +
        $"&version={Uri.EscapeDataString(Etiket(surum))}" +
        $"&os={Uri.EscapeDataString(isletim)}" +
        $"&log-path={Uri.EscapeDataString(gunlukYolu)}";

    /// <summary>Kullanıcı klasörüyle başlayan yolun o kısmını <c>~</c>'ye indirir.</summary>
    public static string GizliYol(string yol, string kullaniciKlasoru)
    {
        if (string.IsNullOrEmpty(yol) || string.IsNullOrEmpty(kullaniciKlasoru)) return yol;
        var kok = kullaniciKlasoru.TrimEnd('\\', '/');
        if (kok.Length == 0) return yol;
        var karsilastirma = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!yol.StartsWith(kok, karsilastirma)) return yol;
        if (yol.Length == kok.Length) return "~";
        var ayrac = yol[kok.Length];
        return ayrac is '\\' or '/' ? "~" + yol[kok.Length..] : yol;
    }

    /// <summary>Kullanıcının kendi klasörüne göre gizlenmiş günlük yolu.</summary>
    public static string GunlukYolu() =>
        GizliYol(Gunluk.Dosya, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>İşletim sistemi ve mimari, bildirimin <c>os</c> alanı için.</summary>
    public static string Isletim() =>
        $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})";

    /// <summary>
    /// Paketin ve kopyalanan metnin sistem özeti. Güncelleme sağlığının son hatası da
    /// <see cref="Gunluk.YoluIndirge"/>'den geçer.
    /// </summary>
    public static string SistemBilgisi(string surum, string isletim, string gunlukYolu, UpdateHealth saglik)
    {
        var sb = new StringBuilder();
        sb.Append("vidshrink: ").AppendLine(Etiket(surum));
        sb.Append("os: ").AppendLine(isletim);
        sb.Append("rid: ").AppendLine(RuntimeInformation.RuntimeIdentifier);
        sb.Append("dotnet: ").AppendLine(RuntimeInformation.FrameworkDescription);
        sb.Append("culture: ").AppendLine(CultureInfo.CurrentUICulture.Name);
        sb.Append("log: ").AppendLine(gunlukYolu);
        sb.Append("update-last-success: ").AppendLine(Zaman(saglik.LastSuccess));
        sb.Append("update-failures: ").AppendLine(saglik.Failures.ToString(CultureInfo.InvariantCulture));
        if (saglik.Failures > 0)
        {
            sb.Append("update-failing-since: ").AppendLine(Zaman(saglik.FailingSince));
            sb.Append("update-last-error: ").AppendLine(Gunluk.YoluIndirge(saglik.LastError));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tanılama paketi: sistem özeti, günlük ve varsa yedeği tek zip'te. Günlük yazılırken
    /// de okunabilsin diye paylaşımlı açılır. Hedef varsa üzerine yazılır.
    /// </summary>
    public static void PaketYaz(string hedef, string gunlukKlasoru, string sistemBilgisi)
    {
        using var akis = new FileStream(hedef, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(akis, ZipArchiveMode.Create);
        Yaz(zip, SistemDosyasi, Encoding.UTF8.GetBytes(sistemBilgisi));
        foreach (var ad in new[] { Gunluk.DosyaAdi, Gunluk.YedekAdi })
        {
            var yol = Path.Combine(gunlukKlasoru, ad);
            if (!File.Exists(yol)) continue;
            using var kaynak = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var bellek = new MemoryStream();
            kaynak.CopyTo(bellek);
            Yaz(zip, ad, bellek.ToArray());
        }
    }

    /// <summary>Paketin önerilen adı.</summary>
    public static string PaketAdi(DateTimeOffset zaman) =>
        $"vidshrink-tani-{zaman.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.zip";

    private static void Yaz(ZipArchive zip, string ad, byte[] icerik)
    {
        var giris = zip.CreateEntry(ad, CompressionLevel.Optimal);
        using var hedef = giris.Open();
        hedef.Write(icerik, 0, icerik.Length);
    }

    private static string Etiket(string surum)
    {
        var deger = (surum ?? "").Trim();
        var arti = deger.IndexOf('+');
        if (arti >= 0) deger = deger[..arti];
        return deger.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? deger[1..] : deger;
    }

    private static string Zaman(DateTimeOffset? zaman) =>
        zaman?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "-";
}
