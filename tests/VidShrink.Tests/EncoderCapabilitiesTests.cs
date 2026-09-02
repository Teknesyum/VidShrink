using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncoderCapabilitiesTests
{
    private const string EncodersWithoutSvtav1 = """
        Encoders:
         V..... = Video
         A..... = Audio
         S..... = Subtitle
         .F.... = Frame-level multithreading
         ..S... = Slice-level multithreading
         ...X.. = Codec is experimental
         ....B. = Supports draw_horiz_band
         .....D = Supports direct rendering method 1
        -------
         V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codecs: h264)
         V..... libx265              libx265 H.265 / HEVC (codecs: hevc)
         V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codecs: h264)
         A..... aac                  AAC (Advanced Audio Coding)
        """;

    private const string EncodersWithSvtav1 = """
        Encoders:
         V..... = Video
        -------
         V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codecs: h264)
         V..... libx265              libx265 H.265 / HEVC (codecs: hevc)
         V..... libsvtav1            SVT-AV1(codecs: av1)
        """;

    private const string Filters = """
        Filters:
          T.. = Timeline support
          C.. = Command support
          |   = Source or sink filter
         ... zscale            V->V       Apply resize, colorspace and bit depth conversion.
         ... tonemap           V->V       Conversion to/from different dynamic ranges.
        """;

    private const string Version = "ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026\n";

    [Fact]
    public void ParseEncodersIgnoresHeaderAndSeparatorLines()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithoutSvtav1, Filters, Version);

        Assert.True(caps.HasEncoder("libx264"));
        Assert.True(caps.HasEncoder("libx265"));
        Assert.True(caps.HasEncoder("h264_nvenc"));
        Assert.False(caps.HasEncoder("libsvtav1"));
        Assert.True(caps.HasFilter("zscale"));
        Assert.True(caps.HasFilter("tonemap"));
        Assert.Equal("9.0-full_build-www.gyan.dev Copyright (c) 2000-2026", caps.Version);
    }

    [Fact]
    public void MissingLibsvtav1DoesNotCrashPlanGenerationAndFallsBackToLibx265()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithoutSvtav1, Filters, Version);
        var info = new MediaInfo
        {
            FilePath = "sample.mp4",
            FileSizeBytes = 500L * 1024 * 1024,
            DurationSeconds = 120,
            Width = 1920,
            Height = 1080,
            Fps = 30,
            VideoCodec = "h264",
            TotalBitrateBps = 35_000_000,
            AudioCodec = "aac",
            AudioBitrateBps = 128_000,
            AudioChannels = 2
        };
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };

        var result = PlanCalculator.BuildDetailed(info, options, null, caps);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
        Assert.Contains("falls back to libx265", result.Plan.Reason);
    }

    [Fact]
    public void PresentLibsvtav1IsSelectedForMaxCompression()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithSvtav1, Filters, Version);
        Assert.True(caps.HasEncoder("libsvtav1"));
    }

    [Theory]
    [InlineData(0, "", true)]
    [InlineData(0, "x265 [warning]: Source height < 720p; disabling lookahead-slices\n", true)]
    [InlineData(0, "Incompatible pixel format 'p010le' for codec 'libx265', auto-selecting format 'yuv420p10le'\n", false)]
    [InlineData(0, "auto-selecting format 'yuv420p10le'\n", false)]
    [InlineData(1, "", false)]
    public void SilentPixelFormatConversionIsNotAcceptance(int exitCode, string diagnostic, bool expected)
        => Assert.Equal(expected, EncoderCapabilities.PixelFormatAccepted(exitCode, diagnostic));

    [FfmpegFact]
    public void RealEncoderRefusesP010leForLibx265AndTheGateReadsThatRefusal()
    {
        Assert.True(EncoderCapabilities.Instance.HasEncoder("libx265"), "libx265 bu ffmpeg derlemesinde yok; olcu kosturulamaz.");

        var refused = EncodeOneFrame("libx265", "p010le");
        var accepted = EncodeOneFrame("libx265", "yuv420p10le");

        Assert.Contains("Incompatible pixel format", refused, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incompatible pixel format", accepted, StringComparison.OrdinalIgnoreCase);

        Assert.False(EncoderCapabilities.PixelFormatAccepted(0, refused));
        Assert.True(EncoderCapabilities.PixelFormatAccepted(0, accepted));
    }

    private static string EncodeOneFrame(string codec, string pixelFormat)
    {
        var args = string.Join(' ',
            "-hide_banner", "-loglevel", "warning",
            "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
            "-vf", $"format={pixelFormat}", "-c:v", codec, "-pix_fmt", pixelFormat,
            "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc",
            "-frames:v", "1", "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null");

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("ffmpeg", args)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stderr;
    }
}
