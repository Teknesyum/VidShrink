using System.Security.Cryptography;
using System.Text;

namespace VidShrink.Core;

/// <summary>MPRIS'in bir oynatıcıdan istediği iz bilgisi; D-Bus'a <c>a{sv}</c> olarak yazılır.</summary>
public sealed record MprisMetadata(string TrackId, string? Url, string? Title, long LengthMicroseconds);

/// <summary>
/// Linux masaüstünün medya denetimi (MPRIS 2.2, <c>org.mpris.MediaPlayer2</c>) ile denetim kanalı
/// arasındaki saf eşleme. Veriyolu yok: yöntem adı <see cref="ControlCommand"/>'a, <see cref="ControlStatus"/>
/// MPRIS özelliklerine çevrilir. Veriyolu tarafı uygulamada (<c>Integration/MprisServer</c>), komutun
/// yürütülmesi denetim kanalının yürüdüğü yoldan (<c>ControlBridge</c>) geçer. libmpv'nin
/// <c>media-controls</c> seçeneği Linux'ta MPRIS vermez (<c>docs/control-channel.md</c>).
/// </summary>
public static class Mpris
{
    public const string BusPrefix = "org.mpris.MediaPlayer2.";
    public const string PlayerName = "vidshrinkplayer";
    public const string ObjectPath = "/org/mpris/MediaPlayer2";
    public const string RootInterface = "org.mpris.MediaPlayer2";
    public const string PlayerInterface = "org.mpris.MediaPlayer2.Player";
    public const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    public const string IntrospectableInterface = "org.freedesktop.DBus.Introspectable";
    public const string NoTrack = "/org/mpris/MediaPlayer2/TrackList/NoTrack";
    public const string TrackPrefix = "/org/mpris/MediaPlayer2/vidshrinkplayer/track/t";
    public const string DesktopEntry = AppIdentity.LinuxName;
    public const string Identity = AppIdentity.Name;

    /// <summary><c>0</c> verilirse MPRIS yayını açılmaz; başka her değer ya da yokluk açık demektir.</summary>
    public const string SwitchVariable = "VIDSHRINK_MPRIS";

    public const string Playing = "Playing";
    public const string Paused = "Paused";
    public const string Stopped = "Stopped";

    public const string MethodPlay = "Play";
    public const string MethodPause = "Pause";
    public const string MethodPlayPause = "PlayPause";
    public const string MethodStop = "Stop";
    public const string MethodSeek = "Seek";
    public const string MethodSetPosition = "SetPosition";
    public const string MethodOpenUri = "OpenUri";
    public const string MethodNext = "Next";
    public const string MethodPrevious = "Previous";

    public const string PropertyPlaybackStatus = "PlaybackStatus";
    public const string PropertyMetadata = "Metadata";
    public const string PropertyPosition = "Position";

    public static IReadOnlyList<string> UriSchemes { get; } = new[] { "file" }.Concat(MediaAddress.Schemes).ToArray();

    public static IReadOnlyList<string> MimeTypes { get; } = new[]
    {
        "video/mp4", "video/x-matroska", "video/webm", "video/quicktime", "video/x-msvideo", "video/mpeg",
        "video/mp2t", "video/ogg", "audio/mpeg", "audio/mp4", "audio/flac", "audio/ogg", "audio/x-wav", "audio/x-matroska"
    };

    public static bool Enabled(bool linux, string? variable)
        => linux && !string.Equals(variable?.Trim(), "0", StringComparison.Ordinal);

    /// <summary>İlk örnek kısa adı alır; ikinci pencere ya da ad tutuluyken MPRIS'in örnek eki kullanılır.</summary>
    public static string BusName(int? instance)
        => instance is { } id ? $"{BusPrefix}{PlayerName}.instance{id}" : BusPrefix + PlayerName;

    public static string PlaybackStatus(ControlStatus? status)
        => status?.PlayerPath is null ? Stopped : status.Playing ? Playing : Paused;

    public static long Microseconds(double? seconds)
        => seconds is { } s && double.IsFinite(s) && s > 0 ? (long)Math.Round(s * 1_000_000d) : 0;

    public static double Seconds(long microseconds) => microseconds / 1_000_000d;

