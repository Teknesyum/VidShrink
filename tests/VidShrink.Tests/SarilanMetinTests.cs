using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.App.Yazi;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// <see cref="SarilanMetin"/>: metin ölçtüğü doğal genişliğin hemen altında yerleşince taşıdığı karakter
/// azalmaz: Avalonia sarılan satırı kutunun yüksekliğine kırptığı için kusur satır sayısında değil, düşen
/// karakterde görünür. Kusurun kaynağı kayan nokta artığı: ölçü 588,0000000000003 çıkıyor, ölçek 1 değilken
/// yuvarlama onu 588'e kırpıyor ve metin kendinden dar kutuda yeniden kuruluyor (42 dil × 3 boy × 5 ölçekli
/// taramada 848.670 bileşimin 179'u).
/// <para>Hangi boyun artık taşıdığı yazı tipine bağlı: bu makinede kusurlu çıkan üç kol CI'nın yazı tipinde
/// kusursuzdu, ölçü kör kaldı. Bu yüzden kollar sabit değil, her makinede taranır: metinler <see cref="Boylar"/>
/// boyunca doğal genişlikten <see cref="Artik"/> kadar dar kurulur, düz <see cref="TextBlock"/>'un karakter
/// düşürdüğü her bileşimde <see cref="SarilanMetin"/> düşürmemeli. Olumsuz kontrol: hiçbir bileşim kusuru
/// üretmezse ölçü kördür ve test düşer.</para>
/// </summary>
public sealed class SarilanMetinTests
{
    private const double Artik = 1e-9;

    private static readonly string[] Metinler =
    {
        "Pyydetty laatu on parempi kuin lähde itse: yläraja on {0} MB ja siinä saavutettu laatu on {1}/100.",
        "Berkas pengaturan tidak dapat dibaca dan tidak ada cadangan, jadi nilai bawaan dimuat. Berkas yang rusak disimpan di sebelahnya.",
        "Ayar dosyası okunamadı ve yedek yok; varsayılanlar yüklendi. Bozuk dosya yanında saklandı.",
        "Die Einstellungsdatei konnte nicht gelesen werden und es gibt keine Sicherung, daher wurden die Standardwerte geladen.",
    };

    private static readonly double[] Boylar = Enumerable.Range(10, 15).Select(b => (double)b).ToArray();

    [Fact]
    public void DogalGenisligindeKarakterDusmez()
    {
        var kollar = AppHost.Run(() =>
        {
            Application.Current!.TryFindResource("FontSans", out var aile);
            int Yerlestir(TextBlock t, string metin, double boy)
            {
                t.Text = metin;
                t.FontFamily = (FontFamily)aile!;
                t.FontSize = boy;
                t.TextWrapping = TextWrapping.Wrap;
                t.Measure(Size.Infinity);
                t.Arrange(new Rect(0, 0, t.TextLayout.WidthIncludingTrailingWhitespace - Artik, t.DesiredSize.Height));
                return t.TextLayout.TextLines.Sum(l => l.Length);
            }

            return Metinler.SelectMany(metin => Boylar.Select(boy => (metin, boy))).Select(k =>
            {
                var olcu = new TextBlock { Text = k.metin, FontFamily = (FontFamily)aile!, FontSize = k.boy, TextWrapping = TextWrapping.Wrap };
                olcu.Measure(Size.Infinity);
                return (Ad: $"{k.metin[..8]}…@{k.boy}", Dogal: olcu.TextLayout.TextLines.Sum(l => l.Length),
                    Duz: Yerlestir(new TextBlock(), k.metin, k.boy), Sarilan: Yerlestir(new SarilanMetin(), k.metin, k.boy));
            }).ToArray();
        });

        var kusurlu = kollar.Where(k => k.Duz < k.Dogal).ToArray();
        Assert.True(kusurlu.Length > 0, $"{kollar.Length} bileşimin hiçbirinde düz TextBlock karakter düşürmedi; kusur yeniden üretilmedi, ölçü kör.");
        Assert.Empty(kusurlu.Where(k => k.Sarilan != k.Dogal).Select(k => $"{k.Ad}: {k.Sarilan}/{k.Dogal}"));
    }
}
