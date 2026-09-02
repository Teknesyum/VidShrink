using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public class VmafPoolingTests
{
    private const int SourceFrames = 3624;
    private const double CleanScore = 95.0;

    private static List<double> Series(int count, double value, params double[] injected)
    {
        var scores = Enumerable.Repeat(value, count).ToList();
        for (var i = 0; i < injected.Length; i++) scores[100 + i * 37] = injected[i];
        return scores;
    }

    private static double HarmonicByDefinition(IEnumerable<double> scores)
    {
        var list = scores.ToList();
        return list.Count / list.Sum(x => 1.0 / x);
    }

    [Fact]
    public void TabanAltiKareYokken_HarmonikOrtalama_TanimaBirebirUyar()
    {
        var scores = Series(SourceFrames, CleanScore, 92.4, 99.7, 88.1, 74.7);

        var pool = VmafPooling.Pool(scores);

        Assert.Equal(0, pool.FloorClampedFrames);
        Assert.False(pool.Suspect);
        Assert.Equal(HarmonicByDefinition(scores), pool.Harmonic!.Value, 9);
        Assert.Equal(74.7, pool.Min!.Value, 9);
    }

    [Fact]
    public void TabanAltiKareler_Sayilir_VeEnDusukDegerKelepcelenmedenRaporlanir()
    {
        var scores = Series(SourceFrames, CleanScore, 0.0, 0.132882, 0.945848, 1.0, 12.378497);

        var pool = VmafPooling.Pool(scores);

        Assert.Equal(3, pool.FloorClampedFrames);
        Assert.True(pool.Suspect);
        Assert.Equal(0.0, pool.Min!.Value);
    }

    [Fact]
    public void SifirIleTabanAltiKucukDeger_HarmonikOrtalamada_AyniKefeyeKonur()
    {
        var sifirli = Series(SourceFrames, CleanScore, 0.0);
        var neredeyseSifirli = Series(SourceFrames, CleanScore, 0.132882);

        var a = VmafPooling.Pool(sifirli);
        var b = VmafPooling.Pool(neredeyseSifirli);

        Assert.Equal(a.Harmonic!.Value, b.Harmonic!.Value, 12);
        Assert.Equal(a.FloorClampedFrames, b.FloorClampedFrames);
        Assert.NotEqual(a.Min!.Value, b.Min!.Value);
        Assert.NotEqual(a.Mean!.Value, b.Mean!.Value);
    }

    [Fact]
    public void TekBirSifir_HarmonikOrtalamayi_OrtalamadanKatKatFazlaDusurur()
    {
        var temiz = VmafPooling.Pool(Series(SourceFrames, CleanScore));
        var tekSifir = VmafPooling.Pool(Series(SourceFrames, CleanScore, 0.0));

        var ortalamaDusus = temiz.Mean!.Value - tekSifir.Mean!.Value;
        var harmonikDusus = temiz.Harmonic!.Value - tekSifir.Harmonic!.Value;

        Assert.True(ortalamaDusus < 0.1, $"ortalama dususu {ortalamaDusus}");
        Assert.True(harmonikDusus > 20 * ortalamaDusus, $"harmonik {harmonikDusus}, ortalama {ortalamaDusus}");
    }

    [Fact]
    public void TabanAltiKareSayisiArttikca_HarmonikOrtalama_TekYonluDuser()
    {
        var oncekiHarmonik = double.PositiveInfinity;
        var oncekiKelepce = -1;

        foreach (var sifirSayisi in new[] { 0, 1, 5, 25, 26 })
        {
            var pool = VmafPooling.Pool(Series(SourceFrames, CleanScore, Enumerable.Repeat(0.0, sifirSayisi).ToArray()));

            Assert.Equal(sifirSayisi, pool.FloorClampedFrames);
            Assert.True(pool.Harmonic!.Value < oncekiHarmonik,
                $"{sifirSayisi} sifirda harmonik {pool.Harmonic.Value}, oncekinde {oncekiHarmonik}");
            Assert.True(pool.FloorClampedFrames > oncekiKelepce);

            oncekiHarmonik = pool.Harmonic.Value;
            oncekiKelepce = pool.FloorClampedFrames;
        }
    }

    [Fact]
    public void SabitDizide_UcIstatistikDe_AyniDegeriVerir()
    {
        var pool = VmafPooling.Pool(Series(600, 91.25));

        Assert.Equal(91.25, pool.Mean!.Value, 9);
        Assert.Equal(91.25, pool.Harmonic!.Value, 9);
        Assert.Equal(91.25, pool.P10!.Value, 9);
        Assert.Equal(91.25, pool.Min!.Value, 9);
        Assert.Equal(0, pool.FloorClampedFrames);
    }

    [Fact]
    public void BosDizi_OlcumYokSayilir_SifirDegilNullDoner()
    {
        var pool = VmafPooling.Pool(Array.Empty<double>());

        Assert.Equal(0, pool.Count);
        Assert.Null(pool.Mean);
        Assert.Null(pool.Harmonic);
        Assert.Null(pool.P10);
        Assert.Null(pool.Min);
    }

    [Fact]
    public void NaNIcerenDizi_SessizceYutulmaz()
    {
        var scores = Series(120, CleanScore, double.NaN);

        Assert.Throws<ArgumentException>(() => VmafPooling.Pool(scores));
    }

    [Fact]
    public void OlcumFiltresi_IkiGirdiyiDe_KareIndeksineKilitler()
    {
        var graph = MeasureFilterGraph.Build(1920, 1080, "libvmaf=model=version=vmaf_v0.6.1neg");

        var branches = graph.Split(';');
        var test = Assert.Single(branches, b => b.StartsWith("[0:v]"));
        var reference = Assert.Single(branches, b => b.StartsWith("[1:v]"));

        Assert.EndsWith("[t]", test);
        Assert.EndsWith("[r]", reference);
        Assert.Contains("[t][r]libvmaf=", graph);
    }

    [Fact]
    public void OlcumFiltresi_KareKilidi_OlceklemedenSonraGelir()
    {
        var graph = MeasureFilterGraph.Build(1280, 720, "psnr");

        var test = graph.Split(';').Single(b => b.StartsWith("[0:v]"));

        Assert.True(
            test.IndexOf("scale=", StringComparison.Ordinal) < test.IndexOf("setpts", StringComparison.Ordinal),
            test);
    }

    [FfmpegFact]
    public async Task KareKilidi_AltKareKaymasinaRagmen_KareleriDogruEsler()
    {
        var frames = await OlcumSerisiAsync(MeasureFilterGraph.Build(160, 120, "psnr=stats_file=psnr.log"));

        Assert.Equal(BeklenenKareSayisi, frames.Count);
        Assert.All(frames, y => Assert.True(y >= TamEslesmeEsigi,
            $"kaynak kendisiyle karsilastirildi ama tam eslesmedi; en dusuk psnr_y={frames.Min():0.##} dB"));
    }

    [FfmpegFact]
    public async Task KareKilidiOlmadan_AltKareKaymasi_SkorDizisiniCokertir()
    {
        var kilitsiz = $"[0:v]scale=w=160:h=120:flags=lanczos[t];[1:v]null[r];[t][r]psnr=stats_file=psnr.log";

        var frames = await OlcumSerisiAsync(kilitsiz);

        Assert.True(frames.Min() < TamEslesmeEsigi,
            $"kare kilidi olmadan da tam eslesme cikti: olcu {KaymaSaniye}s'lik kaymaya duyarsiz");
    }

    [FfmpegFact]
    public async Task OlcumSerisi_KareKilidi_GoreceliKaydirilinca_Bozulur()
    {
        var kaydirilmis =
            "[0:v]scale=w=160:h=120:flags=lanczos,settb=AVTB,setpts=N+1[t];" +
            "[1:v]settb=AVTB,setpts=N[r];" +
            "[t][r]psnr=stats_file=psnr.log";

        Assert.NotEqual(MeasureFilterGraph.Build(160, 120, "psnr=stats_file=psnr.log"), kaydirilmis);

        var frames = await OlcumSerisiAsync(kaydirilmis);

        Assert.True(frames.Min() < TamEslesmeEsigi,
            $"iki akis birbirine gore bir kare kaydirildigi halde olcu tam eslesme veriyor: " +
            $"en dusuk psnr_y={frames.Min():0.##} dB");
    }

    [Fact]
    public void KodekTercihi_AutoIleCompatible_AgresifHedefte_FarkliKodlayiciSecer()
    {
        var info = OyunKaydi();
        var profil = OlculenProfil();

        var uyumlu = Kodek(info, profil, CodecPreference.Compatible);
        var oto = Kodek(info, profil, CodecPreference.Auto);

        Assert.NotEqual(uyumlu, oto);
    }

    [Fact]
    public void KodekTercihi_PlanaGercektenGecer_VarsayilaninaDusmez()
    {
        var info = OyunKaydi();
        var profil = OlculenProfil();

        var secilen = Enum.GetValues<CodecPreference>()
            .ToDictionary(p => p, p => Kodek(info, profil, p));

        Assert.True(secilen.Values.Distinct().Count() > 1,
            "butun kodek tercihleri ayni kodlayiciyi verdi: tercih plana gecmiyor olabilir");
    }

    private static string Kodek(MediaInfo info, ComplexityProfile profil, CodecPreference tercih)
        => PlanCalculator.BuildDetailed(
            info,
            new PlanOptions { TargetMb = 1.5, Codec = tercih, FillPolicy = FillPolicy.FillTarget },
            profil,
            EncoderCapabilities.Instance).Plan.Codec;

    private static MediaInfo OyunKaydi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 830L * 1024 * 1024,
        DurationSeconds = 52.6,
        Width = 1920,
        Height = 1080,
        Fps = 48.0,
        VideoCodec = "h264",
        TotalBitrateBps = 132_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 160_000,
        AudioChannels = 2
    };

    private static ComplexityProfile OlculenProfil() => new()
    {
        ReferenceBppf = 0.1264,
        Measured = true,
        MotionExponent = 0.55,
        MotionMeasured = true,
        DetailExponent = 0.55,
        SampledSeconds = 6,
        SampledFrames = 288
    };

    private const int BeklenenKareSayisi = 60;
    private const double TamEslesmeEsigi = 80.0;
    private const string KaymaSaniye = "0.004";

    private static async Task<IReadOnlyList<double>> OlcumSerisiAsync(string graph)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_kilit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var kaynak = Path.Combine(dir, "kaynak.mkv");

            await FfmpegAsync(dir, "-v", "error", "-y", "-nostdin", "-f", "lavfi",
                "-i", $"testsrc2=size=160x120:rate=30:duration={BeklenenKareSayisi / 30.0:0.###}",
                "-c:v", "ffv1", kaynak);

            await FfmpegAsync(dir, "-v", "error", "-y", "-nostdin",
                "-i", kaynak,
                "-itsoffset", KaymaSaniye, "-i", kaynak,
                "-lavfi", graph, "-f", "null", "-");

            var log = Path.Combine(dir, "psnr.log");
            Assert.True(File.Exists(log), "psnr gunlugu uretilmedi");

            var values = new List<double>();
            foreach (var line in await File.ReadAllLinesAsync(log))
            {
                var m = Regex.Match(line, @"psnr_y:(inf|[0-9.]+)");
                if (m.Success)
                    values.Add(m.Groups[1].Value == "inf"
                        ? double.PositiveInfinity
                        : double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            return values;
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task FfmpegAsync(string workingDirectory, params string[] args)
    {
        var psi = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args);
        psi.WorkingDirectory = workingDirectory;
        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        Assert.True(process.ExitCode == 0, await stderr);
    }
}
