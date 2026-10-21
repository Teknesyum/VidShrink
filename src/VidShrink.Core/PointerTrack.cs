using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VidShrink.Core;

public readonly record struct PointerSample(long Ms, int X, int Y);

public readonly record struct PointerPress(long Ms, int X, int Y, int Button);

public sealed record PointerTrack(int Width, int Height, IReadOnlyList<PointerSample> Moves, IReadOnlyList<PointerPress> Clicks, IReadOnlyList<long> Keys)
{
    public const int Version = 1;

    public const string Suffix = ".pointer.json";

    public static string SidecarOf(string video)
    {
        var folder = Path.GetDirectoryName(video) ?? string.Empty;
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(video) + Suffix);
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", Version);
            writer.WriteNumber("w", Width);
            writer.WriteNumber("h", Height);
            writer.WriteStartArray("moves");
            foreach (var move in Moves)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(move.Ms);
                writer.WriteNumberValue(move.X);
                writer.WriteNumberValue(move.Y);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("clicks");
            foreach (var click in Clicks)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(click.Ms);
                writer.WriteNumberValue(click.X);
                writer.WriteNumberValue(click.Y);
                writer.WriteNumberValue(click.Button);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("keys");
            foreach (var key in Keys) writer.WriteNumberValue(key);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static PointerTrack? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var version) || version.GetInt32() != Version
                || !root.TryGetProperty("w", out var w) || !root.TryGetProperty("h", out var h)) return null;
            var width = w.GetInt32();
            var height = h.GetInt32();
            if (width <= 0 || height <= 0) return null;

            var moves = new List<PointerSample>();
            foreach (var item in Items(root, "moves"))
                if (item.GetArrayLength() >= 3) moves.Add(new PointerSample(item[0].GetInt64(), item[1].GetInt32(), item[2].GetInt32()));
            var clicks = new List<PointerPress>();
            foreach (var item in Items(root, "clicks"))
                if (item.GetArrayLength() >= 4) clicks.Add(new PointerPress(item[0].GetInt64(), item[1].GetInt32(), item[2].GetInt32(), item[3].GetInt32()));
            var keys = new List<long>();
            foreach (var item in Items(root, "keys")) keys.Add(item.GetInt64());

            moves.Sort((a, b) => a.Ms.CompareTo(b.Ms));
            clicks.Sort((a, b) => a.Ms.CompareTo(b.Ms));
            keys.Sort();
            return new PointerTrack(width, height, moves, clicks, keys);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Items(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in array.EnumerateArray())
            if (item.ValueKind is JsonValueKind.Array or JsonValueKind.Number) yield return item;
    }

    public void Save(string path) => File.WriteAllText(path, ToJson(), new UTF8Encoding(false));

    public static PointerTrack? Load(string path)
    {
        try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public (int X, int Y)? PositionAt(long ms)
    {
        if (Moves.Count == 0) return null;
        int low = 0, high = Moves.Count - 1, found = 0;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (Moves[mid].Ms <= ms) { found = mid; low = mid + 1; }
            else high = mid - 1;
        }

        return (Moves[found].X, Moves[found].Y);
    }

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height} moves={Moves.Count} clicks={Clicks.Count} keys={Keys.Count}");
}

public sealed class PointerTrackRecorder
{
    public const int SampleHz = 30;

    private readonly object _gate = new();
    private readonly int _originX;
    private readonly int _originY;
    private readonly int _width;
    private readonly int _height;
    private readonly Func<long> _clock;
    private readonly List<PointerSample> _moves = new();
    private readonly List<PointerPress> _clicks = new();
    private readonly List<long> _keys = new();
    private long _runningBefore;
    private long _resumedAt;
    private bool _running;
    private long _offset;
    private bool _synced;

    public PointerTrackRecorder(int originX, int originY, int width, int height, Func<long> clockMs)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        _originX = originX;
        _originY = originY;
        _width = width;
        _height = height;
        _clock = clockMs ?? throw new ArgumentNullException(nameof(clockMs));
        _resumedAt = _clock();
        _running = true;
    }

    public bool Running
    {
        get { lock (_gate) return _running; }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_running) return;
            _runningBefore += _clock() - _resumedAt;
            _running = false;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_running) return;
            _resumedAt = _clock();
            _running = true;
        }
    }

    public void Sync(TimeSpan captured)
    {
        lock (_gate)
        {
            if (_synced || !_running) return;
            _synced = true;
            _offset = Math.Max(0, (long)captured.TotalMilliseconds - RunningMs());
        }
    }

    public void Move(int screenX, int screenY)
    {
        lock (_gate)
        {
            if (!_running) return;
            var x = screenX - _originX;
            var y = screenY - _originY;
            if (_moves.Count > 0 && _moves[^1].X == x && _moves[^1].Y == y) return;
            _moves.Add(new PointerSample(Now(), x, y));
        }
    }

    public void Click(int screenX, int screenY, int button)
    {
        lock (_gate)
        {
            if (!_running) return;
            _clicks.Add(new PointerPress(Now(), screenX - _originX, screenY - _originY, button));
        }
    }

    public void Key()
    {
        lock (_gate)
        {
            if (_running) _keys.Add(Now());
        }
    }

    public PointerTrack Build()
    {
        lock (_gate) return new PointerTrack(_width, _height, _moves.ToArray(), _clicks.ToArray(), _keys.ToArray());
    }

    private long RunningMs() => _runningBefore + (_running ? _clock() - _resumedAt : 0);

    private long Now() => RunningMs() + _offset;
}
