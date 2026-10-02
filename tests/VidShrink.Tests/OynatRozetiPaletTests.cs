using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Oynat/durdur rozetinin parlama rengi paletten gelir: her palette <c>PauseGlossColor</c> var ve
/// paletin açık ucudur (koyuda gövde yazısı, açıkta zemin). Rozetin dolgusu ve parlaması
/// ham renk taşımaz, yalnız belirteç ve fırça adlarına bağlanır.
/// </summary>
public sealed class OynatRozetiPaletTests
{
    private static string PaletKoku =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Palette");

    public static IEnumerable<object[]> Paletler() => PaletKarsitligiTests.Paletler();

    private static string? Renk(string xaml, string anahtar)
    {
        var m = Regex.Match(xaml, "x:Key=\"" + anahtar + "\"[^>]*>(#[0-9A-Fa-f]{8})<");
        return m.Success ? m.Groups[1].Value : null;
    }

    [Theory]
    [MemberData(nameof(Paletler))]
    public void ParlamaRengiHerPalettedeVarVeAcikUctur(string palet)
    {
        var xaml = File.ReadAllText(Path.Combine(PaletKoku, palet, "Theme.axaml"));
        var parlama = Renk(xaml, "PauseGlossColor");
        Assert.True(parlama is not null, $"{palet}: PauseGlossColor yok.");
        var metin = Renk(xaml, "TextBodyColor")!;
        var zemin = Renk(xaml, "AppBgColor")!;
        var acik = Math.Max(PaletKarsitligiTests.Parlaklik(metin), PaletKarsitligiTests.Parlaklik(zemin));
        var olculen = PaletKarsitligiTests.Parlaklik(parlama!);
        Assert.True(olculen >= 0.5, $"{palet}: parlama {parlama} açık değil ({olculen.ToString("0.00", CultureInfo.InvariantCulture)}).");
        Assert.True(olculen >= acik - 0.01, $"{palet}: parlama {parlama} paletin açık ucundan koyu.");
    }

    [Fact]
    public void RozetinKatmanlariHamRenkTasimazVeMatDolguluDur()
    {
        var axaml = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Playback", "PlayerView.axaml"));
        var bas = axaml.IndexOf("<Border x:Name=\"PauseGlyph\"", StringComparison.Ordinal);
        var son = axaml.IndexOf("<Border x:Name=\"EndCue\"", StringComparison.Ordinal);
        var rozet = axaml[bas..son];

        Assert.DoesNotContain("#", rozet.Replace("{Binding #PauseGlyphIcon.Data}", ""));
        Assert.Matches("x:Name=\"PauseGlyphIcon\"[^>]*Fill=\"\\{StaticResource NeonBlue\\}\"", rozet);
        Assert.DoesNotContain("Stroke", rozet);
        Assert.Matches("x:Name=\"PauseGlyphGloss\"[^>]*Fill=\"\\{StaticResource PauseGlyphGloss\\}\"", rozet);
        Assert.True(rozet.IndexOf("x:Name=\"PauseGlyphIcon\"", StringComparison.Ordinal) < rozet.IndexOf("x:Name=\"PauseGlyphGloss\"", StringComparison.Ordinal),
            "parlama dolgunun üstünde durmalı");

        var tema = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Theme.axaml"));
        var firca = Regex.Match(tema, "<LinearGradientBrush x:Key=\"PauseGlyphGloss\".*?</LinearGradientBrush>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(firca);
        Assert.DoesNotContain("#", firca);
        Assert.Contains("{StaticResource PauseGlossColor}", firca);
        Assert.Contains("{StaticResource PauseGlyphGlossOpacity}", firca);
        Assert.Contains("{StaticResource PauseGlyphGlossReach}", firca);
    }
}
