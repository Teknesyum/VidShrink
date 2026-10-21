using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;

public static class GecisOnizleme
{
    private const long Sn = EditTime.TicksPerSecond;

    public static async Task<int> RunAsync(string[] args)
    {
        var klasor = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(".calisma", "gecis-onizleme"));
        var tekrar = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 3;
        Directory.CreateDirectory(klasor);
        Console.WriteLine(Fmt($"ffmpeg {ToolLocator.FfmpegVersion}"));

        var buyuk = Kaynak(klasor, "kaynak-1080.mp4", 1920, 1080, 30, 8);
        var buyukBilgi = await FfprobeClient.ProbeAsync(buyuk);
        foreach (var tur in Enum.GetValues<TransitionKind>())
        {
            var cizelge = Cizelge(tur, 1m, false);
            var sureler = new List<double>();
            for (var i = 0; i < tekrar; i++)
                sureler.Add(Ciz(cizelge, buyuk, buyukBilgi, Path.Combine(klasor, Fmt($"gecis-{tur}-{i}.mkv"))));
            sureler.Sort();
            Console.WriteLine(Fmt($"a tur={tur} boyut=1920x1080 fps=30 gecis=1sn tekrar={tekrar} orta={sureler[sureler.Count / 2]:0} ms en-az={sureler[0]:0} ms en-cok={sureler[^1]:0} ms"));
        }

        var hizli = Cizelge(TransitionKind.Dissolve, 2m, true);
        var hizliSure = Ciz(hizli, buyuk, buyukBilgi, Path.Combine(klasor, "gecis-ters-hizli.mkv"));
        Console.WriteLine(Fmt($"a tur=Dissolve hiz=2 ters=evet boyut=1920x1080 sure={hizliSure:0} ms"));

