using VidShrink.Core;
using VidShrink.Ffmpeg;

public sealed record TeslimKipi(int Width, int Height, double Fps, string Codec, string Mode, string CrfOrBitrate);

public static class TeslimOzeti
{
    public static TeslimKipi Of(EncodeResult result)
    {
        var used = result.PlanUsed;
        return new TeslimKipi(
            used.Width,
            used.Height,
            used.Fps,
            used.Codec,
            used.Mode,
            used.ModeEnum == EncodeMode.Crf ? $"crf {used.Crf}" : $"{used.VideoBitrateK}k");
    }
}
