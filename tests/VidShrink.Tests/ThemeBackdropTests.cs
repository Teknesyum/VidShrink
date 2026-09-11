using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// T55: arka planın ısıtılması; kuş silüeti sonradan kaldırıldı, parlama ve kıvılcımlar
/// kaldı. Ölçülen şey renk zevki değil, sayılar: gövde metninin zeminle kontrast oranı,
/// grubun opaklığı ve kapladığı alan.
/// </summary>
public sealed class ThemeBackdropTests
{
    private const double Canvas = 1600.0 * 1000.0;

    /// <summary>WCAG AA'nın gövde metni için istediği en düşük kontrast oranı.</summary>
    private const double BodyTextAaThreshold = 4.5;

    private static readonly XNamespace Ui = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

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

    private static readonly string ControlsPath =
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Controls.axaml");

    /// <summary><c>Controls.axaml</c> icindeki bir denetim temasinin bir kurucusu.</summary>
    private static string ControlSetter(string themeKey, string property) =>
        XDocument.Load(ControlsPath).Descendants(Ui + "ControlTheme")
            .Single(theme => (string?)theme.Attribute(X + "Key") == themeKey)
            .Elements(Ui + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == property)
            .Attribute("Value")!.Value.Trim();

    private static double PanelOpacity() =>
        double.Parse(Token("PanelSurfaceOpacity"), CultureInfo.InvariantCulture);

    /// <summary>Bir fircayla boyanan cizimlerin yollari.</summary>
    private static IReadOnlyList<string> PartGeometries(string brushKey) => PhoenixDrawings()
        .Where(drawing => ((string)drawing.Attribute("Brush")!).Trim() == $"{{StaticResource {brushKey}}}")
        .Select(drawing => (string)drawing.Attribute("Geometry")!)
        .ToList();

    private static XElement Resource(string key) => ThemeSources.Resource(key);

    private static string Token(string key) => ThemeSources.Token(key);

