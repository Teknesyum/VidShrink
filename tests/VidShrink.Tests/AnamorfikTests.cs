using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _cikti;

    public AnamorfikTests(ITestOutputHelper cikti) => _cikti = cikti;

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

    private readonly record struct HamOlcum(int Width, int Height, string Sar, string Dar)
    {
        public double GosterimOrani()
        {
            var sarParca = Sar.Split(':');
            var sarPay = double.Parse(sarParca[0], CultureInfo.InvariantCulture);
            var sarPayda = double.Parse(sarParca[1], CultureInfo.InvariantCulture);
            return Width * (sarPay / sarPayda) / Height;
        }
    }

    /// <summary>
    /// Cikti dosyasini ffprobe'un kendi alanlariyla okur: <c>sample_aspect_ratio</c> ve
    /// <c>display_aspect_ratio</c> <see cref="FfprobeClient"/> hic parcalamiyor, HB #40
    /// olcumu ham degeri ister.
    /// </summary>
    private static HamOlcum HamProbe(string yol)
    {
        using var process = new Process
        {
            StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, new[]
            {
                "-v", "error", "-select_streams", "v:0",
                "-show_entries", "stream=width,height,sample_aspect_ratio,display_aspect_ratio",
                "-of", "json", yol
            })
        };
        process.Start();
        var cikti = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"ffprobe basarisiz: {yol}");

        using var belge = JsonDocument.Parse(cikti);
        var akis = belge.RootElement.GetProperty("streams")[0];
        return new HamOlcum(
            akis.GetProperty("width").GetInt32(),
            akis.GetProperty("height").GetInt32(),
            akis.TryGetProperty("sample_aspect_ratio", out var sar) ? sar.GetString() ?? "1:1" : "1:1",
            akis.TryGetProperty("display_aspect_ratio", out var dar) ? dar.GetString() ?? "0:1" : "0:1");
    }

    private static void FfmpegCalistir(IReadOnlyList<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args.ToArray()) };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"kodlama basarisiz: {stderr}");
    }

    /// <summary>
    /// HandBrake acigi #40: anamorfik kaynak kucultulunce gosterim orani (DAR) korunuyor
    /// mu. Gercek kodlama, gercek ffprobe: <see cref="VideoFilterChain.SquarePixelFilter"/>
    /// varligini kontrol eden birim testleri (yukarida) bunu olcmez, yalniz zincirde dizgeyi
    /// arar. Burada urunun gercek yolu (<see cref="ConversionArguments.Build"/>) hem
    /// olceklenen hem olceklenmeyen kolda calistirilip cikti dosyasi okunur.
    ///
    /// <para>720x480 SAR 32:27 (DVD NTSC genis ekran) ve 720x576 SAR 64:45 (DVD PAL genis
    /// ekran) ikisi de gorunumde 16:9; kare piksel 640x480 kontrol kolu. Ham sayilar
    /// <c>docs/olcumler/anamorfik-olcekleme.md</c>'de.</para>
    /// </summary>
    [FfmpegFact]
    public async Task KucultulmusAnamorfikCiktiGosterimOraniniKoruyor()
    {
        var klasor = Klasor();
        try
        {
            var kaynaklar = new (string Ad, string Olcu, string? Sar, double BeklenenDar)[]
            {
                ("dvd-ntsc", "720x480", "32/27", 16.0 / 9),
                ("dvd-pal", "720x576", "64/45", 16.0 / 9),
                ("kare-kontrol", "640x480", null, 640.0 / 480),
            };

            foreach (var (ad, olcu, sar, beklenenDar) in kaynaklar)
            {
                var kaynakYol = Uret(klasor, $"{ad}.mp4", olcu, sar);
                var kaynak = await FfprobeClient.ProbeAsync(kaynakYol);

                // Gercek urun yolu: PlanCalculator gosterim genisliginden merdiven kurar
                // (docs/olcumler/anamorfik-olcekleme.md). Boyutu kucultme kolunu elle de
                // tetikleyip ConversionArguments'in "-vf" iceren dalindan gecmek icin
                // gosterim genisliginin yarisi hedef aliniyor.
                var hedefGenislik = Olcek.Modul((int)Math.Round(kaynak.DisplayWidth * 0.5), 2);
                var hedefYukseklik = Olcek.Modul((int)Math.Round(kaynak.Height * 0.5), 2);
                Assert.True(hedefGenislik != kaynak.Width || hedefYukseklik != kaynak.Height,
                    $"{ad}: yari cozunurluk kaynakla ayni cikti");

                var kucukPlan = PlanCalculator.Build(kaynak, new PlanOptions { TargetMb = 1 });
                _cikti.WriteLine($"{ad}: PlanCalculator.Build -> {kucukPlan.Width}x{kucukPlan.Height} (kaynak {kaynak.Width}x{kaynak.Height}, gosterim {kaynak.DisplayWidth})");

                var olceklenmisCikti = Path.Combine(klasor, $"{ad}-olcekli.mp4");
                var olceklenmisArgs = ConversionArguments.Build(
                    kaynak, new ConversionPlan { Width = hedefGenislik, Height = hedefYukseklik }, olceklenmisCikti);
                FfmpegCalistir(olceklenmisArgs);
                var olceklenmisOlcum = HamProbe(olceklenmisCikti);

                var olceksizCikti = Path.Combine(klasor, $"{ad}-olceksiz.mp4");
                var olceksizArgs = ConversionArguments.Build(kaynak, new ConversionPlan(), olceksizCikti);
                FfmpegCalistir(olceksizArgs);
                var olceksizOlcum = HamProbe(olceksizCikti);

                var yukseklikSadeceCikti = Path.Combine(klasor, $"{ad}-yukseklik.mp4");
                var yukseklikSadeceArgs = ConversionArguments.Build(
                    kaynak, new ConversionPlan { Height = hedefYukseklik }, yukseklikSadeceCikti);
                FfmpegCalistir(yukseklikSadeceArgs);
                var yukseklikSadeceOlcum = HamProbe(yukseklikSadeceCikti);

                _cikti.WriteLine(
                    $"{ad}: olcekli {olceklenmisOlcum.Width}x{olceklenmisOlcum.Height} sar={olceklenmisOlcum.Sar} " +
                    $"dar={olceklenmisOlcum.Dar} oran={olceklenmisOlcum.GosterimOrani():0.###} | " +
                    $"olceksiz {olceksizOlcum.Width}x{olceksizOlcum.Height} sar={olceksizOlcum.Sar} " +
                    $"dar={olceksizOlcum.Dar} oran={olceksizOlcum.GosterimOrani():0.###} | " +
                    $"yukseklik-sadece {yukseklikSadeceOlcum.Width}x{yukseklikSadeceOlcum.Height} sar={yukseklikSadeceOlcum.Sar} " +
                    $"dar={yukseklikSadeceOlcum.Dar} oran={yukseklikSadeceOlcum.GosterimOrani():0.###} | beklenen={beklenenDar:0.###}");

                Assert.InRange(olceklenmisOlcum.GosterimOrani(), beklenenDar - 0.02, beklenenDar + 0.02);
                Assert.InRange(olceksizOlcum.GosterimOrani(), beklenenDar - 0.02, beklenenDar + 0.02);
                Assert.InRange(yukseklikSadeceOlcum.GosterimOrani(), beklenenDar - 0.02, beklenenDar + 0.02);
            }
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
