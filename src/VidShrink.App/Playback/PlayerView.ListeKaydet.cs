using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// O anki oynatma listesini (kuyruk ya da klasör sırası, görünen sırayla) m3u8 dosyasına yazar.
/// Seçici yalnız yolu sorar; yazma <see cref="SavePlaylist"/>'te, hata durum satırına düşer.
/// </summary>
internal partial class PlayerView
{
    internal const string PlaylistSaveExtension = "m3u8";

    internal bool CanSavePlaylist => PlaylistFiles().Count > 0;

    internal bool SavePlaylist(string path)
    {
        var files = PlaylistFiles();
        var saved = false;
        if (files.Count > 0)
        {
            try
            {
                PlaylistFile.Write(path, files.Select(PlaylistEntryFor).ToList());
                saved = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        _notice = saved ? Strings.Get("player.list.saved", path) : Strings.Get("player.list.save-failed");
        _trace.Add("listsave -> " + (saved ? files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no"));
        RefreshState();
        return saved;
    }

    internal async Task PickPlaylistTargetAsync()
    {
        if (!CanSavePlaylist || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) return;
        string? path = null;
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.Get("player.list.save"),
                SuggestedFileName = PlaylistSaveName(),
                DefaultExtension = PlaylistSaveExtension,
                ShowOverwritePrompt = true,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(Strings.Get("player.list.files")) { Patterns = new[] { "*." + PlaylistSaveExtension } }
                }
            }).ConfigureAwait(true);
            path = file?.TryGetLocalPath();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }

        if (path is not null) SavePlaylist(path);
    }

    internal string PlaylistSaveName()
    {
        var name = QueueSource is { } source ? Path.GetFileNameWithoutExtension(source)
            : _path is { } path && !IsAddress(path) && Path.GetDirectoryName(Path.GetFullPath(path)) is { } folder ? Path.GetFileName(folder)
            : "";
        return (string.IsNullOrWhiteSpace(name) ? Strings.Get("player.list.files") : name) + "." + PlaylistSaveExtension;
    }

    private PlaylistEntry PlaylistEntryFor(string file)
    {
        if (_path is not { } current || !QueueEdit.Same(file, current) || _engine is not { IsOpen: true } engine) return new PlaylistEntry(file);
        var tags = engine.Tags;
        var title = string.IsNullOrWhiteSpace(tags.Title) ? null
            : string.IsNullOrWhiteSpace(tags.Artist) ? tags.Title
            : tags.Artist + " - " + tags.Title;
        var seconds = engine.DurationSeconds;
        return new PlaylistEntry(file, double.IsFinite(seconds) && seconds > 0 ? seconds : null, title);
    }

    private MenuItem SavePlaylistRow() => ItemRow("save", "player.list.save", CanSavePlaylist, () =>
    {
        CloseMenus();
        _ = PickPlaylistTargetAsync();
    });
}
