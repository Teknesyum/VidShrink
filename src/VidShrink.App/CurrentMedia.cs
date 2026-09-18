using System;
using System.IO;
using VidShrink.Core;

namespace VidShrink.App;

public enum MediaFocusOwner
{
    None,
    Player,
    Shrink,
    Recorder
}

/// <summary>
/// Sekmelerin ortak odağı: hangi video "geçerli video". Yol, çözümlenmiş
/// <see cref="MediaInfo"/>, süre, kaynak fps ve son bilinen oynatma konumu tek yerde durur;
/// böylece oynatıcıda açılan dosya küçültmeye geçerken ikinci kez ffprobe'lanmaz.
/// Önbellek dosyanın uzunluğu ve son yazma anıyla damgalanır: dosya diskte değiştiyse
/// <see cref="InfoFor"/> boş döner ve çağıran yeniden yoklar.
/// </summary>
public sealed class CurrentMedia
{
    private long _stampLength = -1;
    private long _stampTicks = -1;

    public string? Path { get; private set; }

    public MediaInfo? Info { get; private set; }

    public double DurationSeconds { get; private set; }

    public double SourceFps { get; private set; }

    public double LastPositionSeconds { get; private set; }

    public MediaFocusOwner Owner { get; private set; } = MediaFocusOwner.None;

    public event Action<CurrentMedia>? Changed;

    public bool Holds(string? path) => SamePath(Path, path);

    /// <summary>Yol odaktaysa ve dosya damgalandığı günden beri değişmediyse bilinen çözümleme.</summary>
    public MediaInfo? InfoFor(string path)
        => Holds(path) && Info is { } info && Fresh(path) ? info : null;

    /// <summary>
    /// Odağı yola taşır; çözümleme henüz yok. Yol zaten odaktaysa yalnız sahip yazılır ve
    /// <see cref="Changed"/> sahip <b>gerçekten değiştiyse</b> yayılır: sessiz yazma aboneyi
    /// bayat bırakıyordu, koşulsuz yayım da her çağrıda gereksiz bir tur açardı.
    /// </summary>
    public void Focus(string path, MediaFocusOwner owner)
    {
        if (Holds(path))
        {
            if (Owner == owner) return;

            Owner = owner;
            Changed?.Invoke(this);
            return;
        }

        Path = path;
        Info = null;
        DurationSeconds = 0;
        SourceFps = 0;
        LastPositionSeconds = 0;
        Owner = owner;
        _stampLength = -1;
        _stampTicks = -1;
        Changed?.Invoke(this);
    }

    /// <summary>Odağı çözümlemeyle birlikte yazar ve dosyayı damgalar.</summary>
    public void Publish(string path, MediaInfo info, MediaFocusOwner owner)
    {
        if (!Holds(path)) LastPositionSeconds = 0;

        Path = path;
        Info = info;
        DurationSeconds = info.DurationSeconds;
        SourceFps = info.Fps;
        Owner = owner;
        Stamp(path);
        Changed?.Invoke(this);
    }

    /// <summary>Odaktaki dosyanın son bilinen konumu. Başka dosyanın konumu yazılmaz.</summary>
    public void Remember(string path, double seconds)
    {
        if (!Holds(path) || double.IsNaN(seconds) || seconds < 0) return;
        LastPositionSeconds = seconds;
    }

    public static bool SamePath(string? left, string? right)
    {
        if (left is null || right is null) return false;
        try
        {
            return string.Equals(
                System.IO.Path.GetFullPath(left),
                System.IO.Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }

    private bool Fresh(string path)
    {
        if (_stampLength < 0) return false;
        var (length, ticks) = Damga(path);
        return length == _stampLength && ticks == _stampTicks;
    }

    private void Stamp(string path)
    {
        var (length, ticks) = Damga(path);
        _stampLength = length;
        _stampTicks = ticks;
    }

    private static (long Length, long Ticks) Damga(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return (-1, -1);
            return (file.Length, file.LastWriteTimeUtc.Ticks);
        }
        catch (IOException) { return (-1, -1); }
        catch (UnauthorizedAccessException) { return (-1, -1); }
    }
}
