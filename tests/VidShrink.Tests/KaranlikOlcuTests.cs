using Xunit;

namespace VidShrink.Tests;

public class KaranlikOlcuTests
{
    private static byte[] Rampa()
    {
        var kare = new byte[256];
        for (var i = 0; i < kare.Length; i++) kare[i] = (byte)i;
        return kare;
    }

    [Fact]
    public void AyniKareTavanaVeTamTonaCikar()
    {
        var birikim = new KaranlikBirikim();
        var kare = Rampa();
        birikim.Ekle(kare, kare);
        var sonuc = birikim.Sonuc();

        Assert.Equal(1, sonuc.Kare);
        Assert.Equal(KaranlikOlcu.Esik, sonuc.KaranlikPiksel);
        Assert.Equal(KaranlikOlcu.PsnrTavani, sonuc.KaranlikPsnr);
        Assert.Equal(1.0, sonuc.TonOrani);
        Assert.Equal(0.0, sonuc.OrtalamaKayma);
    }

    [Fact]
    public void KirpilmisGolgeTonSayisiniVeKaymayiDusurur()
    {
        var kaynak = Rampa();
        var test = (byte[])kaynak.Clone();
        for (var i = 0; i < KaranlikOlcu.Esik; i++) test[i] = (byte)(i < 32 ? 16 : i);
        var birikim = new KaranlikBirikim();
        birikim.Ekle(kaynak, test);
        var sonuc = birikim.Sonuc();

        Assert.Equal(33.0 / 64.0, sonuc.TonOrani!.Value, 6);
        Assert.True(sonuc.KaranlikPsnr < 35, $"psnr={sonuc.KaranlikPsnr}");
        Assert.NotEqual(0.0, sonuc.OrtalamaKayma);
    }

    [Fact]
    public void AydinlikPikseldekiHataKaranlikOlcuyeGirmez()
    {
        var kaynak = Rampa();
        var test = (byte[])kaynak.Clone();
        for (var i = KaranlikOlcu.Esik; i < test.Length; i++) test[i] = 255;
        var birikim = new KaranlikBirikim();
        birikim.Ekle(kaynak, test);
        var sonuc = birikim.Sonuc();

        Assert.Equal(KaranlikOlcu.PsnrTavani, sonuc.KaranlikPsnr);
        Assert.Equal(1.0, sonuc.TonOrani);
    }

    [Fact]
    public void KaranlikPikselYoksaOlcuBosDoner()
    {
        var kaynak = Enumerable.Repeat((byte)200, 64).ToArray();
        var birikim = new KaranlikBirikim();
        birikim.Ekle(kaynak, kaynak);
        var sonuc = birikim.Sonuc();

        Assert.Equal(0, sonuc.KaranlikPiksel);
        Assert.Null(sonuc.KaranlikPsnr);
        Assert.Null(sonuc.TonOrani);
    }

    [Fact]
    public void KareHiziVerilinceTestKoluKaynakHizinaCekilir()
    {
        var graf = BenchMeasureFilterGraph.Build(1920, 800, "xpsnr", "24");
        Assert.StartsWith("[0:v]fps=24,scale=w=1920:h=800", graf);

        var hizsiz = BenchMeasureFilterGraph.Build(1920, 800, "xpsnr");
        Assert.DoesNotContain("fps=", hizsiz);
    }

    [Fact]
    public void FarkliBoyKareReddedilir()
    {
        var birikim = new KaranlikBirikim();
        Assert.Throws<ArgumentException>(() => birikim.Ekle(new byte[4], new byte[5]));
    }
}
