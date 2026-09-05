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

    // ---- K1 LibVLC: Time = ms, PositionChanged/TimeChanged bekle ----
    private static int K1Vlc(string source, double intervalSeconds, int repeats)
    {
        LibVLCSharp.Shared.Core.Initialize();
        using var libVlc = new LibVLC("--no-video-title-show", "--quiet");
        var duration = ProbeDuration(source);
        var results = new List<double>();
        var rng = new Random(42);

        using var mediaPlayer = new MediaPlayer(libVlc);
        using var media = new Media(libVlc, new Uri(Path.GetFullPath(source)));
        mediaPlayer.Media = media;
        mediaPlayer.Play();
        WaitUntil(() => mediaPlayer.State == VLCState.Playing, 5000);
        mediaPlayer.SetPause(true);
        WaitUntil(() => mediaPlayer.State == VLCState.Paused, 3000);

        for (var i = 0; i < repeats; i++)
        {
            var baseT = rng.NextDouble() * Math.Max(0.1, duration - intervalSeconds - 1);
            var target = Math.Min(duration - 0.1, baseT + intervalSeconds);
            var targetMs = (long)(target * 1000);

            long observedMs = -1;
            var gotFrame = new ManualResetEventSlim(false);
            EventHandler<MediaPlayerTimeChangedEventArgs>? handler = null;
            handler = (_, e) =>
            {
                if (Math.Abs(e.Time - targetMs) < 250)
                {
                    observedMs = e.Time;
                    gotFrame.Set();
                }
            };
            mediaPlayer.TimeChanged += handler;

            var sw = Stopwatch.StartNew();
            mediaPlayer.Time = targetMs;
            mediaPlayer.SetPause(false);
            gotFrame.Wait(5000);
            sw.Stop();
            mediaPlayer.SetPause(true);
            mediaPlayer.TimeChanged -= handler;

            results.Add(sw.Elapsed.TotalMilliseconds);
            Console.WriteLine($"vlc\t{intervalSeconds}\t{i}\t{sw.Elapsed.TotalMilliseconds:F1}\tistenen={targetMs}\tgozlenen={observedMs}");
        }

        mediaPlayer.Stop();
        ReportGrid("vlc", intervalSeconds, results);
        return 0;
    }

    // ---- K3 own hat: ffmpeg'ten iki ayri pipe (video rawvideo, audio pcm_s16le), NAudio ile
    // sesi calip saati NAudio'nun raporladigi pozisyona gore video karesini eslestirerek olcer. ----
    private static int K3Own(string clipPath, double seconds)
    {
        const int fps = 30;
        const int width = 640, height = 360;
        const int sampleRate = 44100;

        var videoArgs = new[] { "-i", clipPath, "-an", "-f", "rawvideo", "-pix_fmt", "bgra", "-" };
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

    private static void ReportSyncSamples(string label, List<(double wallSec, double videoPts, double audioPts)> samples)
    {
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
