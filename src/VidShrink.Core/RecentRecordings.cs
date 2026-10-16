namespace VidShrink.Core;

public sealed record RecordingEntry(string Path, long Bytes, DateTime WrittenUtc);

/// <summary>
/// EKB-25: kaydedicinin son kayıtları. Kayıt klasöründe kaydedicinin adlandırdığı (<c>kayit_*</c>) ve kaydedicinin
/// yazdığı kaplardaki dosyalar, en yenisi başta. Klasördeki başka videolar ve kayıt sırasında alınan kareler listeye girmez.
/// </summary>
public static class RecentRecordings
{
    public const string Prefix = "kayit_";

    private static readonly string[] Extensions = Enum.GetValues<RecorderContainer>()
        .Select(container => "." + RecorderArguments.Extension(container)).ToArray();

    public static IReadOnlyList<RecordingEntry> List(string folder, int max)
    {
        if (max <= 0 || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return Array.Empty<RecordingEntry>();
        try
        {
            return new DirectoryInfo(folder).EnumerateFiles(Prefix + "*")
                .Where(file => Extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                .Select(file => new RecordingEntry(file.FullName, file.Length, file.LastWriteTimeUtc))
                .OrderByDescending(entry => entry.WrittenUtc)
                .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                .Take(max)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<RecordingEntry>();
        }
    }
}
