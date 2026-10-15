using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.App.Playback;

/// <summary>
/// Çalma listesi kuyruğu: m3u/pls/wpl/asx açılınca girdileri kuyruğa alınır ve ilk girdi
/// çalar; sonraki/önceki, sonda otomatik geçiş ve açılır liste klasör yerine kuyruğu izler.
/// Kuyrukta olmayan bir dosya açılınca kuyruk düşer, oynatıcı klasör gezintisine döner.
/// Liste mpv'ye verilmez: mpv'nin kendi listesi arayüzün yolunu ve kartını güncellemiyor.
/// Uzak adres liste uzantılı olsa da (HLS, radyo pls) kuyruk kurmadan olduğu gibi mpv'ye gider.
/// </summary>
internal partial class PlayerView
{
    private IReadOnlyList<string>? _queue;

    private bool _queueArranged;

    internal IReadOnlyList<string>? Queue => _queue;

    internal string? QueueSource { get; private set; }

    private string ResolveQueue(string path)
    {
        if (!MediaAddress.IsAddress(path) && PlaylistFile.IsPlaylist(path))
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

    internal bool OpenFolder(string folder) => OpenCollected(new[] { folder }, "folder -> ");

    internal bool OpenDroppedItems(IReadOnlyList<string> paths) => OpenCollected(paths, "drop -> ");

    private bool OpenCollected(IReadOnlyList<string> paths, string trace)
    {
        var files = FolderNavigator.Collect(paths);
        return files.Count > 0 && PlayCollected(files, trace);
    }

    /// <summary>
    /// Kabuktan art arda gelen yolların toplu açılışı. Çalan dosya toplanan listedeyse
    /// oynatma kesilmez, liste yalnız kuyruk olur; değilse kuyruk ilk dosyadan başlar.
    /// </summary>
    internal bool OpenShellBatch(IReadOnlyList<string> paths)
    {
        var files = FolderNavigator.Collect(paths);
        if (files.Count == 0) return false;
        if (_path is not { } current || !files.Any(file => QueueEdit.Same(file, current))) return PlayCollected(files, "shell -> ");
        _queue = files;
        _queueArranged = false;
        QueueSource = null;
        _trace.Add("shell -> " + files.Count);
        _trace.Add("queue " + files.Count);
        return true;
    }

    /// <summary>
    /// Kabuğun "sıraya ekle" fiili: toplanan dosyalar çalan listenin sonuna eklenir, oynatma
    /// kesilmez. Hiçbir şey çalmıyorsa liste kuyruk olarak açılır ve ilki çalar.
    /// </summary>
    internal bool EnqueueItems(IReadOnlyList<string> paths)
    {
        var files = FolderNavigator.Collect(paths);
        if (files.Count == 0) return false;
        if (_path is null) return PlayCollected(files, "enqueue -> ");
        var added = false;
        foreach (var file in files) added |= Enqueue(file);
        return added;
    }

    private bool PlayCollected(IReadOnlyList<string> files, string trace)
    {
        _queue = files;
        _queueArranged = false;
        QueueSource = null;
        _trace.Add(trace + files.Count);
        _trace.Add("queue " + files.Count);
        _navigation = OpenQuietlyAsync(files[0]);
        return true;
    }

    internal bool Enqueue(string file) => EditQueue(file, QueueEdit.Append, "enqueue ");

    internal bool PlayNext(string file) => EditQueue(file, QueueEdit.InsertNext, "playnext ");

    internal bool RemoveFromList(string file) => EditQueue(file, QueueEdit.Remove, "remove ");

    internal bool MoveUp(string file) => EditQueue(file, QueueEdit.MoveUp, "moveup ");

    internal bool MoveDown(string file) => EditQueue(file, QueueEdit.MoveDown, "movedown ");

    internal bool SortPlaylist(PlaylistSort sort)
    {
        if (_path is not { } current) return false;
        Func<IReadOnlyList<string>, string, string, IReadOnlyList<string>?> edit = sort switch
        {
            PlaylistSort.Name => QueueEdit.SortByName,
            PlaylistSort.Date => QueueEdit.SortByDate,
            _ => QueueEdit.Reverse,
        };
        return EditQueue(current, edit, "sort " + sort.ToString().ToLowerInvariant() + " ");
    }

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

internal enum PlaylistSort
{
    Name,
    Date,
    Reverse,
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

    internal static IReadOnlyList<string>? MoveUp(IReadOnlyList<string> list, string current, string file)
        => Move(list, file, -1);

    internal static IReadOnlyList<string>? MoveDown(IReadOnlyList<string> list, string current, string file)
        => Move(list, file, 1);

    internal static IReadOnlyList<string>? SortByName(IReadOnlyList<string> list, string current, string file)
        => list.OrderBy(PlayerView.MediaName, FolderNavigator.NaturalComparer.Instance).ToList();

    internal static IReadOnlyList<string>? SortByDate(IReadOnlyList<string> list, string current, string file)
        => list.OrderByDescending(FolderNavigator.Modified)
            .ThenBy(PlayerView.MediaName, FolderNavigator.NaturalComparer.Instance)
            .ToList();

    internal static IReadOnlyList<string>? Reverse(IReadOnlyList<string> list, string current, string file)
        => list.Reverse().ToList();

    private static IReadOnlyList<string>? Move(IReadOnlyList<string> list, string file, int step)
    {
        var index = FolderNavigator.IndexOf(list, file);
        var target = index + step;
        if (index < 0 || target < 0 || target >= list.Count) return null;
        var moved = new List<string>(list);
        (moved[index], moved[target]) = (moved[target], moved[index]);
        return moved;
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
