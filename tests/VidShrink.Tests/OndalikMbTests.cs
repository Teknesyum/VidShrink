using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// <c>docs/netlestirme/025-mb-birimi.md</c> olumsuz kontrolleri: hedefin MB'ı ondalık,
/// 1 MB = 1 000 000 bayt. Katsayı 8388,608'e ya da bayt tavanı 1024²'ye dönerse kırmızı.
/// </summary>
public sealed class OndalikMbTests
{
    private static readonly string EncodeRunnerPath =
        Path.Combine(TipSources.Root, "src", "VidShrink.Ffmpeg", "EncodeRunner.cs");

    private static readonly string LocalesDir =
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    [Fact]
    public void BirMegabaytSekizBinKilobit()
    {
        Assert.Equal(1_000_000.0, Megabayt.Bayt);
        Assert.Equal(8000.0, Megabayt.Kbit);
        Assert.Equal(7960.0, PlanCalculator.VideoKbitFor(1, 1), 9);
        Assert.Equal(1 / 0.995, PlanCalculator.NonVideoMb(8000, 1), 12);
    }

    [Fact]
    public void KayitButcesiAyniKatsayidan()
    {
        Assert.Equal(8000 - RecorderBudget.AudioKbps, RecorderBudget.From(1, 1, 1).VideoKbps);
    }

    [Fact]
    public void HedefinBaytTavaniOndalik()
    {
        Assert.Equal(25_000_000L, Megabayt.Tavan(25));
        Assert.NotEqual(26_214_400L, Megabayt.Tavan(25));
        Assert.Equal(25_000_000L, RecorderArguments.LimitBytes(25));
        Assert.Equal(500_000_000L, DiskSpaceGuard.RequiredBytes(100));
    }

    [Fact]
    public void EncodeRunnerBaytTavaniniMegabayttanAlir()
    {
        var code = File.ReadAllText(EncodeRunnerPath);
        Assert.Contains("var targetBytes = Megabayt.Tavan(effectiveTargetMb);", code);
        Assert.DoesNotContain("1024.0 / 1024.0", code);
        Assert.DoesNotContain("1024 * 1024", code);
    }

    [Fact]
    public void KaynakVeSonucHedeflaAyniBirimde()
    {
        var info = new MediaInfo { FilePath = "a.mp4", FileSizeBytes = 25_000_000L, DurationSeconds = 10 };
        Assert.Equal(25.0, info.FileSizeMb, 12);
        Assert.Equal(25.0, Megabayt.Oku(Megabayt.Tavan(25)), 12);
    }

    [Fact]
    public void UguuYongasiTavaninAltinda()
    {
        Assert.Equal(134, PresetLibrary.BuiltIn.Find("share-uguu")!.TargetMb);
        Assert.True(Megabayt.Tavan(134) < 128L * 1024 * 1024);
        Assert.True(Megabayt.Tavan(135) > 128L * 1024 * 1024);
    }

    [Fact]
    public void DilDosyalarindaMiBYok()
    {
        var files = Directory.GetFiles(LocalesDir, "main.json", SearchOption.AllDirectories);
        Assert.Equal(42, files.Length);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("MiB", text);
            using var doc = JsonDocument.Parse(text);
            Assert.False(doc.RootElement.TryGetProperty("main.chip.128.tip", out _), file);
            var tip = doc.RootElement.GetProperty("main.chip.134.tip").GetString()!;
            Assert.Contains("134", tip);
            Assert.DoesNotContain("128", tip);
        }
    }
}
