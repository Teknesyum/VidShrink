using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E8, suzgec yuzeyi (<c>docs/plan.md</c>). Motor <see cref="VideoFilterChain"/> kirpma,
/// keskinlestirme, gurultu giderme ve dondurmeyi bastan beri uretiyordu; eksik olan
/// <see cref="VideoFilterChain.Parse"/>'a giden kapiydi. Bu olcu kapinin ucunu pimliyor:
/// kullanicinin yazdigi dizge plan seceneklerine, oradan ffmpeg argumanina iniyor.
///
/// <para>Bos dizge bugunku davranistir — kol bunu ayrica olcuyor, yoksa "suzgec yuzeyi
/// eklendi" degisikligi sessizce her kosuma bir suzgec takabilirdi.</para>
/// </summary>
public sealed class SuzgecYuzeyiTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "suzgec-kaynak.mp4"),
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

    private static async Task<(int Exit, string Stdout)> Kos(params string[] ekler)
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            ".calisma", "test-ciktilari", "e8-suzgec", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            await File.WriteAllTextAsync(dosya, "x");

            var stdout = new StringWriter();
            var args = new List<string> { "plan", dosya, "--hedef", "25MB", "--olcumsuz", "--json" };
            args.AddRange(ekler);

            var exit = await CliApp.RunAsync(
                args,
                stdout, new StringWriter(), CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak()),
                    Availability = () => null,
                },
                CancellationToken.None);

            return (exit, stdout.ToString());
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

    /// <summary>
    /// Ayristirici dizgeyi secenege ceviriyor. Bu kol motoru degil kapiyi olcuyor: CLI
    /// cozumlemesi bitince <see cref="CliRequest.Filters"/> dolu olmali.
    /// </summary>
    [Fact]
    public void DizgeIstegeCozuluyor()
    {
        var sonuc = CliParser.Parse(new[]
        {
            "plan", "girdi.mp4", "--hedef", "25MB", "--suzgec", "sharpen=strong,deblock,gray",
        });

        Assert.True(sonuc.Ok);
        var suzgecler = sonuc.Request!.Filters;
        Assert.NotNull(suzgecler);
        Assert.Equal(SharpenMode.Strong, suzgecler!.Sharpen);
        Assert.True(suzgecler.Deblock);
        Assert.True(suzgecler.Grayscale);
    }

    /// <summary>
    /// Istek plan seceneklerine tasiyor. Ayristirmayi gecip <c>PlanOptions</c>'a hic
    /// binmeyen bir alan, argumanda da gorunmezdi.
    /// </summary>
    [Fact]
    public void IstekPlanSeceneginiBesliyor()
    {
        var sonuc = CliParser.Parse(new[]
        {
            "plan", "girdi.mp4", "--hedef", "25MB", "--suzgec", "crop=320:240:0:0",
        });

        var secenekler = sonuc.Request!.ToPlanOptions(25);
        Assert.Equal(new CropRect(320, 240, 0, 0), secenekler.Filters!.Crop);
    }

    /// <summary>
    /// Ucun sonu: dizge ffmpeg'in <c>-vf</c> zincirinde gorunuyor. Yalniz secenegi
    /// pimlemek yetmez, plan hesabi suzgeci dusurebilirdi.
    /// </summary>
    [Fact]
    public async Task SuzgecArgumanZincirineIniyor()
    {
        var (exit, stdout) = await Kos("--suzgec", "sharpen=strong,gray");

        Assert.NotEqual(ExitCodes.Usage, exit);
        var zincir = Zincir(stdout);
        Assert.Contains("unsharp", zincir, StringComparison.Ordinal);
        Assert.Contains("hue=s=0", zincir, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bozuk dizge kosum baslamadan 64 donduruyor. Tanınmayan anahtar sessizce yutulursa
    /// kullanici yazdigi suzgecin uygulandigini sanirdi.
    /// </summary>
    [Fact]
    public async Task BozukDizgeKullanimHatasiVeriyor()
    {
        var (exit, _) = await Kos("--suzgec", "keskinlestir=cok");
        Assert.Equal(ExitCodes.Usage, exit);
    }

    /// <summary>
    /// Olumsuz kontrol: bayrak verilmeyince argumanlar bugunku haliyle kaliyor. Bos dizge
    /// de ayni kapiya cikiyor — ikisi de yeni bir suzgec eklemiyor.
    /// </summary>
    [Fact]
    public async Task SuzgecsizKosumBugunkuArgumaniKoruyor()
    {
        var (_, suzgecsiz) = await Kos();
        var (_, bos) = await Kos("--suzgec", "");

        var a = Zincir(suzgecsiz);
        Assert.Equal(a, Zincir(bos));
        Assert.DoesNotContain("unsharp", a, StringComparison.Ordinal);
        Assert.DoesNotContain("hue=s=0", a, StringComparison.Ordinal);
    }
}
