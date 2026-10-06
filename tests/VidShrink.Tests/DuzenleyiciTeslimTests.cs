using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// D4 teslimi: <see cref="EditExport"/> uc kipte ffmpeg adimlarini kurar, atempo zinciri her
/// halkayi [0,5; 2] icinde tutar, ters klip bellek siniri 1080p30 olcumune oturur. Canli kol
/// 6 sn'lik sentetik kaynagi uc kipte disa aktarir ve ciktinin suresini ffprobe'la okur; iptal
/// kolu yarim dosya ve calisma klasoru birakmadigini olcer. Kanit <c>.calisma/duzenleyici-teslim/</c>.
/// </summary>
public sealed class DuzenleyiciTeslimTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "duzenleyici-teslim");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(string codec = "h264", string? ses = "aac", int w = 1920, int h = 1080, double fps = 30, int bit = 8) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = w,
        Height = h,
        Fps = fps,
        VideoCodec = codec,
        TotalBitrateBps = 1,
        AudioCodec = ses,
        PixelFormat = bit > 8 ? "yuv420p10le" : "yuv420p",
        BitDepth = bit
    };

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static double Sure(ExportPlan plan) => plan.Steps[^1].DurationSeconds;

    [Theory]
    [InlineData("0.01")]
    [InlineData("0.25")]
    [InlineData("0.5")]
    [InlineData("0.75")]
    [InlineData("1.5")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("10")]
    [InlineData("100")]
    public void AtempoZinciriHerHalkayiAraliktaTutarCarpimHiziVerir(string metin)
    {
        var hiz = decimal.Parse(metin, CultureInfo.InvariantCulture);
        var zincir = EditExport.AtempoChain(hiz);

        Assert.NotEmpty(zincir);
        Assert.All(zincir, f => Assert.InRange(f, 0.5, 2.0));
        Assert.Equal((double)hiz, zincir.Aggregate(1.0, (a, f) => a * f), 6);
    }

    [Fact]
    public void AtempoZinciriBilinenDegerlerdeKisaKalir()
    {
        Assert.Empty(EditExport.AtempoChain(1m));
        Assert.Equal("atempo=2", EditExport.AtempoFilter(2m));
        Assert.Equal("atempo=0.5,atempo=0.5", EditExport.AtempoFilter(0.25m));
        Assert.Equal("atempo=1.732051,atempo=1.732051", EditExport.AtempoFilter(3m));
        Assert.Equal(7, EditExport.AtempoChain(0.01m).Count);
    }

    [Fact]
    public void TamKipTekFiltreGrafigiKurar()
    {
        var model = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(3), S(5), 2m, true) });
        var plan = EditExport.Build(model, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);

        var adim = Assert.Single(plan.Steps);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.False(plan.FellBackToFull);
        Assert.Equal(
            "[0:v]trim=start=0:end=2,setpts=PTS-STARTPTS[v0];[0:a]atrim=start=0:end=2,asetpts=PTS-STARTPTS[a0];"
            + "[0:v]trim=start=3:end=5,setpts=PTS-STARTPTS,reverse,setpts=PTS/2,fps=30[v1];"
            + "[0:a]atrim=start=3:end=5,asetpts=PTS-STARTPTS,areverse,atempo=2[a1];"
            + "[v0][a0][v1][a1]concat=n=2:v=1:a=1[vout][aout]",
            Deger(adim.Args, "-filter_complex"));
        Assert.Equal("libx264", Deger(adim.Args, "-c:v"));
        Assert.Equal("aac", Deger(adim.Args, "-c:a"));
        Assert.Equal("+faststart", Deger(adim.Args, "-movflags"));
        Assert.Equal("cikti.mp4", adim.Args[^1]);
        Assert.Equal(3, Sure(plan), 6);
        Assert.Equal(new[] { 1 }, plan.MotionClips);

        var sessiz = EditExport.Build(model, Bilgi(ses: null), Array.Empty<double>(), 0, ExportMode.Full, "cikti.mkv", "is", 8 * Gb);
        var graf = Deger(sessiz.Steps[0].Args, "-filter_complex");
        Assert.DoesNotContain("atrim", graf);
        Assert.EndsWith("[v0][v1]concat=n=2:v=1:a=0[vout]", graf);
        Assert.DoesNotContain("-movflags", sessiz.Steps[0].Args);
        Assert.DoesNotContain("[aout]", sessiz.Steps[0].Args);
    }

    [Fact]
    public void HizliKipAnahtarKareyeYaslanirTekConcatAdimiKurar()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(6), S(8)) });
        var plan = EditExport.Build(model, Bilgi(), new[] { 0.0, 2.5, 5.0, 7.5 }, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);

        var adim = Assert.Single(plan.Steps);
        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal("concat", Deger(adim.Args, "-f"));
        Assert.Equal("copy", Deger(adim.Args, "-c"));
        Assert.Equal(Path.Combine("is", "list.ffconcat"), adim.ListPath);
        Assert.Equal(
            "ffconcat version 1.0\nfile 'kaynak.mp4'\noutpoint 3\nfile 'kaynak.mp4'\ninpoint 5\noutpoint 8\n",
            adim.ListContent);
        Assert.Equal(3 + 3, Sure(plan), 6);
        Assert.DoesNotContain("-c:v", adim.Args);
    }

    [Fact]
    public void AkilliKipKenarGopuKodlarGovdeyiKopyalar()
    {
        var model = new EditTimeline(new[]
        {
            new EditClip(S(1), S(7)),
            new EditClip(S(2.5), S(3.5)),
            new EditClip(S(8), S(10), 0.5m)
        });
        var kareler = new[] { 0.0, 2, 4, 6, 8 };
        var plan = EditExport.Build(model, Bilgi(), kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb, DuzenleyiciAkilliKodekTests.Sinirlar(kareler));

        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.Equal(9, plan.Steps.Count);

        var bas = plan.Steps[0].Args;
        Assert.Equal("1", Deger(bas, "-ss"));
        Assert.Equal("1", Deger(bas, "-t"));
        Assert.Equal("libx264", Deger(bas, "-c:v"));
        Assert.Equal("mpegts", Deger(bas, "-f"));
        Assert.Contains("-an", bas);

        var govde = plan.Steps[1].Args;
        Assert.Equal("2.001", Deger(govde, "-ss"));
        Assert.Equal("4", Deger(govde, "-t"));
        Assert.Equal("copy", Deger(govde, "-c"));
        Assert.Equal("120", Deger(govde, "-frames:v"));

        var kuyruk = plan.Steps[2].Args;
        Assert.Equal("6", Deger(kuyruk, "-ss"));
        Assert.Equal("1", Deger(kuyruk, "-t"));

        var ses = plan.Steps[3].Args;
        Assert.Equal("1", Deger(ses, "-ss"));
        Assert.Equal("6", Deger(ses, "-t"));
        Assert.Equal("copy", Deger(ses, "-c"));
        Assert.Contains("-vn", ses);

        Assert.Equal("1", Deger(plan.Steps[4].Args, "-t"));
        Assert.Equal("libx264", Deger(plan.Steps[4].Args, "-c:v"));
        Assert.Equal("setpts=PTS-STARTPTS,setpts=PTS/0.5,fps=30", Deger(plan.Steps[6].Args, "-vf"));
        Assert.Equal("asetpts=PTS-STARTPTS,atempo=0.5", Deger(plan.Steps[7].Args, "-af"));

        var son = plan.Steps[8];
        Assert.Equal("concat", Deger(son.Args, "-f"));
        Assert.Equal(5, son.ListContent!.Split('\n').Count(s => s.StartsWith("file ", StringComparison.Ordinal)));
        Assert.Equal(3, son.AudioListContent!.Split('\n').Count(s => s.StartsWith("file ", StringComparison.Ordinal)));
        Assert.Equal(new[] { "duration 1", "duration 4", "duration 1", "duration 1", "duration 4" },
            son.ListContent!.Split('\n').Where(s => s.StartsWith("duration ", StringComparison.Ordinal)));
        Assert.Equal(new[] { 2 }, plan.MotionClips);
        Assert.Equal(6 + 1 + 4, Sure(plan), 6);
    }

    [Fact]
    public void DesteklenmeyenKodekTamKipeDuserHizliKipYalnizKopyaIleSurer()
    {
        var duz = new EditTimeline(new[] { new EditClip(S(0), S(4)) });
        var hizli = new EditTimeline(new[] { new EditClip(S(0), S(4), 2m) });
        var kareler = new[] { 0.0, 2 };

        var akilli = EditExport.Build(duz, Bilgi("vp9", "opus"), kareler, 0, ExportMode.Smart, "c.mkv", "is", 8 * Gb);
        Assert.Equal(ExportMode.Full, akilli.Effective);
        Assert.True(akilli.FellBackToFull);

        Assert.Equal(ExportMode.Fast, EditExport.Build(duz, Bilgi("vp9", "opus"), kareler, 0, ExportMode.Fast, "c.mkv", "is", 8 * Gb).Effective);
        Assert.Equal(ExportMode.Full, EditExport.Build(hizli, Bilgi("vp9", "opus"), kareler, 0, ExportMode.Fast, "c.mkv", "is", 8 * Gb).Effective);
        Assert.Equal(ExportMode.Full, EditExport.Build(duz, Bilgi(), Array.Empty<double>(), 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb).Effective);

        var hizliKip = EditExport.Build(hizli, Bilgi(), kareler, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Fast, hizliKip.Effective);
        Assert.Equal("libx264", Deger(hizliKip.Steps[0].Args, "-c:v"));
        Assert.Equal(2, Sure(hizliKip), 6);
    }

    [Fact]
    public void TersKlipSiniriOlcumeOturur()
    {
        var hd = Bilgi();
        var mbSn = EditExport.ReverseBytesPerSecond(hd) / 1e6;
        Assert.InRange(mbSn, EditExport.MeasuredReverseMbPerSecond1080p30 * 0.95, EditExport.MeasuredReverseMbPerSecond1080p30 * 1.05);
        Assert.Equal(2 * EditExport.ReverseBytesPerSecond(hd), EditExport.ReverseBytesPerSecond(Bilgi(bit: 10)), 3);

        var sinir = EditExport.ReverseLimitSeconds(hd, Gb);
        Assert.InRange(sinir, 9, 11);

        var model = new EditTimeline(new[]
        {
            new EditClip(S(0), S(20), 1m, true),
            new EditClip(S(20), S(40)),
            new EditClip(S(40), S(45), 1m, true)
        });
        Assert.Equal(new[] { 0 }, EditExport.ReverseOverLimit(model, hd, Gb));
        Assert.Empty(EditExport.ReverseOverLimit(model, hd, 4 * Gb));

        var plan = EditExport.Build(model, hd, Array.Empty<double>(), 0, ExportMode.Full, "c.mp4", "is", Gb);
        Assert.Equal(new[] { 0 }, plan.ReverseOverLimit);
        Assert.Equal(sinir, plan.ReverseLimitSeconds, 6);
    }

    [Fact]
    public void AnahtarKareDokumuAyrisirBaslangicDusulur()
    {
        var csv = "1.5,K__\r\n1.533333,___\r\nN/A,K__\r\n3.5,K_\r\n2.5,K__\r\n1.500000\r\n";
        var (kareler, baslangic) = EditExport.ParseKeyframes(csv);

        Assert.Equal(1.5, baslangic, 6);
        Assert.Equal(new[] { 0.0, 1.0, 2.0 }, kareler);
        Assert.Contains("packet=pts_time,flags:format=start_time", EditExport.KeyframeProbeArgs("a.mp4"));
    }

    [Fact]
    public void DuzenleyiciAkilliKiplaAcilirKaynaksizDisaAktarmaKapali()
    {
        var (kip, acik, cubuk) = AppHost.Run(() =>
        {
            var view = new EditorView();
            view.ShowTimeline(EditTimeline.FromSource(S(10)), 30);
            return (view.SelectedExportMode, view.BtnExport.IsEnabled,
                view.ExportBar.IsVisible);
        });

        Assert.Equal(ExportMode.Smart, kip);
        Assert.False(acik);
        Assert.False(cubuk);
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

    private static double Sure(string dosya)
    {
        var psi = new ProcessStartInfo("ffprobe", $"-v error -show_entries format=duration -of csv=p=0 \"{dosya}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return double.Parse(metin.Trim(), CultureInfo.InvariantCulture);
    }

    private static string Kaynak()
    {
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=6",
            "-f", "lavfi", "-i", "sine=f=440:d=6",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-threads", "1",
            "-c:a", "aac", "-shortest", kaynak);
        return kaynak;
    }

    private static EditTimeline UcKlip() => new(new[]
    {
        new EditClip(S(0.5), S(2.5)),
        new EditClip(S(3), S(4), 2m, true),
        new EditClip(S(4.2), S(5.7))
    });

    [Fact]
    public async Task CanliDisaAktarmaUcKipteBeklenenSureyiVerir()
    {
        Onceki("kaynak.mp4", "hizli.mp4", "akilli.mp4", "tam.mp4", "olcu.txt");
        var kaynak = Kaynak();
        var satirlar = new List<string>();
        var olculen = new Dictionary<ExportMode, (double Beklenen, double Sure, ExportMode Etkin)>();

        foreach (var (kip, ad) in new[] { (ExportMode.Fast, "hizli.mp4"), (ExportMode.Smart, "akilli.mp4"), (ExportMode.Full, "tam.mp4") })
        {
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(kaynak, UcKlip(), kip, cikti, 8 * Gb);
            var son = 0.0;
            await EditExportRunner.RunAsync(plan, new AnindaIlerleme(p => son = p.Fraction));
            var sure = Sure(cikti);
            olculen[kip] = (Sure(plan), sure, plan.Effective);
            satirlar.Add($"{kip} etkin={plan.Effective} adim={plan.Steps.Count} beklenen={Sure(plan).ToString(CultureInfo.InvariantCulture)} olculen={sure.ToString(CultureInfo.InvariantCulture)} ilerleme={son.ToString(CultureInfo.InvariantCulture)}");
            Assert.Equal(1.0, son, 6);
            Assert.False(Directory.Exists(plan.WorkDirectory));
        }

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);

        Assert.All(olculen.Values, o => Assert.InRange(o.Sure, o.Beklenen - 0.15, o.Beklenen + 0.15));
        Assert.Equal(4.0, olculen[ExportMode.Smart].Beklenen, 6);
        Assert.Equal(4.0, olculen[ExportMode.Full].Beklenen, 6);
        Assert.Equal(2.5 + 0.5 + 1.7, olculen[ExportMode.Fast].Beklenen, 6);
        Assert.Equal(ExportMode.Smart, olculen[ExportMode.Smart].Etkin);
        Assert.Equal(ExportMode.Fast, olculen[ExportMode.Fast].Etkin);
        Assert.Empty(Directory.GetFiles(Kanit, "vidshrink_partial_*"));
        Assert.Empty(Directory.GetDirectories(Kanit, "vidshrink_partial_*"));

        Kapat("kaynak.mp4", "hizli.mp4", "akilli.mp4", "tam.mp4", "olcu.txt");
    }

    [Fact]
    public async Task IptalYarimDosyaBirakmaz()
    {
        Onceki("iptal-kaynak.mp4", "iptal.mp4");
        var kaynak = Path.Combine(Kanit, "iptal-kaynak.mp4");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=6",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "1", kaynak);
        var cikti = Path.Combine(Kanit, "iptal.mp4");
        var plan = await EditExportRunner.PrepareAsync(kaynak, UcKlip(), ExportMode.Full, cikti, 8 * Gb);

        using var cts = new CancellationTokenSource();
        var gorulen = 0.0;
        var ilerleme = new AnindaIlerleme(p => { if (p.Fraction > 0 && gorulen == 0) { gorulen = p.Fraction; cts.Cancel(); } });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EditExportRunner.RunAsync(plan, ilerleme, cts.Token));

        Assert.InRange(gorulen, double.Epsilon, 0.999);

        Assert.False(File.Exists(cikti));
        Assert.False(Directory.Exists(plan.WorkDirectory));
        Assert.Empty(Directory.GetFiles(Kanit, "vidshrink_partial_*"));

        Kapat("iptal-kaynak.mp4");
    }

    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static void Onceki(params string[] adlar) => KanitKapanisi.Onceki(Kanit, adlar);

    private sealed class AnindaIlerleme : IProgress<EncodeProgress>
    {
        private readonly Action<EncodeProgress> _al;

        public AnindaIlerleme(Action<EncodeProgress> al) => _al = al;

        public void Report(EncodeProgress value) => _al(value);
    }
}
