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

        Assert.Equal(FfmpegArguments.NeedsTwoPasses(softwareTwin), FfmpegArguments.NeedsTwoPasses(codec));
        Assert.NotEqual(FfmpegArguments.NeedsTwoPasses(hardwareTwin), FfmpegArguments.NeedsTwoPasses(codec));

        Assert.Equal(CodecModel.CostsQualityInHardware(softwareTwin), CodecModel.CostsQualityInHardware(codec));
        Assert.NotEqual(CodecModel.CostsQualityInHardware(hardwareTwin), CodecModel.CostsQualityInHardware(codec));
    }
}
