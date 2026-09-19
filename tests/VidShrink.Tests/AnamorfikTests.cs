using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E9, anamorfik kaynak (<c>docs/plan.md</c>). Depo piksel en-boy oranini (PAR) hic
/// okumuyordu: <c>sample_aspect_ratio</c> gecen tek satir yoktu. 720x480 bir DVD karesi
/// SAR 32:27 ile 853x480 gosterilir; kare piksel sayilinca olcek merdiveni yanlis orandan
/// iniyor ve cikti yassi kodlaniyordu.
///
/// <para>Karar HandBrake'in <c>--non-anamorphic</c> davranisi: yukseklik korunur, genislik
/// gosterim genisligine cevrilir, cikti kare pikselli olur. O yuzden zincire
/// <c>setsar=1</c> de giriyor — olcek suzgeci kaynagin SAR metadata'sini oldugu gibi
/// tasir, sifirlanmazsa oynatici kareyi ikinci kez esnetir.</para>
/// </summary>
public sealed class AnamorfikTests
{
    private static string Klasor()
    {
        var yol = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            ".calisma", "test-ciktilari", "e9-anamorfik", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(yol);
        return Path.GetFullPath(yol);
    }

    /// <summary>
    /// Gercek bir dosya uretir. <paramref name="sar"/> <c>null</c> ise kare piksel kalir;
    /// aksi halde <c>setsar</c> ile anamorfik isaretlenir. <paramref name="donus"/>
    /// verilince ikinci bir kopyalama gecisi gorunum matrisini yazar: ffmpeg 9.0
    /// <c>-metadata:s:v rotate=</c> etiketini artik dosyaya gecirmiyor, olculdu.
    /// </summary>
    private static string Uret(string klasor, string ad, string olcu, string? sar, int? donus = null)
    {
        var yol = Path.Combine(klasor, ad);
        var args = new List<string>
        {
            "-hide_banner", "-v", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc=size={olcu}:rate=10:duration=2",
        };
        if (sar is not null) args.AddRange(new[] { "-vf", $"setsar={sar}" });
        args.AddRange(new[] { "-c:v", "libx264", "-preset", "ultrafast", yol });
        Ffmpeg(args);

        if (donus is not { } aci) return yol;

        var donuk = Path.Combine(klasor, "donuk-" + ad);
        Ffmpeg(new List<string>
        {
            "-hide_banner", "-v", "error", "-y",
            "-display_rotation", aci.ToString(),
            "-i", yol, "-c", "copy", donuk,
        });
        return donuk;
    }

    private static void Ffmpeg(List<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"kaynak uretilemedi: {stderr}");
    }

    private static MediaInfo Kaynak(int genislik, int yukseklik, int parPay, int parPayda) => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "anamorfik-kaynak.mp4"),
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = genislik,
        Height = yukseklik,
        Fps = 25,
        VideoCodec = "mpeg2video",
        TotalBitrateBps = 5_000_000,
        ParNum = parPay,
        ParDen = parPayda,
        AudioCodec = "ac3",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "mpeg2video"),
            new SourceStream(1, StreamKind.Audio, "ac3", Channels: 2),
        },
    };

    [FfmpegFact]
    public async Task AnamorfikDosyadanPikselOraniOkunuyor()
    {
        var klasor = Klasor();
        try
        {
            var bilgi = await FfprobeClient.ProbeAsync(Uret(klasor, "genis.mp4", "720x480", "32/27"));

            Assert.Equal(720, bilgi.Width);
            Assert.Equal(480, bilgi.Height);
            Assert.Equal(32, bilgi.ParNum);
            Assert.Equal(27, bilgi.ParDen);
            Assert.True(bilgi.IsAnamorphic);
            Assert.Equal(853, bilgi.DisplayWidth);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Olumsuz kontrol: kare pikselli dosyada hicbir sey degismemeli. Yoklama <c>"1:1"</c>
    /// da yazsa, alani hic yazmasa da sonuc ayni kapiya cikar.
    /// </summary>
    [FfmpegFact]
    public async Task KarePikselDosyaBirBireBirDonuyor()
    {
        var klasor = Klasor();
        try
        {
            var bilgi = await FfprobeClient.ProbeAsync(Uret(klasor, "kare.mp4", "320x240", null));

            Assert.Equal(1, bilgi.ParNum);
            Assert.Equal(1, bilgi.ParDen);
            Assert.False(bilgi.IsAnamorphic);
            Assert.Equal(320, bilgi.DisplayWidth);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Ceyrek donuste boyutlarla birlikte oran da ters cevrilir: dondurulmus karede genis
    /// piksel uzun piksele donusur. Ters cevrilmezse dondurulmus DVD iki kat yanlis cikar.
    /// </summary>
    [FfmpegFact]
    public async Task CeyrekDonusteOranTersCevriliyor()
    {
        var klasor = Klasor();
        try
        {
            var bilgi = await FfprobeClient.ProbeAsync(Uret(klasor, "donuk.mp4", "720x480", "32/27", 90));

            Assert.Equal(480, bilgi.Width);
            Assert.Equal(720, bilgi.Height);
            Assert.Equal(27, bilgi.ParNum);
            Assert.Equal(32, bilgi.ParDen);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void MerdivenGosterimGenisligindenIniyor()
    {
        var plan = PlanCalculator.Build(Kaynak(720, 480, 32, 27), new PlanOptions { TargetMb = 25 });

        Assert.NotEqual(720, plan.Width);
        var oran = plan.Width / (double)plan.Height;
        Assert.InRange(oran, 16.0 / 9 - 0.02, 16.0 / 9 + 0.02);
    }

    [Fact]
    public void AnamorfikKaynakIkiUreticideDeSetsarKoyuyor()
    {
        var bilgi = Kaynak(720, 480, 32, 27);
        var plan = PlanCalculator.Build(bilgi, new PlanOptions { TargetMb = 25 });

        Assert.Contains(VideoFilterChain.SquarePixelFilter, VideoFilterChain.Filters(bilgi, plan));

        var donusum = new ConversionPlan { Width = plan.Width, Height = plan.Height };
        var argumanlar = ConversionArguments.Build(bilgi, donusum, "cikti.mp4").ToArray();
        var i = Array.IndexOf(argumanlar, "-vf");
        Assert.True(i >= 0, "donusum yolunda -vf yok");
        Assert.Contains(VideoFilterChain.SquarePixelFilter, argumanlar[i + 1]);
    }

    /// <summary>
    /// Olumsuz kontrol: kare pikselli kaynakta iki ureticinin de zinciri degismemeli.
    /// Yoksa "PAR okunuyor" degisikligi her kosuma sessizce bir suzgec takardi.
    /// </summary>
    [Fact]
    public void KarePikselKaynakZinciriDegistirmiyor()
    {
        var bilgi = Kaynak(1920, 1080, 1, 1);
        var plan = PlanCalculator.Build(bilgi, new PlanOptions { TargetMb = 25 });

        Assert.DoesNotContain(VideoFilterChain.SquarePixelFilter, VideoFilterChain.Filters(bilgi, plan));

        var donusum = new ConversionPlan { Width = plan.Width, Height = plan.Height };
        var argumanlar = ConversionArguments.Build(bilgi, donusum, "cikti.mp4").ToArray();
        var i = Array.IndexOf(argumanlar, "-vf");
        if (i >= 0) Assert.DoesNotContain(VideoFilterChain.SquarePixelFilter, argumanlar[i + 1]);
    }
}
