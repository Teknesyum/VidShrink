using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// <c>--suzgec chroma-smooth=</c> (HandBrake <c>--chroma-smooth</c>): renk duzlemleri yumusar,
/// parlaklik duzlemi oldugu gibi kalir.
/// </summary>
public sealed class KromaYumusatmaTests
{
    private static readonly MediaInfo Kaynak = new()
    {
        FilePath = "kaynak.mp4", FileSizeBytes = 400_000_000L, DurationSeconds = 120, Width = 1920, Height = 1080, Fps = 25,
        VideoCodec = "h264", TotalBitrateBps = 20_000_000
    };

    private static IReadOnlyList<string> Zincir(VideoFilterOptions filtre, int w = 1920, int h = 1080)
        => VideoFilterChain.Filters(Kaynak, new EncodePlan
        {
            Codec = "libx264", Mode = "crf", Crf = 23, VideoBitrateK = 3000,
            Width = w, Height = h, Fps = 25, Preset = "medium", Filters = filtre
        });

    [Theory]
    [InlineData("light", ChromaSmoothMode.Light, "unsharp=lx=3:ly=3:la=0:cx=3:cy=3:ca=-0.5")]
    [InlineData("medium", ChromaSmoothMode.Medium, "unsharp=lx=3:ly=3:la=0:cx=5:cy=5:ca=-1.0")]
    [InlineData("strong", ChromaSmoothMode.Strong, "unsharp=lx=3:ly=3:la=0:cx=7:cy=7:ca=-1.5")]
    public void BelirtimKipeKipSuzgeceIniyor(string ad, ChromaSmoothMode kip, string suzgec)
    {
        var o = VideoFilterChain.Parse("chroma-smooth=" + ad);

        Assert.Equal(kip, o.ChromaSmooth);
        Assert.True(o.ChangesPicture);
        Assert.Equal("chroma-smooth=" + ad, VideoFilterChain.Format(o));
        Assert.Equal(new[] { suzgec }, Zincir(o));
    }

    [Fact]
    public void KapaliykenSuzgecYokVeBozukDegerReddediliyor()
    {
        Assert.Empty(Zincir(VideoFilterOptions.Default));
        Assert.False(VideoFilterOptions.Default.ChangesPicture);
        Assert.DoesNotContain("chroma-smooth", VideoFilterChain.Format(VideoFilterOptions.Default));
        Assert.Throws<ArgumentException>(() => VideoFilterChain.Parse("chroma-smooth=uydurma"));
        Assert.Throws<ArgumentException>(() => VideoFilterChain.Parse("chroma-smooth"));
    }

    [Fact]
    public void OlceklemedenSonraKeskinlestirmedenOnceGeliyor()
    {
        var o = VideoFilterChain.Parse("sharpen=light, chroma-smooth=medium");

        Assert.Equal("sharpen=light, chroma-smooth=medium", VideoFilterChain.Format(o));
        Assert.Equal(
            new[] { "scale=1280:720:flags=lanczos", "unsharp=lx=3:ly=3:la=0:cx=5:cy=5:ca=-1.0", "unsharp=5:5:0.5:3:3:0.0" },
            Zincir(o, 1280, 720));
    }

    private static async Task<(int Code, string Ozet)> DuzlemAsync(string suzgec, string duzlem)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-loglevel", "error", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=25:d=1",
            "-vf", suzgec + ",format=yuv420p,extractplanes=" + duzlem, "-frames:v", "3", "-f", "md5", "-"
        });
        return (sonuc.Code, sonuc.Out.Trim());
    }

    /// <summary>
    /// Canli kol: uc kipin her biri parlakligi bayt bayt birakir, rengi degistirir. Uydurma
    /// anahtar reddedilir; reddedilmeseydi "kabul" olcusu bir sey soylemezdi.
    /// </summary>
    [Fact]
    public async Task CanliParlaklikAyniRenkFarkli()
    {
        var (_, hamY) = await DuzlemAsync("null", "y");
        var (_, hamU) = await DuzlemAsync("null", "u");
        Assert.StartsWith("MD5=", hamY);
        Assert.NotEqual(hamY, hamU);

        var renkler = new List<string> { hamU };
        foreach (var kip in new[] { ChromaSmoothMode.Light, ChromaSmoothMode.Medium, ChromaSmoothMode.Strong })
        {
            var suzgec = VideoFilterChain.ChromaSmoothText(kip)!;
            var (kodY, y) = await DuzlemAsync(suzgec, "y");
            var (kodU, u) = await DuzlemAsync(suzgec, "u");
            Assert.Equal(0, kodY);
            Assert.Equal(0, kodU);
            Assert.Equal(hamY, y);
            Assert.DoesNotContain(u, renkler);
            renkler.Add(u);
        }

        var (uydurma, _) = await DuzlemAsync("unsharp=zz=3", "y");
        Assert.NotEqual(0, uydurma);
    }
}
