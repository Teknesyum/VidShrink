using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace VidShrink.Tests;

/// <summary>
/// T29 K6/K7: madde işareti vurgu rengiyle çizilir ve maddeler arasında bir kademe boşluk
/// kalır. Ölçüm davranışı sınar: boyayıcı gerçek kaynak sözlüğüyle çalıştırılır ve çıkan
/// koşunun fırçası okunur; satır aralığı temanın çözülmüş değerinden gelir.
///
/// Kaynak metni öznitelik sırasına göre arayan bir tarama kırılgandır; ekrandaki blokların
/// hangi temayı taşıdığı XML olarak ayrıştırılarak okunur.
/// </summary>
public sealed class BulletPaintingTests
{
    private static readonly Styles Controls = LoadControls();

    private static readonly XNamespace Ui = "https://github.com/avaloniaui";

    /// <summary>Madde listesi taşıyabilen temalar.</summary>
    private static readonly string[] BulletThemes = ["TipText", "BulletText"];

    private static Styles LoadControls()
    {
        AppHost.Ensure();
        var include = new StyleInclude(new Uri("avares://VidShrink.App/"))
        {
            Source = new Uri("avares://VidShrink.App/Themes/Controls.axaml")
        };

        return (Styles)include.Loaded;
    }

    private static object Resource(string key)
    {
        Assert.True(
            Controls.TryGetResource(key, ThemeVariant.Dark, out var value) && value is not null,
            $"Controls.axaml kaynak ağacında {key} yok.");
        return value!;
    }

    /// <summary>
    /// Vurgu fırçası blokun kendi kaynağına konur: boyayıcı onu anahtarla arar, yani ekranda
    /// pencerenin kaynak ağacından bulduğu fırçanın aynısı.
    /// </summary>
    private static TextBlock Block(string themeKey)
    {
        var block = new TextBlock { Theme = (ControlTheme)Resource(themeKey) };
        block.Resources.Add("NeonBlue", Resource("NeonBlue"));
        return block;
    }

    [Theory]
    [InlineData("TipText")]
    [InlineData("BulletText")]
    public void BulletRunCarriesTheAccentBrush(string themeKey)
    {
        var block = Block(themeKey);
        var accent = (IBrush)Resource("NeonBlue");

        VidShrink.App.MainWindow.PaintBullets(block, "• First line.\n• Second line.");

        var runs = block.Inlines!.OfType<Run>().ToList();
        var bullets = runs.Where(run => run.Text == "• ").ToList();
        var bodies = runs.Where(run => run.Text != "• ").ToList();

        Assert.Equal(2, bullets.Count);
        Assert.All(bullets, run => Assert.Same(accent, run.Foreground));
        Assert.All(bodies, run => Assert.NotSame(accent, run.Foreground));
        Assert.Equal("• First line.\n• Second line.", block.Tag);
    }

    [Theory]
    [InlineData("TipText")]
    [InlineData("BulletText")]
    public void BulletThemeSpacesTheLines(string themeKey)
    {
        var theme = (ControlTheme)Resource(themeKey);
        var step = (double)Resource("SpaceXs");

        var spacing = theme.Setters
            .OfType<Setter>()
            .Where(setter => setter.Property == TextBlock.LineSpacingProperty)
            .Select(setter => setter.Value)
            .ToList();

        Assert.Single(spacing);
        Assert.Equal(step, Assert.IsType<double>(spacing[0]));
        Assert.True(step > 0);
    }

    /// <summary>
    /// Ekranda madde taşıyan her blok boyanan temalardan birini kullanacak. Hakkında
    /// sekmesindeki açıklama metinleri bu ölçüm yazılana kadar boyamanın dışındaydı.
    /// </summary>
    [Fact]
    public void EveryBulletedBlockOnScreenUsesABulletTheme()
    {
        var document = XDocument.Load(TipSources.WindowXamlPath);

        var stray = document
            .Descendants(Ui + "TextBlock")
            .Where(block => (block.Attribute("Text")?.Value ?? string.Empty)
                .Split('\n')
                .Any(line => line.StartsWith("• ", StringComparison.Ordinal)))
            .Select(block => new
            {
                Theme = block.Attribute("Theme")?.Value ?? "(tema yok)",
                First = TipSources.FirstLine(block.Attribute("Text")!.Value)
            })
            .Where(block => !BulletThemes.Any(name => block.Theme.Contains(name, StringComparison.Ordinal)))
            .Select(block => $"{block.Theme}: {block.First}")
            .ToList();

        Assert.True(
            stray.Count == 0,
            "Madde taşıyıp boyanmayan blok var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, stray));
    }
}
