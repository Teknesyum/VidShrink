using System.Diagnostics;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// <c>--kirpma-kipi</c> (HandBrake <c>--crop-mode</c>): dort kip, kiple celisen bayraklarin acik
/// hatasi ve temkinli kipin karari. Temkinli kip orneklerin birlesim dikdortgenini kirpar; canli
/// kol iki bant boyu tasiyan lavfi klibinde otomatik kipin dar, temkinli kipin genis dikdortgeni
/// buldugunu olcer. Olcum <c>docs/olcumler/kirpma-kipi.md</c>.
/// </summary>
public sealed class KirpmaKipiTests
{
    private static readonly CropRect Dar = new(640, 280, 0, 100);
    private static readonly CropRect Genis = new(640, 360, 0, 60);

    private static string[] Komut(params string[] ekler)
        => new[] { "plan", "a.mp4", "--hedef", "25MB" }.Concat(ekler).ToArray();

    [Theory]
    [InlineData("--kirpma-kipi", "auto", CropMode.Auto)]
    [InlineData("--crop-mode", "auto", CropMode.Auto)]
    [InlineData("--kirpma-kipi", "otomatik", CropMode.Auto)]
    [InlineData("--crop-mode", "conservative", CropMode.Conservative)]
    [InlineData("--kirpma-kipi", "temkinli", CropMode.Conservative)]
    [InlineData("--crop-mode", "NONE", CropMode.None)]
    [InlineData("--kirpma-kipi", "yok", CropMode.None)]
    public void KipIkiYazimdaDaAyrisiyor(string secenek, string deger, CropMode beklenen)
    {
        var sonuc = CliParser.Parse(Komut(secenek, deger));

        Assert.Null(sonuc.ErrorKey);
        Assert.Equal(beklenen, sonuc.Request!.CropMode);
        Assert.Equal(beklenen is CropMode.Auto or CropMode.Conservative, sonuc.Request.ProbesCrop);
        Assert.False(sonuc.Request.AutoCrop);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("elle")]
    public void ElleKipDikdortgenleBirlikteGeciyor(string deger)
    {
        var once = CliParser.Parse(Komut("--suzgec", "crop=320:240:10:20", "--crop-mode", deger));
        var sonra = CliParser.Parse(Komut("--crop-mode", deger, "--suzgec", "crop=320:240:10:20"));

        Assert.Null(once.ErrorKey);
        Assert.Null(sonra.ErrorKey);
        Assert.Equal(CropMode.Custom, once.Request!.CropMode);
        Assert.False(once.Request.ProbesCrop);
        Assert.Equal(new CropRect(320, 240, 10, 20), sonra.Request!.Filters!.Crop);
    }

    [Fact]
    public void KipsizKosumdaKipBos()
    {
        var sonuc = CliParser.Parse(Komut("--kirp"));

        Assert.Null(sonuc.Request!.CropMode);
        Assert.True(sonuc.Request.ProbesCrop);
        Assert.False(CliParser.Parse(Komut()).Request!.ProbesCrop);
    }

    [Theory]
    [InlineData("loose")]
    [InlineData("")]
    [InlineData("auto,none")]
    public void UydurmaKipReddediliyor(string deger)
    {
        var sonuc = CliParser.Parse(Komut("--crop-mode", deger));

        Assert.Equal("error.bad-crop-mode", sonuc.ErrorKey);
        Assert.Equal(deger, sonuc.ErrorArgument);
    }

    [Fact]
    public void DegersizKipDegerIstiyor()
        => Assert.Equal("error.missing-value", CliParser.Parse(Komut("--crop-mode")).ErrorKey);

    [Theory]
    [InlineData("error.crop-mode-none-conflict", "none", "--crop-mode", "none", "--kirp")]
    [InlineData("error.crop-mode-none-conflict", "none", "--kirp", "--crop-mode", "none")]
    [InlineData("error.crop-mode-none-conflict", "none", "--crop-mode", "none", "--suzgec", "crop=320:240:10:20")]
    [InlineData("error.crop-mode-none-conflict", "none", "--suzgec", "crop=320:240:10:20", "--crop-mode", "none")]
    [InlineData("error.crop-mode-needs-crop", "custom", "--crop-mode", "custom")]
    [InlineData("error.crop-mode-needs-crop", "custom", "--crop-mode", "custom", "--suzgec", "gray")]
    [InlineData("error.crop-mode-custom-conflict", "custom", "--crop-mode", "custom", "--suzgec", "crop=320:240:10:20", "--kirp")]
    [InlineData("error.crop-mode-manual-conflict", "auto", "--crop-mode", "auto", "--suzgec", "crop=320:240:10:20")]
    [InlineData("error.crop-mode-manual-conflict", "conservative", "--suzgec", "crop=320:240:10:20", "--crop-mode", "conservative")]
    public void KipleCelisenBayrakAcikHata(string anahtar, string kip, params string[] ekler)
    {
        var sonuc = CliParser.Parse(Komut(ekler));

        Assert.Equal(anahtar, sonuc.ErrorKey);
        Assert.Equal(kip, sonuc.ErrorArgument);
    }

