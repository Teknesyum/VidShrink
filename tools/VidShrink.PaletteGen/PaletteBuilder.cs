using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.PaletteGen;

/// <summary>
/// Standart rollerden paletin otuz bir renk anahtarını türetir. Rol → anahtar eşlemesi:
/// <c>renk-1/2/3</c> → <c>NeonBlue/Pink/Purple</c>, <c>renk-2-text</c> → <c>PinkText</c>,
/// <c>black</c> → <c>AppBg</c>, <c>surface</c> → <c>SurfaceTone</c>, <c>text</c> → <c>TextBody</c>,
/// <c>disabled</c> → <c>TextDisabled</c>, <c>success</c> → <c>NeonSuccess</c>,
/// <c>warning</c> → <c>EmberBlaze</c>, <c>danger</c> → <c>NeonEmber</c>, <c>flame</c> → <c>EmberFlame</c>.
/// Geri kalanlar (dolgu, kenar, atmosfer, ateş şeridi, perde, parıltı) bunların alfalı ya da
/// karışık hâlleri. <c>renk-3-text</c> ve <c>glass-base</c> tohumda durur, anahtara inmez.
/// </summary>
public static class PaletteBuilder
{
    public const string FileName = "Theme.axaml";

    /// <summary>
    /// Standardın rol adı → paletin anahtarı. Proje fırçaları rol adını okur; renk tek anahtarda
    /// kalır, böylece palet geçişinde bir değer iki anahtara bölünmez.
    /// </summary>
    public static IReadOnlyList<(string Role, string Key)> RoleAliases { get; } =
    [
        ("Renk1Color", "NeonBlueColor"),
        ("Renk2Color", "NeonPinkColor"),
        ("Renk3Color", "NeonPurpleColor"),
        ("Renk2TextColor", "PinkTextColor"),
        ("SuccessColor", "NeonSuccessColor"),
        ("SurfaceColor", "SurfaceToneColor"),
        ("DisabledColor", "TextDisabledColor"),
        ("OnRenk1Color", "OnNeonColor"),
        ("OnRenk2Color", "OnNeonColor"),
        ("OnRenk3Color", "OnNeonColor"),
        ("Renk1x10Color", "NeonBlueFillColor"),
        ("Renk1x20Color", "NeonBlueHoverColor"),
        ("Renk1x30Color", "NeonBlueActiveColor"),
        ("Renk2x10Color", "NeonPinkFillColor"),
        ("Renk3x50Color", "NeonPurpleBorderColor"),
        ("BorderDefaultColor", "NeonBlueBorderColor"),
        ("BorderStrongColor", "NeonBlueBorderStrongColor"),
        ("DangerColor", "NeonEmberColor"),
        ("WarningColor", "EmberBlazeColor"),
    ];

    /// <summary><c>danger</c> verilmemişse standardın kuralı: <c>danger</c> <c>renk-2</c>'nin değerini izler.</summary>
    public static string Danger(PaletteSeed seed) => seed.Danger ?? seed.Renk2;

    /// <summary><c>flame</c> verilmemişse ateşin ortası, tehlike ile uyarının tam ortası.</summary>
    public static string Flame(PaletteSeed seed) => seed.Flame ?? Mix(Danger(seed), seed.Warning, 0.5);

    /// <summary>Tohumdan palet dosyasının tam metni; satır sonu <c>\n</c>.</summary>
    public static string Build(PaletteSeed seed) => Distinct(Compose(seed));

