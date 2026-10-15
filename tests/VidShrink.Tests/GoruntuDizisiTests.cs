using System.Diagnostics;
using System.Globalization;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// DON-P8, görüntü dizisi girdisi ve çıktısı: numaralı resimlerin <c>image2</c> deseni ve kare hızı,
/// numarasında boşluk olan dizi, PNG/JPEG dizisi ve tek kare argümanları. Canlı kollar 64x48 30 kare,
/// <c>-threads 2</c>; klasör <c>.calisma/goruntu-dizisi-*</c>, test siler.
/// </summary>
public sealed class GoruntuDizisiTests
{
    private static readonly MediaInfo Video = new()
    {
        FilePath = @"C:\media\kaynak.mp4",
        FileSizeBytes = 5_000_000L,
        DurationSeconds = 10,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 4_000_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    private static IEnumerable<string> Adlar(string klasor, string onek, string sonek, params string[] numaralar)
        => numaralar.Select(n => Path.Combine(klasor, onek + n + sonek));

    [Fact]
    public void SifirDolguluDiziDeseniVeBaslangiciVerir()
    {
        var dizi = ImageSequence.Detect(Adlar(@"C:\kareler", "kare_", ".png", "0003", "0001", "0002", "0004"))!;

        Assert.Equal(Path.Combine(@"C:\kareler", "kare_%04d.png"), dizi.Pattern);
        Assert.Equal(1, dizi.StartNumber);
        Assert.Equal(4, dizi.Count);
        Assert.Equal(0, dizi.Skipped);
        Assert.Equal("kare", dizi.DisplayName);
        Assert.EndsWith("kare_0001.png", dizi.Files[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DolgusuzDiziYuzdeDDeseniVerir()
    {
        var dizi = ImageSequence.Detect(Adlar(@"C:\k", "img", ".jpg", "9", "10", "11", "8"))!;

        Assert.Equal(Path.Combine(@"C:\k", "img%d.jpg"), dizi.Pattern);
        Assert.Equal(8, dizi.StartNumber);
        Assert.Equal(4, dizi.Count);
    }

    [Fact]
    public void BoslukluDizideKesintisizKisimSayilirKalaniAtlanir()
    {
        var dizi = ImageSequence.Detect(Adlar(@"C:\k", "f", ".png", "001", "002", "003", "005", "006"))!;

        Assert.Equal(3, dizi.Count);
        Assert.Equal(2, dizi.Skipped);
        Assert.Equal(3, dizi.Files.Count);
    }

    [Fact]
    public void AddakiYuzdeIsaretiKacirilir()
    {
        var dizi = ImageSequence.Detect(Adlar(@"C:\k", "a%b_", ".png", "01", "02"))!;

        Assert.Equal(Path.Combine(@"C:\k", "a%%b_%02d.png"), dizi.Pattern);
        Assert.Equal(Path.Combine(@"C:\o", "a%%b_%05d.png"), ConversionArguments.SequenceOutputPattern(@"C:\o", "a%b", "png"));
    }

    [Fact]
    public void TekResimVeResimOlmayanDosyaDiziSayilmaz()
    {
        Assert.Null(ImageSequence.Detect(Adlar(@"C:\k", "kare_", ".png", "0001")));
        Assert.Null(ImageSequence.Detect(Adlar(@"C:\k", "klip", ".mp4", "1", "2", "3")));
        Assert.Null(ImageSequence.Detect(new[] { @"C:\k\kapak.png", @"C:\k\arka.png" }));
    }

    [Fact]
    public void DiziGirdisiImage2VeKareHiziYazar()
    {
        var dizi = ImageSequence.Detect(Adlar(@"C:\k", "kare_", ".png", "0007", "0008", "0009"))!;
        var info = dizi.ToMediaInfo(new ImageSequenceFrame(64, 48, "png", "rgb24"), 12);

        var args = ConversionArguments.Build(info, new ConversionPlan { Container = "mp4", Fps = 12 }, @"C:\o\cikti.mp4");

        var yer = args.IndexOf("-i");
        Assert.Equal(new[] { "-f", "image2", "-framerate", "12", "-start_number", "7", "-i", dizi.Pattern },
            args.Skip(yer - 6).Take(8));
        Assert.DoesNotContain(args, a => a.StartsWith("fps=", StringComparison.Ordinal));
        Assert.Equal(0.25, info.DurationSeconds, 3);
        Assert.Equal(12, info.Fps);
    }

    [Fact]
    public void VideoKaynaktaImage2Yok()
    {
        var args = ConversionArguments.Build(Video, new ConversionPlan(), @"C:\o\cikti.mp4");

        Assert.DoesNotContain("image2", args);
        Assert.DoesNotContain("-framerate", args);
        Assert.Equal(Video.FilePath, args[args.IndexOf("-i") + 1]);
    }

    [Fact]
    public void PngDizisiCiktisiKodlayiciVeNumarayiYazar()
    {
        var desen = ConversionArguments.SequenceOutputPattern(@"C:\o\kaynak_frames", "kaynak_frames", "png");

        var args = ConversionArguments.Build(Video, new ConversionPlan { Container = "png", Height = 360 }, desen);

        Assert.Equal("png", Deger(args, "-c:v"));
        Assert.Equal("image2", Deger(args, "-f"));
        Assert.Equal("1", Deger(args, "-start_number"));
        Assert.Equal("scale=-2:360:flags=lanczos", Deger(args, "-vf"));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-c:a", args);
        Assert.DoesNotContain("-crf", args);
        Assert.DoesNotContain("-frames:v", args);
        Assert.Equal(Path.Combine(@"C:\o\kaynak_frames", "kaynak_frames_%05d.png"), args[^1]);
    }

    [Fact]
    public void JpegDizisiVeTekKareArgumanlari()
    {
        var jpeg = ConversionArguments.Build(Video, new ConversionPlan { Container = "jpg" }, @"C:\o\d_%05d.jpg");
        var tek = ConversionArguments.Build(Video, new ConversionPlan { Container = "png", SingleFrame = true }, @"C:\o\kare.png");

        Assert.Equal("mjpeg", Deger(jpeg, "-c:v"));
        Assert.Equal("2", Deger(jpeg, "-q:v"));
        Assert.Equal("1", Deger(tek, "-frames:v"));
        Assert.Equal("1", Deger(tek, "-update"));
        Assert.DoesNotContain("image2", tek);
        Assert.Equal(@"C:\o\kare.png", tek[^1]);
    }

    [Fact]
    public void DiziCiktisiSesSeciminiOkumazKopyayiReddeder()
    {
        var sessiz = Video with { AudioCodec = null, AudioChannels = 0 };

        Assert.Empty(ConversionArguments.Validate(sessiz, new ConversionPlan { Container = "png", AudioCodec = "copy" }));
        Assert.Empty(ConversionArguments.Validate(Video, new ConversionPlan { Container = "jpg", VideoCodec = "libx265" }));
        Assert.Contains(ConversionArguments.Validate(Video, new ConversionPlan { Container = "png", VideoCodec = "copy" }),
            hata => hata.Contains("Image sequence output", StringComparison.Ordinal));
        Assert.False(new ConversionPlan { Container = "png" }.CarriesAudio);
        Assert.True(new ConversionPlan { Container = "mp4" }.CarriesAudio);
    }

    [Fact]
    public void CiktiKlasoruVarOlaniEzmez()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "goruntu-dizisi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "klip.mp4");
            Assert.Equal(Path.Combine(klasor, "klip_frames"), EncodeRunner.SequenceOutputFolder(kaynak));
            Directory.CreateDirectory(Path.Combine(klasor, "klip_frames"));
            File.WriteAllText(Path.Combine(klasor, "klip_frames_2"), "");
            Assert.Equal(Path.Combine(klasor, "klip_frames_3"), EncodeRunner.SequenceOutputFolder(kaynak));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task OtuzKarelikDiziVideoGifVePngDizisiOlur()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "goruntu-dizisi-" + Guid.NewGuid().ToString("N"));
        var kareler = Path.Combine(klasor, "kareler");
        Directory.CreateDirectory(kareler);
        try
        {
            await KareUret(kareler, 30);
            var dizi = ImageSequence.Detect(Directory.GetFiles(kareler))!;
            Assert.Equal(30, dizi.Count);
            var kare = await FfprobeClient.ProbeFrameAsync(dizi.Files[0]);
            Assert.Equal(new ImageSequenceFrame(64, 48, "png", kare!.PixelFormat), kare);
            var info = dizi.ToMediaInfo(kare, 10);

            var mp4 = Path.Combine(klasor, "dizi.mp4");
            await Kos(ConversionArguments.Build(info, new ConversionPlan { Container = "mp4", Fps = 10 }, mp4));
            var (paket, sure) = PaketVeSure(mp4);
            Assert.Equal(30, paket);
            Assert.Equal(3.0, sure, 1);

            var gif = Path.Combine(klasor, "dizi.gif");
            var palet = Path.Combine(klasor, "palet.png");
            var gifPlan = new ConversionPlan { Container = "gif", Fps = 10 };
            await Kos(ConversionArguments.Build(info, gifPlan, palet));
            await Kos(ConversionArguments.Build(info, gifPlan, gif, palet));
            Assert.Equal(30, PaketVeSure(gif).Paket);

            var video = await FfprobeClient.ProbeAsync(mp4);
            var cikti = EncodeRunner.SequenceOutputFolder(mp4);
            await Kos(ConversionArguments.Build(video, new ConversionPlan { Container = "png" },
                ConversionArguments.SequenceOutputPattern(cikti, Path.GetFileName(cikti), "png")), cikti);
            var yazilan = Directory.GetFiles(cikti).Select(Path.GetFileName).OrderBy(a => a, StringComparer.Ordinal).ToArray();
            Assert.Equal(30, yazilan.Length);
            Assert.Equal("dizi_frames_00001.png", yazilan[0]);
            Assert.Equal("dizi_frames_00030.png", yazilan[^1]);

            var tek = Path.Combine(klasor, "tek.jpg");
            await Kos(ConversionArguments.Build(video, new ConversionPlan { Container = "jpg", SingleFrame = true }, tek));
            Assert.Equal("mjpeg", (await FfprobeClient.ProbeFrameAsync(tek))!.Codec);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task BoslukluDizideCiktiKesintisizKareSayisiKadar()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "goruntu-dizisi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            await KareUret(klasor, 30);
            File.Delete(Path.Combine(klasor, "kare_0011.png"));
            var dizi = ImageSequence.Detect(Directory.GetFiles(klasor))!;
            Assert.Equal(10, dizi.Count);
            Assert.Equal(19, dizi.Skipped);
            var info = dizi.ToMediaInfo((await FfprobeClient.ProbeFrameAsync(dizi.Files[0]))!, 10);

            var mp4 = Path.Combine(klasor, "dizi.mp4");
            await Kos(ConversionArguments.Build(info, new ConversionPlan { Container = "mp4" }, mp4));
            var (paket, sure) = PaketVeSure(mp4);
            Assert.Equal(10, paket);
            Assert.Equal(1.0, sure, 1);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void PenceredeDiziSecilinceKaynakVeKomutDiziyiSoyler()
    {
        var tr = Locales.Domain("tr", "main");
        var (yuklu, kaynak, komut25, komut24) = DiziPenceresinde(new[] { "001", "002", "003", "005" }, window =>
        {
            window.UseTurkish();
            Bitir(window.BrowseSequenceAsync());
            var ilk = window.TxtConvertCommand.Text ?? "";
            window.CmbConvertFps.SelectedIndex = 3;
            return (window.LoadedSequence?.Count ?? 0, window.TxtConvertSource.Text ?? "", ilk, window.TxtConvertCommand.Text ?? "");
        });

        Assert.Equal(3, yuklu);
        var satirlar = kaynak.Split('\n');
        Assert.Equal(2, satirlar.Length);
        Assert.StartsWith("kare — 3 ", satirlar[0], StringComparison.Ordinal);
        Assert.Equal(string.Format(tr["main.convert.sequence.source"], "kare", "3"), satirlar[0], ignoreCase: true);
        Assert.Equal(string.Format(tr["main.convert.sequence.gap"], "1"), satirlar[1], ignoreCase: true);
        Assert.Contains("-f image2 -framerate 25 -start_number 1 -i", komut25, StringComparison.Ordinal);
        Assert.Contains("-framerate 24 ", komut24, StringComparison.Ordinal);
        Assert.Contains("kare_%03d.png", komut24, StringComparison.Ordinal);
    }

    [Fact]
    public void PenceredeDiziYoksaHataSatiriVeEskiKaynakKalir()
    {
        var tr = Locales.Domain("tr", "main");
        var (yuklu, hata) = DiziPenceresinde(new[] { "001" }, window =>
        {
            window.UseTurkish();
            Bitir(window.BrowseSequenceAsync());
            return (window.LoadedSequence, window.TxtSourceStatus.Text ?? "");
        });

        Assert.Null(yuklu);
        Assert.Equal(tr["main.convert.sequence.none"], hata);
    }

    [Fact]
    public void PenceredeResimKabiTekKareKutusunuGosterir()
    {
        var (mp4, png, jpg) = DiziPenceresinde(new[] { "1", "2" }, window =>
        {
            window.LoadWithoutProbing(Video.FilePath, Video);
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            var ilk = window.ChkConvertSingleFrame.IsVisible;
            window.CmbContainer.SelectedItem = Kap(window, "png");
            var ikinci = window.ChkConvertSingleFrame.IsVisible;
            window.CmbContainer.SelectedItem = Kap(window, "jpg");
            window.ChkConvertSingleFrame.IsChecked = true;
            return (ilk, ikinci, window.TxtConvertCommand.Text ?? "");
        });

        Assert.False(mp4);
        Assert.True(png);
        Assert.Contains("-c:v mjpeg", jpg, StringComparison.Ordinal);
        Assert.Contains("-frames:v 1", jpg, StringComparison.Ordinal);
    }

    [Fact]
    public void DiziAnahtarlariKirkIkiDildeCevrili()
    {
        var anahtarlar = new[]
        {
            "main.convert.sequence.browse", "main.convert.sequence.source", "main.convert.sequence.gap",
            "main.convert.sequence.none", "main.convert.sequence.unreadable", "main.convert.container.png-sequence",
            "main.convert.container.jpg-sequence", "main.convert.single-frame", "main.convert.single-frame.tip",
            "main.validation.sequence-copy"
        };
        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            foreach (var anahtar in anahtarlar)
            {
                if (!values.TryGetValue(anahtar, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language}: {anahtar} yok");
                else if (english[anahtar].Contains("{0}", StringComparison.Ordinal) && !value.Contains("{0}", StringComparison.Ordinal))
                    eksik.Add($"{language}: {anahtar} {{0}} yok");
                else if (english[anahtar].Contains("{1}", StringComparison.Ordinal) && !value.Contains("{1}", StringComparison.Ordinal))
                    eksik.Add($"{language}: {anahtar} {{1}} yok");
            }
        }

        Assert.True(eksik.Count == 0, string.Join(Environment.NewLine, eksik));
    }

    private static void Bitir(Task task)
    {
        Assert.True(task.IsCompleted, "sahte seçici ve yoklayıcı eşzamanlı bitmeliydi");
        task.GetAwaiter().GetResult();
    }

    private static Avalonia.Controls.ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<Avalonia.Controls.ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T DiziPenceresinde<T>(string[] numaralar, Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "goruntu-dizisi-" + Guid.NewGuid().ToString("N"));
        var kareler = Path.Combine(klasor, "kareler");
        Directory.CreateDirectory(kareler);
        foreach (var dosya in Adlar(kareler, "kare_", ".png", numaralar)) File.WriteAllBytes(dosya, Array.Empty<byte>());
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow
                {
                    SettingsPathOverride = Path.Combine(klasor, "settings.json"),
                    SequenceFolderPicker = () => Task.FromResult<string?>(kareler),
                    SequenceProber = (_, _) => Task.FromResult<ImageSequenceFrame?>(new ImageSequenceFrame(64, 48, "png", "rgb24"))
                };
                try { return govde(window); }
                finally { window.Close(); }
            });
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }

    private static async Task KareUret(string klasor, int adet)
    {
        var uret = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=64x48:rate=10",
            "-frames:v", adet.ToString(CultureInfo.InvariantCulture), "-threads", "2", Path.Combine(klasor, "kare_%04d.png")
        });
        Assert.True(uret.Ok, uret.StandardError);
    }

    private static async Task Kos(IReadOnlyList<string> args, string? klasor = null)
    {
        if (klasor is not null) Directory.CreateDirectory(klasor);
        var liste = args.ToList();
        liste.InsertRange(liste.Count - 1, new[] { "-threads", "2" });
        liste.Insert(2, "-nostdin");
        var sonuc = await FfmpegRunner.RunAsync(liste);
        Assert.True(sonuc.Ok, sonuc.StandardError);
    }

    private static (int Paket, double Sure) PaketVeSure(string dosya)
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
                     "-hide_banner", "-v", "error", "-count_packets", "-select_streams", "v:0",
                     "-show_entries", "stream=nb_read_packets:format=duration", "-of", "default=nw=1", dosya
                 })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(10_000);
        _ = hata.Result;

        int paket = 0;
        double sure = 0;
        foreach (var satir in metin.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (satir.StartsWith("nb_read_packets=", StringComparison.Ordinal)) paket = int.Parse(satir[16..], CultureInfo.InvariantCulture);
            if (satir.StartsWith("duration=", StringComparison.Ordinal)) sure = double.Parse(satir[9..], CultureInfo.InvariantCulture);
        }
        return (paket, sure);
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
