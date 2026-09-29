using System.Collections.Generic;
using System.IO;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.App.Playback;

/// <summary>
/// Çalma listesi kuyruğu: m3u/pls/wpl/asx açılınca girdileri kuyruğa alınır ve ilk girdi
/// çalar; sonraki/önceki, sonda otomatik geçiş ve açılır liste klasör yerine kuyruğu izler.
/// Kuyrukta olmayan bir dosya açılınca kuyruk düşer, oynatıcı klasör gezintisine döner.
/// Liste mpv'ye verilmez: mpv'nin kendi listesi arayüzün yolunu ve kartını güncellemiyor.
/// </summary>
internal partial class PlayerView
{
    private IReadOnlyList<string>? _queue;

    internal IReadOnlyList<string>? Queue => _queue;

    internal string? QueueSource { get; private set; }

    private string ResolveQueue(string path)
    {
        if (PlaylistFile.IsPlaylist(path))
        {
            var entries = PlaylistFile.ReadPlayable(path);
            if (entries.Count == 0) throw new PlaybackOpenException(Path.GetFileName(path));
            _queue = entries;
            QueueSource = Path.GetFullPath(path);
            _trace.Add("queue " + entries.Count);
            return entries[0];
        }

        if (_queue is not null && FolderNavigator.IndexOf(_queue, path) < 0)
        {
            _queue = null;
            QueueSource = null;
        }

        return path;
    }

    private IReadOnlyList<string> NavigationList(string path)
        => FolderNavigator.Order(_queue ?? FolderNavigator.Siblings(path), _settings.Shuffle, _shuffleSeed);

    private string? StepFrom(string path, bool forward)
        => FolderNavigator.Step(NavigationList(path), path, forward, _settings.Repeat);
}
