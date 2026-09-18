using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Kod borcu 10: süre yazımı on iki çağrı yerinde elle kuruluyordu, üçü
/// <see cref="CultureInfo.InvariantCulture"/> vermiyordu ve "kalan süre" iki yerde bir
/// saati geçince <c>mm:ss</c>'te kalıp yanlış sayı gösteriyordu. Ölçü beş yüzeyin
/// sınırlarını ve ikinci bir gövdenin doğmadığını okur.
/// </summary>
public sealed class SaatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(12, "00:12")]
    [InlineData(59, "00:59")]
    [InlineData(60, "01:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3732, "01:02:12")]
    [InlineData(86399, "23:59:59")]
    public void EkranKendiBuyuklugunuIzliyor(double saniye, string beklenen)
        => Assert.Equal(beklenen, Saat.Ekran(TimeSpan.FromSeconds(saniye)));

    /// <summary>
    /// Biçimi ölçek seçer, değer değil: bir saatlik kaydın şeridinde ilk saniye de
    /// <c>00:00:01</c> yazar, yoksa sayı ilk dakikadan sonra bir hane genişler ve
    /// şeritteki yazı yerinden oynar.
    /// </summary>
    [Fact]
    public void OlcekBicimiSeciyorDegerDegil()
    {
        var olcek = TimeSpan.FromSeconds(3732);

        Assert.Equal("00:00:01", Saat.Ekran(TimeSpan.FromSeconds(1), olcek));
        Assert.Equal("00:00:01", Saat.Ekran(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1)));
        Assert.Equal("00:01", Saat.Ekran(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3599)));
    }

    [Fact]
    public void NegatifDegerSifiraKirpiliyor()
    {
        Assert.Equal("00:00", Saat.Ekran(TimeSpan.FromSeconds(-5)));
        Assert.Equal("00-00-00-000", Saat.DosyaAdi(TimeSpan.FromSeconds(-5)));
        Assert.Equal("00:00:00.000", Saat.Ffmpeg(TimeSpan.FromSeconds(-5)));
    }

    /// <summary>
    /// Kalan süre ölçüm gelmeden önce de bir şey yazmak zorunda; boşluğun yazısı üç
    /// çağrı yerinde ayrı ayrı kuruluydu. Bir saati geçen kalan artık <c>mm:ss</c>'te
    /// sıkışmıyor — eski kol 70 dakikalık kalanı <c>10:00</c> diye gösteriyordu.
    /// </summary>
    [Fact]
    public void KalanBosluguTasiyorVeBirSaatiGeciyor()
    {
        Assert.Equal("-", Saat.Kalan(null));
        Assert.Equal("00:45", Saat.Kalan(TimeSpan.FromSeconds(45)));
        Assert.Equal("01:10:00", Saat.Kalan(TimeSpan.FromMinutes(70)));
        Assert.NotEqual("10:00", Saat.Kalan(TimeSpan.FromMinutes(70)));
    }

    [Fact]
    public void FfmpegBicimiMilisaniyeTasiyor()
    {
        Assert.Equal("00:00:12.345", Saat.Ffmpeg(TimeSpan.FromMilliseconds(12345)));
        Assert.Equal("01:02:03.004", Saat.Ffmpeg(new TimeSpan(0, 1, 2, 3, 4)));
    }

    /// <summary>
    /// Kesit saati ondalık saniye taşır ve baştaki sıfırı yazmaz; aralık yazısı iki yana
    /// yayılmasın diye ekran saatinden ayrı duruyor.
    /// </summary>
    [Theory]
    [InlineData(0, "0:00.0")]
    [InlineData(72.5, "1:12.5")]
    [InlineData(3599.9, "59:59.9")]
    [InlineData(3732.4, "1:02:12.4")]
    public void KesitOndalikSaniyeTasiyor(double saniye, string beklenen)
        => Assert.Equal(beklenen, Saat.Kesit(TimeSpan.FromSeconds(saniye)));

    /// <summary>Dosya adı damgasında iki nokta olamaz: Windows kabul etmiyor.</summary>
    [Fact]
    public void DosyaAdindaIkiNoktaYok()
    {
        var damga = Saat.DosyaAdi(new TimeSpan(0, 1, 2, 3, 4));

        Assert.Equal("01-02-03-004", damga);
        Assert.DoesNotContain(':', damga);
        Assert.Equal(-1, damga.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    /// <summary>
    /// Yazım kültürden bağımsız; ölçü kültürü iş parçacığına gerçekten kurup okur.
    ///
    /// <para><b>Bu kol bir bekçi, ölçülmüş bir kırmızı değil.</b> Mutasyon denendi:
    /// <c>InvariantCulture</c>'ın on üç geçişini <c>CurrentCulture</c> yapmak
    /// 21/21 yeşil bıraktı — .NET tamsayı biçimlerinde yerel rakam şekli kullanmıyor ve
    /// TimeSpan'in kaçışlı ayraçları zaten kültürsüz. Yani buradaki
    /// <c>InvariantCulture</c> bugün yük taşımıyor; kol, ileride kültüre duyarlı bir
    /// biçim (<c>g</c>, sağlayıcıya bağlı ondalık) sızarsa diye duruyor.</para>
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void KulturYazimiKaydirmiyor(string kultur)
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(kultur);

            Assert.Equal("01:02:12", Saat.Ekran(TimeSpan.FromSeconds(3732)));
            Assert.Equal("00:00:12.345", Saat.Ffmpeg(TimeSpan.FromMilliseconds(12345)));
            Assert.Equal("01-02-03-004", Saat.DosyaAdi(new TimeSpan(0, 1, 2, 3, 4)));
        }
        finally { CultureInfo.CurrentCulture = onceki; }
    }

    /// <summary>
    /// İkinci gövde doğmasın: kaynakta elle kurulan <c>mm\:ss</c> yazımı kalmadı.
    /// Tarayıcının kör olmadığı, elle kurulmuş bir örnekle pimli.
    /// </summary>
    [Fact]
    public void IkinciGovdeYok()
    {
        var desen = new Regex(@"ToString\(@""[hm]{1,2}\\:", RegexOptions.CultureInvariant);

        Assert.True(desen.IsMatch("""value.ToString(@"mm\:ss", CultureInfo.InvariantCulture)"""),
            "Tarayıcı kör: kurulmuş örneği bile görmüyor.");

        var kacaklar = Directory
            .EnumerateFiles(Path.Combine(TipSources.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(yol => !yol.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                       && !yol.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                       && !yol.EndsWith("Saat.cs", StringComparison.Ordinal))
            .Where(yol => desen.IsMatch(File.ReadAllText(yol)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(kacaklar);
    }
}
