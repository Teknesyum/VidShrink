namespace VidShrink.Core;

/// <summary>
/// Pencereye bırakılan yolları küçültülecek videolara indirir. Dosya <see cref="WatchFolder.IsCandidate"/>
/// ise alınır; klasörün yalnız üst düzeyi taranır, alt klasöre inilmez. Sonuç ad sırasında ve tekrarsız.
/// <paramref name="includeAudio"/> açıkken (Dönüştür) <see cref="ShellIntegration.IsAudio"/> olan gizli olmayan dosya da alınır.
/// </summary>
public static class DroppedMedia
{
    public static IReadOnlyList<string> Collect(IEnumerable<string> paths)
        => Collect(paths, includeAudio: false);

    public static IReadOnlyList<string> Collect(IEnumerable<string> paths, bool includeAudio)
        => Collect(paths, includeAudio, Directory.Exists, File.Exists, folder => Directory.EnumerateFiles(folder));

    internal static IReadOnlyList<string> Collect(IEnumerable<string> paths, Func<string, bool> isFolder,
        Func<string, bool> isFile, Func<string, IEnumerable<string>> filesIn)
        => Collect(paths, false, isFolder, isFile, filesIn);

    internal static IReadOnlyList<string> Collect(IEnumerable<string> paths, bool includeAudio, Func<string, bool> isFolder,
        Func<string, bool> isFile, Func<string, IEnumerable<string>> filesIn)
    {
        bool Alinir(string file) => WatchFolder.IsCandidate(file)
            || (includeAudio && ShellIntegration.IsAudio(file) && !Path.GetFileName(file).StartsWith('.'));

        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (isFolder(path))
            {
                IEnumerable<string> files;
                try { files = filesIn(path).ToList(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                foreach (var file in files)
                    if (Alinir(file)) found.Add(file);
            }
            else if (isFile(path) && Alinir(path))
            {
                found.Add(path);
            }
        }
        return found.ToList();
    }

    /// <summary>
    /// Bırakılan resimler ya da klasörün üst düzeyindeki resimler numaralı bir dizi oluşturuyorsa onu verir
    /// (<see cref="ImageSequence.Detect"/>); dizi yoksa <c>null</c>.
    /// </summary>
    public static ImageSequence? Sequence(IEnumerable<string> paths)
        => Sequence(paths, Directory.Exists, folder => Directory.EnumerateFiles(folder));

    internal static ImageSequence? Sequence(IEnumerable<string> paths, Func<string, bool> isFolder, Func<string, IEnumerable<string>> filesIn)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (!isFolder(path))
            {
                files.Add(path);
                continue;
            }
            try { files.AddRange(filesIn(path)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return ImageSequence.Detect(files);
    }
}
