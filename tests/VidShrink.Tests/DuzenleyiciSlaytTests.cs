using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Later 87 kalani ve Later 90: secilebilir durgun klip suresi, HSL renk secici, bos cizelgeye tek resim, <c>ara-klipler/</c>
/// budamasi, monitorde surukleyerek konumlama ve slayt sihirbazi (<see cref="Slideshow"/>, <see cref="EditTimeline.AppendSources"/>).
/// ffmpeg sahte kosucuyla taklit edilir; kanit <c>.calisma/yan/slayt/*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciSlaytTests
{
    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "yan", "slayt");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static double Teslim(SlidePlan plan) => plan.Seconds.Sum() - Math.Max(0, plan.Seconds.Count - 1) * plan.TransitionSeconds;

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kok, "slayt-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor) => KanitKapanisi.Kapat(Kok, Path.GetFileName(klasor));

    private static string Sahte(string klasor, string ad)
    {
        var dosya = Path.Combine(klasor, ad);
        Directory.CreateDirectory(Path.GetDirectoryName(dosya)!);
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    private static string Sonraki(IReadOnlyList<string> args, string anahtar)
    {
        var i = args.ToList().IndexOf(anahtar);
        Assert.True(i >= 0 && i + 1 < args.Count, anahtar + " yok: " + string.Join(' ', args));
        return args[i + 1];
    }

    [Fact]
    public void PlanTeslimSuresiniSlaytSayisiCarpiSureyeOturtur()
    {
        foreach (var (adet, slayt, gecis, fps) in new[] { (3, 3.0, 1.0, 30.0), (5, 2.0, 0.7, 25.0), (2, 4.0, 5.0, 29.97), (4, 1.0, 0.3, 24.0) })
        {
            var plan = Slideshow.Plan(adet, slayt, gecis, fps);
            Assert.Equal(adet, plan.Seconds.Count);
            Assert.True(plan.TransitionSeconds > 0);
            Assert.True(plan.TransitionSeconds <= slayt / 2 + 1.0 / fps, "gecis slaytin yarisini asti");
            Assert.InRange(Teslim(plan), adet * slayt - 1.0 / fps, adet * slayt + 1.0 / fps);
            Assert.All(plan.Seconds, s => Assert.True(s >= plan.TransitionSeconds));
        }

        var duz = Slideshow.Plan(3, 3, 1, 30);
        Assert.Equal(new[] { 3.5, 4.0, 3.5 }, duz.Seconds);
        Assert.Equal(1.0, duz.TransitionSeconds);

        var gecissiz = Slideshow.Plan(3, 2.5, 0, 30);
        Assert.Equal(new[] { 2.5, 2.5, 2.5 }, gecissiz.Seconds);
        Assert.Equal(7.5, Teslim(gecissiz));

        Assert.Equal(new[] { 3.0 }, Slideshow.Plan(1, 3, 1, 30).Seconds);
        Assert.Empty(Slideshow.Plan(0, 3, 1, 30).Seconds);
        Assert.Equal(7 / 30.0, Slideshow.Plan(3, 0.1, 1, 30).TransitionSeconds);
        Assert.Equal(0, Slideshow.Plan(3, 0.5, 1, 2).TransitionSeconds);
        Assert.Equal(new[] { 60.0, 60.0 }, Slideshow.Plan(2, 500, 0, 30).Seconds);
    }

    [Fact]
    public void MuzigeYaymaSureyiBolerSinirdaTutar()
    {
        Assert.Equal(12, Slideshow.FitSeconds(36, 3));
        Assert.Equal(StillClip.MaxSeconds, Slideshow.FitSeconds(600, 3));
        Assert.Equal(StillClip.MinSeconds, Slideshow.FitSeconds(1, 10));
        Assert.Equal(Slideshow.DefaultSeconds, Slideshow.FitSeconds(0, 3));
        Assert.Equal(Slideshow.DefaultSeconds, Slideshow.FitSeconds(30, 0));

        Assert.Equal(StillClip.MinSeconds, StillClip.ClampSeconds(0.01));
        Assert.Equal(StillClip.MaxSeconds, StillClip.ClampSeconds(1000));
        Assert.Equal(2.35, StillClip.ClampSeconds(2.349));
        Assert.Equal(StillClip.ImageSeconds, StillClip.ClampSeconds(double.NaN));
    }

    [Fact]
    public void SiraResimOlmayaniVeTekrariAtarAdaGoreDizer()
    {
        var sira = Slideshow.Order(new[] { @"C:\r\c.png", @"C:\r\b.JPG", @"C:\r\notlar.txt", @"C:\r\a.webp", @"C:\r\c.png", @"C:\r\muzik.mp3" });
        Assert.Equal(new[] { @"C:\r\a.webp", @"C:\r\b.JPG", @"C:\r\c.png" }, sira);
        Assert.Empty(Slideshow.Order(Array.Empty<string>()));
    }

    [Fact]
    public void ResimKaresiBuyuguKutuyaSigdirirYonuKorur()
    {
        MediaInfo Bilgi(int g, int y) => new() { FilePath = "a.png", FileSizeBytes = 1, DurationSeconds = 0, Width = g, Height = y, Fps = 0, VideoCodec = "png", TotalBitrateBps = 0 };

        Assert.Equal((1920, 1080), StillClip.ImageFrame(null));
        Assert.Equal((640, 480), StillClip.ImageFrame(Bilgi(640, 480)));
        Assert.Equal((640, 480), StillClip.ImageFrame(Bilgi(641, 481)));
        Assert.Equal((1440, 1080), StillClip.ImageFrame(Bilgi(4000, 3000)));
        Assert.Equal((1080, 1440), StillClip.ImageFrame(Bilgi(3000, 4000)));
        Assert.Equal((1920, 480), StillClip.ImageFrame(Bilgi(4000, 1000)));
    }

    [Fact]
    public void HslIleOnaltilikRenkGidipGelir()
    {
        Assert.Equal("#3366CC", StillClip.FromHsl(220, 0.6, 0.5));
        Assert.Equal("#CC3333", StillClip.FromHsl(0, 0.6, 0.5));
        Assert.Equal("#808080", StillClip.FromHsl(123, 0, 0.5));
        Assert.Equal("#FFFFFF", StillClip.FromHsl(0, 1, 1));
        Assert.Equal("#CC3333", StillClip.FromHsl(360, 0.6, 0.5));
        foreach (var renk in new[] { "#3366CC", "#00FF7F", "#123456", "#FEDCBA", "#000000", "#FFFFFF" })
        {
            Assert.True(StillClip.TryHsl(renk, out var h, out var s, out var l));
            Assert.Equal(renk, StillClip.FromHsl(h, s, l));
        }

        Assert.True(StillClip.TryHsl("#3366CC", out var ton, out var doygunluk, out var aciklik));
        Assert.Equal(220, ton, 6);
        Assert.Equal(0.6, doygunluk, 6);
        Assert.Equal(0.5, aciklik, 6);
        Assert.False(StillClip.TryHsl("mavi", out _, out _, out _));
    }

    [Fact]
    public void SlaytlarSonaTekAdimdaEklenirGecisVeMuzikBirlikteGeriAlinir()
    {
        var model = EditTimeline.FromSource(S(10));
        var plan = Slideshow.Plan(3, 3, 1, 30);
        var gecis = new ClipTransition(TransitionKind.Dissolve, S(plan.TransitionSeconds));
        var muzik = new EditMusic("m.mp3", S(20), S(10));
        var sureler = plan.Seconds.Select(S).ToArray();

        Assert.Equal(1, model.AppendSources(sureler, gecis, joinPrevious: false, muzik));
        Assert.Equal(4, model.Clips.Count);
        Assert.Null(model.Clips[1].Transition);
        Assert.Equal(gecis, model.Clips[2].Transition);
        Assert.Equal(gecis, model.Clips[3].Transition);
        Assert.Equal(new[] { 1, 2, 3 }, model.Clips.Skip(1).Select(c => c.Source));
        Assert.Equal(muzik, model.Music);
        Assert.Equal(S(10 + 9), model.Duration - model.TransitionOverlap);

        Assert.True(model.Undo());
        Assert.Single(model.Clips);
        Assert.Null(model.Music);
        Assert.True(model.Redo());
        Assert.Equal(4, model.Clips.Count);
        Assert.Equal(muzik, model.Music);

        var bos = EditTimeline.FromSource(sureler[0]);
        Assert.Equal(1, bos.AppendSources(sureler[1..], gecis, joinPrevious: true));
        Assert.Equal(gecis, bos.Clips[1].Transition);
        Assert.Equal(S(9), bos.Duration - bos.TransitionOverlap);

        Assert.Throws<ArgumentException>(() => model.AppendSources(Array.Empty<long>(), null, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.AppendSources(new[] { 0L }, null, false));
    }

    [Fact]
    public void BudamaKullanilaniVeTazeyiKorurEskiArtigiSiler()
    {
        var klasor = Klasor("buda");
        try
        {
            var ara = Path.Combine(klasor, StillClip.FolderName);
            var tut = Sahte(ara, "tut.mp4");
            var eski = Sahte(ara, "eski.mp4");
            var taze = Sahte(ara, "taze.mp4");
            var simdi = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(tut, simdi.AddDays(-3));
            File.SetLastWriteTimeUtc(eski, simdi.AddDays(-3));
            File.SetLastWriteTimeUtc(taze, simdi.AddMinutes(-5));

            Assert.Equal(1, StillClip.Prune(ara, new[] { Path.Combine(ara, ".", "tut.mp4") }, simdi, StillClip.PruneGrace));
            Assert.True(File.Exists(tut));
            Assert.False(File.Exists(eski));
            Assert.True(File.Exists(taze));
            Assert.Equal(0, StillClip.Prune(Path.Combine(klasor, "yok"), Array.Empty<string>(), simdi, StillClip.PruneGrace));
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void ProjeKaydiAraKlipleriBudarOkunamayanProjeVarsaDokunmaz()
    {
        var klasor = Klasor("depo");
        try
        {
            var kaynak = Sahte(klasor, "kaynak.mp4");
            var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
            var ara = Path.Combine(depo.Folder, StillClip.FolderName);
            var tut = Sahte(ara, "tut.mp4");
            var eski = Sahte(ara, "eski.mp4");
            var eskiZaman = DateTime.UtcNow.AddDays(-2);
            File.SetLastWriteTimeUtc(tut, eskiZaman);
            File.SetLastWriteTimeUtc(eski, eskiZaman);

            var bozuk = Path.Combine(depo.Folder, "bozuk" + EditProject.Extension);
            File.WriteAllText(bozuk, "{");
            var model = EditTimeline.FromSource(S(600));
            model.AddSource(S(5));
            var proje = EditProject.From(SourceStamp.Of(kaynak)!, model, extraSources: new[] { SourceStamp.Of(tut)! });
            Assert.True(depo.Save(proje));
            Assert.True(File.Exists(eski));

            File.Delete(bozuk);
            Assert.True(depo.Save(proje));
            Assert.True(File.Exists(tut));
            Assert.False(File.Exists(eski));

            var kullanilan = Sahte(ara, "acik.mp4");
            File.SetLastWriteTimeUtc(kullanilan, eskiZaman);
            Assert.Equal(0, StillClip.Prune(depo, new[] { kullanilan }));
            Assert.True(File.Exists(kullanilan));
            Assert.Equal(1, StillClip.Prune(depo));
            Assert.False(File.Exists(kullanilan));
        }
        finally
        {
            Kapat(klasor);
        }
    }

    private static EditorView Gorunum(string kok, List<IReadOnlyList<string>> kosumlar)
    {
        var view = new EditorView
        {
            KnownInfo = yol => new MediaInfo
            {
                FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1,
                AudioCodec = yol.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "mp3" : null
            },
            Projects = null,
            StillRoot = kok,
            StillRunner = (args, _) =>
            {
                kosumlar.Add(args);
                File.WriteAllBytes(args[^1], new byte[16]);
                return Task.FromResult(new FfmpegRun(true, 0, string.Empty, TimeSpan.Zero));
            },
            PeakReader = (_, _) => Task.FromResult(AudioPeaks.Empty),
            KeyframeReader = (_, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()),
        };
        view.Player.EngineFactory = () => new KlipMotoru();
        return view;
    }

    private static T Bekle<T>(EditorView view, Task<T> gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 10);
        Assert.True(gorev.IsCompleted, "gorev 10 sn icinde bitmedi");
        return gorev.GetAwaiter().GetResult();
    }

    private static void Bekle(EditorView view, Task gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 10);
        Assert.True(gorev.IsCompleted, "gorev 10 sn icinde bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static void Pencerede(string klasor, Action<EditorView, List<IReadOnlyList<string>>, string> govde)
    {
        var kok = Path.Combine(klasor, "ara");
        var kosumlar = new List<IReadOnlyList<string>>();
        AppHost.Run(() =>
        {
            var view = Gorunum(kok, kosumlar);
            var pencere = new Window { Width = 900, Height = 600, Content = view };
            pencere.Show();
            try
            {
                govde(view, kosumlar, kok);
            }
            finally
            {
                DuzenleyiciKapanis.Kapat(pencere, view);
            }
        });
    }

    [Fact]
    public void BosCizelgeyeTekResimSecilenSureyleAcilir()
    {
        var klasor = Klasor("tek-resim");
        try
        {
            var resim = Sahte(klasor, "kapak.png");
            Pencerede(klasor, (view, kosumlar, kok) =>
            {
                view.ImageSeconds = 4;
                Assert.Equal("4", view.TxtStillImageSeconds.Text);
                Bekle(view, view.TakeFilesAsync(new[] { resim }));
                var kosum = Assert.Single(kosumlar);
                Assert.Equal(resim, Sonraki(kosum, "-i"));
                Assert.Equal("4", Sonraki(kosum, "-t"));
                Assert.Contains("scale=320:240", Sonraki(kosum, "-vf"));
                Assert.NotNull(view.Model);
                Assert.Single(view.SourcePaths);
                Assert.Equal(kok, Path.GetDirectoryName(view.SourcePaths[0]));
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void DurgunSureleriVeRenkSeciciKosumaYansir()
    {
        var klasor = Klasor("ayar");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            Pencerede(klasor, (view, kosumlar, _) =>
            {
                Bekle(view, view.OpenSourceAsync(a));
                view.ShowStillPanel(true);
                Assert.True(view.StillPanel.IsVisible);

                view.TxtStillFreezeSeconds.Text = "1.5";
                view.TxtStillImageSeconds.Text = "0.01";
                view.TxtStillImageSeconds.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
                Assert.Equal(1.5, view.FreezeSeconds);
                Assert.Equal(StillClip.MinSeconds, view.ImageSeconds);
                view.ImageSeconds = 1000;
                Assert.Equal(StillClip.MaxSeconds, view.ImageSeconds);
                view.ImageSeconds = 2.5;

                view.Timeline.Playhead = S(10);
                Assert.True(Bekle(view, view.FreezeFrameAsync()));
                Assert.Equal("1.5", Sonraki(kosumlar[^1], "-t"));
                Assert.Equal(S(1.5), view.Model!.Clips[1].SourceEnd);

                view.StillColor = "3366cc";
                Assert.Equal("#3366CC", view.StillColor);
                Assert.Equal(220, view.SldStillHue.Value, 6);
                Assert.Equal(60, view.SldStillSaturation.Value, 6);
                Assert.Equal(50, view.SldStillLightness.Value, 6);
                view.SldStillHue.Value = 0;
                Assert.Equal("#CC3333", view.StillColor);
                Assert.Equal("#CC3333", view.TxtStillColor.Text);
                view.StillColor = "kirmizi";
                Assert.Equal("#CC3333", view.StillColor);

                var once = kosumlar.Count;
                Assert.True(Bekle(view, view.AddPickedColorAsync()));
                Assert.Equal(once + 1, kosumlar.Count);
                Assert.Equal("color=c=0xCC3333:s=320x240:r=30:d=2.5", Sonraki(kosumlar[^1], "-i"));
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void MonitordeSuruklemeKonumuBirakincaTekAdimdaYazar()
    {
        var klasor = Klasor("surukle");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            Pencerede(klasor, (view, _, _) =>
            {
                Bekle(view, view.OpenSourceAsync(a));
                var model = view.Model!;
                Assert.False(view.BeginClipMove(0.5, 0.5));

                view.TimelineView.SelectedIndex = 0;
                view.BtnClipMove.IsChecked = true;
                view.BtnClipBlurRegion.IsChecked = true;
                Assert.False(view.BtnClipMove.IsChecked);
                view.BtnClipMove.IsChecked = true;
                Assert.False(view.BtnClipBlurRegion.IsChecked);

                Assert.True(view.BeginClipMove(0.5, 0.5));
                Assert.True(view.ClipMoving);
                Assert.True(view.DragClipTo(0.7, 0.4));
                Assert.Equal("20", view.TxtClipOffsetX.Text);
                Assert.Equal("-10", view.TxtClipOffsetY.Text);
                Assert.Equal(0, model.Clips[0].Effects.OffsetX);
                Assert.True(view.EndClipMove());
                Assert.False(view.ClipMoving);
                Assert.Equal(0.2, model.Clips[0].Effects.OffsetX, 9);
                Assert.Equal(-0.1, model.Clips[0].Effects.OffsetY, 9);

                Assert.True(view.BeginClipMove(0, 0));
                view.DragClipTo(1, 1);
                Assert.True(view.EndClipMove());
                Assert.Equal(0.5, model.Clips[0].Effects.OffsetX);
                Assert.Equal(0.5, model.Clips[0].Effects.OffsetY);

                Assert.True(view.BeginClipMove(0.3, 0.3));
                Assert.False(view.EndClipMove());
                Assert.Equal(0.5, model.Clips[0].Effects.OffsetX);

                Assert.True(view.Undo());
                Assert.Equal(0.2, model.Clips[0].Effects.OffsetX, 9);
                Assert.True(view.Undo());
                Assert.Equal(0, model.Clips[0].Effects.OffsetX);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void SihirbazDoluCizelgeninSonunaSlaytlariGecisVeMuzikleDizer()
    {
        var klasor = Klasor("sihirbaz");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var resimler = new[] { Sahte(klasor, "c.png"), Sahte(klasor, "a.png"), Sahte(klasor, "b.jpg"), Sahte(klasor, "not.txt") };
            var muzik = Sahte(klasor, "m.mp3");
            Pencerede(klasor, (view, kosumlar, _) =>
            {
                Bekle(view, view.OpenSourceAsync(a));
                var model = view.Model!;
                view.SlideImages = resimler;
                Assert.Equal(3, view.SlideImages.Count);
                view.ShowSlidePanel(true);
                Assert.True(view.SlidePanel.IsVisible);
                Assert.True(view.BtnSlideBuild.IsEnabled);

                Assert.True(Bekle(view, view.BuildSlideshowAsync(resimler, 3, TransitionKind.Dissolve, 1, muzik, fitMusic: false)));
                Assert.Equal(3, kosumlar.Count);
                Assert.Equal(new[] { "a.png", "b.jpg", "c.png" }, kosumlar.Select(k => Path.GetFileName(Sonraki(k, "-i"))));
                Assert.Equal(new[] { "3.5", "4", "3.5" }, kosumlar.Select(k => Sonraki(k, "-t")));
                Assert.Equal(4, model.Clips.Count);
                Assert.Null(model.Clips[1].Transition);
                Assert.Equal(TransitionKind.Dissolve, model.Clips[2].Transition!.Kind);
                Assert.Equal(S(1), model.Clips[3].Transition!.Duration);
                Assert.Equal(S(600 + 9), model.Duration - model.TransitionOverlap);
                Assert.Equal(muzik, model.Music!.Path);
                Assert.Equal(S(600), model.Music.Start);
                Assert.Contains("3", view.SlideStatusText);

                Assert.True(view.Undo());
                Assert.Single(model.Clips);
                Assert.Null(model.Music);

                Assert.True(Bekle(view, view.BuildSlideshowAsync(resimler[..2], 3, null, 1, muzik, fitMusic: true)));
                Assert.Equal(new[] { "60", "60" }, kosumlar.Skip(3).Select(k => Sonraki(k, "-t")));
                Assert.Null(model.Clips[1].Transition);
                Assert.Null(model.Clips[2].Transition);

                Assert.False(Bekle(view, view.BuildSlideshowAsync(new[] { a }, 3, null, 1, null, false)));
                Assert.False(Bekle(view, view.BuildSlideshowAsync(resimler, 3, null, 1, a, false)));
                Assert.Equal(3, model.Clips.Count);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void SihirbazBosDuzenleyicideIlkSlaytiAcarKalaniGecisleBaglar()
    {
        var klasor = Klasor("bos");
        try
        {
            var resimler = new[] { Sahte(klasor, "1.png"), Sahte(klasor, "2.png"), Sahte(klasor, "3.png") };
            var muzik = Sahte(klasor, "m.mp3");
            Pencerede(klasor, (view, kosumlar, kok) =>
            {
                Assert.True(Bekle(view, view.BuildSlideshowAsync(resimler, 2, TransitionKind.Wipe, 0.5, muzik, fitMusic: false)));
                Assert.Equal(3, kosumlar.Count);
                var model = view.Model!;
                Assert.Equal(3, model.Clips.Count);
                Assert.Equal(kok, Path.GetDirectoryName(view.SourcePaths[0]));
                Assert.Equal(kosumlar[0][^1].Replace(".part", string.Empty, StringComparison.Ordinal), view.SourcePaths[0]);
                Assert.Null(model.Clips[0].Transition);
                Assert.Equal(TransitionKind.Wipe, model.Clips[1].Transition!.Kind);
                Assert.Equal(TransitionKind.Wipe, model.Clips[2].Transition!.Kind);
                Assert.Equal(0, model.Music!.Start);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }
}
