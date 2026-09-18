using System;
using System.IO;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Sekmelerin ortak odağı: hangi video "geçerli video". Yol ve çözümlenmiş
/// <see cref="MediaInfo"/> tek yerde durur; böylece oynatıcıda açılan dosya küçültmeye
/// geçerken ikinci kez ffprobe'lanmaz.
/// Önbellek dosyanın uzunluğu ve son yazma anıyla damgalanır: dosya diskte değiştiyse
/// <see cref="InfoFor"/> boş döner ve çağıran yeniden yoklar.
///
/// <para>Yüzey bilerek dar: "kim değiştirir" kapısı bu nesne değil, Ayarlar'daki
/// <c>ChkFollowRecording</c> onay kutusu (<c>docs/plan-duzenleyici.md:167-173</c>). Süre ve
/// kaynak fps de burada tutulmaz — ikisi de <see cref="Info"/>'nun izdüşümü.</para>
/// </summary>
public sealed class CurrentMedia
{
    private long _stampLength = -1;
    private long _stampTicks = -1;

    public string? Path { get; private set; }

    public MediaInfo? Info { get; private set; }

    public bool Holds(string? path) => SamePath(Path, path);

    /// <summary>Yol odaktaysa ve dosya damgalandığı günden beri değişmediyse bilinen çözümleme.</summary>
    public MediaInfo? InfoFor(string path)
        => Holds(path) && Info is { } info && Fresh(path) ? info : null;

    /// <summary>Odağı yola taşır; çözümleme henüz yok.</summary>
    public void Focus(string path)
    {
        if (Holds(path)) return;

        Path = path;
        Info = null;
        _stampLength = -1;
        _stampTicks = -1;
    }

    /// <summary>Odağı çözümlemeyle birlikte yazar ve dosyayı damgalar.</summary>
    public void Publish(string path, MediaInfo info)
    {
        Path = path;
        Info = info;
        Stamp(path);
    }

    public static bool SamePath(string? left, string? right) => PathEquality.Same(left, right);

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
