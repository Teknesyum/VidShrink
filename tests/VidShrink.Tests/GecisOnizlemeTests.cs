using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class GecisOnizlemeTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private static string Kanit(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "gecis-onizleme", ad + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static EditTimeline Cizelge(TransitionKind tur = TransitionKind.Wipe)
        => new(new[]
        {
            new EditClip(0, 2 * Sn),
            new EditClip(2 * Sn, 4 * Sn) { Transition = new ClipTransition(tur, Sn / 2) },
        }, 4 * Sn);

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

    private static IReadOnlyList<string> Argumanlar(EditTimeline cizelge, string kaynak, MediaInfo bilgi, string cikti)
        => TransitionPreview.Arguments(cizelge, 1, new[] { kaynak }, new[] { bilgi }, (320, 180), 30, bilgi.HasAudio, 0, cikti);

    [Fact]
    public void GecisDosyasiYoksaEdlEskisiGibiKalir()
    {
        var cizelge = Cizelge();
        var eski = new EdlPreview("k.mp4", cizelge);
        var bos = new EdlPreview(new[] { "k.mp4" }, cizelge, new Dictionary<int, string>());

        Assert.Equal(eski.Uri, bos.Uri);
        Assert.False(bos.HasBlends);
        Assert.Equal(2, bos.Parts.Count);
        Assert.Equal(4 * Sn, bos.EdlDuration);
    }

    [Fact]
    public void GecisParcasiIkiKlibinOrtusmesiniTekParcadaTasir()
    {
        var onizleme = new EdlPreview(new[] { "k.mp4" }, Cizelge(), new Dictionary<int, string> { [1] = "g.mkv" });

        Assert.True(onizleme.HasBlends);
        Assert.Equal(3, onizleme.Parts.Count);
        var (a, gecis, b) = (onizleme.Parts[0], onizleme.Parts[1], onizleme.Parts[2]);
        Assert.False(a.IsBlend);
        Assert.True(gecis.IsBlend);
        Assert.Equal(3 * Sn / 2, a.EdlLength);
        Assert.Equal(Sn / 2, gecis.EdlLength);
        Assert.Equal(0, gecis.SourceFrom);
        Assert.Equal(3 * Sn / 2, gecis.TimelineStart);
        Assert.Equal(Sn, gecis.TimelineLength);
        Assert.Equal(5 * Sn / 2, b.SourceFrom);
        Assert.Equal(3 * Sn / 2, b.EdlLength);
        Assert.Equal(7 * Sn / 2, onizleme.EdlDuration);
        Assert.Equal(4 * Sn, onizleme.TimelineDuration);
        Assert.Equal("edl://%5%k.mp4,0,1.5;%5%g.mkv,0,0.5;%5%k.mp4,2.5,1.5", onizleme.Uri);

        Assert.Equal(7 * Sn / 4, onizleme.ToEdl(2 * Sn));
        Assert.Equal(2 * Sn, onizleme.ToTimeline(7 * Sn / 4));
        Assert.Equal(2 * Sn, onizleme.ToEdl(5 * Sn / 2));
        Assert.Equal(5 * Sn / 2, onizleme.ToTimeline(2 * Sn));
        Assert.Equal(Sn, onizleme.ToEdl(Sn));
        Assert.Same(gecis, onizleme.PartAtTimeline(2 * Sn));
    }

    [Fact]
    public void GecisParcasiZamanliZincireVeGeometriyeGirmez()
    {
        var cizelge = new EditTimeline(new[]
        {
            new EditClip(0, 2 * Sn) { Effects = ClipEffects.None with { Scale = 0.5 } },
            new EditClip(2 * Sn, 4 * Sn) { Transition = new ClipTransition(TransitionKind.Wipe, Sn / 2) },
        }, 4 * Sn);
        var onizleme = new EdlPreview(new[] { "k.mp4" }, cizelge, new Dictionary<int, string> { [1] = "g.mkv" });
        var gorunum = new EditLook(onizleme, 320, 180);

        Assert.NotNull(gorunum.Geometry(onizleme.Parts[0]));
        Assert.NotNull(gorunum.Geometry(onizleme.Parts[2]));
        Assert.Null(gorunum.Geometry(onizleme.Parts[1]));
    }

    [Fact]
    public void ArgumanlarIkiGirdiyiOrtusmeyeKesipXfadeIleBirlestirir()
    {
        var args = Argumanlar(Cizelge(), "k.mp4", Bilgi("k.mp4"), "c.mkv");
        var grafik = args[args.ToList().IndexOf("-filter_complex") + 1];

        Assert.Equal("c.mkv", args[^1]);
        Assert.Equal(new[] { "1.5", "2" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-ss").Select(x => args[x.i + 1]));
        Assert.Equal(new[] { "0.5", "0.5" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-t").Select(x => args[x.i + 1]));
        Assert.Contains("xfade=transition=wipeleft:duration=0.5:offset=0", grafik, StringComparison.Ordinal);
        Assert.Contains("setpts=PTS-STARTPTS+1.5/TB", grafik, StringComparison.Ordinal);
        Assert.DoesNotContain("acrossfade", grafik, StringComparison.Ordinal);
        Assert.Contains("-an", args);
        Assert.All(args.Select((a, i) => (a, i)).Where(x => x.a == "-threads"), x => Assert.Equal("2", args[x.i + 1]));
    }

    [Fact]
    public void AnahtarCiktiyiSaymazAyarDegisinceDegisir()
    {
        var bilgi = Bilgi("k.mp4");
        var bir = TransitionPreview.Key(Argumanlar(Cizelge(), "k.mp4", bilgi, "a.mkv"));

        Assert.Equal(24, bir.Length);
        Assert.Equal(bir, TransitionPreview.Key(Argumanlar(Cizelge(), "k.mp4", bilgi, "b.mkv")));
        Assert.NotEqual(bir, TransitionPreview.Key(Argumanlar(Cizelge(TransitionKind.Iris), "k.mp4", bilgi, "a.mkv")));
        Assert.NotEqual(bir, TransitionPreview.Key(Argumanlar(Cizelge(), "l.mp4", bilgi, "a.mkv")));
    }

    [Fact]
    public void BekleyenYalnizGecisliKlipleriSayar()
    {
        var cizelge = new EditTimeline(new[]
        {
            new EditClip(0, Sn),
            new EditClip(Sn, 2 * Sn),
            new EditClip(2 * Sn, 3 * Sn) { Transition = new ClipTransition(TransitionKind.Dissolve, Sn / 4) },
        }, 3 * Sn);

        Assert.Equal(new[] { 2 }, TransitionPreview.Pending(cizelge));
    }

    [Fact]
    public void SupurmeYalnizOlmusSurecinKlasorunuSiler()
    {
        var kok = Kanit("supurme");
        try
        {
            var olu = Path.Combine(kok, "2147483646");
            var canli = TransitionPreview.ProcessFolder(kok);
            var yabanci = Path.Combine(kok, "notlar");
            foreach (var k in new[] { olu, canli, yabanci })
            {
                Directory.CreateDirectory(k);
                File.WriteAllBytes(Path.Combine(k, "x.mkv"), new byte[4]);
            }

            Assert.Equal(1, TransitionPreview.SweepStale(kok));
            Assert.False(Directory.Exists(olu));
            Assert.True(Directory.Exists(canli));
            Assert.True(Directory.Exists(yabanci));
            Assert.True(TransitionPreview.Remove(canli));
            Assert.False(Directory.Exists(canli));
            Assert.Equal(0, TransitionPreview.SweepStale(Path.Combine(kok, "yok")));
        }
        finally
        {
            Directory.Delete(kok, true);
        }
    }

    [Fact]
    public void DuzenleyiciGecisHazirOluncaParcayiDegistirirKapanistaSiler()
    {
        var klasor = Kanit("arayuz");
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        var cagri = new List<IReadOnlyList<string>>();
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                    BlendRunner = (args, _) =>
                    {
                        cagri.Add(args);
                        File.WriteAllBytes(args[^1], new byte[8]);
                        return Task.FromResult(new FfmpegRun(true, 0, string.Empty, TimeSpan.Zero));
                    },
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                view.BlendRoot = Path.Combine(klasor, "gecis");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                string gecisKlasoru;
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 1, 10);
                    Assert.Equal(1, view.AutoSplitClips(AutoSplitMode.Count, 2));
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 2, 10);
                    Assert.False(view.Driver!.Preview.HasBlends);
                    var acilan = motor.Acilan;

                    view.TimelineView.SelectedIndex = 1;
                    view.CmbClipTransition.SelectedIndex = 1;
                    DenetimSurucu.Pump(view.Player, () => view.BlendWork.IsCompleted && view.Driver!.Preview.HasBlends, 10);

                    var gecis = Assert.Single(view.BlendFiles);
                    gecisKlasoru = view.BlendFolder;
                    Assert.Equal(gecisKlasoru, Path.GetDirectoryName(gecis));
                    Assert.StartsWith(Path.Combine(klasor, "gecis"), gecisKlasoru, StringComparison.OrdinalIgnoreCase);
                    Assert.Equal(1, view.BlendRenders);
                    Assert.Equal(gecis, Assert.Single(cagri)[^1]);
                    Assert.Contains(EdlPreview.Escape(gecis), view.Driver!.Preview.Uri, StringComparison.Ordinal);
                    Assert.Equal(acilan + 1, motor.Acilan);

                    view.CmbClipTransition.SelectedIndex = 0;
                    DenetimSurucu.Pump(view.Player, () => !view.Driver!.Preview.HasBlends, 10);
                    Assert.Single(cagri);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }

                Assert.False(Directory.Exists(gecisKlasoru));
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task CanliGecisParcasiTeslimKaresiyleOrtusurVeEdlIleAcilir()
    {
        var klasor = Kanit("canli");
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=30:d=4",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "16", "-g", "30", "-pix_fmt", "yuv420p", "-threads", "2", kaynak);
            Assert.True(kod == 0, hata);
            var bilgi = await FfprobeClient.ProbeAsync(kaynak);
            var cizelge = Cizelge();

            var teslim = Path.Combine(klasor, "teslim.mp4");
            var adim = EditExport.Build(cizelge, bilgi, Array.Empty<double>(), 0, ExportMode.Full, teslim, klasor, 8L << 30).Steps.Single().Args.ToList();
            adim.InsertRange(adim.Count - 1, new[] { "-threads", "2" });
            (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, adim.ToArray());
            Assert.True(kod == 0, hata[Math.Max(0, hata.Length - 600)..]);

            double Psnr(TransitionKind tur)
            {
                var gecis = Path.Combine(klasor, ClipTransition.Key(tur) + TransitionPreview.Extension);
                var (k, _, h) = GorunumKanit.Kos(ToolLocator.Ffmpeg, Argumanlar(Cizelge(tur), kaynak, bilgi, gecis).ToArray());
                Assert.True(k == 0, h[Math.Max(0, h.Length - 600)..]);
                (k, _, h) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-threads", "2", "-i", gecis, "-i", teslim, "-filter_complex",
                    "[1:v]trim=start_frame=45:end_frame=60,settb=1/30,setpts=N[b];[0:v]settb=1/30,setpts=N[a];[a][b]psnr", "-f", "null", "-");
                Assert.True(k == 0, h);
                var eslesme = Regex.Match(h, @"PSNR .*average:([0-9.]+|inf)");
                Assert.True(eslesme.Success, h);
                return eslesme.Groups[1].Value == "inf" ? 99 : double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            var dogru = Psnr(TransitionKind.Wipe);
            var yanlis = Psnr(TransitionKind.Iris);
            var kayit = $"wipe={dogru:0.00} iris={yanlis:0.00}";
            Assert.True(dogru >= 40, kayit);
            Assert.True(yanlis <= dogru - 10, kayit);

            var parca = Path.Combine(klasor, ClipTransition.Key(TransitionKind.Wipe) + TransitionPreview.Extension);
            var onizleme = new EdlPreview(new[] { kaynak }, cizelge, new Dictionary<int, string> { [1] = parca });
            using var motor = new MpvEngine();
            motor.SetProperty("ao", "null");
            await motor.OpenAsync(onizleme.Uri);
            var sure = motor.DurationSeconds;
            Assert.True(Math.Abs(sure - EditTime.ToSeconds(onizleme.EdlDuration)) < 0.05, $"sure={sure} {string.Join(" | ", motor.RecentLog)}");
            motor.Dispose();
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
