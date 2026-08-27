using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class FfmpegArgumentsTests
{
    private static MediaInfo Source() => new()
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

    private static EncodePlan Plan() => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = 1200,
        Width = 1280,
        Height = 720,
        Fps = 30,
        Preset = "slow",
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    /// <summary>
    /// Argumanlari bayrak-deger ciftlerine ayirir. Iki argumandan yalnizca birinde bulunan
    /// ciftler K4'un fark listesidir.
    /// </summary>
    private static List<string> Pairs(IReadOnlyList<string> args)
    {
        var pairs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith('-')) { pairs.Add($"<konum> {args[i]}"); continue; }
            if (i + 1 < args.Count && !args[i + 1].StartsWith('-'))
            {
                pairs.Add($"{args[i]} {args[i + 1]}");
                i++;
            }
            else pairs.Add(args[i]);
        }
        return pairs;
    }

    private static string FlagOf(string pair) => pair.Split(' ')[0];

    [Fact]
    public void Ss_girdiden_once_gelir()
    {
        var args = FfmpegArguments.BuildSegment(Source(), Plan(), 12.5, 2.0, "ornek.mp4");

        var ss = args.IndexOf("-ss");
        var input = args.IndexOf("-i");

        Assert.True(ss >= 0, "-ss uretilmedi");
        Assert.True(input >= 0, "-i uretilmedi");
        Assert.True(ss < input, $"-ss {ss}. sirada, -i {input}. sirada");
        Assert.Equal("12.5", args[ss + 1]);
    }

    [Fact]
    public void Sure_girdiden_sonra_gelir()
    {
        var args = FfmpegArguments.BuildSegment(Source(), Plan(), 12.5, 1.75, "ornek.mp4");

        var input = args.IndexOf("-i");
        var t = args.IndexOf("-t");

        Assert.True(t > input, $"-t {t}. sirada, -i {input}. sirada");
        Assert.Equal("1.75", args[t + 1]);
    }

    [Fact]
    public void Parca_argumanlari_tam_kodlamadan_yalniz_uc_baslikta_ayrilir()
    {
        var info = Source();
        var plan = Plan();
        var segment = PreviewSegment.For(info, plan, 12.5, "ornek.mp4");

        var full = Pairs(FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "gunluk"));
        var part = Pairs(segment.Arguments);

        var onlyFull = full.Except(part).ToList();
        var onlyPart = part.Except(full).ToList();

        var izinli = new[] { "-ss", "-t", "-b:v", "-maxrate", "-bufsize", "-crf", "-pass", "-passlogfile", "<konum>" };
        var disarida = onlyFull.Concat(onlyPart).Select(FlagOf).Distinct().Except(izinli).ToList();

        Assert.True(disarida.Count == 0, "izinsiz fark: " + string.Join(", ", disarida));

        var zincir = part.Single(p => FlagOf(p) == "-vf")["-vf ".Length..].Split(',');
        Assert.Contains("scale=1280:720:flags=lanczos", zincir);
        Assert.Equal("fps=30", zincir[^1]);
        Assert.True(Array.IndexOf(zincir, "scale=1280:720:flags=lanczos") < zincir.Length - 1,
            "olcekleme kare dusurmeden sonra geliyor: " + string.Join(',', zincir));
        Assert.DoesNotContain("-r", part.Select(FlagOf));
        Assert.Contains("-preset slow", part);
        Assert.Contains("-pix_fmt yuv420p", part);
        Assert.Contains("-c:a aac", part);
        Assert.Contains("-b:a 128k", part);
        Assert.Contains("-g 60", part);
    }

    [Fact]
    public void Parca_argumanlari_iki_gecis_bayragi_tasimaz()
    {
        var info = Source();
        var segment = PreviewSegment.For(info, Plan(), 12.5, "ornek.mp4");

        Assert.DoesNotContain("-pass", segment.Arguments);
        Assert.DoesNotContain("-passlogfile", segment.Arguments);
        Assert.DoesNotContain("-b:v", segment.Arguments);
        Assert.Contains("-crf", segment.Arguments);
    }
}
