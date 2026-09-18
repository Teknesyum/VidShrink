using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// HandBrake acigi madde 54: kucultmede aralik. Pimler fable 2026-09-18 S6'dan;
/// ucu de sabit karsilastirmaz, <b>iliski</b> pimler — girdiyi degistirince ciktinin
/// nasil degismesi gerektigini soyler.
/// </summary>
public class KucultmeAraligiTests
{
    private static MediaInfo Kaynak(double durationSeconds = 600) => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = durationSeconds,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000
    };

    private static EncodePlan Plan(TrimWindow trim, double targetMb = 25)
        => PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = targetMb, Trim = trim });

    private static double Value(IReadOnlyList<string> args, int index)
        => double.Parse(args[index + 1], CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(40.0, 70.0)]
    [InlineData(123.456, 200.0)]
    [InlineData(6.0, 12.0)]
    [InlineData(10.0, 30.0)]
    public void AramaMelezBolunur_IkiSsToplamiIstenenBaslangic(double start, double end)
    {
        var trim = new TrimWindow(start, end);
        var args = FfmpegArguments.Build(Kaynak(), Plan(trim), "cikti.mp4", 0, null);
        var input = args.IndexOf("-i");
        var seeks = args.Select((value, index) => (value, index)).Where(pair => pair.value == "-ss").Select(pair => pair.index).ToList();

        var before = seeks.Where(index => index < input).ToList();
        var after = seeks.Where(index => index > input).ToList();

        Assert.Single(after);
        var total = seeks.Sum(index => Value(args, index));
        Assert.Equal(start, total, 3);
    }

    [Theory]
    [InlineData(40.0, 30.0, 10.0)]
    [InlineData(123.456, 113.456, 10.0)]
    [InlineData(200.0, 190.0, 10.0)]
    [InlineData(10.0, 0.0, 10.0)]
    [InlineData(6.0, 0.0, 6.0)]
    [InlineData(0.0, 0.0, 0.0)]
    public void AramaBolusumu_HizliKisimKareyeKadarOlaniBirakir(double start, double hizli, double kare)
    {
        var trim = new TrimWindow(start, start + 30);
        var args = FfmpegArguments.Build(Kaynak(), Plan(trim), "cikti.mp4", 0, null);
        var input = args.IndexOf("-i");
        var seeks = args.Select((value, index) => (value, index)).Where(pair => pair.value == "-ss").Select(pair => pair.index).ToList();

        var onceki = seeks.Where(index => index < input).Sum(index => Value(args, index));
        var sonraki = seeks.Where(index => index > input).Sum(index => Value(args, index));

        Assert.Equal(hizli, onceki, 3);
        Assert.Equal(kare, sonraki, 3);
    }

    [Fact]
    public void AramaBolusumu_BaslangicKayinca_YalnizHizliKisimBuyur()
    {
        double Sonraki(double start)
        {
            var args = FfmpegArguments.Build(Kaynak(), Plan(new TrimWindow(start, start + 30)), "cikti.mp4", 0, null);
            var input = args.IndexOf("-i");
            return args.Select((value, index) => (value, index))
                .Where(pair => pair.value == "-ss" && pair.index > input)
                .Sum(pair => Value(args, pair.index));
        }

        var yakin = Sonraki(60);
        var uzak = Sonraki(360);
        Assert.Equal(yakin, uzak, 3);
        Assert.Equal(10.0, uzak, 3);
        Assert.Equal(TrimWindow.SeekLeadSeconds, uzak, 3);
    }

    [Theory]
    [InlineData(10.0, 40.0)]
    [InlineData(10.0, 95.5)]
    [InlineData(200.0, 260.0)]
    public void SureBayragi_TFarkaEsit(double start, double end)
    {
        var args = FfmpegArguments.Build(Kaynak(), Plan(new TrimWindow(start, end)), "cikti.mp4", 0, null);
        var duration = args.IndexOf("-t");
        Assert.True(duration > args.IndexOf("-i"));
        Assert.DoesNotContain("-to", args);
        Assert.Equal(end - start, Value(args, duration), 3);
    }

    [Theory]
    [InlineData(8.0, 30.0)]
    [InlineData(8.0, 60.0)]
    [InlineData(3.0, 30.0)]
    [InlineData(25.0, 60.0)]
    public void BitHizi_KesitSuresiyleTersOranli(double targetMb, double shortSeconds)
    {
        var kisa = Plan(new TrimWindow(20, 20 + shortSeconds), targetMb);
        var uzun = Plan(new TrimWindow(20, 20 + shortSeconds * 2), targetMb);

        Assert.Equal(EncodeMode.TwoPass, kisa.ModeEnum);
        Assert.Equal(EncodeMode.TwoPass, uzun.ModeEnum);
        Assert.True(kisa.VideoBitrateK > 0);
        var oran = (double)kisa.VideoBitrateK / uzun.VideoBitrateK;
        Assert.InRange(oran, 1.95, 2.05);
    }

    /// <summary>
    /// Kesit yalnizca bir zaman penceresi degil, <b>butcenin girdisi</b>: 5 Mbit/sn'lik
    /// kaynagin 30 saniyesi 25 MB'a sigar (passthrough), 60 saniyesi sigmaz (iki gecis).
    /// Kaynak baytini kesit oraniyla olceklemeyen bir mutasyon iki satiri da iki gecise
    /// dusurur ve bu pim kirilir.
    /// </summary>
    [Fact]
    public void KaynagaSigan_KesitPassthroughOlur()
    {
        Assert.Equal(EncodeMode.PassThrough, Plan(new TrimWindow(20, 50)).ModeEnum);
        Assert.Equal(EncodeMode.TwoPass, Plan(new TrimWindow(20, 80)).ModeEnum);
        Assert.Equal(EncodeMode.TwoPass, PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = 25 }).ModeEnum);
    }

    [Fact]
    public void KesitYoksa_ArgumanDizisiDegismez()
    {
        var info = Kaynak();
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });
        var args = FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null);

        Assert.Null(plan.Trim);
        Assert.DoesNotContain("-ss", args);
        Assert.DoesNotContain("-t", args);
    }

    [Fact]
    public void KesitteBolumIsaretleriDusurulur()
    {
        var info = Kaynak();
        var kesitli = FfmpegArguments.Build(info, Plan(new TrimWindow(10, 40)), "cikti.mp4", 0, null);
        var tam = FfmpegArguments.Build(info, PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 }), "cikti.mp4", 0, null);

        Assert.Equal("-1", kesitli[kesitli.IndexOf("-map_chapters") + 1]);
        Assert.Equal("0", tam[tam.IndexOf("-map_chapters") + 1]);
        Assert.Equal("0", kesitli[kesitli.IndexOf("-map_metadata") + 1]);
    }

    [Fact]
    public void KaynakKunyesiKesitOraniylaOlceklenir()
    {
        var info = Kaynak(600);
        var kesit = new TrimWindow(100, 250).Apply(info);

        Assert.Equal(150, kesit.DurationSeconds, 3);
        Assert.Equal(info.FileSizeBytes / 4.0, kesit.FileSizeBytes, 0);
        Assert.Equal(info.FileSizeBytes / (double)info.DurationSeconds, kesit.FileSizeBytes / kesit.DurationSeconds, 1);
    }

    [Fact]
    public void OlcumParcasiKesitBaslangicinaGoreKayar()
    {
        var info = Kaynak();
        var plan = Plan(new TrimWindow(120, 180));
        var parca = FfmpegArguments.BuildSegment(info, plan, 5, 2, "parca.mp4");
        var input = parca.IndexOf("-i");
        var seeks = parca.Select((value, index) => (value, index)).Where(pair => pair.value == "-ss").Select(pair => pair.index).ToList();

        Assert.Equal(125, seeks.Sum(index => Value(parca, index)), 3);
        Assert.Equal(2, Value(parca, parca.IndexOf("-t")), 3);
        Assert.True(seeks[0] < input);
    }

    [Theory]
    [InlineData(40.0, 40.0)]
    [InlineData(40.0, 39.0)]
    public void TersKesitReddedilir(double start, double end)
        => Assert.Throws<ArgumentException>(() => TrimWindow.Of(start, end, 600));

    [Fact]
    public void TumVideoKesitSayilmaz()
        => Assert.Null(TrimWindow.Of(null, 600, 600));

    [Fact]
    public void PassthroughKopyasiKesitiTasir()
    {
        var args = FfmpegArguments.BuildTrimCopy(Kaynak(), new TrimWindow(40, 70), "cikti.mp4");
        var input = args.IndexOf("-i");
        var seeks = args.Select((value, index) => (value, index)).Where(pair => pair.value == "-ss").Select(pair => pair.index).ToList();

        Assert.Equal(40, seeks.Sum(index => Value(args, index)), 3);
        Assert.Single(seeks, index => index > input);
        Assert.Equal(30, Value(args, args.IndexOf("-t")), 3);
        Assert.Equal("copy", args[args.IndexOf("-c") + 1]);
        Assert.Equal("-1", args[args.IndexOf("-map_chapters") + 1]);
        Assert.DoesNotContain("-crf", args);
    }

    [Theory]
    [InlineData(6.0)]
    [InlineData(30.0)]
    [InlineData(120.0)]
    public void DuzeltmeKesitSuresiyleHesaplanir(double kesitSuresi)
    {
        var kaynak = Kaynak();
        var kesitli = Plan(new TrimWindow(20, 20 + kesitSuresi));
        var tam = PlanCalculator.Build(kaynak, new PlanOptions { TargetMb = 25 });

        var args = FfmpegArguments.Build(kaynak, kesitli, "cikti.mp4", 0, null);
        Assert.Equal(Value(args, args.IndexOf("-t")), kesitli.EffectiveDurationSeconds(kaynak.DurationSeconds), 3);
        Assert.Equal(kaynak.DurationSeconds, tam.EffectiveDurationSeconds(kaynak.DurationSeconds), 3);

        var kesitDuzeltme = PlanCalculator.Correct(kesitli, 30, 25, kesitli.EffectiveDurationSeconds(kaynak.DurationSeconds));
        var kaynakSuresiyle = PlanCalculator.Correct(kesitli, 30, 25, kaynak.DurationSeconds);
        Assert.True(kesitDuzeltme.VideoBitrateK > kaynakSuresiyle.VideoBitrateK,
            $"kesit duzeltmesi {kesitDuzeltme.VideoBitrateK}k, kaynak suresiyle {kaynakSuresiyle.VideoBitrateK}k");
    }

    [Theory]
    [InlineData(25.5, 25.0)]
    [InlineData(10.2, 10.0)]
    public void KullaniciKesitiVarken_TasmaKirpmasiOnerilmez(double gerceklesen, double hedef)
    {
        var kesitli = Plan(new TrimWindow(20, 80), hedef);
        var tam = PlanCalculator.Build(Kaynak(), new PlanOptions { TargetMb = hedef });

        Assert.True(OvershootTrim.Offered(tam, gerceklesen, hedef));
        Assert.False(OvershootTrim.Offered(kesitli, gerceklesen, hedef));
    }

    [Theory]
    [InlineData(9999.0)]
    [InlineData(601.0)]
    [InlineData(600.0)]
    public void KaynagiAsanSonUcKaynagaKirpilir(double end)
    {
        var kirpilan = TrimWindow.Of(10, end, 600)!;

        Assert.Equal(10.0, kirpilan.StartSeconds, 3);
        Assert.Equal(600.0, kirpilan.EndSeconds, 3);
        Assert.Equal(590.0, kirpilan.DurationSeconds, 3);
    }

    /// <summary>
    /// Kirpma yalniz <b>asan</b> uca dokunur: kaynagin icinde kalan bir son oldugu gibi
    /// durur. Ust siniri kaynak suresine sabitleyen bir mutasyon burada kirilir.
    /// </summary>
    [Fact]
    public void KaynaginIcindekiSonKirpilmaz()
    {
        var pencere = TrimWindow.Of(10, 200, 600)!;

        Assert.Equal(10.0, pencere.StartSeconds, 3);
        Assert.Equal(200.0, pencere.EndSeconds, 3);
        Assert.Equal(190.0, pencere.DurationSeconds, 3);
    }
}

