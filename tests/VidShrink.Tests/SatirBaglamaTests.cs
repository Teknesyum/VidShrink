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

    [Fact]
    public void BosVeNullBosDoner()
    {
        Assert.Equal("", Bicim.Satir.Bagla(null));
        Assert.Equal("", Bicim.Satir.Bagla(""));
    }
}
