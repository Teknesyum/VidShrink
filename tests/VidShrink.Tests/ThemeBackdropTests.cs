using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// T55: arka planın ısıtılması ve arkadaki anka kuşu silüeti. Ölçülen şey renk zevki
/// değil, sayılar: gövde metninin zeminle kontrast oranı, silüetin opaklığı ve
/// kapladığı alan.
/// </summary>
public sealed class ThemeBackdropTests
{
    private const double Canvas = 1600.0 * 1000.0;

    /// <summary>WCAG AA'nın gövde metni için istediği en düşük kontrast oranı.</summary>
    private const double BodyTextAaThreshold = 4.5;

    private static readonly XNamespace Ui = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string ThemePath = TipSources.ThemePath;

    /// <summary>T55 öncesi <c>WorkspaceBackground</c>'un durakları.</summary>
    private static readonly string[] BaselineWorkspaceStops =
    {
        "#FF08090D", "#FF09090E", "#FF0A090F", "#FF0B0910", "#FF0C0A11", "#FF0D0A12"
    };

    /// <summary>T55 öncesi <c>TitleBarBackground</c>'un durakları.</summary>
    private static readonly string[] BaselineTitleBarStops =
    {
        "#FF101217", "#FF111217", "#FF121217", "#FF121216", "#FF121117", "#FF131117", "#FF141117"
    };

    private static XElement Theme() => XDocument.Load(ThemePath).Root!;

    private static XElement Resource(string key) => Theme()
        .Elements()
        .Single(element => (string?)element.Attribute(X + "Key") == key);

    private static string Token(string key) => Resource(key).Value.Trim();

    private static IEnumerable<string> StopColours(string brushKey)
    {
        var colours = Theme().Elements()
            .Single(element => (string?)element.Attribute(X + "Key") == brushKey)
            .Elements(Ui + "GradientStop")
            .Select(stop => (string)stop.Attribute("Color")!);

        foreach (var colour in colours)
        {
            var key = colour.Trim();
            Assert.StartsWith("{StaticResource", key);
            yield return Token(key.Replace("{StaticResource", string.Empty).Trim(' ', '}'));
        }
    }

