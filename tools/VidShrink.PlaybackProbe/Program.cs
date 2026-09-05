using System.Diagnostics;
using System.Globalization;
using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;


if (args.Length == 0)
{
    Console.WriteLine("kullanim: prep | k1 <etiket> <yol> <sure> | k3 <etiket> <yol> <sure> | k4 <yol> <sure>");
    return 1;
}

switch (args[0])
{
    case "prep": return await PrepAsync();
    case "k1": return await K1Async(args[1], args[2], double.Parse(args[3], CultureInfo.InvariantCulture));
    case "k3": return await K3Async(args[1], args[2], double.Parse(args[3], CultureInfo.InvariantCulture));
    case "k2": return await K2Async(args[1], args[2], double.Parse(args[3], CultureInfo.InvariantCulture));
    case "k4": return await K4Async(args[1], double.Parse(args[2], CultureInfo.InvariantCulture));
    case "rep": return await RepAsync(args[1]);
    default:
        Console.WriteLine("bilinmeyen komut: " + args[0]);
        return 1;
}


static List<double> Targets(double duration, int seed, string mode, int n = 30)
{
    var rnd = new Random(seed);
    var list = new List<double>();
    if (mode == "uniform")
    {
        for (var i = 0; i < n; i++) list.Add(Math.Round(rnd.NextDouble() * duration, 2));
        return list;
    }

    // mixed: %70 komsu ileri surukleme (+0.5..3 sn), %30 rastgele uzak sicrama.
    var cur = Math.Round(rnd.NextDouble() * duration * 0.2, 2);
    for (var i = 0; i < n; i++)
    {
        if (rnd.NextDouble() < 0.7)
        {
            cur = Math.Min(duration - 0.1, cur + 0.5 + rnd.NextDouble() * 2.5);
        }
        else
        {
            cur = Math.Round(rnd.NextDouble() * duration, 2);
        }
        list.Add(cur);
    }
    return list;
}

static double Median(List<double> xs)
{
    var s = xs.OrderBy(x => x).ToList();
    var n = s.Count;
    return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2.0;
}

static double P95(List<double> xs)
{
    var s = xs.OrderBy(x => x).ToList();
    var idx = (int)Math.Ceiling(0.95 * s.Count) - 1;
    return s[Math.Clamp(idx, 0, s.Count - 1)];
}

