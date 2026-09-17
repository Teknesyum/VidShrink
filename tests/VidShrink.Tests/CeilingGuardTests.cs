using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class CeilingGuardTests
{
    private static readonly SizeSample[] RampaIzi =
    {
        new(1174, 1.497, true),
        new(1102, 1.707, true),
        new(908, 1.522, true)
    };

    private static EncodePlan Son(string codec, int k = 908) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = k,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        Preset = codec == "libsvtav1" ? "6" : "slow"
    };

    private static double Mb(int k, double verim) => k * 10 / 8388.608 / 0.995 * verim;

    [Fact]
    public void RampaIzindeSvtIstegiEnKotuVerimleTavaninDokuzdaDokuzunaNisanAlir()
    {
        var plan = CeilingGuard.Plan(Son("libsvtav1"), RampaIzi, 1.4648, 10);

        Assert.NotNull(plan);
        Assert.Equal(786, plan!.VideoBitrateK);
        Assert.Equal("2pass", plan.Mode);
        Assert.False(plan.PeakEqualsRate);
        Assert.True(plan.VideoBitrateK < 908 * CeilingGuard.BelowTriedRequest);
        foreach (var s in RampaIzi)
        {
            var verim = s.ActualMb / Mb(s.VideoBitrateK, 1.0);
            Assert.True(Mb(plan.VideoBitrateK, verim) <= 1.4648, $"verim {verim:0.###} ile {Mb(plan.VideoBitrateK, verim):0.####} MB");
        }
    }

    [Fact]
    public void X264BekcisiTepeyiOrtalamayaEsitlerVeBirSaniyelikTamponuPayaKatar()
    {
        var plan = CeilingGuard.Plan(Son("libx264"), RampaIzi, 1.4648, 10);

        Assert.NotNull(plan);
        Assert.Equal(714, plan!.VideoBitrateK);
        Assert.True(plan.PeakEqualsRate);
    }

    [Fact]
    public void VerimBirinAltindaysaBirSayilirVeBekciButceyiAsmaz()
    {
        var izler = new[] { new SizeSample(1000, Mb(1000, 0.98), true), new SizeSample(990, Mb(990, 0.99), true) };

        var plan = CeilingGuard.Plan(Son("libx264", 990), izler, 1.0, 10);

        Assert.NotNull(plan);
        Assert.Equal(682, plan!.VideoBitrateK);
        Assert.Equal(1.0, CeilingGuard.WorstYield(Son("libx264", 990), izler, 10));
    }

    [Fact]
    public void TavanAltiOrnekEnKotuVerimeKatilmaz()
    {
        var izler = RampaIzi.Append(new SizeSample(500, Mb(500, 1.9), false)).ToArray();

        var plan = CeilingGuard.Plan(Son("libsvtav1", 500), izler, 1.4648, 10);

        Assert.NotNull(plan);
        Assert.Equal((int)Math.Floor(500 * CeilingGuard.BelowTriedRequest), plan!.VideoBitrateK);
        Assert.Equal(1.39908, CeilingGuard.WorstYield(Son("libsvtav1"), izler, 10), 4);
    }

    [Fact]
    public void TekTavanUstuOrnekteBekciKurulmaz()
        => Assert.Null(CeilingGuard.Plan(Son("libsvtav1"), RampaIzi.Take(1).ToArray(), 1.4648, 10));

    [Fact]
    public void IstekKosulabilirTabaninAltinaDuserseBekciKurulmaz()
        => Assert.Null(CeilingGuard.Plan(Son("libsvtav1"), RampaIzi, 0.002, 10));

    [Fact]
    public void BayrakX264ArgumanindaTepeVeTamponuIstegeEsitler()
    {
        var info = new MediaInfo { FilePath = "kaynak.mp4", DurationSeconds = 10, Width = 1920, Height = 1080, Fps = 24, VideoCodec = "h264", FileSizeBytes = 10_000_000, TotalBitrateBps = 8_000_000 };
        var bekci = CeilingGuard.Plan(Son("libx264"), RampaIzi, 1.4648, 10)!;
        var bayraksiz = bekci.Clone();
        bayraksiz.PeakEqualsRate = false;

        var ile = FfmpegArguments.Build(info, bekci, "o.mp4", 2, "gunluk");
        var ilesiz = FfmpegArguments.Build(info, bayraksiz, "o.mp4", 2, "gunluk");

        Assert.Equal("714k", ile[ile.ToList().IndexOf("-maxrate") + 1]);
        Assert.Equal("714k", ile[ile.ToList().IndexOf("-bufsize") + 1]);
        Assert.NotEqual("714k", ilesiz[ilesiz.ToList().IndexOf("-maxrate") + 1]);
    }

    [Fact]
    public void SvtPlanindaBayrakTepeArgumaniYazdirmaz()
    {
        var info = new MediaInfo { FilePath = "kaynak.mp4", DurationSeconds = 10, Width = 1920, Height = 1080, Fps = 24, VideoCodec = "h264", FileSizeBytes = 10_000_000, TotalBitrateBps = 8_000_000 };
        var plan = Son("libsvtav1", 786);
        plan.PeakEqualsRate = true;

        var args = FfmpegArguments.Build(info, plan, "o.mp4", 2, "gunluk");

        Assert.DoesNotContain("-maxrate", args);
        Assert.DoesNotContain("-bufsize", args);
    }

    [Fact]
    public void DuzeltmeBekcininBayraginiTasimaz()
    {
        var bekci = CeilingGuard.Plan(Son("libx264"), RampaIzi, 1.4648, 10)!;

        var sonraki = PlanCalculator.Correct(bekci, 1.6, 1.4648, 10);

        Assert.False(sonraki.PeakEqualsRate);
    }

    [Fact]
    public async Task SormayanYoldaImkansizHedefteBekciKosarVeEnKucukSonucTeslimEdilir()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor);
        var cikti = Path.Combine(klasor, "bekci.mp4");

        var sonuc = await new EncodeRunner().RunAsync(Bilgi(kaynak), KucukPlan(), cikti, targetMb: 0.004, progress: null,
            fillPolicy: FillPolicy.FillTarget);

        var iz = string.Join(" | ", sonuc.Trace!.Select(a => $"{a.Number}:{a.Branch}:{a.VideoBitrateK}k:{a.ActualMb:0.#####}"));
        Assert.Contains(sonuc.Trace!, a => a.Branch.StartsWith("ceiling guard", StringComparison.Ordinal));
        Assert.True(sonuc.Success, iz);
        Assert.True(sonuc.OverTarget, iz);
        Assert.True(sonuc.CeilingExceeded, iz);
        Assert.Equal(3, sonuc.Attempts);
        Assert.True(File.Exists(cikti), iz);
        var denenen = sonuc.Trace!.Where(a => a.Branch == "over ceiling").Select(a => a.ActualMb).ToList();
        Assert.Equal(3, denenen.Count);
        var bekci = sonuc.Trace!.Single(a => a.Branch.StartsWith("ceiling guard", StringComparison.Ordinal));
        Assert.Equal(bekci.VideoBitrateK, sonuc.Trace!.Last(a => a.Branch == "over ceiling").VideoBitrateK);
        Assert.True(bekci.VideoBitrateK < sonuc.Trace!.Where(a => a.Number < 3 && a.Branch == "over ceiling").Min(a => a.VideoBitrateK), iz);
        Assert.Equal(denenen.Min(), new FileInfo(cikti).Length / 1024.0 / 1024.0, 6);
        Assert.Empty(Directory.GetFiles(klasor, "vidshrink_partial_*"));
    }

    [Fact]
    public async Task SoranYoldaBirakSecilinceAyniIzDosyasizBiter()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor);
        var cikti = Path.Combine(klasor, "birak.mp4");

        var sonuc = await new EncodeRunner().RunAsync(Bilgi(kaynak), KucukPlan(), cikti, targetMb: 0.004, progress: null,
            fillPolicy: FillPolicy.FillTarget,
            askBeforeRetry: (s, _) => Task.FromResult(s.CanRetry ? OvershootChoice.Retry : OvershootChoice.Leave));

        var iz = string.Join(" | ", sonuc.Trace!.Select(a => $"{a.Number}:{a.Branch}:{a.VideoBitrateK}k:{a.ActualMb:0.#####}"));
        Assert.True(sonuc.Trace!.Any(a => a.Branch.StartsWith("ceiling guard", StringComparison.Ordinal)), iz);
        Assert.False(sonuc.Success, iz);
        Assert.True(sonuc.CeilingExceeded, iz);
        Assert.False(File.Exists(cikti), iz);
    }

    private static string YeniKlasor()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "tavan-bekcisi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static async Task<string> KaynakYapAsync(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=4",
            "-c:v", "libx264", "-crf", "18", "-g", "10", "-pix_fmt", "yuv420p", kaynak
        }) psi.ArgumentList.Add(arg);

        using var surec = new Process { StartInfo = psi };
        surec.Start();
        var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
        await surec.WaitForExitAsync();
        await bosalt;
        Assert.True(File.Exists(kaynak));
        return kaynak;
    }

    private static MediaInfo Bilgi(string kaynak) => new()
    {
        FilePath = kaynak,
        FileSizeBytes = new FileInfo(kaynak).Length,
        DurationSeconds = 4,
        Width = 320,
        Height = 240,
        Fps = 10,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000
    };

    private static EncodePlan KucukPlan() => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = 12,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ExtraArgs = new List<string> { "-threads", "1" }
    };
}
