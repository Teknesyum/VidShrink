namespace VidShrink.Core;

/// <summary>Adlı listenin bir girdisi; <see cref="Missing"/> diskte olmayan yerel dosyadır.</summary>
public sealed record NamedPlaylistEntry(string Path, bool Missing);

/// <summary>Adlı liste özeti: ad, dosya yolu, girdi ve eksik girdi sayısı.</summary>
public sealed record NamedPlaylist(string Name, string File, int Count, int Missing);

/// <summary>
/// OYB-09: adlı oynatma listeleri. Her liste ayar klasörünün altındaki <see cref="FolderName"/>
/// klasöründe bir m3u8 dosyasıdır; adı dosya adıdır. Klasör ayar klasörünün içinde durduğu için
/// eski addan göç onu ayarlarla birlikte taşır. Eksik dosya listeden atılmaz: okunurken işaretlenir,
/// liste yalnız kullanıcı kaydedince, adlandırınca ya da silince değişir.
/// </summary>
public sealed class NamedPlaylists
{
    public const string FolderName = "oynatma-listeleri";

    public const string Extension = ".m3u8";

    public const int MaxNameLength = 100;

    private static readonly string[] Reserved =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly Func<string, bool> _exists;

    public NamedPlaylists(string folder, Func<string, bool>? exists = null)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Klasör boş olamaz", nameof(folder));
        Folder = Path.GetFullPath(folder);
        _exists = exists ?? File.Exists;
    }

    public string Folder { get; }

    public static NamedPlaylists? ForSettingsFolder(string? settingsFolder)
        => string.IsNullOrWhiteSpace(settingsFolder) ? null : new NamedPlaylists(Path.Combine(settingsFolder, FolderName));

    /// <summary>Kullanıcının yazdığı adın dosya adı olabilen biçimi; boş kalırsa <c>null</c>.</summary>
    public static string? CleanName(string? name)
    {
        if (name is null) return null;
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var chars = name.Select(c => char.IsControl(c) || invalid.Contains(c) ? '_' : c).ToArray();
        var clean = new string(chars).Trim().TrimEnd('.', ' ').Trim();
        if (clean.Length > MaxNameLength) clean = clean[..MaxNameLength].TrimEnd('.', ' ');
        if (clean.Length == 0 || clean.Trim('_', '.').Length == 0) return null;
        var stem = clean.Split('.')[0].Trim();
        if (Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase)) clean = "_" + clean;
        return clean;
    }

    public string FileFor(string name) => Path.Combine(Folder, name + Extension);

    public IReadOnlyList<NamedPlaylist> List()
    {
        var lists = new List<NamedPlaylist>();
        try
        {
            if (!Directory.Exists(Folder)) return lists;
            foreach (var file in Directory.EnumerateFiles(Folder, "*" + Extension))
            {
                if (!string.Equals(Path.GetExtension(file), Extension, StringComparison.OrdinalIgnoreCase)) continue;
                var entries = Entries(file);
                lists.Add(new NamedPlaylist(Path.GetFileNameWithoutExtension(file), file, entries.Count, entries.Count(entry => entry.Missing)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return lists.OrderBy(list => list.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public NamedPlaylist? Find(string name)
        => List().FirstOrDefault(list => string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Listenin girdileri, yazıldığı sırayla; eksik yerel dosyalar işaretli. Okunamayan liste boş döner.</summary>
    public IReadOnlyList<NamedPlaylistEntry> Entries(string file)
    {
        try
        {
            return PlaylistFile.Read(file)
                .Select(entry => new NamedPlaylistEntry(entry, Path.IsPathFullyQualified(entry) && !_exists(entry)))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Array.Empty<NamedPlaylistEntry>();
        }
    }

    /// <summary>Listeyi adla yazar; aynı adlı liste varsa üstüne yazılır. Yazılan listenin adı, olmazsa <c>null</c>.</summary>
    public string? Save(string? name, IReadOnlyList<PlaylistEntry> entries)
    {
        if (CleanName(name) is not { } clean || entries.Count == 0) return null;
        var existing = Find(clean)?.Name ?? clean;
        try
        {
            Directory.CreateDirectory(Folder);
            PlaylistFile.Write(FileFor(existing), entries);
            return existing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Yeni ad başka bir listenin adıysa ya da geçersizse dokunmaz. Yeni ad, olmazsa <c>null</c>.</summary>
    public string? Rename(string name, string? newName)
    {
        if (Find(name) is not { } list || CleanName(newName) is not { } clean) return null;
        if (string.Equals(list.Name, clean, StringComparison.Ordinal)) return clean;
        if (!string.Equals(list.Name, clean, StringComparison.OrdinalIgnoreCase) && Find(clean) is not null) return null;
        try
        {
            var target = FileFor(clean);
            if (string.Equals(list.Name, clean, StringComparison.OrdinalIgnoreCase))
            {
                var step = FileFor(clean + "." + Guid.NewGuid().ToString("N")[..8]);
                File.Move(list.File, step);
                File.Move(step, target);
            }
            else File.Move(list.File, target);
            return clean;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public bool Delete(string name)
    {
        if (Find(name) is not { } list) return false;
        try
        {
            File.Delete(list.File);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// m3u/m3u8 (ve okunabilen öbür liste biçimleri) dosyasını kaynağın adıyla adlı liste yapar; ad doluysa
    /// sonuna " (2)", " (3)" eklenir. Eksik dosyalar listede kalır. Yeni listenin adı, olmazsa <c>null</c>.
    /// </summary>
    public string? Import(string source)
    {
        IReadOnlyList<string> entries;
        try { entries = PlaylistFile.Read(source); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        if (entries.Count == 0 || CleanName(Path.GetFileNameWithoutExtension(source)) is not { } stem) return null;
        var name = stem;
        for (var n = 2; Find(name) is not null; n++) name = stem + " (" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        return Save(name, entries.Select(entry => new PlaylistEntry(entry)).ToList());
    }

    /// <summary>Listeyi verilen yola m3u8 olarak yazar; hedefin klasöründeki dosyalar göreli yazılır, eksikler de.</summary>
    public bool Export(string name, string target)
    {
        if (Find(name) is not { } list) return false;
        var entries = Entries(list.File);
        if (entries.Count == 0) return false;
        try
        {
            PlaylistFile.Write(target, entries.Select(entry => new PlaylistEntry(entry.Path)).ToList());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
