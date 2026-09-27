using System.Globalization;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciEdlCanliTests
{
    private static string Klip(string klasor, string ad)
    {
        Assert.True(ToolLocator.IsAvailable(out var eksik), $"klip uretimi icin {eksik} gerekli");
        var yol = Path.Combine(klasor, ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25", "-t", "1", "-pix_fmt", "yuv420p", "-c:v", "libx264", "-g", "5", yol);
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi: {hata[Math.Max(0, hata.Length - 400)..]}");
        return yol;
    }

    [Fact]
    public async Task IkiParcaliEdlLibmpvdeKesimSuresiyleAcilir()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "duzenleyici-edl", "a,b;c=d " + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(klasor);
        var klip = Klip(klasor, "klip, ş.mp4");
        const long sn = EditTime.TicksPerSecond;
        var onizleme = new EdlPreview(klip, new EditTimeline(new[] { new EditClip(sn * 3 / 5, sn), new EditClip(0, sn * 2 / 5) }));

        using var motor = new MpvEngine();
        motor.SetProperty("ao", "null");
        await motor.OpenAsync(onizleme.Uri);
        var sure = motor.DurationSeconds;
        var kayit = $"uri: {onizleme.Uri}{Environment.NewLine}sure: {sure.ToString("0.###", CultureInfo.InvariantCulture)} sn{Environment.NewLine}log: {string.Join(" | ", motor.RecentLog)}";

        Assert.True(Math.Abs(sure - 0.8) < 0.05, kayit);
        Assert.True(motor.IsOpen, kayit);
        motor.Dispose();
        Directory.Delete(klasor, true);
    }
}