/// <summary>
/// Kesit passthrough'a dustugunde motorun kaynagi <b>kopyalamamasi</b> gerekir; kopya
/// araligi kaybettirir ve iki ayri pencere ayni dosyayi verir. Pim tam bunu olcer: iki
/// pencere, iki ayri sure. Dosya kopyasina donen bir mutasyon ikisini de kaynak suresine
/// esitler ve kirilir.
/// </summary>
public class KucultmeAraligiPassthroughTests
{
    [FfmpegFact]
    public async Task KesitPassthroughtaKopyalanmaz_PencereSureyiBelirler()
    {
        var klasor = Path.Combine(".calisma", "kesit-passthrough", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynakYolu = Path.Combine(klasor, "kaynak.mp4");
            await Uret(kaynakYolu);
            var kaynak = await FfprobeClient.ProbeAsync(kaynakYolu);

            var kisa = await Teslim(kaynak, new TrimWindow(1, 3), Path.Combine(klasor, "kisa.mp4"));
            var uzun = await Teslim(kaynak, new TrimWindow(1, 5), Path.Combine(klasor, "uzun.mp4"));

            Assert.Equal(EncodeMode.PassThrough, kisa.PlanUsed.ModeEnum);
            Assert.Equal(EncodeMode.PassThrough, uzun.PlanUsed.ModeEnum);

            var kisaSure = (await FfprobeClient.ProbeAsync(kisa.OutputPath!)).DurationSeconds;
            var uzunSure = (await FfprobeClient.ProbeAsync(uzun.OutputPath!)).DurationSeconds;

            Assert.True(kisaSure < uzunSure, $"kisa {kisaSure} uzun {uzunSure} degil");
            Assert.True(uzunSure < kaynak.DurationSeconds, $"uzun {uzunSure} kaynak {kaynak.DurationSeconds}");
            Assert.Equal(2, uzunSure - kisaSure, 0);
        }
        finally
        {
            try { Directory.Delete(klasor, recursive: true); } catch { }
        }
    }

