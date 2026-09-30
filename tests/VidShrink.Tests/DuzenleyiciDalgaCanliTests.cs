using System.Diagnostics;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciDalgaCanliTests
{
    private static string Klasor()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "duzenleyici-dalga", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static string Uret(string klasor, string ad, params string[] argumanlar)
    {
        var yol = Path.Combine(klasor, ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[] { "-y", "-hide_banner" }.Concat(argumanlar).Append(yol).ToArray());
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi: {hata[Math.Max(0, hata.Length - 400)..]}");
        return yol;
    }

    private static string YarimSinus(string klasor)
        => Uret(klasor, "ses, ş.wav", "-f", "lavfi", "-i", @"aevalsrc=if(lt(t\,1.5)\,0.5*sin(2*PI*440*t)\,0):s=48000:d=3", "-c:a", "pcm_s16le");

    [FfmpegFact]
    public async Task BilinenGenlikTepelereDogruYansir()
    {
        var klasor = Klasor();
        var kaynak = YarimSinus(klasor);

        var tepe = await AudioPeaks.ReadAsync(ToolLocator.Ffmpeg, kaynak, CancellationToken.None);

        Assert.InRange(tepe.Count, 298, 302);
        for (var i = 10; i < 140; i++)
        {
            Assert.InRange(tepe[i].Max, (short)(16384 * 0.9), (short)(16384 * 1.03));
            Assert.InRange(tepe[i].Min, (short)(-16384 * 1.03), (short)(-16384 * 0.9));
        }
        for (var i = 160; i < tepe.Count - 5; i++)
            Assert.InRange(Math.Max(Math.Abs((int)tepe[i].Min), tepe[i].Max), 0, 64);

        var dilim = tepe.Slice(0, 3 * EditTime.TicksPerSecond, true, 3);
        Assert.True(dilim[0].Max <= 64, $"ters dilimin basi sessiz olmali: {dilim[0]}");
        Assert.True(dilim[2].Max > 16384 * 0.9, $"ters dilimin sonu sesli olmali: {dilim[2]}");
        Directory.Delete(klasor, true);
    }

    [FfmpegFact]
    public async Task SessizKaynakBosDoner()
    {
        var klasor = Klasor();
        var kaynak = Uret(klasor, "sessiz.mp4", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25", "-t", "1", "-pix_fmt", "yuv420p", "-c:v", "libx264");

        var tepe = await AudioPeaks.ReadAsync(ToolLocator.Ffmpeg, kaynak, CancellationToken.None);

        Assert.True(tepe.IsEmpty);
        Directory.Delete(klasor, true);
    }

    [FfmpegFact]
    public async Task IptalSureciOldurur()
    {
        var klasor = Klasor();
        var kaynak = YarimSinus(klasor);
        using var iptal = new CancellationTokenSource();
        var pid = 0;
        var oncelik = ProcessPriorityClass.Normal;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AudioPeaks.ReadAsync(ToolLocator.Ffmpeg, kaynak, iptal.Token, surec =>
        {
            pid = surec.Id;
            try { oncelik = surec.PriorityClass; } catch (InvalidOperationException) { }
            iptal.Cancel();
        }));

        Assert.NotEqual(0, pid);
        Assert.Equal(ProcessPriorityClass.BelowNormal, oncelik);
        Process? kalan = null;
        try { kalan = Process.GetProcessById(pid); } catch (ArgumentException) { }
        Assert.True(kalan is null || kalan.HasExited || !kalan.ProcessName.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase), $"ffmpeg {pid} hala kosuyor");
        kalan?.Dispose();
        Directory.Delete(klasor, true);
    }

    [FfmpegFact]
    public async Task OnbellekDegisiklikteTazelenir()
    {
        var klasor = Klasor();
        var kaynak = YarimSinus(klasor);
        AudioPeaks.ClearCache();

        var ilk = await AudioPeaks.LoadAsync(ToolLocator.Ffmpeg, kaynak);
        var ikinci = await AudioPeaks.LoadAsync(ToolLocator.Ffmpeg, kaynak);
        File.SetLastWriteTimeUtc(kaynak, File.GetLastWriteTimeUtc(kaynak).AddSeconds(5));
        var ucuncu = await AudioPeaks.LoadAsync(ToolLocator.Ffmpeg, kaynak);

        Assert.Same(ilk, ikinci);
        Assert.NotSame(ilk, ucuncu);
        Assert.Equal(ilk.Count, ucuncu.Count);
        AudioPeaks.ClearCache();
        Directory.Delete(klasor, true);
    }
}
