namespace VidShrink.Ffmpeg;

public static class DeviceFrame
{
    internal const int Attempts = 6;

    internal const int RetryMs = 120;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly byte[] PngEnd = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

    internal static bool IsCompletePng(ReadOnlySpan<byte> bytes)
        => bytes.Length >= PngSignature.Length + PngEnd.Length
           && bytes[..PngSignature.Length].SequenceEqual(PngSignature)
           && bytes[^PngEnd.Length..].SequenceEqual(PngEnd);

    public static async Task<bool> CopyAsync(string framePath, string imagePath, CancellationToken ct = default,
        int attempts = Attempts, int retryMs = RetryMs)
    {
        if (string.IsNullOrWhiteSpace(framePath)) throw new ArgumentException("Frame path is required.", nameof(framePath));
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("Image path is required.", nameof(imagePath));

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0) await Task.Delay(retryMs, ct);
            if (Read(framePath) is not { } bytes || !IsCompletePng(bytes)) continue;

            var folder = Path.GetDirectoryName(imagePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(imagePath, bytes, ct);
            return true;
        }

        return false;
    }

    private static byte[]? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
