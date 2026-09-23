using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Sayı yazımının tek gövdesi. Tarama (<c>docs/inceleme/bicim-govdeleri-2026-09-18.md</c>)
/// altı ailede 32 ayrı biçim ve ~110 çağrı yeri saydı: aynı megabayt değeri arayüzün
/// farklı köşelerinde sekiz türlü yazılabiliyordu. Biçim dizgisi burada **parametre
/// değildir** — yüzeyin adı ne yazdığını söyler, ondalığı gövde bilir. Böylece bir biçimi
/// değiştirmek, <c>Strings</c> kataloğunda bir dizgiyi değiştirmek kadar tek yerlidir.
/// </summary>
public static class Bicim
{
    /// <summary>Dosya boyutu. Ailenin en dağınık olanıydı: dokuz ondalık, üç birim tablosu.</summary>
    public static class Boyut
    {
        /// <summary>
        /// Kaynak ya da çıktı boyutu. Ailenin baskın yazımı <c>0.0</c>; tek ondalık
        /// megabayt ölçeğinde okunabilir, ikincisi gürültü.
        /// </summary>
        public static string Mb(double mb, CultureInfo kultur) =>
            mb.ToString("0.0", kultur);

        /// <summary>
        /// Hedef boyut. Kullanıcının kendi girdiği sayı: <c>50</c> yazdıysa <c>50,0</c>
        /// değil <c>50</c> görmeli, yoksa uygulama onun girdisini düzeltmiş gibi durur.
        /// </summary>
        public static string Hedef(double mb, CultureInfo kultur) =>
            mb.ToString("0.##", kultur);

        /// <summary>
        /// Hedefle çıktı arasındaki fark. Tek ondalık burada sapmayı sıfır gösteriyordu;
        /// iki ondalık, "hedefi tutturduk mu" sorusunun gerçek cevabı.
        /// </summary>
        public static string Sapma(double mb, CultureInfo kultur) =>
            mb.ToString("0.00", kultur);

        /// <summary>
        /// Ham bayt. 1024 tabanında bölünüyorsa birim adı <b>ikilik</b> olmak zorunda:
        /// <c>ShareErrorClassifier.Size</c> 1024'e bölüp <c>KB/MB/GB</c> yazıyordu ve
        /// kullanıcıya gerçekte olduğundan ~%5 küçük bir sayı okutuyordu. Yüzey yalnız
        /// yazar: sıfırın <i>anlamı</i> (bilinmiyor / sınırsız) çağrı yerine aittir ve
        /// üç çağrı yerinde üç ayrı sözcük olduğu için buraya taşınmaz.
        /// </summary>
        public static string Bayt(long bayt, CultureInfo kultur)
        {
            if (bayt <= 0) return "0 B";

            string[] adlar = { "B", "KiB", "MiB", "GiB", "TiB" };
            double deger = bayt;
            var basamak = 0;
            while (deger >= 1024 && basamak < adlar.Length - 1)
            {
                deger /= 1024;
                basamak++;
            }

            var sayi = basamak == 0
                ? deger.ToString("0", kultur)
                : deger.ToString("0.##", kultur);

            return sayi + " " + adlar[basamak];
        }
    }

    /// <summary>
    /// Yüzde. Yüzey <b>oran</b> alır, yüzde üretir: <c>* 100</c> çarpımı sekiz çağrı
    /// yerinde elle yazılıyordu.
    /// </summary>
    public static class Yuzde
    {
        /// <summary>
        /// İşaretli yüzde. İşaretin <b>yeri kültüre göre değişir</b> ve elle <c>"%"</c>
        /// eklemek Türkçeyi bozar: ölçüm (<c>.calisma/yuzde-olcu</c>) tr-TR'de
        /// <c>%62,5</c>, en-US'te <c>62.5%</c>, de-DE'de <c>62,5 %</c> yazdığını gösterdi.
        /// Bu yüzden sayı <c>0.#</c> ile yazılıp kültürün kendi yüzde deseniyle
        /// birleştirilir; <c>P1</c> yeri doğru koyar ama sondaki sıfırı atamaz
        /// (<c>50</c> yerine <c>50,0</c>).
        /// </summary>
        public static string Isaretli(double oran, CultureInfo kultur) =>
            DeseneKoy(Orandan(oran, kultur), kultur);

        /// <summary>
        /// İşaretsiz yüzde, oran girdisiyle. Katalog dizgisi işareti kendi taşıyorsa
        /// (<c>"%{0}"</c>) sayı buradan gelir.
        /// </summary>
        public static string Orandan(double oran, CultureInfo kultur) =>
            (oran * 100).ToString("0.#", kultur);

