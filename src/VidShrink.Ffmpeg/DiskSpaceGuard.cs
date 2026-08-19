namespace VidShrink.Ffmpeg;

public static class DiskSpaceGuard
{
    private const long ExtraBufferMb = 200;
    private const long Multiplier = 3;

    public static long RequiredBytes(double targetMb)
        => (long)((targetMb * Multiplier + ExtraBufferMb) * 1024.0 * 1024.0);

    public static bool HasEnoughSpace(long freeBytes, double targetMb)
        => freeBytes >= RequiredBytes(targetMb);

    public static bool TryGetFreeBytes(string path, out long freeBytes)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
            {
                freeBytes = long.MaxValue;
                return false;
            }
            freeBytes = new DriveInfo(root).AvailableFreeSpace;
            return true;
        }
        catch
        {
            freeBytes = 0;
            return false;
        }
    }
}