    private static async Task<EncodeResult> Teslim(MediaInfo kaynak, TrimWindow kesit, string cikti)
    {
        var plan = PlanCalculator.Build(kaynak, new PlanOptions { TargetMb = 50, Trim = kesit });
        return await new EncodeRunner().RunAsync(kaynak, plan, cikti, 50, null, CancellationToken.None);
    }

    private static async Task Uret(string yol)
    {
        var baslangic = new System.Diagnostics.ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-y",
                     "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=15:duration=8",
                     "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-pix_fmt", "yuv420p",
                     "-an", yol
                 })
            baslangic.ArgumentList.Add(arg);

        using var surec = System.Diagnostics.Process.Start(baslangic)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var cikti = surec.StandardOutput.ReadToEndAsync();
        await surec.WaitForExitAsync();
        await Task.WhenAll(hata, cikti);
        Assert.True(File.Exists(yol), await hata);
    }
}

public class KucultmeAraligiCliTests
{
    private static VidShrink.Cli.CliRequest Parse(params string[] args)
    {
        var result = VidShrink.Cli.CliParser.Parse(args);
        Assert.True(result.Ok, result.ErrorKey);
        return result.Request!;
    }

    [Theory]
    [InlineData("10-40", 10.0, 40.0)]
    [InlineData("0:10-0:40", 10.0, 40.0)]
    [InlineData("1:02:03-1:02:13", 3723.0, 3733.0)]
    [InlineData("12.5-20", 12.5, 20.0)]
    public void KesBayragiUclariCozer(string value, double start, double end)
    {
        var request = Parse("kucult", "a.mp4", "--hedef", "25MB", "--kes", value);
        Assert.Equal(start, request.TrimStartSeconds!.Value, 3);
        Assert.Equal(end, request.TrimEndSeconds!.Value, 3);
        Assert.Equal(end - start, request.ToPlanOptions(25, 7200).Trim!.DurationSeconds, 3);
    }

