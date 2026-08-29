using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VidShrink.Tests;

/// <summary>
/// T71: anka artık düz tek renk değil, kırmızıdan sarıya giden bir alev rampasıyla
/// doluyor. Ölçülen şey renk zevki değil: iki yeni tonun mevcut kırmızıdan türediği,
/// rampanın yönü, ve en parlak alev noktasının üstünde gövde metninin kontrast oranı.
/// </summary>
public sealed class ThemeTokenTests
{
    private const double BodyTextAaThreshold = 4.5;

    private const double BaselinePhoenixOpacity = 0.06;

    private static readonly XNamespace Ui = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] FlameBrushKeys =
    {
        "PhoenixBodyFlame", "PhoenixWingFlameLeft", "PhoenixWingFlameRight", "PhoenixCrestFlame"
    };

    private static XElement Theme() => XDocument.Load(TipSources.ThemePath).Root!;

    private static XElement Resource(string key) => Theme()
        .Elements()
        .Single(element => (string?)element.Attribute(X + "Key") == key);

    private static string Token(string key) => Resource(key).Value.Trim();

    private static double Opacity() =>
        double.Parse(Token("PhoenixOpacity"), CultureInfo.InvariantCulture);

    private static (double R, double G, double B) Channels(string argb)
    {
        var value = uint.Parse(argb.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
    }

    private static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double Luminance(string argb)
    {
        var (r, g, b) = Channels(argb);
        return (0.2126 * Linear(r)) + (0.7152 * Linear(g)) + (0.0722 * Linear(b));
    }

    private static double Contrast(string over, string under)
    {
        var a = Luminance(over);
        var b = Luminance(under);
        var (light, dark) = a >= b ? (a, b) : (b, a);
        return (light + 0.05) / (dark + 0.05);
    }

    private static string Blend(string over, string under, double alpha)
    {
        var (or, og, ob) = Channels(over);
        var (ur, ug, ub) = Channels(under);
        var r = (int)Math.Round(255 * ((or * alpha) + (ur * (1 - alpha))));
        var g = (int)Math.Round(255 * ((og * alpha) + (ug * (1 - alpha))));
        var b = (int)Math.Round(255 * ((ob * alpha) + (ub * (1 - alpha))));
        return $"#FF{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>HSL: ton derece, doygunluk ve parlaklık 0..1.</summary>
    private static (double Hue, double Saturation, double Lightness) Hsl(string argb)
    {
        var (r, g, b) = Channels(argb);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;

        if (delta == 0) return (0, 0, lightness);

        var saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        double hue;
        if (max == r) hue = 60 * (((g - b) / delta % 6 + 6) % 6);
        else if (max == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);

        return (hue, saturation, lightness);
    }

    private static IReadOnlyList<XElement> FlameStops(string brushKey) =>
        Resource(brushKey).Elements(Ui + "GradientStop").ToList();

    private static string StopColour(XElement stop)
    {
        var raw = ((string)stop.Attribute("Color")!).Trim();
        return raw.StartsWith("{StaticResource", StringComparison.Ordinal)
            ? Token(raw.Replace("{StaticResource", string.Empty).Trim(' ', '}'))
            : raw;
    }

    private static double StopOffset(XElement stop) =>
        double.Parse((string)stop.Attribute("Offset")!, CultureInfo.InvariantCulture);

    private static IReadOnlyList<XElement> PhoenixDrawings() => Resource("WorkspaceBackground")
        .Descendants(Ui + "DrawingGroup")
        .Single(group => group.Attribute("Opacity") is not null)
        .Descendants(Ui + "GeometryDrawing")
        .ToList();

    /// <summary>
    /// K1: palete tam iki yeni renk belirteci girdi. Ember ailesi büyüdü, başka
    /// hiçbir renk eklenmedi.
    /// </summary>
    [Fact]
    public void TheWarmRampAddsExactlyTwoEmberTokens()
    {
        var emberTokens = Theme()
            .Elements(Ui + "Color")
            .Select(element => (string)element.Attribute(X + "Key")!)
            .Where(key => key.Contains("Ember", StringComparison.Ordinal))
            .ToList();

        Assert.Contains("EmberFlameColor", emberTokens);
        Assert.Contains("EmberBlazeColor", emberTokens);

        var known = new[]
        {
            "NeonEmberColor", "EmberFlameColor", "EmberBlazeColor",
            "EmberDeepColor", "EmberMidColor", "EmberEdgeColor",
            "EmberBarDeepColor", "EmberBarMidColor", "EmberBarEdgeColor"
        };

        Assert.Empty(emberTokens.Except(known));
        Assert.Equal(known.Length, emberTokens.Count);
    }

    /// <summary>
    /// K1: iki ton uydurulmadı, <c>NeonEmberColor</c>'dan hesaplandı. Doygunluk ve
    /// parlaklık kırmızıyla birebir aynı; yalnız ton eşit adımlarla sarıya yürüyor.
    /// </summary>
    [Fact]
    public void BothWarmTonesAreDerivedFromTheEmberRed()
    {
        var red = Hsl(Token("NeonEmberColor"));
        var flame = Hsl(Token("EmberFlameColor"));
        var blaze = Hsl(Token("EmberBlazeColor"));

        foreach (var tone in new[] { flame, blaze })
        {
            Assert.Equal(red.Saturation, tone.Saturation, 3);
            Assert.Equal(red.Lightness, tone.Lightness, 3);
        }

        var firstStep = ((flame.Hue - red.Hue) % 360 + 360) % 360;
        var secondStep = ((blaze.Hue - flame.Hue) % 360 + 360) % 360;

        Assert.Equal(firstStep, secondStep, 3);
        Assert.True(firstStep > 0, "Ton hiç yürümemiş; rampa tek renkten ibaret.");
        Assert.Equal(60.0, blaze.Hue, 3);
    }

    /// <summary>K2: anka artık düz dolguyla değil, dört alev gradyanıyla boyanıyor.</summary>
    [Fact]
    public void ThePhoenixIsPaintedWithGradientsNotAFlatFill()
    {
        var brushes = PhoenixDrawings()
            .Select(drawing => ((string)drawing.Attribute("Brush")!).Trim())
            .ToList();

        Assert.Equal(FlameBrushKeys.Length, brushes.Count);
        foreach (var key in FlameBrushKeys)
            Assert.Contains($"{{StaticResource {key}}}", brushes);

        foreach (var key in FlameBrushKeys)
            Assert.Equal("LinearGradientBrush", Resource(key).Name.LocalName);
    }

    /// <summary>
    /// K2: her alev tabanda sıcak, uçta soğuk. Rampa boyunca ton kırmızıya doğru
    /// düşüyor, son durak saydam.
    /// </summary>
    [Fact]
    public void EveryFlameRunsFromHotBaseToTransparentTip()
    {
        var redHue = Hsl(Token("NeonEmberColor")).Hue;

        foreach (var key in FlameBrushKeys)
        {
            var stops = FlameStops(key);
            Assert.True(stops.Count >= 3, $"{key} rampa değil, {stops.Count} durak var.");

            var offsets = stops.Select(StopOffset).ToList();
            Assert.Equal(offsets.OrderBy(value => value).ToList(), offsets);
            Assert.Equal(0.0, offsets[0]);
            Assert.Equal(1.0, offsets[^1]);

            Assert.Equal("Transparent", StopColour(stops[^1]));

            var heats = stops.Take(stops.Count - 1)
                .Select(stop => Hsl(StopColour(stop)).Hue)
                .Select(hue => hue > 180 ? hue - 360 : hue)
                .ToList();

            Assert.Equal(heats.OrderByDescending(value => value).ToList(), heats);
            Assert.Equal(redHue - 360, heats[^1], 3);
        }
    }

    /// <summary>K3: görünürlük arttı ve tek belirteçten sürülüyor, koda gömülü değil.</summary>
    [Fact]
    public void ThePhoenixOpacityRoseAndStaysInItsToken()
    {
        var group = Resource("WorkspaceBackground")
            .Descendants(Ui + "DrawingGroup")
            .Single(element => element.Attribute("Opacity") is not null);

        Assert.Equal("{StaticResource PhoenixOpacity}", ((string)group.Attribute("Opacity")!).Trim());
        Assert.True(Opacity() > BaselinePhoenixOpacity,
            $"Anka aydınlanmamış: opaklık {BaselinePhoenixOpacity} → {Opacity()}.");
        Assert.InRange(Opacity(), 0.0, 1.0);
    }

    /// <summary>
    /// K4: kontrast ortalamada değil, en parlak alev noktasında ölçülüyor. O nokta
    /// rampanın en açık durağının, çalışma alanının en açık durağı üstüne
    /// <c>PhoenixOpacity</c> ile düştüğü piksel. Gövde metni orada da WCAG AA'yı geçmeli.
    /// </summary>
    [Fact]
    public void BodyTextClearsAaOverTheBrightestFlamePixel()
    {
        var body = Token("TextBodyColor");

        var workspaceLightest = Resource("WorkspaceGradient")
            .Elements(Ui + "GradientStop")
            .Select(StopColour)
            .MaxBy(Luminance)!;

        var flameLightest = FlameBrushKeys
            .SelectMany(FlameStops)
            .Select(StopColour)
            .Where(colour => colour.StartsWith('#'))
            .MaxBy(Luminance)!;

        var brightest = Blend(flameLightest, workspaceLightest, Opacity());
        var ratio = Contrast(body, brightest);

        Assert.True(ratio >= BodyTextAaThreshold,
            $"En parlak alev noktası {brightest} üstünde kontrast {ratio:F2}:1, "
            + $"AA eşiği {BodyTextAaThreshold}:1.");
    }

    /// <summary>K5: anka çiziminde ve alev rampalarında çıplak onaltılık renk yok.</summary>
    [Fact]
    public void NoBareHexHidesInThePhoenix()
    {
        var bareHex = new Regex("#[0-9A-Fa-f]{6,8}");

        foreach (var drawing in PhoenixDrawings())
            Assert.StartsWith("{StaticResource", ((string)drawing.Attribute("Brush")!).Trim());

        foreach (var key in FlameBrushKeys)
        {
            var markup = Resource(key).ToString();
            Assert.False(bareHex.IsMatch(markup), $"{key} içinde çıplak renk var: {markup}");
        }
    }
}
