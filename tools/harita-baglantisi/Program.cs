using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

var inv = CultureInfo.InvariantCulture;

if (args.Length >= 5 && args[0] == "atlama")
{
    var mappedFile = Path.GetFullPath(args[1]);
    var plainFile = Path.GetFullPath(args[2]);
    var seconds = double.Parse(args[3], inv);
    var dir = Path.GetFullPath(args[4]);
    var turns = args.Length > 5 ? int.Parse(args[5], inv) : 6;
    Directory.CreateDirectory(dir);
    var only = await SeekAsync(mappedFile, plainFile, seconds, turns, dir);
    Console.WriteLine($"atlama ham p50 bagli={only.MappedRaw.ToString("0.0", inv)} ms · bagsiz={only.PlainRaw.ToString("0.0", inv)} ms · taban p50={only.Baseline.ToString("0.0", inv)} ms · net bagli={only.Mapped.ToString("0.0", inv)} · net bagsiz={only.Plain.ToString("0.0", inv)} (n={only.Count} her rejim, paylasimli makine)");
    Console.WriteLine(JsonSerializer.Serialize(only, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (args.Length >= 5 && args[0] == "onizleme")
{
    var src = Path.GetFullPath(args[1]);
    var mb = double.Parse(args[2], inv);
    var dir = Path.GetFullPath(args[3]);
    var finalFile = Path.GetFullPath(args[4]);
    var th = args.Length > 5 ? int.Parse(args[5], inv) : 2;
    Directory.CreateDirectory(dir);

    var capsP = EncoderCapabilities.Instance;
    var infoP = await FfprobeClient.ProbeAsync(src);
    var attemptP = await EncodeRunner.TryBuildSceneMapAsync(infoP);
    if (attemptP.Map is null)
    {
        Console.Error.WriteLine("harita uretilemedi: " + attemptP.Fallback);
        return 3;
    }

    var optionsP = new PlanOptions
    {
        TargetMb = mb,
        Intent = Intent.Sharing,
        Codec = CodecPreference.Auto,
        AllowResolutionDrop = true,
        AllowFpsDrop = true,
        HdrPolicy = HdrPolicy.Preserve,
        FillPolicy = FillPolicy.FillTarget,
        SpeedMode = SpeedMode.Quality
    };
    var profileP = ComplexityProfile.FromSourceBitrate(infoP);
    var planP = PlanCalculator.BuildDetailed(infoP, optionsP, profileP, capsP).Plan;
    planP.ExtraArgs = new List<string> { "-threads", th.ToString(inv) };

    var start = Math.Min(10, Math.Max(0, infoP.DurationSeconds - 6));
    var eskiPath = Path.Combine(dir, "onizleme-baglanmamis.mp4");
    var yeniPath = Path.Combine(dir, "onizleme-bagli.mp4");
    var eski = PreviewSegment.For(infoP, planP, start, eskiPath, availability: capsP);
    var yeni = PreviewSegment.For(infoP, planP, start, yeniPath, availability: capsP, scenes: attemptP.Map);

    Console.WriteLine($"plan {planP.Codec} {planP.Width}x{planP.Height}@{planP.Fps:0.###} · onizleme suresi={yeni.DurationSeconds.ToString("0.###", inv)} sn · baslangic={start.ToString("0.###", inv)} sn");
    Console.WriteLine($"-g baglanmamis onizleme={Gop(eski.Arguments)} · bagli onizleme={Gop(yeni.Arguments)} · nihai={Gop(EncodeRunner.EncodeArguments(infoP, planP, "x.mp4", 0, null, capsP, attemptP.Map))}");

    await TimeAsync(eski.Arguments);
    await TimeAsync(yeni.Arguments);

    var eskiKeys = await KeyTimesAsync(eskiPath);
    var yeniKeys = await KeyTimesAsync(yeniPath);
    var pencereSonu = start + yeni.DurationSeconds;
    var nihaiKeys = (await KeyTimesAsync(finalFile)).Where(t => t >= start && t <= pencereSonu).Select(t => t - start).ToList();

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        pencere = new { baslangic = start, sure = yeni.DurationSeconds },
        baglanmamisOnizleme = Ozet(eskiKeys),
        bagliOnizleme = Ozet(yeniKeys),
        nihaiAyniPencere = Ozet(nihaiKeys),
        anahtarKareler = new { baglanmamis = eskiKeys, bagli = yeniKeys, nihai = nihaiKeys }
    }, new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
    return 0;
}

if (args.Length >= 3 && args[0] == "kalite")
{
    var reference = Path.GetFullPath(args[1]);
    var rowsOnly = new List<object>();
    foreach (var candidate in args.Skip(2))
    {
        var (score, method) = await MeasureWithFallbackAsync(reference, Path.GetFullPath(candidate));
        rowsOnly.Add(new
        {
            dosya = Path.GetFileName(candidate),
            yontem = method,
            karsilastirilabilir = score.Comparable,
            vmafOrtalama = score.VmafNegMean,
            vmafP10 = score.VmafNegP10,
            not = score.Message
        });
        Console.WriteLine($"{Path.GetFileName(candidate)} [{method}] vmaf {score.VmafNegMean?.ToString("0.000", inv)} / p10 {score.VmafNegP10?.ToString("0.000", inv)} · karsilastirilabilir={score.Comparable} · {score.Message}");
    }
    Console.WriteLine(JsonSerializer.Serialize(rowsOnly, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (args.Length < 3)
{
    Console.Error.WriteLine("kullanim: t113baglanti <kaynak> <hedefMB> <cikti-klasoru> [threads] [atlama-turu] [fps-kilit]");
    Console.Error.WriteLine("          t113baglanti kalite <referans> <test...>");
    return 2;
}

var sourcePath = Path.GetFullPath(args[0]);
var targetMb = double.Parse(args[1], inv);
var outDir = Path.GetFullPath(args[2]);
var threads = args.Length > 3 ? int.Parse(args[3], inv) : 2;
var seekRounds = args.Length > 4 ? int.Parse(args[4], inv) : 6;
var lockFps = args.Length > 5 && args[5] == "fps-kilit";
Directory.CreateDirectory(outDir);

var caps = EncoderCapabilities.Instance;
var info = await FfprobeClient.ProbeAsync(sourcePath);
Console.WriteLine($"kaynak {Path.GetFileName(sourcePath)} {info.Width}x{info.Height}@{info.Fps:0.###} {info.DurationSeconds:0.###}s {info.FileSizeMb:0.00} MB hdr={info.IsHdr} ses={info.HasAudio}");

var attempt = await EncodeRunner.TryBuildSceneMapAsync(info);
Console.WriteLine($"harita ok={attempt.Ok} dusus={attempt.Fallback} sure_sn={attempt.Elapsed.TotalSeconds.ToString("0.###", inv)} ayrinti={attempt.Detail}");
if (!attempt.Ok) return 3;

var map = attempt.Map!;
var lengths = map.Scenes.Select(s => s.Duration).OrderBy(x => x).ToArray();
var median = lengths.Length % 2 == 1 ? lengths[lengths.Length / 2] : (lengths[lengths.Length / 2 - 1] + lengths[lengths.Length / 2]) / 2.0;
Console.WriteLine($"harita sahne={map.Scenes.Count} medyan_sn={median.ToString("0.###", inv)} esik_NaN={double.IsNaN(map.Threshold)} kural={map.Rule}");

var options = new PlanOptions
{
    TargetMb = targetMb,
    Intent = Intent.Sharing,
    Codec = CodecPreference.Auto,
    AllowResolutionDrop = true,
    AllowFpsDrop = !lockFps,
    HdrPolicy = HdrPolicy.Preserve,
    FillPolicy = FillPolicy.FillTarget,
    SpeedMode = SpeedMode.Quality
};

var profile = ComplexityProfile.FromSourceBitrate(info);
var plan = PlanCalculator.BuildDetailed(info, options, profile, caps).Plan;
plan.ExtraArgs = new List<string> { "-threads", threads.ToString(inv) };
Console.WriteLine($"plan mode={plan.Mode} codec={plan.Codec} preset={plan.Preset} {plan.Width}x{plan.Height}@{plan.Fps:0.###} crf={plan.Crf} vbit={plan.VideoBitrateK}k threads={threads}");

var mappedArgs = EncodeRunner.EncodeArguments(info, plan, "x.mp4", 0, null, caps, map);
var plainArgs = EncodeRunner.EncodeArguments(info, plan, "x.mp4", 0, null, caps);
var previewMapped = PreviewSegment.For(info, plan, Math.Min(10, Math.Max(0, info.DurationSeconds - 6)), "p.mp4", availability: caps, scenes: map);
var previewPlain = PreviewSegment.For(info, plan, Math.Min(10, Math.Max(0, info.DurationSeconds - 6)), "p.mp4", availability: caps);

Console.WriteLine($"aralik bagli nihai={Gop(mappedArgs)} onizleme={Gop(previewMapped.Arguments)} · bagsiz nihai={Gop(plainArgs)} onizleme={Gop(previewPlain.Arguments)}");

var rows = new List<object>();
var files = new Dictionary<string, string>();

foreach (var (label, scenes) in new (string, SceneMap?)[] { ("bagli", map), ("bagsiz", null) })
{
    var outputPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}-{label}.mp4");
    var clock = Stopwatch.StartNew();
    var result = await new EncodeRunner().RunAsync(info, plan.Clone(), outputPath, targetMb, null,
        CancellationToken.None, options.FillPolicy, profile, null, scenes);
    clock.Stop();
    if (!result.Success) { Console.Error.WriteLine($"{label}: kodlama basarisiz — {result.Error}"); return 4; }

    files[label] = result.OutputPath;
    var (quality, method) = await MeasureWithFallbackAsync(sourcePath, result.OutputPath);
    var iFrames = await CountIFramesAsync(result.OutputPath);

    rows.Add(new
    {
        kosum = label,
        gop = label == "bagli" ? Gop(mappedArgs) : Gop(plainArgs),
        boyutMb = result.OutputMb,
        denemeler = result.Attempts,
        vmafOrtalama = quality.VmafNegMean,
        vmafP10 = quality.VmafNegP10,
        karsilastirilabilir = quality.Comparable,
        olcuYontemi = method,
        not = quality.Message,
        iKare = iFrames,
        kodlamaSn = clock.Elapsed.TotalSeconds
    });
    Console.WriteLine($"{label}: {result.OutputMb.ToString("0.000", inv)} MB · vmaf[{method}] {quality.VmafNegMean?.ToString("0.000", inv)} / p10 {quality.VmafNegP10?.ToString("0.000", inv)} · I-kare {iFrames} · {clock.Elapsed.TotalSeconds.ToString("0.0", inv)} s");
}

var seek = await SeekAsync(files["bagli"], files["bagsiz"], info.DurationSeconds, seekRounds, outDir);
Console.WriteLine($"atlama ham p50 bagli={seek.MappedRaw.ToString("0.0", inv)} ms · bagsiz={seek.PlainRaw.ToString("0.0", inv)} ms · taban p50={seek.Baseline.ToString("0.0", inv)} ms · net bagli={seek.Mapped.ToString("0.0", inv)} · net bagsiz={seek.Plain.ToString("0.0", inv)} (n={seek.Count} her rejim, paylasimli makine)");

var report = new
{
    kaynak = Path.GetFileName(sourcePath),
    sureSn = info.DurationSeconds,
    hedefMb = targetMb,
    threads,
    harita = new { sahne = map.Scenes.Count, medyanSn = median, sureSn = attempt.Elapsed.TotalSeconds },
    onizleme = new { bagli = Gop(previewMapped.Arguments), bagsiz = Gop(previewPlain.Arguments) },
    nihai = new { bagli = Gop(mappedArgs), bagsiz = Gop(plainArgs) },
    kosumlar = rows,
    atlama = new { bagliNet = seek.Mapped, bagsizNet = seek.Plain, bagliHam = seek.MappedRaw, bagsizHam = seek.PlainRaw, taban = seek.Baseline, n = seek.Count }
};
var jsonPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}.json");
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("JSON " + jsonPath);
return 0;

/// <summary>
/// Kaynak HDR, plan onu SDR'a tonemap ediyor; duz olcum bu ciftte
/// <c>Comparable = false</c> doner. O durumda referans ayni tonemap'ten gecirilerek
/// yeniden olculur ve hangi yolun kullanildigi satira yazilir.
/// </summary>
static async Task<(QualityScore Score, string Method)> MeasureWithFallbackAsync(string referencePath, string testPath)
{
    var direct = await QualityMeter.MeasureAsync(referencePath, testPath);
    if (direct.Comparable && direct.VmafNegMean is not null) return (direct, "duz");
    var tonemapped = await QualityMeter.MeasureTonemappedReferenceAsync(referencePath, testPath);
    return (tonemapped, "tonemapli-referans");
}

static int Gop(IReadOnlyList<string> args)
{
    var at = args.ToList().IndexOf("-g");
    return at < 0 ? -1 : int.Parse(args[at + 1], CultureInfo.InvariantCulture);
}

static async Task<int> CountIFramesAsync(string path)
{
    using var process = new Process
    {
        StartInfo = Start(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "frame=pict_type", "-of", "csv=p=0", path
        })
    };
    process.Start();
    var stdout = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return stdout.Split('\n').Count(line => line.Trim().TrimEnd(',') == "I");
}

/// <summary>
/// Atlama gecikmesi. Iki dosya <b>tur icinde donusumlu</b> olculur, boylece ikisi de ayni
/// makine yukunu gorur; T98 tur 3 bu duzeltmeyi olcuyle gerekcelendirdi. Her turda ayri bir
/// surec acilis tabani da olculur ve p50'den dusulur.
/// </summary>
static async Task<SeekResult> SeekAsync(
    string mappedPath, string plainPath, double durationSeconds, int rounds, string outDir)
{
    var points = new List<double>();
    for (var i = 1; i <= 20; i++) points.Add(durationSeconds * i / 21.0);

    var mapped = new List<double>();
    var plain = new List<double>();
    var baseline = new List<double>();
    var raw = new List<string> { "tur	rejim	nokta_sn	ms" };

    for (var round = 0; round < rounds; round++)
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var lane = new List<(string Label, List<double> Sink, string[] Args)>
            {
                ("bagli", mapped, new[]
                {
                    "-hide_banner", "-v", "error", "-nostats",
                    "-ss", point.ToString("0.###", CultureInfo.InvariantCulture),
                    "-i", mappedPath, "-frames:v", "1", "-f", "null", "-"
                }),
                ("bagsiz", plain, new[]
                {
                    "-hide_banner", "-v", "error", "-nostats",
                    "-ss", point.ToString("0.###", CultureInfo.InvariantCulture),
                    "-i", plainPath, "-frames:v", "1", "-f", "null", "-"
                }),
                ("taban", baseline, new[]
                {
                    "-hide_banner", "-v", "error", "-nostats",
                    "-f", "lavfi", "-i", "nullsrc=s=32x32:d=0.1", "-frames:v", "1", "-f", "null", "-"
                })
            };

            var shift = (round + index) % lane.Count;
            for (var k = 0; k < lane.Count; k++)
            {
                var (label, sink, callArgs) = lane[(k + shift) % lane.Count];
                var ms = await TimeAsync(callArgs);
                sink.Add(ms);
                raw.Add($"{round}	{label}	{point.ToString("0.###", CultureInfo.InvariantCulture)}	{ms.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }

    await File.WriteAllLinesAsync(Path.Combine(outDir, "atlama-ham.tsv"), raw);
    var basel = Median(baseline);
    return new SeekResult(Median(mapped) - basel, Median(plain) - basel, Median(mapped), Median(plain), basel, mapped.Count);
}


static async Task<double> TimeAsync(IEnumerable<string> args)
{
    using var process = new Process { StartInfo = Start(ToolLocator.Ffmpeg, args) };
    var clock = Stopwatch.StartNew();
    process.Start();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    await stderr;
    clock.Stop();
    return clock.Elapsed.TotalMilliseconds;
}

static ProcessStartInfo Start(string exe, IEnumerable<string> args)
{
    var info = new ProcessStartInfo(exe)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var a in args) info.ArgumentList.Add(a);
    return info;
}

static async Task<List<double>> KeyTimesAsync(string path)
{
    using var process = new Process
    {
        StartInfo = Start(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0", "-skip_frame", "nokey",
            "-show_entries", "frame=best_effort_timestamp_time", "-of", "csv=p=0", path
        })
    };
    process.Start();
    var stdout = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    var times = new List<double>();
    foreach (var line in stdout.Split('\n'))
    {
        var text = line.Trim().TrimEnd(',');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) times.Add(value);
    }
    times.Sort();
    return times;
}

static object Ozet(List<double> times)
{
    var gaps = new List<double>();
    for (var i = 1; i < times.Count; i++) gaps.Add(times[i] - times[i - 1]);
    return new
    {
        sayi = times.Count,
        ilk = times.Count > 0 ? times[0] : double.NaN,
        son = times.Count > 0 ? times[^1] : double.NaN,
        ortalamaAralik = gaps.Count > 0 ? gaps.Average() : double.NaN,
        medyanAralik = Median(gaps),
        enBuyukAralik = gaps.Count > 0 ? gaps.Max() : double.NaN
    };
}

static double Median(List<double> values)
{
    var sorted = values.OrderBy(x => x).ToArray();
    if (sorted.Length == 0) return double.NaN;
    return sorted.Length % 2 == 1
        ? sorted[sorted.Length / 2]
        : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;
}
/// <summary>
/// Atlama p50'si iki bicimde: ham (surec acilisi dahil) ve net (taban dusulmus). Taban
/// olcunun duzeltmesi olacak kadar kucuk degilse net sayi anlamsizdir; ikisi de tasiniyor
/// ki rapor hangisini kullandigini gosterebilsin.
/// </summary>
record SeekResult(double Mapped, double Plain, double MappedRaw, double PlainRaw, double Baseline, int Count);
