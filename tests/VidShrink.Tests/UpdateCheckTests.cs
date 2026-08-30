using VidShrink.App;

namespace VidShrink.Tests;

public sealed class UpdateCheckTests
{
    [Theory]
    [InlineData("0.2.4+e55abcc", "0.2.4")]
    [InlineData("0.2.4-beta+e55abcc", "0.2.4-beta")]
    [InlineData("0.2.4", "0.2.4")]
    public void DisplayVersionCutsOnlyAtTheFirstBuildSeparator(string source, string expected)
        => Assert.Equal(expected, MainWindow.DisplayVersion(source));
}
