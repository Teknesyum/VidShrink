using System.Globalization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciV2IzTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private static string Kanit(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "v2-izi", ad + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static MediaInfo Bilgi(string yol) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1,
        DurationSeconds = 4,
        Width = 320,
        Height = 180,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
    };

    private static VideoLayer Katman(string yol = "l.mp4", bool anahtar = false) => new(yol, 2 * Sn, Sn)
    {
        Scale = 0.25,
        X = 1,
        Y = 0,
        ChromaKey = anahtar,
    };

    private static EditTimeline Tek(params VideoLayer[] katmanlar) => new(new[] { new EditClip(0, 4 * Sn) }, 4 * Sn, layers: katmanlar);

    private static EditTimeline Gecisli(params VideoLayer[] katmanlar) => new(new[]
    {
        new EditClip(0, 2 * Sn),
        new EditClip(2 * Sn, 4 * Sn) { Transition = new ClipTransition(TransitionKind.Wipe, Sn / 2) },
    }, 4 * Sn, layers: katmanlar);

    private static ExportPlan Teslim(EditTimeline cizelge, MediaInfo bilgi, string cikti, string klasor, ExportMode kip = ExportMode.Fast)
        => EditExport.Build(cizelge, bilgi, new double[] { 0, 1, 2, 3 }, 0, kip, cikti, klasor, 8L << 30);

    private static string Grafik(IReadOnlyList<string> args) => args[args.ToList().IndexOf("-filter_complex") + 1];

    [Fact]
    public void KatmanTamKipiZorlarGirdiyiGecikmeyleVeAnahtarlaBindirir()
    {
        var plan = Teslim(Tek(Katman(anahtar: true)), Bilgi("k.mp4"), "c.mp4", ".");
        var args = plan.Steps.Single().Args;
        var grafik = Grafik(args);

        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.LayersForcedFull);
        var i = args.ToList().IndexOf("l.mp4");
        Assert.Equal(new[] { "-ss", "0", "-t", "2", "-itsoffset", "1", "-i", "l.mp4" }, args.Skip(i - 7).Take(8));
        Assert.Contains("[1:v]scale=w=80:h=-2,format=yuva420p," + RecorderArguments.GreenScreenKey + "[lay0];[vbase][lay0]overlay=x=(W-w)*1:y=(H-h)*0:eof_action=pass[vout]",
            grafik, StringComparison.Ordinal);
        Assert.Contains("[vbase]", grafik[..grafik.IndexOf(";[1:v]", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void AnahtarsizKatmanChromakeyTasimazMetinBindirmeninUstundeKalir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(0, 4 * Sn) }, 4 * Sn, texts: new[] { new TextLayer("Üst", 0, Sn) }, layers: new[] { Katman() });
        var grafik = Grafik(Teslim(cizelge, Bilgi("k.mp4"), "c.mp4", ".").Steps.Single().Args);

        Assert.DoesNotContain("chromakey", grafik, StringComparison.Ordinal);
        var bindirme = grafik.IndexOf("eof_action=pass[vcat]", StringComparison.Ordinal);
        Assert.True(bindirme > 0, grafik);
        Assert.True(grafik.IndexOf("ass=", StringComparison.Ordinal) > bindirme, grafik);
    }

    [Fact]
    public void KatmanKatmansizTeslimiDegistirmez()
    {
        var bilgi = Bilgi("k.mp4");
        var plan = Teslim(Tek(), bilgi, "c.mp4", ".");

        Assert.False(plan.LayersForcedFull);
        Assert.DoesNotContain(plan.Steps.SelectMany(s => s.Args), a => a.Contains("overlay", StringComparison.Ordinal) || a == "-itsoffset");
    }

    [Fact]
    public void CiktiZamaniGecisOrtusmesiniYariyaIndirir()
    {
        var cizelge = Gecisli();

        Assert.Equal(Sn, cizelge.ToOutput(Sn));
        Assert.Equal(3 * Sn / 2, cizelge.ToOutput(3 * Sn / 2));
        Assert.Equal(7 * Sn / 4, cizelge.ToOutput(2 * Sn));
        Assert.Equal(2 * Sn, cizelge.ToOutput(5 * Sn / 2));
        Assert.Equal(5 * Sn / 2, cizelge.ToOutput(3 * Sn));
        Assert.Equal(0, cizelge.ToOutput(-Sn));
    }

    [Fact]
    public void KatmanBoyuTeslimSonundaKesilir()
    {
        var gec = Katman() with { Start = 3 * Sn };
        var yerlesen = Assert.Single(EditExport.PlacedLayers(Gecisli(gec)));

        Assert.Equal(5 * Sn / 2, yerlesen.Delay);
        Assert.Equal(Sn, yerlesen.Length);
        Assert.Empty(EditExport.PlacedLayers(Tek(Katman() with { Start = 4 * Sn })));
    }

    [Fact]
    public void YamaKlipParcasiniBolerSureyiKorur()
    {
        var onizleme = new EdlPreview(new[] { "k.mp4" }, Tek(Katman()), null, new[] { new EdlPatch(0, Sn, 3 * Sn, "y.mkv") });

        Assert.Single(onizleme.Parts, p => p.IsBlend && p.Ratio == 1);
        Assert.Equal(3, onizleme.Parts.Count);
        var yama = onizleme.Parts[1];
        Assert.Equal(1, yama.Ratio);
        Assert.Equal(Sn, yama.TimelineStart);
        Assert.Equal(2 * Sn, yama.TimelineLength);
        Assert.Equal(2 * Sn, yama.EdlLength);
        Assert.Equal(3 * Sn, onizleme.Parts[2].SourceFrom);
        Assert.Equal(4 * Sn, onizleme.EdlDuration);
        Assert.Equal(4 * Sn, onizleme.TimelineDuration);
        Assert.Equal(2 * Sn, onizleme.ToEdl(2 * Sn));
        Assert.Equal(5 * Sn / 2, onizleme.ToTimeline(5 * Sn / 2));
        Assert.Equal("edl://%5%k.mp4,0,1;%5%y.mkv,0,2;%5%k.mp4,3,1", onizleme.Uri);
    }

    [Fact]
    public void BekleyenAraliklarGecisBolgesiniAtlar()
    {
        var katman = new VideoLayer("l.mp4", 2 * Sn, Sn);
        var araliklar = LayerPreview.Pending(Gecisli(katman));

        Assert.Equal(new[] { new LayerSpan(0, Sn, 3 * Sn / 2), new LayerSpan(1, Sn / 2, Sn) }, araliklar);
        Assert.Empty(LayerPreview.Pending(Gecisli()));
    }

    [Fact]
    public void OnizlemeArgumaniKatmaniAralikBasinaGeciktirir()
    {
        var cizelge = Tek(Katman() with { Start = 2 * Sn });
        var args = LayerPreview.Arguments(cizelge, new LayerSpan(0, Sn, 3 * Sn), new[] { "k.mp4" }, new[] { Bilgi("k.mp4") }, (320, 180), 30, false, 0, "y.mkv");
        var grafik = Grafik(args);

        Assert.Equal("y.mkv", args[^1]);
        var i = args.ToList().IndexOf("l.mp4");
        Assert.Equal(new[] { "-ss", "0", "-t", "1", "-itsoffset", "1", "-i", "l.mp4" }, args.Skip(i - 7).Take(8));
        Assert.Contains("[v0][lay0]overlay=x=(W-w)*1:y=(H-h)*0:eof_action=pass[vout]", grafik, StringComparison.Ordinal);
        Assert.All(args.Select((a, n) => (a, n)).Where(x => x.a == "-threads"), x => Assert.Equal("2", args[x.n + 1]));
    }

    [Fact]
    public void ProjeKatmaniYazipGeriOkur()
    {
        var katman = Katman(@"C:\v\üst.mp4", anahtar: true) with { SourceStart = Sn / 2, SourceEnd = 3 * Sn / 2, Scale = 0.5, X = 0.25, Y = 0.75 };
        var damga = new SourceStamp(@"C:\a.mp4", 16, 1);
        var json = EditProject.From(damga, Tek(katman)).ToJson();
        var proje = EditProject.Parse(json);

        Assert.NotNull(proje);
        Assert.Equal(katman, Assert.Single(proje!.Layers));
        var is_ = proje.ToJob(_ => damga, out _);
        Assert.Equal(katman, Assert.Single(is_!.Timeline.Layers));
        Assert.DoesNotContain("\"layers\"", EditProject.From(damga, Tek()).ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void BozukKatmanProjeyiReddeder()
    {
        var damga = new SourceStamp(@"C:\a.mp4", 16, 1);
        var json = EditProject.From(damga, Tek(Katman())).ToJson().Replace("\"scale\": 0.25", "\"scale\": 3", StringComparison.Ordinal)
            .Replace("\"scale\":0.25", "\"scale\":3", StringComparison.Ordinal);

        Assert.Contains("3", json, StringComparison.Ordinal);
        Assert.Null(EditProject.Parse(json));
    }

    [Fact]
    public void KatmanDegisikligiTekAdimdaGeriAlinir()
    {
        var cizelge = Tek();
        var katman = Katman();

        Assert.Equal(0, cizelge.AddLayer(katman));
        Assert.True(cizelge.UpdateLayer(0, katman with { ChromaKey = true }));
        Assert.True(cizelge.MoveLayer(0, 2 * Sn));
        Assert.Equal(2 * Sn, cizelge.Layers[0].Start);

        Assert.True(cizelge.Undo());
        Assert.Equal(katman with { ChromaKey = true }, cizelge.Layers[0]);
        Assert.True(cizelge.Undo());
        Assert.Equal(katman, cizelge.Layers[0]);
        Assert.True(cizelge.Undo());
        Assert.False(cizelge.HasLayers);
        Assert.True(cizelge.Redo());
        Assert.Equal(katman, Assert.Single(cizelge.Layers));

        cizelge.DeleteLayer(0);
        Assert.False(cizelge.HasLayers);
        Assert.True(cizelge.Undo());
        Assert.Single(cizelge.Layers);
        Assert.Throws<ArgumentOutOfRangeException>(() => cizelge.AddLayer(katman with { Scale = 2 }));
    }

    private static (int R, int G, int B) Renk(string dosya, double an, int x, int y, int w, int h, string klasor)
    {
        var ham = Path.Combine(klasor, "renk-" + Guid.NewGuid().ToString("N")[..6] + ".rgb");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-threads", "2", "-ss", an.ToString(CultureInfo.InvariantCulture), "-i", dosya,
            "-frames:v", "1", "-vf", $"crop={w}:{h}:{x}:{y},scale=1:1:flags=area,format=rgb24", "-f", "rawvideo", ham);
        Assert.True(kod == 0, hata);
        var b = File.ReadAllBytes(ham);
        File.Delete(ham);
        return (b[0], b[1], b[2]);
    }

    private static string Kos(string klasor, string ad, params string[] args)
    {
        var yol = Path.Combine(klasor, ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-nostdin", "-y" }.Concat(args)
            .Concat(new[] { "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-pix_fmt", "yuv420p", "-threads", "2", yol }).ToArray());
        Assert.True(kod == 0, hata);
        return yol;
    }

    [FfmpegFact]
    public async Task TeslimKaresindeKatmanKutusuYerindeVeYesilPerdeDelinir()
    {
        var klasor = Kanit("teslim");
        try
        {
            var kaynak = Kos(klasor, "kaynak.mp4", "-f", "lavfi", "-i", "color=c=red:s=320x180:r=30:d=4");
            var ust = Kos(klasor, "ust.mp4", "-f", "lavfi", "-i", "color=c=0x00FF00:s=320x180:r=30:d=2", "-vf", "drawbox=x=120:y=60:w=80:h=60:color=blue:t=fill");
            var bilgi = await FfprobeClient.ProbeAsync(kaynak);

            string Teslim_(bool anahtar)
            {
                var cikti = Path.Combine(klasor, (anahtar ? "anahtarli" : "duz") + ".mp4");
                var katman = new VideoLayer(ust, 2 * Sn, Sn) { Scale = 0.5, X = 1, Y = 0, ChromaKey = anahtar };
                var adim = Teslim(Tek(katman), bilgi, cikti, klasor).Steps.Single().Args.ToList();
                adim.InsertRange(adim.Count - 1, new[] { "-threads", "2" });
                var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, adim.ToArray());
                Assert.True(kod == 0, hata[Math.Max(0, hata.Length - 600)..]);
                return cikti;
            }

            var duz = Teslim_(false);
            var anahtarli = Teslim_(true);
            var kutuKenari = (x: 168, y: 8, w: 16, h: 16);
            var kutuOrtasi = (x: 232, y: 38, w: 16, h: 14);
            var disari = (x: 40, y: 120, w: 40, h: 40);

            var olcum = new List<string>();
            (int R, int G, int B) Oku(string dosya, double an, (int x, int y, int w, int h) b, string ad)
            {
                var renk = Renk(dosya, an, b.x, b.y, b.w, b.h, klasor);
                olcum.Add($"{Path.GetFileNameWithoutExtension(dosya)} t={an:0.0} {ad}={renk}");
                return renk;
            }

            var once = Oku(duz, 0.5, kutuKenari, "kenar");
            var ortaDuz = Oku(duz, 2, kutuOrtasi, "orta");
            var kenarDuz = Oku(duz, 2, kutuKenari, "kenar");
            var disDuz = Oku(duz, 2, disari, "dis");
            var sonra = Oku(duz, 3.5, kutuKenari, "kenar");
            var ortaAnahtar = Oku(anahtarli, 2, kutuOrtasi, "orta");
            var kenarAnahtar = Oku(anahtarli, 2, kutuKenari, "kenar");
            var kayit = string.Join(" | ", olcum);

            static bool Kirmizi((int R, int G, int B) c) => c.R > 200 && c.G < 60 && c.B < 60;
            Assert.True(Kirmizi(once), kayit);
            Assert.True(ortaDuz.B > 200 && ortaDuz.R < 60, kayit);
            Assert.True(kenarDuz.G > 200 && kenarDuz.R < 60, kayit);
            Assert.True(Kirmizi(disDuz), kayit);
            Assert.True(Kirmizi(sonra), kayit);
            Assert.True(ortaAnahtar.B > 200 && ortaAnahtar.R < 60, kayit);
            Assert.True(Kirmizi(kenarAnahtar), kayit);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
