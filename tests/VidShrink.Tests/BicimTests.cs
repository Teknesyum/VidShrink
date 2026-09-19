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
    public void YuzdeOranAlirCarpimiKendiYapar()
    {
        Assert.Equal("62,5%", Bicim.Yuzde(0.625, Tr));
        Assert.Equal("62.5%", Bicim.Yuzde(0.625, En));
        Assert.Equal("50%", Bicim.Yuzde(0.5, Tr));
    }

    [Fact]
    public void KbpsOndalikYazmaz()
    {
        Assert.Equal("1500", Bicim.BitHizi.Kbps(1500, Tr));
        Assert.Equal("1500", Bicim.BitHizi.BpsToKbps(1_500_000, Tr));
        Assert.Equal("1500", Bicim.BitHizi.BpsToKbps(1_499_600, Tr));
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

    [Fact]
    public void TaniKulturdenEtkilenmez()
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = Tr;
            Assert.Equal("12.3", Bicim.Tani.Mb(12.34));
            Assert.Equal("62.5%", Bicim.Tani.Yuzde(0.625));
            Assert.Equal("23.98", Bicim.Tani.Kare(23.976));
        }
        finally
        {
            CultureInfo.CurrentCulture = onceki;
        }
    }
}
