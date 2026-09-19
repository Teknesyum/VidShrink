using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

/// <summary>
/// Tani gunlugu: HandBrake'in Activity Log'unun karsiligi. Kullanici sorun bildirirken
/// kopyalayacagi tek yer, o yuzden her kosum yazilir; "once gunlugu ac, sonra tekrarla"
/// demek kullaniciyi ikinci kez calistirmaktir.
///
/// <para><b>Gizlilik tek kural:</b> hicbir satirda tam yol bulunmaz. Komut satirindaki ve
/// stderr'deki yollar dosya adina indirilir; kullanici adi, klasor agaci ve surucu harfi
/// gunluge hic girmez. Olcum <c>docs/olcumler/e7-tani-gunlugu.md</c>.</para>
/// </summary>
public static class Gunluk
{
    /// <summary>Devirme esigi: bu boyu asan gunluk yedege alinir ve bastan baslar.</summary>
    public const long DevirmeSiniri = 1024 * 1024;

    /// <summary>stderr'den gunluge inen satir sayisi.</summary>
    public const int KuyrukSatiri = 15;

    /// <summary>Gunluk klasorunun adi; veri sifirlama da bu adi kullanir.</summary>
    public const string KlasorAdi = "gunluk";

    /// <summary>Gunluk dosyasinin adi.</summary>
    public const string DosyaAdi = "vidshrink.log";

    /// <summary>Devirmede tutulan tek yedegin adi.</summary>
    public const string YedekAdi = "vidshrink.1.log";

    private static readonly Regex Yollar = new(
        @"(?:[A-Za-z]:[\\/]|\\\\[^\\/\s""]+[\\/])[^\s""]*|/(?:[^/\s"":]+/)+[^/\s"":]*",
        RegexOptions.Compiled);

    private static readonly object Kilit = new();

    /// <summary>Gunluk klasoru: ayar dosyasinin yaninda, <c>VIDSHRINK_SETTINGS_PATH</c>'i izler.</summary>
    public static string Klasor
    {
        get
        {
            var ayar = UpdateSettings.DefaultPath;
            var kok = Path.GetDirectoryName(ayar);
            return Path.Combine(string.IsNullOrEmpty(kok) ? "." : kok, KlasorAdi);
        }
    }

    /// <summary>Gunluk dosyasinin tam yolu. Kullaniciya gosterilmez, yalniz acilir.</summary>
    public static string Dosya => Path.Combine(Klasor, DosyaAdi);

    /// <summary>Devirmede tutulan tek yedek.</summary>
    public static string Yedek => Path.Combine(Klasor, YedekAdi);

    /// <summary>
    /// Tam yolu dosya adina indirir. Tanimadigi metne dokunmaz: yol gibi gorunmeyen
    /// her sey oldugu gibi kalir, cunku ffmpeg'in kendi anahtarlari da <c>:</c> tasiyor.
    /// </summary>
    public static string YoluIndirge(string metin)
    {
        if (string.IsNullOrEmpty(metin)) return metin;
        return Yollar.Replace(metin, esleme =>
        {
            var ham = esleme.Value.TrimEnd('"');
            var son = ham.LastIndexOfAny(new[] { '\\', '/' });
            var ad = son >= 0 && son + 1 < ham.Length ? ham[(son + 1)..] : ham;
            return ad.Length == 0 ? ham : ad;
        });
    }

    /// <summary>Bir kosumun gunluk blogu. Diske yazilmadan once de okunabilir olsun diye ayri.</summary>
    public static string Blok(
        string komut,
        string uygulamaSurumu,
        string ffmpegSurumu,
        int cikisKodu,
        TimeSpan sure,
        IEnumerable<string> hataKuyrugu)
    {
        var sb = new StringBuilder();
        sb.Append("=== ").Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)).AppendLine(" ===");
        sb.Append("vidshrink: ").AppendLine(uygulamaSurumu);
        sb.Append("ffmpeg: ").AppendLine(ffmpegSurumu);
        sb.Append("komut: ").AppendLine(YoluIndirge(komut));
        sb.Append("cikis: ").Append(cikisKodu.ToString(CultureInfo.InvariantCulture))
          .Append("  sure: ").Append(sure.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)).AppendLine(" sn");

        var kuyruk = hataKuyrugu is null ? Array.Empty<string>() : hataKuyrugu.TakeLast(KuyrukSatiri).ToArray();
        if (kuyruk.Length > 0)
        {
            sb.AppendLine("stderr:");
            foreach (var satir in kuyruk) sb.Append("  ").AppendLine(YoluIndirge(satir));
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Blogu gunluge ekler. Yazamamak isi bozmaz: gunluk yuzunden kosum yarida kalmaz.
    /// </summary>
    public static bool Yaz(string blok) => Yaz(Klasor, blok);

    /// <summary>
    /// Blogu verilen klasordeki gunluge ekler. Klasor disaridan gelebildigi icin olcum
    /// kendi klasorunu kurabiliyor: iki kosum ayni dosyada yarismiyor.
    /// </summary>
    public static bool Yaz(string klasor, string blok)
    {
        try
        {
            lock (Kilit)
            {
                Directory.CreateDirectory(klasor);
                Devir(klasor);
                File.AppendAllText(Path.Combine(klasor, DosyaAdi), blok);
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Gunlugun tamami; yoksa bos dizge.</summary>
    public static string Oku()
    {
        try
        {
            return File.Exists(Dosya) ? File.ReadAllText(Dosya) : string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void Devir(string klasor)
    {
        var dosya = Path.Combine(klasor, DosyaAdi);
        var bilgi = new FileInfo(dosya);
        if (!bilgi.Exists || bilgi.Length < DevirmeSiniri) return;
        File.Copy(dosya, Path.Combine(klasor, YedekAdi), overwrite: true);
        File.Delete(dosya);
    }
}
