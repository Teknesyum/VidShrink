using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class CodecModelTests
{
    [Theory]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_videotoolbox")]
    public void VideoToolboxIsNotFiledUnderSoftware(string codec)
    {
        Assert.NotEqual(EncoderVendor.Software, CodecModel.Vendor(codec));
    }
}
