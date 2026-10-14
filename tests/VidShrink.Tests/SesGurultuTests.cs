using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// ARC-P1: kaydedicinin gürültü süzgeci (<c>afftdn</c>) Küçült'ün ses zincirine ve <c>--ses-temizle</c>
/// bayrağına bağlanır; kaynağın yüksekliği <c>ebur128</c> ile bir kez ölçülüp LUFS ve gerçek tepe olarak okunur.
/// Süzgeç yalnız istenince yazılır, istenince ses kopyalanmaz. Canlı kol gürültü tabanını ffmpeg'in kendi ölçeriyle okur.
/// </summary>
public sealed class SesGurultuTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 44100);

    private static StreamPlan Karar(StreamRequest istek, string kodek = "aac")
        => StreamMapping.Decide(Kaynak(600, Video, Ses), istek, OutputContainer.Mp4, 160, null, kodek, true, 100);

    /// <summary>Gürültü süzgeci hizalamadan hemen sonra, kazanç ve normalleştirmeden önce durur.</summary>
    [Fact]
    public void ZincirSirasi()
    {
        Assert.Equal("aresample=async=1:first_pts=0,afftdn",
            StreamMapping.AudioFilter(new StreamRequest(AudioDenoise: true), 44100));
        Assert.Equal("aresample=async=1:first_pts=0,afftdn,volume=-6dB,loudnorm=I=-24:LRA=7:TP=-2,aresample=44100",
            StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true, AudioGainDb: -6, AudioDenoise: true), 44100));
        Assert.Equal("afftdn", AudioCaptureArguments.NoiseSuppressionFilterName);
    }

    /// <summary>Olumsuz kontrol: kutu kapalıyken süzgeç hiçbir zincire yazılmaz.</summary>
    [Fact]
    public void KapaliykenYazilmaz()
    {
        Assert.False(StreamRequest.Default.FiltersAudio);
        Assert.True(new StreamRequest(AudioDenoise: true).FiltersAudio);
        Assert.DoesNotContain("afftdn", StreamMapping.AudioFilter(StreamRequest.Default, 44100));
        Assert.DoesNotContain("afftdn", StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true, AudioGainDb: 3), 44100));
    }

    /// <summary>Kutu açıkken kopyalanabilir aac kodlanır ve <c>-filter:a</c>'da <c>afftdn</c> geçer; kapalıyken argümanda yok.</summary>
    [Fact]
    public void AcikkenArgumandaGecer()
    {
        var acik = Karar(new StreamRequest(AudioDenoise: true));
        var kapali = Karar(StreamRequest.Default);

        Assert.False(acik.Audio[0].Copies);
        Assert.Contains("afftdn", Sonraki(acik.OutputArguments(), "-filter:a"));
        Assert.True(kapali.Audio[0].Copies);
        Assert.DoesNotContain(kapali.OutputArguments(), arg => arg.Contains("afftdn", StringComparison.Ordinal));
    }

    /// <summary>Kopya zorunluysa süzgeç kurulamaz: not düşer, argümanda süzgeç yok.</summary>
    [Fact]
    public void KopyaZorunlukenNotDusuyor()
    {
        var plan = Karar(new StreamRequest(AudioDenoise: true), "copy");

        Assert.True(plan.Audio[0].Copies);
        Assert.Contains(StreamNote.AudioFilterSkippedOnCopy, plan.Notes);
        Assert.DoesNotContain("-filter:a", plan.OutputArguments());
    }

    /// <summary>Plan isteği taşıyor: seçenek plana, oradan kap değişince de süzgece geçiyor; kapalıyken geçmiyor.</summary>
    [Fact]
    public void PlanIstegiTasiyor()
    {
        var info = Kaynak(600, Video, Ses);
        var acik = PlanCalculator.Build(info, new PlanOptions { TargetMb = 100, AudioDenoise = true });
        var kapali = PlanCalculator.Build(info, new PlanOptions { TargetMb = 100 });

        Assert.True(acik.Streams!.Request.AudioDenoise);
        Assert.Contains("afftdn", StreamMapping.ForOutput(info, acik, "cikti.mkv").Audio[0].Filter);
        Assert.False(kapali.Streams!.Request.AudioDenoise);
        Assert.DoesNotContain("afftdn", StreamMapping.ForOutput(info, kapali, "cikti.mkv").Audio[0].Filter ?? "");
    }

    /// <summary>CLI: iki yazım da seçeneği açar, bayraksız kapalı kalır; izle tek dosya seçeneğini reddeder.</summary>
    [Theory]
    [InlineData("--ses-temizle", true)]
    [InlineData("--denoise", true)]
    [InlineData(null, false)]
    public void CliBayragi(string? bayrak, bool beklenen)
    {
        var args = new List<string> { "plan", "a.mp4", "--hedef", "25" };
        if (bayrak is not null) args.Add(bayrak);
        var parsed = CliParser.Parse(args);

        Assert.True(parsed.Ok);
        Assert.Equal(beklenen, parsed.Request!.ToPlanOptions(10).AudioDenoise);
    }

    [Fact]
    public void IzleBayragiReddeder()
    {
        var parsed = CliParser.Parse(new[] { "izle", "gelen", "--cikti", "giden", "--hedef", "25MB", "--ses-temizle" });
        Assert.Equal("error.not-in-watch", parsed.ErrorKey);
        Assert.Contains("--ses-temizle", CliParser.NotInWatch);
        Assert.Contains("--denoise", CliParser.NotInWatch);
    }

    private const string Ozet = """
        [Parsed_ebur128_0 @ 0000022d7425a340] Summary:

          Integrated loudness:
            I:         -32.2 LUFS
            Threshold: -42.2 LUFS

          Loudness range:
            LRA:        20.0 LU
            Threshold: -52.2 LUFS
            LRA low:   -52.2 LUFS
            LRA high:  -32.2 LUFS

          True peak:
            Peak:      -28.5 dBFS
        """;

    /// <summary>Özet LUFS ve gerçek tepeyi verir; ara satırlardaki <c>I:</c> değil özetteki okunur.</summary>
    [Fact]
    public void OzetOkunur()
    {
        var ara = "[Parsed_ebur128_0 @ 1] t: 0.4  TARGET:-23 LUFS    M: -40.1 S:-120.7     I: -40.1 LUFS       LRA:   0.0 LU  FTPK: -30.0 dBFS  TPK: -30.0 dBFS\n";
        var rapor = LoudnessReport.Parse(ara + Ozet);

        Assert.NotNull(rapor);
        Assert.Equal(-32.2, rapor!.IntegratedLufs);
        Assert.Equal(-28.5, rapor.TruePeakDbfs);
    }

    /// <summary>Sessiz kaynakta tepe <c>-inf</c>: değer yok, tümleşik yükseklik yine okunur. Özetsiz metin rapor vermez.</summary>
    [Fact]
    public void SessizVeOzetsiz()
    {
        var sessiz = LoudnessReport.Parse(Ozet.Replace("-32.2 LUFS", "-70.0 LUFS").Replace("-28.5 dBFS", "-inf dBFS"));
        Assert.NotNull(sessiz);
        Assert.Equal(-70.0, sessiz!.IntegratedLufs);
        Assert.Null(sessiz.TruePeakDbfs);

        Assert.Null(LoudnessReport.Parse("Stream #0:0: Audio: aac\n"));
        Assert.Null(LoudnessReport.Parse(""));
    }

    /// <summary>Ölçüm tek ses izini çözer, yalnız iki iş parçacığıyla, çıktısız.</summary>
    [Fact]
    public void OlcumArgumanlari()
    {
        var args = LoudnessReport.Arguments("C:\\kaynak\\film.mkv");

        Assert.Equal("2", Sonraki(args, "-threads"));
        Assert.Equal("C:\\kaynak\\film.mkv", Sonraki(args, "-i"));
        Assert.Equal("0:a:0", Sonraki(args, "-map"));
        Assert.Equal("ebur128=peak=true:framelog=quiet", Sonraki(args, "-af"));
        Assert.Equal("null", Sonraki(args, "-f"));
        Assert.Contains("-vn", args);
    }

    /// <summary>
    /// Canlı kol: ilk 1,5 sn ton, sonrası yalnız beyaz gürültü (~-51 dBFS). Kutu açık çıktının gürültü
    /// tabanı (2. saniyeden sonra ortalama düzey) kutusuz çıktıya göre en az 6 dB düşüyor; ton bölümü
    /// 1,5 dB içinde kalıyor (süzgeç sesi kesmiyor). Ölçüm kolu aynı kaynağın yüksekliğini okuyor.
    /// </summary>
    [Fact]
    public async Task CanliGurultuTabani()
    {
        var kaynak = Yol("gurultu-kaynak.mkv");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=3,noise=alls=60:allf=t",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3,volume=0:enable='gte(t,1.5)'",
            "-f", "lavfi", "-i", "anoisesrc=d=3:c=white:a=0.005:r=48000",
            "-filter_complex", "[1:a][2:a]amix=inputs=2:normalize=0[a]",
            "-map", "0:v", "-map", "[a]", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", "aac", "-b:a", "192k", kaynak
        });

        await KodlaAsync(kaynak, Yol("gurultu-duz.mp4"), _ => { });
        await KodlaAsync(kaynak, Yol("gurultu-temiz.mp4"), options => options.AudioDenoise = true);

        var duzTaban = await DuzeyAsync(Yol("gurultu-duz.mp4"), "-ss", "2");
        var temizTaban = await DuzeyAsync(Yol("gurultu-temiz.mp4"), "-ss", "2");
        var duzTon = await DuzeyAsync(Yol("gurultu-duz.mp4"), "-t", "1");
        var temizTon = await DuzeyAsync(Yol("gurultu-temiz.mp4"), "-t", "1");

        Assert.True(duzTaban - temizTaban >= 6, $"gürültü tabanı {duzTaban} -> {temizTaban} dB");
        Assert.InRange(Math.Abs(duzTon - temizTon), 0, 1.5);

        var rapor = await LoudnessProbe.RunAsync(kaynak);
        Assert.NotNull(rapor);
        Assert.InRange(rapor!.IntegratedLufs, -40, -15);
        Assert.NotNull(rapor.TruePeakDbfs);
        Assert.InRange(rapor.TruePeakDbfs!.Value, -30, 0);

        Kapat("gurultu-*");
    }

    private static async Task<double> DuzeyAsync(string yol, string kesBayragi, string kesDegeri)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-nostats", "-threads", "2", kesBayragi, kesDegeri, "-i", yol, "-map", "0:a:0", "-af", "volumedetect", "-f", "null", "-" });
        Assert.Equal(0, sonuc.Code);
        var eslesme = Regex.Match(sonuc.Err, @"mean_volume: (-?\d+(?:\.\d+)?) dB");
        Assert.True(eslesme.Success, sonuc.Err);
        return double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
