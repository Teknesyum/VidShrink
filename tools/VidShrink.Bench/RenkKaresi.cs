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
        return await NedenAsync(klasor);
    }

    private const string Bt709Etiket = "-colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv";
    private const string YalnizMatrisEtiket = "-colorspace bt709 -color_primaries smpte170m -color_trc smpte170m -color_range tv";
    private const string Bt601Etiket = "-colorspace smpte170m -color_primaries smpte170m -color_trc smpte170m -color_range tv";
    private const string YalnizMatrisParam = "setparams=color_primaries=smpte170m:color_trc=smpte170m";
    private const string Donusum709 = "zscale=min=170m:tin=601:pin=170m:rin=limited:m=709:t=709:p=709:r=limited";

    private static readonly (string Ad, string Suzgec, string Bicim, string Etiket)[] NedenKollari =
    {
        ("ayni-dosya", "null", "yuv420p", Bt601Etiket),
        ("gidis-donus-bilinear", "zscale=filter=bilinear,format=yuv444p,zscale=filter=bilinear", "yuv420p", Bt601Etiket),
        ("gidis-donus-spline36", "zscale=filter=spline36,format=yuv444p,zscale=filter=spline36", "yuv420p", Bt601Etiket),
        ("gidis-donus-lanczos", "zscale=filter=lanczos,format=yuv444p,zscale=filter=lanczos", "yuv420p", Bt601Etiket),
        ("bt709-444", Donusum709, "yuv444p", Bt709Etiket),
        ("bt709-bilinear", Donusum709 + ":filter=bilinear", "yuv420p", Bt709Etiket),
        ("bt709-bicubic", Donusum709 + ":filter=bicubic", "yuv420p", Bt709Etiket),
        ("bt709-spline36", Donusum709 + ":filter=spline36", "yuv420p", Bt709Etiket),
        ("bt709-lanczos", Donusum709 + ":filter=lanczos", "yuv420p", Bt709Etiket),
        ("bt709-cekirdek", "", "yuv420p", Bt709Etiket),
        ("matris-zscale", "zscale=min=170m:tin=601:pin=170m:rin=limited:m=709:t=601:p=170m:r=limited", "yuv420p", YalnizMatrisEtiket),
        ("matris-zscale-lanczos", "zscale=min=170m:tin=601:pin=170m:rin=limited:m=709:t=601:p=170m:r=limited:filter=lanczos", "yuv420p", YalnizMatrisEtiket),
        ("matris-sws-varsayilan", "scale=in_color_matrix=bt601:out_color_matrix=bt709:in_range=tv:out_range=tv," + YalnizMatrisParam, "yuv420p", YalnizMatrisEtiket),
        ("matris-sws-dogru", "scale=in_color_matrix=bt601:out_color_matrix=bt709:in_range=tv:out_range=tv:flags=accurate_rnd+full_chroma_int+full_chroma_inp," + YalnizMatrisParam, "yuv420p", YalnizMatrisEtiket)
    };

    private static async Task<int> NedenAsync(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kayipsiz.mp4");
        var (kod, hata) = Kos(ToolLocator.Ffmpeg, Arglar(Fmt($"-hide_banner -nostdin -y -v error -f lavfi -i testsrc2=s={En}x{Boy}:r=24:d=2 -c:v libx264 -qp 0 -preset veryfast -pix_fmt yuv420p {Bt601Etiket} -threads 2"), kaynak));
        if (kod != 0)
        {
            Console.WriteLine("kayipsiz kaynak uretilemedi: " + hata);
            return 1;
        }
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var kaynakRgb = Kare(kaynak, OrtakRgb(info));
        var cekirdek = VideoFilterChain.ConversionColorFilter(info, ColorMatrixTarget.Bt709, ColorRangeTarget.Keep)!;
        Console.WriteLine("neden: kayipsiz (-qp 0) kodlama, kaynak testsrc2 BT.601 sinirli 4:2:0; cekirdek suzgeci: " + cekirdek);
        foreach (var (ad, suzgec, bicim, etiket) in NedenKollari)
        {
            var cikti = Path.Combine(klasor, "neden-" + ad + ".mp4");
            var vf = suzgec.Length == 0 ? cekirdek : suzgec;
            (kod, hata) = Kos(ToolLocator.Ffmpeg, Arglar(Fmt($"-hide_banner -nostdin -y -v error -threads 2 -i"), kaynak, "-vf", vf,
                "-c:v", "libx264", "-qp", "0", "-preset", "veryfast", "-pix_fmt", bicim, "-threads", "2").Concat(etiket.Split(' ')).Append(cikti).ToArray());
            if (kod != 0)
            {
                Console.WriteLine(Fmt($"neden={ad} kodlanamadi: {hata}"));
                return 1;
            }
            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            var rgb = Olc(kaynakRgb, Kare(cikti, OrtakRgb(sonuc)));
            Console.WriteLine(Fmt($"neden={ad} bicim={bicim} etiket={sonuc.ColorSpace}/{sonuc.ColorPrimaries}/{sonuc.ColorTransfer}/{sonuc.ColorRange} rgb-ort-fark={rgb.Ort:0.00} rgb-psnr={rgb.Psnr:0.00}"));
        }
        return 0;
    }

    private static string[] Arglar(string bosluklu, params string[] ek) => bosluklu.Split(' ').Concat(ek).ToArray();

    private static string OrtakRgb(MediaInfo info)
    {
        var (m, t, p) = info.ColorSpace?.ToLowerInvariant() switch
        {
            "bt709" => ("709", "709", "709"),
            "bt2020nc" => ("2020_ncl", "2020_10", "2020"),
            _ => ("170m", "601", "170m")
        };
        t = info.ColorTransfer?.ToLowerInvariant() switch
        {
            "bt709" => "709",
            "smpte170m" or "bt470bg" => "601",
            "bt2020-10" => "2020_10",
            _ => t
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
