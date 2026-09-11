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

    private static readonly HashSet<string> Extensions =
        new(ShellIntegration.MediaExtensions.Select(extension => "." + extension), StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<string> Siblings(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return Array.Empty<string>();

        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(file => Extensions.Contains(Path.GetExtension(file)))
                .OrderBy(file => Path.GetFileName(file), NaturalComparer.Instance)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
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

    internal static string? Step(string current, bool forward, RepeatMode repeat, bool shuffle, int seed)
    {
        var files = Order(Siblings(current), shuffle, seed);
        if (files.Count == 0) return null;

        var full = Path.GetFullPath(current);
        var index = -1;
        for (var i = 0; i < files.Count; i++)
            if (PathComparer.Equals(files[i], full)) index = i;

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
