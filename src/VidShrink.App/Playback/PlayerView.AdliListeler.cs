using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-09: adlı oynatma listeleri ayar klasörünün altında durur (<see cref="NamedPlaylists.FolderName"/>).
/// Liste açılınca m3u8 dosyası olarak açılır; kuyruk yolu eksik dosyaları atlar, liste dosyası değişmez.
/// </summary>
internal partial class PlayerView
{
    internal NamedPlaylists? PlaylistStore() => NamedPlaylists.ForSettingsFolder(SettingsFolder());

    internal IReadOnlyList<PlaylistEntry> CurrentPlaylistEntries() => PlaylistFiles().Select(PlaylistEntryFor).ToList();

    internal bool OpenNamedPlaylist(string file)
    {
        if (!File.Exists(file))
        {
            _notice = Strings.Get("player.list.missing");
            RefreshState();
            return false;
        }

        _trace.Add("namedlist -> " + Path.GetFileNameWithoutExtension(file));
        _navigation = OpenQuietlyAsync(file);
        return true;
    }

    internal PlaylistsWindow? ShowPlaylists()
    {
        if (PlaylistStore() is not { } store)
        {
            _trace.Add("namedlists -> no");
            return null;
        }

        var window = new PlaylistsWindow(store, CurrentPlaylistEntries, OpenNamedPlaylist, text =>
        {
            _notice = text;
            RefreshState();
        });
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
        _trace.Add("namedlists -> " + store.List().Count);
        return window;
    }

    private MenuItem PlaylistsRow()
    {
        var item = new MenuItem { Header = Strings.Get("player.playlists.menu"), Tag = "playlists", IsEnabled = SettingsFolder() is not null };
        item.Click += (_, _) =>
        {
            CloseMenus();
            ShowPlaylists();
        };
        return item;
    }
}
