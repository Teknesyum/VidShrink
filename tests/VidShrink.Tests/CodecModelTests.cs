using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class CodecModelTests
{
    [Theory]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_videotoolbox")]
    public void VideoToolboxIsNotFiledUnderSoftware(string codec)
    {
        Assert.Equal(EncoderVendor.VideoToolbox, CodecModel.Vendor(codec));
    }

    [Theory]
    [InlineData("hevc_videotoolbox", "libx265", "hevc_nvenc")]
    [InlineData("h264_videotoolbox", "libx264", "h264_nvenc")]
    public void VideoToolboxStaysOffTheHardwarePath(string codec, string softwareTwin, string hardwareTwin)
    {
        Assert.False(CodecModel.IsHardware(codec));

        Assert.Equal(CodecModel.FloorBppf(softwareTwin), CodecModel.FloorBppf(codec));
        Assert.NotEqual(CodecModel.FloorBppf(hardwareTwin), CodecModel.FloorBppf(codec));

        Assert.Equal(CodecModel.QualityLimit(softwareTwin), CodecModel.QualityLimit(codec));
        Assert.NotEqual(CodecModel.QualityLimit(hardwareTwin), CodecModel.QualityLimit(codec));

        Assert.Equal(CodecModel.MinBitrateK(softwareTwin, 1920, 1080, 60), CodecModel.MinBitrateK(codec, 1920, 1080, 60));
        Assert.NotEqual(CodecModel.MinBitrateK(hardwareTwin, 1920, 1080, 60), CodecModel.MinBitrateK(codec, 1920, 1080, 60));

        Assert.NotEqual(FfmpegArguments.NeedsTwoPasses(softwareTwin), FfmpegArguments.NeedsTwoPasses(codec));
        Assert.Equal(FfmpegArguments.NeedsTwoPasses(hardwareTwin), FfmpegArguments.NeedsTwoPasses(codec));

        Assert.Equal(CodecModel.CostsQualityInHardware(softwareTwin), CodecModel.CostsQualityInHardware(codec));
        Assert.NotEqual(CodecModel.CostsQualityInHardware(hardwareTwin), CodecModel.CostsQualityInHardware(codec));
    }

    private static MediaInfo Source() => new()
    {
        FilePath = "in.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000
    };

    private static EncodePlan TwoPassPlan(string codec) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = 2000,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = "slow",
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    [Theory]
    [InlineData("hevc_videotoolbox", true)]
    [InlineData("h264_videotoolbox", true)]
    [InlineData("libx265", false)]
    [InlineData("libx264", false)]
    [InlineData("libsvtav1", false)]
    public void VideoToolboxRunsOnceWithoutPresetOrPassFlags(string codec, bool videoToolbox)
    {
        var single = FfmpegArguments.Build(Source(), TwoPassPlan(codec), "out.mp4", 0, null);

        Assert.Equal(videoToolbox, CodecModel.SinglePassRateControl(codec));
        Assert.Equal(!videoToolbox, FfmpegArguments.NeedsTwoPasses(codec));
        Assert.Equal(!videoToolbox, CodecModel.TakesPreset(codec));
        Assert.Equal(!videoToolbox, single.Contains("-preset"));
        Assert.DoesNotContain("-pass", single);
        Assert.Contains("-b:v", single);
        Assert.False(CodecModel.IsHardware(codec));
    }

    [Theory]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_videotoolbox")]
    public void VideoToolboxHasNoRowInTheBitrateNeedTable(string codec)
    {
        var offTable = CodecModel.RelativeBitrateNeed("an_encoder_that_does_not_exist");

        Assert.Equal(offTable, CodecModel.RelativeBitrateNeed(codec));
        Assert.NotEqual(CodecModel.RelativeBitrateNeed("libx265"), CodecModel.RelativeBitrateNeed(codec));
        Assert.NotEqual(CodecModel.RelativeBitrateNeed("hevc_nvenc"), CodecModel.RelativeBitrateNeed(codec));
    }
}