    [Fact]
    public void AcikUcKaynakSonunaKadar()
    {
        var request = Parse("kucult", "a.mp4", "--hedef", "25MB", "--kes", "90-");
        Assert.Null(request.TrimEndSeconds);
        Assert.Equal(510, request.ToPlanOptions(25, 600).Trim!.DurationSeconds, 3);
    }

    [Theory]
    [InlineData("40-10")]
    [InlineData("10")]
    [InlineData("10-20-30")]
    [InlineData("-20")]
    public void BozukKesitKullanimHatasi(string value)
    {
        var result = VidShrink.Cli.CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--kes", value });
        Assert.False(result.Ok);
    }

    [Fact]
    public void KesVerilmezseKesitYok()
        => Assert.Null(Parse("kucult", "a.mp4", "--hedef", "25MB").ToPlanOptions(25, 600).Trim);

    [Theory]
    [InlineData("--kes")]
    [InlineData("--cut")]
    public void IzleKesitiKabulEtmez(string bayrak)
    {
        var result = VidShrink.Cli.CliParser.Parse(new[] { "izle", "klasor", "--cikti", "hedef", "--hedef", "25MB", bayrak, "10-40" });
        Assert.False(result.Ok);
        Assert.Equal("error.unknown-option", result.ErrorKey);

        var kucult = VidShrink.Cli.CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", bayrak, "10-40" });
        Assert.True(kucult.Ok, kucult.ErrorKey);
    }
}