        var kucuk = Kaynak(klasor, "kaynak-360.mp4", 640, 360, 30, 8);
        var kucukBilgi = await FfprobeClient.ProbeAsync(kucuk);
        await LavfiAsync(kucuk);
        await KarsilastirAsync(klasor, kucuk, kucukBilgi);
        return 0;
    }

    private static EditTimeline Cizelge(TransitionKind tur, decimal hiz, bool ters)
        => new(new[]
        {
            new EditClip(0, 4 * Sn, hiz, ters),
            new EditClip(4 * Sn, 8 * Sn) { Transition = new ClipTransition(tur, Sn) },
        }, 8 * Sn);

    private static double Ciz(EditTimeline cizelge, string kaynak, MediaInfo bilgi, string cikti)
    {
        var tuval = (Math.Max(2, ClipFilters.DisplayWidth(bilgi.Width, bilgi.ParNum, bilgi.ParDen) / 2 * 2), Math.Max(2, bilgi.Height / 2 * 2));
        var argumanlar = TransitionPreview.Arguments(cizelge, 1, new[] { kaynak }, new[] { bilgi }, tuval, bilgi.Fps, bilgi.HasAudio, 0, cikti);
        var saat = Stopwatch.StartNew();
        var (kod, metin) = Kos(ToolLocator.Ffmpeg, argumanlar.ToArray());
        saat.Stop();
        if (kod != 0) throw new InvalidOperationException("on cizim dustu: " + metin[Math.Max(0, metin.Length - 600)..]);
        return saat.Elapsed.TotalMilliseconds;
    }

    private static async Task LavfiAsync(string kaynak)
    {
        var onizleme = new EdlPreview(kaynak, new EditTimeline(new[] { new EditClip(0, 2 * Sn), new EditClip(5 * Sn, 7 * Sn) }, 8 * Sn));
        foreach (var (ad, oncesi) in new[] { ("acildiktan-sonra", false), ("acilmadan-once", true) })
        {
            using var motor = new MpvEngine();
            motor.SetProperty("ao", "null");
            var hata = string.Empty;
            if (oncesi) hata = Dene(() => motor.SetProperty("lavfi-complex", "[vid1]hflip[vo]"));
            await motor.OpenAsync(onizleme.Uri);
            if (!oncesi) hata = Dene(() => motor.SetProperty("lavfi-complex", "[vid1]hflip[vo]"));
            motor.Play();
            var saat = Stopwatch.StartNew();
            while (saat.Elapsed < TimeSpan.FromSeconds(8) && motor.PositionSeconds < 3 && !motor.EndReached) await Task.Delay(50);
            var deger = motor.GetProperty("lavfi-complex") ?? "(yok)";
            var gunluk = string.Join(" | ", motor.RecentLog.Where(s => s.Contains("lavfi", StringComparison.OrdinalIgnoreCase) || s.Contains("error", StringComparison.OrdinalIgnoreCase)).TakeLast(4));
            Console.WriteLine(Fmt($"b kol={ad} hata={(hata.Length == 0 ? "yok" : hata)} deger={deger} konum={motor.PositionSeconds:0.00} sn sinir-gecildi={(motor.PositionSeconds > 2 ? "evet" : "hayir")} kare={motor.FramesRendered} gunluk={(gunluk.Length == 0 ? "-" : gunluk)}"));
        }
    }

    private static async Task KarsilastirAsync(string klasor, string kaynak, MediaInfo bilgi)
    {
        var cizelge = Cizelge(TransitionKind.Wipe, 1m, false);
        var gecis = Path.Combine(klasor, "karsilastirma-gecis.mkv");
        Ciz(cizelge, kaynak, bilgi, gecis);
        var teslim = Path.Combine(klasor, "karsilastirma-teslim.mp4");
        var plan = EditExport.Build(cizelge, bilgi, Array.Empty<double>(), 0, ExportMode.Full, teslim, klasor, 8L << 30);
        var adim = plan.Steps.Single().Args.ToList();
        adim.InsertRange(adim.Count - 1, new[] { "-threads", "2" });
        var (kod, metin) = Kos(ToolLocator.Ffmpeg, adim.ToArray());
        if (kod != 0) throw new InvalidOperationException("teslim dustu: " + metin[Math.Max(0, metin.Length - 600)..]);

        var istatistik = Path.Combine(klasor, "psnr.txt");
        var (pkod, pmetin) = KosIcinde(klasor, ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-threads", "2", "-i", gecis, "-i", teslim, "-filter_complex",
            "[1:v]trim=start_frame=90:end_frame=120,settb=1/30,setpts=N[b];[0:v]settb=1/30,setpts=N[a];[a][b]psnr=stats_file=psnr.txt", "-f", "null", "-");
        if (pkod != 0) throw new InvalidOperationException("psnr dustu: " + pmetin);
        var kareler = File.ReadAllLines(istatistik)
            .Select(l => double.Parse(l.Split(' ').First(x => x.StartsWith("psnr_avg:", StringComparison.Ordinal))[9..], CultureInfo.InvariantCulture)).ToList();
        File.Delete(istatistik);
        Console.WriteLine(Fmt($"c dosya gecis-karesi={kareler.Count} psnr-ort={kareler.Average():0.00} psnr-en-az={kareler.Min():0.00} psnr-en-cok={kareler.Max():0.00}"));

        var yeni = new EdlPreview(new[] { kaynak }, cizelge, new Dictionary<int, string> { [1] = gecis });
        var eski = new EdlPreview(new[] { kaynak }, cizelge);
        foreach (var kareNo in new[] { 60, 95, 99, 104, 110, 150 })
        {
            var an = kareNo / 30.0;
            var teslimler = new[] { -1, 0, 1 }.Select(d => Teslim(teslim, kareNo + d, bilgi.Width, bilgi.Height)).ToArray();
            var cizelgeAni = yeni.ToTimeline(EditTime.FromSeconds(an));
            var (yeniKare, yeniAn) = await OnizlemeAsync(yeni.Uri, EditTime.ToSeconds(yeni.ToEdl(cizelgeAni)) + 0.25 / 30, bilgi.Width, bilgi.Height);
            var (eskiKare, eskiAn) = await OnizlemeAsync(eski.Uri, EditTime.ToSeconds(eski.ToEdl(cizelgeAni)) + 0.25 / 30, bilgi.Width, bilgi.Height);
            var yeniPsnr = teslimler.Select(t => Psnr(yeniKare, t).ToString("0.00", CultureInfo.InvariantCulture));
            var eskiPsnr = teslimler.Select(t => Psnr(eskiKare, t).ToString("0.00", CultureInfo.InvariantCulture));
            Console.WriteLine(Fmt($"c kare={kareNo} teslim-ani={an:0.000} sn cizelge-ani={EditTime.ToSeconds(cizelgeAni):0.000} sn yeni-kare-ani={yeniAn:0.000} eski-kare-ani={eskiAn:0.000} yeni-psnr(n-1,n,n+1)={string.Join("/", yeniPsnr)} eski-psnr(n-1,n,n+1)={string.Join("/", eskiPsnr)}"));
        }
    }

    private static byte[] Teslim(string dosya, int kare, int en, int boy)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", dosya, "-vf", Fmt($"select=eq(n\\,{kare})"), "-frames:v", "1", "-pix_fmt", "bgra", "-f", "rawvideo", "-" })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        using var bellek = new MemoryStream();
        surec.StandardOutput.BaseStream.CopyTo(bellek);
        surec.WaitForExit();
        _ = hata.Result;
        var veri = bellek.ToArray();
        if (veri.Length != en * boy * 4) throw new InvalidOperationException(Fmt($"teslim karesi {kare} okunamadi: {veri.Length} bayt"));
        return veri;
    }

    private static async Task<(byte[] Kare, double An)> OnizlemeAsync(string uri, double saniye, int en, int boy)
    {
        using var motor = new MpvEngine();
        motor.SetProperty("ao", "null");
        await motor.OpenAsync(uri);
        motor.Pause();
        await motor.SeekAsync(saniye, SeekPrecision.Exact);
        var saat = Stopwatch.StartNew();
        long gorulen = 0;
        byte[]? kare = null;
        var kareAni = double.NaN;
        while (saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                if (w != en || h != boy) return;
                var satirlar = new byte[en * boy * 4];
                for (var y = 0; y < boy; y++) Marshal.Copy(p + y * s, satirlar, y * en * 4, en * 4);
                kare = satirlar;
            }, out var an);
            kareAni = an;
            if (kare is not null && Math.Abs(an - saniye) < 0.5 / 30 + 1.0 / 30) break;
            kare = null;
            await Task.Delay(20);
        }

        return (kare ?? throw new InvalidOperationException("onizleme karesi gelmedi: " + string.Join(" | ", motor.RecentLog.TakeLast(5))), kareAni);
    }

    private static double Psnr(byte[] a, byte[] b)
    {
        double kare = 0;
        long sayi = 0;
        for (var i = 0; i < a.Length; i += 4)
            for (var c = 0; c < 3; c++)
            {
                var d = a[i + c] - b[i + c];
                kare += d * d;
                sayi++;
            }

        var mse = kare / sayi;
        return mse == 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 / mse);
    }

    private static string Kaynak(string klasor, string ad, int en, int boy, int fps, int sure)
    {
        var yol = Path.Combine(klasor, ad);
        if (File.Exists(yol)) return yol;
        var (kod, metin) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-f", "lavfi", "-i", Fmt($"testsrc2=s={en}x{boy}:r={fps}:d={sure}"), "-f", "lavfi", "-i", Fmt($"sine=frequency=440:sample_rate=48000:duration={sure}"),
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "16", "-g", fps.ToString(CultureInfo.InvariantCulture), "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-threads", "2", "-shortest", yol);
        if (kod != 0) throw new InvalidOperationException("kaynak uretilemedi: " + metin);
        return yol;
    }

    private static string Dene(Action is_)
    {
        try
        {
            is_();
            return string.Empty;
        }
        catch (Exception e)
        {
            return e.GetType().Name + ": " + e.Message;
        }
    }

    private static (int Kod, string Metin) Kos(string arac, params string[] args) => KosIcinde(Environment.CurrentDirectory, arac, args);

    private static (int Kod, string Metin) KosIcinde(string klasor, string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { WorkingDirectory = klasor, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        surec.WaitForExit();
        return (surec.ExitCode, cikti.Result + hata.Result);
    }

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
