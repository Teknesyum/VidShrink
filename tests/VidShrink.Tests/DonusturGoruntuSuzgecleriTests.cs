using System.Globalization;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de görüntü süzgeçleri (DON-P10): kırpma, döndürme, tarama çözme ve gürültü azaltma Küçült'ün
/// süzgeç seçeneklerinden (<see cref="VideoFilterOptions"/>) ve aynı zincirden yazılır; sabit ya da değişken
/// kare hızı, ölçekleme algoritması ve siyah bantla dolgu da aynı yerden gelir. Kopya kipinde süzgeç seçilince
/// görüntü yeniden kodlanır.
/// </summary>
public sealed class DonusturGoruntuSuzgecleriTests : IDisposable
{
    private static readonly SourceStream Aac = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000, SampleRate: 48000);

    public void Dispose() => OtomatikKirpma.Tespit = CropProbe.RunAsync;

    private static MediaInfo Ornek() => Kaynak(62, Video, Aac) with { FilePath = @"C:\Kayitlar\gezi.mkv", Fps = 30 };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container).ToList();

    private static string Vf(IReadOnlyList<string> args) => Sonraki(args, "-vf");

    /// <summary>Zincir sırası Küçült'ünkiyle aynı: tarama çözme, kırpma, gürültü, döndürme, sonra ölçek.</summary>
    [Fact]
    public void ZincirSirasiKucultIleAyni()
    {
        var plan = new ConversionPlan
        {
            Container = "mp4",
            Height = 720,
            Filters = ConversionPlan.DefaultFilters with
            {
                Deinterlace = DeinterlaceMode.On,
                Crop = new CropRect(1920, 800, 0, 140),
                Denoise = DenoiseFilter.Hqdn3d,
                Transpose = TransposeMode.Clockwise
            }
        };

        Assert.Equal(
            VideoFilterChain.DeinterlaceChain + ",crop=1920:800:0:140,hqdn3d=3:2:2:3,transpose=clock,scale=-2:720:flags=lanczos",
            Vf(Kur(Ornek(), plan)));
    }

    /// <summary>Varsayılan plan bugünkü komutu yazar: tarama çözme Dönüştür'de kapalı başlar, geçmeli kaynakta da.</summary>
    [Fact]
    public void VarsayilanPlanSuzgecYazmaz()
    {
        var info = Ornek() with { IsInterlaced = true };
        var plan = new ConversionPlan { Container = "mp4" };

        Assert.Equal(DeinterlaceMode.Off, plan.Filters.Deinterlace);
        Assert.DoesNotContain("-vf", Kur(info, plan));
        Assert.Contains(VideoFilterChain.DeinterlaceChain,
            Vf(Kur(info, plan with { Filters = plan.Filters with { Deinterlace = DeinterlaceMode.Auto } })));
    }

    /// <summary>Metin altyazı yakma döndürmeden sonra, ölçekten önce oturur.</summary>
    [Fact]
    public void YakmaDondurmedenSonraOlcektenOnce()
    {
        var info = Kaynak(62, Video, Aac, new SourceStream(2, StreamKind.Subtitle, "subrip", "tur")) with { FilePath = @"C:\Kayitlar\gezi.mkv" };
        var plan = new ConversionPlan
        {
            Container = "mp4",
            Height = 720,
            BurnSubtitle = 0,
            Filters = ConversionPlan.DefaultFilters with { Transpose = TransposeMode.FlipHorizontal }
        };

        var vf = Vf(Kur(info, plan));
        var flip = vf.IndexOf("hflip", StringComparison.Ordinal);
        var burn = vf.IndexOf("subtitles=", StringComparison.Ordinal);
        var scale = vf.IndexOf("scale=-2:720", StringComparison.Ordinal);
        Assert.True(flip >= 0 && flip < burn && burn < scale, vf);
    }

    /// <summary>Resim altyazı yakmada kırpma bindirmeden önceki dala girer, tuval kırpılmış genişliğe ölçeklenir.</summary>
    [Fact]
    public void ResimYakmadaKirpmaBindirmedenOnce()
    {
        var info = Kaynak(62, Video, Aac, new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "tur")) with { FilePath = @"C:\Kayitlar\gezi.mkv" };
        var plan = new ConversionPlan
        {
            Container = "mkv",
            BurnSubtitle = 0,
            Filters = ConversionPlan.DefaultFilters with { Crop = new CropRect(1920, 800, 0, 140) }
        };

        var graph = Sonraki(Kur(info, plan), "-filter_complex");
        Assert.StartsWith("[0:0]crop=1920:800:0:140[b];[0:2]scale=1920:-1[s];[b][s]overlay=", graph);
    }

    /// <summary>Kopya kipinde süzgeç seçilince görüntü kabın kodlayıcısıyla yeniden kodlanır ve not düşülür.</summary>
    [Theory]
    [InlineData("mp4", "libx264")]
    [InlineData("mkv", "libx264")]
    [InlineData("mov", "libx264")]
    [InlineData("avi", "libx264")]
    [InlineData("webm", "libvpx-vp9")]
    [InlineData("mxf", IntermediateCodecs.DnxHr)]
    public void KopyadaSuzgecYenidenKodlamayaGecer(string kap, string kodlayici)
    {
        var info = Ornek() with { VideoCodec = kap is "webm" ? "vp9" : kap is "mxf" ? "dnxhd" : "h264" };
        var kopya = new ConversionPlan { Container = kap, VideoCodec = "copy", AudioCodec = kap switch { "mxf" => "pcm_s16le", "webm" => "libopus", _ => "copy" } };
        var suzgecli = kopya with { Filters = kopya.Filters with { Transpose = TransposeMode.CounterClockwise } };

        Assert.Equal("copy", Sonraki(Kur(info, kopya), "-c:v"));
        Assert.DoesNotContain(ConversionNote.FiltersReencode, ConversionArguments.Notes(kopya, info));

        Assert.Empty(ConversionArguments.Validate(info, suzgecli));
        var args = Kur(info, suzgecli);
        Assert.Equal(kodlayici, Sonraki(args, "-c:v"));
        Assert.Contains("transpose=cclock", Vf(args));
        Assert.Contains(ConversionNote.FiltersReencode, ConversionArguments.Notes(suzgecli, info));
        if (kap == "mxf") Assert.Equal("dnxhr_hq", Sonraki(args, "-profile:v"));
    }

    /// <summary>Kopyada kare hızı kipi ve dolgu da yeniden kodlamaya geçirir; ölçekleme algoritması tek başına geçirmez.</summary>
    [Fact]
    public void KopyadaKipVeDolguYenidenKodlar()
    {
        var info = Ornek();
        var kopya = new ConversionPlan { Container = "mkv", VideoCodec = "copy" };

        Assert.Equal("libx264", Sonraki(Kur(info, kopya with { FrameRate = FrameRateMode.Constant }), "-c:v"));
        Assert.Equal("copy", Sonraki(Kur(info, kopya with { Filters = kopya.Filters with { Scaler = ScaleAlgorithm.Bicubic } }), "-c:v"));
        Assert.Equal("copy", Sonraki(Kur(info, kopya with { Letterbox = true }), "-c:v"));
    }

    /// <summary>Ölçekleme algoritması iki yolda da zincirin alanından okunur.</summary>
    [Theory]
    [InlineData(ScaleAlgorithm.Lanczos, "lanczos")]
    [InlineData(ScaleAlgorithm.Bicubic, "bicubic")]
    [InlineData(ScaleAlgorithm.Bilinear, "bilinear")]
    [InlineData(ScaleAlgorithm.Spline, "spline")]
    [InlineData(ScaleAlgorithm.Neighbor, "neighbor")]
    public void OlceklemeAlgoritmasiIkiYoldaOkunur(ScaleAlgorithm algoritma, string bayrak)
    {
        var info = Ornek();
        var filters = ConversionPlan.DefaultFilters with { Scaler = algoritma };

        Assert.Equal("scale=1280:720:flags=" + bayrak, Vf(Kur(info, new ConversionPlan { Container = "mp4", Width = 1280, Height = 720, Filters = filters })));
        Assert.Equal("scale=-2:480:flags=" + bayrak, Vf(Kur(info, new ConversionPlan { Container = "mp4", Height = 480, Filters = filters })));

        var kucult = new EncodePlan { Width = 1280, Height = 720, Fps = info.Fps, Filters = VideoFilterOptions.Default with { Scaler = algoritma } };
        Assert.Contains("scale=1280:720:flags=" + bayrak, VideoFilterChain.Filters(info, kucult));
    }

    /// <summary>Belirtim ölçekleme algoritmasını taşır; varsayılan yazılmaz.</summary>
    [Fact]
    public void BelirtimOlceklemeyiTasir()
    {
        var options = VideoFilterOptions.Default with { Scaler = ScaleAlgorithm.Spline };

        Assert.Equal("scaler=spline", VideoFilterChain.Format(options));
        Assert.Equal(options, VideoFilterChain.Parse(VideoFilterChain.Format(options)));
        Assert.Equal("", VideoFilterChain.Format(VideoFilterOptions.Default));
        Assert.Throws<ArgumentException>(() => VideoFilterChain.Parse("scaler=gauss2"));
    }

    /// <summary>Dolgu en-boyu koruyarak hedef çerçeveye sığdırır ve farkı siyah bantla doldurur; tek boyutta okunmaz.</summary>
    [Fact]
    public void DolguHedefCerceveyeSigdirir()
    {
        var info = Ornek();
        var plan = new ConversionPlan { Container = "mp4", Width = 1080, Height = 1920, Letterbox = true };

        Assert.Equal(
            "scale=1080:1920:force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1",
            Vf(Kur(info, plan)));
        Assert.Equal("scale=-2:720:flags=lanczos", Vf(Kur(info, plan with { Width = null, Height = 720 })));
    }

    /// <summary>Anamorfik kaynakta dolgu önce kare piksele çevirir; yoksa sığdırma depolama oranını okur.</summary>
    [Fact]
    public void AnamorfikDolguOnceKarePiksel()
    {
        var info = Ornek() with { Width = 720, Height = 576, ParNum = 16, ParDen = 15 };
        var plan = new ConversionPlan { Container = "mp4", Width = 1280, Height = 720, Letterbox = true };

        Assert.StartsWith("scale=trunc(iw*sar/2)*2:ih:flags=lanczos,setsar=1,scale=1280:720:force_original_aspect_ratio=decrease", Vf(Kur(info, plan)));
    }

    /// <summary>Sabit kip <c>-r</c> ve <c>cfr</c> yazar; hız seçilmemişse kaynağınki.</summary>
    [Fact]
    public void SabitKipCfrYazar()
    {
        var info = Ornek();
        var args = Kur(info, new ConversionPlan { Container = "mp4", FrameRate = FrameRateMode.Constant });
        Assert.Equal("30", Sonraki(args, "-r"));
        Assert.Equal("cfr", Sonraki(args, "-fps_mode"));

        args = Kur(info, new ConversionPlan { Container = "mp4", Fps = 24, FrameRate = FrameRateMode.Constant });
        Assert.Equal("24", Sonraki(args, "-r"));
        Assert.Contains("fps=24", Vf(args));
    }

    /// <summary>Değişken kip tavanı zaman tabanıyla yazar ve <c>fps</c> süzgecini bırakır (Küçült'ün tavanlı kipi).</summary>
    [Fact]
    public void DegiskenKipVfrYazar()
    {
        var info = Ornek();
        var args = Kur(info, new ConversionPlan { Container = "mp4", Fps = 24, FrameRate = FrameRateMode.Peak });

        Assert.Equal("1/24", Sonraki(args, "-enc_time_base"));
        Assert.Equal("vfr", Sonraki(args, "-fps_mode"));
        Assert.DoesNotContain("-vf", args);
        Assert.DoesNotContain("-r", args);

        Assert.Equal("1/30", Sonraki(Kur(info, new ConversionPlan { Container = "mp4", FrameRate = FrameRateMode.Peak }), "-enc_time_base"));
        Assert.DoesNotContain("-fps_mode", Kur(info, new ConversionPlan { Container = "mp4" }));
    }

    /// <summary>Çerçeveden taşan kırpma reddedilir; süzgeçler ses kabında okunmaz.</summary>
    [Fact]
    public void TasanKirpmaReddedilir()
    {
        var info = Ornek();
        var plan = new ConversionPlan { Container = "mp4", Filters = ConversionPlan.DefaultFilters with { Crop = new CropRect(1920, 800, 0, 400) } };

        Assert.Contains("crop: rectangle leaves the source frame", ConversionArguments.Validate(info, plan));
        Assert.Empty(ConversionArguments.Validate(info, plan with { Container = "m4a", AudioCodec = "aac" }));
    }

    /// <summary>GIF ve hareketli görsel de aynı zinciri okur.</summary>
    [Fact]
    public void GifZinciriOkur()
    {
        var info = Ornek();
        var plan = new ConversionPlan { Container = "gif", Height = 240, Filters = ConversionPlan.DefaultFilters with { Transpose = TransposeMode.UpsideDown } };

        Assert.StartsWith("hflip,vflip,scale=-2:240:flags=lanczos,palettegen", Vf(Kur(info, plan)));
    }

    /// <summary>
    /// Gerçek ffmpeg: 320x240 kaynak döndürülüp 320x240 çerçeveye sığdırılır, bantla doldurulur ve sabit 15 kare
    /// yazılır. Çıktı çerçevesi, ortadaki görüntü genişliği ve kare hızı ffprobe ile okunur.
    /// </summary>
    [FfmpegFact]
    public async Task GercekKomutCerceveyiVeHiziTutar()
    {
        var klasor = Path.Combine(Folder, "don-p10");
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var cikti = Path.Combine(klasor, "cikti.mp4");
        try
        {
            await Kos(new[]
            {
                "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", kaynak
            });
            var info = await FfprobeClient.ProbeAsync(kaynak);
            var plan = new ConversionPlan
            {
                Container = "mp4",
                Width = 320,
                Height = 240,
                Letterbox = true,
                Fps = 15,
                FrameRate = FrameRateMode.Constant,
                Filters = ConversionPlan.DefaultFilters with { Transpose = TransposeMode.Clockwise, Scaler = ScaleAlgorithm.Bicubic }
            };
            await Kos(ConversionArguments.Build(info, plan, cikti).ToList());

            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal(320, sonuc.Width);
            Assert.Equal(240, sonuc.Height);
            Assert.Equal(15, sonuc.Fps, 2);
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Pencere kolu: Dönüştür'ün süzgeç kutuları plana ve komuta girer, kopya seçiliyken komut yeniden kodlar;
    /// otomatik kırpma sahte yoklamanın dikdörtgenini yazar, dolgu yalnız özel boyutta açıktır.
    /// </summary>
    [Fact]
    public void PanelSecimleriPlanaVeKomutaGirer()
    {
        var bulunan = new CropRect(1920, 800, 0, 140);
        OtomatikKirpma.Tespit = (_, _) => Task.FromResult(new CropDetection(bulunan, Array.Empty<CropRect>(), TimeSpan.Zero));
        var info = Ornek();
        var sonuc = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "mkv");
            window.CmbConvertCodec.SelectedItem = Oge(window.CmbConvertCodec, "copy");
            var bos = window.CurrentConversionPlan();
            var dolguKapali = window.ChkConvertLetterbox.IsEnabled;
            var once = window.TxtConvertCommand.Text ?? "";
            window.CmbConvertDeinterlace.SelectedItem = Oge(window.CmbConvertDeinterlace, "on");
            window.CmbConvertDenoise.SelectedItem = Oge(window.CmbConvertDenoise, "strong");
            window.CmbConvertRotate.SelectedItem = Oge(window.CmbConvertRotate, "cclock");
            window.CmbConvertScaler.SelectedItem = Oge(window.CmbConvertScaler, "bicubic");
            window.CmbConvertFpsMode.SelectedItem = Oge(window.CmbConvertFpsMode, "constant");
            window.ChkConvertAutoCrop.IsChecked = true;
            window.CmbResolution.SelectedItem = Oge(window.CmbResolution, "custom");
            window.TxtCustomResolution.Text = "1280x720";
            var dolguAcik = window.ChkConvertLetterbox.IsEnabled;
            window.ChkConvertLetterbox.IsChecked = true;
            var plan = window.CurrentConversionPlan();
            var sonra = window.TxtConvertCommand.Text ?? "";
            var not = window.TxtConvertValidation.Text ?? "";
            return (bos, dolguKapali, once, dolguAcik, plan, sonra, not);
        });

        Assert.Equal(ConversionPlan.DefaultFilters, sonuc.bos.Filters);
        Assert.False(sonuc.bos.Letterbox);
        Assert.Equal(FrameRateMode.Auto, sonuc.bos.FrameRate);
        Assert.False(sonuc.dolguKapali);
        Assert.Contains("-c:v copy", sonuc.once);
        Assert.True(sonuc.dolguAcik);
        Assert.Equal(DeinterlaceMode.On, sonuc.plan.Filters.Deinterlace);
        Assert.Equal(DenoiseFilter.Hqdn3d, sonuc.plan.Filters.Denoise);
        Assert.Equal(FilterStrength.Strong, sonuc.plan.Filters.DenoiseStrength);
        Assert.Equal(TransposeMode.CounterClockwise, sonuc.plan.Filters.Transpose);
        Assert.Equal(ScaleAlgorithm.Bicubic, sonuc.plan.Filters.Scaler);
        Assert.Equal(bulunan, sonuc.plan.Filters.Crop);
        Assert.Equal(FrameRateMode.Constant, sonuc.plan.FrameRate);
        Assert.True(sonuc.plan.Letterbox);
        Assert.Contains("-c:v libx264", sonuc.sonra);
        Assert.Contains("crop=1920:800:0:140,hqdn3d=7:7:5:5,transpose=cclock,scale=1280:720:force_original_aspect_ratio=decrease", sonuc.sonra);
        Assert.Contains("flags=bicubic", sonuc.sonra);
        Assert.Contains("-fps_mode cfr", sonuc.sonra);
        Assert.Contains(Locales.Domain("tr", "main")["main.convert.note.filters-reencode"], sonuc.not);
    }

    private static readonly string[] Anahtarlar =
    {
        "main.convert.scaler", "main.convert.fps-mode", "main.convert.fps-mode.constant", "main.convert.fps-mode.variable",
        "main.convert.letterbox", "main.convert.note.filters-reencode"
    };

    [Fact]
    public void AltiAnahtarKirkIkiDildeCevrili()
    {
        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            foreach (var key in Anahtarlar)
            {
                if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language} {key}: yok");
                else if (language != "en" && value == english[key] && key is not ("main.convert.fps-mode.constant" or "main.convert.fps-mode.variable"))
                    eksik.Add($"{language} {key}: cevrilmemis");
            }
        }
        Assert.Empty(eksik);
    }

    private static async Task Kos(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        liste.InsertRange(liste.Count - 1, new[] { "-threads", "2" });
        if (!liste.Contains("-nostdin")) liste.Insert(0, "-nostdin");
        var sonuc = await FfmpegRunner.RunAsync(liste);
        Assert.True(sonuc.Ok, sonuc.StandardError);
    }

    private static ComboBoxItem Oge(ComboBox kutu, string etiket)
        => kutu.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-goruntu-suzgecleri", Guid.NewGuid().ToString("N"));
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
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }
}