        /// <summary>
        /// Değer zaten 0-100 aralığındaysa (<c>ScalePercent</c>, <c>OverPercent</c>)
        /// ikinci bir çarpım yapılmaz — çağrı yerlerinin yarısı oran, yarısı yüzde
        /// taşıyordu ve ayrım hiçbir yerde yazılı değildi.
        /// </summary>
        public static string Hazir(double yuzde, CultureInfo kultur) =>
            yuzde.ToString("0.#", kultur);

        /// <summary>Tam sayı yüzde (panel ölçeği): ondalık burada gürültü.</summary>
        public static string Tam(double oran, CultureInfo kultur) =>
            (oran * 100).ToString("0", kultur);

        /// <summary>
        /// Zaten yüzde taşıyan tam sayı değer (ses düzeyi). <see cref="Tam"/> ile
        /// karıştırılmasın diye ayrı ad: biri oranı yüzle çarpar, bu çarpmaz.
        /// </summary>
        public static string HazirTam(double yuzde, CultureInfo kultur) =>
            yuzde.ToString("0", kultur);

        private static string DeseneKoy(string sayi, CultureInfo kultur)
        {
            var isaret = kultur.NumberFormat.PercentSymbol;
            return kultur.NumberFormat.PercentPositivePattern switch
            {
                0 => sayi + " " + isaret,
                2 => isaret + sayi,
                3 => isaret + " " + sayi,
                _ => sayi + isaret,
            };
        }
    }

    /// <summary>
    /// Hedef boyut menü etiketi: 1000'in tam katları GB, diğerleri MB. Aynı gövde
    /// <c>ShellMenu.TargetLabel</c> ve <c>ShellIntegration.FormatQuickShrinkLabel</c>
    /// olarak iki kez yazılmıştı; biri kültür veriyordu, diğeri vermiyordu.
    /// </summary>
    public static string HedefEtiketi(int mb) =>
        mb >= 1000 && mb % 1000 == 0
            ? (mb / 1000).ToString("0", CultureInfo.InvariantCulture) + " GB"
            : mb.ToString("0", CultureInfo.InvariantCulture) + " MB";

    /// <summary>
    /// Bit hızı. Ondalığı hiç anlam taşımıyor, birim eki katalogdan gelir.
    ///
    /// <b>Kültür almaz, çünkü alamaz.</b> Ailenin geri kalanı kültür parametresi taşıyor;
    /// bu yüzey taşımıyor ve bu bir eksiklik değil, ölçülmüş bir karar: basamak ayracı
    /// ve ondalık olmayan bir tam sayıda <c>"0"</c> biçimi .NET'in bildiği
    /// <b>her</b> kültürde aynı yazımı veriyor (ölçüm <c>.calisma/bithizi-olcu</c>:
    /// 1500/18800/192 üç değeri tüm özgül kültürlerde tek yazım). Kültür parametresi
    /// tutulsaydı hiçbir mutasyonun kıramayacağı — yani hiçbir zaman pimlenemeyecek —
    /// bir söz olurdu. Ondalık gerektiren bir birim (Mbps) eklenirse kültür onunla gelir.
    /// </summary>
    public static class BitHizi
    {
        /// <summary>Kilobit/saniye değeri. Sayı burada, birim adı <c>Strings</c>'te.</summary>
        public static string Kbps(int kbps) =>
            kbps.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Bit/saniye'den kilobit'e. Bölme üç yerde tamsayı, bir yerde <c>double</c>
        /// yapılıyordu; aynı akış iki farklı sayı gösterebiliyordu.
        /// </summary>
        public static string BpsToKbps(long bps) =>
            Kbps((int)Math.Round(bps / 1000.0));
    }

    /// <summary>
    /// Çözünürlük. İki yazım vardı (<c>1920x1080</c> ve <c>1920×1080</c>); çarpı işareti
    /// doğru tipografik seçim ve oynatıcı zaten onu kullanıyor. Kültür almaz: boyutlar
    /// tam sayı, basamak ayracı istemiyorlar.
    /// </summary>
    /// <summary>
    /// Oynatma hızı çarpanı (<c>1,25×</c>). İki ondalık, çünkü adım 0,05.
    /// </summary>
    public static string Kat(double kat, CultureInfo kultur) =>
        kat.ToString("0.##", kultur);

    public static string Cozunurluk(int genislik, int yukseklik) =>
        genislik.ToString(CultureInfo.InvariantCulture)
        + "×" + yukseklik.ToString(CultureInfo.InvariantCulture);

    /// <summary>Kare hızı. Baskın yazım <c>0.##</c>; oynatıcıdaki <c>0.###</c> tek aykırıydı.</summary>
    public static string Kare(double fps, CultureInfo kultur) =>
        fps.ToString("0.##", kultur);

