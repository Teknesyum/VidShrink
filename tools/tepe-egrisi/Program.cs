using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.TepeEgrisi;

internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static int Main(string[] argv)
    {
        if (argv.Length == 0) { Kullanim(); return 2; }
        var komut = argv[0];
        var p = Ayristir(argv.Skip(1));

        var kaynak = Zorunlu(p, "kaynak");
        var ad = Zorunlu(p, "ad");
        var kodlayici = Zorunlu(p, "kodlayici");
        var isDizin = Zorunlu(p, "is");
        var threads = int.Parse(p.GetValueOrDefault("threads", "6"), Inv);
        Directory.CreateDirectory(Path.Combine(isDizin, "ciktilar"));
        Directory.CreateDirectory(Path.Combine(isDizin, "vmaf"));

        var bilgi = Sonda(kaynak);
        var csv = Path.Combine(isDizin, "olcum.csv");
        var gunluk = Path.Combine(isDizin, "komutlar.log");
        if (!File.Exists(csv))
            File.WriteAllText(csv, "kaynak;kodlayici;yol;gen;yuk;fps;taban_k;oran;tepe;bitrate_k;maxrate_k;bufsize_k;hedef_mib;teslim_mib;hedef_orani;vmaf_mean;vmaf_p10;akis;pixfmt;renk;sure_sn\n");

        switch (komut)
        {
            case "izgara":
            {
                var oranlar = p["oranlar"].Split(',').Select(s => double.Parse(s, Inv)).ToArray();
                var tepeler = p["tepeler"].Split(',').ToArray();
                var tabanK = CodecModel.MinBitrateK(kodlayici, bilgi.Width, bilgi.Height, bilgi.Fps);
                if (tabanK <= 0) tabanK = int.Parse(Zorunlu(p, "taban"), Inv);
                foreach (var oran in oranlar)
                {
                    var bitrateK = (int)Math.Round(tabanK * oran);
                    foreach (var tepe in tepeler)
                        Kos(bilgi, ad, kodlayici, "2gecis", tabanK, oran, tepe, bitrateK, null, isDizin, csv, gunluk, threads);
                }
                return 0;
            }
            case "vbv":
            {
                var crf = int.Parse(Zorunlu(p, "crf"), Inv);
                var tepeler = p["tepeler"].Split(',').ToArray();
                var bitrateK = int.Parse(Zorunlu(p, "bitrate"), Inv);
                var tabanK = CodecModel.MinBitrateK(kodlayici, bilgi.Width, bilgi.Height, bilgi.Fps);
                foreach (var tepe in tepeler)
                    Kos(bilgi, ad, kodlayici, "crf", tabanK, 0, tepe, bitrateK, crf, isDizin, csv, gunluk, threads);
                return 0;
            }
            default:
                Kullanim();
                return 2;
        }
    }

    private static void Kullanim()
    {
        Console.Error.WriteLine("tepe-egrisi izgara --kaynak F --ad A --kodlayici C --oranlar 4.6,10.2 --tepeler 1.02,1.10,1.50 --is D [--threads N] [--taban K]");
        Console.Error.WriteLine("tepe-egrisi vbv    --kaynak F --ad A --kodlayici libx265 --crf 23 --bitrate K --tepeler yok,1.10,1.25,1.50,2.00 --is D");
    }

    private static Dictionary<string, string> Ayristir(IEnumerable<string> argv)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        string? anahtar = null;
        foreach (var a in argv)
        {
            if (a.StartsWith("--", StringComparison.Ordinal)) { anahtar = a[2..]; d[anahtar] = "true"; }
            else if (anahtar is not null) { d[anahtar] = a; anahtar = null; }
        }
        return d;
    }

    private static string Zorunlu(Dictionary<string, string> p, string ad)
        => p.TryGetValue(ad, out var v) ? v : throw new ArgumentException($"eksik: --{ad}");

    private static MediaInfo Sonda(string yol)
    {
        var json = FfKos("ffprobe", new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=width,height,r_frame_rate,pix_fmt,color_space,color_transfer,color_primaries,codec_name",
            "-show_entries", "format=duration,size",
            "-of", "json", yol
        }, out var sondaHata) ?? throw new InvalidOperationException($"ffprobe hata {yol}: {sondaHata}");
        using var doc = JsonDocument.Parse(json);
        var s = doc.RootElement.GetProperty("streams")[0];
        var f = doc.RootElement.GetProperty("format");
        var rate = s.GetProperty("r_frame_rate").GetString()!.Split('/');
        var fps = double.Parse(rate[0], Inv) / double.Parse(rate[1], Inv);
        var trc = s.TryGetProperty("color_transfer", out var t) ? t.GetString() : null;
        var boyut = long.Parse(f.GetProperty("size").GetString()!, Inv);
        var sure = double.Parse(f.GetProperty("duration").GetString()!, Inv);
        return new MediaInfo
        {
            FilePath = yol,
            FileSizeBytes = boyut,
            DurationSeconds = sure,
            Width = s.GetProperty("width").GetInt32(),
            Height = s.GetProperty("height").GetInt32(),
            Fps = fps,
            VideoCodec = s.GetProperty("codec_name").GetString()!,
            TotalBitrateBps = (long)(boyut * 8 / sure),
            IsHdr = trc is "smpte2084" or "arib-std-b67",
            PixelFormat = s.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() : null,
            ColorPrimaries = s.TryGetProperty("color_primaries", out var cp) ? cp.GetString() : null,
            ColorTransfer = trc,
            ColorSpace = s.TryGetProperty("color_space", out var cs) ? cs.GetString() : null,
            BitDepth = 10
        };
    }

    private static void Kos(
        MediaInfo bilgi, string ad, string kodlayici, string yol, int tabanK, double oran,
        string tepeMetin, int bitrateK, int? crf, string isDizin, string csv, string gunluk, int threads)
    {
        var kap = EncoderCapabilities.Instance;
        FfmpegArguments.WarmPsychovisual(kodlayici, kap);
        var hdr = HdrResolver.Resolve(bilgi, HdrPolicy.Preserve, kodlayici, kap);

        var plan = new EncodePlan
        {
            Codec = kodlayici,
            Mode = crf is null ? "2pass" : "crf",
            VideoBitrateK = bitrateK,
            Crf = crf,
            AudioCodec = null,
            Width = bilgi.Width,
            Height = bilgi.Height,
            Fps = bilgi.Fps,
            Preset = FfmpegArguments.DefaultPreset(kodlayici),
            PixelFormat = hdr.PixelFormat,
            HdrVideoFilter = hdr.VideoFilter,
            HdrColorArgs = new List<string>(hdr.ColorArgs),
            ExtraArgs = Sabitlenmis(kodlayici, threads)
        };

        var etiket = $"{ad}-{kodlayici}-{yol}-{(crf is null ? oran.ToString("0.000", Inv) : "crf" + crf)}-t{tepeMetin}".Replace('.', '_');
        var cikti = Path.Combine(isDizin, "ciktilar", etiket + ".mp4");
        var vmafJson = Path.Combine(isDizin, "vmaf", etiket + ".json");

        double tepe = tepeMetin == "yok" ? 0 : double.Parse(tepeMetin, Inv);
        int maxrateK = 0, bufsizeK = 0;

        var sw = Stopwatch.StartNew();
        if (!File.Exists(cikti))
        {
            var passLog = Path.Combine(isDizin, "ciktilar", etiket);
            var iki = FfmpegArguments.NeedsTwoPasses(kodlayici) && crf is null;
            if (iki)
            {
                var p1 = TepeyiDegistir(FfmpegArguments.Build(bilgi, plan, cikti, 1, passLog, kap), bitrateK, tepe, crf is not null, out _, out _);
                Yaz(gunluk, etiket + " [1]", p1);
                if (FfKos("ffmpeg", p1, out var h1) is null) throw new InvalidOperationException($"pass1 hata {etiket}: {h1}");
            }
            var son = TepeyiDegistir(FfmpegArguments.Build(bilgi, plan, cikti, iki ? 2 : 0, iki ? passLog : null, kap), bitrateK, tepe, crf is not null, out maxrateK, out bufsizeK);
            Yaz(gunluk, etiket, son);
            if (FfKos("ffmpeg", son, out var h2) is null) throw new InvalidOperationException($"kodlama hata {etiket}: {h2}");
        }
        else
        {
            TepeyiDegistir(FfmpegArguments.Build(bilgi, plan, cikti, 0, null, kap), bitrateK, tepe, crf is not null, out maxrateK, out bufsizeK);
        }
        sw.Stop();

        var teslimBayt = new FileInfo(cikti).Length;
        var teslimMib = teslimBayt / 1048576.0;
        var hedefMib = bitrateK * 1000.0 * bilgi.DurationSeconds / 8.0 / 1048576.0;
        var (akis, pixfmt, renk) = CiktiKapisi(cikti);

        if (!File.Exists(vmafJson)) Vmaf(cikti, bilgi.FilePath, vmafJson, gunluk, threads);
        var (ortalama, p10) = VmafOku(vmafJson);

        var satir = string.Join(';', new[]
        {
            ad, kodlayici, yol,
            bilgi.Width.ToString(Inv), bilgi.Height.ToString(Inv), bilgi.Fps.ToString("0.###", Inv),
            tabanK.ToString(Inv), oran.ToString("0.0000", Inv), tepeMetin,
            bitrateK.ToString(Inv), maxrateK.ToString(Inv), bufsizeK.ToString(Inv),
            hedefMib.ToString("0.0000", Inv), teslimMib.ToString("0.0000", Inv),
            (teslimMib / hedefMib).ToString("0.0000", Inv),
            ortalama.ToString("0.0000", Inv), p10.ToString("0.0000", Inv),
            akis.ToString(Inv), pixfmt, renk,
            sw.Elapsed.TotalSeconds.ToString("0.0", Inv)
        });
        File.AppendAllText(csv, satir + "\n");
        Console.WriteLine(satir);
    }

    private static List<string> Sabitlenmis(string kodlayici, int threads)
    {
        var e = new List<string> { "-threads", threads.ToString(Inv) };
        if (kodlayici.Equals("libx265", StringComparison.OrdinalIgnoreCase))
            e.AddRange(new[] { "-x265-params", $"pools={threads}:frame-threads=2" });
        if (kodlayici.Equals("libsvtav1", StringComparison.OrdinalIgnoreCase))
            e.AddRange(new[] { "-svtav1-params", $"lp={threads}" });
        return e;
    }

    private static IReadOnlyList<string> TepeyiDegistir(
        IReadOnlyList<string> args, int bitrateK, double tepe, bool crfYolu, out int maxrateK, out int bufsizeK)
    {
        maxrateK = 0;
        bufsizeK = 0;
        var yeni = new List<string>(args.Count);
        for (var i = 0; i < args.Count; i++)
        {
            if ((args[i] == "-maxrate" || args[i] == "-bufsize") && i + 1 < args.Count)
            {
                i++;
                continue;
            }
            yeni.Add(args[i]);
        }
        if (tepe <= 0) return yeni;

        maxrateK = (int)(bitrateK * tepe);
        bufsizeK = (int)(bitrateK * (crfYolu ? tepe * 2.0 : FfmpegArguments.BufferFactor(tepe)));
        var at = yeni.IndexOf("-preset");
        at = at >= 0 ? at + 2 : yeni.Count;
        yeni.InsertRange(at, new[] { "-maxrate", $"{maxrateK}k", "-bufsize", $"{bufsizeK}k" });
        return yeni;
    }

    private static (int Akis, string PixFmt, string Renk) CiktiKapisi(string yol)
    {
        var json = FfKos("ffprobe", new[]
        {
            "-v", "error", "-show_entries", "stream=index,pix_fmt,color_space,color_transfer,color_primaries",
            "-of", "json", yol
        }, out var kapiHata) ?? throw new InvalidOperationException($"ffprobe hata {yol}: {kapiHata}");
        using var doc = JsonDocument.Parse(json);
        var akislar = doc.RootElement.GetProperty("streams");
        var v = akislar[0];
        string G(string ad) => v.TryGetProperty(ad, out var e) ? e.GetString() ?? "-" : "-";
        return (akislar.GetArrayLength(), G("pix_fmt"), $"{G("color_space")}/{G("color_transfer")}/{G("color_primaries")}");
    }

    private static void Vmaf(string test, string referans, string ciktiJson, string gunluk, int threads)
    {
        var yolu = ciktiJson.Replace('\\', '/').Replace(":", "\\:");
        var args = new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-i", test, "-i", referans,
            "-lavfi", $"[0:v][1:v]libvmaf=model=version=vmaf_v0.6.1neg:n_threads={threads}:log_fmt=json:log_path={yolu}",
            "-f", "null", "-"
        };
        Yaz(gunluk, "vmaf " + Path.GetFileName(ciktiJson), args);
        if (FfKos("ffmpeg", args, out var hata) is null)
            throw new InvalidOperationException($"vmaf hata {ciktiJson}: {hata}");
    }

    private static (double Ortalama, double P10) VmafOku(string json)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var puanlar = doc.RootElement.GetProperty("frames")
            .EnumerateArray()
            .Select(f => f.GetProperty("metrics").GetProperty("vmaf").GetDouble())
            .ToArray();
        if (puanlar.Length == 0) throw new InvalidOperationException($"vmaf karesiz: {json}");
        var sirali = puanlar.OrderBy(x => x).ToArray();
        var idx = (int)Math.Floor(0.10 * (sirali.Length - 1));
        return (puanlar.Average(), sirali[idx]);
    }

    private static void Yaz(string gunluk, string etiket, IEnumerable<string> args)
        => File.AppendAllText(gunluk, $"### {etiket}\n{FfmpegArguments.ToCommandLine(args)}\n\n");

    private static string? FfKos(string arac, IEnumerable<string> args, out string hata)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var cikti = proc.StandardOutput.ReadToEnd();
        hata = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? cikti : null;
    }
}
