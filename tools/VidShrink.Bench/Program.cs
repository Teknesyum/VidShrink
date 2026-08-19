using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "measure" => await MeasureAsync(args),
        "shrink" => await ShrinkAsync(args),
        "compare" => Compare(args),
        _ => Unknown(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  bench measure <referans> <test>");
    Console.WriteLine("  bench shrink <kaynak> <hedefMb,...> --out <klasor>");
    Console.WriteLine("  bench compare <a.json> <b.json>");
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {command}");
    PrintUsage();
    return 1;
}

static async Task<int> MeasureAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench measure <referans> <test>");
        return 1;
    }

    var score = await QualityMeter.MeasureAsync(args[1], args[2], CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(score));
    return 0;
}

static async Task<int> ShrinkAsync(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench shrink <kaynak> <hedefMb,...> --out <klasor>");
        return 1;
    }

    var source = args[1];
    var targets = args[2]
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
        .ToList();

    string? outDir = null;
    for (var i = 3; i < args.Length; i++)
        if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];

    if (outDir is null)
    {
        Console.Error.WriteLine("--out <klasor> gerekli");
        return 1;
    }

    Directory.CreateDirectory(outDir);

    var info = await FfprobeClient.ProbeAsync(source);
    var complexity = await ComplexityProbe.RunAsync(info);
    var results = new List<BenchResult>();

    foreach (var targetMb in targets)
    {
        var options = new PlanOptions { TargetMb = targetMb };
        var planResult = PlanCalculator.BuildDetailed(info, options, complexity, EncoderCapabilities.Instance);
        var plan = planResult.Plan;

        var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(source)}_{targetMb.ToString("0.#", CultureInfo.InvariantCulture)}mb.mp4");
        var stopwatch = Stopwatch.StartNew();
        var encodeResult = await new EncodeRunner().RunAsync(info, plan, outputPath, targetMb, null, CancellationToken.None);
        stopwatch.Stop();

        var score = await QualityMeter.MeasureAsync(source, outputPath, CancellationToken.None);

        var result = new BenchResult(
            targetMb,
            encodeResult.OutputMb,
            encodeResult.OutputMb / targetMb * 100.0,
            plan.Width,
            plan.Height,
            plan.Fps,
            plan.Codec,
            plan.Mode,
            plan.ModeEnum == EncodeMode.Crf ? $"crf {plan.Crf}" : $"{plan.VideoBitrateK}k",
            stopwatch.Elapsed.TotalSeconds,
            score.VmafNegHarmonic,
            score.VmafNegP10,
            score.Xpsnr);
        results.Add(result);

        Console.WriteLine(
            $"{result.TargetMb:0.##} MB -> {result.ActualMb:0.##} MB ({result.FillPercent:0.#}%), " +
            $"{result.Width}x{result.Height}@{result.Fps:0.##}, {result.Codec}/{result.Mode}, " +
            $"VMAF-NEG harm={Fmt(result.VmafNegHarmonic)} p10={Fmt(result.VmafNegP10)}, XPSNR={Fmt(result.Xpsnr)}");
    }

    var resultsPath = Path.Combine(outDir, $"results-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"sonuclar: {resultsPath}");
    return 0;
}

static int Compare(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: bench compare <a.json> <b.json>");
        return 1;
    }

    var a = JsonSerializer.Deserialize<List<BenchResult>>(File.ReadAllText(args[1])) ?? new List<BenchResult>();
    var b = JsonSerializer.Deserialize<List<BenchResult>>(File.ReadAllText(args[2])) ?? new List<BenchResult>();

    var count = Math.Max(a.Count, b.Count);
    for (var i = 0; i < count; i++)
    {
        var ar = i < a.Count ? a[i] : null;
        var br = i < b.Count ? b[i] : null;
        if (ar is null || br is null)
        {
            Console.WriteLine($"[{i}] eslesme yok (a:{ar is not null} b:{br is not null})");
            continue;
        }

        Console.WriteLine($"[{i}] hedef {ar.TargetMb:0.##}MB -> {br.TargetMb:0.##}MB");
        Console.WriteLine($"  gercekMb   {ar.ActualMb:0.##} -> {br.ActualMb:0.##}");
        Console.WriteLine($"  doluluk%   {ar.FillPercent:0.#} -> {br.FillPercent:0.#}");
        Console.WriteLine($"  cozunurluk {ar.Width}x{ar.Height} -> {br.Width}x{br.Height}");
        Console.WriteLine($"  fps        {ar.Fps:0.##} -> {br.Fps:0.##}");
        Console.WriteLine($"  codec/mod  {ar.Codec}/{ar.Mode} -> {br.Codec}/{br.Mode}");
        Console.WriteLine($"  crf/bitrate {ar.CrfOrBitrate} -> {br.CrfOrBitrate}");
        Console.WriteLine($"  sure(s)    {ar.EncodeSeconds:0.#} -> {br.EncodeSeconds:0.#}");
        Console.WriteLine($"  vmaf harm  {Fmt(ar.VmafNegHarmonic)} -> {Fmt(br.VmafNegHarmonic)}");
        Console.WriteLine($"  vmaf p10   {Fmt(ar.VmafNegP10)} -> {Fmt(br.VmafNegP10)}");
        Console.WriteLine($"  xpsnr      {Fmt(ar.Xpsnr)} -> {Fmt(br.Xpsnr)}");
    }

    return 0;
}

static string Fmt(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";

sealed record BenchResult(
    double TargetMb,
    double ActualMb,
    double FillPercent,
    int Width,
    int Height,
    double Fps,
    string Codec,
    string Mode,
    string CrfOrBitrate,
    double EncodeSeconds,
    double? VmafNegHarmonic,
    double? VmafNegP10,
    double? Xpsnr);
