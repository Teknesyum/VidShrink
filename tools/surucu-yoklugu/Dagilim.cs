using System.Diagnostics;

namespace VidShrink.SurucuYoklugu;

public static class Dagilim
{
    private const string NulHedef = "NUL";

    private static readonly int[] YukSeviyeleri = { 0, 4, 8, 16 };

    private sealed record Yoklama(long SureMs, int CikisKodu, string Hata);

    private static ProcessStartInfo YeniPsi(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = VidShrink.Ffmpeg.ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    private static Yoklama HamYoklama(string codec, int timeoutMs)
    {
        var args = new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc2=size=256x256:rate=30:duration=0.1",
            "-c:v", codec, "-frames:v", "1",
            "-f", "null", NulHedef
        };
        var clock = Stopwatch.StartNew();
        using var process = new Process { StartInfo = YeniPsi(args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(true); } catch { }
            clock.Stop();
            return new Yoklama(clock.ElapsedMilliseconds, -1, "SURE-ASIMI");
        }
        Task.WaitAll(new Task[] { stdout, stderr }, 2000);
        clock.Stop();
        var hata = stderr.IsCompletedSuccessfully ? stderr.Result.Trim() : "";
        hata = hata.Replace('\r', ' ').Replace('\n', ' ');
        return new Yoklama(clock.ElapsedMilliseconds, process.ExitCode, hata);
    }

    private static List<Process> YukBaslat(int adet)
    {
        var list = new List<Process>();
        for (var i = 0; i < adet; i++)
        {
            var p = new Process { StartInfo = YeniPsi(new[]
            {
                "-hide_banner", "-loglevel", "quiet",
                "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=30:duration=900",
                "-c:v", "libx264", "-preset", "veryslow", "-f", "null", NulHedef
            }) };
            p.Start();
            list.Add(p);
        }
        return list;
    }

    private static void YukDurdur(List<Process> yuk)
    {
        foreach (var p in yuk)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            p.Dispose();
        }
    }

    private static double IslemciYuzdesi(int ornekMs)
    {
        var once = ToplamIslemciMs();
        var clock = Stopwatch.StartNew();
        Thread.Sleep(ornekMs);
        clock.Stop();
        var pay = ToplamIslemciMs() - once;
        var payda = clock.Elapsed.TotalMilliseconds * Environment.ProcessorCount;
        return payda <= 0 ? 0 : 100.0 * pay / payda;
    }

    private static double ToplamIslemciMs()
    {
        double toplam = 0;
        foreach (var p in Process.GetProcesses())
        {
            try { toplam += p.TotalProcessorTime.TotalMilliseconds; }
            catch { }
            finally { p.Dispose(); }
        }
        return toplam;
    }

    public static void Olc(int tekrar)
    {
        Console.WriteLine();
        Console.WriteLine("6. Yoklama suresinin yuke gore dagilimi");
        Console.WriteLine("---------------------------------------");
        Console.WriteLine($"cekirdek: {Environment.ProcessorCount}   yoklama basina tekrar: {tekrar}");
        Console.WriteLine("Sure asimi 30 s'ye acildi: olculen GERCEK sureler, 4000'de kirpilmamis.");
        Console.WriteLine("'>N' sutunlari: o sinir kullanilsaydi kac yoklama 'calismiyor' sayilirdi.");
        Console.WriteLine("Makine zaten paylasimliydi; ek_yuk BUNUN USTUNE eklenen surec sayisi.");
        Console.WriteLine();

        foreach (var codec in new[] { "h264_nvenc", "h264_amf" })
        {
            Console.WriteLine($"--- {codec} ---");
            Console.WriteLine("ek_yuk islemci%  n   min   ort ortanca   p90   max  >4000 >8000 >15000 cikis0 stderr");
            foreach (var seviye in YukSeviyeleri)
            {
                var yuk = YukBaslat(seviye);
                Thread.Sleep(3000);
                var islemci = IslemciYuzdesi(2000);

                var olcumler = new List<Yoklama>();
                for (var i = 0; i < tekrar; i++) olcumler.Add(HamYoklama(codec, 30_000));
                YukDurdur(yuk);
                Thread.Sleep(1500);

                var s = olcumler.Select(o => o.SureMs).OrderBy(x => x).ToArray();
                var gecen = olcumler.Count(o => o.CikisKodu == 0);
                var hata = olcumler.Select(o => o.Hata).FirstOrDefault(h => h.Length > 0) ?? "(bos)";
                if (hata.Length > 42) hata = hata[..42];

                Console.WriteLine(
                    $"{seviye,6} {islemci,7:0.0} {s.Length,2} {s[0],5} {s.Average(),5:0} {P(s, 0.50),7} " +
                    $"{P(s, 0.90),5} {s[^1],5} {s.Count(x => x > 4000),6} {s.Count(x => x > 8000),5} " +
                    $"{s.Count(x => x > 15000),6} {gecen,6} {hata}");
            }
            Console.WriteLine();
        }
    }

    private static long P(long[] sirali, double q)
    {
        if (sirali.Length == 0) return 0;
        var i = (int)Math.Ceiling(q * sirali.Length) - 1;
        return sirali[Math.Clamp(i, 0, sirali.Length - 1)];
    }

    public static void KodlamaMaliyeti(int saniye)
    {
        Console.WriteLine();
        Console.WriteLine("7. 'Olculemedi' durumunda yanlis secimin bedeli");
        Console.WriteLine("-----------------------------------------------");
        var dizin = Path.Combine(Path.GetTempPath(), "vidshrink_t123_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dizin);
        var ornek = Path.Combine(dizin, "ornek.mp4");
        try
        {
            using (var uret = new Process { StartInfo = YeniPsi(new[]
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", $"testsrc2=size=1920x1080:rate=60:duration={saniye}",
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p", ornek
            }) })
            {
                uret.Start();
                uret.StandardOutput.ReadToEnd();
                uret.StandardError.ReadToEnd();
                uret.WaitForExit();
            }

            Console.WriteLine($"1080p60, {saniye} s ornek; -f null cikis. Uc kosum, en iyisi alindi.");
            Console.WriteLine("kodlayici   duvar_ms  cikis");
            foreach (var codec in new[] { "h264_nvenc", "libx264" })
            {
                var en = long.MaxValue;
                var cikis = -1;
                for (var i = 0; i < 3; i++)
                {
                    var args = new List<string> { "-hide_banner", "-loglevel", "error", "-i", ornek, "-an", "-c:v", codec };
                    if (codec == "libx264") { args.Add("-preset"); args.Add("veryfast"); }
                    args.AddRange(new[] { "-f", "null", NulHedef });
                    var clock = Stopwatch.StartNew();
                    using var p = new Process { StartInfo = YeniPsi(args) };
                    p.Start();
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    clock.Stop();
                    if (clock.ElapsedMilliseconds < en) { en = clock.ElapsedMilliseconds; cikis = p.ExitCode; }
                }
                Console.WriteLine($"{codec,-11} {en,8}  {cikis}");
            }
        }
        finally
        {
            try { Directory.Delete(dizin, true); } catch { }
        }
    }
}
