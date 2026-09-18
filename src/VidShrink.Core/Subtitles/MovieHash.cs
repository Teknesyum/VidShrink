namespace VidShrink.Core.Subtitles;

/// <summary>
/// OpenSubtitles'ın "moviehash" değeri: dosya boyutu ile dosyanın ilk 64 KB'ı ve son
/// 64 KB'ındaki 64-bit küçük-uçlu sözcüklerin toplamı. Toplama sarmalı serbest
/// (mod 2^64). Değer içeriğin tamamını değil iki ucunu okuduğu için büyük dosyada da
/// anlıktır; sağlayıcıda tam eşleşme aramasını bu sağlar.
/// </summary>
/// <remarks>
/// 128 KB'tan küçük dosyada iki blok üst üste binerdi; algoritma bu dosyaları
/// desteklemez ve <see cref="Compute(string)"/> <c>null</c> döner.
/// </remarks>
public static class MovieHash
{
    /// <summary>Her iki uçtan okunan blok boyu.</summary>
    public const int ChunkBytes = 64 * 1024;

    /// <summary>Altında hash üretilmeyen en küçük dosya boyu.</summary>
    public const long MinimumBytes = 2L * ChunkBytes;

    /// <summary>Dosyanın hash'i, ya da dosya küçük/okunamazsa <c>null</c>.</summary>
    public static string? Compute(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Compute(stream, stream.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Akıştan hash. <paramref name="length"/> dosyanın tam boyu olmalıdır.</summary>
    public static string? Compute(Stream stream, long length)
    {
        if (length < MinimumBytes) return null;

        var sum = unchecked((ulong)length);
        var block = new byte[ChunkBytes];

        stream.Seek(0, SeekOrigin.Begin);
        if (!Fill(stream, block)) return null;
        sum = unchecked(sum + Total(block));

        stream.Seek(length - ChunkBytes, SeekOrigin.Begin);
        if (!Fill(stream, block)) return null;
        sum = unchecked(sum + Total(block));

        return sum.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool Fill(Stream stream, byte[] block)
    {
        var read = 0;
        while (read < block.Length)
        {
            var step = stream.Read(block, read, block.Length - read);
            if (step <= 0) return false;
            read += step;
        }

        return true;
    }

    private static ulong Total(byte[] block)
    {
        var sum = 0UL;
        for (var at = 0; at < block.Length; at += sizeof(ulong))
            sum = unchecked(sum + System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(at)));
        return sum;
    }
}