static async Task<int> PrepAsync()
{
    var kaynak = "C:/Users/Teknesyum/Desktop/Projeler/VidShrink/.calisma/kaynak/parca-1.mkv";
    if (!File.Exists(kaynak))
    {
        Console.WriteLine("kaynak yok: " + kaynak);
        return 1;
    }

    var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "kaynaklar"));
    Directory.CreateDirectory(outDir);

    var orta = Path.Combine(outDir, "orta-180s.mkv");
    var uzun = Path.Combine(outDir, "uzun-600s.mkv");

    Console.WriteLine("kisa kaynak: " + kaynak);
    Console.WriteLine("orta uretiliyor -> " + orta);
    await RunAsync(ToolLocator.Ffmpeg, new[] { "-y", "-stream_loop", "8", "-i", kaynak, "-t", "180", "-c", "copy", orta });
    Console.WriteLine("uzun uretiliyor -> " + uzun);
    await RunAsync(ToolLocator.Ffmpeg, new[] { "-y", "-stream_loop", "30", "-i", kaynak, "-t", "600", "-c", "copy", uzun });

    Console.WriteLine("kisa=" + kaynak);
    Console.WriteLine("orta=" + orta);
    Console.WriteLine("uzun=" + uzun);
    return 0;
}
static async Task<int> K1Async(string etiket, string yol, double sure)
{
    yol = Path.GetFullPath(yol);
    Console.WriteLine("== K1 taban (eski: her aramada yeni surec) == " + etiket + " " + yol);
    foreach (var mode in new[] { "uniform", "mixed" })
    {
        var targets = Targets(sure, 42, mode);
        var lat = new List<double>();
        foreach (var t in targets)
        {
            var sw = Stopwatch.StartNew();
            await SeekViaFreshProcessAsync(yol, t);
            sw.Stop();
            lat.Add(sw.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine($"[{etiket}/{mode}] n={lat.Count} ham_ms=[{string.Join(",", lat.Select(x => x.ToString("F1", CultureInfo.InvariantCulture)))}]");
        Console.WriteLine($"[{etiket}/{mode}] medyan={Median(lat):F1}ms p95={P95(lat):F1}ms");
    }
    return 0;
}

static async Task<int> K2Async(string etiket, string yol, double sure)
{
    yol = Path.GetFullPath(yol);
    Console.WriteLine("== K2 surec kalicilik (video + ses tetiklenerek) == " + etiket + " " + yol);
    using var pipe = new DecoderPipe();
    await pipe.OpenAsync(yol);
    var sink = new AudioSink(pipe.HasAudio);
    pipe.AttachAudioSink(sink);
    foreach (var mode in new[] { "uniform", "mixed" })
    {
        var targets = Targets(sure, 42, mode);
        var startedBefore = pipe.ProcessesStarted;
        foreach (var t in targets)
        {
            await pipe.SeekAsync(t);
            if (pipe.HasAudio) pipe.SeekAudio(t);
        }
        Console.WriteLine($"[{etiket}/{mode}] n={targets.Count} surec_baslatma={pipe.ProcessesStarted - startedBefore} ses_var={pipe.HasAudio}");
    }
    return 0;
}

static async Task<int> K3Async(string etiket, string yol, double sure)
{
    yol = Path.GetFullPath(yol);
    Console.WriteLine("== K3 yeni boru (DecoderPipe) == " + etiket + " " + yol);
    using var pipe = new DecoderPipe();
    await pipe.OpenAsync(yol);
    foreach (var mode in new[] { "uniform", "mixed" })
    {
        var targets = Targets(sure, 42, mode);
        var lat = new List<double>();
        var startedBefore = pipe.ProcessesStarted;
        foreach (var t in targets)
        {
            var sw = Stopwatch.StartNew();
            await pipe.SeekAsync(t);
            sw.Stop();
            lat.Add(sw.Elapsed.TotalMilliseconds);
        }
        var s = lat.OrderBy(x => x).ToList();
        Console.WriteLine($"[{etiket}/{mode}] n={lat.Count} ham_ms=[{string.Join(",", lat.Select(x => x.ToString("F1", CultureInfo.InvariantCulture)))}]");
        Console.WriteLine($"[{etiket}/{mode}] medyan={Median(lat):F1}ms p95={P95(lat):F1}ms maks={s[^1]:F1}ms surec_baslatma={pipe.ProcessesStarted - startedBefore}");
    }
    return 0;
}

static async Task<int> K4Async(string yol, double sure)
{
    yol = Path.GetFullPath(yol);
    Console.WriteLine("== K4 senkron kayma (surekli oynatma, videoPts bagimsiz ilerliyor, T167 olcusu) == " + yol + " sure=" + sure);

    using var pipe = new DecoderPipe();
    await pipe.OpenAsync(yol);
    var sink = new AudioSink(pipe.HasAudio);
    pipe.AttachAudioSink(sink);

    using var playback = pipe.StartContinuousPlayback(0);
    pipe.SeekAudio(0);

    var samples = new List<(double videoPts, double audioPts, double driftMs)>();
    var sw = Stopwatch.StartNew();
    long lastFrames = -1;
    while (sw.Elapsed.TotalSeconds < sure)
    {
        await Task.Delay(5);
        var frames = playback.FramesDecoded;
        if (frames == 0 || frames == lastFrames) continue;
        lastFrames = frames;
        var videoPts = playback.LatestVideoPts;
        var audioPts = sink.PositionSeconds;
        var driftMs = pipe.HasAudio ? (videoPts - audioPts) * 1000.0 : 0;
        samples.Add((videoPts, audioPts, driftMs));
    }

    Console.WriteLine($"ses_var={pipe.HasAudio} n={samples.Count} sure_s={sw.Elapsed.TotalSeconds:F1} kare_sayisi={lastFrames + 1}");
    Console.WriteLine("ham_kayma_ms=[" + string.Join(",", samples.Select(s => s.driftMs.ToString("F2", CultureInfo.InvariantCulture))) + "]");
    Console.WriteLine("videoPts=[" + string.Join(",", samples.Select(s => s.videoPts.ToString("F3", CultureInfo.InvariantCulture))) + "]");
    Console.WriteLine("audioPts=[" + string.Join(",", samples.Select(s => s.audioPts.ToString("F3", CultureInfo.InvariantCulture))) + "]");
    if (samples.Count > 0)
    {
        var maxAbs = samples.Max(s => Math.Abs(s.driftMs));
        var baslangic = samples[0].driftMs;
        var son = samples[^1].driftMs;
        Console.WriteLine($"maksimum_mutlak_kayma_ms={maxAbs:F2}");
        Console.WriteLine($"baslangic_kayma_ms={baslangic:F2} son_kayma_ms={son:F2} degisim_ms={(son - baslangic):F2}");
    }
    return 0;
}

static async Task SeekViaFreshProcessAsync(string yol, double hedefSaniye)
{
    var args = new[]
    {
        "-ss", hedefSaniye.ToString(CultureInfo.InvariantCulture), "-i", yol,
        "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "bgra", "-"
    };
    var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var process = new Process { StartInfo = psi };
    process.Start();
    await process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
    await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
}

static async Task RunAsync(string exe, string[] args)
{
    var psi = new ProcessStartInfo(exe)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var process = new Process { StartInfo = psi };
    process.Start();
    await process.StandardOutput.ReadToEndAsync();
    var err = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        Console.WriteLine("ffmpeg hata: " + err);
}

static async Task<int> RepAsync(string yol)
{
    Console.WriteLine("probe basliyor...");
    var sw = Stopwatch.StartNew();
    using var pipe = new DecoderPipe();
    var openTask = pipe.OpenAsync(yol);
    var done = await Task.WhenAny(openTask, Task.Delay(10000));
    if (done != openTask)
    {
        Console.WriteLine("ZAMAN ASIMI: OpenAsync 10 sn de bitmedi, " + sw.Elapsed);
        return 1;
    }
    await openTask;
    Console.WriteLine("OpenAsync bitti: " + sw.Elapsed + " HasAudio=" + pipe.HasAudio + " Dur=" + pipe.DurationSeconds);
    return 0;
}
