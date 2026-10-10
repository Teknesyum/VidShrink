using System.Diagnostics;
using System.Text;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür sekmesinin hareketli WebP ve AVIF çıktısı: argüman dizisi, mp4 olumsuz kontrolü ve
/// 2 sn'lik 160x90 lavfi klibinin gerçek çevirisi. Ölçüm <c>docs/olcumler/hareketli-webp-avif.md</c>.
/// </summary>
public sealed class HareketliWebpAvifTests
{
    private static readonly MediaInfo Source = new()
    {
        FilePath = @"C:\media\source.mp4",
        FileSizeBytes = 20_000_000L,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    [Fact]
    public void WebpKodlayiciDonguVeSesBayraklariniTasir()
    {
        var plan = new ConversionPlan { Container = "webp", VideoCodec = "libx264", Crf = 23, AudioCodec = "aac", Height = 360 };

        var args = ConversionArguments.Build(Source, plan, @"C:\media\out.webp");

        Assert.Equal("libwebp_anim", Deger(args, "-c:v"));
        Assert.Equal("0", Deger(args, "-loop"));
        Assert.Equal("webp", Deger(args, "-f"));
        Assert.Equal("63", Deger(args, "-quality"));
        Assert.Equal("scale=-2:360:flags=lanczos", Deger(args, "-vf"));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-c:a", args);
        Assert.DoesNotContain("-crf", args);
        Assert.DoesNotContain("-movflags", args);
        Assert.Equal(@"C:\media\out.webp", args[^1]);
    }

    [Fact]
    public void AvifKodlayiciDonguVeSesBayraklariniTasir()
    {
        var plan = new ConversionPlan { Container = "avif", VideoCodec = "libx264", Crf = 23, AudioCodec = "copy" };

        var args = ConversionArguments.Build(Source, plan, @"C:\media\out.avif");

        Assert.Equal("libsvtav1", Deger(args, "-c:v"));
        Assert.Equal("0", Deger(args, "-loop"));
        Assert.Equal("avif", Deger(args, "-f"));
        Assert.Equal("32", Deger(args, "-crf"));
        Assert.Equal("yuv420p", Deger(args, "-pix_fmt"));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-c:a", args);
        Assert.DoesNotContain("-quality", args);
        Assert.DoesNotContain("-movflags", args);
        Assert.Equal(@"C:\media\out.avif", args[^1]);
    }

    [Theory]
    [InlineData("libx264", 10, "100", "18")]
    [InlineData("libx264", 45, "0", "55")]
    [InlineData("libsvtav1", 30, "68", "30")]
    [InlineData("libvpx-vp9", 63, "0", "55")]
    [InlineData("libx264", 5, "100", "18")]
    public void KaliteSeciliKodeginAraligindakiKonumdanCevrilir(string codec, int crf, string webpQuality, string avifCrf)
    {
        var webp = ConversionArguments.Build(Source, new ConversionPlan { Container = "webp", VideoCodec = codec, Crf = crf }, "out.webp");
        var avif = ConversionArguments.Build(Source, new ConversionPlan { Container = "avif", VideoCodec = codec, Crf = crf }, "out.avif");

        Assert.Equal(webpQuality, Deger(webp, "-quality"));
        Assert.Equal(avifCrf, Deger(avif, "-crf"));
    }

    [Fact]
    public void BitHiziKipindeAvifBitHiziAlirWebpVarsayilandaKalir()
    {
        var webp = ConversionArguments.Build(Source,
            new ConversionPlan { Container = "webp", QualityMode = ConversionQualityMode.Bitrate, VideoBitrateK = 900 }, "out.webp");
        var avif = ConversionArguments.Build(Source,
            new ConversionPlan { Container = "avif", QualityMode = ConversionQualityMode.Bitrate, VideoBitrateK = 900 }, "out.avif");

        Assert.DoesNotContain("-quality", webp);
        Assert.DoesNotContain("-b:v", webp);
        Assert.Equal("900k", Deger(avif, "-b:v"));
        Assert.DoesNotContain("-crf", avif);
    }

    [Fact]
    public void Mp4CiktisindaHareketliGorselBayraklariYok()
    {
        var args = ConversionArguments.Build(Source, new ConversionPlan(), @"C:\media\out.mp4");

        Assert.DoesNotContain("-loop", args);
        Assert.DoesNotContain("-quality", args);
        Assert.DoesNotContain("-an", args);
        Assert.DoesNotContain("-f", args);
        Assert.DoesNotContain("libwebp_anim", args);
        Assert.Equal("libx264", Deger(args, "-c:v"));
        Assert.Equal("aac", Deger(args, "-c:a"));
    }

    [Theory]
    [InlineData("webp")]
    [InlineData("avif")]
    public void SesSecimiDogrulamayiDusurmezAkisKopyasiReddedilir(string container)
    {
        var sessiz = new MediaInfo
        {
            FilePath = Source.FilePath, FileSizeBytes = Source.FileSizeBytes, DurationSeconds = 120,
            Width = 1920, Height = 1080, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1_400_000
        };

        Assert.Empty(ConversionArguments.Validate(Source, new ConversionPlan { Container = container, AudioCodec = "libopus" }));
        Assert.Empty(ConversionArguments.Validate(Source, new ConversionPlan { Container = container, AudioCodec = "copy" }));
        Assert.Empty(ConversionArguments.Validate(sessiz, new ConversionPlan { Container = container, AudioCodec = "copy" }));
        Assert.Contains(
            ConversionArguments.Validate(Source, new ConversionPlan { Container = container, VideoCodec = "copy" }),
            hata => hata.Contains("does not support copying", StringComparison.Ordinal));
        Assert.NotEmpty(ConversionArguments.Validate(Source, new ConversionPlan { Container = "mp4", AudioCodec = "libopus" }));
    }

    [Fact]
    public void UzantiKaptanGelirVeSecimListesininSonundaDurur()
    {
        Assert.EndsWith(".webp", ShrinkEngine.UniqueOutputPath(@"C:\media\source.mp4", "converted", "webp"), StringComparison.Ordinal);
        Assert.EndsWith(".avif", ShrinkEngine.UniqueOutputPath(@"C:\media\source.mp4", "converted", "avif"), StringComparison.Ordinal);
        Assert.True(new ConversionPlan { Container = "webp" }.AnimatedImage);
        Assert.True(new ConversionPlan { Container = "avif" }.AnimatedImage);
        Assert.False(new ConversionPlan { Container = "gif" }.AnimatedImage);
        Assert.False(new ConversionPlan { Container = "webm" }.AnimatedImage);
        Assert.Equal("image/webp", VidShrink.Core.Share.MediaTypes.ForFile("a.webp"));
        Assert.Equal("image/avif", VidShrink.Core.Share.MediaTypes.ForFile("a.avif"));

        var xaml = File.ReadAllText(Path.Combine(KokDizin(), "src", "VidShrink.App", "MainWindow.axaml"));
        var kutu = xaml[xaml.IndexOf("x:Name=\"CmbContainer\"", StringComparison.Ordinal)..];
        kutu = kutu[..kutu.IndexOf("</ComboBox>", StringComparison.Ordinal)];
        var etiketler = System.Text.RegularExpressions.Regex.Matches(kutu, "Tag=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(
            new[] { "mp4", "mkv", "webm", "mov", "avi", "gif", "mp3", "m4a", "wav", "flac", "webp", "avif", "mxf" },
            etiketler);
    }

    [FfmpegFact]
    public async Task KisaKlipHareketliWebpVeAvifOlur()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "hareketli-gorsel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=15:duration=2",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var info = await FfprobeClient.ProbeAsync(kaynak);
            Assert.True(info.HasAudio);

            var webp = Path.Combine(klasor, "cikti.webp");
            var webpCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info, new ConversionPlan { Container = "webp" }, webp)));
            Assert.True(webpCeviri.Ok, webpCeviri.StandardError);
            var webpAkislari = Akislar(webp);
            Assert.DoesNotContain(webpAkislari, akis => akis.Tur == "audio");
            Assert.Contains(webpAkislari, akis => akis.Tur == "video" && akis.Kodek.StartsWith("webp", StringComparison.Ordinal) && akis.Paket > 1);
            Assert.Equal(0, WebpDonguSayisi(webp));

            var avif = Path.Combine(klasor, "cikti.avif");
            var avifCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info, new ConversionPlan { Container = "avif" }, avif)));
            Assert.True(avifCeviri.Ok, avifCeviri.StandardError);
            var avifAkislari = Akislar(avif);
            Assert.DoesNotContain(avifAkislari, akis => akis.Tur == "audio");
            Assert.Contains(avifAkislari, akis => akis.Tur == "video" && akis.Kodek == "av1" && akis.Paket == 30);
            Assert.Equal("avis", Encoding.ASCII.GetString(File.ReadAllBytes(avif), 8, 4));

            var uydurma = ConversionArguments.Build(info, new ConversionPlan { Container = "webp" }, Path.Combine(klasor, "uydurma.webp")).ToList();
            uydurma[uydurma.IndexOf("libwebp_anim")] = "libwebp_uydurma";
            var red = await FfmpegRunner.RunAsync(IkiIplik(uydurma));
            Assert.False(red.Ok);
            Assert.Contains("Unknown encoder", red.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static IReadOnlyList<string> IkiIplik(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        liste.InsertRange(liste.Count - 1, new[] { "-threads", "2" });
        liste.Insert(2, "-nostdin");
        return liste;
    }

    private static int WebpDonguSayisi(string dosya)
    {
        var bayt = File.ReadAllBytes(dosya);
        var imza = Encoding.ASCII.GetBytes("ANIM");
        for (var i = 12; i + 14 <= bayt.Length; i++)
        {
            if (bayt[i] != imza[0] || bayt[i + 1] != imza[1] || bayt[i + 2] != imza[2] || bayt[i + 3] != imza[3]) continue;
            return bayt[i + 12] | (bayt[i + 13] << 8);
        }
        throw new InvalidDataException("ANIM parcasi yok: " + dosya);
    }

    private sealed record Akis(string Tur, string Kodek, int Paket);

    private static IReadOnlyList<Akis> Akislar(string dosya)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-v", "error", "-count_packets",
                     "-show_entries", "stream=codec_type,codec_name,nb_read_packets", "-of", "csv=p=0", dosya
                 })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(10_000);
        _ = hata.Result;

        var akislar = new List<Akis>();
        foreach (var satir in metin.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
        {
            var parca = satir.Split(',');
            if (parca.Length < 3) continue;
            akislar.Add(new Akis(parca[1], parca[0], int.TryParse(parca[2], out var paket) ? paket : 0));
        }
        return akislar;
    }

    private static string Deger(IReadOnlyList<string> args, string bayrak)
    {
        var yer = args.IndexOf(bayrak);
        Assert.True(yer >= 0 && yer + 1 < args.Count, bayrak + " yok: " + string.Join(' ', args));
        return args[yer + 1];
    }

    private static string KokDizin()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new DirectoryNotFoundException("VidShrink.sln bulunamadi.");
    }
}
