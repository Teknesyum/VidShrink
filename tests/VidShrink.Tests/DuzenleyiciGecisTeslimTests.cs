using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZB-P6, iki klip arasinda capraz gecis (<see cref="ClipTransition"/>): Tam kipte goruntu <c>xfade</c>, ses <c>acrossfade</c>
/// ile ust uste biner; <c>offset</c> o ana dek birlesen uzunluk eksi gecis suresidir, her klip zinciri <c>settb=AVTB</c> ile
/// biter. Gecis Hizli ve Akilli kipi Tam'a dusurur (<see cref="ExportPlan.TransitionsForcedFull"/>); gecissiz cizelgenin
/// grafigi eski <c>concat</c> bicimindedir. Gecis suresi komsu kliplerin kisasinin yarisina kirpilir. Canli kol iki 2 sn'lik
/// parcayi 0,5 sn gecisle teslim edip suresinin 3,5 sn oldugunu ffprobe ile okur, gecissiz es 4 sn.
/// Arayuz kolu sahte <see cref="KlipMotoru"/> ile: klip panelindeki gecis secimi ve suresi ikinci klibe yazilir, sure
/// 0,1-5 sn araligina kirpilir, "Yok" gecisi kaldirir, geri alma calisir ve EDL yeniden acilmaz.
/// Kanit <c>.calisma/worktree-agent-a08ba4829402285fe/gecis/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciGecisTeslimTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a08ba4829402285fe", "gecis");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        BitDepth = 8,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 2)
        }
    };

    private static ClipTransition Gecis(TransitionKind tur, double saniye) => new(tur, S(saniye));

    private static EditTimeline IkiKlip(ClipTransition? gecis = null)
        => new(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) { Transition = gecis } });

    private static ExportPlan Plan(ExportMode kip, EditTimeline cizelge)
        => EditExport.Build(cizelge, Bilgi(), Kareler, 0, kip, "c.mp4", "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true);

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static string Grafik(ExportPlan plan) => Deger(Assert.Single(plan.Steps).Args, "-filter_complex");

    [Fact]
    public void TamKipteXfadeVeAcrossfadeYazilir()
    {
        var plan = Plan(ExportMode.Full, IkiKlip(Gecis(TransitionKind.Dissolve, 0.5)));
        var grafik = Grafik(plan);

        Assert.Contains("setpts=PTS-STARTPTS,fps=30,format=yuv420p,settb=AVTB[v0];", grafik);
        Assert.Contains("setpts=PTS-STARTPTS,fps=30,format=yuv420p,settb=AVTB[v1];", grafik);
        Assert.EndsWith("[v0][v1]xfade=transition=fade:duration=0.5:offset=1.5[vout];[a0][a1]acrossfade=d=0.5[aout]", grafik);
        Assert.DoesNotContain("concat", grafik);
        Assert.Equal(3.5, Assert.Single(plan.Steps).DurationSeconds, 6);
    }

    [Fact]
    public void GecissizGrafikEskiConcatBicimindedir()
    {
        var grafik = Grafik(Plan(ExportMode.Full, IkiKlip()));

        Assert.EndsWith("[v0][a0][v1][a1]concat=n=2:v=1:a=1[vout][aout]", grafik);
        Assert.DoesNotContain("settb", grafik);
        Assert.DoesNotContain("format=yuv420p", grafik);
        Assert.DoesNotContain("xfade", grafik);
    }

    [Fact]
    public void UcKliptetOffsetBirikirGecissizKomsuConcatOlur()
    {
        var cizelge = new EditTimeline(new[]
        {
            new EditClip(S(0), S(2)),
            new EditClip(S(2), S(4)) { Transition = Gecis(TransitionKind.Wipe, 0.5) },
            new EditClip(S(4), S(6)) { Transition = Gecis(TransitionKind.Iris, 1) },
            new EditClip(S(6), S(8))
        });
        var plan = Plan(ExportMode.Full, cizelge);
        var grafik = Grafik(plan);

        Assert.Contains("[v0][v1]xfade=transition=wipeleft:duration=0.5:offset=1.5[x1];", grafik);
        Assert.Contains("[a0][a1]acrossfade=d=0.5[y1_0];", grafik);
        Assert.Contains("[x1][v2]xfade=transition=circleopen:duration=1:offset=2.5[x2];", grafik);
        Assert.Contains("[y1_0][a2]acrossfade=d=1[y2_0];", grafik);
        Assert.Contains("[x2][v3]concat=n=2:v=1:a=0[vout];", grafik);
        Assert.EndsWith("[y2_0][a3]concat=n=2:v=0:a=1[aout]", grafik);
        Assert.Equal(6.5, Assert.Single(plan.Steps).DurationSeconds, 6);
    }

    [Theory]
    [InlineData(TransitionKind.Dissolve, "fade")]
    [InlineData(TransitionKind.Wipe, "wipeleft")]
    [InlineData(TransitionKind.Slide, "slideleft")]
    [InlineData(TransitionKind.Iris, "circleopen")]
    public void TurXfadeAdinaIner(TransitionKind tur, string ad)
    {
        Assert.Contains("xfade=transition=" + ad + ":", Grafik(Plan(ExportMode.Full, IkiKlip(Gecis(tur, 1)))));
        Assert.Equal(tur, ClipTransition.FromKey(ClipTransition.Key(tur)));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void GecisHizliVeAkilliKipiTamaDusurur(ExportMode kip)
    {
        var plan = Plan(kip, IkiKlip(Gecis(TransitionKind.Slide, 0.5)));

        Assert.Equal(kip, plan.Requested);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.TransitionsForcedFull);
        Assert.Contains("xfade=transition=slideleft", Grafik(plan));

        var gecissiz = Plan(kip, IkiKlip());
        Assert.Equal(kip, gecissiz.Effective);
        Assert.False(gecissiz.TransitionsForcedFull);
    }

    [Fact]
    public void EfektDeTamaDusururkenGecisBayragiKalkmaz()
    {
        var cizelge = new EditTimeline(new[]
        {
            new EditClip(S(1), S(3)) { Effects = ClipEffects.None with { VolumeDb = -6 } },
            new EditClip(S(4), S(6)) { Transition = Gecis(TransitionKind.Dissolve, 0.5) }
        });
        var plan = Plan(ExportMode.Fast, cizelge);

        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.EffectsForcedFull);
        Assert.False(plan.TransitionsForcedFull);
    }

    [Fact]
    public void IlkKliptekiGecisYokSayilir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)) { Transition = Gecis(TransitionKind.Dissolve, 1) }, new EditClip(S(4), S(6)) });

        Assert.False(cizelge.HasTransitions);
        Assert.Equal(0, cizelge.TransitionLength(0));
        Assert.Equal(0, cizelge.TransitionOverlap);
        Assert.Equal(ExportMode.Fast, Plan(ExportMode.Fast, cizelge).Effective);
    }

    [Fact]
    public void GecisKisaKlibinYarisinaKirpilir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(2), S(3)) { Transition = Gecis(TransitionKind.Dissolve, 5) } });

        Assert.Equal(S(0.5), cizelge.TransitionLength(1));
        Assert.Equal(S(0.5), cizelge.TransitionOverlap);
        Assert.Equal(S(3), cizelge.Duration);
        Assert.Contains(":duration=0.5:offset=1.5[vout]", Grafik(Plan(ExportMode.Full, cizelge)));
    }

    [Fact]
    public void GecisSiniriDisindaReddedilir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClipTransition(TransitionKind.Dissolve, S(0.05)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClipTransition(TransitionKind.Dissolve, S(5.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClipTransition((TransitionKind)9, S(1)));
        Assert.Equal(S(0.1), new ClipTransition(TransitionKind.Wipe, S(0.1)).Duration);
        Assert.Equal(S(5), new ClipTransition(TransitionKind.Wipe, S(5)).Duration);
        Assert.Null(ClipTransition.FromKey("uydurma"));
    }

    [Fact]
    public void GecisYazmaGeriAlinirVeBolmeBasaBirakir()
    {
        var cizelge = IkiKlip();
        var gecis = Gecis(TransitionKind.Iris, 1);

        Assert.True(cizelge.SetTransition(new[] { 1 }, gecis));
        Assert.Equal(gecis, cizelge.Clips[1].Transition);
        Assert.False(cizelge.SetTransition(new[] { 1 }, gecis));
        Assert.True(cizelge.Undo());
        Assert.Null(cizelge.Clips[1].Transition);
        Assert.True(cizelge.Redo());
        Assert.Equal(gecis, cizelge.Clips[1].Transition);

        Assert.True(cizelge.Split(cizelge.ClipStart(1) + S(1)));
        Assert.Equal(gecis, cizelge.Clips[1].Transition);
        Assert.Null(cizelge.Clips[2].Transition);
    }

    [Fact]
    public void BirlestirmeDeGecisZinciriKurar()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi() with { FilePath = "birinci.mp4", Streams = Array.Empty<SourceStream>() }, new[] { 0.0 }, 0),
            [1] = new(Bilgi() with { FilePath = "ikinci.mp4", Width = 640, Height = 360, Streams = Array.Empty<SourceStream>() }, new[] { 0.0 }, 0)
        };
        var cizelge = EditTimeline.FromSource(S(10));
        Assert.Equal(1, cizelge.AddSource(S(4)));
        Assert.True(cizelge.SetTransition(new[] { 1 }, Gecis(TransitionKind.Dissolve, 1)));

        var plan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        var grafik = Grafik(plan);

        Assert.Contains(",settb=AVTB[v0];", grafik);
        Assert.Contains(",settb=AVTB[v1];", grafik);
        Assert.Contains("[v0][v1]xfade=transition=fade:duration=1:offset=9[vout];", grafik);
        Assert.EndsWith("[a0][a1]acrossfade=d=1[aout]", grafik);
        Assert.Equal(13, Assert.Single(plan.Steps).DurationSeconds, 6);

        Assert.True(cizelge.SetTransition(new[] { 1 }, null));
        var gecissiz = Grafik(EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb));
        Assert.DoesNotContain("settb", gecissiz);
        Assert.EndsWith("concat=n=2:v=1:a=1[vout][aout]", gecissiz);
    }

    [Fact]
    public void ProjeDosyasiGecisiTasir()
    {
        var damga = new SourceStamp("kaynak.mp4", 10, 20);
        var cizelge = IkiKlip(Gecis(TransitionKind.Slide, 0.75));

        var json = EditProject.From(damga, cizelge).ToJson();
        var geri = EditProject.Parse(json);

        Assert.Contains("\"transition\"", json);
        Assert.NotNull(geri);
        Assert.Equal(cizelge.Clips[1].Transition, geri!.Clips[1].Transition);
        Assert.Null(geri.Clips[0].Transition);

        var eski = EditProject.From(damga, IkiKlip()).ToJson();
        Assert.DoesNotContain("transition", eski);
        Assert.All(EditProject.Parse(eski)!.Clips, c => Assert.Null(c.Transition));

        Assert.Null(EditProject.Parse(json.Replace("\"slide\"", "\"uydurma\"")));
    }

    [Theory]
    [InlineData(ExportMode.Fast, true)]
    [InlineData(ExportMode.Smart, true)]
    [InlineData(ExportMode.Full, false)]
    public void ZorlamaNotuYalnizDusulenKipteYazilir(ExportMode kip, bool zorlandi)
    {
        var eski = Strings.Language;
        try
        {
            Strings.Use("tr");
            var not = Strings.Get("editor.export.transitions-full");
            Assert.Equal("Geçişler yeniden kodlama ister; Tam kipine geçildi.", not);
            Assert.Equal(zorlandi, EditorView.Notes(new[] { Plan(kip, IkiKlip(Gecis(TransitionKind.Dissolve, 0.5))) }).Contains(not, StringComparison.Ordinal));
            Assert.DoesNotContain(not, EditorView.Notes(new[] { Plan(kip, IkiKlip()) }));
        }
        finally
        {
            Strings.Use(eski);
        }
    }

    [Fact]
    public void ArayuzGecisSecimiIkinciKlibeYazilirVeEdlYenidenAcilmaz()
    {
        var klasor = Path.Combine(Kanit, "arayuz");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 1, 10);
                    var model = view.Model!;
                    Assert.Equal(1, view.AutoSplitClips(AutoSplitMode.Count, 2));
                    Assert.Equal(2, model.Clips.Count);
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 2, 10);
                    var acilan = motor.Acilan;

                    view.TimelineView.SelectedIndex = 1;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal(5, view.CmbClipTransition.ItemCount);
                    Assert.Equal(0, view.CmbClipTransition.SelectedIndex);
                    Assert.False(view.TxtClipTransition.IsEnabled);

                    void Yaz(TextBox kutu, string metin)
                    {
                        kutu.Text = metin;
                        kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    }

                    view.CmbClipTransition.SelectedIndex = 1;
                    Assert.Equal(new ClipTransition(TransitionKind.Dissolve, S(1)), model.Clips[1].Transition);
                    Assert.Null(model.Clips[0].Transition);
                    Assert.True(view.TxtClipTransition.IsEnabled);
                    Assert.Equal("1", view.TxtClipTransition.Text);

                    Yaz(view.TxtClipTransition, "0.5");
                    Assert.Equal(new ClipTransition(TransitionKind.Dissolve, S(0.5)), model.Clips[1].Transition);

                    view.CmbClipTransition.SelectedIndex = 4;
                    Assert.Equal(new ClipTransition(TransitionKind.Iris, S(0.5)), model.Clips[1].Transition);

                    Yaz(view.TxtClipTransition, "9");
                    Assert.Equal(S(5), model.Clips[1].Transition!.Duration);
                    Assert.Equal("5", view.TxtClipTransition.Text);

                    Yaz(view.TxtClipTransition, "abc");
                    Assert.Equal(S(1), model.Clips[1].Transition!.Duration);

                    Assert.True(view.Undo());
                    Assert.Equal(new ClipTransition(TransitionKind.Iris, S(5)), model.Clips[1].Transition);
                    Assert.Equal(4, view.CmbClipTransition.SelectedIndex);

                    view.CmbClipTransition.SelectedIndex = 0;
                    Assert.Null(model.Clips[1].Transition);
                    Assert.False(view.TxtClipTransition.IsEnabled);
                    Assert.Equal(acilan, motor.Acilan);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kanit);
        }
    }

    [FfmpegFact]
    public async Task CanliTeslimGecisSuresiKadarKisalir()
    {
        string[] dosyalar = { "kaynak.mp4", "gecisli.mp4", "gecissiz.mp4", "olcu.txt" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=4",
            "-f", "lavfi", "-i", "sine=f=440:d=4",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
        var satirlar = new List<string>();

        foreach (var (gecis, ad, beklenen) in new[] { (Gecis(TransitionKind.Dissolve, 0.5), "gecisli.mp4", 3.5), ((ClipTransition?)null, "gecissiz.mp4", 4.0) })
        {
            var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(2), S(4)) { Transition = gecis } });
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Full, cikti, 8 * Gb);
            await EditExportRunner.RunAsync(plan, new Bos());
            var sureler = Ffprobe("-show_entries", "stream=codec_type,duration", "-of", "csv=p=0", cikti)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.Split(','))
                .ToDictionary(p => p[0], p => double.Parse(p[1], CultureInfo.InvariantCulture));
            satirlar.Add($"{ad} video={sureler["video"]:0.###} audio={sureler["audio"]:0.###} beklenen={beklenen}");

            Assert.InRange(sureler["video"], beklenen - 0.05, beklenen + 0.05);
            Assert.InRange(sureler["audio"], beklenen - 0.1, beklenen + 0.1);
        }

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    private static string Ffprobe(params string[] args)
    {
        var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return metin;
    }

    private sealed class Bos : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
        }
    }
}
