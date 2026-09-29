using System.Globalization;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public class DuzenlenebilirCiktiTests
{
    private static MediaInfo Source() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan Plan(string codec, string mode) => new()
    {
        Codec = codec,
        Mode = mode,
        Crf = mode == "crf" ? 28 : null,
        VideoBitrateK = 1200,
        Width = 1280,
        Height = 720,
        Fps = 30,
        Preset = "slow",
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static string? Deger(IReadOnlyList<string> args, string flag)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
                return args[i + 1];
        return null;
    }

    private static string? ParametreAnahtari(IReadOnlyList<string> args, string flag, string key)
    {
        var value = Deger(args, flag);
        if (value is null) return null;
        foreach (var part in value.Split(':'))
            if (part.StartsWith(key + "=", StringComparison.Ordinal))
                return part[(key.Length + 1)..];
        return null;
    }

    public static TheoryData<string, string, int> Kollar()
    {
        var data = new TheoryData<string, string, int>();
        foreach (var codec in new[] { "libx264", "libx265", "libsvtav1", "libvpx-vp9" })
        {
            data.Add(codec, "2pass", 1);
            data.Add(codec, "2pass", 2);
            data.Add(codec, "crf", 0);
        }
        foreach (var codec in new[] { "h264_nvenc", "hevc_nvenc", "av1_nvenc" })
        {
            data.Add(codec, "2pass", 0);
            data.Add(codec, "crf", 0);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Kollar))]
    public void KucultCiktisiHerKoldaSinirliAnahtarKareAraligiTasir(string codec, string mode, int pass)
    {
        var plan = Plan(codec, mode);
        var args = FfmpegArguments.Build(Source(), plan, "cikti.mp4", pass, pass > 0 ? "log" : null);

        var g = Deger(args, "-g");
        Assert.NotNull(g);
        var frames = int.Parse(g!, CultureInfo.InvariantCulture);
        var tavan = CodecModel.IsHardware(codec)
            ? FfmpegArguments.HardwareKeyframeCeilingSeconds
            : FfmpegArguments.KeyframeCeilingMaxSeconds;
        Assert.InRange(frames, 1, (int)Math.Round(plan.Fps * tavan));
        Assert.Equal(FfmpegArguments.KeyframeInterval(codec, plan.Fps).MaxFrames, frames);
    }

    [Theory]
    [InlineData("libx265", "-x265-params")]
    [InlineData("libsvtav1", "-svtav1-params")]
    public void KodlayiciParametresindekiKeyintGyleAyni(string codec, string flag)
    {
        var args = FfmpegArguments.Build(Source(), Plan(codec, "2pass"), "cikti.mp4", 2, "log");

        Assert.Single(args, a => a == flag);
        Assert.NotNull(Deger(args, "-g"));
        Assert.Equal(Deger(args, "-g"), ParametreAnahtari(args, flag, "keyint"));
    }

    [Theory]
    [InlineData("libx265", "-x265-params")]
    [InlineData("libsvtav1", "-svtav1-params")]
    public void KullanicininParametresiKeyintiDusurmez(string codec, string flag)
    {
        var plan = Plan(codec, "2pass");
        plan.ExtraArgs.AddRange(new[] { flag, "aq-mode=2" });
        var args = FfmpegArguments.Build(Source(), plan, "cikti.mp4", 2, "log");

        Assert.Single(args, a => a == flag);
        Assert.NotNull(Deger(args, "-g"));
        Assert.Equal(Deger(args, "-g"), ParametreAnahtari(args, flag, "keyint"));
        Assert.Equal("2", ParametreAnahtari(args, flag, "aq-mode"));
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void KayitVarsayilaniIkiSaniyelikAnahtarKareYazar(string codec)
    {
        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Screen,
            VideoCodec = codec,
            Fps = 30
        };

        var args = RecorderArguments.Build(istek, @"C:\kayit\a.mp4");

        Assert.Equal("60", Deger(args, "-g"));
    }
}
