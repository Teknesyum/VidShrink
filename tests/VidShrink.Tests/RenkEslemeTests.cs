using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using VidShrink.App.Editing;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class RenkEslemeTests(ITestOutputHelper cikis)
{
    private const string Dal = "worktree-agent-later-88-89";

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    [Fact]
    public void ShowinfoKareleriniKanalKanalBirlestirir()
    {
        var log = "[Parsed_showinfo_3 @ 0x1] n:   0 pts:0 mean:[100 50 200] stdev:[10.0 5.0 20.0]\n"
            + "[Parsed_showinfo_3 @ 0x1] n:   1 pts:1 mean:[110 70 180] stdev:[10.0 5.0 20.0]\n"
            + "baska satir mean yok\n";
        var s = ClipMatch.Parse(log);
        Assert.NotNull(s);
        Assert.Equal(2, s!.Frames);
        Assert.Equal(105, s.MeanG, 6);
        Assert.Equal(60, s.MeanB, 6);
        Assert.Equal(190, s.MeanR, 6);
        Assert.Equal(Math.Sqrt(100 + 25), s.StdG, 6);
        Assert.Equal(Math.Sqrt(25 + 100), s.StdB, 6);
        Assert.Equal(Math.Sqrt(400 + 100), s.StdR, 6);
        Assert.Null(ClipMatch.Parse("hic kare yok"));
    }

    private static double Uygula(double girdi, (double InMin, double InMax, double OutMin, double OutMax) l)
    {
        var p = l.InMax > l.InMin ? Math.Clamp((girdi - l.InMin) / (l.InMax - l.InMin), 0, 1) : girdi >= l.InMax ? 1 : 0;
        return l.OutMin + p * (l.OutMax - l.OutMin);
    }

    [Fact]
    public void SeviyeKarsiligiDogrusalEslemeyiKirparakVerir()
    {
        foreach (var (kazanc, kayma) in new[] { (1.0, 0.0), (1.5, -0.2), (0.6, 0.3), (2.5, -0.9), (0.3, 0.8), (1.2, 0.1) })
        {
            var l = ClipMatch.Levels(kazanc, kayma);
            for (var i = 0; i <= 50; i++)
            {
                var x = i / 50.0;
                var beklenen = Math.Clamp(kazanc * x + kayma, 0, 1);
                Assert.True(Math.Abs(Uygula(x, l) - beklenen) < 1e-3, $"a={kazanc} b={kayma} x={x}: {Uygula(x, l)} / {beklenen}");
            }
        }
    }

    [Fact]
    public void EslemeHedefinIstatistiginiReferansaTasir()
    {
        var referans = new ColorStats(120, 100, 80, 40, 30, 20, 8);
        var hedef = new ColorStats(60, 140, 90, 20, 60, 10, 8);
        var e = ClipMatch.Match(ClipEffects.None with { Brightness = 0.1 }, referans, hedef);
        Assert.True(e.HasMatch);
        Assert.True(e.HasColor);
        Assert.Equal(0.1, e.Brightness);
        var kazanc = new[] { e.MatchGainR, e.MatchGainG, e.MatchGainB };
        var kayma = new[] { e.MatchLiftR, e.MatchLiftG, e.MatchLiftB };
        for (var c = 0; c < 3; c++)
        {
            Assert.True(Math.Abs(kazanc[c] * hedef.Mean(c) + kayma[c] * 255 - referans.Mean(c)) < 0.1, "ortalama " + c);
            Assert.True(Math.Abs(kazanc[c] * hedef.Std(c) - referans.Std(c)) < 0.01, "sapma " + c);
        }

        var duz = ClipMatch.Match(ClipEffects.None, referans, new ColorStats(10, 10, 10, 0, 0, 0, 1));
        Assert.Equal(1, duz.MatchGainR);
        Assert.Equal((120 - 10) / 255.0, duz.MatchLiftR, 3);
        Assert.Equal(ClipEffects.MaxMatchGain, ClipMatch.Match(ClipEffects.None, new ColorStats(0, 0, 0, 200, 200, 200, 1), new ColorStats(0, 0, 0, 1, 1, 1, 1)).MatchGainR);
        Assert.False(e.WithoutMatch().HasMatch);
        Assert.Equal(e with { MatchGainR = 1, MatchGainG = 1, MatchGainB = 1, MatchLiftR = 0, MatchLiftG = 0, MatchLiftB = 0 }, e.WithoutMatch());
    }

    [Fact]
    public void EslemeRenkZincirininBasindaDurur()
    {
        var e = ClipMatch.Match(ClipEffects.None with { Contrast = 1.2 }, new ColorStats(120, 100, 80, 40, 30, 20, 1), new ColorStats(60, 140, 90, 20, 60, 10, 1));
        var parca = ClipFilters.ColorParts(e);
        Assert.StartsWith("colorlevels=rimin=", parca[0]);
        Assert.StartsWith("eq=contrast=", parca[1]);
        Assert.Empty(ClipMatch.Filter(ClipEffects.None));
        Assert.DoesNotContain("colorlevels", ClipFilters.Color(ClipEffects.None with { Contrast = 1.2 }));
        var bozuk = (ClipEffects.None with { MatchGainR = double.NaN, MatchGainG = 99, MatchLiftB = -5 }).Normalized();
        Assert.Equal((1.0, ClipEffects.MaxMatchGain, -1.0), (bozuk.MatchGainR, bozuk.MatchGainG, bozuk.MatchLiftB));
    }

    [Fact]
    public void ProjeDosyasiEslemeyiTasirEskiDosyaEslemesizAcilir()
    {
        var model = EditTimeline.FromSource(EditTime.FromSeconds(10));
        var esli = ClipMatch.Match(ClipEffects.None, new ColorStats(120, 100, 80, 40, 30, 20, 1), new ColorStats(60, 140, 90, 20, 60, 10, 1));
        model.SetEffects(new[] { 0 }, _ => esli);
        var json = EditProject.From(new SourceStamp(@"C:\esleme.mp4", 16, 1), model).ToJson();
        Assert.Contains("\"match\"", json);
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);

        var eski = Regex.Replace(json, ",\\s*\"match\":\\s*\\{[^}]*\\}", string.Empty);
        Assert.DoesNotContain("\"match\"", eski);
        Assert.False(EditProject.Parse(eski)!.Clips[0].Effects.HasMatch);
    }

    [Fact]
    public void BulaniklikKutusuSurukmeyiKareyeKirpar()
    {
        var kare = new Rect(20, 10, 160, 90);
        Assert.Equal(new Rect(40, 30, 60, 20), EditorView.BlurDragRect(new Point(100, 50), new Point(40, 30), kare));
        Assert.Equal(new Rect(20, 10, 80, 40), EditorView.BlurDragRect(new Point(-50, -50), new Point(100, 50), kare));
        Assert.Equal(new Rect(100, 50, 80, 50), EditorView.BlurDragRect(new Point(100, 50), new Point(999, 999), kare));
        Assert.Equal(0, EditorView.BlurDragRect(new Point(60, 40), new Point(60, 40), kare).Width);
    }

    private static void Kos(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
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
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        _ = standart.Result;
    }

    private static string Klip(string klasor, string ad, string renk)
    {
        var yol = Path.Combine(klasor, ad);
        Kos("-hide_banner", "-nostdin", "-v", "error", "-y", "-f", "lavfi",
            "-i", "testsrc2=s=160x120:r=10:d=2," + renk,
            "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0", "-pix_fmt", "yuv444p", "-threads", "2", yol);
        return yol;
    }

    private static (double Ortalama, double Sapma) Fark(ColorStats a, ColorStats b)
        => (Enumerable.Range(0, 3).Max(c => Math.Abs(a.Mean(c) - b.Mean(c))), Enumerable.Range(0, 3).Max(c => Math.Abs(a.Std(c) - b.Std(c))));

    [FfmpegFact]
    public async Task EslemeIkiKlibinOrtalamaVeSapmaFarkiniKucultur()
    {
        var klasor = Path.Combine(Kanit, "renk-esleme");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "referans.mp4", "hedef.mp4", "olcum.txt" };
        KanitKapanisi.Onceki(klasor, adlar);
        var referansYolu = Klip(klasor, "referans.mp4", "eq=contrast=0.6:brightness=0.08,colorbalance=rm=0.25:bm=-0.2");
        var hedefYolu = Klip(klasor, "hedef.mp4", "eq=contrast=1.3:brightness=-0.1,colorbalance=bm=0.3:gm=0.1");

        var referans = await ColorProbe.RunAsync(referansYolu, 0, 2, string.Empty);
        var hedef = await ColorProbe.RunAsync(hedefYolu, 0, 2, string.Empty);
        Assert.NotNull(referans);
        Assert.NotNull(hedef);
        Assert.Equal(ClipMatch.SampleFrames, referans!.Frames);
        var esli = ClipMatch.Match(ClipEffects.None, referans, hedef!);
        var sonra = await ColorProbe.RunAsync(hedefYolu, 0, 2, ClipFilters.Color(esli));
        Assert.NotNull(sonra);

        var once = Fark(referans, hedef!);
        var simdi = Fark(referans, sonra!);
        string Satir(string ad, ColorStats s) => string.Format(CultureInfo.InvariantCulture,
            "{0}: ortalama R {1:0.00} G {2:0.00} B {3:0.00} | sapma R {4:0.00} G {5:0.00} B {6:0.00}", ad, s.MeanR, s.MeanG, s.MeanB, s.StdR, s.StdG, s.StdB);
        var satirlar = new[]
        {
            Satir("referans", referans), Satir("hedef", hedef!), Satir("eslenmis", sonra!),
            string.Format(CultureInfo.InvariantCulture, "en buyuk ortalama farki: {0:0.00} -> {1:0.00}", once.Ortalama, simdi.Ortalama),
            string.Format(CultureInfo.InvariantCulture, "en buyuk sapma farki: {0:0.00} -> {1:0.00}", once.Sapma, simdi.Sapma),
            "suzgec: " + ClipFilters.Color(esli),
        };
        File.WriteAllLines(Path.Combine(klasor, "olcum.txt"), satirlar);
        foreach (var s in satirlar) cikis.WriteLine(s);

        Assert.True(once.Ortalama > 15 && once.Sapma > 10, $"kaynaklar yeterince farkli degil: {once}");
        Assert.True(simdi.Ortalama < once.Ortalama / 4, $"ortalama farki kuculmedi: {once.Ortalama:0.00} -> {simdi.Ortalama:0.00}");
        Assert.True(simdi.Sapma < once.Sapma / 3, $"sapma farki kuculmedi: {once.Sapma:0.00} -> {simdi.Sapma:0.00}");

        KanitKapanisi.Kapat(klasor, adlar);
    }
}
