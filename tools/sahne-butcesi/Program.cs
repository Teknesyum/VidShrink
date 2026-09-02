using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.SahneButcesi;

public sealed record Pencere(string Ad, string Dosya, string Not);

public sealed record SahneKaydi(int Index, double Start, double End, long Bits, double Complexity);

public sealed record HaritaKaydi(string Pencere, double Duration, double Fps, IReadOnlyList<SahneKaydi> Scenes, double TaramaSaniye);

public sealed record PlanKaydi(string Codec, string Mode, int VideoBitrateK, int? Crf, int Width, int Height, double Fps, string Preset, string PixelFormat, double TargetMb);

public sealed record K1Kaydi(
    string Pencere,
    PlanKaydi Plan,
    int ReferansCrf,
    long ReferansToplamBit,
    long PlanToplamBit,
    IReadOnlyList<double> HakEdilen,
    IReadOnlyList<double> Verilen,
    IReadOnlyList<double> Harita,
    IReadOnlyList<string> Bilinmiyor);

public sealed record OlcumKaydi(
    string Pencere,
    string Kol,
    double GerceklesenMb,
    double HedefMb,
    double BandAltMb,
    double BandUstMb,
    bool BandIcinde,
    double? VmafMean,
    double? VmafP10,
    double? VmafMin,
    double? VmafWorstScene,
    string? Bilinmiyor);

public static class Program
{
    public static readonly Pencere[] Pencereler =
    {
        new("p1-karisik", "p1-karisik.mkv", "144,117-333,300 — oyun + menu + diyalog, 28 gercek kesim"),
        new("p2-durgun", "p2-durgun.mkv", "333,300-519,666 — menu / egitim ekrani, 7 gercek kesim"),
        new("p3-hareketli", "p3-hareketli.mkv", "600,000-789,000 — kesintisiz dovus, 0 gercek kesim")
    };

    public const int ReferansCrf = 26;
    public const double HedefMb = 60.0;
    public const int Threads = 8;

    /// Uretimdeki iki yazilim kolu: MaxCompression varsayilani libsvtav1, Compatible libx264.
    public static readonly Dictionary<string, CodecPreference> Kollar = new()
    {
        ["maks"] = CodecPreference.MaxCompression,
        ["uyumlu"] = CodecPreference.Compatible
    };

    private static string Kok = string.Empty;
    private static string Is = string.Empty;
    private static string Kol = "maks";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static async Task<int> Main(string[] args)
    {
        Kok = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        Is = Path.Combine(Kok, ".calisma", "T114");
        Directory.CreateDirectory(Is);

        if (args.Length == 0) { Console.Error.WriteLine("kullanim: <harita|k1|k4|k5|k7> <maks|uyumlu> [pencere]"); return 2; }

        var komut = args[0];
        if (args.Length > 1)
        {
            if (!Kollar.ContainsKey(args[1])) { Console.Error.WriteLine($"kol yok: {args[1]}"); return 2; }
            Kol = args[1];
        }
        if (komut == "rapor")
        {
            Rapor.Uret(Is, Path.Combine(Kok, "docs", "olcumler", "sahne-butcesi.md"), Json);
            return 0;
        }

        var secilen = args.Length > 2
            ? Pencereler.Where(p => p.Ad == args[2]).ToArray()
            : Pencereler;
        if (secilen.Length == 0) { Console.Error.WriteLine($"pencere yok: {args[2]}"); return 2; }

        foreach (var p in secilen)
        {
            switch (komut)
            {
                case "harita": await HaritaAsync(p); break;
                case "k1": await K1Async(p); break;
                case "k5": await K5Async(p); break;
                case "k7": await K7Async(p); break;
                case "k4": K4(p); break;
                default: Console.Error.WriteLine($"bilinmeyen komut: {komut}"); return 2;
            }
        }
        return 0;
    }

    private static string Kaynak(Pencere p) => Path.Combine(Is, "kaynak", p.Dosya);

    private static string Yol(string ad) => Path.Combine(Is, ad);

