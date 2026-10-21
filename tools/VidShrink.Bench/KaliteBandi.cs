using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

public static class KaliteBandi
{
    private static readonly (string Ad, string Kaynak)[] Klipler =
    {
        ("testsrc2", "testsrc2=s=640x360:r=24:d=4"),
        ("mandelbrot", "mandelbrot=s=640x360:r=24,trim=duration=4"),
        ("gurultu", "testsrc2=s=640x360:r=24:d=4,noise=alls=24:allf=t+u")
    };

    private static readonly int[] Crfler = { 14, 18, 23, 28, 33, 38, 43 };

    private static readonly OutputQualityMetric[] Olcutler = { OutputQualityMetric.Vmaf, OutputQualityMetric.Ssim, OutputQualityMetric.Psnr };

    public static async Task<int> RunAsync(string[] args)
    {
        var klasor = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(".calisma", "kalite-bandi"));
        Directory.CreateDirectory(klasor);
        Console.WriteLine(Fmt($"ffmpeg {ToolLocator.FfmpegVersion}"));
        var eksik = new[] { "zscale", "libvmaf", "ssim", "psnr" }.Where(f => !EncoderCapabilities.Instance.HasFilter(f)).ToArray();
        if (eksik.Length > 0)
        {
            Console.WriteLine("eksik filtre: " + string.Join(",", eksik));
            return 1;
        }

        Console.WriteLine("klip\tcrf\tkbps\tvmaf\tssim\tpsnr\tbant-vmaf\tbant-ssim\tbant-psnr");
        var satirlar = new List<(OutputQualityBand Vmaf, OutputQualityBand Ssim, OutputQualityBand Psnr)>();
        foreach (var (ad, kaynakFiltre) in Klipler)
        {
            var kaynak = Path.Combine(klasor, ad + ".mkv");
            Uret(kaynak, "-f", "lavfi", "-i", kaynakFiltre, "-c:v", "libx264", "-qp", "0", "-preset", "veryfast", "-pix_fmt", "yuv420p");
            foreach (var crf in Crfler)
            {
                var cikti = Path.Combine(klasor, Fmt($"{ad}-crf{crf}.mp4"));
                Uret(cikti, "-i", kaynak, "-c:v", "libx264", "-crf", crf.ToString(CultureInfo.InvariantCulture), "-preset", "medium", "-pix_fmt", "yuv420p");
                var bilgi = await FfprobeClient.ProbeAsync(cikti);
                var kbps = new FileInfo(cikti).Length * 8 / 1000.0 / bilgi.DurationSeconds;
                var puan = new Dictionary<OutputQualityMetric, OutputQuality>();
                foreach (var olcut in Olcutler)
                    puan[olcut] = await OutputQualityCheck.MeasureAsync(kaynak, cikti, olcut)
                        ?? throw new InvalidOperationException(Fmt($"{ad} crf{crf} {olcut} olculemedi"));
                var v = puan[OutputQualityMetric.Vmaf];
                var s = puan[OutputQualityMetric.Ssim];
                var p = puan[OutputQualityMetric.Psnr];
                satirlar.Add((v.Band, s.Band, p.Band));
                Console.WriteLine(Fmt($"{ad}\t{crf}\t{kbps:0}\t{v.Score:0.00}\t{s.Score:0.0000}\t{p.Score:0.00}\t{v.Band}\t{s.Band}\t{p.Band}"));
            }
        }

        Console.WriteLine(Fmt($"ssim-vmaf-ayni-bant={satirlar.Count(r => r.Ssim == r.Vmaf)}/{satirlar.Count} bir-basamak-icinde={satirlar.Count(r => Math.Abs(r.Ssim - r.Vmaf) <= 1)}/{satirlar.Count}"));
        Console.WriteLine(Fmt($"psnr-vmaf-ayni-bant={satirlar.Count(r => r.Psnr == r.Vmaf)}/{satirlar.Count} bir-basamak-icinde={satirlar.Count(r => Math.Abs(r.Psnr - r.Vmaf) <= 1)}/{satirlar.Count}"));
        return 0;
    }

    private static void Uret(string cikti, params string[] girdi)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-y", "-v", "error" }.Concat(girdi).Concat(new[] { "-an", "-threads", "2", cikti }))
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var hataGorevi = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(cikti) + ": " + hataGorevi.Result);
    }

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
