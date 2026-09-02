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
}
