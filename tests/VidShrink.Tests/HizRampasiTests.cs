using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Hiz rampasi (DZB-48) ve akici agir cekim (DZB-50). Rampa klibi kaynakta esit dilimlere boler,
/// dilim hizlari dogrusal gider; her dilim disa aktarimda kendi <c>setpts</c>/<c>atempo</c>
/// zincirini, onizlemede kendi EDL parcasini ve oynatma hizini tasir. Akici agir cekim yavas
/// klipte <c>fps</c> yerine <c>minterpolate</c> yazar. Canli olcu ffprobe ile cikti suresinin
/// cizelge suresine esitligini ve ara karenin gercekten uretildigini sayar.
/// 18 Ekim 2026: onizleme olcumu - <c>EdlPreviewDriver.ApplySpeed</c> parca basina hiz yazar,
/// rampa dilimleri ayri EDL parcasi oldugu icin onizlemede tasinir; <c>minterpolate</c> onizlemede
/// yok, yalniz disa aktarimda.
/// </summary>
public sealed class HizRampasiTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

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
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };

    private static IReadOnlyList<ExportStep> Adimlar(EditTimeline c, ExportMode kip)
        => EditExport.Build(c, Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb).Steps;

    private static string Hepsi(EditTimeline c, ExportMode kip) => string.Join("\n", Adimlar(c, kip).Select(a => string.Join(" ", a.Args)));

    [Fact]
    public void HizlarUclardaTamAradaDogrusalVeYuvarlanir()
    {
        Assert.Equal(new[] { 1m, 0.81m, 0.63m, 0.44m, 0.25m }, SpeedRamp.Speeds(1m, 0.25m, 5));
        Assert.Equal(new[] { 0.25m, 0.44m, 0.63m, 0.81m, 1m }, SpeedRamp.Speeds(0.25m, 1m, 5));
        Assert.Equal(new[] { 2m, 2m }, SpeedRamp.Speeds(2m, 2m, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpeedRamp.Speeds(1m, 0.25m, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpeedRamp.Speeds(0m, 1m, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpeedRamp.Speeds(1m, 101m, 3));
    }

    [Fact]
    public void DilimlerKaynagiBosluksuzKaplarAyarlarTasinir()
    {
        var gecis = new ClipTransition(TransitionKind.Dissolve, S(0.5));
        var klip = new EditClip(S(2), S(7), 1m)
        {
            Effects = ClipEffects.None with { FadeIn = S(0.4), FadeOut = S(0.6), Rotation = 90 },
            Label = "rampa", Group = 3, Source = 1, Transition = gecis, Smooth = true
        };
        var dilimler = SpeedRamp.Pieces(klip, 1m, 0.25m);

        Assert.Equal(5, dilimler.Count);
        Assert.Equal(S(2), dilimler[0].SourceStart);
        Assert.Equal(S(7), dilimler[^1].SourceEnd);
        for (var i = 1; i < dilimler.Count; i++) Assert.Equal(dilimler[i - 1].SourceEnd, dilimler[i].SourceStart);
        Assert.All(dilimler, d => Assert.Equal(S(1), d.SourceLength));
        Assert.Equal(new[] { 1m, 0.81m, 0.63m, 0.44m, 0.25m }, dilimler.Select(d => d.Speed));
        Assert.All(dilimler, d => Assert.True(d.Smooth && d.Label == "rampa" && d.Group == 3 && d.Source == 1 && d.Effects.Rotation == 90));
        Assert.Equal(S(0.4), dilimler[0].Effects.FadeIn);
        Assert.Equal(0, dilimler[0].Effects.FadeOut);
        Assert.Equal(S(0.6), dilimler[^1].Effects.FadeOut);
        Assert.Equal(0, dilimler[^1].Effects.FadeIn);
        Assert.Equal(gecis, dilimler[0].Transition);
        Assert.All(dilimler.Skip(1), d => Assert.Null(d.Transition));
    }

    [Fact]
    public void TersKliptaOynatmaSirasiKaynaginSonundanBaslar()
    {
        var dilimler = SpeedRamp.Pieces(new EditClip(0, S(4), 1m, true), 1m, 0.5m, 4);

        Assert.All(dilimler, d => Assert.True(d.Reversed));
        Assert.Equal(S(4), dilimler[0].SourceEnd);
        Assert.Equal(0, dilimler[^1].SourceStart);
        Assert.Equal(1m, dilimler[0].Speed);
        Assert.Equal(0.5m, dilimler[^1].Speed);
        for (var i = 1; i < dilimler.Count; i++) Assert.Equal(dilimler[i].SourceEnd, dilimler[i - 1].SourceStart);
    }

    [Fact]
    public void KisaKliptaDilimSayisiAzalirCokKisadaRampaYok()
    {
        Assert.Equal(3, SpeedRamp.Pieces(new EditClip(0, S(0.3)), 1m, 0.25m).Count);
        var tek = new EditClip(0, SpeedRamp.MinSlice * 2 - 1);
        Assert.Same(tek, Assert.Single(SpeedRamp.Pieces(tek, 1m, 0.25m)));

        var c = EditTimeline.FromSource(SpeedRamp.MinSlice);
        Assert.False(c.RampSpeed(0, 1m, 0.25m));
        Assert.False(c.CanUndo);
    }

    [Fact]
    public void CizelgeRampasiTekAdimdaGeriAlinirSureDilimlerinToplami()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.Split(S(5)));
        var once = c.Clips.ToArray();

        Assert.True(c.RampSpeed(1, 1m, 0.25m));
        Assert.Equal(6, c.Clips.Count);
        Assert.Equal(once[0], c.Clips[0]);
        var beklenen = new[] { 1.0, 0.81, 0.63, 0.44, 0.25 }.Sum(h => 1 / h);
        Assert.InRange(EditTime.ToSeconds(c.Duration) - 5, beklenen - 1e-4, beklenen + 1e-4);

        Assert.True(c.Undo());
        Assert.Equal(once, c.Clips);
        Assert.True(c.Redo());
        Assert.Equal(6, c.Clips.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.RampSpeed(6, 1m, 0.5m));
    }

    [Fact]
    public void AkicilikSecilenlereYazilirGeriAlinirBolmedeKalir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.Split(S(5)));

        Assert.True(c.SetSmooth(new[] { 1 }, true));
        Assert.False(c.Clips[0].Smooth);
        Assert.True(c.Clips[1].Smooth);
        Assert.False(c.SetSmooth(new[] { 1 }, true));

        Assert.True(c.Split(S(7)));
        Assert.True(c.Clips[1].Smooth && c.Clips[2].Smooth);
        Assert.True(c.SetSpeed(2, 0.5m));
        Assert.True(c.Clips[2].Smooth);

        Assert.True(c.Undo());
        Assert.True(c.Undo());
        Assert.True(c.Undo());
        Assert.All(c.Clips, k => Assert.False(k.Smooth));
    }

    [Fact]
    public void AkiciYavasKlipFpsYerineMinterpolateYazar()
    {
        Assert.True(SpeedRamp.Interpolates(new EditClip(0, 10, 0.5m) { Smooth = true }));
        Assert.False(SpeedRamp.Interpolates(new EditClip(0, 10, 1m) { Smooth = true }));
        Assert.False(SpeedRamp.Interpolates(new EditClip(0, 10, 0.5m)));
        var yavas = new EditClip(0, S(1), 0.5m) { Smooth = true };
        Assert.Equal("tpad=stop_mode=clone:stop=2,minterpolate=fps=25:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1,trim=duration=2",
            SpeedRamp.SmoothFilter(25, yavas));
        Assert.Contains(",minterpolate=fps=30:", SpeedRamp.SmoothFilter(0, yavas));
        Assert.EndsWith(",trim=duration=4", SpeedRamp.SmoothFilter(30, new EditClip(0, S(1), 0.25m)));

        EditTimeline Cizelge(decimal hiz, bool akici)
        {
            var c = EditTimeline.FromSource(S(4));
            c.SetSpeed(0, hiz);
            if (akici) c.SetSmooth(new[] { 0 }, true);
            return c;
        }

        foreach (var kip in new[] { ExportMode.Full, ExportMode.Fast, ExportMode.Smart })
        {
            var akici = Hepsi(Cizelge(0.5m, true), kip);
            Assert.Contains("setpts=PTS/0.5,tpad=stop_mode=clone:stop=2,minterpolate=fps=30:mi_mode=mci", akici);
            Assert.DoesNotContain("setpts=PTS/0.5,fps=", akici);

            var duz = Hepsi(Cizelge(0.5m, false), kip);
            Assert.Contains("setpts=PTS/0.5,fps=30", duz);
            Assert.DoesNotContain("minterpolate", duz);

            Assert.DoesNotContain("minterpolate", Hepsi(Cizelge(2m, true), kip));
        }
    }

    [Fact]
    public void RampaHerDilimeKendiSetptsVeAtempoZinciriniYazar()
    {
        var c = EditTimeline.FromSource(S(5));
        Assert.True(c.RampSpeed(0, 1m, 0.25m));
        var cizge = Hepsi(c, ExportMode.Full);

        foreach (var hiz in new[] { "0.81", "0.63", "0.44", "0.25" })
            Assert.Contains("setpts=PTS/" + hiz + ",fps=30", cizge);
        Assert.Contains(EditExport.AtempoFilter(0.25m), cizge);
        Assert.Contains("concat=n=5", cizge);

        var hizli = Adimlar(c, ExportMode.Fast);
        Assert.Equal(4, hizli.Count(a => a.Args.Any(x => x.Contains("setpts=PTS/0.", StringComparison.Ordinal))));
    }

    [Fact]
    public void OnizlemeRampaDilimleriniAyriParcaOlarakTasir()
    {
        var c = EditTimeline.FromSource(S(5));
        Assert.True(c.RampSpeed(0, 1m, 0.25m));
        var onizleme = new EdlPreview("kaynak.mp4", c);

        Assert.Equal(5, onizleme.Parts.Count);
        Assert.Equal(new[] { 1m, 0.81m, 0.63m, 0.44m, 0.25m }, onizleme.Parts.Select(p => p.Clip.Speed));
        Assert.Equal(S(5), onizleme.EdlDuration);
        Assert.Equal(c.Duration, onizleme.TimelineDuration);
    }

    [Fact]
    public void ProjeAkiciligiYalnizAcikkenYazarVeGeriOkur()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.Split(S(5)));
        Assert.True(c.SetSmooth(new[] { 1 }, true));
        var json = EditProject.From(new SourceStamp("a.mp4", 1, 1), c).ToJson();

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"smooth\": true"));
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(c.Clips, okunan!.Clips);
        Assert.True(okunan.Clips[1].Smooth);
    }

    private static string Klasor(string ad)
    {
        var yol = Path.Combine(TestPaths.OutputRoot, "hiz-rampasi", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static string Kos(string dosya, params string[] args)
    {
        var psi = new ProcessStartInfo(dosya)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var standart = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(120_000))
        {
            surec.Kill(true);
            Assert.Fail(Path.GetFileName(dosya) + " 120 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return standart.Result;
    }

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
        => plan with
        {
            Steps = plan.Steps.Select(adim =>
            {
                var args = adim.Args.ToList();
                args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
                return adim with { Args = args };
            }).ToArray()
        };

    private static (double Video, double Ses, int Kare) Olc(string video)
    {
        string Al(string akis, string alan) => Kos(ToolLocator.Ffprobe, "-v", "error", "-select_streams", akis, "-count_frames",
            "-show_entries", "stream=" + alan, "-of", "default=noprint_wrappers=1:nokey=1", video).Trim();
        return (double.Parse(Al("v:0", "duration"), CultureInfo.InvariantCulture),
            double.Parse(Al("a:0", "duration"), CultureInfo.InvariantCulture),
            int.Parse(Al("v:0", "nb_read_frames"), CultureInfo.InvariantCulture));
    }

    private static int AyriKare(string video)
        => Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-i", video, "-map", "0:v:0", "-vf", "mpdecimate", "-fps_mode", "passthrough", "-threads", "2", "-f", "framemd5", "-")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !s.StartsWith('#'))
            .Count();

    private static string Kaynak(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=s=160x120:r=30:d=2",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);
        return kaynak;
    }

    [FfmpegFact]
    public async Task CanliRampaCiktisininSuresiCizelgeSuresineEsit()
    {
        var klasor = Klasor("rampa");
        var kaynak = Kaynak(klasor);
        try
        {
            foreach (var (kip, akici) in new[] { (ExportMode.Full, false), (ExportMode.Fast, false), (ExportMode.Full, true) })
            {
                var c = EditTimeline.FromSource(S(2));
                if (akici) Assert.True(c.SetSmooth(new[] { 0 }, true));
                Assert.True(c.RampSpeed(0, 1m, 0.25m, 4));
                var beklenen = EditTime.ToSeconds(c.Duration);
                var cikti = Path.Combine(klasor, kip + (akici ? "-akici" : string.Empty) + ".mp4");
                var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, kip, cikti, 8 * Gb);
                await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);

                var (video, ses, _) = Olc(cikti);
                Assert.InRange(video, beklenen - 0.1, beklenen + 0.1);
                Assert.InRange(ses, beklenen - 0.1, beklenen + 0.1);
            }
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task CanliAkiciAgirCekimAraKareUretirSureIkiKat()
    {
        var klasor = Klasor("akici");
        var kaynak = Kaynak(klasor);
        try
        {
            async Task<string> Aktar(string ad, bool akici)
            {
                var c = EditTimeline.FromSource(S(2));
                Assert.True(c.Split(S(1)));
                c.Delete(1);
                Assert.True(c.SetSpeed(0, 0.5m));
                if (akici) Assert.True(c.SetSmooth(new[] { 0 }, true));
                var cikti = Path.Combine(klasor, ad);
                var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Full, cikti, 8 * Gb);
                Assert.Equal(akici, plan.Steps[0].Args.Any(a => a.Contains("minterpolate", StringComparison.Ordinal)));
                await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
                return cikti;
            }

            var duz = await Aktar("duz.mp4", false);
            var akici = await Aktar("akici.mp4", true);

            foreach (var video in new[] { duz, akici })
            {
                var (v, s, kare) = Olc(video);
                var olcu = $"{Path.GetFileName(video)}: goruntu {v}, ses {s}, kare {kare}";
                Assert.True(v is >= 1.9 and <= 2.1, olcu);
                Assert.True(s is >= 1.9 and <= 2.1, olcu);
                Assert.True(kare is >= 58 and <= 62, olcu);
            }

            var duzAyri = AyriKare(duz);
            var akiciAyri = AyriKare(akici);
            Assert.True(akiciAyri > duzAyri * 3 / 2, $"duz {duzAyri}, akici {akiciAyri}");
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
