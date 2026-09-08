using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.PaletteGen;

/// <summary>
/// Palet uretici. Tek bir kaynaktan — <c>Themes/Palette/Neon.axaml</c> — yirmi kardes
/// palet yazar. Renkler uydurulmuyor: her palet Neon'un kendi renklerinin ton cemberinde
/// dondurulmus hali, doygunluk ve aciklik oldugu gibi kaliyor. Boylece yirmi kombinasyon
/// da ayni tasarim, farkli ton; ve kaynak palet degisirse hepsi yeniden uretilir.
/// </summary>
internal static class Program
{
    private static readonly Regex ColorLine = new(
        @"(?<open><(?<tag>Color|BoxShadows)\s+x:Key=""(?<key>[^""]+)"">)(?<body>[^<]*)(?<close></\k<tag>>)",
        RegexOptions.Compiled);

    private static readonly Regex Hex = new(@"#[0-9A-Fa-f]{8}", RegexOptions.Compiled);

    /// <summary>
    /// Yirmi ton, on sekizer derece. Ad tonun kendisini soyluyor; sifirinci basamak
    /// kaynagin kendisi oldugu icin adi da Neon.
    /// </summary>
    internal static readonly string[] Names =
    {
        "Neon", "Lagoon", "Azure", "Cobalt", "Indigo",
        "Violet", "Orchid", "Magenta", "Rose", "Crimson",
        "Scarlet", "Flare", "Amber", "Gold", "Citron",
        "Lime", "Fern", "Emerald", "Jade", "Teal"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Kullanim: vidshrink-palettegen <Themes/Palette klasoru>");
            return 2;
        }

        var folder = args[0];
        var source = Path.Combine(folder, Names[0] + ".axaml");

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Kaynak palet yok: {source}");
            return 2;
        }

        var text = File.ReadAllText(source);
        var step = 360.0 / Names.Length;

        for (var index = 1; index < Names.Length; index++)
        {
            var turned = Turn(text, step * index);
            var target = Path.Combine(folder, Names[index] + ".axaml");
            File.WriteAllText(target, turned, new UTF8Encoding(false));
            Console.WriteLine($"{Names[index]}.axaml  +{step * index:0}°");
        }

        return 0;
    }

    private static string Turn(string text, double degrees)
        => ColorLine.Replace(text, match =>
            match.Groups["open"].Value +
            Hex.Replace(match.Groups["body"].Value, hex => TurnHex(hex.Value, degrees)) +
            match.Groups["close"].Value);

    private static string TurnHex(string hex, double degrees)
    {
        var alpha = byte.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var red = byte.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var green = byte.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var blue = byte.Parse(hex.Substring(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var (hue, saturation, lightness) = ToHsl(red, green, blue);
        var (r, g, b) = ToRgb((hue + degrees) % 360.0, saturation, lightness);

        return $"#{alpha:X2}{r:X2}{g:X2}{b:X2}";
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(byte red, byte green, byte blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;

        if (Math.Abs(max - min) < 1e-9) return (0.0, 0.0, lightness);

        var span = max - min;
        var saturation = lightness > 0.5 ? span / (2.0 - max - min) : span / (max + min);

        double hue;
        if (Math.Abs(max - r) < 1e-9) hue = (g - b) / span + (g < b ? 6.0 : 0.0);
        else if (Math.Abs(max - g) < 1e-9) hue = (b - r) / span + 2.0;
        else hue = (r - g) / span + 4.0;

        return (hue * 60.0, saturation, lightness);
    }

    private static (byte R, byte G, byte B) ToRgb(double hue, double saturation, double lightness)
    {
        if (saturation <= 0.0)
        {
            var flat = Round(lightness);
            return (flat, flat, flat);
        }

        var second = lightness < 0.5
            ? lightness * (1.0 + saturation)
            : lightness + saturation - lightness * saturation;
        var first = 2.0 * lightness - second;
        var turn = hue / 360.0;

        return (
            Round(Channel(first, second, turn + 1.0 / 3.0)),
            Round(Channel(first, second, turn)),
            Round(Channel(first, second, turn - 1.0 / 3.0)));
    }

    private static double Channel(double first, double second, double turn)
    {
        if (turn < 0.0) turn += 1.0;
        if (turn > 1.0) turn -= 1.0;

        if (turn < 1.0 / 6.0) return first + (second - first) * 6.0 * turn;
        if (turn < 1.0 / 2.0) return second;
        if (turn < 2.0 / 3.0) return first + (second - first) * (2.0 / 3.0 - turn) * 6.0;
        return first;
    }

    private static byte Round(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
}
