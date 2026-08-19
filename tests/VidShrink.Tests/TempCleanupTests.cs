using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class TempCleanupTests
{
    [Fact]
    public void CleanupRemovesVidshrinkPrefixedAndPartialFilesButKeepsOthers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var passLog = Path.Combine(dir, "vidshrink_abc123-0.log");
            var palette = Path.Combine(dir, "vidshrink_" + Guid.NewGuid().ToString("N") + ".png");
            var partial = Path.Combine(dir, "output.mp4.partial");
            var unrelated = Path.Combine(dir, "keep.txt");
            File.WriteAllText(passLog, "x");
            File.WriteAllText(palette, "x");
            File.WriteAllText(partial, "x");
            File.WriteAllText(unrelated, "x");

            TempCleanup.CleanupStaleArtifacts(dir);

            Assert.False(File.Exists(passLog));
            Assert.False(File.Exists(palette));
            Assert.False(File.Exists(partial));
            Assert.True(File.Exists(unrelated));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
