using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

if (args.Length < 1)
{
    Console.Error.WriteLine("kullanim: cipa-yeniden <kaynak> [hedefMb]");
    return 1;
}

var source = args[0];
var targets = args.Length > 1
    ? args[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray()
    : new[] { 60.0 };

var info = await FfprobeClient.ProbeAsync(source);
var probed = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Quality, true, QualityMeasurement.Instance);

var windows = probed.QualityMeasurements
    .Select(q => new
    {
        q.StartSeconds,
        q.Comparable,
        q.VmafNegMean,
        q.VmafNegHarmonic,
        q.VmafNegP10,
        q.VmafNegMin
    })
    .ToArray();

var anchors = probed.QualityMeasurements
    .Where(q => q is { Comparable: true, VmafNegMean: not null })
    .Select(q => q.VmafNegMean!.Value)
    .ToArray();

var profile = probed.Profile;
if (anchors.Length > 0) profile = profile.WithProbeQuality(anchors);

var planlar = targets.Select(t =>
{
    var options = new PlanOptions
    {
        TargetMb = t,
        Codec = CodecPreference.Auto,
        FillPolicy = FillPolicy.FillTarget,
        SpeedMode = SpeedMode.Quality
    };
    var r = PlanCalculator.BuildDetailed(info, options, profile, EncoderCapabilities.Instance);
    return new
    {
        HedefMb = t,
        r.Plan.Width,
        r.Plan.Height,
        r.Plan.Fps,
        r.Plan.Codec,
        r.Plan.Mode,
        r.Plan.Crf,
        r.Plan.VideoBitrateK,
        r.Plan.Preset,
        r.PredictedQuality
    };
}).ToArray();

var payload = new
{
    Kaynak = source,
    Hedefler = targets,
    OlcerKilidi = MeasureFilterGraph.Build("null", "null", "libvmaf"),
    ProbeBppf = profile.ProbeBppf,
    Pencereler = windows,
    Cipalar = anchors,
    Anchor = profile.QualityAnchor,
    Planlar = planlar
};

Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
{
    WriteIndented = true,
    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
}));
return 0;
