using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Önizleme şeridinin kodlama imleci gerçek kodlamaya bağlı: yalnız demo saatinden
/// besleniyordu. İmleç geçiş içindeki yeri gösterir, etiket geçişi ve denemeyi söyler,
/// kodlama bitince ikisi de kalkar.
/// </summary>
public sealed class KodlamaImleciTests
{
    private static EncodeProgress Ilerleme(double kesir, string asama) => new(kesir, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20), 3.2, asama);

    [Theory]
    [InlineData(0.25, "pass 1/2 (attempt 1)", 0.5, 1, 2, 1)]
    [InlineData(0.75, "pass 2/2 (attempt 3)", 0.5, 2, 2, 3)]
    [InlineData(1.0, "pass 2/2 (attempt 1)", 1.0, 2, 2, 1)]
    [InlineData(0.4, "encoding (attempt 2)", 0.4, 1, 1, 2)]
    public void AsamaGecisIciYereCevrilir(double kesir, string asama, double yer, int gecis, int gecisSayisi, int deneme)
    {
        var imlec = MainWindow.EncodeMarkerOf(Ilerleme(kesir, asama));

        Assert.NotNull(imlec);
        Assert.Equal(yer, imlec.Value.Fraction, 6);
        Assert.Equal((gecis, gecisSayisi, deneme), (imlec.Value.Pass, imlec.Value.PassCount, imlec.Value.Attempt));
    }

    [Theory]
    [InlineData("scene detection")]
    [InlineData("pass 3/2 (attempt 1)")]
    [InlineData("")]
    public void TanimadigiAsamadaImlecYok(string asama) => Assert.Null(MainWindow.EncodeMarkerOf(Ilerleme(0.5, asama)));

    /// <summary>Motorun yazdığı biçim geri okunur; yazan ve okuyan aynı kayıt.</summary>
    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(2, 2, 7)]
    [InlineData(1, 1, 3)]
    public void AsamaYazilipOkunur(int gecis, int gecisSayisi, int deneme)
    {
        var asama = new EncodeStage(gecis, gecisSayisi, deneme);
        Assert.Equal(asama, EncodeStage.Parse(asama.ToString()));
    }

    [Fact]
    public void SeritKodlamayiGosterirVeBitinceKaldirir()
    {
        var (gorunur, kesir, etiket, beklenen, sonra) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            try
            {
                var serit = pencere.Preview.Controls;
                serit.StopDemoClock();
                serit.ClearEncode();
                pencere.ShowEncodeProgressForTest(Ilerleme(0.75, "pass 2/2 (attempt 3)"));
                var g = serit.EncodeChip.IsVisible;
                var k = serit.EncodeFraction;
                var e = serit.EncodeText.Text;
                pencere.EndEncodeForTest();
                return (g, k, e, LanguageCatalog.EncodeMarker("en", 2, 2, 3), serit.EncodeChip.IsVisible);
            }
            finally { pencere.Close(); }
        });

        Assert.True(gorunur);
        Assert.Equal(0.5, kesir, 6);
        Assert.Equal(beklenen, etiket);
        Assert.False(sonra);
    }
}
