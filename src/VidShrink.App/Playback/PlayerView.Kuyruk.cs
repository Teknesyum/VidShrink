using System;
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

    private bool _queueArranged;

    internal IReadOnlyList<string>? Queue => _queue;

    internal string? QueueSource { get; private set; }

    private string ResolveQueue(string path)
    {
        if (PlaylistFile.IsPlaylist(path))
        {
            var entries = PlaylistFile.ReadPlayable(path);
            if (entries.Count == 0) throw new PlaybackOpenException(Path.GetFileName(path));
            _queue = entries;
            _queueArranged = false;
            QueueSource = Path.GetFullPath(path);
            _trace.Add("queue " + entries.Count);
            return entries[0];
        }

        if (_queue is not null && FolderNavigator.IndexOf(_queue, path) < 0)
        {
            _queue = null;
            _queueArranged = false;
            QueueSource = null;
        }

        return path;
    }

    private IReadOnlyList<string> NavigationList(string path)
        => _queue is { } queue && _queueArranged
            ? queue
            : FolderNavigator.Order(_queue ?? FolderNavigator.Siblings(path), _settings.Shuffle, _shuffleSeed);

    internal bool Enqueue(string file) => EditQueue(file, QueueEdit.Append, "enqueue ");

    internal bool PlayNext(string file) => EditQueue(file, QueueEdit.InsertNext, "playnext ");

    internal bool RemoveFromList(string file) => EditQueue(file, QueueEdit.Remove, "remove ");

    private bool EditQueue(string file, Func<IReadOnlyList<string>, string, string, IReadOnlyList<string>?> edit, string trace)
    {
        if (_path is not { } current) return false;
        if (edit(PlaylistFiles(), current, file) is not { } next) return false;
        _queue = next;
        _queueArranged = true;
        _trace.Add(trace + Path.GetFileName(file));
        return true;
    }

    private string? StepFrom(string path, bool forward)
        => FolderNavigator.Step(NavigationList(path), path, forward, _settings.Repeat);
}

internal static class QueueEdit
{
    internal static IReadOnlyList<string>? Append(IReadOnlyList<string> list, string current, string file)
    {
        if (Same(file, current)) return null;
        var rest = Without(list, file);
        rest.Add(file);
        return rest;
    }

    internal static IReadOnlyList<string>? InsertNext(IReadOnlyList<string> list, string current, string file)
    {
        if (Same(file, current)) return null;
        var rest = Without(list, file);
        rest.Insert(FolderNavigator.IndexOf(rest, current) + 1, file);
        return rest;
    }

    internal static IReadOnlyList<string>? Remove(IReadOnlyList<string> list, string current, string file)
    {
        if (Same(file, current) || FolderNavigator.IndexOf(list, file) < 0) return null;
        return Without(list, file);
    }

    internal static bool Same(string file, string current) => FolderNavigator.IndexOf(new[] { file }, current) == 0;

    private static List<string> Without(IReadOnlyList<string> list, string file)
    {
        var rest = new List<string>(list);
        for (var index = FolderNavigator.IndexOf(rest, file); index >= 0; index = FolderNavigator.IndexOf(rest, file))
            rest.RemoveAt(index);
        return rest;
    }
}
