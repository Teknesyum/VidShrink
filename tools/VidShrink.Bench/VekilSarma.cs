using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;

public static class VekilSarma
{
    private static readonly double[] Konumlar = { 0.7, 5.3, 2.1, 7.4, 3.9, 1.2, 6.6, 4.4, 2.8, 0.3, 5.9, 3.2 };

    public static async Task<int> RunAsync(string[] args)
    {
        var outDir = Path.Combine(Environment.CurrentDirectory, ".calisma", "vekil-sarma");
        var turlar = 3;
        var saniye = 8;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--runs" when i + 1 < args.Length:
                    turlar = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--seconds" when i + 1 < args.Length:
                    saniye = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
            }
        }

        Directory.CreateDirectory(outDir);
        var kaynak = Path.Combine(outDir, "kaynak-4k-hevc10.mkv");
        if (!File.Exists(kaynak))
        {
            var uret = Stopwatch.StartNew();
            await Ffmpeg("-hide_banner", "-nostdin", "-y", "-threads", "2", "-f", "lavfi", "-i",
                "testsrc2=s=3840x2160:r=30:d=" + saniye.ToString(CultureInfo.InvariantCulture),
                "-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p10le",
                "-x265-params", "keyint=60:min-keyint=60:pools=2:frame-threads=1:log-level=error", "-threads", "2", kaynak);
            Console.WriteLine($"kaynak\t{uret.Elapsed.TotalSeconds:0.0} sn\t{new FileInfo(kaynak).Length} bayt");
        }

        var info = await FfprobeClient.ProbeAsync(kaynak, allowAudioOnly: false, CancellationToken.None);
        var klasor = Path.Combine(outDir, "vekiller");
        var vekil = ProxyMedia.Find(klasor, kaynak);
        if (vekil is null)
        {
            var yap = Stopwatch.StartNew();
            vekil = await ProxyRunner.CreateAsync(kaynak, klasor, info.DurationSeconds, info.Width, info.Height, null);
            Console.WriteLine($"vekil\t{yap.Elapsed.TotalSeconds:0.0} sn\t{new FileInfo(vekil).Length} bayt");
        }

        var vekilBilgi = await FfprobeClient.ProbeAsync(vekil, allowAudioOnly: false, CancellationToken.None);
        Console.WriteLine($"kaynak\t{info.Width}x{info.Height}\t{info.VideoCodec}\t{info.BitDepth} bit\t{info.DurationSeconds:0.00} sn");
        Console.WriteLine($"vekil\t{vekilBilgi.Width}x{vekilBilgi.Height}\t{vekilBilgi.VideoCodec}\t{vekilBilgi.BitDepth} bit\t{vekilBilgi.DurationSeconds:0.00} sn");

        var sonuc = new Dictionary<string, List<double>> { ["asil"] = new(), ["vekil"] = new() };
        var dusen = new Dictionary<string, int> { ["asil"] = 0, ["vekil"] = 0 };
        for (var tur = 0; tur < turlar; tur++)
        {
            foreach (var (ad, yol) in tur % 2 == 0 ? new[] { ("asil", kaynak), ("vekil", vekil) } : new[] { ("vekil", vekil), ("asil", kaynak) })
            {
                var (sureler, dusus) = await OlcAsync(yol, info.DurationSeconds);
                sonuc[ad].AddRange(sureler);
                dusen[ad] += dusus;
                Console.WriteLine($"tur{tur + 1}\t{ad}\t{string.Join(" ", sureler.Select(s => s.ToString("0", CultureInfo.InvariantCulture)))}");
            }
        }

        Console.WriteLine("ad\tadet\tdusen\tmedyan_ms\tp90_ms\tortalama_ms\tenkucuk_ms\tenbuyuk_ms");
        foreach (var (ad, liste) in sonuc)
        {
            var s = liste.OrderBy(x => x).ToArray();
            Console.WriteLine(string.Join('\t', ad, s.Length.ToString(CultureInfo.InvariantCulture), dusen[ad].ToString(CultureInfo.InvariantCulture),
                Yuzde(s, 0.5).ToString("0.0", CultureInfo.InvariantCulture), Yuzde(s, 0.9).ToString("0.0", CultureInfo.InvariantCulture),
                s.Average().ToString("0.0", CultureInfo.InvariantCulture), s[0].ToString("0.0", CultureInfo.InvariantCulture),
                s[^1].ToString("0.0", CultureInfo.InvariantCulture)));
        }

        return 0;
    }

    private static async Task<(List<double> Sureler, int Dusus)> OlcAsync(string yol, double sure)
    {
        using var motor = new MpvEngine(new PlaybackOptions { Audio = false, SeekTimeout = TimeSpan.FromSeconds(10) });
        motor.SetProperty("vd-lavc-threads", "2");
        var onizleme = new EdlPreview(Path.GetFullPath(yol), EditTimeline.FromSource(EditTime.FromSeconds(sure)));
        await motor.OpenAsync(onizleme.Uri);
        await motor.SeekAsync(sure / 2, SeekPrecision.Exact);
        var sureler = new List<double>();
        var dusus = 0;
        foreach (var konum in Konumlar)
        {
            var saat = Stopwatch.StartNew();
            var r = await motor.SeekAsync(konum, SeekPrecision.Exact);
            if (r.Outcome == SeekOutcome.Shown) sureler.Add(saat.Elapsed.TotalMilliseconds);
            else dusus++;
        }

        return (sureler, dusus);
    }

    private static double Yuzde(double[] sirali, double p)
    {
        var yer = (sirali.Length - 1) * p;
        var alt = (int)Math.Floor(yer);
        var ust = Math.Min(sirali.Length - 1, alt + 1);
        return sirali[alt] + (sirali[ust] - sirali[alt]) * (yer - alt);
    }

    private static async Task Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        await surec.WaitForExitAsync();
        if (surec.ExitCode != 0) throw new InvalidOperationException(await hata);
    }
}
