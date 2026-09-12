using System.Text.RegularExpressions;
using Avalonia.Controls;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// Kesit A pimi: programın tek simge takımı <c>Themes/Icons.axaml</c>.
///
/// Üç şey ölçülüyor. Birincisi kapalı küme: biçimlemede geçen her
/// <c>{StaticResource Icon...}</c> başvurusunun karşılığı o dosyada var; olmayan anahtar
/// Avalonia'da sessizce boş <c>Path</c> çiziyor, yani çalışma anında görünmüyor.
/// <c>IconSizeSm</c>/<c>IconStroke</c> gibi ölçü belirteçleri aynı önekle başlıyor; onların
/// kaynağı <c>Theme.axaml</c>, ölçüm iki dosyayı birlikte okuyup yalnız her ikisinde de
/// bulunmayan adı suçluyor.
///
/// İkincisi emoji yasağı. Kullanıcının cümlesi "ses simgemiz bile kötü" idi; şeridin
/// 🔉/🔊 düğmeleri gitti, yerlerine <c>IconVolumeMute</c>/<c>IconVolume</c> geldi. Emoji
/// yazı tipine bağlı, 42 dilin yazı yığınında aynı görünmüyor ve <c>Foreground</c>'dan
/// renk almıyor. Ölçüm yalnız emoji bloklarını yakalıyor; <c>▾</c> <c>▶</c> gibi geometrik
/// şekiller bu ağda değil — onları Kesit B düşürüyor.
///
/// Üçüncüsü Ayarlar sekmesinin yeri: kullanıcı "ayarlar sol tarafın en sağında olsun"
/// dedi, şerit yatay ve sola yaslı, dolayısıyla en sağ = son sekme. Ayrıca görünür
/// olmalı; eskiden <c>IsVisible="False"</c> ile gizliydi ve başlık çubuğundaki ayrı
/// tekerlekten açılıyordu.
/// </summary>
public sealed class SimgeTakimiTests
{
    private static readonly string AppRoot =
        Path.Combine(TipSources.Root, "src", "VidShrink.App");

    private static readonly Regex Anahtar = new(
        @"x:Key=""(Icon[A-Za-z]*)""", RegexOptions.Compiled);

    private static readonly Regex Basvuru = new(
        @"\{StaticResource (Icon[A-Za-z]*)\}", RegexOptions.Compiled);

    private static readonly Regex Emoji = new(
        @"[\uD83C-\uD83E][\uDC00-\uDFFF]|[☀-⛿️]", RegexOptions.Compiled);

    private static IEnumerable<string> Dosyalar(string uzanti) =>
        Directory.EnumerateFiles(AppRoot, "*" + uzanti, SearchOption.AllDirectories)
            .Where(yol => !yol.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                       && !yol.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void HerSimgeBasvurusununKarsiligiTakimda()
    {
        var takim = Anahtar.Matches(File.ReadAllText(Path.Combine(AppRoot, "Themes", "Icons.axaml")))
            .Select(eslesme => eslesme.Groups[1].Value)
            .ToHashSet();

        Assert.True(takim.Count > 0, "Icons.axaml okunamadı ya da hiç anahtar taşımıyor.");

        var olcu = Anahtar.Matches(File.ReadAllText(Path.Combine(AppRoot, "Themes", "Theme.axaml")))
            .Select(eslesme => eslesme.Groups[1].Value)
            .ToHashSet();

        var eksik = Dosyalar(".axaml")
            .SelectMany(yol => File.ReadAllLines(yol)
                .Select((satir, sira) => (yol, satir, numara: sira + 1))
                .SelectMany(giris => Basvuru.Matches(giris.satir)
                    .Select(eslesme => (giris.yol, giris.numara, ad: eslesme.Groups[1].Value))))
            .Where(giris => !takim.Contains(giris.ad) && !olcu.Contains(giris.ad))
            .Select(giris => $"{Path.GetFileName(giris.yol)}:{giris.numara}: {giris.ad}")
            .ToList();

        Assert.True(eksik.Count == 0,
            "Icons.axaml'de karşılığı olmayan simge başvurusu:\n" + string.Join("\n", eksik));
    }

    [Fact]
    public void HicbirDugmeEmojiTasimiyor()
    {
        var suclular = Dosyalar(".axaml").Concat(Dosyalar(".cs"))
            .SelectMany(yol => File.ReadAllLines(yol)
                .Select((satir, sira) => (yol, satir, numara: sira + 1)))
            .Where(giris => giris.satir.Contains("Content") && Emoji.IsMatch(giris.satir))
            .Select(giris => $"{Path.GetFileName(giris.yol)}:{giris.numara}: {giris.satir.Trim()}")
            .ToList();

        Assert.True(suclular.Count == 0,
            "Düğme içeriğinde emoji kaldı, simge takımından geometri kullanılmalı:\n"
            + string.Join("\n", suclular));
    }

    [Fact]
    public void AyarlarSekmesiSeridinSonundaVeGorunur()
    {
        var (sonuncu, gorunur, simge) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var serit = pencere.FindControl<TabControl>("Tabs");
            Assert.True(serit is not null, "Tabs şeridi biçimlemede yok.");

            var sekmeler = serit!.Items.OfType<TabItem>().ToList();
            var ayarlar = pencere.FindControl<TabItem>("TabSettings");
            Assert.True(ayarlar is not null, "TabSettings biçimlemede yok.");

            return (ReferenceEquals(sekmeler[^1], ayarlar), ayarlar!.IsVisible, ayarlar.Tag);
        });

        Assert.True(sonuncu, "Ayarlar şeridin son sekmesi değil.");
        Assert.True(gorunur, "Ayarlar sekmesi gizli.");
        Assert.NotNull(simge);
    }

    [Fact]
    public void HerSekmeninKendiSimgesiVar()
    {
        var simgesiz = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var serit = pencere.FindControl<TabControl>("Tabs");
            Assert.True(serit is not null, "Tabs şeridi biçimlemede yok.");

            return serit!.Items.OfType<TabItem>()
                .Where(sekme => sekme.Tag is not Avalonia.Media.Geometry)
                .Select(sekme => sekme.Name ?? sekme.Header?.ToString() ?? "adsız")
                .ToList();
        });

        Assert.True(simgesiz.Count == 0,
            "Simgesiz sekme kaldı: " + string.Join(", ", simgesiz));
    }
}
