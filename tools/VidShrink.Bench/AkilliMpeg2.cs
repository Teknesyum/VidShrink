using System.Diagnostics;
using System.Globalization;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

/// <summary>
/// Akilli kipte MPEG-2 kaynagin birlesim noktalari. Kaynak 8 sn 320x240 24 kare/sn lavfi; uc kesimli cizelge
/// akilli kipte teslim edilir. Cikti kare ve paket sayisi beklenenle, kare hizi kaynakla karsilastirilir; cozme
/// stderr'i sayilir; her cikti karesi kaynaktaki karsiligiyla PSNR'la olculur, birlesim noktalarinin iki
/// yanindaki uc karenin en dusugu ayrica yazilir.
/// </summary>
public static class AkilliMpeg2
{
    private const int Fps = 24;

    private static readonly (double Bas, double Son)[] Kesimler = { (0.5, 3.0), (3.5, 5.75), (6.25, 7.5) };

    private static readonly string[] Kapali = { "-c:v", "mpeg2video", "-b:v", "2M", "-bf", "2", "-g", "24", "-flags", "+cgop", "-sc_threshold", "1000000000" };

    private static readonly Dictionary<string, (string Kap, string[] Kodlayici, string Cikti)> Kollar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kapali-mkv"] = ("mkv", Kapali, "mkv"),
        ["kapali-mp4"] = ("mp4", Kapali, "mp4"),
        ["kapali-ts"] = ("ts", Kapali, "mkv"),
        ["taramali-tt-mkv"] = ("mkv", Kapali.Concat(new[] { "-flags", "+cgop+ildct+ilme", "-vf", "setfield=tff" }).ToArray(), "mkv"),
        ["taramali-bb-ts"] = ("ts", Kapali.Concat(new[] { "-flags", "+cgop+ildct+ilme", "-vf", "setfield=bff" }).ToArray(), "mp4"),
        ["acik-mp4"] = ("mp4", new[] { "-c:v", "mpeg2video", "-b:v", "2M", "-bf", "2", "-g", "24" }, "mp4")
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var secilen = Kollar.Keys.ToArray();
        var klasor = Path.Combine(".calisma", "akilli-mpeg2");
        var esik = 30.0;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--arms" when i + 1 < args.Length:
                    secilen = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--out" when i + 1 < args.Length:
                    klasor = args[++i];
                    break;
                case "--min-psnr" when i + 1 < args.Length:
                    esik = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
            }
        }

        klasor = Path.GetFullPath(klasor);
        Directory.CreateDirectory(klasor);
        var araliklar = Kesimler.Select(k => (Ilk: (int)Math.Round(k.Bas * Fps), Adet: (int)Math.Round((k.Son - k.Bas) * Fps))).ToArray();
        var beklenen = araliklar.Sum(a => a.Adet);
        var dikisler = new List<int>();
        var toplam = 0;
        foreach (var a in araliklar.Take(araliklar.Length - 1))
        {
            toplam += a.Adet;
            dikisler.Add(toplam);
        }

        Console.WriteLine(Fmt($"ffmpeg {ToolLocator.FfmpegVersion}"));
        Console.WriteLine(Fmt($"kesimler {string.Join(" ", Kesimler.Select(k => Fmt($"{k.Bas:0.###}-{k.Son:0.###}")))} beklenen-kare {beklenen} dikis {string.Join(",", dikisler)} esik-psnr {esik:0.0}"));

        var kalan = 0;
        foreach (var ad in secilen)
        {
            if (!Kollar.TryGetValue(ad, out var kol))
            {
                Console.WriteLine(Fmt($"kol={ad} bilinmiyor"));
                kalan++;
                continue;
            }

            var kaynak = Path.Combine(klasor, "kaynak-" + ad + "." + kol.Kap);
            var uretArgs = new List<string>
            {
                "-hide_banner", "-nostdin", "-y", "-v", "error",
                "-f", "lavfi", "-i", "testsrc2=s=320x240:r=24:d=8", "-f", "lavfi", "-i", "sine=f=440:d=8", "-threads", "2"
            };
            uretArgs.AddRange(kol.Kodlayici);
            uretArgs.AddRange(new[] { "-c:a", "aac", "-shortest", kaynak });
            var (uret, uretHata) = Kos(ToolLocator.Ffmpeg, uretArgs.ToArray());
            if (uret != 0)
            {
                Console.WriteLine(Fmt($"kol={ad} kaynak uretilemedi: {Ilk(uretHata)}"));
                kalan++;
                continue;
            }

            var cikti = Path.Combine(klasor, "cikti-" + ad + "." + kol.Cikti);
            var cizelge = new EditTimeline(Kesimler.Select(k => new EditClip(EditTime.FromSeconds(k.Bas), EditTime.FromSeconds(k.Son))).ToArray());
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Smart, cikti, 8_000_000_000);
            plan = plan with
            {
                Steps = plan.Steps.Select(s => s with { Args = s.Args.Take(s.Args.Count - 1).Concat(new[] { "-threads", "2", s.Args[^1] }).ToArray() }).ToArray()
            };
            var govde = plan.Steps.Count(s => s.Args.Contains("-frames:v"));
            if (plan.Effective != ExportMode.Smart)
            {
                Console.WriteLine(Fmt($"kol={ad} plan={plan.Effective} akilli kip kurulamadi"));
                kalan++;
                continue;
            }

            try
            {
                await EditExportRunner.RunAsync(plan, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(Fmt($"kol={ad} teslim dustu: {Ilk(ex.Message)}"));
                kalan++;
                continue;
            }

            var kb = Oku(kaynak, false);
            var cb = Oku(cikti, true);
            var (cozKod, cozHata) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-f", "null", "-");
            var hataBayt = cozHata.Trim().Length;
            var psnr = Psnr(klasor, cikti, kaynak, araliklar);
            var birebir = psnr.Count(double.IsPositiveInfinity);
            var enDusuk = psnr.Count == 0 ? double.NaN : psnr.Min();
            var dikisPsnr = dikisler.Concat(new[] { 0, beklenen })
                .SelectMany(d => Enumerable.Range(d - 3, 6)).Where(n => n >= 0 && n < psnr.Count).Select(n => psnr[n])
                .DefaultIfEmpty(double.NaN).Min();
            var kare = cb.GetValueOrDefault("nb_read_frames");
            var paket = cb.GetValueOrDefault("nb_read_packets");
            var alanTutar = kb.GetValueOrDefault("field_order") is "progressive" or null
                ? cb.GetValueOrDefault("field_order") is "progressive" or null
                : cb.GetValueOrDefault("field_order") is "tt" or "bb" or "tb" or "bt";
            var tamam = kare == beklenen.ToString(CultureInfo.InvariantCulture) && paket == kare && psnr.Count == beklenen
                        && cozKod == 0 && hataBayt == 0 && enDusuk >= esik
                        && cb.GetValueOrDefault("r_frame_rate") == kb.GetValueOrDefault("r_frame_rate")
                        && cb.GetValueOrDefault("codec_name") == "mpeg2video" && alanTutar;
            if (!tamam) kalan++;
            Console.WriteLine(Fmt($"kol={ad} adim={plan.Steps.Count} govde-kopya={govde} kare={kare}/{beklenen} paket={paket} sure={cb.GetValueOrDefault("duration")} ") +
                                  Fmt($"hiz={cb.GetValueOrDefault("r_frame_rate")}/{kb.GetValueOrDefault("r_frame_rate")} alan={cb.GetValueOrDefault("field_order")}/{kb.GetValueOrDefault("field_order")} ") +
                                  Fmt($"profil={cb.GetValueOrDefault("profile")} cozme-hatasi-bayt={hataBayt} birebir={birebir} en-dusuk-psnr={enDusuk:0.00} dikis-psnr={dikisPsnr:0.00} sonuc={(tamam ? "gecti" : "kaldi")}"));
        }

        return kalan == 0 ? 0 : 2;
    }

    private static List<double> Psnr(string klasor, string cikti, string kaynak, (int Ilk, int Adet)[] araliklar)
    {
        var secim = string.Join("+", araliklar.Select(a => Fmt($"between(n\\,{a.Ilk}\\,{a.Ilk + a.Adet - 1})")));
        var log = Path.Combine(klasor, "psnr.log");
        if (File.Exists(log)) File.Delete(log);
        var cizge = $"[0:v]settb=AVTB,setpts=N/{Fps}/TB[a];[1:v]select={secim},settb=AVTB,setpts=N/{Fps}/TB[b];[a][b]psnr=stats_file=psnr.log";
        var psi = Bilgi(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-i", kaynak, "-lavfi", cizge, "-an", "-f", "null", "-");
        psi.WorkingDirectory = klasor;
        Calistir(psi);
        if (!File.Exists(log)) return new List<double>();
        return File.ReadAllLines(log).Where(s => s.Contains("psnr_avg:", StringComparison.Ordinal))
            .Select(s => s.Split("psnr_avg:")[1].Split(' ')[0])
            .Select(d => d == "inf" ? double.PositiveInfinity : double.Parse(d, CultureInfo.InvariantCulture)).ToList();
    }

    private static Dictionary<string, string> Oku(string dosya, bool say)
    {
        var args = new List<string> { "-v", "error", "-select_streams", "v:0" };
        if (say) args.AddRange(new[] { "-count_packets", "-count_frames" });
        args.AddRange(new[]
        {
            "-show_entries", "stream=codec_name,profile,field_order,r_frame_rate,nb_read_packets,nb_read_frames:format=duration",
            "-of", "default=noprint_wrappers=1", dosya
        });
        var (_, cikti) = Kos(ToolLocator.Ffprobe, args.ToArray());
        return cikti.Split('\n').Select(s => s.Trim().Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
    }

    private static ProcessStartInfo Bilgi(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    private static (int Kod, string Metin) Calistir(ProcessStartInfo psi)
    {
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        surec.WaitForExit();
        var ust = cikti.GetAwaiter().GetResult();
        var alt = hata.GetAwaiter().GetResult();
        return (surec.ExitCode, ust.Length > 0 ? ust + alt : alt);
    }

    private static (int Kod, string Metin) Kos(string arac, params string[] args) => Calistir(Bilgi(arac, args));

    private static string Ilk(string metin) => metin.Trim().Split('\n')[0].Trim();

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
