namespace VidShrink.Ffmpeg;

public static class TempCleanup
{
    public static void CleanupStaleArtifacts(string tempDir)
    {
        DeleteMatching(tempDir, "vidshrink_*");
        DeleteMatching(tempDir, "*.partial");
    }

    private static void DeleteMatching(string tempDir, string pattern)
    {
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(tempDir, pattern).ToList(); }
        catch { return; }

        foreach (var file in files)
        {
            try { File.Delete(file); } catch { }
        }
    }
}