    public static void WriteAll(string folder, IEnumerable<PaletteSeed> seeds)
    {
        foreach (var stale in Directory.GetFiles(folder, "*.axaml", SearchOption.AllDirectories)) File.Delete(stale);

        foreach (var seed in seeds)
        {
            var home = Path.Combine(folder, seed.Name);
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home, FileName), Build(seed), new System.Text.UTF8Encoding(false));
        }
    }

    private static string Compose(PaletteSeed seed)
    {
        var bg = seed.Black;
        var ember = Danger(seed);
        var flame = Flame(seed);
        var blaze = seed.Warning;
        var onAccent = OnAccent(seed);
        var light = Luminance(bg) > 0.5;
        string Warm(string ground, double weight)
        {
            var warmed = Dim(Mix(ground, ember, weight), light);
            if (seed.Atmos is null) return warmed;
            return Shade(Dim(Mix(ground, seed.Atmos, weight * 3), light), Luminance(warmed));
        }
        var hot = seed.Atmos is null ? blaze : Mix(seed.Atmos, "#FFFFFF", 0.35);
        var mid = seed.Atmos ?? flame;
        var edge = seed.Atmos is null ? ember : Mix(seed.Atmos, bg, 0.4);

        string[] lines =
        [
            "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"",
            "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">",
            "",
            $"  <!-- {seed.Note} -->",
            "  <!-- Elle yazılmadı: Themes/Palette/seeds.json içindeki standart rollerden",
            "       (renk-1 … warning) üretildi. Değiştirmek için tohumu düzenle ve",
            "       VidShrink.PaletteGen aracını çalıştır; komut docs/tema.md içinde. -->",
            "",
            $"  <Color x:Key=\"NeonBlueColor\">{Solid(seed.Renk1)}</Color>",
            $"  <Color x:Key=\"NeonPinkColor\">{Solid(seed.Renk2)}</Color>",
            $"  <Color x:Key=\"NeonPurpleColor\">{Solid(seed.Renk3)}</Color>",
            $"  <Color x:Key=\"NeonSuccessColor\">{Solid(seed.Success)}</Color>",
            $"  <Color x:Key=\"SurfaceToneColor\">{Solid(seed.Surface)}</Color>",
            $"  <Color x:Key=\"AppBgColor\">{Solid(bg)}</Color>",
            $"  <Color x:Key=\"TextBodyColor\">{Solid(seed.Text)}</Color>",
            $"  <Color x:Key=\"TextDisabledColor\">{Solid(seed.Disabled)}</Color>",
            $"  <Color x:Key=\"OnNeonColor\">{onAccent}</Color>",
            $"  <Color x:Key=\"PinkTextColor\">{Solid(seed.Renk2Text)}</Color>",
            "",
            $"  <Color x:Key=\"NeonBlueFillColor\">{Alpha(seed.Renk1, 0x1A)}</Color>",
            $"  <Color x:Key=\"NeonBlueHoverColor\">{Alpha(seed.Renk1, 0x33)}</Color>",
            $"  <Color x:Key=\"NeonBlueActiveColor\">{Alpha(seed.Renk1, 0x4D)}</Color>",
            $"  <Color x:Key=\"NeonBlueBorderColor\">{Alpha(seed.Renk1, 0x4D)}</Color>",
            $"  <Color x:Key=\"NeonBlueBorderStrongColor\">{Alpha(seed.Renk1, 0x80)}</Color>",
            $"  <Color x:Key=\"NeonPinkFillColor\">{Alpha(seed.Renk2, 0x1A)}</Color>",
            $"  <Color x:Key=\"NeonPurpleBorderColor\">{Alpha(seed.Renk3, 0x80)}</Color>",
            "",
            $"  <Color x:Key=\"NeonEmberColor\">{Solid(ember)}</Color>",
            $"  <Color x:Key=\"EmberFlameColor\">{Solid(flame)}</Color>",
            $"  <Color x:Key=\"EmberBlazeColor\">{Solid(blaze)}</Color>",
            $"  <Color x:Key=\"AtmosHotColor\">{Solid(hot)}</Color>",
            $"  <Color x:Key=\"AtmosMidColor\">{Solid(mid)}</Color>",
            $"  <Color x:Key=\"AtmosEdgeColor\">{Solid(edge)}</Color>",
            $"  <Color x:Key=\"EmberDeepColor\">{Solid(Warm(bg, 0.02))}</Color>",
            $"  <Color x:Key=\"EmberMidColor\">{Solid(Warm(bg, 0.035))}</Color>",
            $"  <Color x:Key=\"EmberEdgeColor\">{Solid(Warm(bg, 0.05))}</Color>",
            $"  <Color x:Key=\"EmberBarDeepColor\">{Solid(Warm(seed.Surface, 0.03))}</Color>",
            $"  <Color x:Key=\"EmberBarMidColor\">{Solid(Warm(seed.Surface, 0.05))}</Color>",
            $"  <Color x:Key=\"EmberBarEdgeColor\">{Solid(Warm(seed.Surface, 0.07))}</Color>",
            "",
            $"  <Color x:Key=\"PlaybackScrimColor\">{Alpha(bg, 0xCC)}</Color>",
            $"  <Color x:Key=\"PlaybackScrimEdgeColor\">{Alpha(bg, 0x00)}</Color>",
            "",
            $"  <BoxShadows x:Key=\"GlowBlue\">0 0 20 0 {Alpha(seed.Renk1, 0x40)}</BoxShadows>",
            $"  <BoxShadows x:Key=\"GlowPink\">0 0 20 0 {Alpha(seed.Renk2, 0x40)}</BoxShadows>",
            $"  <BoxShadows x:Key=\"GlowPurple\">0 0 20 0 {Alpha(seed.Renk3, 0x40)}</BoxShadows>",
            "  <BoxShadows x:Key=\"GlowNone\">0 0 0 0 #00000000</BoxShadows>",
            "",
            "  <!-- Standardın rol adları. Themes/Theme.axaml fırçaları bu adlara bağlanır;",
            "       değer yukarıdaki tek anahtardan gelir, ikinci bir renk yazılmaz. -->",
            .. RoleAliases.Select(alias => $"  <StaticResource x:Key=\"{alias.Role}\" ResourceKey=\"{alias.Key}\"/>"),
            "",
            "</ResourceDictionary>",
            ""
        ];

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Palet değişimi eski rengi yeni renge değerle eşler (<c>PaletteCatalog.Recolour</c>): iki anahtar
    /// aynı değeri taşıyıp öteki palette ayrışırsa renk belirsiz sayılır ve hiç boyanmaz. Bu yüzden
    /// bir palette her renk değeri tek anahtara aittir; çakışan sonraki anahtarın mavi kanalı bir
    /// birim kayar. Ölçü: <c>PaletteApplyTests.AyniRenkIkiAnahtardaYok</c>.
    /// </summary>
    public static string Distinct(string xaml)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Regex.Replace(xaml, "(<Color x:Key=\"[^\"]+\">)#([0-9A-F]{8})(<)", m =>
        {
            var value = Convert.ToUInt32(m.Groups[2].Value, 16);
            var step = (value & 0xFF) == 0xFF ? uint.MaxValue : 1u;
            while (!seen.Add(value.ToString("X8"))) value += step;
            return $"{m.Groups[1].Value}#{value:X8}{m.Groups[3].Value}";
        });
    }

    /// <summary>
    /// Neon dolgunun üstündeki yazı siyah ya da beyaz: üç dolguya en kötü kontrastı yüksek olan.
    /// Ölçü: <c>PaletKarsitligiTests</c>.
    /// </summary>
    public static string OnAccent(PaletteSeed seed)
    {
        var fills = new[] { seed.Renk1, seed.Renk2, seed.Renk3 };
        double Worst(double text) => fills.Min(fill =>
        {
            var tone = Luminance(fill);
            return (Math.Max(text, tone) + 0.05) / (Math.Min(text, tone) + 0.05);
        });
        return Worst(0) >= Worst(1) ? "#FF000000" : "#FFFFFFFF";
    }

    private static (int R, int G, int B) Parse(string hex)
    {
        var body = hex.TrimStart('#');
        if (body.Length == 8) body = body[2..];
        return (
            int.Parse(body[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(body.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(body.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static string Solid(string hex) => Alpha(hex, 0xFF);

    private static string Alpha(string hex, int alpha)
    {
        var (r, g, b) = Parse(hex);
        return $"#{alpha:X2}{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Ateş şeridi zeminin üstünde ısınır ama gövde yazısından uzaklaşır. Koyu palette 0,78 ile
    /// koyulaşır, açık palette aynı büyüklükte beyaza doğru gider.
    /// Ölçü: <c>ThemeBackdropTests.WarmingTheTitleBarDoesNotCostBodyTextContrast</c>.
    /// </summary>
    private static string Dim(string hex, bool light)
    {
        if (light) return Mix(hex, "#FFFFFF", 1 - 0.78);

        var (r, g, b) = Parse(hex);
        return $"#{(int)Math.Round(r * 0.78):X2}{(int)Math.Round(g * 0.78):X2}{(int)Math.Round(b * 0.78):X2}";
    }

    /// <summary>
    /// Atmosfer rengi zemini boyar ama karartısını değiştirmez: ton korunur, parlaklık ember
    /// karışımının parlaklığına iner. Ölçü: <c>ThemeBackdropTests.WarmingTheWorkspaceDoesNotCostBodyTextContrast</c>.
    /// </summary>
    private static string Shade(string hex, double target)
    {
        var (r, g, b) = Parse(hex);
        for (var k = 1.0; k > 0; k -= 0.005)
        {
            var dark = $"#{(int)(r * k):X2}{(int)(g * k):X2}{(int)(b * k):X2}";
            if (Luminance(dark) <= target) return dark;
        }
        return "#000000";
    }

    public static string Mix(string first, string second, double weight)
    {
        var (r1, g1, b1) = Parse(first);
        var (r2, g2, b2) = Parse(second);
        int Blend(int a, int b) => (int)Math.Round(a + ((b - a) * weight));
        return $"#{Blend(r1, r2):X2}{Blend(g1, g2):X2}{Blend(b1, b2):X2}";
    }

    public static double Luminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        double Channel(int value)
        {
            var part = value / 255.0;
            return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
    }
}
