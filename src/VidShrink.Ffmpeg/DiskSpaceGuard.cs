using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class DiskSpaceGuard
{
    private const long ExtraBufferMb = 200;
    private const long Multiplier = 3;

    /// <summary>SIK-P3: kuyruk koşarken boş alan bunun altına inince kodlama kendiliğinden duraklar.</summary>
    public static readonly long PauseBelowBytes = (long)(ExtraBufferMb * Megabayt.Bayt);

    public static bool ShouldPause(long freeBytes) => freeBytes < PauseBelowBytes;

    /// <summary>EKA-11: kaydedicinin uyari esigi; kaydin boyu onceden bilinmedigi icin kuyruktan genis tutulur.</summary>
    public static readonly long RecorderLowBelowBytes = (long)(1000 * Megabayt.Bayt);

    /// <summary>EKA-11: kayit surerken bos alanin yeniden okunma araligi.</summary>
    public static readonly TimeSpan RecorderCheckInterval = TimeSpan.FromSeconds(10);

    public static bool IsLowForRecording(long freeBytes) => freeBytes < RecorderLowBelowBytes;

    public static long RequiredBytes(double targetMb)
        => (long)((targetMb * Multiplier + ExtraBufferMb) * Megabayt.Bayt);

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
