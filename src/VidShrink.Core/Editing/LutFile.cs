using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VidShrink.Core.Editing;

public sealed record LutCube(int Size, float[] DomainMin, float[] DomainMax, float[] Data);

public static class LutFile
{
    public const string Extension = ".cube";

    public const int MaxSize = 256;

    private static readonly UTF8Encoding Utf8 = new(false);

    internal static string? CacheFolderOverride { get; set; }

    public static string CacheFolder => CacheFolderOverride ?? Path.Combine(Path.GetTempPath(), "VidShrink", "lut");

    public static LutCube? Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Utf8)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static LutCube? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var size = 0;
        var min = new float[] { 0, 0, 0 };
        var max = new float[] { 1, 1, 1 };
        float[]? data = null;
        var count = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var key = parts[0].ToUpperInvariant();
            if (key == "TITLE") continue;
            if (key == "LUT_1D_SIZE") return null;
            if (key == "LUT_3D_SIZE")
            {
                if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out size) || size < 2 || size > MaxSize || data is not null)
                    return null;
                data = new float[size * size * size * 3];
                continue;
            }

            if (key is "DOMAIN_MIN" or "DOMAIN_MAX")
            {
                if (parts.Length < 4 || !Triple(parts, out var values)) return null;
                if (key == "DOMAIN_MIN") min = values;
                else max = values;
                continue;
            }

            if (char.IsLetter(parts[0][0])) continue;
            if (data is null || parts.Length < 3 || !Triple(parts, out var node) || count >= data.Length) return null;
            node.CopyTo(data, count);
            count += 3;
        }

        if (data is null || count != data.Length) return null;
        for (var i = 0; i < 3; i++)
            if (!(max[i] > min[i])) return null;
        return new LutCube(size, min, max, data);
    }

    public static string? Resolve(string? stored, string? folder)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        try
        {
            var candidates = new List<string>();
            if (Path.IsPathFullyQualified(stored)) candidates.Add(stored);
            else if (!string.IsNullOrEmpty(folder)) candidates.Add(Path.Combine(folder, stored));
            if (!string.IsNullOrEmpty(folder))
            {
                var name = stored.Replace('\\', '/').Split('/')[^1];
                if (name.Length > 0) candidates.Add(Path.Combine(folder, name));
            }

            foreach (var candidate in candidates)
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }

        return null;
    }

    public static string? ForMix(string path, int mix)
    {
        if (mix >= 100) return path;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;
            var key = info.FullName + "|" + info.Length.ToString(CultureInfo.InvariantCulture) + "|"
                + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + mix.ToString(CultureInfo.InvariantCulture);
            var hash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(key)), 0, 12).ToLowerInvariant();
            var target = Path.Combine(CacheFolder, hash + Extension);
            if (File.Exists(target)) return target;
            if (Read(path) is not { } cube) return null;
            Directory.CreateDirectory(CacheFolder);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, Write(Blend(cube, mix)), Utf8);
            File.Move(temp, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static LutCube Blend(LutCube cube, int mix)
    {
        var t = Math.Clamp(mix, 0, 100) / 100f;
        var n = cube.Size;
        var data = new float[cube.Data.Length];
        for (var i = 0; i < n * n * n; i++)
        {
            var index = new[] { i % n, i / n % n, i / (n * n) };
            for (var c = 0; c < 3; c++)
            {
                var identity = cube.DomainMin[c] + (cube.DomainMax[c] - cube.DomainMin[c]) * index[c] / (n - 1);
                var value = cube.Data[i * 3 + c];
                data[i * 3 + c] = identity + (value - identity) * t;
            }
        }

        return cube with { Data = data };
    }

    public static string Write(LutCube cube)
    {
        var text = new StringBuilder();
        text.Append("LUT_3D_SIZE ").Append(cube.Size.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("DOMAIN_MIN ").Append(Triple(cube.DomainMin, 0)).Append('\n');
        text.Append("DOMAIN_MAX ").Append(Triple(cube.DomainMax, 0)).Append('\n');
        for (var i = 0; i < cube.Data.Length; i += 3) text.Append(Triple(cube.Data, i)).Append('\n');
        return text.ToString();
    }

    public static string FilterPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Escape(Escape(path, "\\':"), "\\'[],;");
    }

    private static string Escape(string text, string special)
    {
        var result = new StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            if (special.Contains(ch)) result.Append('\\');
            result.Append(ch);
        }

        return result.ToString();
    }

    private static bool Triple(string[] parts, out float[] values)
    {
        values = new float[3];
        var offset = parts.Length >= 4 && char.IsLetter(parts[0][0]) ? 1 : 0;
        for (var i = 0; i < 3; i++)
            if (!float.TryParse(parts[offset + i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !float.IsFinite(values[i]))
                return false;
        return true;
    }

    private static string Triple(float[] values, int at)
        => string.Join(" ", values.Skip(at).Take(3).Select(v => v.ToString("0.######", CultureInfo.InvariantCulture)));
}
