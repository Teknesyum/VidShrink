using System.Globalization;

namespace VidShrink.Core;

public static class BudgetFill
{
    public const double Floor = 0.97;
    public const double Aim = 0.985;
    public const double NvencAim = 0.97;
    public const int ExtraAttempts = 1;

    public static bool Wants(double deliveredMb, double targetMb, int attemptsUsed, int attemptLimit, bool alreadyUsed)
        => !alreadyUsed
           && targetMb > 0
           && deliveredMb > 0
           && deliveredMb < Floor * targetMb
           && attemptsUsed < attemptLimit + ExtraAttempts;

    public static bool Keeps(double previousMb, double topUpMb, double targetMb)
        => topUpMb <= targetMb && topUpMb > previousMb;

    public static double? AimFor(string codec) => CodecModel.Vendor(codec) switch
    {
        EncoderVendor.Software => Aim,
        EncoderVendor.Nvenc => NvencAim,
        _ => null
    };

    public static EncodePlan? Plan(EncodePlan delivered, double deliveredMb, IReadOnlyList<SizeSample> samples, double targetMb, double durationSeconds)
    {
        if (AimFor(delivered.Codec) is not double aim) return null;
        if (targetMb <= 0 || durationSeconds <= 0 || deliveredMb <= 0 || deliveredMb >= Floor * targetMb) return null;

        var audioMb = PlanCalculator.NonVideoMb(delivered.NonVideoK, durationSeconds);
        var deliveredVideoMb = deliveredMb - audioMb;
        var aimMb = aim * targetMb;
        var aimVideoMb = aimMb - audioMb;
        if (deliveredVideoMb <= 0.001 || aimVideoMb <= deliveredVideoMb) return null;

        var twoPass = delivered.ModeEnum == EncodeMode.TwoPass && delivered.VideoBitrateK > 0;
        double baseK = twoPass ? delivered.VideoBitrateK : PlanCalculator.VideoKbitFor(deliveredVideoMb, durationSeconds);
        var scaledK = baseK * aimVideoMb / deliveredVideoMb;

        var bound = double.MaxValue;
        if (twoPass)
        {
            foreach (var over in samples.Where(s => s.OverCeiling && s.VideoBitrateK > delivered.VideoBitrateK && s.ActualMb > deliveredMb))
            {
                var interpolated = baseK + (over.VideoBitrateK - baseK) * (aimMb - deliveredMb) / (over.ActualMb - deliveredMb);
                bound = Math.Min(bound, Math.Min(interpolated, over.VideoBitrateK - 1));
            }
        }

        var k = (int)Math.Floor(Math.Min(scaledK, bound));
        if (k <= baseK) return null;
        if (k < PlanCalculator.RunnableVideoBitrateK(delivered.Width, delivered.Height, delivered.Fps)) return null;

        var topUp = delivered.Clone();
        topUp.Mode = "2pass";
        topUp.Crf = null;
        topUp.VideoBitrateK = k;
        var factor = k / Math.Max(1.0, baseK);
        topUp.Reason = string.Create(CultureInfo.InvariantCulture,
            $"retry: budget fill, the result used {deliveredMb:0.###} MB of the {targetMb:0.###} MB target, under {Floor:0.00} of it, so one more attempt aims at {aim:0.000} of the target and asks for {k}k; if it lands over the target the previous result is delivered");
        topUp.ReasonCodes = new List<ReasonNote> { new(ReasonCode.RetryScaled, Mb: deliveredMb, TargetMb: targetMb, AudioMb: audioMb, Factor: factor) };
        return topUp;
    }
}
