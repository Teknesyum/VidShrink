using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.SurucuYoklugu;

public static class Program
{
    private static readonly string[] FastHardwareOrder =
    {
        "av1_nvenc", "hevc_nvenc", "av1_qsv", "hevc_qsv", "av1_amf", "hevc_amf", "h264_nvenc"
    };

    private static readonly string[] HardwareCandidates =
    {
        "h264_nvenc", "h264_qsv", "h264_amf",
        "hevc_nvenc", "hevc_qsv", "hevc_amf",
        "av1_nvenc"
    };

    private sealed class ProbingAdapter : IEncoderAvailability
    {
        private readonly IEncoderAvailability _inner;
        public ProbingAdapter(IEncoderAvailability inner) => _inner = inner;
        public bool HasEncoder(string name) => CodecModel.IsHardware(name) ? _inner.WorksAsEncoder(name) : _inner.HasEncoder(name);
        public bool WorksAsEncoder(string codec) => _inner.WorksAsEncoder(codec);
    }

    private sealed class SahteMakine : IEncoderAvailability
    {
        private readonly HashSet<string> _built;
        private readonly HashSet<string> _works;

        public SahteMakine(string[] built, string[] works)
        {
            _built = new HashSet<string>(built, StringComparer.OrdinalIgnoreCase);
            _works = new HashSet<string>(works, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasEncoder(string name) => _built.Contains(name);
        public bool WorksAsEncoder(string codec) => _works.Contains(codec);
    }

    public static int Main(string[] args)
    {
        var bolum = args.Length > 0 ? args[0] : "hepsi";
        var tekrar = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20;

        if (bolum is "hepsi" or "ayrisma") Ayrisma();
        if (bolum is "hepsi" or "maliyet") Maliyet(tekrar);
        if (bolum is "hepsi" or "onbellek") Onbellek();
        if (bolum is "hepsi" or "geridusme") GeriDusme();
        if (bolum is "hepsi" or "yuk") YukDuyarliligi(bolum == "yuk" ? tekrar : 5);
        return 0;
    }

    private static void Ayrisma()
    {
        Baslik("1. Derleme listesi ile gercek yoklama ayrisiyor mu");
        var caps = TazeYetenek();
        Console.WriteLine($"ffmpeg {caps.Version}");
        Console.WriteLine("kodlayici      HasEncoder  WorksAsEncoder  yoklama_ms");
        foreach (var codec in HardwareCandidates)
        {
            var has = caps.HasEncoder(codec);
            var probe = caps.Probe(codec);
            Console.WriteLine($"{codec,-14} {has,-11} {probe.Succeeded,-15} {probe.ElapsedMs}");
        }
    }

    private static void Maliyet(int tekrar)
    {
        Baslik("2. Yoklamanin plan hesabina maliyeti");

        var info = OrnekKaynak();
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality };

        var yoklamasizTaze = TazeYetenek();
        var yoklamasizSoguk = Sure(() => PlanCalculator.BuildDetailed(info, options, null, yoklamasizTaze));
        var yoklamasizSicak = SicakOrtalama(() => PlanCalculator.BuildDetailed(info, options, null, yoklamasizTaze), tekrar);

        var yoklamaliTaze = new ProbingAdapter(TazeYetenek());
        var yoklamaliSoguk = Sure(() => PlanCalculator.BuildDetailed(info, options, null, yoklamaliTaze));
        var yoklamaliSicak = SicakOrtalama(() => PlanCalculator.BuildDetailed(info, options, null, yoklamaliTaze), tekrar);

        Console.WriteLine($"tekrar (sicak): {tekrar}");
        Console.WriteLine("yol                       soguk_ms  sicak_ort_ms");
        Console.WriteLine($"HasEncoder (bugunku)      {yoklamasizSoguk,8:0.00}  {yoklamasizSicak,12:0.0000}");
        Console.WriteLine($"WorksAsEncoder (onerilen) {yoklamaliSoguk,8:0.00}  {yoklamaliSicak,12:0.0000}");
        Console.WriteLine($"secilen kodlayici: HasEncoder={PlanCalculator.Build(info, options, yoklamasizTaze).Codec}  WorksAsEncoder={PlanCalculator.Build(info, options, yoklamaliTaze).Codec}");

        var fastOptions = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, SpeedMode = SpeedMode.Fast };
        var fastTaze = TazeYetenek();
        var fastSoguk = Sure(() => PlanCalculator.BuildDetailed(info, fastOptions, null, fastTaze));
        var fastSicak = SicakOrtalama(() => PlanCalculator.BuildDetailed(info, fastOptions, null, fastTaze), tekrar);
        Console.WriteLine($"PickFastCodec (zaten yoklamali, {FastHardwareOrder.Length} aday) {fastSoguk,8:0.00}  {fastSicak,12:0.0000}");

        Console.WriteLine();
        Console.WriteLine("PerformanceProbe donanim adayi secimi:");
        var probeTaze = TazeYetenek();
        var probeHas = Sure(() => _ = HardwareCandidates.FirstOrDefault(probeTaze.HasEncoder));
        var probeTaze2 = TazeYetenek();
        var probeWorks = Sure(() => _ = SecimYurutusu(probeTaze2));
        var probeWorksSicak = SicakOrtalama(() => _ = SecimYurutusu(probeTaze2), tekrar);
        Console.WriteLine($"bugunku (listedeki ilk)   soguk {probeHas,10:0.00} ms  -> {HardwareCandidates.FirstOrDefault(probeTaze.HasEncoder) ?? "yok"}");
        Console.WriteLine($"onerilen (calisan ilk)    soguk {probeWorks,10:0.00} ms  sicak_ort {probeWorksSicak:0.0000} ms  -> {SecimYurutusu(probeTaze2) ?? "yok"}");
    }