    private static async Task<HaritaKaydi> HaritaAsync(Pencere p)
    {
        var hedef = Yol($"harita-{p.Ad}.json");
        if (File.Exists(hedef))
        {
            var mevcut = JsonSerializer.Deserialize<HaritaKaydi>(await File.ReadAllTextAsync(hedef), Json)!;
            Console.WriteLine($"{p.Ad}: harita zaten var, {mevcut.Scenes.Count} sahne");
            return mevcut;
        }

        var info = await FfprobeClient.ProbeAsync(Kaynak(p));
        var scan = await SceneDetector.ScanAsync(Kaynak(p));
        if (!scan.Ok) throw new InvalidOperationException($"{p.Ad}: sahne taramasi basarisiz: {scan.Error}");
        var map = SceneMap.BuildDerived(info.DurationSeconds, scan.Candidates, scan.Frames, ThresholdRule.Measured);

        var kayit = new HaritaKaydi(
            p.Ad, info.DurationSeconds, info.Fps,
            map.Scenes.Select(s => new SahneKaydi(s.Index, s.Start, s.End, s.Bits, s.Complexity)).ToArray(),
            scan.Elapsed.TotalSeconds);
        await File.WriteAllTextAsync(hedef, JsonSerializer.Serialize(kayit, Json));
        Console.WriteLine($"{p.Ad}: {kayit.Scenes.Count} sahne, tarama {Kabuk.Inv(kayit.TaramaSaniye, "0.0")} sn");
        return kayit;
    }

    public static SceneMap Map(HaritaKaydi k) => new()
    {
        Threshold = double.NaN,
        Duration = k.Duration,
        Rule = ThresholdRule.Measured,
        Scenes = k.Scenes.Select(s => new Scene
        { Index = s.Index, Start = s.Start, End = s.End, Bits = s.Bits, Complexity = s.Complexity }).ToArray()
    };

    private static async Task<(EncodePlan Plan, MediaInfo Info)> PlanAsync(Pencere p)
    {
        var info = await FfprobeClient.ProbeAsync(Kaynak(p));
        var options = new PlanOptions
        {
            TargetMb = HedefMb,
            Intent = Intent.Sharing,
            Codec = Kollar[Kol],
            HdrPolicy = HdrPolicy.Preserve,
            FillPolicy = FillPolicy.FillTarget,
            SpeedMode = SpeedMode.Quality
        };
        var plan = PlanCalculator.Build(info, options, EncoderCapabilities.Instance);
        return (plan, info);
    }

    /// Is parcacigini sabitleyen kodlayici parametresi. Kodlayici basina farkli;
    /// yanlis bayrak ffmpeg tarafindan sessizce atilir ve pinleme kaybolur.
    public static string[] IsParcacigiArgs(string codec) => codec.ToLowerInvariant() switch
    {
        var c when c.Contains("x265", StringComparison.Ordinal)
            => new[] { "-x265-params", $"pools={Threads}:log-level=error" },
        var c when c.Contains("x264", StringComparison.Ordinal)
            => new[] { "-x264-params", $"threads={Threads}" },
        var c when c.Contains("svtav1", StringComparison.Ordinal)
            => new[] { "-svtav1-params", $"lp={Threads}" },
        _ => Array.Empty<string>()
    };

    public static string? ZonesFlag(string codec) => codec.ToLowerInvariant() switch
    {
        var c when c.Contains("x265", StringComparison.Ordinal) => "-x265-params",
        var c when c.Contains("x264", StringComparison.Ordinal) => "-x264-params",
        _ => null
    };

