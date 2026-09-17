using System.Globalization;
using System.Text;
using VidShrink.Core;

namespace VidShrink.KaydediciYerlesim;

/// <summary>
/// Kaydedicinin ekran yerlesimi olcusu. Monitor listesi ve olcek carpani enjekte ediliyor,
/// boylece tek ekranli bir makinede de cok ekranli ve olcek != 1 durumlari olculebiliyor.
/// Her satir uretilen <c>gdigrab</c> yakalama dikdortgeninin fiziksel piksel karsiligini
/// yaziyor.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var target = args.Length > 0 ? args[0] : Path.Combine(".calisma", "yerlesim");
        Directory.CreateDirectory(target);

        var report = new StringBuilder();
        void Line(string text)
        {
            Console.WriteLine(text);
            report.AppendLine(text);
        }

        Line("# Kaydedici Yerlesim Olcumu");
        Line("ffmpeg gdigrab koordinat uzayi: fiziksel piksel (surec PROCESS_PER_MONITOR_DPI_AWARE).");
        Line(string.Empty);

        foreach (var layout in Layouts())
        {
            Line("## " + layout.Name);
            Line("monitorler: " + string.Join(" | ", layout.Screens.Select(Describe)));
            Line(string.Empty);
            Line("### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)");
            Line("indeks\tESKI kol (index!=0 ? RegionForScreen : null)\tESKI yakalanan px\tYENI ScreenCapture\tYENI yakalanan px\tmonitorun kendisi");

            var bounds = layout.Screens.Select(s => s.Bounds).ToList();
            for (var i = 0; i < bounds.Count; i++)
            {
                var request = new RecorderRequest
                {
                    Platform = RecorderPlatform.Windows,
                    Target = RecorderTargetKind.Screen,
                    ScreenIndex = i,
                    Screens = bounds
                };

                var eskiKol = i != 0 ? RecorderArguments.RegionForScreen(bounds, i) : null;
                var built = RecorderArguments.Build(request, Path.Combine(target, "x.mp4"));
                var offsetX = Argument(built, "-offset_x");
                var offsetY = Argument(built, "-offset_y");
                var size = Argument(built, "-video_size");
                var monitor = new RecorderRegion(bounds[i].X, bounds[i].Y, bounds[i].Width, bounds[i].Height);
                var yeniPx = size is null
                    ? Describe(RecorderLayout.Union(bounds)) + " (butun masaustu)"
                    : size + "+" + offsetX + "," + offsetY;
                var eskiPx = eskiKol is null
                    ? Describe(RecorderLayout.Union(bounds)) + " (butun masaustu)"
                    : Describe(eskiKol);

                Line(i.ToString(CultureInfo.InvariantCulture) + "\t" + Describe(eskiKol) + "\t" + eskiPx
                     + "\t" + Describe(RecorderArguments.ScreenCapture(bounds, i)) + "\t" + yeniPx
                     + "\t" + Describe(monitor));
            }

            Line(string.Empty);
            Line("### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)");
            var union = RecorderLayout.Union(bounds)!;
            var cover = RecorderLayout.Cover(layout.Screens)!;
            var eski = EskiKapla(layout.Screens);
            var acilanOlcek = RecorderLayout.ScaleAt(layout.Screens, union.X, union.Y) ?? 1;
            Line("masaustu birlesimi\t" + Describe(union));
            Line("ortunun dogdugu monitorun olcegi\t" + Num(acilanOlcek));
            Line("ESKI formul (Screens.Primary.Scaling=" + Num(eski.Scale) + ")\tnokta boy "
                 + Num(eski.Width) + "x" + Num(eski.Height) + "\tfiziksel kaplama "
                 + Describe(eski.Physical(acilanOlcek)) + "\t" + Verdict(eski.Physical(acilanOlcek), union));
            Line("YENI formul (kosedeki monitorun olcegi=" + Num(cover.Scale) + ")\tnokta boy "
                 + Num(cover.Width) + "x" + Num(cover.Height) + "\tfiziksel kaplama "
                 + Describe(cover.Physical(acilanOlcek)) + "\t" + Verdict(cover.Physical(acilanOlcek), union));

            Line(string.Empty);
            Line("### Monitor bosluguna dusen bolge");
            foreach (var probe in layout.Probes)
            {
                var request = new RecorderRequest
                {
                    Platform = RecorderPlatform.Windows,
                    Target = RecorderTargetKind.Region,
                    Region = probe,
                    Screens = bounds
                };
                var errors = RecorderArguments.Validate(request, Path.Combine(target, "x.mp4"));
                var covered = RecorderLayout.Covered(bounds, probe);
                Line(Describe(probe) + "\tCovered=" + covered + "\tValidate=" + (errors.Count == 0
                    ? "(hatasiz)"
                    : string.Join(" ", errors)));
            }

            Line(string.Empty);
        }

        var file = Path.Combine(target, "yerlesim.txt");
        File.WriteAllText(file, report.ToString());
        Console.WriteLine("yazildi: " + Path.GetFullPath(file));
        return 0;
    }

    /// <summary>
    /// Bolge cizim ortusunun <c>e962538e</c>'deki formulu, satir satir:
    /// <c>Position = _desktop.Position; Width = _desktop.Width / Screens.Primary.Scaling;</c>.
    /// Birincil monitor listenin ilkidir.
    /// </summary>
    private static OverlayCover EskiKapla(IReadOnlyList<ScreenPlacement> screens)
    {
        var union = RecorderLayout.Union(screens.Select(s => s.Bounds).ToList())!;
        var primary = screens[0].Scale;
        return new OverlayCover(union.X, union.Y, union.Width / primary, union.Height / primary, primary);
    }

    private static string Verdict(RecorderRegion actual, RecorderRegion union)
        => actual.Width == union.Width && actual.Height == union.Height
            ? "TAM"
            : "EKSIK/FAZLA " + (actual.Width - union.Width).ToString(CultureInfo.InvariantCulture) + "x"
              + (actual.Height - union.Height).ToString(CultureInfo.InvariantCulture) + " px";

    private static string? Argument(IReadOnlyList<string> built, string name)
    {
        for (var i = 0; i + 1 < built.Count; i++)
            if (built[i] == name)
                return built[i + 1];
        return null;
    }

    private static string Describe(ScreenPlacement s)
        => Describe(new RecorderRegion(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)) + " @" + Num(s.Scale);

    private static string Describe(RecorderRegion? r)
        => r is null
            ? "(yok)"
            : r.Width.ToString(CultureInfo.InvariantCulture) + "x" + r.Height.ToString(CultureInfo.InvariantCulture)
              + "+" + r.X.ToString(CultureInfo.InvariantCulture) + "," + r.Y.ToString(CultureInfo.InvariantCulture);

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record Layout(string Name, IReadOnlyList<ScreenPlacement> Screens, IReadOnlyList<RecorderRegion> Probes);

    private static IEnumerable<Layout> Layouts()
    {
        yield return new Layout(
            "(a) Tek ekran, olcek 1,0 — 1920x1080",
            new[] { new ScreenPlacement(new ScreenBounds(0, 0, 0, 1920, 1080), 1.0) },
            new[] { new RecorderRegion(100, 100, 640, 480), new RecorderRegion(1800, 100, 640, 480) });

        yield return new Layout(
            "(b1) Tek ekran, olcek 1,25 — 2400x1350 fiziksel",
            new[] { new ScreenPlacement(new ScreenBounds(0, 0, 0, 2400, 1350), 1.25) },
            new[] { new RecorderRegion(100, 100, 640, 480) });

        yield return new Layout(
            "(b2) Tek ekran, olcek 1,5 — 2880x1620 fiziksel",
            new[] { new ScreenPlacement(new ScreenBounds(0, 0, 0, 2880, 1620), 1.5) },
            new[] { new RecorderRegion(100, 100, 640, 480) });

        yield return new Layout(
            "(c) Iki ekran ayni olcek, ikincisi solda negatif X",
            new[]
            {
                new ScreenPlacement(new ScreenBounds(0, 0, 0, 1920, 1080), 1.0),
                new ScreenPlacement(new ScreenBounds(1, -1280, 0, 1280, 1024), 1.0)
            },
            new[] { new RecorderRegion(-1200, 100, 640, 480), new RecorderRegion(-640, 1040, 640, 40) });

        yield return new Layout(
            "(d) Iki ekran farkli olcek, ikincisi solda negatif X ve olcek 1,5",
            new[]
            {
                new ScreenPlacement(new ScreenBounds(0, 0, 0, 1920, 1080), 1.0),
                new ScreenPlacement(new ScreenBounds(1, -2880, 0, 2880, 1620), 1.5)
            },
            new[] { new RecorderRegion(-2800, 100, 640, 480), new RecorderRegion(-640, 1100, 640, 400) });
    }
}
