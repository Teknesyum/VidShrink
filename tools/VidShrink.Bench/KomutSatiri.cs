using VidShrink.Core;
using VidShrink.Ffmpeg;

public static class KomutSatiri
{
    public static int GecisNo(EncodePlan plan)
        => plan.ModeEnum == EncodeMode.TwoPass && FfmpegArguments.NeedsTwoPasses(plan.Codec) ? 2 : 0;

    public static IReadOnlyList<string> Argumanlar(
        MediaInfo info, EncodePlan plan, string outputPath, string passLogDir, IEncoderAvailability availability)
    {
        var pass = GecisNo(plan);
        return EncodeRunner.EncodeArguments(
            info, plan, outputPath, pass, pass > 0 ? Path.Combine(passLogDir, "pass") : null, availability);
    }

    public static string Yaz(
        MediaInfo info, EncodePlan plan, string outputPath, string passLogDir, IEncoderAvailability availability)
        => "komut: " + FfmpegArguments.ToCommandLine(Argumanlar(info, plan, outputPath, passLogDir, availability));
}