    private static async Task K1Async(Pencere p)
    {
        var hedef = Yol($"k1-{Kol}-{p.Ad}.json");
        if (File.Exists(hedef)) { Console.WriteLine($"{p.Ad}: k1 zaten var"); return; }

        var harita = await HaritaAsync(p);
        var map = Map(harita);
        var (plan, info) = await PlanAsync(p);
        Console.WriteLine($"{p.Ad}: plan {plan.Codec} {plan.Mode} {plan.VideoBitrateK}k {plan.Width}x{plan.Height}@{Kabuk.Inv(plan.Fps, "0.##")} preset={plan.Preset}");

        var bilinmiyor = new List<string>();

        var refBits = new long[map.Scenes.Count];
        var refDir = Path.Combine(Is, "referans", $"{Kol}-{p.Ad}");
        Directory.CreateDirectory(refDir);
        for (var i = 0; i < map.Scenes.Count; i++)
        {
            var s = map.Scenes[i];
            var cikti = Path.Combine(refDir, $"sahne-{i:D3}.mkv");
            if (!File.Exists(cikti))
            {
                var argv = new List<string>
                {
                    "-hide_banner", "-v", "error", "-y",
                    "-ss", Kabuk.Inv(s.Start, "0.######"), "-t", Kabuk.Inv(s.Duration, "0.######"),
                    "-i", Kaynak(p), "-an",
                    "-c:v", plan.Codec, "-preset", plan.Preset,
                    "-crf", ReferansCrf.ToString(CultureInfo.InvariantCulture),
                    "-pix_fmt", plan.PixelFormat
                };
                argv.AddRange(IsParcacigiArgs(plan.Codec));
                argv.AddRange(new[] { "-threads", Threads.ToString(CultureInfo.InvariantCulture), cikti });
                var r = Kabuk.Kos(ToolLocator.Ffmpeg, argv);
                if (r.TimedOut || r.Code != 0)
                {
                    bilinmiyor.Add($"referans sahne {i}: {(r.TimedOut ? "zaman asimi" : r.StdErr)}");
                    continue;
                }
            }
            refBits[i] = new FileInfo(cikti).Length * 8;
        }

        var planCikti = Path.Combine(Is, $"plan-{Kol}-{p.Ad}.mkv");
        if (!File.Exists(planCikti)) Kodla(info, plan, map, planCikti, null);
        if (!File.Exists(planCikti)) { bilinmiyor.Add("plan kodlamasi cikti uretmedi"); }

        var verilenBits = File.Exists(planCikti) ? SahneBitleri(planCikti, map) : new double[map.Scenes.Count];

        var deserved = Normalize(refBits.Select(b => (double)b).ToArray());
        var given = Normalize(verilenBits);
        var mapShare = Butce.HaritaPaylari(map);

        var kayit = new K1Kaydi(
            p.Ad,
            new PlanKaydi(plan.Codec, plan.Mode, plan.VideoBitrateK, plan.Crf, plan.Width, plan.Height, plan.Fps, plan.Preset, plan.PixelFormat, HedefMb),
            ReferansCrf,
            refBits.Sum(),
            (long)verilenBits.Sum(),
            deserved, given, mapShare, bilinmiyor);
        await File.WriteAllTextAsync(hedef, JsonSerializer.Serialize(kayit, Json));
        await File.WriteAllTextAsync(Yol($"k1-{Kol}-{p.Ad}.csv"), Butce.Csv(map, deserved, given, mapShare));

        Console.WriteLine($"{Kol}/{p.Ad}: rho(verilen,hak) = {Kabuk.Inv(SceneMap.Spearman(given, deserved), "0.000")}  " +
                          $"rho(harita,hak) = {Kabuk.Inv(SceneMap.Spearman(mapShare, deserved), "0.000")}  " +
                          $"MAE verilen = {Kabuk.Inv(Butce.MeanAbsoluteError(given, deserved) * 100, "0.000")} pp  " +
                          $"MAE harita = {Kabuk.Inv(Butce.MeanAbsoluteError(mapShare, deserved) * 100, "0.000")} pp  " +
                          $"ters dusen = {Butce.TersDusenler(deserved, given, mapShare)}/{deserved.Length}");
        foreach (var b in bilinmiyor) Console.WriteLine($"  bilinmiyor: {b}");
    }

    private static double[] Normalize(double[] values)
    {
        var total = values.Sum();
        return total > 0 ? values.Select(v => v / total).ToArray() : values;
    }

    private static void Kodla(MediaInfo info, EncodePlan plan, SceneMap? map, string cikti, string? zones)
    {
        var used = plan.Clone();
        if (zones is not null)
        {
            var flag = ZonesFlag(used.Codec)
                ?? throw new InvalidOperationException($"{used.Codec} zones desteklemiyor.");
            used.ExtraArgs.AddRange(new[] { flag, $"zones={zones}" });
        }
        used.ExtraArgs.AddRange(IsParcacigiArgs(used.Codec));
        used.ExtraArgs.AddRange(new[] { "-threads", Threads.ToString(CultureInfo.InvariantCulture) });

        var log = Path.Combine(Is, "gecis", Path.GetFileNameWithoutExtension(cikti));
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);

        if (FfmpegArguments.NeedsTwoPasses(used.Codec) && used.ModeEnum == EncodeMode.TwoPass)
        {
            var p1 = FfmpegArguments.Build(info, used, cikti, 1, log, EncoderCapabilities.Instance, map);
            var r1 = Kabuk.Kos(ToolLocator.Ffmpeg, p1);
            if (r1.Code != 0) { Console.Error.WriteLine($"gecis1 hata: {r1.StdErr}"); return; }
            var p2 = FfmpegArguments.Build(info, used, cikti, 2, log, EncoderCapabilities.Instance, map);
            var r2 = Kabuk.Kos(ToolLocator.Ffmpeg, p2);
            if (r2.Code != 0) { Console.Error.WriteLine($"gecis2 hata: {r2.StdErr}"); return; }
        }
        else
        {
            var a = FfmpegArguments.Build(info, used, cikti, 0, null, EncoderCapabilities.Instance, map);
            var r = Kabuk.Kos(ToolLocator.Ffmpeg, a);
            if (r.Code != 0) { Console.Error.WriteLine($"kodlama hata: {r.StdErr}"); return; }
        }
    }

