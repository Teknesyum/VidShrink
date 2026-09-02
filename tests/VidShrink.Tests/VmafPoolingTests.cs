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

        Assert.Contains("settb=AVTB,setpts=N", test);
        Assert.Contains("settb=AVTB,setpts=N", reference);
        Assert.EndsWith("[t]", test);
        Assert.EndsWith("[r]", reference);
        Assert.Contains("[t][r]libvmaf=", graph);
    }

    [Fact]
    public void OlcumFiltresi_KareKilidi_OlceklemedenSonraGelir()
    {
        var graph = MeasureFilterGraph.Build(1280, 720, "psnr");

        var test = graph.Split(';').Single(b => b.StartsWith("[0:v]"));

        Assert.True(test.IndexOf("scale=", StringComparison.Ordinal) < test.IndexOf("setpts=N", StringComparison.Ordinal), test);
        Assert.Contains("settb=AVTB,setpts=N", test);
    }
}
