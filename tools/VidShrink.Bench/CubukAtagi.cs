using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;

public static class CubukAtagi
{
    private static readonly int[] Anlar = { 50, 100, 200, 384, 500, 800, 1000, 1500, 2000 };
    private static readonly double[] Esikler = { 1, 5, 8, 10, 12 };

    public static async Task<int> RunAsync(string[] args)
    {
        var tekrar = args.Length > 1 && int.TryParse(args[1], out var t) ? t : 5;
        var gecikmeler = await ManifestGecikmeleriAsync(tekrar);
        var ortanca = gecikmeler.Count > 0 ? gecikmeler.OrderBy(x => x).ElementAt(gecikmeler.Count / 2) : double.NaN;
        var tik = TikAraliklari(250);

        Console.WriteLine(Fmt($"kare-suresi-sabiti-ms {InstallProgress.FrameMilliseconds}"));
        Console.WriteLine(Fmt($"atak-kare {InstallProgress.BurstFrames} atak-ms {InstallProgress.BurstFrames * InstallProgress.FrameMilliseconds}"));
        Console.WriteLine(Fmt($"tik-16ms-gercek ortanca {Yuzdelik(tik, 0.5):F2} p90 {Yuzdelik(tik, 0.9):F2} maks {tik.Max():F2}"));
        Console.WriteLine("manifest-gecikme-ms " + string.Join(" ", gecikmeler.Select(g => g.ToString("F0", CultureInfo.InvariantCulture))) + Fmt($" ortanca {ortanca:F0}"));

        foreach (var manifestMs in new[] { double.IsNaN(ortanca) ? 300 : Math.Round(ortanca), double.PositiveInfinity })
        {
            foreach (var atak in new[] { true, false })
            {
                foreach (var (ad, kareler) in new[] { ("ideal", (IReadOnlyList<double>)Enumerable.Repeat((double)InstallProgress.FrameMilliseconds, 200).ToList()), ("olculen", tik) })
                {
                    var (anlik, esik) = Sur(atak, manifestMs, kareler);
                    var baslik = Fmt($"senaryo manifest={(double.IsInfinity(manifestMs) ? "yok" : manifestMs.ToString("F0", CultureInfo.InvariantCulture))} atak={(atak ? "var" : "yok")} kare={ad}");
                    Console.WriteLine(baslik);
                    Console.WriteLine("  cubuk@ms " + string.Join(" ", Anlar.Select(a => Fmt($"{a}:{anlik[a]:F2}"))));
                    Console.WriteLine("  esige-ms " + string.Join(" ", Esikler.Select(e => Fmt($"{e}%:{(esik[e] is { } v ? v.ToString("F0", CultureInfo.InvariantCulture) : "-")}"))));
                }
            }
        }

        return 0;
    }

    private static (Dictionary<int, double> Anlik, Dictionary<double, double?> Esik) Sur(bool atak, double manifestMs, IReadOnlyList<double> kareler)
    {
        var ilerleme = new InstallProgress();
        if (!atak)
        {
            ilerleme.Step(0, 0, "tuket");
            for (var i = 0; i < InstallProgress.BurstFrames; i++) ilerleme.Advance();
        }

        ilerleme.Step(0, 10, "manifest");
        var anlik = new Dictionary<int, double>();
        var esik = Esikler.ToDictionary(e => e, _ => (double?)null);
        var zaman = 0.0;
        var bulundu = false;
        var sira = 0;
        var sonAn = Anlar.Max();
        var bar = 0.0;
        var anIndeks = 0;

        while (zaman < sonAn)
        {
            var kare = kareler[sira++ % kareler.Count];
            if (!bulundu && zaman + kare >= manifestMs)
            {
                var once = manifestMs - zaman;
                if (once > 0)
                {
                    ilerleme.Advance(TimeSpan.FromMilliseconds(once));
                    bar = ilerleme.Bar;
                }
                ilerleme.Step(12, 20, "bulundu");
                bulundu = true;
                kare -= Math.Max(0, once);
                zaman = manifestMs;
            }

            ilerleme.Advance(TimeSpan.FromMilliseconds(kare));
            bar = ilerleme.Bar;
            zaman += kare;
            while (anIndeks < Anlar.Length && Anlar[anIndeks] <= zaman) anlik[Anlar[anIndeks++]] = bar;
            foreach (var e in Esikler)
                if (esik[e] is null && bar >= e) esik[e] = zaman;
        }

        return (anlik, esik);
    }

    private static async Task<List<double>> ManifestGecikmeleriAsync(int tekrar)
    {
        var sonuc = new List<double>();
        var adres = UpdateCheck.LatestAssetUrl(UpdateCheck.ManifestAssetName(UpdateCheck.Rid));
        for (var i = 0; i < tekrar; i++)
        {
            var saat = Stopwatch.StartNew();
            try
            {
                using var istemci = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                istemci.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Bench");
                await istemci.GetStringAsync(adres);
                sonuc.Add(saat.Elapsed.TotalMilliseconds);
            }
            catch (Exception hata)
            {
                Console.Error.WriteLine("manifest: " + hata.Message);
            }
        }

        return sonuc;
    }

    private static List<double> TikAraliklari(int adet)
    {
        var sonuc = new List<double>(adet);
        var saat = Stopwatch.StartNew();
        var once = saat.Elapsed.TotalMilliseconds;
        for (var i = 0; i < adet; i++)
        {
            Thread.Sleep(InstallProgress.FrameMilliseconds);
            var simdi = saat.Elapsed.TotalMilliseconds;
            sonuc.Add(simdi - once);
            once = simdi;
        }

        return sonuc;
    }

    private static double Yuzdelik(IReadOnlyList<double> degerler, double p)
    {
        var sirali = degerler.OrderBy(x => x).ToArray();
        return sirali[Math.Clamp((int)Math.Ceiling(p * sirali.Length) - 1, 0, sirali.Length - 1)];
    }

    private static string Fmt(FormattableString metin) => metin.ToString(CultureInfo.InvariantCulture);
}
