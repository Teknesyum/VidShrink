using System.Globalization;
using System.Text.RegularExpressions;
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

    /// <summary>T72 oncesi ankayi cizen dort yol.</summary>
    private static readonly string[] BaselinePhoenixGeometries =
    {
        "M 800,168 C 838,180 856,214 852,252 C 848,290 838,308 834,330 C 880,380 900,432 898,500 C 894,570 872,640 848,700 C 872,760 880,820 868,884 C 848,838 836,796 826,752 C 822,806 814,846 800,890 C 786,846 778,806 774,752 C 764,796 752,838 732,884 C 720,820 728,760 752,700 C 728,640 706,570 702,500 C 700,432 720,380 766,330 C 762,308 752,290 748,252 C 744,214 762,180 800,168 Z",
        "M 750,440 C 610,306 424,196 240,130 C 288,222 316,290 344,352 C 372,364 402,420 434,486 C 462,498 496,548 534,600 C 566,614 604,650 646,684 C 690,700 726,702 754,694 C 762,610 758,520 750,440 Z",
        "M 850,440 C 990,306 1176,196 1360,130 C 1312,222 1284,290 1256,352 C 1228,364 1198,420 1166,486 C 1138,498 1104,548 1066,600 C 1034,614 996,650 954,684 C 910,700 874,702 846,694 C 838,610 842,520 850,440 Z",
        "M 800,166 C 812,118 838,82 872,50 C 862,102 846,138 826,174 Z M 330,192 C 306,132 296,86 300,36 C 328,90 348,140 360,190 Z M 470,246 C 452,196 448,158 456,116 C 478,164 492,206 500,246 Z M 1270,192 C 1294,132 1304,86 1300,36 C 1272,90 1252,140 1240,190 Z M 1130,246 C 1148,196 1152,158 1144,116 C 1122,164 1108,206 1100,246 Z"
    };

    private static XElement Theme() => XDocument.Load(ThemePath).Root!;

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

    /// <summary>Bir fircayla boyanan butun yollarin ortak sinir kutusu.</summary>
    private static Rect PartBounds(string brushKey) => AppHost.Run(() =>
    {
        var boxes = PartGeometries(brushKey).Select(path => Geometry.Parse(path).Bounds).ToList();
        return new Rect(
            boxes.Min(box => box.X),
            boxes.Min(box => box.Y),
            boxes.Max(box => box.Right) - boxes.Min(box => box.X),
            boxes.Max(box => box.Bottom) - boxes.Min(box => box.Y));
    });

    /// <summary>Yolu x=800 ekseninde aynalar.</summary>
    private static string Mirror(string geometry) => MirrorPoint.Replace(geometry, match =>
    {
        var x = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return $"{Math.Round(1600 - x, 1).ToString(CultureInfo.InvariantCulture)},{match.Groups[2].Value}";
    });

    private static readonly Regex MirrorPoint = new(@"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)");

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

    /// <summary>Kanat, kuyruk ve tepelik tüyleri — ankanın birincil tüyleri.</summary>
    private static readonly string[] FeatherBrushKeys =
    {
        "PhoenixWingFlameNear", "PhoenixWingFlameFar", "PhoenixTailFlame", "PhoenixCrestFlame"
    };

    private static readonly string[] GlowBrushKeys =
    {
        "PhoenixGlowOuter", "PhoenixGlowMid", "PhoenixGlowInner"
    };

    private const string SparkBrushKey = "PhoenixEmberSpark";

    /// <summary>Bir tüyden okunan sayılar.</summary>
    private sealed record FeatherProbe(
        string Brush, int EdgeTurns, int Fringes,
        double LongestFringe, double ShortestFringe, double SpreadDegrees, double Vane);

    private static readonly Lazy<IReadOnlyList<FeatherProbe>> FeatherProbes = new(MeasureFeathers);

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
    /// Her birincil tüyü kendi ekseninde tarar. Eksen, tüyün gövde merkezine en yakın dolu
    /// noktasından (kök) en uzak dolu noktasına (uç) çizilir; ölçüler eksene dik tarama
    /// çizgilerinin kestiği <b>ayrı dolu aralıklardan</b> okunur.
    /// <list type="bullet">
    /// <item><b>Kenar dalgası:</b> eksenin %10–%75 bandında 16 dik tarama; en uzun aralığın
    /// genişliği bir dizi kurar, dizideki yön değişimi sayısı kenarın kaç kez artıp azaldığıdır.
    /// Düz kenarlı tüyde bu sayı sıfırdır.</item>
    /// <item><b>Saçak sayısı:</b> uç bölgesindeki en yüksek ayrı aralık sayısı.</item>
    /// <item><b>Saçak ucu:</b> dışa gidildikçe kollar teker teker biter. Bir aralık kaybolup
    /// sonraki altı istasyonda da geri gelmiyorsa orası o saçağın ucudur. Saçak boyu ucun
    /// yarılma noktasına eksen üzerindeki uzaklığı, yönü ise ucun yarılma noktasına göre
    /// açısıdır.</item>
    /// </list>
    /// </summary>
    private static IReadOnlyList<FeatherProbe> MeasureFeathers() => AppHost.Run(() =>
    {
        var centre = Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single()).Bounds.Center;
        var probes = new List<FeatherProbe>();

        foreach (var key in FeatherBrushKeys)
            foreach (var path in PartGeometries(key))
            {
                var shape = Geometry.Parse(path);
                var box = shape.Bounds;

                Point tip = default, root = default;
                double far = -1, near = double.MaxValue;

                for (var y = box.Y; y <= box.Bottom; y += 4)
                    for (var x = box.X; x <= box.Right; x += 4)
                    {
                        var point = new Point(x, y);
                        if (!shape.FillContains(point)) continue;

                        var span = Dist(point, centre);
                        if (span > far) { far = span; tip = point; }
                        if (span < near) { near = span; root = point; }
                    }

                var length = Dist(tip, root);
                var ux = (tip.X - root.X) / length;
                var uy = (tip.Y - root.Y) / length;

                List<(double Lo, double Hi, double Mid, double Width)> Runs(double share)
                {
                    var ax = root.X + (ux * length * share);
                    var ay = root.Y + (uy * length * share);
                    var found = new List<(double, double, double, double)>();
                    double? open = null;

                    for (var offset = -130.0; offset <= 130.0; offset += 1)
                    {
                        var on = shape.FillContains(new Point(ax - (uy * offset), ay + (ux * offset)));
                        if (on) open ??= offset;
                        else if (open is { } lo)
                        {
                            if (offset - lo >= 2) found.Add((lo, offset, (lo + offset) / 2, offset - lo));
                            open = null;
                        }
                    }

                    if (open is { } last && 130 - last >= 2)
                        found.Add((last, 130, (last + 130) / 2, 130 - last));

                    return found;
                }

                var widths = new List<double>();
                for (var i = 0; i < 16; i++)
                {
                    var runs = Runs(0.10 + (0.65 * i / 15));
                    widths.Add(runs.Count == 0 ? 0 : runs.Max(run => run.Width));
                }

                var turns = 0;
                var direction = 0;
                for (var i = 1; i < widths.Count; i++)
                {
                    var step = Math.Sign(widths[i] - widths[i - 1]);
                    if (step != 0 && direction != 0 && step != direction) turns++;
                    if (step != 0) direction = step;
                }

                var stations = new List<(double Share, List<(double Lo, double Hi, double Mid, double Width)> Runs)>();
                for (var share = 0.55; share <= 0.998; share += 0.006)
                    stations.Add((share, Runs(share)));

                var fringes = stations.Max(station => station.Runs.Count);
                var start = stations.FindIndex(station => station.Runs.Count == fringes);
                while (start > 0 && stations[start - 1].Runs.Count >= 2) start--;
                var split = stations[start].Share;

                const int Ahead = 6;
                var tips = new List<(double Share, double Mid)>();
                for (var i = start; i < stations.Count - 1; i++)
                    foreach (var run in stations[i].Runs)
                    {
                        var back = false;
                        for (var k = i + 1; k < Math.Min(stations.Count, i + 1 + Ahead) && !back; k++)
                            back = stations[k].Runs.Any(other => other.Hi >= run.Lo && other.Lo <= run.Hi);

                        if (!back) tips.Add((stations[i].Share, run.Mid));
                    }

                foreach (var run in stations[^1].Runs) tips.Add((stations[^1].Share, run.Mid));

                var arms = tips
                    .Select(item => (Len: (item.Share - split) * length, item.Mid))
                    .Where(item => item.Len > 3)
                    .OrderByDescending(item => item.Len)
                    .Take(fringes)
                    .Select(item => (item.Len,
                        Angle: Math.Atan2(item.Mid, Math.Max(item.Len, 1)) * 180 / Math.PI))
                    .ToList();

                double spread = 0;
                for (var i = 0; i < arms.Count; i++)
                    for (var j = i + 1; j < arms.Count; j++)
                        spread = Math.Max(spread, Math.Abs(arms[i].Angle - arms[j].Angle));

                var midRuns = Runs(0.45);

                probes.Add(new FeatherProbe(
                    key, turns, fringes,
                    arms.Count == 0 ? 0 : arms.Max(arm => arm.Len),
                    arms.Count == 0 ? 0 : arms.Min(arm => arm.Len),
                    spread,
                    midRuns.Count == 0 ? 0 : midRuns.Max(run => run.Width)));
            }

        return (IReadOnlyList<FeatherProbe>)probes;
    });

    /// <summary>
    /// K1: figür açılmış kanat okuyor, duran kuş değil. Kanat ucundan kanat ucuna genişlik
    /// gövdenin boyunu 1,6 kattan fazla aşıyor; çizimin tamamının sınır kutusu da aynı oranı
    /// tutuyor — T72'de o kutu 987×948, yani 1,04'tü.
    /// </summary>
    [Fact]
    public void TheWingspanDominatesTheBody()
    {
        var (spread, figure, body) = AppHost.Run(() => (
            BoundsOf(PartGeometries("PhoenixWingFlameNear").Concat(PartGeometries("PhoenixWingFlameFar"))),
            BoundsOf(PhoenixGeometries()),
            Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single()).Bounds));

        Assert.True(spread.Width >= body.Height * 1.6,
            $"Kanat açıklığı {spread.Width:F0}, gövde boyu {body.Height:F0}; "
            + $"oran {spread.Width / body.Height:F2}, eşik 1,60.");

        Assert.True(figure.Width >= figure.Height * 1.6,
            $"Çizimin kutusu {figure.Width:F0}×{figure.Height:F0}; "
            + $"oran {figure.Width / figure.Height:F2}, eşik 1,60.");
    }

    /// <summary>
    /// K1 (tur 3): hiçbir tüyün kenarı düz değil. Tüy ekseni boyunca alınan on altı dik
    /// taramanın genişlik dizisi en az üç kez yön değiştiriyor; tek yönde monoton daralan
    /// tüy yok. Düz kenarlı bir bıçak dilimi bu ölçüde sıfır verir.
    /// </summary>
    [Fact]
    public void NoFeatherEdgeRunsStraight()
    {
        var probes = FeatherProbes.Value;
        var straight = probes.Where(probe => probe.EdgeTurns < 3).ToList();

        Assert.True(straight.Count == 0,
            $"{straight.Count} tüyün kenarı düz: "
            + string.Join(", ", straight.Select(probe => $"{probe.Brush} {probe.EdgeTurns} dönüş")));

        Assert.DoesNotContain(probes, probe => probe.EdgeTurns == 0);
    }

    /// <summary>
    /// K2 (tur 3): her tüyün ucu eşit olmayan iki-dört saçağa ayrılıyor, saçaklar aynı yöne
    /// bakmıyor ve en uzun saçak en kısanın en az 1,8 katı. Tur 1'in "en az üç kılcal"
    /// kazanımı da duruyor: ayrı aralık sayısı üçün altına inmiyor.
    /// </summary>
    [Fact]
    public void FeatherTipsSplitIntoUnevenFringes()
    {
        var probes = FeatherProbes.Value;
        Assert.True(probes.Count >= 24, $"Birincil tüy sayısı {probes.Count}.");

        foreach (var key in FeatherBrushKeys)
            Assert.Contains(probes, probe => probe.Brush == key);

        var miscounted = probes.Where(probe => probe.Fringes is < 3 or > 4).ToList();
        Assert.True(miscounted.Count == 0,
            $"{miscounted.Count} tüyün saçağı 3-4 aralığının dışında: "
            + string.Join(", ", miscounted.Select(probe => $"{probe.Brush} {probe.Fringes}")));

        var even = probes
            .Where(probe => probe.ShortestFringe <= 0 || probe.LongestFringe < probe.ShortestFringe * 1.8)
            .ToList();
        Assert.True(even.Count == 0,
            $"{even.Count} tüyün saçakları eşit uzunlukta: "
            + string.Join(", ", even.Select(probe =>
                $"{probe.Brush} {probe.LongestFringe:F0}/{probe.ShortestFringe:F0}")));

        var aligned = probes.Where(probe => probe.SpreadDegrees < 15).ToList();
        Assert.True(aligned.Count == 0,
            $"{aligned.Count} tüyün saçakları aynı yöne bakıyor: "
            + string.Join(", ", aligned.Select(probe => $"{probe.Brush} {probe.SpreadDegrees:F0}°")));
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

    /// <summary>
    /// K4 (tur 3): parlamanın kenarı hiçbir yerde görünmüyor. Üç katman da merkezden dışa
    /// solan <c>RadialGradientBrush</c> ve son durakları tam saydam; çizgisel bir rampa
    /// olsaydı şeklin bir kenarı opak kalır, arkada kahverengi bir leke olarak seçilirdi.
    /// Katmanlar tüylerden geniş ve tüylerin arkasında duruyor.
    /// </summary>
    [Fact]
    public void TheGlowFadesOutWithoutAVisibleEdge()
    {
        foreach (var key in GlowBrushKeys)
        {
            var brush = Resource(key);
            Assert.Equal("RadialGradientBrush", brush.Name.LocalName);

            var stops = brush.Elements(Ui + "GradientStop").ToList();
            var last = ((string)stops[^1].Attribute("Color")!).Trim();
            Assert.Equal("Transparent", last);
            Assert.Equal(1.0, double.Parse((string)stops[^1].Attribute("Offset")!, CultureInfo.InvariantCulture));

            Assert.NotNull(brush.Attribute("Opacity"));
        }

        foreach (var key in FeatherBrushKeys)
            Assert.Null(Resource(key).Attribute("Opacity"));

        var order = PhoenixDrawings()
            .Select((drawing, index) => (Index: index,
                Brush: ((string)drawing.Attribute("Brush")!).Trim()
                    .Replace("{StaticResource", string.Empty).Trim(' ', '}')))
            .ToList();

        var glow = order.Where(item => GlowBrushKeys.Contains(item.Brush)).ToList();
        var feathers = order.Where(item => FeatherBrushKeys.Contains(item.Brush)).ToList();

        Assert.Equal(GlowBrushKeys.Length, glow.Select(item => item.Brush).Distinct().Count());
        Assert.True(glow.Max(item => item.Index) < feathers.Min(item => item.Index),
            "Parlama katmanı alev tüylerinin önüne geçmiş.");

        var (widest, glowBoxes, body) = AppHost.Run(() => (
            FeatherBrushKeys.SelectMany(PartGeometries)
                .Select(path => Geometry.Parse(path).Bounds)
                .Max(box => Math.Max(box.Width, box.Height)),
            GlowBrushKeys.ToDictionary(key => key, key => BoundsOf(PartGeometries(key))),
            Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single()).Bounds));

        var outer = glowBoxes["PhoenixGlowOuter"];
        Assert.True(Math.Min(outer.Width, outer.Height) > widest,
            $"En dış parlama {outer.Width:F0}×{outer.Height:F0}, en geniş tek tüy {widest:F0}; "
            + "parlama tüy kutusunu aşmıyor.");

        var widestVane = FeatherProbes.Value.Max(probe => probe.Vane);
        foreach (var (key, box) in glowBoxes)
        {
            Assert.True(Math.Min(box.Width, box.Height) > widestVane,
                $"{key} en geniş tüy yaprağından ({widestVane:F0}) dar: {box.Width:F0}×{box.Height:F0}.");
            Assert.True(Math.Min(box.Width, box.Height) > body.Width,
                $"{key} gövdenin genişliğinden ({body.Width:F0}) dar.");
            Assert.True(box.Contains(body.Center), $"{key} gövdenin ardında durmuyor.");
        }
    }

    /// <summary>
    /// K5 (tur 3): tepelik kapalı bir blok değil. Tüyler geriye — gagayla ters yöne —
    /// savruluyor ve hiçbir ikisi birbirine değmiyor.
    /// </summary>
    [Fact]
    public void TheCrestSweepsBackAsSeparateFeathers()
    {
        var crest = PartGeometries("PhoenixCrestFlame");
        Assert.True(crest.Count >= 3, $"Tepelik {crest.Count} tüy.");

        var touching = AppHost.Run(() =>
        {
            var shapes = crest.Select(Geometry.Parse).ToList();
            var pairs = 0;

            for (var i = 0; i < shapes.Count; i++)
                for (var j = i + 1; j < shapes.Count; j++)
                {
                    var overlap = shapes[i].Bounds.Intersect(shapes[j].Bounds);
                    if (overlap.Width <= 0 || overlap.Height <= 0) continue;

                    var hit = false;
                    for (var y = overlap.Y; y <= overlap.Bottom && !hit; y += 1)
                        for (var x = overlap.X; x <= overlap.Right && !hit; x += 1)
                        {
                            var point = new Point(x, y);
                            hit = shapes[i].FillContains(point) && shapes[j].FillContains(point);
                        }

                    if (hit) pairs++;
                }

            return pairs;
        });

        Assert.True(touching == 0, $"{touching} tepelik tüyü çifti birbirine değiyor; kapalı blok okuyor.");

        var (beak, crestBox, body) = AppHost.Run(() => (
            Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single()).Bounds.Right,
            BoundsOf(crest),
            Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single()).Bounds));

        Assert.True(crestBox.X < body.X,
            $"Tepelik geriye savrulmuyor: kutusu {crestBox.X:F0}'dan başlıyor, gövde {body.X:F0}'dan.");
        Assert.True(crestBox.Center.X < beak, "Tepelik gaganın önüne düşmüş.");
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

    /// <summary>
    /// K3: silüet arka planda durur. Kapladığı alan ölçülüyor; tuval dolmuyor,
    /// ama kuş da bir leke kadar küçük kalmıyor. Ölçü <b>silüeti</b> — tüyler, gövde ve
    /// korları — sayar; parlama katmanları ayrı tutulur, çünkü kenarında saydama inen bir
    /// yıkama geometrik olarak kapladığı yeri boyamaz.
    /// </summary>
    [Fact]
    public void ThePhoenixCoversABackdropSizedShareOfTheCanvas()
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
        var silhouette = PhoenixGeometries().Where(path => !glowPaths.Contains(path)).ToList();

        Assert.InRange(Share(silhouette), 0.08, 0.40);
        Assert.InRange(Share(glowPaths.ToList()), 0.10, 0.40);
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
    /// K1: eski dort yoldan hicbiri oldugu gibi durmuyor. Anka yamanmadi, bastan cizildi.
    /// </summary>
    [Fact]
    public void NoneOfTheOldPhoenixPathsSurvived()
    {
        foreach (var geometry in PhoenixGeometries())
            Assert.DoesNotContain(geometry.Replace("F1 ", string.Empty), BaselinePhoenixGeometries);
    }

    /// <summary>
    /// K1: kus tek kutle degil. Ayri tuylerden kuruluyor ve toplam yol sayisi sozlesmenin
    /// verdigi aralikta kaliyor.
    /// </summary>
    [Fact]
    public void ThePhoenixIsBuiltFromManySeparatePaths()
    {
        Assert.InRange(PhoenixGeometries().Count, 20, 60);
    }

    /// <summary>
    /// K1: birden fazla alt yol tasiyan tek cizim var - govde, gozu delik olarak tasiyor.
    /// Avalonia'nin ontanimli dolgu kurali tek-cift oldugu icin o cizim <c>F1</c> ile
    /// sifirdan-farkli kurali bildirmek zorunda; tarayicidaki onizlemenin ontanimlisi da
    /// budur, ikisi ayni seyi gosteriyor. Tek parcali yollarin boyle bir bildirimi yok.
    /// </summary>
    [Fact]
    public void OnlyMultiPartPathsDeclareTheFillRule()
    {
        foreach (var geometry in PhoenixGeometries())
        {
            var parts = geometry.Split('M').Length - 1;
            if (parts > 1) Assert.StartsWith("F1 ", geometry);
            else Assert.StartsWith("M ", geometry);
        }

        Assert.Contains(PhoenixGeometries(), geometry => geometry.StartsWith("F1 ", StringComparison.Ordinal));
    }

    /// <summary>K1: her kanat ayri tuylerden kuruluyor, tek dolgu levha degil.</summary>
    [Fact]
    public void EachWingIsBuiltFromSeparateFeathers()
    {
        Assert.True(PartGeometries("PhoenixWingFlameNear").Count >= 6,
            $"Yakin kanat {PartGeometries("PhoenixWingFlameNear").Count} tuy.");
        Assert.True(PartGeometries("PhoenixWingFlameFar").Count >= 6,
            $"Uzak kanat {PartGeometries("PhoenixWingFlameFar").Count} tuy.");

        foreach (var geometry in PartGeometries("PhoenixWingFlameNear").Concat(PartGeometries("PhoenixWingFlameFar")))
            Assert.Equal(1, geometry.Split('M').Length - 1);
    }

    /// <summary>
    /// K1: profilden bakilan kusta uzak kanat kisalir. Iki kanat ayni yol degil ve uzak
    /// kanadin genisligi yakin kanadin en cok yuzde yetmisi.
    /// </summary>
    [Fact]
    public void TheFarWingIsShorterThanTheNearWingAndNotItsMirror()
    {
        var near = PartBounds("PhoenixWingFlameNear");
        var far = PartBounds("PhoenixWingFlameFar");

        Assert.True(far.Width <= near.Width * 0.70,
            $"Uzak kanat {far.Width:F0}, yakin kanat {near.Width:F0}; oran {far.Width / near.Width:F2}.");

        var mirrored = PartGeometries("PhoenixWingFlameFar").Select(Mirror).ToList();
        foreach (var geometry in PartGeometries("PhoenixWingFlameNear"))
            Assert.DoesNotContain(geometry, mirrored);
    }

    /// <summary>K2: kuyruk tek kutle degil; en az bes tuy ve uclari farkli boyda.</summary>
    [Fact]
    public void TheTailIsSeparateFeathersOfDifferentLength()
    {
        var tail = PartGeometries("PhoenixTailFlame");
        Assert.True(tail.Count >= 5, $"Kuyruk {tail.Count} tuy.");

        var tips = AppHost.Run(() => tail
            .Select(path => Math.Round(Geometry.Parse(path).Bounds.Bottom))
            .Distinct()
            .Count());

        Assert.True(tips >= 5, $"Kuyruk tuylerinin ucu {tips} ayri derinlikte; hepsi ayni boyda.");
    }


    /// <summary>
    /// K4: govde sabit kalinlikta boru degil. Gogus en genis; boyun ve bel ondan dar.
    /// Genislikler cizimden, govdenin kendi kutusundan orneklenerek olculuyor.
    /// </summary>
    [Fact]
    public void TheBodyNarrowsAtTheNeckAndTheWaist()
    {
        var (neck, chest, waist) = AppHost.Run(() =>
        {
            var shape = Geometry.Parse(PartGeometries("PhoenixBodyFlame").Single());
            var box = shape.Bounds;

            double Width(double share)
            {
                var y = box.Y + (box.Height * share);
                var hits = 0;
                for (var x = box.X; x < box.Right; x += 1)
                    if (shape.FillContains(new Point(x, y))) hits++;
                return hits;
            }

            return (Width(0.30), Width(0.55), Width(0.82));
        });

        Assert.True(chest > neck * 1.5, $"Boyun {neck:F0}, gogus {chest:F0}: govde boyunda daralmiyor.");
        Assert.True(chest > waist * 1.2, $"Bel {waist:F0}, gogus {chest:F0}: govde belde daralmiyor.");
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
