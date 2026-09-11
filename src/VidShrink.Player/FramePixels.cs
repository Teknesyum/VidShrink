namespace VidShrink.Player;

public static unsafe class FramePixels
{
    public static void CopyRows(IntPtr source, int sourceStride, IntPtr target, int targetStride, int height)
    {
        if (source == IntPtr.Zero || target == IntPtr.Zero || height <= 0) return;

        var rowBytes = Math.Min(sourceStride, targetStride);
        if (rowBytes <= 0) return;

        if (sourceStride == targetStride)
        {
            var total = (long)sourceStride * height;
            Buffer.MemoryCopy((void*)source, (void*)target, total, total);
            return;
        }

        for (var row = 0; row < height; row++)
        {
            Buffer.MemoryCopy(
                (byte*)source + (long)row * sourceStride,
                (byte*)target + (long)row * targetStride,
                targetStride,
                rowBytes);
        }
    }
}
