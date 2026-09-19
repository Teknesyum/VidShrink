using System.Diagnostics;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E4, baslik ve kaynak tarama (<c>docs/olcumler/e4-baslik-tarama.md</c>). Yoklama artik
/// <c>-show_programs</c> da okuyor: cok programli bir yayinda her program bir baslik.
/// Secim kurallari saf (<see cref="SourceTitles"/>), girdi argumani yalniz DVD'de degisiyor.
///
/// <para>Program secimi eslemeyi degil envanteri daraltiyor: eslemeler zaten mutlak indeksle
/// (<c>0:&lt;indeks&gt;</c>) yaziliyor, o yuzden <c>-map 0:p:N</c> sozdizimine gerek yok. Olcunun
/// pimledigi sey bu: secilen basligin akislari kaliyor, oburleri dusuyor.</para>
/// </summary>
public sealed class BaslikTaramaTests
{
    private static string Klasor()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            ".calisma", "test-ciktilari", "e4-baslik", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(yol);
        return Path.GetFullPath(yol);
    }

    /// <summary>
    /// Iki programli gercek bir MPEG-TS uretir: program 1 uc saniyelik video + ses,
    /// program 2 bes saniyelik yalniz video. Sureler bilerek farkli — <c>--ana-icerik</c>
    /// ve <c>--asgari-sure</c> ancak farkli surelerde olculebilir.
    /// </summary>
    private static string CokluTs(string klasor)
    {
        var yol = Path.Combine(klasor, "coklu.ts");
        var args = new[]
        {
            "-hide_banner", "-v", "error", "-y",
            "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=3",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=3",
            "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=5",
            "-map", "0:v", "-map", "1:a", "-map", "2:v",
            "-c:v", "mpeg2video", "-c:a", "mp2",
            "-program", "program_num=1:title=Kisa Baslik:st=0:st=1",
            "-program", "program_num=2:title=Uzun Baslik:st=2",
            yol,
        };
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"TS uretilemedi: {stderr}");
        return yol;
    }

    private static IReadOnlyList<SourceTitle> Ornek() => new[]
    {
        new SourceTitle(1, 3, 160, 120, 2, 0, "Kisa Baslik") { Kind = TitleSourceKind.Program, StreamIndexes = new[] { 0, 1 } },
        new SourceTitle(2, 5, 160, 120, 1, 0, "Uzun Baslik") { Kind = TitleSourceKind.Program, StreamIndexes = new[] { 2 } },
    };

    [FfmpegFact]
    public async Task IkiProgramliYayinIkiBaslikVeriyor()
    {
        var klasor = Klasor();
        try
        {
            var bilgi = await FfprobeClient.ProbeAsync(CokluTs(klasor));

            Assert.Equal(2, bilgi.Titles.Count);
            Assert.True(bilgi.HasMultipleTitles);
            Assert.Equal(new[] { 1, 2 }, bilgi.Titles.Select(b => b.Number).ToArray());
            Assert.Equal(new[] { "Kisa Baslik", "Uzun Baslik" }, bilgi.Titles.Select(b => b.Label).ToArray());
            Assert.Equal(new[] { 2, 1 }, bilgi.Titles.Select(b => b.StreamCount).ToArray());
            Assert.All(bilgi.Titles, b => Assert.Equal(TitleSourceKind.Program, b.Kind));
            Assert.Equal(3, bilgi.Titles[0].DurationSeconds, 1);
            Assert.Equal(5, bilgi.Titles[1].DurationSeconds, 1);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    /// <summary>
    /// Olumsuz kontrol: tek programli siradan bir dosyada envanter tek ogeli kaliyor ve
    /// baslik secici gorunmuyor. Boylece "iki baslik" olcusu kaynagin kendisini olcuyor,
    /// yoklamanin her dosyaya iki satir yazmasini degil.
    /// </summary>
    [FfmpegFact]
    public async Task DuzDosyadaTekBaslikVar()
    {
        var klasor = Klasor();
        try
        {
            var yol = Path.Combine(klasor, "tek.mp4");
            var args = new[]
            {
                "-hide_banner", "-v", "error", "-y",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=2",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", yol,
            };
            using (var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) })
            {
                process.Start();
                process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }

            var bilgi = await FfprobeClient.ProbeAsync(yol);
            Assert.Single(bilgi.Titles);
            Assert.False(bilgi.HasMultipleTitles);
            Assert.Equal(1, bilgi.Titles[0].Number);
            Assert.Equal(TitleSourceKind.File, bilgi.Titles[0].Kind);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public void AnaIcerikEnUzunuSeciyor()
    {
        Assert.Null(SourceTitles.Sec(Ornek(), null, anaIcerik: true, null, out var secilen));
        Assert.NotNull(secilen);
        Assert.Equal(2, secilen!.Number);

        Assert.Null(SourceTitles.Sec(Ornek(), null, anaIcerik: false, null, out var varsayilan));
        Assert.Equal(1, varsayilan!.Number);
    }

    /// <summary>Esitlikte kucuk numara kazaniyor; sira girdinin degil numaranin.</summary>
    [Fact]
    public void EsitSurelerdeKucukNumaraKazaniyor()
    {
        var basliklar = new[]
        {
            new SourceTitle(7, 5, 160, 120, 1, 0, null),
            new SourceTitle(3, 5, 160, 120, 1, 0, null),
        };
        Assert.Equal(3, SourceTitles.AnaIcerik(basliklar)!.Number);
    }

    [Fact]
    public void AsgariSureKisayiEliyor()
    {
        Assert.Equal(new[] { 2 }, SourceTitles.Ele(Ornek(), 4).Select(b => b.Number).ToArray());
        Assert.Equal(new[] { 1, 2 }, SourceTitles.Ele(Ornek(), null).Select(b => b.Number).ToArray());
        Assert.Equal(new[] { 1, 2 }, SourceTitles.Ele(Ornek(), 3).Select(b => b.Number).ToArray());
        Assert.Equal("error.no-titles-after-min", SourceTitles.Sec(Ornek(), null, false, 9, out _));
        Assert.Equal("error.bad-title", SourceTitles.Sec(Ornek(), 1, false, 4, out _));
    }

    [Fact]
    public void OlmayanBaslikNumarasiHataVeriyor()
    {
        Assert.Equal("error.bad-title", SourceTitles.Sec(Ornek(), 9, false, null, out var yok));
        Assert.Null(yok);
        Assert.Null(SourceTitles.Sec(Ornek(), 2, false, null, out var var));
        Assert.Equal(2, var!.Number);
        Assert.Equal("error.no-titles", SourceTitles.Sec(Array.Empty<SourceTitle>(), null, false, null, out _));
    }

    /// <summary>
    /// Secilen baslik envanteri daraltiyor: program 2'nin tek akisi kaliyor, program 1'in
    /// iki akisi dusuyor. Sure ve olcu de basliktan geliyor.
    /// </summary>
    [Fact]
    public void SecilenBaslikEnvanteriDaraltiyor()
    {
        var bilgi = Kaynak();
        var dar = SourceTitles.Uygula(bilgi, Ornek()[1]);

        Assert.Equal(new[] { 2 }, dar.Streams.Select(s => s.Index).ToArray());
        Assert.Equal(5, dar.DurationSeconds, 3);
        Assert.Equal(3, bilgi.Streams.Count);
    }

    [Fact]
    public void DvdKoluDemuxerArgumaniniKuruyor()
    {
        Assert.Equal(new[] { "-f", "dvdvideo", "-title", "3" }, new DiscSource(3, null).InputArguments());
        Assert.Equal(new[] { "-f", "dvdvideo", "-title", "3", "-angle", "2" }, new DiscSource(3, 2).InputArguments());

        var bilgi = Kaynak();
        var plan = Plan();
        plan.Disc = new DiscSource(3, 2);
        var argumanlar = FfmpegArguments.Build(bilgi, plan, Path.Combine(Path.GetTempPath(), "cikti.mp4"), 0, null);
        var girdi = argumanlar.ToList().IndexOf("-i");
        Assert.True(girdi > 0);
        Assert.Equal(new[] { "-f", "dvdvideo", "-title", "3", "-angle", "2" },
            argumanlar.Skip(girdi - 6).Take(6).ToArray());

        var diskSiz = FfmpegArguments.Build(bilgi, Plan(), Path.Combine(Path.GetTempPath(), "cikti.mp4"), 0, null);
        Assert.DoesNotContain("dvdvideo", diskSiz);
    }

    [Fact]
    public void BaslikVeAnaIcerikBirlikteVerilemiyor()
    {
        Assert.Equal("error.title-and-main-feature",
            CliParser.Parse(new[] { "kucult", "a.mp4", "--baslik", "2", "--ana-icerik" }).ErrorKey);
        Assert.Equal("error.title-and-main-feature",
            CliParser.Parse(new[] { "kucult", "a.mp4", "--ana-icerik", "--baslik", "2" }).ErrorKey);
        Assert.Equal("error.bad-title", CliParser.Parse(new[] { "kucult", "a.mp4", "--baslik", "0" }).ErrorKey);
        Assert.Equal("error.bad-angle", CliParser.Parse(new[] { "kucult", "a.mp4", "--aci", "0" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--aci", "9", "--asgari-sure", "60" }).ErrorKey);
    }

    /// <summary>
    /// Bos baslik envanteri hata degil: yoklama baslik tasimiyorsa (sahte servis, eski
    /// yoklama) kucultme normal yoluna devam eder. Kullanici acikca baslik istediginde
    /// ayni envanter hata verir; iki kolu birlikte pimliyoruz.
    /// </summary>
    [Fact]
    public async Task BaslikTasimayanYoklamaKucultmeyiDurdurmuyor()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            await File.WriteAllTextAsync(dosya, "x");

            var stderrSuz = new StringWriter();
            var suz = await CliApp.RunAsync(
                new[] { "plan", dosya, "--hedef", "25MB", "--olcumsuz" },
                new StringWriter(), stderrSuz, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak()),
                    Availability = () => null,
                },
                CancellationToken.None);

            Assert.NotEqual(ExitCodes.Usage, suz);

            var stderrIstekli = new StringWriter();
            var istekli = await CliApp.RunAsync(
                new[] { "plan", dosya, "--hedef", "25MB", "--olcumsuz", "--baslik", "2" },
                new StringWriter(), stderrIstekli, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak()),
                    Availability = () => null,
                },
                CancellationToken.None);

            Assert.Equal(ExitCodes.Usage, istekli);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    /// <summary>
    /// <c>--tarama</c> hicbir sey kodlamiyor: kodlayici yoklamasi cagrilirsa olcu catlar.
    /// Envanterin iki satiri ve numaralari ciktida.
    /// </summary>
    [Fact]
    public async Task TaramaHicbirSeyiKodlamiyor()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.ts");
            await File.WriteAllTextAsync(dosya, "x");
            var stdout = new StringWriter();

            var exit = await CliApp.RunAsync(
                new[] { "kucult", dosya, "--hedef", "25MB", "--tarama" },
                stdout, new StringWriter(), CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak() with { Titles = Ornek() }),
                    Availability = () => throw new InvalidOperationException("tarama kodlamamali"),
                },
                CancellationToken.None);

            Assert.Equal(ExitCodes.InBand, exit);
            var metin = stdout.ToString();
            Assert.Contains("Kisa Baslik", metin, StringComparison.Ordinal);
            Assert.Contains("Uzun Baslik", metin, StringComparison.Ordinal);
            Assert.DoesNotContain("{0}", metin, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    /// <summary><c>--asgari-sure</c> taramada da eliyor: kisa baslik listeye hic girmiyor.</summary>
    [Fact]
    public async Task TaramaAsgariSureyiUyguluyor()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.ts");
            await File.WriteAllTextAsync(dosya, "x");
            var stdout = new StringWriter();

            await CliApp.RunAsync(
                new[] { "kucult", dosya, "--hedef", "25MB", "--tarama", "--asgari-sure", "4" },
                stdout, new StringWriter(), CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak() with { Titles = Ornek() }),
                    Availability = () => throw new InvalidOperationException("tarama kodlamamali"),
                },
                CancellationToken.None);

            var metin = stdout.ToString();
            Assert.DoesNotContain("Kisa Baslik", metin, StringComparison.Ordinal);
            Assert.Contains("Uzun Baslik", metin, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    /// <summary>Olmayan baslik numarasi uygulama duzeyinde cumleye donuyor, yer tutucu sizmiyor.</summary>
    [Fact]
    public async Task OlmayanBaslikCumlesiNumarayiTasiyor()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.ts");
            await File.WriteAllTextAsync(dosya, "x");
            var stderr = new StringWriter();

            var exit = await CliApp.RunAsync(
                new[] { "kucult", dosya, "--hedef", "25MB", "--baslik", "9" },
                new StringWriter(), stderr, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak() with { Titles = Ornek() }),
                    Availability = () => throw new InvalidOperationException("hatali baslik kodlamamali"),
                },
                CancellationToken.None);

            Assert.Equal(ExitCodes.Usage, exit);
            Assert.Contains("9", stderr.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("{0}", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "kaynak.ts"),
        FileSizeBytes = 5_000_000,
        DurationSeconds = 8,
        Width = 160,
        Height = 120,
        Fps = 10,
        VideoCodec = "mpeg2video",
        TotalBitrateBps = 5_000_000,
        AudioCodec = "mp2",
        AudioBitrateBps = 384_000,
        AudioChannels = 1,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "mpeg2video"),
            new SourceStream(1, StreamKind.Audio, "mp2", Channels: 1),
            new SourceStream(2, StreamKind.Video, "mpeg2video"),
        },
    };

    private static EncodePlan Plan() => new()
    {
        Mode = "2pass",
        Codec = "libx264",
        VideoBitrateK = 800,
        AudioBitrateK = 96,
        Width = 160,
        Height = 120,
        Fps = 10,
    };
}
