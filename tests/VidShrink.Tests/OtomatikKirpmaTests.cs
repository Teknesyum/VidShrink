using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// K8 borcu 7 (<c>docs/plan.md</c>). <see cref="CropProbe"/> bastan beri hazirdi, uretimde
/// cagiran tek satir yoktu. Bu olcu <c>--kirp</c> kapisini pimliyor: yoklamanin dikdortgeni
/// suzgec zincirine, oradan ffmpeg argumanina iniyor.
///
/// <para>Bayraksiz kosumda yoklamanin hic calismamasi ayri bir kol — yoksa "kirpma acildi"
/// degisikligi sessizce her kosuma on ffmpeg surecinin yukunu takabilirdi.</para>
/// </summary>
public sealed class OtomatikKirpmaTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "kirpma-kaynak.mp4"),
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

    private static async Task<Kosum> Kos(CropRect? bulunan, params string[] ekler)
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            ".calisma", "test-ciktilari", "k8-kirpma", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            await File.WriteAllTextAsync(dosya, "x");

            var sayac = 0;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var args = new List<string> { "plan", dosya, "--hedef", "25MB", "--olcumsuz", "--json" };
            args.AddRange(ekler);

            var exit = await CliApp.RunAsync(
                args,
                stdout, stderr, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak()),
                    Availability = () => null,
                    DetectCrop = (_, _) =>
                    {
                        sayac++;
                        return Task.FromResult(new CropDetection(bulunan, Array.Empty<CropRect>(), TimeSpan.Zero));
                    },
                },
                CancellationToken.None);

            return new Kosum(exit, stdout.ToString(), stderr.ToString(), sayac);
        }
        finally
        {
            Directory.Delete(Path.GetFullPath(klasor), recursive: true);
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

    [Fact]
    public async Task BayrakVerilinceYoklamaninDikdortgeniZincireGiriyor()
    {
        var kosum = await Kos(new CropRect(640, 360, 0, 60), "--kirp");

        Assert.Equal(1, kosum.YoklamaSayisi);
        Assert.Contains("crop=640:360:0:60", Zincir(kosum.Stdout));
        Assert.Contains("640:360:0:60", kosum.Stderr);
    }

    [Fact]
    public async Task BantYokkenZincirdeKirpmaOlmuyor()
    {
        var kosum = await Kos(null, "--kirp");

        Assert.Equal(1, kosum.YoklamaSayisi);
        Assert.DoesNotContain("crop=", Zincir(kosum.Stdout));
    }

    [Fact]
    public async Task BayraksizKosumdaYoklamaHicCalismiyor()
    {
        var kosum = await Kos(new CropRect(640, 360, 0, 60));

        Assert.Equal(0, kosum.YoklamaSayisi);
        Assert.DoesNotContain("crop=", Zincir(kosum.Stdout));
    }

    [Fact]
    public async Task ElleVerilenKirpmaYoklamayiEziyor()
    {
        var kosum = await Kos(new CropRect(640, 360, 0, 60), "--kirp", "--suzgec", "crop=320:240:10:20");

        Assert.Equal(0, kosum.YoklamaSayisi);
        Assert.Contains("crop=320:240:10:20", Zincir(kosum.Stdout));
        Assert.DoesNotContain("640:360", Zincir(kosum.Stdout));
    }

    [Fact]
    public async Task KirpmaDigerSuzgeclerleBirlikteYasiyor()
    {
        var kosum = await Kos(new CropRect(640, 360, 0, 60), "--kirp", "--suzgec", "denoise=hqdn3d");

        Assert.Equal(1, kosum.YoklamaSayisi);
        var zincir = Zincir(kosum.Stdout);
        Assert.Contains("crop=640:360:0:60", zincir);
        Assert.Contains("hqdn3d", zincir);
    }

    [Fact]
    public void IkiDilDeKirpmaAnahtarlariniTasiyor()
    {
        foreach (var dil in new[] { "tr", "en" })
        {
            var metin = CliText.ForLanguage(dil);
            Assert.False(string.IsNullOrWhiteSpace(metin["progress.crop"]));
            Assert.Contains("{0}", metin["result.crop"]);
            Assert.False(string.IsNullOrWhiteSpace(metin["result.crop-none"]));
        }
    }
}