    [Theory]
    [InlineData("--crop-mode", "auto", "--kirp")]
    [InlineData("--crop-mode", "conservative", "--kirp")]
    [InlineData("--crop-mode", "none", "--suzgec", "gray")]
    [InlineData("--crop-mode", "auto", "--suzgec", "gray")]
    public void CelismeyenBirlesimGeciyor(params string[] ekler)
        => Assert.Null(CliParser.Parse(Komut(ekler)).ErrorKey);

    [Theory]
    [InlineData("--kirpma-kipi")]
    [InlineData("--crop-mode")]
    public void IzleKipiKabulEtmiyor(string secenek)
    {
        var sonuc = CliParser.Parse(new[] { "izle", "gelen", "--cikti", "giden", "--hedef", "25MB", secenek, "auto" });

        Assert.Equal("error.not-in-watch", sonuc.ErrorKey);
        Assert.Equal(secenek, sonuc.ErrorArgument);
    }

    [Fact]
    public void KipAdlariEnumlaAyniSirada()
    {
        Assert.Equal(Enum.GetNames<CropMode>().Select(ad => ad.ToLowerInvariant()), CliParser.CropModes);
        foreach (var kip in Enum.GetValues<CropMode>())
        {
            Assert.True(CliParser.TryParseCropMode(CliParser.CropModeText(kip), out var geri));
            Assert.Equal(kip, geri);
        }
    }

    [Fact]
    public void TemkinliKararOrneklerinBirlesimi()
    {
        var karisik = Enumerable.Repeat(Dar, 7).Concat(Enumerable.Repeat(Genis, 3)).ToArray();

        Assert.Equal(Dar, CropProbe.Decide(karisik, 640, 480));
        Assert.Equal(Genis, CropProbe.DecideConservative(karisik, 640, 480));
        Assert.Equal(Dar, CropProbe.DecideConservative(Enumerable.Repeat(Dar, 10).ToArray(), 640, 480));
    }

    [Fact]
    public void TemkinliKararHerKenariAyriOlcuyor()
    {
        var ornekler = new[] { new CropRect(600, 480, 40, 0), new CropRect(640, 400, 0, 0), new CropRect(600, 400, 40, 0) };

        Assert.Equal(new CropRect(600, 400, 40, 0), CropProbe.Decide(ornekler.Append(ornekler[2]).ToArray(), 640, 480));
        Assert.Null(CropProbe.DecideConservative(ornekler, 640, 480));
        Assert.Equal(new CropRect(620, 440, 10, 20),
            CropProbe.DecideConservative(new[] { new CropRect(600, 440, 10, 20), new CropRect(580, 400, 50, 40) }, 640, 480));
    }

    [Fact]
    public void TemkinliKararTamKareyiVeBosListeyiKirpmiyor()
    {
        Assert.Null(CropProbe.DecideConservative(Array.Empty<CropRect>(), 640, 480));
        Assert.Null(CropProbe.DecideConservative(new[] { Dar, new CropRect(640, 480, 0, 0) }, 640, 480));
        Assert.Null(CropProbe.DecideConservative(new[] { new CropRect(-2, 0, 0, 0) }, 640, 480));
    }

    [Fact]
    public void TemkinliKararTekSayiliKenariDisariCifteYuvarliyor()
    {
        var tek = new[] { new CropRect(600, 400, 11, 20), new CropRect(600, 400, 20, 31) };

        var sonuc = CropProbe.DecideConservative(tek, 640, 480);

        Assert.Equal(new CropRect(610, 412, 11, 20), sonuc);
        Assert.Equal(new CropRect(630, 400, 10, 0),
            CropProbe.DecideConservative(new[] { new CropRect(629, 400, 11, 0) }, 640, 480));
        Assert.Equal(new CropRect(638, 400, 0, 0),
            CropProbe.DecideConservative(new[] { new CropRect(639, 400, 0, 0) }, 639, 480));
    }

