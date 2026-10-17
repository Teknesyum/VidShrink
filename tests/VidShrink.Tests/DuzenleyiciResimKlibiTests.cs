using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZB-04 resim klibi, DZB-05 duz renk klibi, DZB-49 kare dondurma: <see cref="StillClip"/> argumanlari, ara videonun
/// <see cref="EditTimeline.InsertSource"/> ile araya girmesi, duzenleyicide resim ekleme, renk klibi ve dondurma. Canli kol
/// 1 sn'lik ara videolari kodlayip ffprobe ile sureyi ve olcuyu olcer. Kanit <c>.calisma/yan/resim-klibi/*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciResimKlibiTests
{
    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "yan", "resim-klibi");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kok, "resim-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        KanitKapanisi.Kapat(Kok);
    }

    private static string Sonraki(IReadOnlyList<string> args, string anahtar)
    {
        var i = args.ToList().IndexOf(anahtar);
        Assert.True(i >= 0 && i + 1 < args.Count, anahtar + " yok: " + string.Join(' ', args));
        return args[i + 1];
    }

    [Fact]
    public void ResimArgumaniDongulerSureyiKeserOlcuyeSigdirir()
    {
        var args = StillClip.FromImage(@"C:\r\kapak%1.png", @"C:\o\c.mp4.part", 1280, 720, 29.97, 5);

        Assert.Equal("1", Sonraki(args, "-loop"));
        Assert.Equal("none", Sonraki(args, "-pattern_type"));
        Assert.Equal(@"C:\r\kapak%1.png", Sonraki(args, "-i"));
        Assert.Equal("5", Sonraki(args, "-t"));
        Assert.Equal("29.97", Sonraki(args, "-r"));
        Assert.Equal("30", Sonraki(args, "-g"));
        Assert.Equal("2", Sonraki(args, "-threads"));
        Assert.Equal("libx264", Sonraki(args, "-c:v"));
        Assert.Contains("scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1", Sonraki(args, "-vf"));
        Assert.Contains("-an", args);
        Assert.Equal(@"C:\o\c.mp4.part", args[^1]);
        Assert.Equal("mp4", args[^2]);
        Assert.True(args.ToList().IndexOf("-loop") < args.ToList().IndexOf("-i"));
    }

    [Fact]
    public void RenkArgumaniLavfiRenginiOlcuVeSureyleKurar()
    {
        var args = StillClip.FromColor("#ff8800", "c.mp4", 640, 360, 25, 5);

        Assert.Equal("lavfi", Sonraki(args, "-f"));
        Assert.Equal("color=c=0xFF8800:s=640x360:r=25:d=5", Sonraki(args, "-i"));
        Assert.Equal("25", Sonraki(args, "-g"));
        Assert.Throws<ArgumentException>(() => StillClip.FromColor("kirmizi", "c.mp4", 640, 360, 25, 5));

        Assert.True(StillClip.TryColor("00aaFF", out var renk));
        Assert.Equal("0x00AAFF", renk);
        Assert.False(StillClip.TryColor("#12345", out _));
        Assert.False(StillClip.TryColor("#1234567", out _));
        Assert.False(StillClip.TryColor("#GG0000", out _));
        Assert.False(StillClip.TryColor(null, out _));
    }

    [Fact]
    public void KareCikarmaAramayiGirdidenOnceYaparTekKareYazar()
    {
        var args = StillClip.GrabFrame(@"C:\v\a.mp4", 12.5, @"C:\o\k.png");

        Assert.Equal("12.5", Sonraki(args, "-ss"));
        Assert.True(args.ToList().IndexOf("-ss") < args.ToList().IndexOf("-i"));
        Assert.Equal("1", Sonraki(args, "-frames:v"));
        Assert.Equal("0:v:0", Sonraki(args, "-map"));
        Assert.Equal(@"C:\o\k.png", args[^1]);
        Assert.Equal("0", Sonraki(StillClip.GrabFrame("a.mp4", -3, "k.png"), "-ss"));
    }

    [Fact]
    public void KareOlcusuGorunenOlcudenCiftSayiyaIner()
    {
        Assert.Equal((1920, 1080), StillClip.Frame(null));
        Assert.Equal(30, StillClip.Rate(null));
        var bilgi = new MediaInfo { FilePath = "a.mp4", FileSizeBytes = 1, DurationSeconds = 1, Width = 721, Height = 481, Fps = 24, VideoCodec = "h264", TotalBitrateBps = 1 };
        Assert.Equal((720, 480), StillClip.Frame(bilgi));
        Assert.Equal(24, StillClip.Rate(bilgi));
        var anamorfik = bilgi with { Width = 720, Height = 576, ParNum = 16, ParDen = 11 };
        Assert.Equal((1046, 576), StillClip.Frame(anamorfik));
    }

    [Fact]
    public void AraVideoAdiGirdiyeBagliVeKararli()
    {
        var a = StillClip.OutputName("resim", "a.png", 10, 20, 1280, 720, 30.0, 5.0);
        Assert.Equal(a, StillClip.OutputName("resim", "a.png", 10, 20, 1280, 720, 30.0, 5.0));
        Assert.NotEqual(a, StillClip.OutputName("resim", "a.png", 10, 21, 1280, 720, 30.0, 5.0));
        Assert.NotEqual(a, StillClip.OutputName("kare", "a.png", 10, 20, 1280, 720, 30.0, 5.0));
        Assert.StartsWith("resim-", a, StringComparison.Ordinal);
        Assert.EndsWith(".mp4", a, StringComparison.Ordinal);
    }

    [Fact]
    public void KaynakArayaEklenirSonrakileriIterTekAdimdaGeriAlinir()
    {
        var model = EditTimeline.FromSource(S(10));

        Assert.Equal(1, model.InsertSource(S(2), S(4)));
        Assert.Equal(new[] { new EditClip(0, S(4)), new EditClip(0, S(2)) { Source = 1 }, new EditClip(S(4), S(10)) }, model.Clips);
        Assert.Equal(S(12), model.Duration);
        Assert.Equal(S(2), model.DurationOf(1));

        Assert.True(model.Undo());
        Assert.Single(model.Clips);
        Assert.Single(model.ExtraSources);
        Assert.True(model.Redo());
        Assert.Equal(3, model.Clips.Count);

        Assert.Equal(3, model.InsertSource(S(5), model.Duration));
        Assert.Equal(2, model.ExtraSources.Count);
        Assert.Equal(new EditClip(0, S(5)) { Source = 2 }, model.Clips[^1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.InsertSource(S(1), model.Duration + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.InsertSource(0, 0));
    }

    private static EditorView Gorunum(string kok, List<IReadOnlyList<string>> kosumlar, bool basarili = true)
    {
        var view = new EditorView
        {
            KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
            Projects = null,
            StillRoot = kok,
            StillRunner = (args, _) =>
            {
                kosumlar.Add(args);
                if (basarili) File.WriteAllBytes(args[^1], new byte[16]);
                return Task.FromResult(new FfmpegRun(basarili, basarili ? 0 : 1, string.Empty, TimeSpan.Zero));
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

    private static string Sahte(string klasor, string ad)
    {
        var dosya = Path.Combine(klasor, ad);
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    [Fact]
    public void ResimSonaAraVideoOlarakEklenirRenkVeDondurmaOynatmaBasinaGirer()
    {
        var klasor = Klasor("arayuz");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var resim = Sahte(klasor, "kapak.png");
            var kok = Path.Combine(klasor, "ara");
            var kosumlar = new List<IReadOnlyList<string>>();

            AppHost.Run(() =>
            {
                var view = Gorunum(kok, kosumlar);
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Bekle(view, view.OpenSourceAsync(a));
                    Assert.True(Bekle(view, view.AddSourceAsync(resim)));
                    var kosum = Assert.Single(kosumlar);
                    Assert.Equal(resim, Sonraki(kosum, "-i"));
                    Assert.Contains("scale=320:240", Sonraki(kosum, "-vf"));
                    Assert.Equal("5", Sonraki(kosum, "-t"));
                    Assert.Equal(2, view.SourcePaths.Count);
                    Assert.Equal(kok, Path.GetDirectoryName(view.SourcePaths[1]));
                    Assert.Empty(Directory.GetFiles(kok, "*.part"));
                    Assert.Equal(new EditClip(0, S(5)) { Source = 1 }, view.Model!.Clips[1]);

                    Assert.True(Bekle(view, view.AddSourceAsync(resim)));
                    Assert.Single(kosumlar);
                    Assert.Equal(view.SourcePaths[1], view.SourcePaths[2]);

                    view.Timeline.Playhead = S(10);
                    Assert.True(Bekle(view, view.FreezeFrameAsync()));
                    Assert.Equal(3, kosumlar.Count);
                    Assert.Equal("10", Sonraki(kosumlar[1], "-ss"));
                    Assert.Equal(a, Sonraki(kosumlar[1], "-i"));
                    Assert.Equal(kosumlar[1][^1], Sonraki(kosumlar[2], "-i"));
                    Assert.Equal("2", Sonraki(kosumlar[2], "-t"));
                    Assert.False(File.Exists(kosumlar[1][^1]));
                    var model = view.Model!;
                    Assert.Equal(new EditClip(0, S(10)), model.Clips[0]);
                    Assert.Equal(new EditClip(0, S(2)) { Source = 3 }, model.Clips[1]);
                    Assert.Equal(new EditClip(S(10), S(600)), model.Clips[2]);
                    Assert.Equal(1, view.Timeline.SelectedIndex);

                    view.Timeline.Playhead = S(12);
                    Assert.True(Bekle(view, view.AddColorClipAsync(StillClip.White)));
                    Assert.Equal("color=c=0xFFFFFF:s=320x240:r=30:d=5", Sonraki(kosumlar[3], "-i"));
                    Assert.Equal(new EditClip(0, S(5)) { Source = 4 }, model.Clips[2]);
                    Assert.Equal(S(10 + 2 + 5 + 590 + 5 + 5), model.Duration);
                    Assert.True(view.Undo());
                    Assert.Equal(S(10 + 2 + 590 + 5 + 5), model.Duration);
                    Assert.False(Bekle(view, view.AddColorClipAsync("mavi")));
                    Assert.Equal(4, kosumlar.Count);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void AraVideoYapilamazsaCizelgeDegismezDurumSatiriSoyler()
    {
        var klasor = Klasor("hata");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var resim = Sahte(klasor, "kapak.jpg");
            var kok = Path.Combine(klasor, "ara");
            var kosumlar = new List<IReadOnlyList<string>>();

            AppHost.Run(() =>
            {
                var view = Gorunum(kok, kosumlar, basarili: false);
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Bekle(view, view.OpenSourceAsync(a));
                    Assert.False(Bekle(view, view.AddSourceAsync(resim)));
                    Assert.False(Bekle(view, view.FreezeFrameAsync()));
                    Assert.Equal(2, kosumlar.Count);
                    Assert.Single(view.Model!.Clips);
                    Assert.Single(view.SourcePaths);
                    Assert.Contains("a.mp4", view.ExportStatusText);
                    Assert.Empty(Directory.GetFiles(kok));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void BirakilanlarArasindaIlkVideoAcilirResimSonraEklenir()
    {
        var klasor = Klasor("birak");
        try
        {
            var resim = Sahte(klasor, "kapak.png");
            var a = Sahte(klasor, "a.mp4");
            var kok = Path.Combine(klasor, "ara");
            var kosumlar = new List<IReadOnlyList<string>>();

            AppHost.Run(() =>
            {
                var view = Gorunum(kok, kosumlar);
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Bekle(view, view.TakeFilesAsync(new[] { resim, a }));
                    Assert.Equal(a, view.SourcePaths[0]);
                    Assert.Equal(2, view.SourcePaths.Count);
                    Assert.Equal(resim, Sonraki(Assert.Single(kosumlar), "-i"));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [FfmpegFact]
    public async Task AraVideolarSureyiVeOlcuyuTutar()
    {
        var klasor = Klasor("canli");
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var olustur = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=160x120:r=25:d=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-threads", "2", kaynak
            });
            Assert.True(olustur.Ok, olustur.StandardError);

            var kare = Path.Combine(klasor, "kare.png");
            var cikar = await FfmpegRunner.RunAsync(StillClip.GrabFrame(kaynak, 1.0, kare));
            Assert.True(cikar.Ok, cikar.StandardError);
            Assert.True(File.Exists(kare));

            var resim = Path.Combine(klasor, "resim.mp4");
            var r = await FfmpegRunner.RunAsync(StillClip.FromImage(kare, resim, 96, 96, 25, 1));
            Assert.True(r.Ok, r.StandardError);
            var renk = Path.Combine(klasor, "renk.mp4");
            var c = await FfmpegRunner.RunAsync(StillClip.FromColor("#202020", renk, 64, 48, 30, 1));
            Assert.True(c.Ok, c.StandardError);

            var ri = await FfprobeClient.ProbeAsync(resim);
            Assert.InRange(ri.DurationSeconds, 0.95, 1.05);
            Assert.Equal(96, ri.Width);
            Assert.Equal(96, ri.Height);
            Assert.Equal(25.0, ri.Fps, 1);
            Assert.False(ri.HasAudio);

            var ci = await FfprobeClient.ProbeAsync(renk);
            Assert.InRange(ci.DurationSeconds, 0.95, 1.05);
            Assert.Equal(64, ci.Width);
            Assert.Equal(48, ci.Height);
            Assert.Equal(30.0, ci.Fps, 1);
        }
        finally
        {
            Kapat(klasor);
        }
    }
}
