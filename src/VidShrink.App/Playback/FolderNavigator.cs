using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal static class FolderNavigator
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static readonly HashSet<string> VideoExtensions =
        new(ShellIntegration.MediaExtensions.Select(extension => "." + extension), StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AudioExtensions =
        new(ShellIntegration.AudioExtensions.Select(extension => "." + extension), StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<string> Siblings(string path)
    {
        if (MediaAddress.IsAddress(path)) return Array.Empty<string>();
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();
        var kind = ShellIntegration.IsAudio(path) ? AudioExtensions : VideoExtensions;

        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(file => kind.Contains(Path.GetExtension(file)))
                .OrderBy(file => Path.GetFileName(file), NaturalComparer.Instance)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static readonly EnumerationOptions Visible = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true
    };

    internal static IReadOnlyList<string> Collect(IEnumerable<string> paths)
    {
        var files = new List<string>();
        var folders = new List<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || MediaAddress.IsAddress(path)) continue;
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (File.Exists(full)) files.Add(full);
            else if (Directory.Exists(full)) folders.Add(Path.TrimEndingDirectorySeparator(full));
        }

        var result = new List<string>();
        var seen = new HashSet<string>(PathComparer);
        Gather(files, folders, result, seen);
        return result;
    }

    private static void Gather(IEnumerable<string> files, IEnumerable<string> folders, List<string> result, HashSet<string> seen)
    {
        foreach (var file in files.OrderBy(path => Path.GetFileName(path), NaturalComparer.Instance))
            if (IsPlayable(file) && seen.Add(file)) result.Add(file);

        foreach (var folder in folders.OrderBy(path => Path.GetFileName(path), NaturalComparer.Instance))
        {
            if (!seen.Add(folder + Path.DirectorySeparatorChar)) continue;
            try
            {
                var inner = Directory.EnumerateFiles(folder, "*", Visible).ToList();
                var below = Directory.EnumerateDirectories(folder, "*", Visible)
                    .Where(sub => !new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    .ToList();
                Gather(inner, below, result, seen);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool IsPlayable(string file)
    {
        var extension = Path.GetExtension(file);
        return VideoExtensions.Contains(extension) || AudioExtensions.Contains(extension);
    }

    internal static IReadOnlyList<string> Order(IReadOnlyList<string> files, bool shuffle, int seed)
    {
        if (!shuffle || files.Count < 2) return files;
        var order = files.ToArray();
        var random = new Random(seed);
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        return order;
    }

    internal static int IndexOf(IReadOnlyList<string> files, string current)
    {
        var full = PlayerView.IsAddress(current) ? current : Path.GetFullPath(current);
        var index = -1;
        for (var i = 0; i < files.Count; i++)
            if (PathComparer.Equals(files[i], full)) index = i;
        return index;
    }

    internal static string? Step(string current, bool forward, RepeatMode repeat, bool shuffle, int seed)
        => Step(Order(Siblings(current), shuffle, seed), current, forward, repeat);

    internal static string? Step(IReadOnlyList<string> files, string current, bool forward, RepeatMode repeat)
    {
        if (files.Count == 0) return null;

        var index = IndexOf(files, current);
        if (index < 0) return forward ? files[0] : files[^1];

        var next = index + (forward ? 1 : -1);
        if (next >= 0 && next < files.Count) return files[next];
        if (repeat != RepeatMode.All) return null;
        return files[(next + files.Count) % files.Count];
    }

    internal sealed class NaturalComparer : IComparer<string>
    {
        internal static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var i = 0;
            var j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var startX = i;
                    var startY = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    var digitsX = x[startX..i].TrimStart('0');
                    var digitsY = y[startY..j].TrimStart('0');
                    if (digitsX.Length != digitsY.Length) return digitsX.Length.CompareTo(digitsY.Length);
                    var byValue = string.CompareOrdinal(digitsX, digitsY);
                    if (byValue != 0) return byValue;
                    continue;
                }

                var byChar = char.ToLowerInvariant(x[i]).CompareTo(char.ToLowerInvariant(y[j]));
                if (byChar != 0) return byChar;
                i++;
                j++;
            }

            var byLength = (x.Length - i).CompareTo(y.Length - j);
            return byLength != 0 ? byLength : string.CompareOrdinal(x, y);
        }
    }
}
