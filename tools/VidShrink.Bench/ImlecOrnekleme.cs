using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Ffmpeg;

public static class ImlecOrnekleme
{
    public static async Task<int> RunAsync(string[] args)
    {
        var saniye = 10;
        var kayitSaniye = 15;
        var tekrar = 2;
        var klasor = Path.Combine(Environment.CurrentDirectory, ".calisma", "imlec-ornekleme");
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seconds" when i + 1 < args.Length:
                    saniye = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--record-seconds" when i + 1 < args.Length:
                    kayitSaniye = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--runs" when i + 1 < args.Length:
                    tekrar = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--out" when i + 1 < args.Length:
                    klasor = args[++i];
                    break;
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("pointer-sampling yalniz Windows'ta olcer (GetPhysicalCursorPos, gdigrab).");
            return 1;
        }

        Directory.CreateDirectory(klasor);
        Console.WriteLine(Fmt($"makine cekirdek={Environment.ProcessorCount} sure={saniye}s kayit={kayitSaniye}s tekrar={tekrar}"));

        var bos = Olc(saniye, null);
        Console.WriteLine(Fmt($"ornekleyici bos cpu-ms={bos.CpuMs:F1} cpu-ms/s={bos.CpuMs / saniye:F3}"));
        foreach (var hz in new[] { 30, 120 })
        {
            var olcu = Olc(saniye, hz);
            var net = olcu.CpuMs - bos.CpuMs;
            Console.WriteLine(Fmt($"ornekleyici hz={hz} okuma={olcu.Okuma} gercek-hz={olcu.Okuma / (double)saniye:F1} cpu-ms={olcu.CpuMs:F1} net-cpu-ms/s={net / saniye:F3} tek-cekirdek-yuzde={net / saniye / 10:F4} hareket={olcu.Hareket}"));
        }

        for (var tur = 1; tur <= tekrar; tur++)
        {
            foreach (var ornekleyici in tur % 2 == 1 ? new[] { false, true } : new[] { true, false })
            {
                var kayit = await KaydetAsync(klasor, kayitSaniye, ornekleyici);
                Console.WriteLine(Fmt($"kayit tur={tur} ornekleyici={(ornekleyici ? "acik" : "kapali")} kare={kayit.Kare} dusen={kayit.Dusen} kopya={kayit.Kopya} ffmpeg-cpu-ms={kayit.FfmpegCpuMs:F0} ffmpeg-cpu-ms/s={kayit.FfmpegCpuMs / kayitSaniye:F1} tezgah-cpu-ms={kayit.TezgahCpuMs:F1} okuma={kayit.Okuma} cikis={kayit.Cikis}"));
            }
        }

        return 0;
    }

    private sealed record OrnekleyiciOlcu(double CpuMs, long Okuma, int Hareket);

    private static OrnekleyiciOlcu Olc(int saniye, int? hz)
    {
        using var kendi = Process.GetCurrentProcess();
        var kayit = new PointerTrackRecorder(0, 0, 1920, 1080, () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
        long okuma = 0;
        using var ornekleyici = hz is { } h ? new PointerSampler(Sayan(() => okuma++), kayit.Move, h) : null;
        kendi.Refresh();
        var once = kendi.TotalProcessorTime;
        ornekleyici?.Start();
        Thread.Sleep(TimeSpan.FromSeconds(saniye));
        ornekleyici?.Stop();
        kendi.Refresh();
        var sonra = kendi.TotalProcessorTime;
        return new OrnekleyiciOlcu((sonra - once).TotalMilliseconds, Interlocked.Read(ref okuma), kayit.Build().Moves.Count);
    }

    private sealed record KayitOlcu(int Kare, int Dusen, int Kopya, double FfmpegCpuMs, double TezgahCpuMs, long Okuma, int Cikis);

    private static async Task<KayitOlcu> KaydetAsync(string klasor, int saniye, bool ornekleyiciAcik)
    {
        var hedef = Path.Combine(klasor, ornekleyiciAcik ? "acik.mkv" : "kapali.mkv");
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-nostdin", "-y", "-f", "gdigrab", "-framerate", "30", "-offset_x", "0", "-offset_y", "0",
                     "-video_size", "1280x720", "-draw_mouse", "1", "-i", "desktop", "-t", saniye.ToString(CultureInfo.InvariantCulture),
                     "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", "-pix_fmt", "yuv420p", hedef
                 })
            psi.ArgumentList.Add(a);

        using var kendi = Process.GetCurrentProcess();
        var kayit = new PointerTrackRecorder(0, 0, 1280, 720, () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
        long okuma = 0;
        using var ornekleyici = ornekleyiciAcik ? new PointerSampler(Sayan(() => okuma++), kayit.Move) : null;
        kendi.Refresh();
        var tezgahOnce = kendi.TotalProcessorTime;

        using var surec = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg baslamadi");
        ornekleyici?.Start();
        var hata = surec.StandardError.ReadToEndAsync();
        var cikti = surec.StandardOutput.ReadToEndAsync();
        await surec.WaitForExitAsync();
        ornekleyici?.Stop();
        var metin = await hata;
        await cikti;
        var ffmpegCpu = surec.TotalProcessorTime.TotalMilliseconds;
        kendi.Refresh();
        var tezgah = (kendi.TotalProcessorTime - tezgahOnce).TotalMilliseconds;

        int kare = 0, kopya = 0, dusen = 0;
        foreach (var satir in metin.Split('\r', '\n'))
        {
            if (!satir.Contains("frame=", StringComparison.Ordinal)) continue;
            if (Sayi(satir, "frame=") is { } f) kare = f;
            if (Sayi(satir, "dup=") is { } d) kopya = d;
            if (Sayi(satir, "drop=") is { } r) dusen = r;
        }

        try { File.Delete(hedef); }
        catch (IOException) { }
        return new KayitOlcu(kare, dusen, kopya, ffmpegCpu, tezgah, Interlocked.Read(ref okuma), surec.ExitCode);
    }

    private static int? Sayi(string satir, string ad)
    {
        var at = satir.IndexOf(ad, StringComparison.Ordinal);
        if (at < 0) return null;
        var m = Regex.Match(satir[(at + ad.Length)..], @"^\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static Func<(int X, int Y)?> Sayan(Action say) => () =>
    {
        say();
        return PointerSampler.ReadSystem();
    };

    private static string Fmt(FormattableString metin) => metin.ToString(CultureInfo.InvariantCulture);
}
