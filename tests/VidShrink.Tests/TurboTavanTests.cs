using VidShrink.Core;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// <c>CodecModel.TurboFirstPassCeilings</c> tablosu <c>libx264</c> icin de bir tavan
/// vaat ediyor, ama o tavan uretim yolunda acilamaz: <c>veryfast</c> ilk gecis
/// <c>weightp=1</c>, <c>slow</c> ikinci gecis <c>weightp=2</c> kosar ve x264 ikinci gecisi
/// <c>different weightp setting than first pass (2 vs 1)</c> diyerek hic acmaz — cikti sifir
/// bayt olur. Bu olculer tablonun vaadini degil <b>karari</b> pimler: hangi kodek turboya
/// gercekten acilabilir ve uretim yolu tam olarak o kumeyi mi aciyor.
/// Olcum: <c>docs/olcumler/turbo-x264-mayini.md</c>.
/// <para>
/// Sifir bayt duvari asilabilir — iki gecise ayni <c>weightp</c> yazmak yetiyor — ve x264
/// yine acilmadi: esitlenmis turbo uretim borusunda %0,58 - %4,44 kazandirip VMAF'tan
/// 0,35 - 0,83 puan goturuyor, <c>libx265</c> ayni olcumde %29,6 - %33,5 kazandirip VMAF'i
/// dusurmuyor. Bu olculer o karari pimler; kararla birlikte dusmeleri beklenir.
/// Olcum: <c>docs/olcumler/x264-turbo-acilis.md</c>.
/// </para>
/// </summary>
public sealed class TurboTavanTests
{
    private readonly ITestOutputHelper _cikti;