    private static (double R, double G, double B) Channels(string argb)
    {
        var hex = argb.TrimStart('#');
        var value = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
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

    /// <summary>Opak <paramref name="over"/> rengini <paramref name="alpha"/> oranıyla karıştırır.</summary>
    private static string Blend(string over, string under, double alpha)
    {
        var (or, og, ob) = Channels(over);
        var (ur, ug, ub) = Channels(under);
        var r = (int)Math.Round(255 * ((or * alpha) + (ur * (1 - alpha))));
        var g = (int)Math.Round(255 * ((og * alpha) + (ug * (1 - alpha))));
        var b = (int)Math.Round(255 * ((ob * alpha) + (ub * (1 - alpha))));
        return $"#FF{r:X2}{g:X2}{b:X2}";
    }

    private static double WorstContrast(IEnumerable<string> stops)
    {
        var body = Token("TextBodyColor");
        return stops.Min(stop => Contrast(body, stop));
    }

    private static IEnumerable<XElement> PhoenixDrawings() => Resource("WorkspaceBackground")
        .Descendants(Ui + "DrawingGroup")
        .Single(group => group.Attribute("Opacity") is not null)
        .Descendants(Ui + "GeometryDrawing");

    /// <summary>Ankayı boyayan fırçaların anahtarları; liste çizimden okunur, elle sayılmaz.</summary>
    private static IReadOnlyList<string> PhoenixBrushKeys() => PhoenixDrawings()
        .Select(drawing => ((string)drawing.Attribute("Brush")!).Trim())
        .Select(brush => brush.Replace("{StaticResource", string.Empty).Trim(' ', '}'))
        .Distinct()
        .ToList();

    /// <summary>
    /// Alev rampasının belirteçten gelen durakları. Saydama giden son duraklar
    /// atlanır: onlar zemini açmaz, kapatır.
    /// </summary>
    private static IEnumerable<string> PhoenixFlameColours() => PhoenixBrushKeys()
        .SelectMany(key => Resource(key).Elements(Ui + "GradientStop"))
        .Select(stop => ((string)stop.Attribute("Color")!).Trim())
        .Where(colour => colour.StartsWith("{StaticResource", StringComparison.Ordinal))
        .Select(colour => Token(colour.Replace("{StaticResource", string.Empty).Trim(' ', '}')));

    private static IReadOnlyList<string> PhoenixGeometries() => PhoenixDrawings()
        .Select(drawing => (string)drawing.Attribute("Geometry")!)
        .ToList();

    /// <summary>K1: çalışma alanının en açık noktası bile bugünkünden daha okunaklı.</summary>
    [Fact]
    public void WarmingTheWorkspaceDoesNotCostBodyTextContrast()
    {
        var before = WorstContrast(BaselineWorkspaceStops);
        var after = WorstContrast(StopColours("WorkspaceGradient"));

        Assert.True(after >= before,
            $"Çalışma alanı kontrastı {before:F2} → {after:F2} düştü.");
    }

    /// <summary>K1: başlık çubuğu için de aynı ölçü.</summary>
    [Fact]
    public void WarmingTheTitleBarDoesNotCostBodyTextContrast()
    {
        var before = WorstContrast(BaselineTitleBarStops);
        var after = WorstContrast(StopColours("TitleBarBackground"));

        Assert.True(after >= before,
            $"Başlık çubuğu kontrastı {before:F2} → {after:F2} düştü.");
    }

    /// <summary>K1: kırmızı sıcaklık ölçülebilir olmalı — kırmızı kanal artıyor, mavi geriliyor.</summary>
    [Fact]
    public void TheWorkspaceGradientLeansRed()
    {
        foreach (var stop in StopColours("WorkspaceGradient"))
        {
            var (r, g, b) = Channels(stop);
            Assert.True(r > b, $"{stop} kırmızıya değil maviye çalıyor.");
            Assert.True(r > g, $"{stop} kırmızıya değil yeşile çalıyor.");
        }
    }

    /// <summary>K2: durakların içinde ham onaltılık yok; renk belirteçten geliyor.</summary>
    [Fact]
    public void EveryBackdropColourComesFromAToken()
    {
        var brushes = new[] { "WorkspaceGradient", "TitleBarBackground" };
        foreach (var brush in brushes)
            foreach (var stop in Resource(brush).Elements(Ui + "GradientStop"))
                Assert.StartsWith("{StaticResource", ((string)stop.Attribute("Color")!).Trim());

        foreach (var drawing in Resource("WorkspaceBackground").Descendants(Ui + "GeometryDrawing"))
            Assert.StartsWith("{StaticResource", ((string)drawing.Attribute("Brush")!).Trim());
    }

    /// <summary>K3: silüet vektör. Depoda ikili dosyaya, resim kaynağına bağlanmıyor.</summary>
    [Fact]
    public void ThePhoenixIsDrawnNotLoaded()
    {
        var backdrop = Resource("WorkspaceBackground").ToString();

        Assert.DoesNotContain("avares://", backdrop, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageDrawing", backdrop, StringComparison.Ordinal);
        Assert.NotEmpty(PhoenixGeometries());
        foreach (var geometry in PhoenixGeometries())
            Assert.StartsWith("M ", geometry);
    }

    /// <summary>K4: görünürlük tek belirteçten sürülüyor.</summary>
    [Fact]
    public void ThePhoenixHasOneVisibilityKnob()
    {
        var group = Resource("WorkspaceBackground")
            .Descendants(Ui + "DrawingGroup")
            .Where(element => element.Attribute("Opacity") is not null)
            .ToList();

        Assert.Single(group);
        Assert.Equal("{StaticResource PhoenixOpacity}", ((string)group[0].Attribute("Opacity")!).Trim());
        Assert.InRange(double.Parse(Token("PhoenixOpacity"), CultureInfo.InvariantCulture), 0.0, 1.0);
    }

    /// <summary>
    /// K1+K3: silüetin üstünde de kontrast korunuyor. En kötü hâl, alev rampasının
    /// en parlak durağının çalışma alanının en açık durağı üzerine düştüğü nokta.
    /// <para>
    /// Eşik artık T55'ten kalan arka plan tabanı değil, WCAG AA gövde metni eşiği.
    /// O taban bir okunabilirlik kararı değildi; ısıtmadan önceki arka planın
    /// rastgele kalmış hâliydi ve ankayı görünür kılan her ayarı, metin fazlasıyla
    /// okunaklı kalsa bile reddediyordu.
    /// </para>
    /// </summary>
    [Fact]
    public void BodyTextStaysReadableOverThePhoenix()
    {
        var opacity = double.Parse(Token("PhoenixOpacity"), CultureInfo.InvariantCulture);
        var body = Token("TextBodyColor");

        var lightestFlame = PhoenixFlameColours().MaxBy(Luminance)!;
        var lightestGround = StopColours("WorkspaceGradient").MaxBy(Luminance)!;
        var over = Blend(lightestFlame, lightestGround, opacity);

        var ratio = Contrast(body, over);

        Assert.True(ratio >= BodyTextAaThreshold,
            $"Alevin en parlak noktası {over} üstünde kontrast {ratio:F2}:1, "
            + $"WCAG AA eşiği {BodyTextAaThreshold}:1.");
    }

    /// <summary>
    /// K3: silüet arka planda durur. Kapladığı alan ölçülüyor; tuval dolmuyor,
    /// ama kuş da bir leke kadar küçük kalmıyor.
    /// </summary>
    [Fact]
    public void ThePhoenixCoversABackdropSizedShareOfTheCanvas()
    {
        var share = AppHost.Run(() =>
        {
            var shapes = PhoenixGeometries().Select(Geometry.Parse).ToList();
            var hits = 0;
            var total = 0;

            for (var y = 4.0; y < 1000; y += 8)
                for (var x = 4.0; x < 1600; x += 8)
                {
                    total++;
                    var point = new Point(x, y);
                    if (shapes.Any(shape => shape.FillContains(point))) hits++;
                }

            return (double)hits / total;
        });

        Assert.InRange(share, 0.08, 0.40);
    }

    /// <summary>K5: silüet tuvalin dışına taşmıyor; kırpılma kenardan olur, yerleşimden değil.</summary>
    [Fact]
    public void ThePhoenixStaysInsideTheBackdropCanvas()
    {
        var bounds = AppHost.Run(() =>
        {
            var boxes = PhoenixGeometries().Select(path => Geometry.Parse(path).Bounds).ToList();
            return new Rect(
                boxes.Min(box => box.X),
                boxes.Min(box => box.Y),
                boxes.Max(box => box.Right) - boxes.Min(box => box.X),
                boxes.Max(box => box.Bottom) - boxes.Min(box => box.Y));
        });

        Assert.True(bounds.X >= 0 && bounds.Y >= 0, $"Silüet tuvalin dışına çıkıyor: {bounds}");
        Assert.True(bounds.Right <= 1600 && bounds.Bottom <= 1000, $"Silüet tuvalin dışına çıkıyor: {bounds}");
        Assert.True(bounds.Width * bounds.Height < Canvas, "Silüetin kutusu tuvalin tamamı.");
    }
}