    private static IEnumerable<string> StopColours(string brushKey)
    {
        var colours = ThemeSources.Resources()
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

    private static readonly string[] GlowBrushKeys =
    {
        "PhoenixGlowOuter", "PhoenixGlowMid", "PhoenixGlowInner"
    };

    private const string SparkBrushKey = "PhoenixEmberSpark";

    private static double Dist(Point a, Point b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    /// <summary>Bütün yolların ortak sınır kutusu.</summary>
    private static Rect BoundsOf(IEnumerable<string> paths)
    {
        var boxes = paths.Select(path => Geometry.Parse(path).Bounds).ToList();
        return new Rect(
            boxes.Min(box => box.X),
            boxes.Min(box => box.Y),
            boxes.Max(box => box.Right) - boxes.Min(box => box.X),
            boxes.Max(box => box.Bottom) - boxes.Min(box => box.Y));
    }

    /// <summary>
    /// K3 (tur 3): kor parçacıkları damla değil. Her biri düz kenarlarla çizilmiş, en az beş
    /// köşeli, kenar uzunlukları eşit olmayan düzensiz bir çokgen; köşe sayıları farklılaşıyor
    /// ve hiçbir ikisi birbirinin ölçekli kopyası değil. Yukarı doğru savruluyorlar: üst yarıda
    /// alt yarıdakinden çok sayıda kor var.
    /// </summary>
    [Fact]
    public void EmbersAreIrregularShardsThatDriftUpward()
    {
        var paths = PartGeometries(SparkBrushKey);
        Assert.True(paths.Count >= 12, $"Kor parçacığı {paths.Count} tane.");

        var corners = paths.Select(path => Regex.Matches(path, @"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)")
            .Select(match => new Point(
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)))
            .ToList()).ToList();

        foreach (var shard in corners)
            Assert.True(shard.Count >= 5, $"Kor {shard.Count} köşeli; düzensiz çokgen değil.");

        Assert.DoesNotContain(paths, path => path.Contains('C', StringComparison.Ordinal));
        Assert.True(corners.Select(shard => shard.Count).Distinct().Count() >= 3,
            "Bütün korlar aynı köşe sayısında.");

        var signatures = new List<string>();
        foreach (var shard in corners)
        {
            var edges = shard
                .Select((point, index) => Dist(point, shard[(index + 1) % shard.Count]))
                .ToList();

            Assert.True(edges.Max() / edges.Min() >= 1.2,
                $"Kor kenarları eşit uzunlukta: {edges.Max():F1}/{edges.Min():F1}.");

            var cx = shard.Average(point => point.X);
            var cy = shard.Average(point => point.Y);
            var radii = shard.Select(point => Dist(point, new Point(cx, cy))).ToList();
            var mean = radii.Average();

            signatures.Add(shard.Count + ":" + string.Join(
                "|", radii.Select(radius => Math.Round(radius / mean, 2)).OrderBy(value => value)));
        }

        Assert.Equal(signatures.Count, signatures.Distinct().Count());

        var (boxes, figure) = AppHost.Run(() => (
            paths.Select(path => Geometry.Parse(path).Bounds).ToList(),
            BoundsOf(PhoenixGeometries())));

        var sizes = boxes.Select(box => Math.Max(box.Width, box.Height)).ToList();
        Assert.True(sizes.Max() / sizes.Min() >= 3.0,
            $"En büyük kor {sizes.Max():F1}, en küçük {sizes.Min():F1}; fark "
            + $"{sizes.Max() / sizes.Min():F2} kat, eşik 3.");

        var middle = figure.Y + (figure.Height / 2);
        var above = boxes.Count(box => box.Center.Y < middle);
        var below = boxes.Count - above;
        Assert.True(above > below, $"Üst yarıda {above}, alt yarıda {below} kor: yukarı savrulmuyorlar.");

        var lanes = boxes.GroupBy(box => Math.Round(box.Center.X)).Max(lane => lane.Count());
        var rows = boxes.GroupBy(box => Math.Round(box.Center.Y)).Max(row => row.Count());
        Assert.True(lanes <= 4, $"{lanes} kor aynı x'te dizilmiş.");
        Assert.True(rows <= 4, $"{rows} kor aynı y'de dizilmiş.");
    }

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
            Assert.Matches("^(F1 )?M ", geometry);
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

    [Fact]
    public void TheGlowCoversTheCanvasAndTheSparksStaySmall()
    {
        double Share(IReadOnlyList<string> paths) => AppHost.Run(() =>
        {
            var shapes = paths.Select(Geometry.Parse).ToList();
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

        var glowPaths = GlowBrushKeys.SelectMany(PartGeometries).ToHashSet();
        var rest = PhoenixGeometries().Where(path => !glowPaths.Contains(path)).ToList();

        Assert.InRange(Share(rest), 0.0, 0.01);
        Assert.InRange(Share(glowPaths.ToList()), 0.10, 0.40);
    }

    public static readonly TheoryData<string> BirdBrushKeys = new()
    {
        "PhoenixWingFlameFar", "PhoenixWingFlameNear", "PhoenixTailFlame", "PhoenixBodyFlame", "PhoenixCrestFlame"
    };

    [Theory]
    [MemberData(nameof(BirdBrushKeys))]
    public void TheBirdBrushIsGoneFromTheTheme(string key)
    {
        var theme = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Theme.axaml"));

        Assert.DoesNotContain(key, theme, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheGlowAndTheSparksPaintTheBackdropGroup()
    {
        Assert.Equal(
            GlowBrushKeys.Append(SparkBrushKey).OrderBy(key => key),
            PhoenixBrushKeys().OrderBy(key => key));
    }

    [Fact]
    public void TheGlowFadesOutWithoutAVisibleEdge()
    {
        foreach (var key in GlowBrushKeys)
        {
            var brush = Resource(key);
            Assert.Equal("RadialGradientBrush", brush.Name.LocalName);

            var stops = brush.Elements(Ui + "GradientStop").ToList();
            Assert.Equal("Transparent", ((string)stops[^1].Attribute("Color")!).Trim());
            Assert.Equal(1.0, double.Parse((string)stops[^1].Attribute("Offset")!, CultureInfo.InvariantCulture));
            Assert.NotNull(brush.Attribute("Opacity"));
        }

        var order = PhoenixDrawings()
            .Select((drawing, index) => (Index: index,
                Brush: ((string)drawing.Attribute("Brush")!).Trim()
                    .Replace("{StaticResource", string.Empty).Trim(' ', '}')))
            .ToList();

        Assert.True(
            order.Where(item => GlowBrushKeys.Contains(item.Brush)).Max(item => item.Index)
            < order.Where(item => item.Brush == SparkBrushKey).Min(item => item.Index),
            "Parlama kıvılcımların önüne geçmiş.");

        var boxes = AppHost.Run(() => GlowBrushKeys
            .Select(key => BoundsOf(PartGeometries(key)))
            .ToList());

        foreach (var box in boxes)
        {
            Assert.Equal(box.Width, box.Height, 1);
            Assert.Equal(boxes[0].Center.X, box.Center.X, 1);
            Assert.Equal(boxes[0].Center.Y, box.Center.Y, 1);
        }
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

    /// <summary>
    /// K3: panel zemini artik donuk degil. Saydamlik bir opaklik belirtecinden geliyor,
    /// <c>Panel</c> temasi o belirteci tasiyan fircayi okuyor ve palete renk eklenmedi:
    /// fircanin rengi mevcut <c>SurfaceToneColor</c>.
    /// </summary>
    [Fact]
    public void ThePanelBackgroundIsDrivenByAnOpacityToken()
    {
        Assert.Equal("{StaticResource PanelSurface}", ControlSetter("Panel", "Background"));

        var brush = Resource("PanelSurface");
        Assert.Equal("SolidColorBrush", brush.Name.LocalName);
        Assert.Equal("{StaticResource SurfaceToneColor}", ((string)brush.Attribute("Color")!).Trim());
        Assert.Equal("{StaticResource PanelSurfaceOpacity}", ((string)brush.Attribute("Opacity")!).Trim());

        Assert.InRange(PanelOpacity(), 0.75, 1.0);
        Assert.True(PanelOpacity() < 1.0, "Panel hala donuk; arkadaki anka gorunmuyor.");
    }

    /// <summary>
    /// K4: en kotu hal. Alev rampasinin en parlak duragi, calisma alaninin en acik duragi
    /// uzerinde ankanin opakligiyla duruyor; panelin saydam zemini onun ustune biniyor ve
    /// govde metni de panelin ustunde. Olculen renklerin hepsi cizimden okunuyor.
    /// </summary>
    [Fact]
    public void BodyTextStaysReadableOverThePanelThatShowsTheFlame()
    {
        var lightestFlame = PhoenixFlameColours().MaxBy(Luminance)!;
        var lightestGround = StopColours("WorkspaceGradient").MaxBy(Luminance)!;

        var backdrop = Blend(lightestFlame, lightestGround,
            double.Parse(Token("PhoenixOpacity"), CultureInfo.InvariantCulture));
        var panel = Blend(Token("SurfaceToneColor"), backdrop, PanelOpacity());

        var ratio = Contrast(Token("TextBodyColor"), panel);

        Assert.True(ratio >= BodyTextAaThreshold,
            $"Alevin en parlak noktasini gosteren panel zemini {panel} ustunde kontrast "
            + $"{ratio:F2}:1, WCAG AA esigi {BodyTextAaThreshold}:1.");
    }
}