    public TurboTavanTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 240,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 8_000_000
    };

    private static PlanOptions Secenekler(SpeedMode kip, CodecPreference tercih) => new()
    {
        TargetMb = 25,
        Codec = tercih,
        SpeedMode = kip
    };

    private static readonly CodecPreference[] Tercihler =
    {
        CodecPreference.Compatible, CodecPreference.MaxCompression, CodecPreference.Fast, CodecPreference.Auto
    };

    private sealed class TekKodek : IEncoderAvailability
    {
        private readonly string _kodek;

        public TekKodek(string kodek) => _kodek = kodek;

        private bool Esit(string ad) => string.Equals(ad, _kodek, StringComparison.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => Esit(name);

        public bool WorksAsEncoder(string codec) => Esit(codec);

        public EncoderProbeState EncoderState(string codec)
            => Esit(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
    }

    private Dictionary<string, SortedSet<string>> UretimYolununTurbosu(SpeedMode kip)
    {
        var gorulen = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var kurulu in FfmpegArguments.KnownCodecs)
        foreach (var tercih in Tercihler)
        {
            var plan = PlanCalculator.Build(Kaynak(), Secenekler(kip, tercih), new TekKodek(kurulu));
            if (!gorulen.TryGetValue(plan.Codec, out var degerler))
                gorulen[plan.Codec] = degerler = new SortedSet<string>(StringComparer.Ordinal);
            degerler.Add(plan.TurboFirstPass ? "acik" : "kapali");
        }
        return gorulen;
    }

    /// <summary>
    /// Tablo <c>libx264</c> icin gercekten bir tavan vaat ediyor — ilk gecisin on ayarini son
    /// gecisten ayiracak kadar — ama o tavan guvenli degil.
    /// </summary>
    [Fact]
    public void X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil()
    {
        const string sonGecis = "slow";
        var ilkGecis = FfmpegArguments.FirstPassPreset("libx264", sonGecis, turbo: true);

        _cikti.WriteLine($"libx264 son={sonGecis} turbo ilk={ilkGecis} " +
                         $"tavan={CodecModel.TurboFirstPassCeiling("libx264") ?? "<yok>"} " +
                         $"guvenli={CodecModel.TurboFirstPassIsSafe("libx264")}");

        Assert.True(CodecModel.SupportsTurboFirstPass("libx264"));
        Assert.NotEqual(sonGecis, ilkGecis);
        Assert.False(CodecModel.TurboFirstPassIsSafe("libx264"));
    }

    /// <summary>
    /// Tavan vaat edilen kume ile guvenli kume ayni degil; farki tasiyan kodek listelenir,
    /// sayilmaz.
    /// </summary>
    [Fact]
    public void Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264()
    {
        var vaat = CodecModel.TurboFirstPassCodecs.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var guvenli = vaat.Where(CodecModel.TurboFirstPassIsSafe).ToArray();
        var guvensiz = vaat.Where(k => !CodecModel.TurboFirstPassIsSafe(k)).ToArray();

        foreach (var kodek in vaat)
            _cikti.WriteLine($"{kodek,-10} tavan={CodecModel.TurboFirstPassCeiling(kodek),-9} " +
                             $"guvenli={CodecModel.TurboFirstPassIsSafe(kodek)}");
        _cikti.WriteLine($"vaat={string.Join(",", vaat)} guvenli={string.Join(",", guvenli)} " +
                         $"guvensiz={string.Join(",", guvensiz)}");

        Assert.Equal(new[] { "libx264", "libx265" }, vaat);
        Assert.Equal(new[] { "libx265" }, guvenli);
        Assert.Equal(new[] { "libx264" }, guvensiz);
    }

    /// <summary>
    /// Guvensiz tavan hicbir hiz kipinde uretim yolunda acilmiyor. Kume bos kalirsa olcu
    /// hicbir sey olcmeden gecerdi; once bos olmadigi pimlenir.
    /// </summary>
    [Fact]
    public void Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor()
    {
        var guvensiz = CodecModel.TurboFirstPassCodecs
            .Where(k => !CodecModel.TurboFirstPassIsSafe(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

        _cikti.WriteLine($"guvensiz kume: {(guvensiz.Length == 0 ? "<bos>" : string.Join(",", guvensiz))}");
        Assert.NotEmpty(guvensiz);

        foreach (var kip in new[] { SpeedMode.Quality, SpeedMode.Fast })
        foreach (var kodek in guvensiz)
        foreach (var tercih in Tercihler)
        {
            var plan = PlanCalculator.Build(Kaynak(), Secenekler(kip, tercih), new TekKodek(kodek));
            if (!plan.Codec.Equals(kodek, StringComparison.OrdinalIgnoreCase)) continue;
            _cikti.WriteLine($"{kip,-7} {tercih,-14} {plan.Codec,-10} son={plan.Preset,-6} turbo={plan.TurboFirstPass}");
            Assert.False(plan.TurboFirstPass, $"{kip}/{tercih}/{plan.Codec}");
        }
    }

    /// <summary>
    /// Uretim yolunun hizli kipte turboyu actigi kume ile <see cref="CodecModel.TurboFirstPassIsSafe"/>
    /// ayni seyi soylemeli. Iki taraftan biri kayarsa olcu olur.
    /// </summary>
    [Fact]
    public void Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni()
    {
        var hizli = UretimYolununTurbosu(SpeedMode.Fast);
        var kalite = UretimYolununTurbosu(SpeedMode.Quality);

        foreach (var kodek in hizli.Keys.Union(kalite.Keys).OrderBy(k => k, StringComparer.Ordinal))
            _cikti.WriteLine($"{kodek,-12} " +
                             $"hizli={(hizli.TryGetValue(kodek, out var h) ? string.Join("/", h) : "<ulasilmadi>"),-12} " +
                             $"kalite={(kalite.TryGetValue(kodek, out var k2) ? string.Join("/", k2) : "<ulasilmadi>"),-12} " +
                             $"guvenli={CodecModel.TurboFirstPassIsSafe(kodek)}");

        Assert.Contains("libx264", hizli.Keys);
        Assert.Contains("libx265", hizli.Keys);

        foreach (var kodek in hizli.Keys)
        {
            var beklenen = CodecModel.TurboFirstPassIsSafe(kodek) ? "acik" : "kapali";
            Assert.Equal(new[] { beklenen }, hizli[kodek].ToArray());
        }

        foreach (var kodek in kalite.Keys)
            Assert.Equal(new[] { "kapali" }, kalite[kodek].ToArray());
    }
}
