namespace VidShrink.Core;

/// <summary>
/// Pencereye bırakılan yolları küçültülecek videolara indirir. Dosya <see cref="WatchFolder.IsCandidate"/>
/// ise alınır; klasörün yalnız üst düzeyi taranır, alt klasöre inilmez. Sonuç ad sırasında ve tekrarsız.
/// </summary>
public static class DroppedMedia
{
    public static IReadOnlyList<string> Collect(IEnumerable<string> paths)
        => Collect(paths, Directory.Exists, File.Exists, folder => Directory.EnumerateFiles(folder));

    internal static IReadOnlyList<string> Collect(IEnumerable<string> paths, Func<string, bool> isFolder,
        Func<string, bool> isFile, Func<string, IEnumerable<string>> filesIn)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (isFolder(path))
            {
                IEnumerable<string> files;
                try { files = filesIn(path).ToList(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                foreach (var file in files)
                    if (WatchFolder.IsCandidate(file)) found.Add(file);
            }
            else if (isFile(path) && WatchFolder.IsCandidate(path))
            {
                found.Add(path);
            }
        }
        return found.ToList();
    }
}
