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
        var son = Renk(xaml, "PauseGlossFadeColor");
        Assert.True(son is not null, $"{palet}: PauseGlossFadeColor yok.");
        Assert.StartsWith("#00", son);
        for (var kanal = 3; kanal < 9; kanal += 2)
        {
            var fark = Math.Abs(Convert.ToInt32(parlama![kanal..(kanal + 2)], 16) - Convert.ToInt32(son![kanal..(kanal + 2)], 16));
            Assert.True(fark <= 2, $"{palet}: bitiş durağı {son} parlama {parlama} renginden uzak.");
        }
    }

    [Fact]
    public void ParlamaDurakPaletGecislerindeHepAyniPaletinRenginde() => AppHost.Run(() =>
    {
        var app = Avalonia.Application.Current!;
        app.TryGetResource("PauseGlyphGloss", null, out var fircaNesne);
        var firca = (Avalonia.Media.LinearGradientBrush)fircaNesne!;
        var baslangic = VidShrink.App.Themes.PaletteCatalog.Use(VidShrink.App.Themes.PaletteCatalog.Default);
        var kusurlar = new List<string>();
        try
        {
            var adlar = VidShrink.App.Themes.PaletteCatalog.Names.ToList();
            var zincir = adlar.Concat(Enumerable.Reverse(adlar)).Concat(adlar.Where((_, i) => i % 2 == 0)).Concat(adlar.Where((_, i) => i % 3 == 0)).ToList();
            var onceki = VidShrink.App.Themes.PaletteCatalog.Default;
            foreach (var ad in zincir)
            {
                VidShrink.App.Themes.PaletteCatalog.Use(ad);
                app.TryGetResource("PauseGlossColor", null, out var renkNesne);
                var beklenen = (Avalonia.Media.Color)renkNesne!;
                var gercek = firca.GradientStops[0].Color;
                if (beklenen != gercek) kusurlar.Add($"{onceki} -> {ad}: beklenen {beklenen}, durak {gercek}");
                app.TryGetResource("PauseGlossFadeColor", null, out var sonNesne);
                var sonBeklenen = (Avalonia.Media.Color)sonNesne!;
                var sonGercek = firca.GradientStops[1].Color;
                if (sonBeklenen != sonGercek) kusurlar.Add($"{onceki} -> {ad}: beklenen son durak {sonBeklenen}, durak {sonGercek}");
                onceki = ad;
            }
        }
        finally
        {
            VidShrink.App.Themes.PaletteCatalog.Use(baslangic);
        }

        Assert.True(kusurlar.Count == 0, string.Join(Environment.NewLine, kusurlar.Take(20)));
        return 0;
    });

    [Fact]
    public void PaletleriSirayaGezinceHerFircaPaletinRenginde() => AppHost.Run(() =>
    {
        var app = Avalonia.Application.Current!;
        var baslangic = VidShrink.App.Themes.PaletteCatalog.Use(VidShrink.App.Themes.PaletteCatalog.Default);
        var kusurlar = new List<string>();
        try
        {
            var onceki = VidShrink.App.Themes.PaletteCatalog.Default;
            foreach (var ad in VidShrink.App.Themes.PaletteCatalog.Names)
            {
                VidShrink.App.Themes.PaletteCatalog.Use(ad);
                var sozluk = new Avalonia.Markup.Xaml.Styling.ResourceInclude((Uri?)null)
                {
                    Source = new Uri($"avares://VidShrink.App/Themes/Palette/{ad}/Theme.axaml")
                }.Loaded;
                var takmaAdlar = VidShrink.PaletteGen.PaletteBuilder.RoleAliases.Select(a => a.Role).ToHashSet(StringComparer.Ordinal);
                foreach (var anahtar in sozluk.Keys.OfType<string>().Where(k => k.EndsWith("Color", StringComparison.Ordinal) && !takmaAdlar.Contains(k)))
                {
                    if (!sozluk.TryGetValue(anahtar, out var deger) || deger is not Avalonia.Media.Color beklenen) continue;
                    var ad2 = anahtar[..^"Color".Length];
                    if (!app.TryGetResource(ad2, null, out var nesne) || nesne is not Avalonia.Media.SolidColorBrush firca) continue;
                    if (firca.Color != beklenen) kusurlar.Add($"{onceki} -> {ad}: {ad2} {firca.Color}, beklenen {beklenen}");
                }
                app.TryGetResource("PauseGlyphGloss", null, out var gradyan);
                if (gradyan is Avalonia.Media.LinearGradientBrush g && app.TryGetResource("PauseGlossColor", null, out var renk) && renk is Avalonia.Media.Color r && g.GradientStops[0].Color != r)
                    kusurlar.Add($"{onceki} -> {ad}: PauseGlyphGloss {g.GradientStops[0].Color}, beklenen {r}");
                if (gradyan is Avalonia.Media.LinearGradientBrush g2 && app.TryGetResource("PauseGlossFadeColor", null, out var son) && son is Avalonia.Media.Color s && g2.GradientStops[1].Color != s)
                    kusurlar.Add($"{onceki} -> {ad}: PauseGlyphGloss son durak {g2.GradientStops[1].Color}, beklenen {s}");
                onceki = ad;
            }
        }
        finally
        {
            VidShrink.App.Themes.PaletteCatalog.Use(baslangic);
        }

        Assert.True(kusurlar.Count == 0, string.Join(Environment.NewLine, kusurlar.Take(25)));
        return 0;
    });

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
        Assert.Contains("{StaticResource PauseGlossFadeColor}", firca);
        Assert.DoesNotContain("Transparent", firca);
        Assert.Contains("{StaticResource PauseGlyphGlossOpacity}", firca);
        Assert.Contains("{StaticResource PauseGlyphGlossReach}", firca);
    }
}
