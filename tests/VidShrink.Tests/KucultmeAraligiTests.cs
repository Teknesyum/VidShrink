using System.Globalization;
using VidShrink.Core;
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
        Assert.Equal(start > TrimWindow.SeekLeadSeconds ? 1 : 0, before.Count);
        var total = seeks.Sum(index => Value(args, index));
        Assert.Equal(start, total, 3);
        if (before.Count == 1) Assert.True(Value(args, after[0]) <= TrimWindow.SeekLeadSeconds);
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
}
