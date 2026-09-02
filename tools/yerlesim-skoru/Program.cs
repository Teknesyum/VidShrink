using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

var ci = CultureInfo.InvariantCulture;

if (args.Length < 4)
{
    Console.Error.WriteLine("kullanim: t107skor <kaynak> <hedefMb> <kodlayici> <yerlesim-dosyasi> [--videok N]");
    return 1;
}

var source = args[0];
var targetMb = double.Parse(args[1], ci);
var codec = args[2];
var layoutFile = args[3];
double? videoKOverride = null;
for (var i = 4; i < args.Length - 1; i++)
    if (args[i] == "--videok") videoKOverride = double.Parse(args[i + 1], ci);

var info = await FfprobeClient.ProbeAsync(source);
var probed = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Quality, measureQuality: true, QualityMeasurement.Instance);
var complexity = probed.Profile;
var anchors = probed.QualityMeasurements
    .Where(q => q is { Comparable: true, VmafNegMean: not null })
    .Select(q => q.VmafNegMean!.Value)
    .ToArray();
if (anchors.Length > 0) complexity = complexity.WithProbeQuality(anchors);
complexity = complexity.WithoutSampleContainerBias(info.Width, info.Height);

var regime = CompressionStrategy.RegimeFor(info.FileSizeMb, targetMb);
var level = complexity.Level;

Console.WriteLine("#profil\t" + string.Join("\t", new[]
{
    $"kaynak={Path.GetFileName(source)}",
    $"WxH={info.Width}x{info.Height}",
    $"fps={info.Fps.ToString("0.###", ci)}",
    $"sure={info.DurationSeconds.ToString("0.###", ci)}",
    $"kaynakMb={info.FileSizeMb.ToString("0.##", ci)}",
    $"hedefMb={targetMb.ToString("0.##", ci)}",
    $"rejim={regime}",
    $"ReferenceBppf={complexity.ReferenceBppf.ToString("0.######", ci)}",
    $"DetailExponent={complexity.DetailExponent.ToString("0.####", ci)}",
    $"MotionExponent={complexity.MotionExponent.ToString("0.####", ci)}",
    $"MotionMeasured={complexity.MotionMeasured}",
    $"Measured={complexity.Measured}",
    $"AtReference={level.AtReference.ToString("0.###", ci)}",
    $"PerHalving={level.PerHalving.ToString("0.###", ci)}",
    $"LevelMeasured={level.Measured}",
    $"SlopeMeasured={level.SlopeMeasured}",
    $"cipa={(anchors.Length > 0 ? string.Join("/", anchors.Select(a => a.ToString("0.##", ci))) : "yok")}",
    $"FloorAdaptation={complexity.FloorAdaptation.ToString("0.####", ci)}",
}));

var options = new PlanOptions { TargetMb = targetMb, Codec = CodecPreference.Auto };
var planResult = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance);
var plan = planResult.Plan;
Console.WriteLine($"#plan\t{plan.Codec}\t{plan.Width}x{plan.Height}\t{plan.Fps.ToString("0.###", ci)}\tvideoK={plan.VideoBitrateK}\tmod={plan.Mode}\tnotlar={string.Join(",", planResult.Advice.Notes)}");

var videoK = videoKOverride ?? plan.VideoBitrateK;
Console.WriteLine($"#videoK\t{videoK.ToString("0.###", ci)}\tkodlayici={codec}");
Console.WriteLine("yerlesim\tfps\tolcek\tgerekli\tsaglanan\trate\tolcekCezasi\tfpsCezasi\thisterez\tskor\ttabanGecer\tkullanilirK");

foreach (var line in File.ReadAllLines(layoutFile))
{
    var t = line.Trim();
    if (t.Length == 0 || t.StartsWith('#')) continue;
    var f = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    var w = int.Parse(f[0], ci);
    var h = int.Parse(f[1], ci);
    var fps = double.Parse(f[2], ci);
    var k = f.Length > 3 ? double.Parse(f[3], ci) : videoK;

    var parts = PlanCalculator.ScoreLayout(complexity, codec, k, w, h, fps, info.Fps, info.Height, regime);
    var clears = PlanCalculator.LayoutClearsFloor(complexity, codec, k, w, h, fps, info.Fps);
    var usable = CodecModel.UsableBitrateK(codec, w, h, fps);
    Console.WriteLine(string.Join("\t", new[]
    {
        $"{w}x{h}", fps.ToString("0.###", ci), ((double)h / info.Height).ToString("0.####", ci),
        parts.Required.ToString("0.######", ci), parts.Provided.ToString("0.######", ci),
        parts.Rate.ToString("0.###", ci), parts.ScalePenalty.ToString("0.###", ci),
        parts.FpsPenalty.ToString("0.###", ci), parts.Hysteresis.ToString("0.##", ci),
        parts.Score.ToString("0.###", ci), clears.ToString(), usable.ToString(ci)
    }));
}
return 0;
