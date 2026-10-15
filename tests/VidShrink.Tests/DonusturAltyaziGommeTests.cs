using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de altyazı gömme (DON-P3): seçilen iz görüntüye yakılır. Metin izi küçültmenin
/// <see cref="VideoFilterChain.BurnFilter"/> süzgeciyle <c>-vf</c> zincirinin başına, resim izi
/// <see cref="VideoFilterChain.OverlayGraph"/> grafiğiyle <c>-filter_complex</c>'e girer; biçem
/// <c>force_style</c> ile yalnız metin izde yazılır. Yakılan iz yumuşak altyazı olarak çıkmaz.
/// </summary>
public sealed class DonusturAltyaziGommeTests
{
    private static readonly SourceStream Tur = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Srt = new(2, StreamKind.Subtitle, "subrip", "eng");
    private static readonly SourceStream Pgs = new(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "deu");

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container).ToList();

    private static List<string> Eslemeler(IReadOnlyList<string> args)
        => args.Select((arg, i) => (arg, i)).Where(x => x.arg == "-map" && x.i + 1 < args.Count).Select(x => args[x.i + 1]).ToList();

    [Fact]
    public void MetinIziBicemleBirlikteVfZincirininBasinaYaziliyor()
    {
        var plan = new ConversionPlan { Container = "mkv", Height = 720, BurnSubtitle = 0, BurnFont = "Arial", BurnFontSize = 24, BurnColor = "FFFF00", BurnBox = true };

        var args = Kur(Ornek(Video, Tur, Srt), plan);

        Assert.Equal(
            "subtitles=filename='C\\:/Kayitlar/gezi.mkv':si=0:force_style='FontName=Arial,FontSize=24,PrimaryColour=&H0000FFFF,BorderStyle=3',scale=-2:720:flags=lanczos",
            Sonraki(args, "-vf"));
        Assert.DoesNotContain("-filter_complex", args);
    }

    [Fact]
    public void BicemsizMetinIzindeForceStyleYok()
    {
        var args = Kur(Ornek(Video, Tur, Srt), new ConversionPlan { Container = "mp4", BurnSubtitle = 0 });

        Assert.Equal("subtitles=filename='C\\:/Kayitlar/gezi.mkv':si=0", Sonraki(args, "-vf"));
    }

    [Fact]
    public void KesitteSuzgecSetptsArasinaOturuyor()
    {
        var plan = new ConversionPlan { Container = "mp4", Start = TimeSpan.FromSeconds(12.5), BurnSubtitle = 0 };

        var vf = Sonraki(Kur(Ornek(Video, Tur, Srt), plan), "-vf");

        Assert.StartsWith("setpts=PTS+12.5/TB,subtitles=", vf);
        Assert.EndsWith(",setpts=PTS-12.5/TB", vf);
    }

    [Fact]
    public void ResimIziFilterComplexVeVHaritasiYaziyor()
    {
        var plan = new ConversionPlan { Container = "mkv", Height = 720, BurnSubtitle = 1 };

        var args = Kur(Ornek(Video, Tur, Srt, Pgs), plan);

        Assert.DoesNotContain("-vf", args);
        Assert.Equal("[0:3]scale=1920:-1[s];[0:0][s]overlay=x=(W-w)/2:y=H-h:eof_action=pass,scale=-2:720:flags=lanczos[v]", Sonraki(args, "-filter_complex"));
        Assert.Equal(new[] { "[v]", "0:1", "0:2" }, Eslemeler(args));
    }

    [Fact]
    public void ResimIziAltyaziTasimayanKaptaDaVHaritasiYaziyor()
    {
        var plan = new ConversionPlan { Container = "avi", AudioCodec = "libmp3lame", BurnSubtitle = 1 };

        var args = Kur(Ornek(Video, Tur, Srt, Pgs), plan);

        Assert.EndsWith("[v]", Sonraki(args, "-filter_complex"));
        Assert.Equal(new[] { "[v]", "0:1" }, Eslemeler(args));
    }

    [Fact]
    public void YakilanIzYumusakAltyaziOlarakCikmiyor()
    {
        var info = Ornek(Video, Tur, Srt, Pgs);

        Assert.Equal(new[] { "0:0", "0:1", "0:3" }, Eslemeler(Kur(info, new ConversionPlan { Container = "mkv", BurnSubtitle = 0 })));
        Assert.Equal(new[] { "0:0", "0:1", "0:2", "0:3" }, Eslemeler(Kur(info, new ConversionPlan { Container = "mkv" })));
    }

    [Fact]
    public void ResimIzindeBicemYazilmiyor()
    {
        var plan = new ConversionPlan { Container = "mkv", BurnSubtitle = 1, BurnFont = "Arial", BurnColor = "FFFF00" };

        var graph = Sonraki(Kur(Ornek(Video, Tur, Srt, Pgs), plan), "-filter_complex");

        Assert.DoesNotContain("force_style", graph);
        Assert.DoesNotContain("subtitles=", graph);
    }

    public static TheoryData<string, string> Retler => new()
    {
        { "copy", "Burning subtitles requires video encoding." },
        { "gif", "Burning subtitles requires video encoding." },
        { "webp", "Burning subtitles requires video encoding." },
        { "avif", "Burning subtitles requires video encoding." },
        { "mp3", "Burning subtitles requires video encoding." }
    };

    [Theory]
    [MemberData(nameof(Retler))]
    public void GoruntuKodlanmayanPlandaYakmaReddediliyor(string kip, string hata)
    {
        var plan = kip == "copy"
            ? new ConversionPlan { Container = "mkv", VideoCodec = "copy", AudioCodec = "copy", BurnSubtitle = 0 }
            : new ConversionPlan { Container = kip, AudioCodec = kip == "mp3" ? "libmp3lame" : "aac", BurnSubtitle = 0 };

        Assert.Contains(hata, ConversionArguments.Validate(Ornek(Video, Tur, Srt), plan));
        Assert.DoesNotContain(hata, ConversionArguments.Validate(Ornek(Video, Tur, Srt), new ConversionPlan { Container = "mkv", BurnSubtitle = 0 }));
    }

    [Fact]
    public void OlmayanIzVeBozukRenkReddediliyor()
    {
        var info = Ornek(Video, Tur, Srt);

        Assert.Contains("burn: the source has no such subtitle", ConversionArguments.Validate(info, new ConversionPlan { BurnSubtitle = 3 }));
        Assert.Contains("burn style: color must be RRGGBB", ConversionArguments.Validate(info, new ConversionPlan { BurnSubtitle = 0, BurnColor = "sari" }));
        Assert.Contains("burn style: color must be RRGGBB", ConversionArguments.Validate(info, new ConversionPlan { BurnSubtitle = 0, BurnColor = "FFF" }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { BurnSubtitle = 0, BurnColor = "00ff7f" }));
    }

    [Theory]
    [InlineData("FFFFFF", "&H00FFFFFF")]
    [InlineData("FFFF00", "&H0000FFFF")]
    [InlineData("00ff7f", "&H007FFF00")]
    public void RenkAssSirasinaCevriliyor(string renk, string ass)
    {
        var style = VideoFilterChain.BurnStyle(new VideoFilterOptions { BurnColor = renk });

        Assert.Equal("PrimaryColour=" + ass, style);
    }

    [Fact]
    public void YakmasizPlanEskiKomutuYaziyor()
    {
        var info = Ornek(Video, Tur, Srt);
        var plan = new ConversionPlan { Container = "mp4", BurnFont = "Arial", BurnColor = "FFFF00" };

        var args = Kur(info, plan);

        Assert.DoesNotContain("-vf", args);
        Assert.DoesNotContain("-filter_complex", args);
        Assert.Null(plan.BurnOptions.BurnFont);
    }
}
