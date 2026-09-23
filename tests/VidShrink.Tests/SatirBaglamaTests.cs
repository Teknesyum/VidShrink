using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SatirBaglamaTests
{
    [Theory]
    [InlineData("16,00 MB.", "16,00\u00A0MB.")]
    [InlineData("(15,2 MB)", "(15,2\u00A0MB)")]
    [InlineData("16.00 MB।", "16.00\u00A0MB।")]
    [InlineData("Ses bit hızı (kbit/sn)", "Ses bit hızı (kbit/\u2060sn)")]
    [InlineData("Ses yüksekliğini eşitle (EBU R128)", "Ses yüksekliğini eşitle (EBU\u00A0R128)")]
    [InlineData("kesit 2:17,2–3:07,5", "kesit 2:17,2–⁠3:07,5")]
    [InlineData("1920 × 1080", "1920\u00A0×\u00A01080")]
    [InlineData("Özel genişlik × yükseklik", "Özel genişlik ×\u00A0yükseklik")]
    public void SayiBirimVeAralikBaglanir(string girdi, string beklenen)
        => Assert.Equal(beklenen, Bicim.Satir.Bagla(girdi));

    [Theory]
    [InlineData("Kuyruk sürüyor ve sıradaki dosya bekliyor")]
    [InlineData("00:30 লেগেছে")]
    [InlineData("C:/Kullanicilar/uzun-klasor/video.mp4")]
    [InlineData("Uzun bir cümle (içinde parantezle uzun bir açıklama var)")]
    [InlineData("İki – ayrı yan")]
    [InlineData("2 dosyasından birincisi")]
    public void CumleDokunulmadanKalir(string girdi)
        => Assert.Equal(girdi, Bicim.Satir.Bagla(girdi));

    [Theory]
    [InlineData("Bir medya dosyasını buraya bırakın", "Bir medya dosyasını buraya bırakın")]
    [InlineData("Drop a media file here", "Drop a media file here")]
    public void SonSozcukOncekineBaglanir(string girdi, string beklenen)
        => Assert.Equal(beklenen, Bicim.Satir.SonuBagla(girdi));

    [Theory]
    [InlineData("Mediendatei ablegen")]
    [InlineData("Tek")]
    [InlineData("Dosyayı buraya olağanüstüdüzenlemeler")]
    public void SonSozcukUzunsaYaIkiSozcuksaBaglanmaz(string girdi)
        => Assert.Equal(girdi, Bicim.Satir.SonuBagla(girdi));

    [Theory]
    [InlineData("الدقة 1228×690", "الدقة \u200E1228×690\u200E")]
    [InlineData("الحجم 16,00 MB.", "الحجم \u200E16,00 MB\u200E.")]
    [InlineData("المدة 3:07.5 → 2:17.2 ثانية", "المدة \u200E3:07.5 → 2:17.2\u200E ثانية")]
    [InlineData("420.0 م.ب → 15.2 م.ب", "\u200E420.0\u200E م.ب ← \u200E15.2\u200E م.ب")]
    [InlineData("اختر (100 MB, 250 MB, 1 GB) للهدف", "اختر \u200E(100 MB, 250 MB, 1 GB)\u200E للهدف")]
    [InlineData("1440 × 2560", "\u200E1440 × 2560\u200E")]
    [InlineData("Ctrl+<", "\u200ECtrl+<\u200E")]
    [InlineData("]", "\u200E]\u200E")]
    [InlineData("الحجم 16 MB →", "الحجم \u200E16 MB\u200E ←")]
    public void SoldanSagaAdaYalitilir(string girdi, string beklenen)
        => Assert.Equal(beklenen, Bicim.Satir.Yalit(girdi));

    [Theory]
    [InlineData("مرحبا بالعالم")]
    [InlineData("مرحبا · بالعالم")]
    [InlineData("  ")]
    [InlineData("")]
    public void YalitilacakAdaYoksaDokunulmaz(string girdi)
        => Assert.Equal(girdi, Bicim.Satir.Yalit(girdi));

    [Fact]
    public void YalitimIkinciKezUygulanmaz()
    {
        var bir = Bicim.Satir.Yalit("الدقة 1228×690");
        Assert.Equal(bir, Bicim.Satir.Yalit(bir));
    }

    [Fact]
    public void YalitilmisMetneEklenenParcaDaYalitilir()
        => Assert.Equal(Bicim.Satir.Yalit("تم 420.0 م.ب وصل HDR10+ إلى"),
            Bicim.Satir.Yalit(Bicim.Satir.Yalit("تم 420.0 م.ب") + " وصل HDR10+ إلى"));

    [Fact]
    public void YalitimSatirSonundaKapanir()
        => Assert.Equal("أ 12 px\u200E\nب".Replace("12", "\u200E12"), Bicim.Satir.Yalit("أ 12 px\nب"));

    [Fact]
    public void BosVeNullBosDoner()
    {
        Assert.Equal("", Bicim.Satir.Bagla(null));
        Assert.Equal("", Bicim.Satir.Bagla(""));
    }
}
