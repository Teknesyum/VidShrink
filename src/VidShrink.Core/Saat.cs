using System.Globalization;

namespace VidShrink.Core;

/// <summary>
/// Süre yazımının tek gövdesi. On iki çağrı yeri kendi biçimini elle kuruyordu ve üçü
/// <see cref="CultureInfo.InvariantCulture"/> vermiyordu; aynı saniye arayüzün iki
/// köşesinde iki türlü yazılabiliyordu. Dört yüzey var çünkü aileler gerçekten ayrı:
/// ekranda okunan saat, kalan süre, ffmpeg'in istediği argüman ve dosya adına giren
/// damga (iki nokta kullanılamaz).
/// </summary>
public static class Saat
{
    /// <summary>
    /// Ekran saati. Biçimi <paramref name="olcek"/> seçer, <paramref name="deger"/> değil:
    /// bir saatlik kaydın şeridi baştan sona <c>hh:mm:ss</c> kalır, yoksa ilk dakikada
    /// <c>mm:ss</c> yazıp sonra genişler ve sayılar yerinden oynar.
    /// </summary>
    public static string Ekran(TimeSpan deger, TimeSpan olcek)
    {
        if (deger < TimeSpan.Zero) deger = TimeSpan.Zero;

        return olcek.TotalHours >= 1
            ? ((int)deger.TotalHours).ToString("00", CultureInfo.InvariantCulture)
              + ":" + deger.Minutes.ToString("00", CultureInfo.InvariantCulture)
              + ":" + deger.Seconds.ToString("00", CultureInfo.InvariantCulture)
            : ((int)deger.TotalMinutes).ToString("00", CultureInfo.InvariantCulture)
              + ":" + deger.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Ölçeği bilinmeyen tek bir süre kendi büyüklüğüne bakar.</summary>
    public static string Ekran(TimeSpan deger) => Ekran(deger, deger);

    /// <summary>
    /// Kalan süre. Ölçüm gelmeden önce de bir şey yazılması gerekiyor; boşluğun yazısı
    /// üç çağrı yerinde ayrı ayrı <c>"-"</c> olarak duruyordu.
    /// </summary>
    public static string Kalan(TimeSpan? deger) => deger is null ? "-" : Ekran(deger.Value);

    /// <summary>
    /// Kesit saati: ondalık saniye taşır, çünkü kırpma penceresi saniyenin altında
    /// konuşuyor ve kullanıcı ekranda kaç onda kesildiğini görüyor. Baştaki sıfırı
    /// yazmaz — aralık yazısı (<c>0:00–1:12.5</c>) iki yana da yayılmasın.
    /// </summary>
    public static string Kesit(TimeSpan deger)
    {
        if (deger < TimeSpan.Zero) deger = TimeSpan.Zero;

        return deger.TotalHours >= 1
            ? deger.ToString(@"h\:mm\:ss\.f", CultureInfo.InvariantCulture)
            : deger.ToString(@"m\:ss\.f", CultureInfo.InvariantCulture);
    }

    /// <summary>ffmpeg'in <c>-ss</c> / <c>-to</c> için beklediği biçim.</summary>
    public static string Ffmpeg(TimeSpan deger)
        => (deger < TimeSpan.Zero ? TimeSpan.Zero : deger).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    /// <summary>
    /// Dosya adına giren damga: iki nokta yerine tire, milisaniye dahil. Ekran saatinden
    /// ayrı durmak zorunda, çünkü Windows dosya adında <c>:</c> kabul etmiyor.
    /// </summary>
    public static string DosyaAdi(TimeSpan deger)
    {
        if (deger < TimeSpan.Zero) deger = TimeSpan.Zero;

        return ((int)deger.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + "-" + deger.Minutes.ToString("00", CultureInfo.InvariantCulture)
            + "-" + deger.Seconds.ToString("00", CultureInfo.InvariantCulture)
            + "-" + deger.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Ölçülen saniye sayısı: saat değil, sayı. Kırpmanın "şu kadar saniye atıldı"
    /// bildirimi gibi yerlerde tek ondalık kalıyor — megabaytla aynı gerekçe, ikinci
    /// ondalık bu ölçekte gürültü.
    /// </summary>
    public static string Sure(double saniye, CultureInfo kultur) =>
        saniye.ToString("0.0", kultur);

    /// <summary>
    /// Tam saniye. Basamak ayracı <b>yazılmaz</b>: bütçe satırı <c>N0</c> kullanıyordu ve
    /// Türkçe arayüzde 2796 saniyeyi <c>2.796</c> diye yazıp ondalık gibi okutuyordu.
    /// </summary>
    public static string TamSaniye(double saniye, CultureInfo kultur) =>
        saniye.ToString("0", kultur);

    /// <summary>
    /// Motorun İngilizce tanı ve istem metinlerindeki saniye. Kültür sabit: bu satırlar
    /// çevrilmiyor ve iki koşumun günlüğü karşılaştırılabilir kalmalı. Üç çağrı yeri
    /// araya biçim koymadan yazıyordu ve makinenin kültürünü okuyordu — Türkçe bir
    /// makinede İngilizce istem <c>12,34 s</c> diye virgüllü çıkıyordu.
    /// </summary>
    public static class Tani
    {
        /// <inheritdoc cref="Sure(double, CultureInfo)"/>
        public static string Saniye(double saniye) =>
            saniye.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
