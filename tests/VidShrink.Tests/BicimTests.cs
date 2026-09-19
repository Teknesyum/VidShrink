using System;
using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Biçim gövdesinin ölçüsü. Tarama altı ailede 32 ayrı biçim saydı; bu süit her yüzeyin
/// ondalığını, kültür dikişini ve birim adını ayrı ayrı pimler. Kültür ayrı ayrı
/// pimleniyor çünkü <c>Saat</c>'te <see cref="CultureInfo.InvariantCulture"/> mutasyonu
/// 21 yeşil testin hiçbirini kırmamıştı — ondalık ayracı sınanmadan kültür pimli sayılmaz.
/// </summary>
public sealed class BicimTests
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void BoyutMbTekOndalik()
    {
        Assert.Equal("12,3", Bicim.Boyut.Mb(12.34, Tr));
        Assert.Equal("12.3", Bicim.Boyut.Mb(12.34, En));
        Assert.Equal("50,0", Bicim.Boyut.Mb(50, Tr));
    }

    [Fact]
    public void HedefGereksizSifirYazmaz()
    {
        Assert.Equal("50", Bicim.Boyut.Hedef(50, Tr));
        Assert.Equal("49,75", Bicim.Boyut.Hedef(49.75, Tr));
        Assert.Equal("49.75", Bicim.Boyut.Hedef(49.75, En));
    }

    [Fact]
    public void SapmaIkiOndalikTutar()
    {
        Assert.Equal("0,04", Bicim.Boyut.Sapma(0.04, Tr));
        Assert.Equal("0.04", Bicim.Boyut.Sapma(0.04, En));
        Assert.NotEqual(Bicim.Boyut.Mb(0.04, Tr), Bicim.Boyut.Sapma(0.04, Tr));
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1 KiB")]
    [InlineData(1536L, "1,5 KiB")]
    [InlineData(1048576L, "1 MiB")]
    [InlineData(1073741824L, "1 GiB")]
    [InlineData(1099511627776L, "1 TiB")]
    public void BaytIkilikBirimAdiYazar(long bayt, string beklenen) =>
        Assert.Equal(beklenen, Bicim.Boyut.Bayt(bayt, Tr));

    [Fact]
    public void BaytOndalikBirimAdiYazmaz()
    {
        var yazi = Bicim.Boyut.Bayt(1048576, Tr);
        Assert.Contains("MiB", yazi);
        Assert.DoesNotContain("MB", yazi);
    }

    [Fact]
    public void BaytKulturuKullanir() =>
        Assert.Equal("1.5 KiB", Bicim.Boyut.Bayt(1536, En));

    [Fact]
    public void BaytIkiOndalikTavani() =>
        Assert.Equal("1,18 MiB", Bicim.Boyut.Bayt(1_234_567, Tr));

    [Theory]
    [InlineData(50, "50 MB")]
    [InlineData(1023, "1023 MB")]
    [InlineData(1024, "1 GB")]
    [InlineData(2048, "2 GB")]
    [InlineData(1536, "1536 MB")]
    public void HedefEtiketiTamKatlariGbYazar(int mb, string beklenen) =>
        Assert.Equal(beklenen, Bicim.HedefEtiketi(mb));

    [Fact]
    public void YuzdeOranAlirCarpimiKendiYapar()
    {
        Assert.Equal("62,5", Bicim.Yuzde.Orandan(0.625, Tr));
        Assert.Equal("62.5", Bicim.Yuzde.Orandan(0.625, En));
        Assert.Equal("50", Bicim.Yuzde.Orandan(0.5, Tr));
    }

    /// <summary>
    /// Ondalik tavani: yuzde olceginde ikinci ondalik gurultu. Tek ondalikli degerler
    /// bunu goremiyordu — <c>0.#</c> ile <c>0.##</c> ayni sonucu veriyordu.
    /// </summary>
    [Fact]
    public void YuzdeTekOndalikTavaniTutar()
    {
        Assert.Equal("12,3", Bicim.Yuzde.Orandan(0.12345, Tr));
        Assert.Equal("12.3", Bicim.Yuzde.Hazir(12.345, En));
    }

    /// <summary>
    /// İşaretin yeri kültüre göre değişiyor: Türkçede başta, İngilizcede sonda, Almancada
    /// sonda ve bölünmez boşlukla. Elle <c>"%"</c> eklemek uygulamanın kendi dilini bozar
    /// — ölçüm <c>.calisma/yuzde-olcu</c>.
    /// </summary>
    [Fact]
    public void IsaretliYuzdeKulturunYerineUyar()
    {
        Assert.Equal("%62,5", Bicim.Yuzde.Isaretli(0.625, Tr));
        Assert.Equal("62.5%", Bicim.Yuzde.Isaretli(0.625, En));
        Assert.Equal("62,5 %", Bicim.Yuzde.Isaretli(0.625, CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("%50", Bicim.Yuzde.Isaretli(0.5, Tr));
    }

    /// <summary>
    /// İşaretin <b>kendisi</b> de kültürden gelir, elle <c>"%"</c> yazılmaz: ölçüm
    /// (<c>.calisma/yuzde-olcu</c>) Farsçanın <c>٪</c> (U+066A) kullandığını gösterdi.
    /// Türkçe/İngilizce/Almanca üçü de <c>%</c> olduğu için bu kol olmadan işareti
    /// sabitlemek hiçbir ölçüyü kırmıyordu.
    /// </summary>
    [Fact]
    public void YuzdeIsaretiKulturden()
    {
        var yazi = Bicim.Yuzde.Isaretli(0.5, CultureInfo.GetCultureInfo("fa-IR"));

        Assert.Contains("٪", yazi);
        Assert.DoesNotContain("%", yazi);
    }

    [Fact]
    public void HazirYuzdeIkinciKezCarpmaz()
    {
        Assert.Equal("62,5", Bicim.Yuzde.Hazir(62.5, Tr));
        Assert.Equal("12", Bicim.Yuzde.Hazir(12, Tr));
    }

    /// <summary>
    /// Tam yuzde ondalik yazmaz. Yarim degerde .NET'in <c>"0"</c> bicimi <b>sifirdan uzaga</b>
    /// yuvarliyor (bankacinin yuvarlamasi degil): 62,5 → 63. Beklenti olculdu, varsayilmadi.
    /// </summary>
    [Fact]
    public void TamYuzdeOndalikYazmaz()
    {
        Assert.Equal("100", Bicim.Yuzde.Tam(1.0, Tr));
        Assert.Equal("175", Bicim.Yuzde.Tam(1.75, Tr));
        Assert.Equal("63", Bicim.Yuzde.Tam(0.625, Tr));
        Assert.DoesNotContain(",", Bicim.Yuzde.Tam(0.625, Tr));
    }

    [Fact]
    public void KbpsOndalikYazmaz()
    {
        Assert.Equal("1500", Bicim.BitHizi.Kbps(1500));
        Assert.Equal("1500", Bicim.BitHizi.BpsToKbps(1_500_000));
        Assert.Equal("1500", Bicim.BitHizi.BpsToKbps(1_499_600));
        Assert.Equal("1499", Bicim.BitHizi.BpsToKbps(1_499_400));
        Assert.DoesNotContain(".", Bicim.BitHizi.Kbps(200_000));
        Assert.DoesNotContain(",", Bicim.BitHizi.Kbps(200_000));
    }

    [Fact]
    public void CozunurlukCarpiIsaretiKullanir()
    {
        Assert.Equal("1920×1080", Bicim.Cozunurluk(1920, 1080));
        Assert.DoesNotContain("x", Bicim.Cozunurluk(1920, 1080));
    }

    [Fact]
    public void KareIkiOndalikTavani()
    {
        Assert.Equal("23,98", Bicim.Kare(23.976, Tr));
        Assert.Equal("23.98", Bicim.Kare(23.976, En));
        Assert.Equal("30", Bicim.Kare(30, Tr));
    }

    [Fact]
    public void DosyaDamgasiKulturdenBagimsiz()
    {
        var an = new DateTimeOffset(2026, 9, 19, 14, 5, 3, TimeSpan.Zero);
        Assert.Equal("2026-09-19_14-05-03", Bicim.DosyaDamgasi(an));
        Assert.DoesNotContain(":", Bicim.DosyaDamgasi(an));
    }

    [Fact]
    public void DamgaKulturuKullanir()
    {
        var an = new DateTimeOffset(2026, 9, 19, 14, 5, 0, TimeSpan.Zero);
        Assert.NotEqual(Bicim.Damga(an, Tr), Bicim.Damga(an, En));
    }

    /// <summary>
    /// Gösterilen damga kullanıcının saatini yazar, UTC'yi değil. <c>ToLocalTime</c>
    /// düşürüldüğünde ölçü kırmızı dönsün diye saat farkı makinenin kendi diliminden
    /// hesaplanıyor: sabit bir saat yazılsa ölçü yalnız +03:00'te doğru olurdu.
    /// </summary>
    [Fact]
    public void DamgaYerelSaatiYazar()
    {
        var an = new DateTimeOffset(2026, 9, 19, 14, 5, 0, TimeSpan.Zero);
        var beklenen = TimeZoneInfo.ConvertTime(an, TimeZoneInfo.Local);

        Assert.Equal(beklenen.ToString("d MMMM HH:mm", Tr), Bicim.Damga(an, Tr));
        if (beklenen.Offset != TimeSpan.Zero)
            Assert.NotEqual(an.ToString("d MMMM HH:mm", Tr), Bicim.Damga(an, Tr));
    }

    /// <summary>
    /// Dosya adına giren damga takvimi de sabitler. <c>tr-TR</c> Gregoryen olduğu için
    /// kültürü değiştirmek bu makinede çıktıyı değiştirmiyordu; ölçü bu yüzden takvimi
    /// başka olan <c>th-TH</c> ile koşuyor — orada yıl 2569 yazılırdı.
    /// </summary>
    [Fact]
    public void DosyaDamgasiTakvimiDeSabitler()
    {
        var an = new DateTimeOffset(2026, 9, 19, 14, 5, 3, TimeSpan.Zero);
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
            Assert.Equal("2026-09-19_14-05-03", Bicim.DosyaDamgasi(an));
            Assert.DoesNotContain("2569", Bicim.DosyaDamgasi(an));
        }
        finally { CultureInfo.CurrentCulture = onceki; }
    }

    [Fact]
    public void TaniKulturdenEtkilenmez()
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = Tr;
            Assert.Equal("12.3", Bicim.Tani.Mb(12.34));
            Assert.Equal("62.5", Bicim.Tani.Yuzde(0.625));
            Assert.Equal("23.98", Bicim.Tani.Kare(23.976));
        }
        finally
        {
            CultureInfo.CurrentCulture = onceki;
        }
    }
}
