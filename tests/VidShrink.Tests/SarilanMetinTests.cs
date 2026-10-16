using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VidShrink.App.Yazi;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// <see cref="SarilanMetin"/>: metin ölçtüğü doğal genişliğin yerleşim yuvarlamasından geçmiş
/// hâlinde yerleşince taşıdığı karakter azalmaz: Avalonia sarılan satırı kutunun yüksekliğine kırptığı
/// için kusur satır sayısında değil, düşen karakterde görünür. Kollar 42 dil × 3 boy × 5 ölçekli taramadan (848.670
/// bileşimin 179'u sarılıyordu) kayan nokta artığını taşır: ölçek 1 değilken yuvarlama ölçüyü
/// sekiz haneye kırpıyor (588,0000000000003 → 588) ve metin kendinden dar kutuda yeniden kuruluyor. Olumsuz kontrol: aynı genişlikte düz <see cref="TextBlock"/> karakter düşürür; düşürmezse
/// kol kusuru yeniden üretmiyor, ölçü kördür.
/// </summary>
public sealed class SarilanMetinTests
{
    public static TheoryData<string, string, double, double> Kollar => new()
    {
        { "fi", "Pyydetty laatu on parempi kuin lähde itse: yläraja on {0} MB ja siinä saavutettu laatu on {1}/100.", 14, 1.25 },
        { "fi", "Pyydetty laatu on parempi kuin lähde itse: yläraja on {0} MB ja siinä saavutettu laatu on {1}/100.", 16, 1.5 },
        { "id", "Berkas pengaturan tidak dapat dibaca dan tidak ada cadangan, jadi nilai bawaan dimuat. Berkas yang rusak disimpan di sebelahnya.", 14, 2.0 },
    };

    [Theory]
    [MemberData(nameof(Kollar))]
    public void DogalGenisligindeKarakterDusmez(string dil, string metin, double boy, double olcek)
    {
        var (duz, sarilan, satir) = AppHost.Run(() =>
        {
            Application.Current!.TryFindResource("FontSans", out var aile);
            int Yerlestir(TextBlock t)
            {
                t.Text = metin;
                t.FontFamily = (FontFamily)aile!;
                t.FontSize = boy;
                t.TextWrapping = TextWrapping.Wrap;
                t.Measure(Size.Infinity);
                var genislik = LayoutHelper.RoundLayoutValueUp(t.TextLayout.WidthIncludingTrailingWhitespace, olcek);
                t.Arrange(new Rect(0, 0, genislik, t.DesiredSize.Height));
                return t.TextLayout.TextLines.Sum(l => l.Length);
            }
            var olcu = new TextBlock { Text = metin, FontFamily = (FontFamily)aile!, FontSize = boy, TextWrapping = TextWrapping.Wrap };
            olcu.Measure(Size.Infinity);
            return (Yerlestir(new TextBlock()), Yerlestir(new SarilanMetin()), olcu.TextLayout.TextLines.Sum(l => l.Length));
        });

        Assert.True(duz < satir, $"{dil}: düz TextBlock {duz} karakter taşıyor, doğal {satir}; kusur yeniden üretilmedi, ölçü kör.");
        Assert.Equal(satir, sarilan);
    }
}
