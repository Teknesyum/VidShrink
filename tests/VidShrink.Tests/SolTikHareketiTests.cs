using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class SolTikHareketiTests
{
    private const double Tutma = 0.5;
    private const double Cift = 0.4;

    private static SolTikHareketi Yeni() => new(new ClickArbiter(), Tutma, Cift);

    private static SolTikSonucu Tik(SolTikHareketi h, double an, double x = 10, double y = 10, double sure = 0.08)
    {
        h.Bas(x, y, an);
        Assert.False(h.Zaman(an + sure));
        return h.Birak(an + sure);
    }

    [Fact]
    public void TekTikBirakistaTikOlur()
    {
        var h = Yeni();
        h.Bas(10, 10, 1);
        Assert.False(h.Zaman(1.1));
        Assert.Equal(SolTikSonucu.Tik, h.Birak(1.1));
    }

    [Fact]
    public void SureIcindekiIkinciTikCiftTikOlurUcuncusuYeniTik()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1));
        Assert.Equal(SolTikSonucu.CiftTik, Tik(h, 1 + Cift - 0.01));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1 + Cift + 0.05));
        Assert.Equal(SolTikSonucu.CiftTik, Tik(h, 1 + Cift + 0.25));
    }

    [Fact]
    public void SureyiAsanIkinciTikTekTikKalir()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1 + Cift + 0.01));
    }

    [Fact]
    public void UzaktakiIkinciTikCiftSayilmaz()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1, 10, 10));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1.1, 10 + ClickArbiter.DragThresholdDip, 10));
    }

    [Fact]
    public void EsigiAsanBasisTutmaOlurBirakisTikSayilmaz()
    {
        var h = Yeni();
        h.Bas(10, 10, 1);
        Assert.False(h.Zaman(1 + Tutma - 0.01));
        Assert.False(h.Tutuyor);
        Assert.True(h.Zaman(1 + Tutma));
        Assert.True(h.Tutuyor);
        Assert.False(h.Zaman(1 + Tutma + 0.1));
        Assert.False(h.Oyna(10 + 3 * ClickArbiter.DragThresholdDip, 10));
        Assert.Equal(SolTikSonucu.TutmaBitti, h.Birak(2));
        Assert.False(h.Tutuyor);
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 2.05));
    }

    [Fact]
    public void TutmadanSonrakiHizliTikCiftSayilmaz()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1));
        h.Bas(10, 10, 1.2);
        Assert.True(h.Zaman(1.2 + Tutma));
        Assert.Equal(SolTikSonucu.TutmaBitti, h.Birak(1.9));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1.95));
    }

    [Fact]
    public void SaatGecikseDeUzunBasisTikSayilmaz()
    {
        var h = Yeni();
        h.Bas(10, 10, 1);
        Assert.Equal(SolTikSonucu.TutmaBitti, h.Birak(1 + Tutma + 0.2));
    }

    [Fact]
    public void SuruklemeTutmayiVeTikiYutar()
    {
        var h = Yeni();
        h.Bas(100, 100, 1);
        Assert.False(h.Oyna(100 + ClickArbiter.DragThresholdDip - 1, 100));
        Assert.True(h.Oyna(100 + ClickArbiter.DragThresholdDip, 100));
        Assert.False(h.Zaman(1 + Tutma * 3));
        Assert.False(h.Tutuyor);
        Assert.Equal(SolTikSonucu.Surukleme, h.Birak(1 + Tutma * 3));
    }

    [Fact]
    public void SuruklemeIkinciBasistaCiftTikiBozar()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1));
        h.Bas(10, 10, 1.1);
        Assert.True(h.Oyna(10 + ClickArbiter.DragThresholdDip, 10));
        Assert.Equal(SolTikSonucu.Surukleme, h.Birak(1.2));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1.25));
    }

    [Fact]
    public void IptalTutmayiBitirirSonrakiTikTekKalir()
    {
        var h = Yeni();
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1));
        h.Bas(10, 10, 1.1);
        Assert.True(h.Zaman(1.1 + Tutma));
        Assert.True(h.Iptal());
        Assert.Equal(SolTikSonucu.Yok, h.Birak(1.8));
        Assert.Equal(SolTikSonucu.Tik, Tik(h, 1.85));
    }

    [Fact]
    public void KalanSureTutmaEsigineSayar()
    {
        var h = Yeni();
        h.Bas(10, 10, 1);
        Assert.Equal(Tutma - 0.2, h.Kalan(1.2), 9);
        Assert.Equal(0, h.Kalan(1 + Tutma + 1));
    }
}