    private static MediaInfo Kaynak(string yol) => new()
    {
        FilePath = yol,
        FileSizeBytes = 5_000_000,
        DurationSeconds = 8,
        Width = 640,
        Height = 480,
        Fps = 25,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", Channels: 2),
        },
    };

    private sealed record Kosum(int Exit, string Stdout, string Stderr, int YoklamaSayisi);

    private static string Klasor(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kirpma-kipi", ad);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static async Task<Kosum> Kos(IReadOnlyList<CropRect> ornekler, params string[] ekler)
    {
        var klasor = Klasor(Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            await File.WriteAllTextAsync(dosya, "x");
            var sayac = 0;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var args = new List<string> { "plan", dosya, "--hedef", "25MB", "--olcumsuz", "--json" };
            args.AddRange(ekler);

            var exit = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak(dosya)),
                    Availability = () => null,
                    DetectCrop = (_, _) =>
                    {
                        sayac++;
                        return Task.FromResult(new CropDetection(CropProbe.Decide(ornekler, 640, 480), ornekler, TimeSpan.Zero));
                    },
                },
                CancellationToken.None);

            return new Kosum(exit, stdout.ToString(), stderr.ToString(), sayac);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    private static string Zincir(string stdout)
    {
        using var belge = JsonDocument.Parse(stdout);
        var argumanlar = belge.RootElement.GetProperty("arguments")
            .EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var i = Array.IndexOf(argumanlar, "-vf");
        return i >= 0 && i + 1 < argumanlar.Length ? argumanlar[i + 1] : "";
    }

    private static CropRect[] Karisik => Enumerable.Repeat(Dar, 7).Concat(Enumerable.Repeat(Genis, 3)).ToArray();

    [Fact]
    public async Task OtomatikKipCogunlugunDikdortgeniniKirpiyor()
    {
        var kosum = await Kos(Karisik, "--crop-mode", "auto");

        Assert.Equal(ExitCodes.InBand, kosum.Exit);
        Assert.Equal(1, kosum.YoklamaSayisi);
        Assert.Contains($"crop={Dar}", Zincir(kosum.Stdout));
        Assert.Contains(CliText.ForLanguage("tr").Format("result.crop", Dar.ToString()), kosum.Stderr);
    }

    [Fact]
    public async Task TemkinliKipBirlesimDikdortgeniniKirpiyor()
    {
        var kosum = await Kos(Karisik, "--crop-mode", "conservative");

        Assert.Equal(ExitCodes.InBand, kosum.Exit);
        Assert.Equal(1, kosum.YoklamaSayisi);
        Assert.Contains($"crop={Genis}", Zincir(kosum.Stdout));
        Assert.DoesNotContain($"crop={Dar}", Zincir(kosum.Stdout));
        Assert.Contains(CliText.ForLanguage("tr").Format("result.crop-conservative", Genis.ToString()), kosum.Stderr);
    }

    [Fact]
    public async Task TemkinliKipBirlesmeyenOrnekteKirpmiyorVeSoyluyor()
    {
        var ornekler = Enumerable.Repeat(Genis, 7).Concat(Enumerable.Repeat(new CropRect(640, 480, 0, 0), 3)).ToArray();

        var temkinli = await Kos(ornekler, "--crop-mode", "conservative");
        var otomatik = await Kos(ornekler, "--crop-mode", "auto");

        Assert.DoesNotContain("crop=", Zincir(temkinli.Stdout));
        Assert.Contains(CliText.ForLanguage("tr")["result.crop-conservative-none"], temkinli.Stderr);
        Assert.Contains($"crop={Genis}", Zincir(otomatik.Stdout));
    }

    [Fact]
    public async Task BantYokkenTemkinliKipOlaganIletiyiYaziyor()
    {
        var kosum = await Kos(Array.Empty<CropRect>(), "--crop-mode", "conservative");

        Assert.DoesNotContain("crop=", Zincir(kosum.Stdout));
        Assert.Contains(CliText.ForLanguage("tr")["result.crop-none"], kosum.Stderr);
        Assert.DoesNotContain(CliText.ForLanguage("tr")["result.crop-conservative-none"], kosum.Stderr);
    }

    [Fact]
    public async Task YokKipiYoklamayiHicKosmuyor()
    {
        var kosum = await Kos(Karisik, "--crop-mode", "none");

        Assert.Equal(ExitCodes.InBand, kosum.Exit);
        Assert.Equal(0, kosum.YoklamaSayisi);
        Assert.DoesNotContain("crop=", Zincir(kosum.Stdout));
    }

    [Fact]
    public async Task ElleKipYalnizVerilenDikdortgeniKirpiyor()
    {
        var kosum = await Kos(Karisik, "--crop-mode", "custom", "--suzgec", "crop=320:240:10:20");

        Assert.Equal(0, kosum.YoklamaSayisi);
        Assert.Contains("crop=320:240:10:20", Zincir(kosum.Stdout));
    }

    [Fact]
    public async Task CeliskiKosumuDurduruyorVeIletiKipiSoyluyor()
    {
        var kosum = await Kos(Karisik, "--crop-mode", "none", "--suzgec", "crop=320:240:10:20");

        Assert.Equal(ExitCodes.Usage, kosum.Exit);
        Assert.Equal(0, kosum.YoklamaSayisi);
        Assert.Contains(CliText.ForLanguage("tr").Format("error.crop-mode-none-conflict", "none"), kosum.Stderr);
        Assert.Equal("", kosum.Stdout);
    }

    [Fact]
    public void IkiDilKipAnahtarlariniVeYardimiTasiyor()
    {
        var anahtarlar = new[]
        {
            "error.bad-crop-mode", "error.crop-mode-none-conflict", "error.crop-mode-custom-conflict",
            "error.crop-mode-needs-crop", "error.crop-mode-manual-conflict", "result.crop-conservative",
        };
        foreach (var dil in new[] { "tr", "en" })
        {
            var metin = CliText.ForLanguage(dil);
            foreach (var anahtar in anahtarlar) Assert.Contains("{0}", metin[anahtar]);
            Assert.NotEqual("result.crop-conservative-none", metin["result.crop-conservative-none"]);
            Assert.Contains("--kirpma-kipi", metin["help"]);
            foreach (var kip in CliParser.CropModes) Assert.Contains(kip, metin["help"]);
        }
        Assert.Contains("--crop-mode", CliText.ForLanguage("en")["help"]);
    }

    private static async Task Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-y", "-loglevel", "error" }.Concat(args)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        _ = p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        Assert.True(p.ExitCode == 0, await err);
    }

    private static string Bant(int boy, string kosul)
        => $"drawbox=x=0:y=0:w=iw:h={boy}:color=black:t=fill:enable='{kosul}',drawbox=x=0:y=ih-{boy}:w=iw:h={boy}:color=black:t=fill:enable='{kosul}'";

    /// <summary>
    /// 6 sn'lik 640x480 klip: ilk 1,8 sn 60 piksellik, kalani 100 piksellik bant. On ornegin
    /// ucu genis, yedisi dar dikdortgeni gorur.
    /// </summary>
    [FfmpegFact]
    public async Task CanliKlipteTemkinliKipGenisDikdortgeniBuluyor()
    {
        var klasor = Klasor("canli");
        var yol = Path.Combine(klasor, "iki-bant.mkv");
        await Ffmpeg("-f", "lavfi", "-i", "testsrc2=size=640x480:rate=25", "-t", "6",
            "-vf", Bant(60, "lt(t,1.8)") + "," + Bant(100, "gte(t,1.8)"),
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-threads", "2", yol);
        var info = await FfprobeClient.ProbeAsync(yol);

        var yoklama = await CropProbe.RunAsync(info);

        Assert.True(yoklama.Samples.Count >= 8, yoklama.Samples.Count.ToString());
        Assert.Contains(Genis, yoklama.Samples);
        Assert.Equal(Dar, yoklama.Rect);
        Assert.Equal(Genis, CropProbe.DecideConservative(yoklama.Samples, info.Width, info.Height));

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(
            new[] { "plan", yol, "--hedef", "0.2MB", "--olcumsuz", "--json", "--crop-mode", "conservative" },
            stdout, stderr, CliText.ForLanguage("tr"), new CliServices { Availability = () => null }, CancellationToken.None);

        Assert.True(exit == ExitCodes.InBand, stderr.ToString());
        Assert.Contains($"crop={Genis}", Zincir(stdout.ToString()));

        KanitKapanisi.Kapat(klasor, new[] { "iki-bant.mkv" });
    }
}
