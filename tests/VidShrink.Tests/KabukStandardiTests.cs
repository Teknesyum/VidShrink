using System.Text.RegularExpressions;

namespace VidShrink.Tests;

public sealed class KabukStandardiTests
{
    private static readonly string AppRoot = Path.Combine(TipSources.Root, "src", "VidShrink.App");

    private static readonly string PaletteRoot =
        Path.Combine(AppRoot, "Themes", "Palette") + Path.DirectorySeparatorChar;

    private static string[] PaletDisiAxaml() =>
        Directory.GetFiles(Path.Combine(TipSources.Root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(yol => !yol.StartsWith(PaletteRoot, StringComparison.OrdinalIgnoreCase))
            .OrderBy(yol => yol, StringComparer.Ordinal)
            .ToArray();

    private static string[] YorumsuzSatirlar(string metin)
    {
        var temiz = Regex.Replace(metin, "<!--.*?-->", eslesme =>
            new string('\n', eslesme.Value.Count(karakter => karakter == '\n')), RegexOptions.Singleline);
        return temiz.Replace("\r\n", "\n").Split('\n');
    }

    private static string Kisalt(string yol) =>
        Path.GetRelativePath(TipSources.Root, yol).Replace('\\', '/');

    [Fact]
    public void RenkYalnizPaletten()
    {
        var desen = new Regex(@"(?<![\w#])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})(?![\w])");
        var kacaklar = new List<string>();

        foreach (var yol in PaletDisiAxaml())
        {
            var satirlar = YorumsuzSatirlar(File.ReadAllText(yol));
            for (var i = 0; i < satirlar.Length; i++)
                if (desen.IsMatch(satirlar[i]))
                    kacaklar.Add($"{Kisalt(yol)}:{i + 1}: {satirlar[i].Trim()}");
        }

        Assert.Empty(kacaklar);
    }

    [Fact]
    public void InfoRengiYoktur()
    {
        var desen = new Regex(@"<(?:Color|SolidColorBrush|LinearGradientBrush|BoxShadows)\s+x:Key=""([^""]+)""");
        var dosyalar = Directory
            .GetFiles(Path.Combine(AppRoot, "Themes"), "*.axaml", SearchOption.AllDirectories)
            .OrderBy(yol => yol, StringComparer.Ordinal);
        var bulunan = new List<string>();

        foreach (var yol in dosyalar)
            foreach (Match eslesme in desen.Matches(File.ReadAllText(yol)))
                if (eslesme.Groups[1].Value.Contains("Info", StringComparison.OrdinalIgnoreCase))
                    bulunan.Add($"{Kisalt(yol)}: {eslesme.Groups[1].Value}");

        Assert.Empty(bulunan);
    }

    [Fact]
    public void PariltiYaziyaKonmaz()
    {
        var yaziOgeleri = new[] { "TextBlock", "SelectableTextBlock", "TextBox", "Label", "AccessText" };
        var cocukDesen = new Regex($@"<({string.Join('|', yaziOgeleri)})\.Effect\b");
        var etiketDesen = new Regex($@"<({string.Join('|', yaziOgeleri)})\b[^>]*>", RegexOptions.Singleline);
        var kacaklar = new List<string>();

        foreach (var yol in PaletDisiAxaml())
        {
            var metin = File.ReadAllText(yol);

            foreach (Match eslesme in cocukDesen.Matches(metin))
                kacaklar.Add($"{Kisalt(yol)}: <{eslesme.Groups[1].Value}.Effect>");

            foreach (Match eslesme in etiketDesen.Matches(metin))
                if (eslesme.Value.Contains("Effect=", StringComparison.Ordinal)
                    || eslesme.Value.Contains("BoxShadow=", StringComparison.Ordinal))
                    kacaklar.Add($"{Kisalt(yol)}: {eslesme.Groups[1].Value} oznitelikte parilti");
        }

        Assert.Empty(kacaklar);
    }

    [Fact]
    public void PariltiKapsayicidadir()
    {
        var izinli = new HashSet<string>(StringComparer.Ordinal)
        {
            "Border", "Path", "Ellipse", "Rectangle", "Panel", "Grid", "StackPanel", "DockPanel"
        };
        var satirOgeleri = new[]
        {
            "ListBoxItem", "ComboBoxItem", "DataGridRow", "DataGridCell", "TreeViewItem", "ItemsControl", "ListBox"
        };
        var sahipDesen = new Regex(@"<(\w+)\.Effect>");
        var secimDesen = new Regex(@"<Style\s+Selector=""([^""]*)""");
        var kacaklar = new List<string>();

        foreach (var yol in PaletDisiAxaml())
        {
            var satirlar = File.ReadAllLines(yol);

            foreach (Match eslesme in sahipDesen.Matches(string.Join('\n', satirlar)))
                if (!izinli.Contains(eslesme.Groups[1].Value))
                    kacaklar.Add($"{Kisalt(yol)}: <{eslesme.Groups[1].Value}.Effect>");

            for (var i = 0; i < satirlar.Length; i++)
            {
                if (!satirlar[i].Contains(@"Property=""BoxShadow""", StringComparison.Ordinal)
                    && !satirlar[i].Contains(@"Property=""Effect""", StringComparison.Ordinal))
                    continue;

                var zincir = SecimZinciri(satirlar, i, secimDesen);
                foreach (var oge in satirOgeleri)
                    if (zincir.Contains(oge, StringComparison.Ordinal))
                        kacaklar.Add($"{Kisalt(yol)}:{i + 1}: {oge} parliyor -> {zincir}");
            }
        }

        Assert.Empty(kacaklar);
    }

    private static string SecimZinciri(string[] satirlar, int satir, Regex secimDesen)
    {
        var parcalar = new List<string>();

        for (var i = satir; i >= 0; i--)
        {
            var eslesme = secimDesen.Match(satirlar[i]);
            if (!eslesme.Success) continue;

            var secim = eslesme.Groups[1].Value;
            parcalar.Insert(0, secim);
            if (!secim.StartsWith('^')) break;
        }

        return string.Join(" | ", parcalar);
    }

    [Fact]
    public void YerTutucuMetinYok()
    {
        var kacaklar = new List<string>();
        var dosyalar = Directory
            .GetFiles(Path.Combine(TipSources.Root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(yol => yol.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
                          || yol.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(yol => yol, StringComparer.Ordinal);

        foreach (var yol in dosyalar)
        {
            var satirlar = File.ReadAllLines(yol);
            for (var i = 0; i < satirlar.Length; i++)
                if (satirlar[i].Contains("Watermark", StringComparison.Ordinal))
                    kacaklar.Add($"{Kisalt(yol)}:{i + 1}: {satirlar[i].Trim()}");
        }

        Assert.Empty(kacaklar);
    }

    [Fact]
    public void HataBildirimiKendiKapanmaz()
    {
        var kaynak = File.ReadAllText(Path.Combine(AppRoot, "ShrinkJobWindow.axaml.cs"));

        var sure = Regex.Match(kaynak, @"TimeSpan\s+Linger\s*=\s*TimeSpan\.FromSeconds\((\d+)\)");
        Assert.True(sure.Success, "Linger suresi bulunamadi");
        Assert.Equal("6", sure.Groups[1].Value);

        Assert.Contains("Interval = Linger", kaynak, StringComparison.Ordinal);

        var kurulum = Regex.Matches(kaynak, @"^[^\r\n]*\bArmClose\(\);", RegexOptions.Multiline);
        Assert.NotEmpty(kurulum);
        foreach (Match satir in kurulum)
            Assert.Contains("State == ShrinkJobState.Bitti", satir.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void KendiBaslikCubugu()
    {
        var eksikler = new List<string>();
        var pencereler = Directory
            .GetFiles(AppRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(yol => File.ReadAllText(yol)
                .Contains(@"ExtendClientAreaToDecorationsHint=""True""", StringComparison.Ordinal))
            .OrderBy(yol => yol, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(pencereler);

        foreach (var yol in pencereler)
        {
            var isaret = File.ReadAllText(yol);
            if (!isaret.Contains(@"WindowDecorations=""BorderOnly""", StringComparison.Ordinal))
                eksikler.Add($"{Kisalt(yol)}: WindowDecorations=\"BorderOnly\" yok");

            var arka = yol + ".cs";
            if (!File.Exists(arka)) { eksikler.Add($"{Kisalt(yol)}: arka kod yok"); continue; }

            var govde = File.ReadAllText(arka);
            if (!govde.Contains("BeginMoveDrag(", StringComparison.Ordinal))
                eksikler.Add($"{Kisalt(arka)}: BeginMoveDrag yok");
        }

        var ana = File.ReadAllText(Path.Combine(AppRoot, "MainWindow.axaml.cs"));
        var ciftTik = ana.IndexOf("e.ClickCount == 2", StringComparison.Ordinal);
        if (ciftTik < 0) eksikler.Add("MainWindow.axaml.cs: cift tik kolu yok");
        else if (ana.IndexOf("ToggleMaximizeRestore()", ciftTik, StringComparison.Ordinal) < 0)
            eksikler.Add("MainWindow.axaml.cs: cift tik buyutme/geri alma cagirmiyor");

        if (!ana.Contains("WindowState = WindowState == WindowState.Maximized", StringComparison.Ordinal))
            eksikler.Add("MainWindow.axaml.cs: buyutme/geri alma gecisi yok");

        Assert.Empty(eksikler);
    }

    [Fact]
    public void GorselTemaKitapligiYok()
    {
        var yasak = new[]
        {
            "MaterialDesign", "Material.Avalonia", "MahApps", "HandyControl", "WPF-UI", "WPFUI",
            "Wpf.Ui", "FluentAvalonia", "Semi.Avalonia", "Citrus.Avalonia", "Actipro", "MUI", "Syncfusion"
        };
        var desen = new Regex(@"PackageReference\s+Include=""([^""]+)""");
        var kacaklar = new List<string>();

        foreach (var yol in Directory
                     .GetFiles(TipSources.Root, "*.csproj", SearchOption.AllDirectories)
                     .OrderBy(yol => yol, StringComparer.Ordinal))
        {
            if (Kisalt(yol).StartsWith(".calisma/", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (Match eslesme in desen.Matches(File.ReadAllText(yol)))
            {
                var ad = eslesme.Groups[1].Value;
                foreach (var kitaplik in yasak)
                    if (ad.Contains(kitaplik, StringComparison.OrdinalIgnoreCase))
                        kacaklar.Add($"{Kisalt(yol)}: {ad}");
            }
        }

        Assert.Empty(kacaklar);
    }
}
