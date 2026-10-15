using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P8 (later 57): önizlemede çizilen kırpma dikdörtgeni. Ekran noktası önizlemenin içerik
/// dikdörtgenine (yakınlaştırma ve kaydırma dahil) göre orana, oran kaynak pikseline çevrilir;
/// genişlik ve yükseklik çift sayıya iner, dikdörtgen kareden taşmaz.
/// </summary>
public sealed class ElleKirpmaTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "elle-kirpma");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Kaynak(int genislik, int yukseklik) => new()
    {
        FilePath = "kaynak.mkv", FileSizeBytes = 1, DurationSeconds = 1, Width = genislik, Height = yukseklik,
        Fps = 25, VideoCodec = "h264", TotalBitrateBps = 1
    };

    [Fact]
    public void SigdirilmisOnizlemedekiDikdortgenKaynakPikselineCevrilir()
    {
        var oran = ManualCrop.FromScreen(100 + 240, 50 + 135, 100 + 720, 50 + 405, 100, 50, 960, 540);

        Assert.Equal(new CropRect(960, 540, 480, 270), ManualCrop.ToSource(oran, 1920, 1080));
    }

    [Fact]
    public void YakinlastirilmisVeKaydirilmisOnizlemeAyniKaynakDikdortgeniniVerir()
    {
        var oran = ManualCrop.FromScreen(0, 0, 1920, 1080, -960, -540, 3840, 2160);

        Assert.Equal(new CropRect(960, 540, 480, 270), ManualCrop.ToSource(oran, 1920, 1080));
    }

    [Fact]
    public void TekSayilarCifteInerVeTersSuruklemeDuzelir()
    {
        var oran = ManualCrop.FromScreen(701, 451, 101, 51, 0, 0, 1280, 720);
        var rect = ManualCrop.ToSource(oran, 1280, 720)!.Value;

        Assert.Equal(new CropRect(600, 400, 100, 50), rect);
        Assert.Empty(VideoFilterChain.Validate(Kaynak(1280, 720), VideoFilterOptions.Default.WithCrop(rect)));
    }

    [Fact]
    public void KareDisinaTasanSecimKareyeKelepcelenirTamKareKirpmaz()
    {
        var oran = ManualCrop.FromScreen(-50, -50, 2000, 2000, 0, 0, 1280, 720);

        Assert.Equal(ManualCrop.Full, oran);
        Assert.Null(ManualCrop.ToSource(oran, 1280, 720));
    }

    [Fact]
    public void TekBoyutluKaynaktaDikdortgenKareIcindeKalir()
    {
        var rect = ManualCrop.ToSource(new CropFraction(0, 0, 1, 0.4), 1281, 721)!.Value;

        Assert.Equal(new CropRect(1280, 288, 0, 0), rect);
        Assert.Empty(VideoFilterChain.Validate(Kaynak(1281, 721), VideoFilterOptions.Default.WithCrop(rect)));
    }

    [Fact]
    public void TasimaKareIcindeDurur()
    {
        var tasinan = ManualCrop.Move(new CropFraction(0.5, 0.5, 0.9, 0.8), 0.3, -0.7);

        Assert.Equal(new CropFraction(0.6, 0, 1, 0.3), tasinan with
        {
            Left = Math.Round(tasinan.Left, 6), Top = Math.Round(tasinan.Top, 6),
            Right = Math.Round(tasinan.Right, 6), Bottom = Math.Round(tasinan.Bottom, 6)
        });
    }

    /// <summary>
    /// Uçtan uca: ekranda çizilen dikdörtgen <see cref="VideoFilterChain.PictureFilters"/>'tan geçip
    /// ffmpeg'le kodlanır, çıktının çözünürlüğü ffprobe ile okunur. 320x240 kaynak 640x480'e
    /// büyütülmüş önizlemede; seçimin tek yüksekliği (151) çifte (150) iner.
    /// </summary>
    [FfmpegFact]
    public async Task CizilenDikdortgenCiktiCozunurlugunuBelirler()
    {
        var kaynak = Path.Combine(Kanit, "kaynak.mkv");
        var cikti = Path.Combine(Kanit, "kirpik.mkv");
        await Ffmpeg("-f", "lavfi", "-i", "testsrc2=size=320x240:rate=25", "-t", "1",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", kaynak);
        var info = await FfprobeClient.ProbeAsync(kaynak);

        var oran = ManualCrop.FromScreen(10 + 100, 20 + 60, 10 + 500, 20 + 362, 10, 20, 640, 480);
        var rect = ManualCrop.ToSource(oran, info.Width, info.Height)!.Value;
        Assert.Equal(new CropRect(200, 150, 50, 30), rect);

        var vf = string.Join(",", VideoFilterChain.PictureFilters(info, VideoFilterOptions.Default.WithCrop(rect)));
        await Ffmpeg("-i", kaynak, "-vf", vf, "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", cikti);
        var sonuc = await FfprobeClient.ProbeAsync(cikti);

        Assert.Equal((200, 150), (sonuc.Width, sonuc.Height));
        KanitKapanisi.Kapat(Path.Combine(GirdiKanit.Root, ".calisma", "elle-kirpma"), "kaynak.mkv", "kirpik.mkv");
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
}