    /// <summary>Aynı yola aynı kimlik; nesne yolu öğesi yalnız onaltılık harf taşır.</summary>
    public static string TrackId(string? path)
    {
        if (string.IsNullOrEmpty(path)) return NoTrack;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return TrackPrefix + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    public static MprisMetadata Metadata(ControlStatus? status)
    {
        var path = status?.PlayerPath;
        if (path is null) return new MprisMetadata(NoTrack, null, null, 0);
        var title = string.IsNullOrWhiteSpace(status!.Title) ? DisplayName(path) : status.Title!.Trim();
        return new MprisMetadata(TrackId(path), Url(path), title, Microseconds(status.DurationSeconds));
    }

    public static string? Url(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (MediaAddress.IsAddress(path)) return path.Trim();
        try { return new Uri(Path.GetFullPath(path)).AbsoluteUri; }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static string DisplayName(string path)
    {
        if (MediaAddress.IsAddress(path)) return path.Trim();
        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary><c>file://</c> yerel yola, ağ adresi kendine döner; başka şema <c>null</c>.</summary>
    public static string? PathFromUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;
        var text = uri.Trim();
        if (MediaAddress.IsAddress(text)) return text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed)) return null;
        return parsed.IsFile ? parsed.LocalPath : null;
    }

    /// <summary>
    /// MPRIS yöntemini denetim komutuna çevirir. <c>null</c>: yöntem bu durumda bir şey yapmaz (MPRIS
    /// bunu hata değil yok sayma olarak ister: yanlış iz kimliği, sınır dışı konum, sonraki iz yok).
    /// <paramref name="microseconds"/> <c>Seek</c>'te göreli, <c>SetPosition</c>'da mutlak konumdur.
    /// </summary>
    public static ControlCommand? Command(string member, ControlStatus? status, long microseconds = 0, string? argument = null)
    {
        var loaded = status?.PlayerPath is not null;
        switch (member)
        {
            case MethodPlay:
                return loaded ? new ControlCommand(ControlCommandNames.Play) : null;
            case MethodPause:
            case MethodStop:
                return loaded ? new ControlCommand(ControlCommandNames.Pause) : null;
            case MethodPlayPause:
                return loaded ? new ControlCommand(status!.Playing ? ControlCommandNames.Pause : ControlCommandNames.Play) : null;
            case MethodSeek:
            {
                if (!loaded) return null;
                var target = Math.Max(0, status!.PositionSeconds + Seconds(microseconds));
                if (status.DurationSeconds is { } length && length > 0) target = Math.Min(target, length);
                return new ControlCommand(ControlCommandNames.Seek) { Seconds = target };
            }
            case MethodSetPosition:
            {
                if (!loaded || !string.Equals(argument, TrackId(status!.PlayerPath), StringComparison.Ordinal)) return null;
                if (microseconds < 0) return null;
                var target = Seconds(microseconds);
                if (status.DurationSeconds is { } length && length > 0 && target > length) return null;
                return new ControlCommand(ControlCommandNames.Seek) { Seconds = target };
            }
            case MethodOpenUri:
                return PathFromUri(argument) is { } path ? new ControlCommand(ControlCommandNames.Open) { Path = path } : null;
            default:
                return null;
        }
    }

    /// <summary>Yöntem MPRIS'in Player arayüzünde var mı; yoksa yanıt <c>UnknownMethod</c> olur.</summary>
    public static bool IsPlayerMethod(string member) => member is MethodPlay or MethodPause or MethodPlayPause
        or MethodStop or MethodSeek or MethodSetPosition or MethodOpenUri or MethodNext or MethodPrevious;

    /// <summary>
    /// <c>PropertiesChanged</c> ile duyurulacak özellikler. <c>Position</c> MPRIS gereği burada yoktur;
    /// atlama <see cref="Seeked"/> ile ayrıca bildirilir.
    /// </summary>
    public static IReadOnlyList<string> Changed(ControlStatus? before, ControlStatus? after)
    {
        var list = new List<string>();
        if (!string.Equals(PlaybackStatus(before), PlaybackStatus(after), StringComparison.Ordinal)) list.Add(PropertyPlaybackStatus);
        if (Metadata(before) != Metadata(after)) list.Add(PropertyMetadata);
        return list;
    }

    public static readonly TimeSpan SeekTolerance = TimeSpan.FromSeconds(1.5);

    /// <summary>Aynı izde konum beklenenden <see cref="SeekTolerance"/>'tan fazla saptıysa atlama vardır.</summary>
    public static bool Seeked(ControlStatus? before, ControlStatus? after, TimeSpan elapsed)
    {
        if (before?.PlayerPath is null || after?.PlayerPath is null) return false;
        if (!string.Equals(before.PlayerPath, after.PlayerPath, StringComparison.Ordinal)) return false;
        var expected = before.PositionSeconds + (before.Playing ? elapsed.TotalSeconds : 0);
        return Math.Abs(after.PositionSeconds - expected) > SeekTolerance.TotalSeconds;
    }
}
