using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class DiskSpaceGuardTests
{
    [Theory]
    [InlineData(100, 500, true)]
    [InlineData(100, 499, false)]
    [InlineData(0, 200, true)]
    [InlineData(0, 199, false)]
    public void HasEnoughSpaceComparesAgainstTargetTimesThreePlusBuffer(double targetMb, long freeMb, bool expected)
    {
        var freeBytes = freeMb * 1024L * 1024L;
        Assert.Equal(expected, DiskSpaceGuard.HasEnoughSpace(freeBytes, targetMb));
    }

    [Fact]
    public void RequiredBytesMatchesTargetTimesThreePlusTwoHundred()
    {
        Assert.Equal(500L * 1024 * 1024, DiskSpaceGuard.RequiredBytes(100));
    }
}
