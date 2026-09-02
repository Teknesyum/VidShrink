using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

var src = args[0];
var targetMb = double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
var outPath = args[2];
var mode = args.Length > 3 ? args[3] : "auto";

var caps = EncoderCapabilities.Instance;
var info = await FfprobeClient.ProbeAsync(src);
Console.WriteLine($"kaynak: {info.Width}x{info.Height}@{info.Fps} {info.DurationSeconds:0.###}s {info.FileSizeMb:0.00} MB hdr={info.IsHdr} ses={info.HasAudio} sesK={info.AudioBitrateBps / 1000}");

var options = new PlanOptions
{
    TargetMb = targetMb,
    Intent = Intent.Sharing,
    Codec = CodecPreference.Auto,
    AllowResolutionDrop = true,
    AllowFpsDrop = true,
    HdrPolicy = HdrPolicy.Preserve,
    FillPolicy = FillPolicy.FillTarget,
    SpeedMode = SpeedMode.Quality
};

if (mode.StartsWith("opt:"))
{
    foreach (var kv in mode[4..].Split(','))
    {
        var p = kv.Split('=');
        switch (p[0])
        {
            case "codec": options.Codec = Enum.Parse<CodecPreference>(p[1], true); break;
            case "res": options.AllowResolutionDrop = bool.Parse(p[1]); break;
            case "fps": options.AllowFpsDrop = bool.Parse(p[1]); break;
            case "intent": options.Intent = Enum.Parse<Intent>(p[1], true); break;
            case "fill": options.FillPolicy = Enum.Parse<FillPolicy>(p[1], true); break;
        }
    }
}

var speed = options.SpeedMode;
var profile = await ComplexityProbe.RunAsync(info, speed, CancellationToken.None);
var draft = PlanCalculator.BuildDetailed(info, options, profile, caps).Plan;
for (var round = 0; round < 2; round++)
{
    var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, CancellationToken.None);
    profile = calibrated;
    if (!calibrated.Calibrated) break;
    var settled = PlanCalculator.BuildDetailed(info, options, calibrated, caps).Plan;
    var scale = info.Width > 0 ? (double)settled.Width / info.Width : 1.0;
    if (calibrated.AppliesTo(settled.Codec, scale, settled.Fps)) break;
    draft = settled;
    profile = calibrated.WithoutCalibration();
}

var detailed = PlanCalculator.BuildDetailed(info, options, profile, caps);
var plan = detailed.Plan;
Console.WriteLine($"PLAN mode={plan.Mode} codec={plan.Codec} preset={plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps} crf={plan.Crf} vbit={plan.VideoBitrateK}k abit={plan.AudioBitrateK}k ach={plan.AudioChannels} pix={plan.PixelFormat} hdrfilt={plan.HdrVideoFilter is null} tahminMB={detailed.Estimate.ExpectedMb:0.00} tahminKalite={detailed.PredictedQuality:0.0}");
Console.WriteLine("ARGV1: " + FfmpegArguments.ToCommandLine(EncodeRunner.EncodeArguments(info, plan, outPath, 1, "pl", caps)));
Console.WriteLine("ARGV2: " + FfmpegArguments.ToCommandLine(EncodeRunner.EncodeArguments(info, plan, outPath, 2, "pl", caps)));

if (Environment.GetEnvironmentVariable("T102_PLAN_ONLY") == "1") return;

var sw = System.Diagnostics.Stopwatch.StartNew();
var result = await new EncodeRunner().RunAsync(info, plan, outPath, targetMb, null, CancellationToken.None, options.FillPolicy, profile, null);
sw.Stop();
Console.WriteLine($"SONUC basari={result.Success} deneme={result.Attempts} cikti={result.OutputMb:0.00} MB sure={sw.Elapsed.TotalSeconds:0.0}s yol={result.OutputPath}");
Console.WriteLine("JSON " + JsonSerializer.Serialize(new
{
    mode,
    targetMb,
    plan.Mode,
    plan.Codec,
    plan.Preset,
    plan.Width,
    plan.Height,
    plan.Fps,
    plan.Crf,
    plan.VideoBitrateK,
    plan.AudioBitrateK,
    plan.PixelFormat,
    result.Success,
    result.Attempts,
    result.OutputMb,
    seconds = sw.Elapsed.TotalSeconds
}));