    /// <summary>
    /// Kullanıcıya gösterilen tarih damgası. Üç dosyada aynı dizgi elle kopyalanmıştı;
    /// biçim aynıydı ama biri kültürü başka kaynaktan alıyordu.
    /// </summary>
    public static string Damga(DateTimeOffset an, CultureInfo kultur) =>
        an.ToLocalTime().ToString("d MMMM HH:mm", kultur);

    /// <summary>
    /// Dosya adına giren damga. Kültür <b>alamaz</b>: yerel ay adı ve iki nokta dosya
    /// adını bozar, Windows iki noktayı hiç kabul etmez.
    /// </summary>
    public static string DosyaDamgasi(DateTimeOffset an) =>
        an.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);

    public static class Satir
    {
        public const char BolunmezBosluk = ' ';
        public const char Baglac = '⁠';
        private const int KisaParantez = 14;
        private const int KisaBirim = 7;
        private const int KisaEgikParca = 8;
        private const int KisaSonSozcuk = 12;

        public static string Bagla(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return metin ?? string.Empty;
            var c = metin.ToCharArray();
            for (var i = 1; i < c.Length - 1; i++)
            {
                if (c[i] != ' ') continue;
                if ((char.IsDigit(c[i - 1]) || (c[i - 1] == SoldanSagaIsareti && i > 1 && char.IsDigit(c[i - 2]))) && BirimMi(metin, i + 1)) c[i] = BolunmezBosluk;
                else if (c[i - 1] == '×' || (c[i + 1] == '×' && char.IsDigit(c[i - 1]))) c[i] = BolunmezBosluk;
            }

            for (var i = 0; i < c.Length; i++)
            {
                if (c[i] != '(') continue;
                var kapanis = metin.IndexOf(')', i + 1);
                if (kapanis < 0 || kapanis - i - 1 > KisaParantez) continue;
                for (var j = i + 1; j < kapanis; j++)
                    if (c[j] == ' ') c[j] = BolunmezBosluk;
            }

            var sonuc = new StringBuilder(c.Length + 4);
            for (var i = 0; i < c.Length; i++)
            {
                sonuc.Append(c[i]);
                if (i == 0 || i + 1 >= c.Length || char.IsWhiteSpace(c[i - 1]) || char.IsWhiteSpace(c[i + 1])) continue;
                if (c[i] == '–') sonuc.Append(Baglac);
                else if (c[i] == '/' && char.IsLetter(c[i - 1]) && char.IsLetter(c[i + 1]) && ParcaUzunlugu(c, i) <= KisaEgikParca)
                    sonuc.Append(Baglac);
            }

            return sonuc.ToString();
        }

        public static string SonuBagla(string? metin)
        {
            var bagli = Bagla(metin);
            var son = bagli.LastIndexOf(' ');
            if (son <= 0 || bagli.IndexOf(' ') == son || bagli.Length - son - 1 > KisaSonSozcuk) return bagli;
            return string.Concat(bagli.AsSpan(0, son), BolunmezBosluk.ToString(), bagli.AsSpan(son + 1));
        }

        public const char SoldanSagaIsareti = '\u200E';

        public static string Yalit(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return string.Empty;
            if (metin.IndexOf(SoldanSagaIsareti) >= 0) metin = metin.Replace(SoldanSagaIsareti.ToString(), string.Empty);
            if (!metin.Any(SagdanSolaMi))
                return metin.Any(c => !char.IsWhiteSpace(c)) ? SoldanSagaIsareti + metin + SoldanSagaIsareti : metin;

            var sonuc = new StringBuilder(metin.Length + 8);
            var satirBasi = 0;
            for (var i = 0; i <= metin.Length; i++)
            {
                if (i < metin.Length && metin[i] is not '\n' and not '\r') continue;
                SatiriYalit(metin, satirBasi, i, sonuc);
                if (i < metin.Length) sonuc.Append(metin[i]);
                satirBasi = i + 1;
            }

            return sonuc.ToString();
        }

        private static void SatiriYalit(string metin, int bas, int son, StringBuilder sonuc)
        {
            var parcalar = new List<(int Bas, int Son, bool Sol)>();
            var i = bas;
            while (i < son)
            {
                if (char.IsWhiteSpace(metin[i])) { i++; continue; }
                var j = i;
                var sagdanSola = false;
                while (j < son && !char.IsWhiteSpace(metin[j])) sagdanSola |= SagdanSolaMi(metin[j++]);
                parcalar.Add((i, j, !sagdanSola));
                i = j;
            }

            var yazilan = bas;
            var k = 0;
            while (k < parcalar.Count)
            {
                if (!parcalar[k].Sol) { k++; continue; }
                var ilk = k;
                while (k < parcalar.Count && parcalar[k].Sol) k++;
                var sonParca = k - 1;
                var cekirdekIlk = ilk;
                while (cekirdekIlk <= sonParca && !HarfYaDaRakam(metin, parcalar[cekirdekIlk])) cekirdekIlk++;
                var cekirdekSon = sonParca;
                while (cekirdekSon >= cekirdekIlk && !HarfYaDaRakam(metin, parcalar[cekirdekSon])) cekirdekSon--;

                var cekirdekVar = cekirdekIlk <= cekirdekSon;
                OklariCevir(metin, parcalar, ilk, cekirdekVar ? cekirdekIlk - 1 : sonParca, sonuc, ref yazilan);
                if (!cekirdekVar) continue;
                var cekirdekBas = parcalar[cekirdekIlk].Bas;
                var cekirdekBitis = parcalar[cekirdekSon].Son;
                while (cekirdekBitis > cekirdekBas + 1 && CumleNoktalamasi(metin[cekirdekBitis - 1])) cekirdekBitis--;
                sonuc.Append(metin, yazilan, cekirdekBas - yazilan)
                    .Append(SoldanSagaIsareti)
                    .Append(metin, cekirdekBas, cekirdekBitis - cekirdekBas)
                    .Append(SoldanSagaIsareti);
                yazilan = cekirdekBitis;
                OklariCevir(metin, parcalar, cekirdekSon + 1, sonParca, sonuc, ref yazilan);
            }

            sonuc.Append(metin, yazilan, son - yazilan);
        }

        private static void OklariCevir(string metin, List<(int Bas, int Son, bool Sol)> parcalar, int ilk, int son, StringBuilder sonuc, ref int yazilan)
        {
            for (var p = ilk; p <= son; p++)
            {
                if (!metin.AsSpan(parcalar[p].Bas, parcalar[p].Son - parcalar[p].Bas).SequenceEqual("→")) continue;
                sonuc.Append(metin, yazilan, parcalar[p].Bas - yazilan).Append('←');
                yazilan = parcalar[p].Son;
            }
        }

        private static bool HarfYaDaRakam(string metin, (int Bas, int Son, bool Sol) parca)
        {
            for (var i = parca.Bas; i < parca.Son; i++)
                if (char.IsLetterOrDigit(metin[i])) return true;
            return false;
        }

        private static bool CumleNoktalamasi(char c) => c is '.' or ',' or ';' or ':' or '!' or '?' or '،' or '؛';

        private static bool SagdanSolaMi(char c) =>
            c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFC';

        private static bool BirimMi(string metin, int bas)
        {
            var son = bas;
            while (son < metin.Length && !char.IsWhiteSpace(metin[son])) son++;
            while (son > bas && char.IsPunctuation(metin[son - 1])) son--;
            if (son == bas || son - bas > KisaBirim || !char.IsLetter(metin[bas])) return false;
            for (var i = bas; i < son; i++)
                if (!char.IsLetter(metin[i]) && metin[i] is not '/' and not '.') return false;
            return true;
        }

        private static int ParcaUzunlugu(char[] c, int i)
        {
            var bas = i;
            while (bas > 0 && char.IsLetter(c[bas - 1])) bas--;
            var son = i + 1;
            while (son < c.Length && char.IsLetter(c[son])) son++;
            return son - bas;
        }
    }

    /// <summary>
    /// Motorun İngilizce tanı metinleri. Çevrilmiyorlar; kültüre göre virgül alırlarsa
    /// iki koşumun günlüğü karşılaştırılamaz hale gelir, o yüzden burada kültür
    /// <see cref="CultureInfo.InvariantCulture"/> olarak sabittir.
    /// </summary>
    public static class Tani
    {
        /// <inheritdoc cref="Boyut.Mb(double, CultureInfo)"/>
        public static string Mb(double mb) => Boyut.Mb(mb, CultureInfo.InvariantCulture);

        /// <inheritdoc cref="Boyut.Hedef(double, CultureInfo)"/>
        public static string Hedef(double mb) => Boyut.Hedef(mb, CultureInfo.InvariantCulture);

        /// <inheritdoc cref="Boyut.Sapma(double, CultureInfo)"/>
        public static string Sapma(double mb) => Boyut.Sapma(mb, CultureInfo.InvariantCulture);

        /// <inheritdoc cref="Bicim.Yuzde.Orandan(double, CultureInfo)"/>
        public static string Yuzde(double oran) => Bicim.Yuzde.Orandan(oran, CultureInfo.InvariantCulture);

        /// <inheritdoc cref="Bicim.Kare(double, CultureInfo)"/>
        public static string Kare(double fps) => Bicim.Kare(fps, CultureInfo.InvariantCulture);
    }
}
