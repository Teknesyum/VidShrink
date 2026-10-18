using System.Collections;
using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

public enum KeyframeEase
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
}

public interface IClipKeyframe<TSelf> where TSelf : struct, IClipKeyframe<TSelf>
{
    long Offset { get; }

    KeyframeEase Ease { get; }

    TSelf At(long offset);

    static abstract TSelf Blend(TSelf from, TSelf to, double amount, long offset);
}

public readonly record struct VolumeKey(long Offset, double Db, KeyframeEase Ease = KeyframeEase.Linear) : IClipKeyframe<VolumeKey>
{
    public VolumeKey At(long offset) => this with { Offset = offset };

    public static VolumeKey Blend(VolumeKey from, VolumeKey to, double amount, long offset)
        => new(offset, from.Db + (to.Db - from.Db) * amount, from.Ease);
}

public readonly record struct CropKey(long Offset, double Left, double Top, double Right, double Bottom, KeyframeEase Ease = KeyframeEase.Linear) : IClipKeyframe<CropKey>
{
    public CropKey At(long offset) => this with { Offset = offset };

    public static CropKey Blend(CropKey from, CropKey to, double amount, long offset)
        => new(offset,
            from.Left + (to.Left - from.Left) * amount,
            from.Top + (to.Top - from.Top) * amount,
            from.Right + (to.Right - from.Right) * amount,
            from.Bottom + (to.Bottom - from.Bottom) * amount,
            from.Ease);
}

public sealed class KeyframeTrack<T> : IReadOnlyList<T>, IEquatable<KeyframeTrack<T>> where T : struct, IClipKeyframe<T>
{
    private readonly T[] _keys;

    public static KeyframeTrack<T> Empty { get; } = new(Array.Empty<T>());

    public KeyframeTrack(IEnumerable<T> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var byOffset = new SortedDictionary<long, T>();
        foreach (var key in keys)
        {
            var offset = Math.Max(0, key.Offset);
            byOffset[offset] = key.At(offset);
        }

        _keys = byOffset.Values.ToArray();
    }

    public int Count => _keys.Length;

    public T this[int index] => _keys[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_keys).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public KeyframeTrack<T> With(T key) => new(_keys.Where(k => k.Offset != Math.Max(0, key.Offset)).Append(key));

    public KeyframeTrack<T> Map(Func<T, T> change) => new(_keys.Select(change));

    public static double Shape(KeyframeEase ease, double progress)
    {
        var p = Math.Clamp(progress, 0, 1);
        return ease switch
        {
            KeyframeEase.EaseIn => p * p,
            KeyframeEase.EaseOut => p * (2 - p),
            KeyframeEase.EaseInOut => p * p * (3 - 2 * p),
            _ => p,
        };
    }

    public static string ShapeExpression(KeyframeEase ease, string p) => ease switch
    {
        KeyframeEase.EaseIn => p + "*" + p,
        KeyframeEase.EaseOut => p + "*(2-" + p + ")",
        KeyframeEase.EaseInOut => p + "*" + p + "*(3-2*" + p + ")",
        _ => p,
    };

    public T ValueAt(long offset)
    {
        if (_keys.Length == 0) throw new InvalidOperationException("Anahtar kare yok");
        if (offset <= _keys[0].Offset) return _keys[0].At(offset);
        for (var i = 0; i < _keys.Length - 1; i++)
        {
            var a = _keys[i];
            var b = _keys[i + 1];
            if (offset >= b.Offset) continue;
            var progress = (double)(offset - a.Offset) / (b.Offset - a.Offset);
            return T.Blend(a, b, Shape(a.Ease, progress), offset);
        }

        return _keys[^1].At(offset);
    }

    public KeyframeTrack<T> Slice(long from, long to)
    {
        if (_keys.Length == 0 || to <= from) return this;
        var inner = _keys.Where(k => k.Offset > from && k.Offset < to).Select(k => k.At(k.Offset - from));
        return new KeyframeTrack<T>(inner.Prepend(ValueAt(from).At(0)).Append(ValueAt(to).At(to - from)));
    }

    public KeyframeTrack<T> Scaled(double factor)
        => _keys.Length == 0 || factor == 1 ? this : new KeyframeTrack<T>(_keys.Select(k => k.At((long)Math.Round(k.Offset * factor, MidpointRounding.AwayFromZero))));

    public string Expression(Func<T, double> pick, string time)
    {
        if (_keys.Length == 0) throw new InvalidOperationException("Anahtar kare yok");
        var t = "(" + time + ")";
        if (_keys.Length == 1) return Number(pick(_keys[0]));

        var text = new StringBuilder();
        for (var i = 0; i < _keys.Length - 1; i++)
        {
            var a = _keys[i];
            var b = _keys[i + 1];
            var start = EditTime.ToSeconds(a.Offset);
            var length = EditTime.ToSeconds(b.Offset - a.Offset);
            var va = pick(a);
            var delta = pick(b) - va;
            var p = "clip((" + t + "-" + Number(start) + ")/" + Number(length) + ",0,1)";
            var segment = delta == 0 ? Number(va) : Number(va) + "+" + Number(delta) + "*(" + ShapeExpression(a.Ease, p) + ")";
            text.Append("if(lt(").Append(t).Append(',').Append(Number(EditTime.ToSeconds(b.Offset))).Append("),").Append(segment).Append(',');
        }

        text.Append(Number(pick(_keys[^1]))).Append(')', _keys.Length - 1);
        return text.ToString();
    }

    public bool Equals(KeyframeTrack<T>? other) => other is not null && _keys.AsSpan().SequenceEqual(other._keys);

    public override bool Equals(object? obj) => obj is KeyframeTrack<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var key in _keys) hash.Add(key);
        return hash.ToHashCode();
    }

    private static string Number(double value)
    {
        var text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return value < 0 ? "(" + text + ")" : text;
    }
}

public static class KeyframeEaseKeys
{
    private static readonly string[] Keys = { "linear", "in", "out", "in-out" };

    public static string Key(KeyframeEase ease) => Keys[(int)ease];

    public static KeyframeEase Parse(string? key)
    {
        var index = Array.IndexOf(Keys, key);
        return index < 0 ? KeyframeEase.Linear : (KeyframeEase)index;
    }
}
