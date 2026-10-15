using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// later 61: Dönüştür'de arşiv kodekleri (FFV1, CineForm), APNG çıktısı ve GIF'in döngü, renk ve
/// dither seçimi. Argüman dizisi, kap uyumu, doğrulama iletileri, pencere kolu ve kısa lavfi
/// klibin gerçek çevirisi. Kayıplı GIF yerel ffmpeg 9.0 gif kodlayıcısında yok, sunulmaz.
/// </summary>
public sealed class ArsivApngGifTests
{
    private static readonly MediaInfo Kaynak = new()
    {
        FilePath = @"C:\media\source.mp4",
        FileSizeBytes = 40_000_000L,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_700_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private const string Cikti = @"C:\media\out.gif";
    private const string Palet = @"C:\media\palette.png";

    [Fact]
    public void Ffv1KayipsizSeceneklerleYazilirKaliteBayragiYok()
    {
        var plan = new ConversionPlan { Container = "mkv", VideoCodec = "ffv1", VideoProfile = "archive", AudioCodec = "copy" };

        var args = ConversionArguments.Build(Kaynak, plan, @"C:\media\out.mkv");

        Assert.Equal("ffv1", Deger(args, "-c:v"));
        Assert.Equal("3", Deger(args, "-level"));
        Assert.Equal("1", Deger(args, "-g"));
        Assert.Equal("1", Deger(args, "-slicecrc"));
        foreach (var yok in new[] { "-pix_fmt", "-profile:v", "-crf", "-preset", "-b:v" })
            Assert.DoesNotContain(yok, args);
    }

    [Fact]
    public void CineFormOnYuzde10BitYazilir()
    {
        var plan = new ConversionPlan { Container = "mov", VideoCodec = "cfhd", VideoProfile = "film3+", AudioCodec = "copy" };

        var args = ConversionArguments.Build(Kaynak, plan, @"C:\media\out.mov");

        Assert.Equal("cfhd", Deger(args, "-c:v"));
        Assert.Equal("film3+", Deger(args, "-quality"));
        Assert.Equal("yuv422p10le", Deger(args, "-pix_fmt"));
        foreach (var yok in new[] { "-profile:v", "-crf", "-preset", "-b:v" })
            Assert.DoesNotContain(yok, args);
    }

    [Theory]
    [InlineData("ffv1", "archive", "mkv,mov,avi")]
    [InlineData("cfhd", "film3+", "mkv,mov,avi")]
    [InlineData("prores_ks", "hq", "mov")]
    public void ArsivKodeklerininKapTablosu(string kodek, string profil, string beklenen)
    {
        var videoKaplari = new[] { "mp4", "mkv", "mov", "webm", "avi", "mxf" };
        var kabul = videoKaplari.Where(kap => Uyumlu(kap, kodek, profil));

        Assert.Equal(beklenen.Split(','), kabul.ToArray());
    }

    [Fact]
    public void ArsivKodekleriListedeVeAraKodekSayilir()
    {
        var liste = KutuEtiketleri("CmbConvertCodec");

        Assert.Equal(new[] { "ffv1:archive", "cfhd:film3+" }, ConversionCodecChoice.Archive.ToArray());
        Assert.Contains("ffv1:archive", liste);
        Assert.Contains("cfhd:film3+", liste);
        Assert.Equal("ffv1", IntermediateCodecs.FromId("ffv1:archive")?.Encoder);
        Assert.Equal("cfhd", IntermediateCodecs.FromId("cfhd:film3+")?.Encoder);
        Assert.True(IntermediateCodecs.IsIntermediate("ffv1"));
        Assert.True(IntermediateCodecs.IsIntermediate("cfhd"));
        Assert.False(IntermediateCodecs.IsIntermediate("libx264"));
        Assert.Contains("ffv1:archive", CliConvert.VideoCodecs);
        Assert.Contains("cfhd:film3+", CliConvert.VideoCodecs);
        Assert.Contains("apng", CliConvert.Containers);
    }

    [Fact]
    public void HedefBoyutYoluArsivKodeginiReddeder()
    {
        var sonuc = PlanParser.Parse("{\"codec\":\"ffv1\",\"mode\":\"2pass\",\"videoBitrateK\":2000,\"width\":1920,\"height\":1080,\"fps\":30}",
            Kaynak, new PlanOptions { TargetMb = 10 });

        Assert.False(sonuc.Ok);
        Assert.Contains(sonuc.Errors, e => e.Contains("intermediate codec", StringComparison.Ordinal));
    }

    [Fact]
    public void ApngSonsuzDonguyleSessizYazilir()
    {
        var plan = new ConversionPlan { Container = "apng", VideoCodec = "libx264", AudioCodec = "aac", Height = 360 };

        var args = ConversionArguments.Build(Kaynak, plan, @"C:\media\out.apng");

        Assert.True(plan.AnimatedImage);
        Assert.Equal("apng", Deger(args, "-c:v"));
        Assert.Equal("0", Deger(args, "-plays"));
        Assert.Equal("apng", Deger(args, "-f"));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-loop", args);
        Assert.DoesNotContain("-crf", args);
        Assert.Empty(ConversionArguments.Validate(Kaynak, plan));
    }

    [Fact]
    public void ApngKodlayicisiYoksaKapSunulmaz()
    {
        Assert.Equal("apng", ConversionArguments.RequiredEncoder("apng"));
        Assert.False(ConversionArguments.ContainerAvailable("apng", new SahteKodlayicilar()));
        Assert.True(ConversionArguments.ContainerAvailable("apng", new SahteKodlayicilar("apng")));
        Assert.True(ConversionArguments.ContainerAvailable("apng", null));
        Assert.Contains("apng", KutuEtiketleri("CmbContainer"));
    }

    [Fact]
    public void GifVarsayilanKomutuDegismez()
    {
        var plan = new ConversionPlan { Container = "gif" };

        var ilk = ConversionArguments.Build(Kaynak, plan, Palet);
        var ikinci = ConversionArguments.Build(Kaynak, plan, Cikti, Palet);

        Assert.EndsWith("palettegen=stats_mode=diff", Deger(ilk, "-vf"), StringComparison.Ordinal);
        Assert.EndsWith("paletteuse=dither=sierra2_4a", Deger(ikinci, "-lavfi"), StringComparison.Ordinal);
        Assert.DoesNotContain("-loop", ilk);
        Assert.DoesNotContain("-loop", ikinci);
    }

    [Fact]
    public void GifDonguRenkVeDitherKomutaIner()
    {
        var plan = new ConversionPlan { Container = "gif", GifLoop = 3, GifColors = 64, GifDither = "bayer" };

        var ilk = ConversionArguments.Build(Kaynak, plan, Palet);
        var ikinci = ConversionArguments.Build(Kaynak, plan, Cikti, Palet);

        Assert.EndsWith("palettegen=stats_mode=diff:max_colors=64", Deger(ilk, "-vf"), StringComparison.Ordinal);
        Assert.DoesNotContain("-loop", ilk);
        Assert.EndsWith("paletteuse=dither=bayer", Deger(ikinci, "-lavfi"), StringComparison.Ordinal);
        Assert.Equal("3", Deger(ikinci, "-loop"));
        Assert.Equal(Cikti, ikinci[^1]);
        Assert.Equal("-1", Deger(ConversionArguments.Build(Kaynak, plan with { GifLoop = -1 }, Cikti, Palet), "-loop"));
        Assert.Empty(ConversionArguments.Validate(Kaynak, plan));
    }

    [Theory]
    [InlineData(1, null, null, "GIF palette colors must be between 2 and 256.")]
    [InlineData(257, null, null, "GIF palette colors must be between 2 and 256.")]
    [InlineData(null, -2, null, "GIF loop count must be between -1 and 65535.")]
    [InlineData(null, 65536, null, "GIF loop count must be between -1 and 65535.")]
    [InlineData(null, null, "uydurma", "The GIF dither mode is not supported.")]
    public void GecersizGifSecimiReddedilir(int? renk, int? dongu, string? dither, string ileti)
    {
        var plan = new ConversionPlan { Container = "gif", GifColors = renk, GifLoop = dongu, GifDither = dither };

        Assert.Contains(ileti, ConversionArguments.Validate(Kaynak, plan));

        Strings.Use("en");
        var cevrilmemis = LanguageCatalog.Display(Strings.Get("main.validation.untranslated-engine", ileti));
        Assert.NotEqual(cevrilmemis, LanguageCatalog.Validation(ileti));
    }

    [Theory]
    [InlineData(2, -1, "none")]
    [InlineData(256, 65535, "atkinson")]
    public void SinirdakiGifSecimiGecer(int renk, int dongu, string dither)
        => Assert.Empty(ConversionArguments.Validate(Kaynak, new ConversionPlan { Container = "gif", GifColors = renk, GifLoop = dongu, GifDither = dither }));

    [Fact]
    public void GifPaneliYalnizGifKabindaGorunurSecimKomutaIner()
    {
        var (mp4Gorunur, gifGorunur, komut, varsayilan) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Kaynak.FilePath, Kaynak);
            var mp4 = window.ConvertGifPanel.IsVisible;
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "gif");
            Dispatcher.UIThread.RunJobs();
            var gif = window.ConvertGifPanel.IsVisible;
            var bos = window.TxtConvertCommand.Text ?? "";
            window.CmbConvertGifLoop.SelectedItem = Oge(window.CmbConvertGifLoop, "-1");
            window.CmbConvertGifDither.SelectedItem = Oge(window.CmbConvertGifDither, "floyd_steinberg");
            window.CmbConvertGifColors.SelectedItem = Oge(window.CmbConvertGifColors, "32");
            Dispatcher.UIThread.RunJobs();
            var plan = window.CurrentConversionPlan();
            Assert.Equal(32, plan.GifColors);
            return (mp4, gif, window.TxtConvertCommand.Text ?? "", bos);
        });

        Assert.False(mp4Gorunur);
        Assert.True(gifGorunur);
        Assert.DoesNotContain("-loop", varsayilan, StringComparison.Ordinal);
        Assert.Contains("dither=sierra2_4a", varsayilan, StringComparison.Ordinal);
        Assert.Contains("-loop -1", komut, StringComparison.Ordinal);
        Assert.Contains("dither=floyd_steinberg", komut, StringComparison.Ordinal);
    }

    [FfmpegFact]
    public async Task KisaKlipArsivApngVeGifOlur()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "worktree-agent-afaf2ec2d9c08aa3b", "test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=15:duration=2",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var info = await FfprobeClient.ProbeAsync(kaynak);

            var ffv1 = Path.Combine(klasor, "cikti.mkv");
            var ffv1Ceviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info,
                new ConversionPlan { Container = "mkv", VideoCodec = "ffv1", VideoProfile = "archive", AudioCodec = null }, ffv1)));
            Assert.True(ffv1Ceviri.Ok, ffv1Ceviri.StandardError);
            Assert.Equal("ffv1", AkisOku(ffv1, "codec_name"));

            var cfhd = Path.Combine(klasor, "cikti.mov");
            var cfhdCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info,
                new ConversionPlan { Container = "mov", VideoCodec = "cfhd", VideoProfile = "film3+", AudioCodec = null }, cfhd)));
            Assert.True(cfhdCeviri.Ok, cfhdCeviri.StandardError);
            Assert.Equal("cfhd", AkisOku(cfhd, "codec_name"));
            Assert.Equal("yuv422p10le", AkisOku(cfhd, "pix_fmt"));

            var apng = Path.Combine(klasor, "cikti.apng");
            var apngCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info, new ConversionPlan { Container = "apng" }, apng)));
            Assert.True(apngCeviri.Ok, apngCeviri.StandardError);
            Assert.Equal("apng", AkisOku(apng, "codec_name"));

            var gifPlan = new ConversionPlan { Container = "gif", GifLoop = 2, GifColors = 16, GifDither = "none" };
            var palet = Path.Combine(klasor, "palet.png");
            var gif = Path.Combine(klasor, "cikti.gif");
            var paletCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info, gifPlan, palet)));
            Assert.True(paletCeviri.Ok, paletCeviri.StandardError);
            var gifCeviri = await FfmpegRunner.RunAsync(IkiIplik(ConversionArguments.Build(info, gifPlan, gif, palet)));
            Assert.True(gifCeviri.Ok, gifCeviri.StandardError);
            Assert.Equal(2, GifDonguSayisi(gif));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private sealed class SahteKodlayicilar(params string[] calisan) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => calisan.Contains(name);
        public bool WorksAsEncoder(string codec) => HasEncoder(codec);
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    private static bool Uyumlu(string kap, string kodek, string? profil)
    {
        var plan = new ConversionPlan { Container = kap, VideoCodec = kodek, VideoProfile = profil, AudioCodec = "copy" };
        return !ConversionArguments.Validate(Kaynak, plan).Any(h => h.Contains("video encoder", StringComparison.Ordinal));
    }

    private static int GifDonguSayisi(string dosya)
    {
        var bayt = File.ReadAllBytes(dosya);
        var imza = "NETSCAPE2.0"u8.ToArray();
        for (var i = 0; i + imza.Length + 5 <= bayt.Length; i++)
        {
            if (!bayt.AsSpan(i, imza.Length).SequenceEqual(imza)) continue;
            return bayt[i + imza.Length + 2] | (bayt[i + imza.Length + 3] << 8);
        }
        return -1;
    }

    private static string AkisOku(string dosya, string alan)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-hide_banner", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=" + alan, "-of", "csv=p=0", dosya })
            psi.ArgumentList.Add(arg);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(10_000);
        _ = hata.Result;
        return metin.Trim();
    }

    private static IReadOnlyList<string> IkiIplik(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        liste.InsertRange(liste.Count - 1, new[] { "-threads", "2" });
        liste.Insert(2, "-nostdin");
        return liste;
    }

    private static string Deger(IReadOnlyList<string> args, string bayrak)
    {
        var yer = args.ToList().IndexOf(bayrak);
        Assert.True(yer >= 0 && yer + 1 < args.Count, bayrak + " yok: " + string.Join(' ', args));
        return args[yer + 1];
    }

    private static List<string> KutuEtiketleri(string ad)
    {
        var xaml = File.ReadAllText(Path.Combine(KokDizin(), "src", "VidShrink.App", "MainWindow.axaml"));
        var kutu = xaml[xaml.IndexOf($"x:Name=\"{ad}\"", StringComparison.Ordinal)..];
        kutu = kutu[..kutu.IndexOf("</ComboBox>", StringComparison.Ordinal)];
        return Regex.Matches(kutu, "Tag=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
    }

    private static ComboBoxItem Oge(ComboBox kutu, string etiket)
        => kutu.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "worktree-agent-afaf2ec2d9c08aa3b", "pencere-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json") };
                try { return govde(window); }
                finally { window.Close(); }
            });
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static string KokDizin()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new InvalidOperationException("VidShrink.sln bulunamadı");
    }
}