    public static double[] SahneBitleri(string dosya, SceneMap map)
    {
        var (code, text) = Kabuk.Yakala(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "packet=pts_time,size",
            "-of", "csv=p=0", dosya
        });
        if (code != 0) throw new InvalidOperationException($"ffprobe paket okumasi basarisiz: {dosya}");

        var bits = new double[map.Scenes.Count];
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            var parts = t.Split(',');
            if (parts.Length < 2) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pts)) continue;
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;
            var idx = Index(map, pts);
            if (idx >= 0) bits[idx] += size * 8;
        }
        return bits;
    }

    private static int Index(SceneMap map, double time)
    {
        for (var i = 0; i < map.Scenes.Count; i++)
            if (time >= map.Scenes[i].Start && time < map.Scenes[i].End) return i;
        return time >= map.Duration ? map.Scenes.Count - 1 : -1;
    }

    /// Aday x kodlayici izgarasi. Cikis kodunun sifir olmasi destek demek degil:
    /// SVT-AV1 ve x264/x265 parametre ayristiricilari tanimadiklari anahtari
    /// uyariyla gecer. Bu yuzden her hucre **iki farkli degerle** kodlanir ve
    /// ciktilarin baytlari karsilastirilir. Ayni cikti = parametre yok sayildi.
    /// Hucre: kodlayicinin params bayragina eklenecek anahtar=deger cifti. nvenc'te
    /// ayri bayraklar kullanilir; orada <c>Ham</c> dolu olur.
    public sealed record K4Hucre(string Codec, string Aday, string A, string B, string[]? HamA = null, string[]? HamB = null);

    public static readonly K4Hucre[] K4Izgarasi =
    {
        new("libx265", "zones", "zones=0,119,b=2.00", "zones=0,119,b=0.50"),
        new("libx265", "qcomp", "qcomp=0.50", "qcomp=0.95"),
        new("libx264", "zones", "zones=0,119,b=2.00", "zones=0,119,b=0.50"),
        new("libx264", "qcomp", "qcomp=0.50", "qcomp=0.95"),
        new("libsvtav1", "zones", "zones=0,119,b=2.00", "zones=0,119,b=0.50"),
        new("libsvtav1", "qcomp", "qp-scale-compress-strength=0", "qp-scale-compress-strength=3"),
        new("libsvtav1", "aq", "variance-boost-strength=1", "variance-boost-strength=4"),
        new("hevc_nvenc", "zones", "", "", Array.Empty<string>(), null),
        new("hevc_nvenc", "aq", "", "", new[] { "-spatial-aq", "0" }, new[] { "-spatial-aq", "1", "-aq-strength", "15" }),
        new("av1_nvenc", "zones", "", "", Array.Empty<string>(), null),
        new("av1_nvenc", "aq", "", "", new[] { "-spatial-aq", "0" }, new[] { "-spatial-aq", "1", "-aq-strength", "15" })
    };

    private static void K4(Pencere p)
    {
        var hedef = Yol("k4-izgara.csv");
        if (File.Exists(hedef)) { Console.WriteLine("k4 zaten var"); return; }

        var deneme = Path.Combine(Is, "k4");
        Directory.CreateDirectory(deneme);
        var satirlar = new List<string> { "kodlayici;aday;destek;a_bayt;b_bayt;fark_bayt;gurultu_bayt;not" };

        var gurultu = new Dictionary<string, long?>();
        foreach (var codec in K4Izgarasi.Select(h => h.Codec).Distinct())
        {
            if (!EncoderCapabilities.Instance.HasEncoder(codec)) { gurultu[codec] = null; continue; }
            var (ok1, l1, _) = K4Kodla(p, codec, null, null, Path.Combine(deneme, $"{codec}-kontrol-1.mkv"));
            var (ok2, l2, _) = K4Kodla(p, codec, null, null, Path.Combine(deneme, $"{codec}-kontrol-2.mkv"));
            gurultu[codec] = ok1 && ok2 ? Math.Abs(l1 - l2) : null;
            satirlar.Add($"{codec};kontrol;-;{l1};{l2};{(ok1 && ok2 ? Math.Abs(l1 - l2).ToString(CultureInfo.InvariantCulture) : string.Empty)};;" +
                         "ayni parametreyle iki kosum — tekrar gurultusu");
        }

        foreach (var h in K4Izgarasi)
        {
            if (!EncoderCapabilities.Instance.HasEncoder(h.Codec))
            { satirlar.Add($"{h.Codec};{h.Aday};bilinmiyor;;;;;kodlayici bu makinede yok"); continue; }
            if (h.HamA is { Length: 0 })
            { satirlar.Add($"{h.Codec};{h.Aday};hayir;;;;;kodlayicida zone parametresi yok"); continue; }
            if (gurultu[h.Codec] is not { } noise)
            { satirlar.Add($"{h.Codec};{h.Aday};bilinmiyor;;;;;kontrol kosumu cikti uretmedi"); continue; }

            var (okA, lenA, errA) = K4Kodla(p, h.Codec, h.A, h.HamA, Path.Combine(deneme, $"{h.Codec}-{h.Aday}-a.mkv"));
            var (okB, lenB, errB) = K4Kodla(p, h.Codec, h.B, h.HamB, Path.Combine(deneme, $"{h.Codec}-{h.Aday}-b.mkv"));
            if (!okA || !okB)
            {
                satirlar.Add($"{h.Codec};{h.Aday};bilinmiyor;{lenA};{lenB};;{noise};" +
                             (errA + " " + errB).Replace(';', ' ').Replace('\n', ' ').Trim());
                continue;
            }
            var fark = Math.Abs(lenA - lenB);
            var etkili = fark > noise * 2 && fark > lenA / 100;
            satirlar.Add($"{h.Codec};{h.Aday};{(etkili ? "evet" : "hayir")};{lenA};{lenB};{fark};{noise};" +
                         (etkili
                             ? "iki deger belirgin farkli cikti uretti"
                             : "fark tekrar gurultusunun icinde — parametre etkisiz ya da yok sayildi"));
        }
        File.WriteAllLines(hedef, satirlar);
        foreach (var s in satirlar) Console.WriteLine(s);
    }

    private static (bool Ok, long Length, string Err) K4Kodla(Pencere p, string codec, string? param, string[]? ham, string cikti)
    {
        var argv = new List<string>
        {
            "-hide_banner", "-v", "error", "-y",
            "-ss", "0", "-t", "2", "-i", Kaynak(p), "-an",
            "-c:v", codec, "-b:v", "2000k", "-pix_fmt", "yuv420p"
        };
        var pin = IsParcacigiArgs(codec);
        if (pin.Length == 2)
            argv.AddRange(new[] { pin[0], string.IsNullOrEmpty(param) ? pin[1] : pin[1] + ":" + param });
        else if (!string.IsNullOrEmpty(param))
            throw new InvalidOperationException($"{codec} icin params bayragi yok.");
        if (ham is { Length: > 0 }) argv.AddRange(ham);
        argv.AddRange(new[] { "-threads", Threads.ToString(CultureInfo.InvariantCulture), cikti });

        var r = Kabuk.Kos(ToolLocator.Ffmpeg, argv, TimeSpan.FromMinutes(15));
        if (r.TimedOut) return (false, 0, "zaman asimi");
        if (r.Code != 0) return (false, 0, r.StdErr);
        return (true, new FileInfo(cikti).Length, string.Empty);
    }

    private static async Task K5Async(Pencere p) => await AbAsync(p, "k5", new[] { "taban", "dagitim" });

    private static async Task K7Async(Pencere p) => await AbAsync(p, "k7", new[] { "eksik-kesim", "fazla-kesim" });

    private static async Task AbAsync(Pencere p, string asama, string[] kollar)
    {
        var hedef = Yol($"{asama}-{Kol}-{p.Ad}.json");
        if (File.Exists(hedef)) { Console.WriteLine($"{p.Ad}: {asama} zaten var"); return; }

        var harita = await HaritaAsync(p);
        var map = Map(harita);
        var (plan, info) = await PlanAsync(p);
        if (ZonesFlag(plan.Codec) is null)
        {
            var not = $"{plan.Codec} zones desteklemiyor; {asama} bu kolda kosulamaz.";
            await File.WriteAllTextAsync(hedef, JsonSerializer.Serialize(new[]
            {
                new OlcumKaydi(p.Ad, "yok", 0, HedefMb, 0, 0, false, null, null, null, null, not)
            }, Json));
            Console.WriteLine($"{Kol}/{p.Ad}: BILINMIYOR — {not}");
            return;
        }
        var band = FillBand.For(HedefMb);
        var gamma = Butce.Gamma(Butce.DefaultQcomp);
        var sonuclar = new List<OlcumKaydi>();

        foreach (var kol in kollar)
        {
            var cikti = Path.Combine(Is, $"{asama}-{Kol}-{p.Ad}-{kol}.mkv");
            string? zones = null;
            if (kol != "taban")
            {
                var kullanilan = kol switch
                {
                    "dagitim" => map,
                    "eksik-kesim" => Butce.KesimDusur(map, 2),
                    "fazla-kesim" => Butce.KesimEkle(map),
                    _ => throw new InvalidOperationException(kol)
                };
                zones = Butce.ZonesArg(kullanilan, Butce.ZoneCarpanlari(kullanilan, gamma), harita.Fps);
                await File.WriteAllTextAsync(Yol($"{asama}-{Kol}-{p.Ad}-{kol}.zones.txt"),
                    $"gamma={Kabuk.Inv(gamma)}\nsahne={kullanilan.Scenes.Count}\nzones={zones}\n");
            }
            if (!File.Exists(cikti)) Kodla(info, plan, map, cikti, zones);
            if (!File.Exists(cikti))
            {
                sonuclar.Add(new OlcumKaydi(p.Ad, kol, 0, HedefMb, band.LowerMb, band.UpperMb, false, null, null, null, null, "kodlama cikti uretmedi"));
                continue;
            }

            var mb = new FileInfo(cikti).Length / 1024.0 / 1024.0;
            QualityScore? skor = null;
            string? bilinmiyor = null;
            try { skor = await QualityMeter.MeasureAsync(Kaynak(p), cikti); }
            catch (QualityMeasurementFailedException ex) { bilinmiyor = ex.Message; }

            sonuclar.Add(new OlcumKaydi(
                p.Ad, kol, mb, HedefMb, band.LowerMb, band.UpperMb,
                mb >= band.LowerMb && mb <= band.UpperMb,
                skor?.VmafNegMean, skor?.VmafNegP10, skor?.VmafNegMin, skor?.VmafNegWorstScene,
                bilinmiyor));
            Console.WriteLine($"{p.Ad}/{kol}: {Kabuk.Inv(mb, "0.00")} MB band[{Kabuk.Inv(band.LowerMb, "0.0")}-{Kabuk.Inv(band.UpperMb, "0.0")}] " +
                              $"vmaf={Kabuk.Inv(skor?.VmafNegMean ?? double.NaN, "0.000")} p10={Kabuk.Inv(skor?.VmafNegP10 ?? double.NaN, "0.000")} " +
                              $"min={Kabuk.Inv(skor?.VmafNegMin ?? double.NaN, "0.000")} enkotu={Kabuk.Inv(skor?.VmafNegWorstScene ?? double.NaN, "0.000")}" +
                              (bilinmiyor is null ? string.Empty : $" BILINMIYOR: {bilinmiyor}"));
        }

        await File.WriteAllTextAsync(hedef, JsonSerializer.Serialize(sonuclar, Json));
    }
}
