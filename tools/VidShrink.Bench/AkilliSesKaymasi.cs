using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

/// <summary>
/// Akilli kipte sesi aynen kopyalanan kodeklerin ses-goruntu kaymasi. Kaynak 12 sn siyah, her tam
/// saniyede bir beyaz kare ve 50 ms 1 kHz ton tasir. Uc kesimli cizelge akilli kipte teslim edilir,
/// ciktidaki her beyaz karenin zamani ile ayni saniyenin tonunun basi karsilastirilir. Kayma, ciktidaki
/// farkin kaynaktaki farktan sapmasidir. Ses kapisi atlanir (bilgi AAC diye islenir), boylece kapinin
/// disindaki kodek de olculur.
/// </summary>
public static class AkilliSesKaymasi
{
    private const double Fps = 24;

    private static readonly (double Bas, double Son)[] Kesimler = { (0.5, 3.25), (4.625, 7.25), (8.125, 11.625) };

    private static readonly Dictionary<string, string> Kodlayicilar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aac"] = "aac",
        ["ac3"] = "ac3",
        ["eac3"] = "eac3",
        ["mp3"] = "libmp3lame",
        ["opus"] = "libopus"
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var kodekler = new[] { "aac", "ac3", "eac3", "mp3", "opus" };
        var kaplar = new[] { "mkv", "mp4", "mov", "m4v" };
        var klasor = Path.Combine(".calisma", "akilli-ses-kaymasi");
        var esikMs = 1000 / Fps;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--codecs" when i + 1 < args.Length:
                    kodekler = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--containers" when i + 1 < args.Length:
                    kaplar = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--out" when i + 1 < args.Length:
                    klasor = args[++i];
                    break;
                case "--threshold-ms" when i + 1 < args.Length:
                    esikMs = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
            }
        }

        klasor = Path.GetFullPath(klasor);
        Directory.CreateDirectory(klasor);
        Console.WriteLine(Fmt($"ffmpeg {ToolLocator.FfmpegVersion}"));
        Console.WriteLine(Fmt($"kesimler {string.Join(" ", Kesimler.Select(k => Fmt($"{k.Bas:0.###}-{k.Son:0.###}")))} esik-ms {esikMs:0.0}"));

        var kalan = 0;
        foreach (var kodek in kodekler)
        {
            if (!Kodlayicilar.TryGetValue(kodek, out var kodlayici))
            {
                Console.WriteLine(Fmt($"kodek={kodek} bilinmiyor"));
                kalan++;
                continue;
            }

            var kaynak = Path.Combine(klasor, "kaynak-" + kodek + ".mkv");
            var (uret, uretHata) = Kos(ToolLocator.Ffmpeg,
                "-hide_banner", "-nostdin", "-y", "-v", "error",
                "-f", "lavfi", "-i", "color=c=black:s=320x240:r=24:d=12,drawbox=x=0:y=0:w=320:h=240:color=white:t=fill:enable='eq(mod(n\\,24)\\,0)'",
                "-f", "lavfi", "-i", "aevalsrc='if(lt(mod(t\\,1)\\,0.05)\\,0.5*sin(2*PI*1000*t)\\,0)':s=48000:d=12",
                "-threads", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-g", "24", "-c:a", kodlayici, "-b:a", "192k", "-shortest", kaynak);
            if (uret != 0)
            {
                Console.WriteLine(Fmt($"kodek={kodek} kaynak uretilemedi: {Ilk(uretHata)}"));
                kalan++;
                continue;
            }

            var kaynakOfset = Ofsetler(kaynak, out _).Select(o => o.Ses - o.Goruntu).ToArray();
            var taban = kaynakOfset.Length > 0 ? kaynakOfset.Average() : 0;
            var bilgi = await FfprobeClient.ProbeAsync(kaynak);
            var csv = await EditExportRunner.ProbeKeyframesAsync(kaynak);
            var (kareler, baslangic) = EditExport.ParseKeyframes(csv);
            var sinirlar = SmartCutCodec.CleanCuts(SmartCutCodec.ParsePackets(csv), baslangic);
            var cizelge = new EditTimeline(Kesimler.Select(k => new EditClip(EditTime.FromSeconds(k.Bas), EditTime.FromSeconds(k.Son))).ToArray());

            foreach (var kap in kaplar)
            {
                var cikti = Path.Combine(klasor, "cikti-" + kodek + "." + kap);
                var is_ = Path.Combine(klasor, "is-" + kodek + "-" + kap);
                var plan = EditExport.Build(cizelge, bilgi with { AudioCodec = "aac" }, kareler, baslangic, ExportMode.Smart,
                    EncodeRunner.PartialPathFor(cikti), is_, 8_000_000_000, sinirlar, EncoderCapabilities.Instance.HasEncoder);
                plan = plan with
                {
                    OutputPath = cikti,
                    Steps = plan.Steps.Select(s => s with { Args = s.Args.Take(s.Args.Count - 1).Concat(new[] { "-threads", "2", s.Args[^1] }).ToArray() }).ToArray()
                };
                if (plan.Effective != ExportMode.Smart)
                {
                    Console.WriteLine(Fmt($"kodek={kodek} kap={kap} plan={plan.Effective} akilli kip kurulamadi"));
                    kalan++;
                    continue;
                }

                try
                {
                    await EditExportRunner.RunAsync(plan, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Fmt($"kodek={kodek} kap={kap} teslim dustu: {Ilk(ex.Message)}"));
                    kalan++;
                    continue;
                }

                var (cozKod, cozHata) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-f", "null", "-");
                var ciktiKodek = SesKodegi(cikti);
                var olcu = Ofsetler(cikti, out var sayi);
                var kaymalar = olcu.Select(o => (o.Ses - o.Goruntu - taban) * 1000).ToArray();
                var maks = kaymalar.Length == 0 ? double.NaN : kaymalar.Max(Math.Abs);
                var hataSayisi = string.IsNullOrWhiteSpace(cozHata) ? 0 : cozHata.Trim().Split('\n').Length;
                var tamam = sayi.Goruntu == 9 && olcu.Count == 9 && maks <= esikMs && cozKod == 0 && hataSayisi == 0 && ciktiKodek == kodek;
                if (!tamam) kalan++;
                var dizi = string.Join(",", kaymalar.Select(k => k.ToString("0.0", CultureInfo.InvariantCulture)));
                Console.WriteLine(Fmt($"kodek={kodek} kap={kap} cikti-ses={ciktiKodek} isaret={sayi.Goruntu}/{olcu.Count} kaynak-ofset-ms={taban * 1000:0.0} kayma-ms=[{dizi}] maks-ms={maks:0.0} cozme-hatasi={hataSayisi} sonuc={(tamam ? "gecti" : "kaldi")}"));
            }
        }

        return kalan == 0 ? 0 : 2;
    }

    private static IReadOnlyList<(double Goruntu, double Ses)> Ofsetler(string dosya, out (int Goruntu, int Ses) sayi)
    {
        var (_, vlog) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-threads", "2", "-i", dosya, "-map", "0:v:0",
            "-vf", "signalstats,metadata=mode=print:key=lavfi.signalstats.YAVG", "-f", "null", "-");
        var goruntu = new List<double>();
        double? zaman = null;
        foreach (var satir in vlog.Split('\n'))
        {
            var pts = Regex.Match(satir, @"pts_time:(-?[0-9.]+)");
            if (pts.Success) zaman = double.Parse(pts.Groups[1].Value, CultureInfo.InvariantCulture);
            var y = Regex.Match(satir, @"lavfi\.signalstats\.YAVG=([0-9.]+)");
            if (y.Success && zaman is { } t && double.Parse(y.Groups[1].Value, CultureInfo.InvariantCulture) > 128) goruntu.Add(t);
        }

        var (_, alog) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-threads", "2", "-i", dosya, "-map", "0:a:0",
            "-af", "silencedetect=n=-30dB:d=0.2", "-f", "null", "-");
        var ses = Regex.Matches(alog, @"silence_end: (-?[0-9.]+)")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        sayi = (goruntu.Count, ses.Count);

        var ciftler = new List<(double, double)>();
        foreach (var g in goruntu)
        {
            if (ses.Count == 0) break;
            var en = ses.OrderBy(s => Math.Abs(s - g)).First();
            if (Math.Abs(en - g) < 0.4) ciftler.Add((g, en));
        }

        return ciftler;
    }

    private static string SesKodegi(string dosya)
    {
        var (_, cikti) = Kos(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=codec_name", "-of", "default=nw=1:nk=1", dosya);
        return cikti.Trim().Split('\n')[0].Trim();
    }

    private static (int Kod, string Metin) Kos(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        surec.WaitForExit();
        var ust = cikti.GetAwaiter().GetResult();
        var alt = hata.GetAwaiter().GetResult();
        return (surec.ExitCode, ust.Length > 0 ? ust + alt : alt);
    }

    private static string Ilk(string metin) => metin.Trim().Split('\n')[0].Trim();

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
