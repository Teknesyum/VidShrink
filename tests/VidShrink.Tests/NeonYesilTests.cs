using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VidShrink.Tests;

/// <summary>
/// Neon paletinde zemin yeşil, vurgu camgöbeğinden mora giden gradyan
/// (<c>docs/tasarim/fable-neon-yesil-2026-09-23.md</c>). Yeşil <c>seeds.json</c>'daki
/// isteğe bağlı <c>atmos</c> çekirdeğinden gelir; çekirdeği olmayan paletin atmosferi
/// eskisi gibi kendi ember/flame/blaze üçlüsüdür. Kayıt kırmızısı değişmez.
/// </summary>
public sealed class NeonYesilTests
{
    private static readonly XNamespace Ui = "https://github.com/avaloniaui";

    private static string PaletKoku =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Palette");

    private static string Renk(string palet, string anahtar)
    {
        var xaml = File.ReadAllText(Path.Combine(PaletKoku, palet, "Theme.axaml"));
        var m = Regex.Match(xaml, "x:Key=\"" + anahtar + "\"[^>]*>(#[0-9A-Fa-f]{8})<");
        Assert.True(m.Success, $"{palet}: {anahtar} yok.");
        return m.Groups[1].Value;
    }

    private static (int R, int G, int B) Kanallar(string argb) => (
        Convert.ToInt32(argb.Substring(3, 2), 16),
        Convert.ToInt32(argb.Substring(5, 2), 16),
        Convert.ToInt32(argb.Substring(7, 2), 16));

    [Theory]
    [InlineData("AtmosHotColor")]
    [InlineData("AtmosMidColor")]
    [InlineData("AtmosEdgeColor")]
    [InlineData("EmberDeepColor")]
    [InlineData("EmberMidColor")]
    [InlineData("EmberEdgeColor")]
    [InlineData("EmberBarMidColor")]
    public void NeonAtmosferiYesil(string anahtar)
    {
        var (r, g, b) = Kanallar(Renk("Neon", anahtar));

        Assert.True(g > r, $"{anahtar}: yeşil {g} kırmızıdan {r} büyük değil.");
        Assert.True(g >= b, $"{anahtar}: yeşil {g} maviden {b} küçük.");
    }

    [Fact]
    public void KayitKirmizisiDegismedi()
    {
        Assert.Equal("#FFFF0033", Renk("Neon", "NeonEmberColor"));
    }

    /// <summary>
    /// Olumsuz kontrol: <c>atmos</c> çekirdeği olmayan palette atmosfer ember üçlüsüdür.
    /// Üreteç aynı değeri iki anahtara vermediği için mavi kanal en çok iki birim kayar.
    /// </summary>
    [Theory]
    [InlineData("Dracula")]
    [InlineData("Gruvbox")]
    [InlineData("CatppuccinLatte")]
    public void CekirdeksizPaletteAtmosferEmber(string palet)
    {
        YakinEsit(Renk(palet, "EmberBlazeColor"), Renk(palet, "AtmosHotColor"));
        YakinEsit(Renk(palet, "EmberFlameColor"), Renk(palet, "AtmosMidColor"));
        YakinEsit(Renk(palet, "NeonEmberColor"), Renk(palet, "AtmosEdgeColor"));
    }

    private static void YakinEsit(string beklenen, string gelen)
    {
        var (r1, g1, b1) = Kanallar(beklenen);
        var (r2, g2, b2) = Kanallar(gelen);
        Assert.True(r1 == r2 && g1 == g2 && Math.Abs(b1 - b2) <= 2, $"{beklenen} ≠ {gelen}");
    }

    [Fact]
    public void VurguGradyaniCamgobegindenMora()
    {
        var duraklar = ThemeSources.Resource("AccentGradient").Elements(Ui + "GradientStop")
            .Select(d => ((string)d.Attribute("Color")!).Trim())
            .ToList();

        Assert.Equal(new[] { "{StaticResource NeonBlueColor}", "{StaticResource NeonPurpleColor}" }, duraklar);
    }

    /// <summary>Birincil düğme, ilerleme çubuğu ve güncelleme çubuğu gradyanla dolar; sarı dolgu kalmadı.</summary>
    [Fact]
    public void DolgularGradyanda()
    {
        var kontroller = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Controls.axaml"));
        var pencere = File.ReadAllText(TipSources.WindowXamlPath);

        var birincil = Regex.Match(kontroller, "x:Key=\"PrimaryButton\"[\\s\\S]*?Property=\"Background\" Value=\"([^\"]+)\"");
        var cubuk = Regex.Match(kontroller, "x:Key=\"\\{x:Type ProgressBar\\}\"[\\s\\S]*?Property=\"Foreground\" Value=\"([^\"]+)\"");
        var guncelleme = Regex.Match(pencere, "x:Name=\"UpdateBarFill\"[^>]*Background=\"([^\"]+)\"");

        Assert.Equal("{StaticResource AccentGradient}", birincil.Groups[1].Value);
        Assert.Equal("{StaticResource AccentGradient}", cubuk.Groups[1].Value);
        Assert.Equal("{StaticResource AccentGradient}", guncelleme.Groups[1].Value);
    }

    /// <summary>
    /// Güncelleme çubuğunun rengi indirme sürerken koddan yeniden boyanıyor; XAML'daki gradyan
    /// ilk karede eziliyordu (ilk görüntü sarıydı). Rozet de aynı gradyanı taşır.
    /// </summary>
    [Fact]
    public void GuncellemeKoduSariyaBoyamiyor()
    {
        var uygulama = Path.Combine(TipSources.Root, "src", "VidShrink.App");
        var cubuk = File.ReadAllText(Path.Combine(uygulama, "MainWindow.Guncelleme.cs"));
        var rozet = File.ReadAllText(Path.Combine(uygulama, "MainWindow.axaml.cs"));

        Assert.Matches("_ => \"AccentGradient\"", cubuk);
        Assert.Matches("UpdateBadgeState.Downloading => \"AccentGradient\"", rozet);
        Assert.DoesNotContain("\"EmberBlaze\"", cubuk);
        Assert.DoesNotContain("\"EmberBlaze\"", rozet);
    }
}
