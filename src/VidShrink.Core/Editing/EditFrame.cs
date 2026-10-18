namespace VidShrink.Core.Editing;

public readonly record struct EditFrame(int Num, int Den)
{
    public const double MaxRatio = 4;

    public static EditFrame Source => default;

    public bool IsSource => Num <= 0 || Den <= 0;

    public EditFrame Normalized()
        => !IsSource && (double)Num / Den is >= 1 / MaxRatio and <= MaxRatio ? this : Source;

    public (int Width, int Height) Size(int width, int height)
    {
        var side = Math.Max(2, Math.Min(width, height) / 2 * 2);
        var ratio = (double)Num / Den;
        return ratio >= 1 ? (Even(side * ratio), side) : (side, Even(side / ratio));
    }

    public override string ToString() => IsSource ? "source" : Num + ":" + Den;

    private static int Even(double value) => Math.Max(2, (int)Math.Round(value / 2, MidpointRounding.AwayFromZero) * 2);
}
