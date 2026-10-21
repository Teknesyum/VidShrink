using System.Globalization;
using System.Text.Json;

namespace VidShrink.Core.Editing;

public readonly record struct TransformKey(long Offset, double Scale, double OffsetX, double OffsetY, double Angle, KeyframeEase Ease = KeyframeEase.Linear) : IClipKeyframe<TransformKey>
{
    public TransformKey At(long offset) => this with { Offset = offset };

    public TransformKey WithEase(KeyframeEase ease) => this with { Ease = ease };

    public static TransformKey Blend(TransformKey from, TransformKey to, double amount, long offset)
        => new(offset,
            from.Scale + (to.Scale - from.Scale) * amount,
            from.OffsetX + (to.OffsetX - from.OffsetX) * amount,
            from.OffsetY + (to.OffsetY - from.OffsetY) * amount,
            from.Angle + (to.Angle - from.Angle) * amount,
            from.Ease);
}

public sealed partial record ClipEffects
{
    public const double MaxKeyAngle = 360;

    public KeyframeTrack<TransformKey> TransformKeys { get; init; } = KeyframeTrack<TransformKey>.Empty;

    public bool HasMovingTransform => TransformKeys.Count > 1;

    public ClipEffects MirroredKeys(long length)
        => HasKeyframes
            ? this with { VolumeKeys = VolumeKeys.Mirrored(length), CropKeys = CropKeys.Mirrored(length), TransformKeys = TransformKeys.Mirrored(length) }
            : this;

    public TransformKey StillTransform
        => TransformKeys.Count > 0 ? TransformKeys[0] : new TransformKey(0, Scale, OffsetX, OffsetY, Angle);

    private static TransformKey TransformEdges(TransformKey key)
        => key with
        {
            Scale = double.IsFinite(key.Scale) ? Math.Round(Math.Clamp(key.Scale, MinScale, MaxScale), 3) : 1,
            OffsetX = Offset(key.OffsetX),
            OffsetY = Offset(key.OffsetY),
            Angle = double.IsFinite(key.Angle) ? Math.Round(Math.Clamp(key.Angle, -MaxKeyAngle, MaxKeyAngle), 1) : 0,
        };
}

public static class ClipMotion
{
    public static IReadOnlyList<string> Filters(KeyframeTrack<TransformKey> keys, (int Width, int Height) size, (int Width, int Height) target, string time)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0) throw new ArgumentException("Anahtar kare yok", nameof(keys));

        var fit = Math.Min((double)target.Width / size.Width, (double)target.Height / size.Height);
        var baseWidth = size.Width * fit;
        var baseHeight = size.Height * fit;
        var scale = keys.Expression(k => k.Scale, time);
        var parts = new List<string>
        {
            "scale=w='max(2,2*round(" + Number(baseWidth) + "*(" + scale + ")/2))':h='max(2,2*round(" + Number(baseHeight) + "*(" + scale + ")/2))':eval=frame",
        };

        var top = keys.Max(k => k.Scale);
        var turns = keys.Any(k => k.Angle != 0);
        var contentWidth = baseWidth * top;
        var contentHeight = baseHeight * top;
        if (turns) contentWidth = contentHeight = Math.Sqrt(contentWidth * contentWidth + contentHeight * contentHeight);
        var boxWidth = EvenUp(Math.Max(contentWidth, target.Width * (1 + 2 * keys.Max(k => Math.Abs(k.OffsetX))))) + 2;
        var boxHeight = EvenUp(Math.Max(contentHeight, target.Height * (1 + 2 * keys.Max(k => Math.Abs(k.OffsetY))))) + 2;
        parts.Add("pad=w=" + Int(boxWidth) + ":h=" + Int(boxHeight) + ":x='(ow-iw)/2':y='(oh-ih)/2':eval=frame");
        if (turns)
            parts.Add("rotate=a='(" + keys.Expression(k => k.Angle, time) + ")*PI/180':ow=" + Int(boxWidth) + ":oh=" + Int(boxHeight) + ":c=black");

        var x = Number((boxWidth - target.Width) / 2.0) + "-(" + keys.Expression(k => k.OffsetX, time) + ")*" + Int(target.Width);
        var y = Number((boxHeight - target.Height) / 2.0) + "-(" + keys.Expression(k => k.OffsetY, time) + ")*" + Int(target.Height);
        parts.Add("crop=w=" + Int(target.Width) + ":h=" + Int(target.Height) + ":x='" + x + "':y='" + y + "'");
        return parts;
    }

    internal static void Write(Utf8JsonWriter writer, ClipEffects effects)
    {
        if (effects.TransformKeys.Count == 0) return;
        writer.WriteStartArray("transformKeys");
        foreach (var key in effects.TransformKeys)
        {
            writer.WriteStartObject();
            writer.WriteNumber("offset", key.Offset);
            writer.WriteNumber("scale", key.Scale);
            writer.WriteNumber("offsetX", key.OffsetX);
            writer.WriteNumber("offsetY", key.OffsetY);
            writer.WriteNumber("angle", key.Angle);
            writer.WriteString("ease", KeyframeEaseKeys.Key(key.Ease));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    internal static ClipEffects Read(JsonElement e, ClipEffects effects)
    {
        if (!e.TryGetProperty("transformKeys", out var keys) || keys.ValueKind != JsonValueKind.Array) return effects;
        return effects with
        {
            TransformKeys = new KeyframeTrack<TransformKey>(keys.EnumerateArray().Where(k => k.ValueKind == JsonValueKind.Object).Select(k => new TransformKey(
                k.TryGetProperty("offset", out var offset) && offset.ValueKind == JsonValueKind.Number ? offset.GetInt64() : 0,
                Field(k, "scale", 1), Field(k, "offsetX", 0), Field(k, "offsetY", 0), Field(k, "angle", 0),
                KeyframeEaseKeys.Parse(k.TryGetProperty("ease", out var ease) && ease.ValueKind == JsonValueKind.String ? ease.GetString() : null)))),
        };
    }

    private static double Field(JsonElement key, string name, double fallback)
        => key.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : fallback;

    private static int EvenUp(double value) => Math.Max(2, (int)Math.Ceiling(value / 2 - 1e-9) * 2);

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value)
    {
        var text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return value < 0 ? "(" + text + ")" : text;
    }
}
