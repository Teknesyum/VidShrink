using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    /// <summary>
    /// ARC-01: bilgi panosundaki "tum ayrintilar" dugmesinin dosyasi. Adresten acilan yayinda ve
    /// diskte olmayan dosyada <c>null</c>; dugme o zaman gizlenir, cunku ffprobe yerel dosya okur.
    /// </summary>
    internal string? DetailsPath => _path is { } path && !OnAddress && File.Exists(path) ? path : null;

    /// <summary>Medya bilgisi penceresini acar; dosya pencere acildiktan sonra yoklanir.</summary>
    internal MediaInfoWindow? ShowMediaDetails()
    {
        if (DetailsPath is not { } path) return null;
        var window = new MediaInfoWindow(path, null);
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
        _ = window.LoadAsync();
        _trace.Add("mediainfo -> open");
        return window;
    }

    private void OnInfoDetails(object? sender, RoutedEventArgs e)
    {
        ShowMediaDetails();
        e.Handled = true;
    }
}
