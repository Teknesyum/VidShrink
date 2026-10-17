using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

/// <summary>
/// Donustur renk kolu (DON-26). BT.601 sinirli aralikli bir kaynak, her kolda Donustur plani ile kodlanir; kaynak ve
/// cikti ayni karesi iki bicimde okunur. RGB'de her dosya kendi etiketiyle zscale'den ortak BT.709 RGB'ye cozulur:
/// fark kucuk kalmali, renk korunmus. Ham YUV'de fark buyumeli: duzlemler gercekten yeni matrise ve araliga
/// cevrilmis, yalniz etiket degismemis. "yalniz-etiket" kolu matrisi cevirmeden etiketi degistirir: RGB farki buyur.
/// </summary>
public static class RenkKaresi
{
    private const int En = 320;
    private const int Boy = 240;

    private static readonly (string Ad, ColorMatrixTarget Matris, ColorRangeTarget Aralik)[] Kollar =
    {
        ("koru", ColorMatrixTarget.Keep, ColorRangeTarget.Keep),
        ("bt709", ColorMatrixTarget.Bt709, ColorRangeTarget.Keep),
        ("bt2020-tam", ColorMatrixTarget.Bt2020, ColorRangeTarget.Full),
        ("bt601-tam", ColorMatrixTarget.Bt601, ColorRangeTarget.Full)
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var klasor = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(".calisma", "renk-karesi"));
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var (uretKod, uretHata) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-f", "lavfi", "-i", Fmt($"testsrc2=s={En}x{Boy}:r=24:d=2"), "-c:v", "libx264", "-crf", "12", "-preset", "veryfast",
            "-pix_fmt", "yuv420p", "-colorspace", "smpte170m", "-color_primaries", "smpte170m", "-color_trc", "smpte170m",
            "-color_range", "tv", "-threads", "2", kaynak);
        if (uretKod != 0)
        {
            Console.WriteLine("kaynak uretilemedi: " + uretHata);
            return 1;
        }

        Console.WriteLine(Fmt($"ffmpeg {ToolLocator.FfmpegVersion}"));
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var kaynakRgb = Kare(kaynak, OrtakRgb(info));
        var kaynakYuv = Kare(kaynak, "yuv420p");
        foreach (var (ad, matris, aralik) in Kollar)
        {
            var cikti = Path.Combine(klasor, ad + ".mp4");
            var plan = new ConversionPlan
            {
                Container = "mp4",
                AudioCodec = null,
                EncoderPreset = "veryfast",
                Crf = 12,
                ColorRange = aralik,
                Filters = ConversionPlan.DefaultFilters with { ColorMatrix = matris },
                ExtraArgs = new[] { "-threads", "2" }
            };
            await new EncodeRunner().ConvertAsync(info, plan, cikti, null);
            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            var rgb = Olc(kaynakRgb, Kare(cikti, OrtakRgb(sonuc)));
            var yuv = Olc(kaynakYuv, Kare(cikti, "yuv420p"));
            Console.WriteLine(Fmt($"kol={ad} etiket={sonuc.ColorSpace}/{sonuc.ColorPrimaries}/{sonuc.ColorRange} rgb-ort-fark={rgb.Ort:0.00} rgb-psnr={rgb.Psnr:0.00} yuv-ort-fark={yuv.Ort:0.00} yuv-psnr={yuv.Psnr:0.00}"));
        }
        var etiket = Path.Combine(klasor, "yalniz-etiket.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-v", "error", "-threads", "2", "-i", kaynak, "-c", "copy",
            "-bsf:v", "h264_metadata=matrix_coefficients=1:colour_primaries=1:transfer_characteristics=1", etiket);
        var yanlis = await FfprobeClient.ProbeAsync(etiket);
        var e1 = Olc(kaynakRgb, Kare(etiket, OrtakRgb(yanlis)));
        var e2 = Olc(kaynakYuv, Kare(etiket, "yuv420p"));
        Console.WriteLine(Fmt($"kol=yalniz-etiket etiket={yanlis.ColorSpace}/{yanlis.ColorPrimaries}/{yanlis.ColorRange} rgb-ort-fark={e1.Ort:0.00} rgb-psnr={e1.Psnr:0.00} yuv-ort-fark={e2.Ort:0.00} yuv-psnr={e2.Psnr:0.00}"));
        return 0;
    }

    private static string OrtakRgb(MediaInfo info)
    {
        var (m, t, p) = info.ColorSpace?.ToLowerInvariant() switch
        {
            "bt709" => ("709", "709", "709"),
            "bt2020nc" => ("2020_ncl", "2020_10", "2020"),
            _ => ("170m", "601", "170m")
        };
        p = info.ColorPrimaries?.ToLowerInvariant() switch
        {
            "bt709" => "709",
            "bt2020" => "2020",
            "smpte170m" => "170m",
            _ => p
        };
        var r = info.ColorRange?.ToLowerInvariant() is "pc" ? "full" : "limited";
        return Fmt($"zscale=min={m}:tin={t}:pin={p}:rin={r}:t=709:p=709,format=gbrp");
    }

    private static byte[] Kare(string dosya, string bicim)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", dosya, "-vf", Suzgec(bicim), "-frames:v", "1", "-pix_fmt", Bicim(bicim), "-f", "rawvideo", "-" })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        using var bellek = new MemoryStream();
        surec.StandardOutput.BaseStream.CopyTo(bellek);
        surec.WaitForExit();
        _ = hata.Result;
        return bellek.ToArray();
    }

    private static string Suzgec(string bicim) => bicim.StartsWith("zscale", StringComparison.Ordinal) ? "select=eq(n\\,24)," + bicim : "select=eq(n\\,24)";

    private static string Bicim(string bicim) => bicim.StartsWith("zscale", StringComparison.Ordinal) ? "gbrp" : bicim;

    private static (double Ort, double Psnr) Olc(byte[] a, byte[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            throw new InvalidOperationException(Fmt($"kare boyu tutmuyor: {a.Length} / {b.Length}"));
        double mutlak = 0, kare = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var f = (double)a[i] - b[i];
            mutlak += Math.Abs(f);
            kare += f * f;
        }
        var mse = kare / a.Length;
        return (mutlak / a.Length, mse == 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 / mse));
    }

    private static (int Kod, string Metin) Kos(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        surec.WaitForExit();
        return (surec.ExitCode, cikti.Result + hata.Result);
    }

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