    private static string? SecimYurutusu(IEncoderAvailability availability)
    {
        var built = HardwareCandidates.Where(availability.HasEncoder).ToArray();
        if (built.Length == 0) return null;
        foreach (var candidate in built)
            if (availability.WorksAsEncoder(candidate)) return candidate;
        return built[0];
    }

    private static void YukDuyarliligi(int tekrar)
    {
        Baslik("5. Yoklama makine yukune duyarli mi");
        Console.WriteLine("Ayni kodlayici, her seferinde taze onbellek. EncoderCapabilities.RunProbe");
        Console.WriteLine("sureci 4000 ms sonra olduruyor; asan yoklama 'calismiyor' diye okunuyor.");
        Console.WriteLine("kodlayici      gecen/deneme  sureler_ms");
        foreach (var codec in new[] { "h264_nvenc", "av1_nvenc" })
        {
            var gecen = 0;
            var sureler = new List<long>();
            for (var i = 0; i < tekrar; i++)
            {
                var probe = TazeYetenek().Probe(codec);
                if (probe.Succeeded) gecen++;
                sureler.Add(probe.ElapsedMs);
            }
            Console.WriteLine($"{codec,-14} {gecen}/{tekrar}          {string.Join(" ", sureler)}");
        }
    }

    private static void Onbellek()
    {
        Baslik("3. Basarisiz yoklama da onbellege giriyor mu");
        var caps = TazeYetenek();

        var dusen = HardwareCandidates.FirstOrDefault(c => caps.HasEncoder(c) && !TazeYetenek().Probe(c).Succeeded);
        if (dusen is null)
        {
            Console.WriteLine("olculmedi: bu makinede derleme listesinde olup yoklamasi dusen kodlayici yok");
            return;
        }

        var taze = TazeYetenek();
        var ilk = Sure(() => _ = taze.WorksAsEncoder(dusen));
        var ikinci = Sure(() => _ = taze.WorksAsEncoder(dusen));
        var ucuncu = Sure(() => _ = taze.WorksAsEncoder(dusen));
        Console.WriteLine($"dusen kodlayici: {dusen}");
        Console.WriteLine($"ayni ornek uzerinde  1. cagri {ilk:0.00} ms   2. cagri {ikinci:0.00} ms   3. cagri {ucuncu:0.00} ms");

        var yeniOrnek = TazeYetenek();
        var yeniIlk = Sure(() => _ = yeniOrnek.WorksAsEncoder(dusen));
        Console.WriteLine($"yeni ornek           1. cagri {yeniIlk:0.00} ms");

        var gecen = HardwareCandidates.FirstOrDefault(c => TazeYetenek().Probe(c).Succeeded);
        if (gecen is not null)
        {
            var g = TazeYetenek();
            var gIlk = Sure(() => _ = g.WorksAsEncoder(gecen));
            var gIkinci = Sure(() => _ = g.WorksAsEncoder(gecen));
            Console.WriteLine($"gecen kodlayici: {gecen}   1. cagri {gIlk:0.00} ms   2. cagri {gIkinci:0.00} ms");
        }
        else
        {
            Console.WriteLine("gecen kodlayici: olculmedi, bu makinede yoklamayi gecen donanim kodlayicisi yok");
        }
    }

    private static void GeriDusme()
    {
        Baslik("4. Surucusuz makinede kullaniciya ne gorunuyor");
        var info = OrnekKaynak();
        var surucusuz = new SahteMakine(
            built: new[] { "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc" },
            works: new[] { "libx264", "libx265", "libsvtav1" });

        foreach (var (ad, availability) in new (string, IEncoderAvailability)[]
                 {
                     ("bugunku (HasEncoder)", surucusuz),
                     ("onerilen (WorksAsEncoder)", new ProbingAdapter(surucusuz))
                 })
        {
            foreach (var (kip, options) in new (string, PlanOptions)[]
                     {
                         ("Codec=Fast, SpeedMode=Quality", new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Quality }),
                         ("Codec=Auto, SpeedMode=Fast", new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, SpeedMode = SpeedMode.Fast })
                     })
            {
                var result = PlanCalculator.BuildDetailed(info, options, null, availability);
                Console.WriteLine($"{ad} | {kip}");
                Console.WriteLine($"  kodlayici : {result.Plan.Codec}");
                Console.WriteLine($"  notlar    : {(result.Advice.Notes.Count == 0 ? "yok" : string.Join(", ", result.Advice.Notes))}");
                Console.WriteLine($"  gerekce   : {(result.Plan.Reason.Length == 0 ? "yok" : result.Plan.Reason)}");
            }
        }
    }

    private static MediaInfo OrnekKaynak() => new()
    {
        FilePath = "ornek.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static EncoderCapabilities TazeYetenek()
        => EncoderCapabilities.Parse(
            Yakala(new[] { "-hide_banner", "-encoders" }),
            Yakala(new[] { "-hide_banner", "-filters" }),
            Yakala(new[] { "-version" }));

    private static string Yakala(string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }

    private static double Sure(Action action)
    {
        var clock = Stopwatch.StartNew();
        action();
        clock.Stop();
        return clock.Elapsed.TotalMilliseconds;
    }

    private static double SicakOrtalama(Action action, int tekrar)
    {
        action();
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < tekrar; i++) action();
        clock.Stop();
        return clock.Elapsed.TotalMilliseconds / tekrar;
    }

    private static void Baslik(string text)
    {
        Console.WriteLine();
        Console.WriteLine(text);
        Console.WriteLine(new string('-', text.Length));
    }
}
