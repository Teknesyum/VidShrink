using System.Globalization;

namespace VidShrink.Core;

public sealed record CameraMode(int Width, int Height, double Fps)
{
    private const double FrameIntervalUnits = 10_000_000;

    public string Size => Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture);

    public string Rate => Math.Round(Fps, 2).ToString("0.##", CultureInfo.InvariantCulture);

    public long FrameInterval => (long)Math.Round(FrameIntervalUnits / Fps);

    public string FrameRateArgument => (FrameIntervalUnits / (FrameInterval + 0.5)).ToString("0.######", CultureInfo.InvariantCulture);

    public bool IsValid => Width > 0 && Height > 0 && Fps > 0 && double.IsFinite(Fps);

    public override string ToString() => Size + "@" + Fps.ToString("0.######", CultureInfo.InvariantCulture);

    public static CameraMode? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var at = text.Split('@');
        if (at.Length != 2) return null;
        var size = at[0].Split('x');
        if (size.Length != 2) return null;
        if (!int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width)) return null;
        if (!int.TryParse(size[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height)) return null;
        if (!double.TryParse(at[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var fps)) return null;
        var mode = new CameraMode(width, height, fps);
        return mode.IsValid ? mode : null;
    }
}
