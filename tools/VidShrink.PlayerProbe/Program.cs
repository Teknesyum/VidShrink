using System.Diagnostics;
using System.Globalization;
using LibVLCSharp.Shared;
using NAudio.Wave;
using VidShrink.Ffmpeg;

namespace VidShrink.PlayerProbe;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Kullanim: PlayerProbe <mod> [args]");
            Console.WriteLine("  gen-sync-clip <cikisYolu> <sureSn>");
            Console.WriteLine("  k1-own <kaynak> <aralikSn> <tekrar>");
            Console.WriteLine("  k1-vlc <kaynak> <aralikSn> <tekrar>");
            Console.WriteLine("  k3-own <senkronKlibi> <sureSn>");
            Console.WriteLine("  k3-vlc <senkronKlibi> <sureSn>");
            return 1;
        }

        return args[0] switch
        {
            "gen-sync-clip" => GenSyncClip(args[1], double.Parse(args[2], CultureInfo.InvariantCulture)),
            "k1-own" => K1Own(args[1], double.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3])),
            "k1-vlc" => K1Vlc(args[1], double.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3])),
            "k1-taban" => K1Taban(int.Parse(args[1])),
            "k3-own" => K3Own(args[1], double.Parse(args[2], CultureInfo.InvariantCulture)),
            "k3-vlc" => K3Vlc(args[1], double.Parse(args[2], CultureInfo.InvariantCulture)),
            _ => Unknown(args[0])
        };
    }

    private static int Unknown(string mod)
    {
        Console.Error.WriteLine($"Bilinmeyen mod: {mod}");
        return 1;
    }

    // ---- gen-sync-clip: video (drawtext saniye sayaci, CFR 30fps) + audio (1 sn'de bir kisa bip) ----
    private static int GenSyncClip(string outPath, double seconds)
    {
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var fontFile = FindRepoFont().Replace("\\", "/").Replace(":", "\\:");
        var videoFilter = $"drawtext=fontfile='{fontFile}':text='%{{eif\\:n/30\\:d}}':x=40:y=40:fontsize=64:fontcolor=white:box=1:boxcolor=black";
        var audioFilter = "aeval='if(lt(mod(t\\,1)\\,0.05)\\,sin(2*PI*1000*t)*0.9\\,0)':c=mono";

        var args = new[]
        {
            "-y",
            "-f", "lavfi", "-i", $"color=size=640x360:rate=30:duration={seconds.ToString(CultureInfo.InvariantCulture)}:color=black",
            "-f", "lavfi", "-i", $"anullsrc=r=44100:cl=mono:d={seconds.ToString(CultureInfo.InvariantCulture)}",
            "-vf", videoFilter,
            "-af", audioFilter,
            "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-g", "30",
            "-c:a", "pcm_s16le",
            outPath
        };

        var exit = RunProcess(ToolLocator.Ffmpeg, args, out var stderrTail, timeoutMs: 120_000);
        Console.WriteLine($"gen-sync-clip cikis kodu: {exit}");
        if (exit != 0) Console.WriteLine(stderrTail);
        return exit;
    }

    // ---- K1 own hat: -ss (input-side, hizli arama) + -frames:v 1, rawvideo bgra ----
    private static int K1Own(string source, double intervalSeconds, int repeats)
    {
        var duration = ProbeDuration(source);
        var results = new List<double>();
        var rng = new Random(42);

        for (var i = 0; i < repeats; i++)
        {
            var baseT = rng.NextDouble() * Math.Max(0.1, duration - intervalSeconds - 1);
            var target = Math.Min(duration - 0.1, baseT + intervalSeconds);

            var sw = Stopwatch.StartNew();
            var args = new[]
            {
                "-ss", target.ToString(CultureInfo.InvariantCulture),
                "-i", source,
                "-an",
                "-frames:v", "1",
                "-f", "rawvideo", "-pix_fmt", "bgra",
                "-"
            };
            var psi = ProcessStartInfoFor(ToolLocator.Ffmpeg, args);
            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.BaseStream;
            var buf = new byte[64 * 1024];
            long total = 0;
            int read;
            while ((read = stdout.Read(buf, 0, buf.Length)) > 0) total += read;
            proc.WaitForExit(10_000);
            sw.Stop();

            results.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"own\t{intervalSeconds}\t{i}\t{sw.Elapsed.TotalMilliseconds:F1}\tbayt={total}");
        }

        ReportGrid("own", intervalSeconds, results);
        return 0;
    }

    // ---- K1 taban: own hattinin her sar isteginde odedigi surec baslatma bedeli.
    // Kaynak yok, kodcozme yok — yalniz ffmpeg surecinin acilip tek 16x16 kare uretip
    // kapanmasi. k1-own'un olcusunden bu taban dusuldugunde geriye sar+kodcozme kalir. ----
    private static int K1Taban(int repeats)
    {
        var results = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var sw = Stopwatch.StartNew();
            var args = new[]
            {
                "-f", "lavfi", "-i", "color=size=16x16:rate=25:duration=0.04:color=black",
                "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "bgra", "-"
            };
            using var proc = Process.Start(ProcessStartInfoFor(ToolLocator.Ffmpeg, args))!;
            var stdout = proc.StandardOutput.BaseStream;
            var buf = new byte[4096];
            long total = 0;
            int read;
            while ((read = stdout.Read(buf, 0, buf.Length)) > 0) total += read;
            proc.WaitForExit(10_000);
            sw.Stop();
            results.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"taban\t0\t{i}\t{sw.Elapsed.TotalMilliseconds:F1}\tbayt={total}");
        }
        ReportGrid("taban", 0, results);
        return 0;
    }

    // ---- K1 LibVLC: SetVideoFormat + SetVideoCallbacks; kronometreyi GOSTERILEN KARENIN
    // kendi icerigi durdurur. Hedefin etrafindaki +-1,5 sn'lik pencere ffmpeg ile ayrik
    // karelere acilip parmak izine cevrilir; ekrana gelen kare "hedeften once" degil
    // "hedeften sonra" kumesine en cok benzediginde saat durur. own hat da ayni olayi
    // olcer (hedef karenin baytlari tamamlandiginda), boylece iki sutun ayni tanim. ----
    private const int FpW = 32;
    private const int FpH = 18;
    private const double MatchFloor = 0.90;
    private const double StaleFloor = 0.9995;

    private static int K1Vlc(string source, double intervalSeconds, int repeats)
    {
        var (width, height, fps) = ProbeVideoInfo(source);
        var half = (int)Math.Round(fps * 1.5);
        if (half < 5) half = 5;

        LibVLCSharp.Shared.Core.Initialize();
        using var libVlc = new LibVLC("--no-video-title-show", "--quiet");
        var duration = ProbeDuration(source);
        var results = new List<double>();
        var rng = new Random(42);

        using var mediaPlayer = new MediaPlayer(libVlc);
        using var media = new Media(libVlc, new Uri(Path.GetFullPath(source)));
        mediaPlayer.Media = media;

        var pitch = width * 4;
        var frameBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(pitch * height);
        mediaPlayer.SetVideoFormat("BGRA", (uint)width, (uint)height, (uint)pitch);

        List<float[]>? before = null;
        List<float[]>? after = null;
        float[]? lastShown = null;
        float[]? preSeek = null;
        ManualResetEventSlim? hit = null;
        var bestAfter = -2.0;
        var bestBefore = -2.0;
        var displayed = 0;
        var stale = 0;

        mediaPlayer.SetVideoCallbacks(
            (_, planes) => { System.Runtime.InteropServices.Marshal.WriteIntPtr(planes, 0, frameBuf); return IntPtr.Zero; },
            (_, _, _) => { },
            (_, _) =>
            {
                var fp = Fingerprint(frameBuf, width, height, pitch);
                Volatile.Write(ref lastShown, fp);
                var b = Volatile.Read(ref before);
                var a = Volatile.Read(ref after);
                if (b is null || a is null) return;
                Interlocked.Increment(ref displayed);
                var ps = Volatile.Read(ref preSeek);
                if (ps is not null && Correlation(ps, fp) > StaleFloor)
                {
                    Interlocked.Increment(ref stale);
                    return;
                }
                var ca = BestCorrelation(a, fp);
                var cb = BestCorrelation(b, fp);
                bestAfter = ca;
                bestBefore = cb;
                if (ca >= MatchFloor && ca > cb) hit?.Set();
            });

        mediaPlayer.Play();
        WaitUntil(() => mediaPlayer.State == VLCState.Playing, 5000);
        mediaPlayer.SetPause(true);
        WaitUntil(() => mediaPlayer.State == VLCState.Paused, 3000);

        for (var i = 0; i < repeats; i++)
        {
            var baseT = rng.NextDouble() * Math.Max(0.1, duration - intervalSeconds - 1);
            var target = Math.Min(duration - 0.1, baseT + intervalSeconds);
            var targetMs = (long)(target * 1000);

            var windowStart = Math.Max(0.0, target - half / fps);
            var fps2 = ReferenceFingerprints(source, windowStart, half * 2, width, height);
            if (fps2.Count < half + 1)
            {
                Console.WriteLine($"vlc\t{intervalSeconds}\t{i}\tREFERANS-YETERSIZ\tkare={fps2.Count}");
                continue;
            }

            var split = (int)Math.Round((target - windowStart) * fps);
            if (split < 1) split = 1;
            if (split > fps2.Count - 1) split = fps2.Count - 1;

            Volatile.Write(ref preSeek, Volatile.Read(ref lastShown));
            Volatile.Write(ref before, fps2.Take(split).ToList());
            Volatile.Write(ref after, fps2.Skip(split).ToList());
            hit = new ManualResetEventSlim(false);
            Volatile.Write(ref displayed, 0);
            Volatile.Write(ref stale, 0);
            bestAfter = -2.0;
            bestBefore = -2.0;

            var sw = Stopwatch.StartNew();
            mediaPlayer.Time = targetMs;
            mediaPlayer.SetPause(false);
            var ok = hit.Wait(5000);
            sw.Stop();
            mediaPlayer.SetPause(true);

            Volatile.Write(ref before, null);
            Volatile.Write(ref after, null);

            if (!ok)
            {
                Console.WriteLine($"vlc\t{intervalSeconds}\t{i}\tESLESME-YOK\tistenen={targetMs}\tkare={displayed}\tbayat={stale}\tson_r_sonra={bestAfter:F4}\tson_r_once={bestBefore:F4}");
                continue;
            }

            results.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"vlc\t{intervalSeconds}\t{i}\t{sw.Elapsed.TotalMilliseconds:F1}\tistenen={targetMs}\tkare={displayed}\tbayat={stale}\tr_sonra={bestAfter:F4}\tr_once={bestBefore:F4}");
        }

        mediaPlayer.Stop();
        System.Runtime.InteropServices.Marshal.FreeHGlobal(frameBuf);
        if (results.Count > 0) ReportGrid("vlc", intervalSeconds, results);
        else Console.WriteLine($"vlc\tGRID\t{intervalSeconds}\tOLCUM-YOK");
        return 0;
    }

    private static List<float[]> ReferenceFingerprints(string source, double startSeconds, int frameCount, int width, int height)
    {
        var args = new[]
        {
            "-ss", startSeconds.ToString(CultureInfo.InvariantCulture),
            "-i", source,
            "-an",
            "-frames:v", frameCount.ToString(CultureInfo.InvariantCulture),
            "-f", "rawvideo", "-pix_fmt", "bgra",
            "-"
        };
        using var proc = Process.Start(ProcessStartInfoFor(ToolLocator.Ffmpeg, args))!;
        var stream = proc.StandardOutput.BaseStream;
        var frameBytes = width * height * 4;
        var buf = new byte[frameBytes];
        var list = new List<float[]>();
        while (ReadExact(stream, buf, frameBytes)) list.Add(Fingerprint(buf, width, height, width * 4));
        proc.WaitForExit(30_000);
        return list;
    }

    private static float[] Fingerprint(byte[] buf, int width, int height, int pitch)
    {
        var fp = new float[FpW * FpH];
        for (var y = 0; y < FpH; y++)
        {
            var sy = (int)((y + 0.5) * height / FpH);
            for (var x = 0; x < FpW; x++)
            {
                var sx = (int)((x + 0.5) * width / FpW);
                fp[y * FpW + x] = buf[sy * pitch + sx * 4 + 1];
            }
        }
        return Normalize(fp);
    }

    private static float[] Fingerprint(IntPtr buf, int width, int height, int pitch)
    {
        var fp = new float[FpW * FpH];
        for (var y = 0; y < FpH; y++)
        {
            var sy = (int)((y + 0.5) * height / FpH);
            for (var x = 0; x < FpW; x++)
            {
                var sx = (int)((x + 0.5) * width / FpW);
                fp[y * FpW + x] = System.Runtime.InteropServices.Marshal.ReadByte(buf, sy * pitch + sx * 4 + 1);
            }
        }
        return Normalize(fp);
    }

    private static float[] Normalize(float[] fp)
    {
        double mean = 0;
        foreach (var v in fp) mean += v;
        mean /= fp.Length;
        double ss = 0;
        for (var i = 0; i < fp.Length; i++)
        {
            fp[i] = (float)(fp[i] - mean);
            ss += fp[i] * (double)fp[i];
        }
        var norm = Math.Sqrt(ss);
        if (norm < 1e-6) return fp;
        for (var i = 0; i < fp.Length; i++) fp[i] = (float)(fp[i] / norm);
        return fp;
    }

    private static double Correlation(float[] a, float[] b)
    {
        double dot = 0;
        for (var i = 0; i < a.Length; i++) dot += a[i] * (double)b[i];
        return dot;
    }

    private static double BestCorrelation(List<float[]> refs, float[] fp)
    {
        var best = -2.0;
        foreach (var r in refs)
        {
            double dot = 0;
            for (var i = 0; i < fp.Length; i++) dot += r[i] * (double)fp[i];
            if (dot > best) best = dot;
        }
        return best;
    }

    private static (int width, int height, double fps) ProbeVideoInfo(string path)
    {
        var psi = ProcessStartInfoFor(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=width,height,avg_frame_rate",
            "-of", "default=noprint_wrappers=1:nokey=1", path
        });
        using var proc = Process.Start(psi)!;
        var text = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
        var w = int.Parse(lines[0], CultureInfo.InvariantCulture);
        var h = int.Parse(lines[1], CultureInfo.InvariantCulture);
        var rate = lines[2].Split('/');
        var fps = double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture);
        return (w, h, fps);
    }

    // ---- K3 own hat: ffmpeg'ten iki ayri pipe (video rawvideo, audio pcm_s16le), NAudio ile
    // sesi calip saati NAudio'nun raporladigi pozisyona gore video karesini eslestirerek olcer.
    // Video borusu uretimin yaptigi gibi "-re" ile gercek zamanda akar
    // (ComparisonGraph.cs:87, PanelHost.cs:460 Realtime=true). Uretimin ikinci pacing
    // katmani (PanelHost.cs:694-720 vsync drain, :902-914 bayat kare dusurme) burada
    // kurulmadi; bu probe yalniz kaynak tarafi pacing'i olcer. ----
    private static int K3Own(string clipPath, double seconds)
    {
        const int fps = 30;
        const int width = 640, height = 360;
        const int sampleRate = 44100;

        var videoArgs = new[] { "-re", "-i", clipPath, "-an", "-f", "rawvideo", "-pix_fmt", "bgra", "-" };
        var audioArgs = new[] { "-i", clipPath, "-vn", "-f", "s16le", "-ar", sampleRate.ToString(), "-ac", "1", "-" };

        using var videoProc = Process.Start(ProcessStartInfoFor(ToolLocator.Ffmpeg, videoArgs))!;
        using var audioProc = Process.Start(ProcessStartInfoFor(ToolLocator.Ffmpeg, audioArgs))!;

        var waveFormat = new WaveFormat(sampleRate, 16, 1);
        var buffer = new BufferedWaveProvider(waveFormat) { BufferDuration = TimeSpan.FromSeconds(seconds + 5), DiscardOnBufferOverflow = false };
        using var output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
        output.Init(buffer);
        double AudioPositionSeconds() => output.GetPosition() / (double)waveFormat.AverageBytesPerSecond;

        var frameBytes = width * height * 4;
        var videoStream = videoProc.StandardOutput.BaseStream;
        var audioStream = audioProc.StandardOutput.BaseStream;

        var pump = new Thread(() =>
        {
            var chunk = new byte[8192];
            int r;
            while ((r = audioStream.Read(chunk, 0, chunk.Length)) > 0)
                buffer.AddSamples(chunk, 0, r);
        }) { IsBackground = true };
        pump.Start();

        var sw = Stopwatch.StartNew();
        output.Play();

        long frameIndex = 0;
        var frameBuf = new byte[frameBytes];
        var samples = new List<(double wallSec, double videoPts, double audioPts)>();

        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var got = ReadExact(videoStream, frameBuf, frameBytes);
            if (!got) break;
            var videoPts = frameIndex / (double)fps;
            frameIndex++;

            var audioPts = AudioPositionSeconds();
            samples.Add((sw.Elapsed.TotalSeconds, videoPts, audioPts));
        }

        output.Stop();
        try { videoProc.Kill(true); } catch { }
        try { audioProc.Kill(true); } catch { }

        ReportSyncSamples("own", samples);
        return 0;
    }

    // ---- K3 LibVLC: SetAudioCallbacks (pts gercek) + SetVideoCallbacks (display anini isaretler,
    // CFR oldugu icin kare index/fps video pts'i verir). ----
    private static int K3Vlc(string clipPath, double seconds)
    {
        const int fps = 30;
        LibVLCSharp.Shared.Core.Initialize();
        using var libVlc = new LibVLC("--no-video-title-show", "--quiet");
        using var media = new Media(libVlc, new Uri(Path.GetFullPath(clipPath)));
        using var mediaPlayer = new MediaPlayer(libVlc);
        mediaPlayer.Media = media;

        var sw = new Stopwatch();
        var videoSamples = new List<(double wallSec, double videoPts)>();
        var audioSamples = new List<(double wallSec, double audioPtsSec)>();
        long frameIndex = 0;
        var gate = new object();

        mediaPlayer.SetVideoFormat("BGRA", 640, 360, 640 * 4);
        var frameBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(640 * 360 * 4);
        mediaPlayer.SetVideoCallbacks(
            (_, planes) => { System.Runtime.InteropServices.Marshal.WriteIntPtr(planes, 0, frameBuf); return IntPtr.Zero; },
            (_, _, _) => { },
            (_, _) =>
            {
                lock (gate)
                {
                    var vp = frameIndex / (double)fps;
                    frameIndex++;
                    videoSamples.Add((sw.Elapsed.TotalSeconds, vp));
                }
            });

        long? firstAudioPtsUs = null;
        mediaPlayer.SetAudioFormat("S16N", 44100, 1);
        mediaPlayer.SetAudioCallbacks(
            (_, samplesPtr, count, pts) =>
            {
                lock (gate)
                {
                    // libvlc'nin pts'i vlc_tick_now() epoch'unda mutlak bir saat; medya
                    // basi 0 degil. Video pts'i (kare index/fps) medya basina gore
                    // oldugundan ilk gozlenen audio pts'i sifir noktasi olarak alip
                    // gorece bir eksene tasiyoruz.
                    firstAudioPtsUs ??= pts;
                    var relativeSec = (pts - firstAudioPtsUs.Value) / 1_000_000.0;
                    audioSamples.Add((sw.Elapsed.TotalSeconds, relativeSec));
                }
            },
            null, null, null, null);

        sw.Start();
        mediaPlayer.Play();
        WaitUntil(() => sw.Elapsed.TotalSeconds > seconds || mediaPlayer.State == VLCState.Ended, (int)(seconds * 1000) + 5000);
        mediaPlayer.Stop();
        System.Runtime.InteropServices.Marshal.FreeHGlobal(frameBuf);

        var merged = new List<(double wallSec, double videoPts, double audioPts)>();
        foreach (var (wallSec, videoPts) in videoSamples)
        {
            var nearestAudio = NearestAudio(audioSamples, wallSec);
            if (nearestAudio is not null) merged.Add((wallSec, videoPts, nearestAudio.Value));
        }

        ReportSyncSamples("vlc", merged);
        return 0;
    }

    private static double? NearestAudio(List<(double wallSec, double audioPtsSec)> samples, double wallSec)
    {
        double? best = null;
        var bestDiff = double.MaxValue;
        foreach (var (w, a) in samples)
        {
            var diff = Math.Abs(w - wallSec);
            if (diff < bestDiff) { bestDiff = diff; best = a; }
        }
        return bestDiff < 0.2 ? best : null;
    }

    // Ilk orneklem atilir: k3-vlc'de sifir noktasi ilk audio callback'in pts'i oldugu icin
    // o orneklem tanimi geregi drift=0 verir ve baslangic penceresinin ortalamasini kirar.
    // Simetri icin own hattinda da atiliyor.
    private static void ReportSyncSamples(string label, List<(double wallSec, double videoPts, double audioPts)> all)
    {
        var samples = all.Skip(1).ToList();
        if (samples.Count == 0)
        {
            Console.WriteLine($"{label}\tsync\torneklem yok");
            return;
        }

        var startWindow = samples.Where(s => s.wallSec < 2.0).ToList();
        var endWindow = samples.Where(s => s.wallSec > samples[^1].wallSec - 2.0).ToList();

        double DriftMs((double wallSec, double videoPts, double audioPts) s) => (s.videoPts - s.audioPts) * 1000.0;

        var startDrift = startWindow.Count > 0 ? startWindow.Average(DriftMs) : double.NaN;
        var endDrift = endWindow.Count > 0 ? endWindow.Average(DriftMs) : double.NaN;

        Console.WriteLine($"{label}\tsync\tornek={samples.Count}\tbaslangic_ms={startDrift:F1}\tson_ms={endDrift:F1}");
        foreach (var s in samples.Take(3).Concat(samples.TakeLast(3)))
            Console.WriteLine($"{label}\tsync-ham\twall={s.wallSec:F2}\tvideoPts={s.videoPts:F3}\taudioPts={s.audioPts:F3}\tdrift_ms={DriftMs(s):F1}");
    }

    private static bool ReadExact(Stream s, byte[] buf, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = s.Read(buf, offset, count - offset);
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    private static void ReportGrid(string label, double intervalSeconds, List<double> results)
    {
        var sorted = results.OrderBy(x => x).ToList();
        var median = sorted[sorted.Count / 2];
        var worst = sorted[^1];
        Console.WriteLine($"{label}\tGRID\t{intervalSeconds}\tmedyan_ms={median:F1}\ten_kotu_ms={worst:F1}\tn={sorted.Count}");
    }

    private static double ProbeDuration(string path)
    {
        var psi = ProcessStartInfoFor(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path
        });
        using var proc = Process.Start(psi)!;
        var text = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return double.Parse(text.Trim(), CultureInfo.InvariantCulture);
    }

    private static ProcessStartInfo ProcessStartInfoFor(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = null
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    private static int RunProcess(string exe, IEnumerable<string> args, out string stderrTail, int timeoutMs)
    {
        var psi = ProcessStartInfoFor(exe, args);
        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(timeoutMs);
        stderrTail = stderr.Length > 2000 ? stderr[^2000..] : stderr;
        return proc.ExitCode;
    }

    private static string FindRepoFont()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VidShrink.sln")))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Repo koku bulunamadi (VidShrink.sln).");
        return Path.Combine(dir.FullName, "src", "VidShrink.App", "Fonts", "AtkinsonHyperlegibleNext-Regular.ttf");
    }

    private static void WaitUntil(Func<bool> cond, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!cond() && sw.ElapsedMilliseconds < timeoutMs) Thread.Sleep(20);
    }
}
