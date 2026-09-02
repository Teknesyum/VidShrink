using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class TurboFirstPassTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan Plan(string kodek = "libx264", string onAyar = "slow") => new()
    {
        Codec = kodek,
        Mode = "2pass",
        VideoBitrateK = 1200,
        Width = 1280,
        Height = 720,
        Fps = 30,
        Preset = onAyar,
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static readonly string[] YazilimMerdiveni =
        { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" };

    private static IReadOnlyList<string> Merdiven(string kodek) => FfmpegArguments.PresetLadder(kodek);

    private static string OnAyar(IReadOnlyList<string> args)
    {
        var yer = args.IndexOf("-preset");
        return yer < 0 ? "<yok>" : args[yer + 1];
    }

    [Fact]
    public void Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz()
    {
        var plan = Plan();
        plan.TurboFirstPass = true;

        var ilk = OnAyar(FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 1, "gunluk"));
        var son = OnAyar(FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 2, "gunluk"));

        Assert.Equal("slow", son);
        Assert.Equal("veryfast", ilk);
    }

    public static IEnumerable<object[]> BilinenKodekler()
        => FfmpegArguments.KnownCodecs.Select(kodek => new object[] { kodek });

    [Fact]
    public void Turbo_kumesi_tam_olarak_x264_ve_x265()
    {
        Assert.Equal(
            new[] { "libx264", "libx265" },
            CodecModel.TurboFirstPassCodecs.OrderBy(kodek => kodek, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [MemberData(nameof(BilinenKodekler))]
    public void Kume_disindaki_her_kodek_turbo_tanimiyor(string kodek)
    {
        var kumede = kodek is "libx264" or "libx265";
        Assert.Equal(kumede, CodecModel.SupportsTurboFirstPass(kodek));
        Assert.Equal(kumede, CodecModel.TurboFirstPassCeiling(kodek) is not null);
    }

    [Fact]
    public void Kumedeki_her_kodegin_tavani_kendi_merdiveninde_gecerli_bir_basamak()
    {
        foreach (var kodek in CodecModel.TurboFirstPassCodecs)
        {
            var tavan = CodecModel.TurboFirstPassCeiling(kodek);
            Assert.NotNull(tavan);
            Assert.True(FfmpegArguments.IsValidPreset(kodek, tavan!), $"{kodek}: {tavan}");
        }
    }

    [Theory]
    [InlineData("ultrafast", "ultrafast")]
    [InlineData("superfast", "superfast")]
    [InlineData("veryfast", "veryfast")]
    [InlineData("faster", "veryfast")]
    [InlineData("fast", "veryfast")]
    [InlineData("medium", "veryfast")]
    [InlineData("slow", "veryfast")]
    [InlineData("slower", "veryfast")]
    [InlineData("veryslow", "veryfast")]
    public void Ilk_gecis_merdiveni_tavanda_kesiliyor(string sonGecis, string beklenen)
    {
        foreach (var kodek in CodecModel.TurboFirstPassCodecs)
        {
            Assert.Equal(beklenen, FfmpegArguments.FirstPassPreset(kodek, sonGecis, turbo: true));

            var plan = Plan(kodek, sonGecis);
            plan.TurboFirstPass = true;
            Assert.Equal(beklenen, OnAyar(FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 1, "gunluk")));
        }
    }

    [Theory]
    [InlineData("ultrafast")]
    [InlineData("veryfast")]
    [InlineData("medium")]
    [InlineData("slow")]
    [InlineData("veryslow")]
    public void Turbo_son_gecisin_argumanina_dokunmuyor(string onAyar)
    {
        foreach (var kodek in CodecModel.TurboFirstPassCodecs)
        {
            var kapali = Plan(kodek, onAyar);
            var acik = Plan(kodek, onAyar);
            acik.TurboFirstPass = true;

            Assert.Equal(
                FfmpegArguments.Build(Kaynak(), kapali, "cikti.mp4", 2, "gunluk"),
                FfmpegArguments.Build(Kaynak(), acik, "cikti.mp4", 2, "gunluk"));
            Assert.Equal(
                FfmpegArguments.Build(Kaynak(), kapali, "cikti.mp4", 0, null),
                FfmpegArguments.Build(Kaynak(), acik, "cikti.mp4", 0, null));
            Assert.Equal(onAyar, OnAyar(FfmpegArguments.Build(Kaynak(), acik, "cikti.mp4", 2, "gunluk")));
        }
    }

    [Fact]
    public void Ilk_gecis_hicbir_zaman_son_gecisten_yavas_kosmuyor()
    {
        foreach (var kodek in CodecModel.TurboFirstPassCodecs)
        {
            var merdiven = Merdiven(kodek);
            for (var basamak = 0; basamak < merdiven.Count; basamak++)
            {
                var ilk = FfmpegArguments.FirstPassPreset(kodek, merdiven[basamak], turbo: true);
                Assert.True(merdiven.IndexOf(ilk) <= basamak, $"{kodek}: {merdiven[basamak]} -> {ilk}");
            }
        }
    }

    [Fact]
    public void Kumedeki_kodeklerin_merdiveni_dokuz_basamak_ve_hizlidan_yavasa()
    {
        foreach (var kodek in CodecModel.TurboFirstPassCodecs)
            Assert.Equal(YazilimMerdiveni, Merdiven(kodek));
    }
}
