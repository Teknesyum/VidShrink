using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'ün kodlayıcı, HDR ve renk seçimleri (DON-P11): hız ön ayarı, tune, profil, seviye ve bit hızı
/// kipinde iki geçiş (DON-15); HDR seçimi Dönüştür'ün kendi kutusundan (DON-25); BT.601, BT.709, BT.2020
/// matrisi ve sınırlı/tam aralık (DON-26). Değer kodeğin listesinde değilse plan reddedilir.
/// </summary>
public sealed class DonusturKodlayiciTests
{
    private static readonly SourceStream Aac = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000, SampleRate: 48000);

    private static MediaInfo Ornek() => Kaynak(62, Video, Aac) with
    {
        FilePath = @"C:\Kayitlar\gezi.mkv", Fps = 30, ColorSpace = "smpte170m", ColorRange = "tv"
    };

    private static MediaInfo Hdr() => Ornek() with
    {
        IsHdr = true, ColorSpace = "bt2020nc", ColorPrimaries = "bt2020", ColorTransfer = "smpte2084", VideoCodec = "hevc"
    };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan, int pass = 0)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container, pass: pass, passLogPrefix: pass > 0 ? "gunluk" : null).ToList();

    private static bool Ardisik(IReadOnlyList<string> args, params string[] parca)
    {
        for (var index = 0; index + parca.Length <= args.Count; index++)
            if (parca.Select((deger, kayma) => args[index + kayma] == deger).All(esit => esit)) return true;
        return false;
    }

    /// <summary>Seçim yapılmayan plan bugünkü komutu yazar: kodeğin varsayılan ön ayarı, tune, profil, seviye ve geçiş yok.</summary>
    [Fact]
    public void VarsayilanPlanBugunkuKomutuYazar()
    {
        var args = Kur(Ornek(), new ConversionPlan { Container = "mp4", QualityMode = ConversionQualityMode.Bitrate, TwoPass = false });

        Assert.True(Ardisik(args, "-preset", FfmpegArguments.DefaultPreset("libx264")));
        foreach (var bayrak in new[] { "-tune", "-profile:v", "-level:v", "-pass", "-passlogfile", "-color_range", "-colorspace" })
            Assert.DoesNotContain(bayrak, args);
        Assert.DoesNotContain("-vf", args);
    }

    /// <summary>x264'te seçilen ön ayar, tune, profil ve seviye komuta girer.</summary>
    [Fact]
    public void OnAyarTuneProfilSeviyeKomutaGirer()
    {
        var plan = new ConversionPlan { Container = "mp4", EncoderPreset = "veryfast", Tune = "film", CodecProfile = "high", Level = "4.1" };
        var args = Kur(Ornek(), plan);

        Assert.True(Ardisik(args, "-preset", "veryfast"));
        Assert.True(Ardisik(args, "-tune", "film"));
        Assert.True(Ardisik(args, "-profile:v", "high"));
        Assert.True(Ardisik(args, "-level:v", "4.1"));
        Assert.DoesNotContain(FfmpegArguments.DefaultPreset("libx264"), args);
    }

    /// <summary>VP9'un hız merdiveni <c>-cpu-used</c>'a iner; x265 tune alır, profil ve seviye listesi yok.</summary>
    [Fact]
    public void KodekListeleriKodlayiciyaGore()
    {
        Assert.True(Ardisik(Kur(Ornek(), new ConversionPlan { Container = "webm", VideoCodec = "libvpx-vp9", AudioCodec = "libopus", EncoderPreset = "4" }), "-cpu-used", "4"));
        Assert.Equal(new[] { "animation", "grain" }, ConversionArguments.TuneOptions("libx265"));
        Assert.Empty(ConversionArguments.TuneOptions("libsvtav1"));
        Assert.Empty(ConversionArguments.ProfileOptions("libx265"));
        Assert.Empty(ConversionArguments.LevelOptions("libvpx-vp9"));
        Assert.Equal(new[] { "baseline", "main", "high" }, ConversionArguments.ProfileOptions("h264_nvenc"));
        Assert.Contains("5.2", ConversionArguments.LevelOptions("libx264"));
        Assert.True(ConversionArguments.TwoPassAvailable("libvpx-vp9"));
        Assert.False(ConversionArguments.TwoPassAvailable("h264_nvenc"));
    }

    /// <summary>Kodeğin listesinde olmayan değer doğrulamadan geçmez ve komut kurulmaz.</summary>
    [Fact]
    public void ListeDisiDegerReddedilir()
    {
        var info = Ornek();
        var yanlis = new[]
        {
            new ConversionPlan { Container = "mp4", EncoderPreset = "hizli" },
            new ConversionPlan { Container = "mp4", Tune = "zerolatency" },
            new ConversionPlan { Container = "mkv", VideoCodec = "libx265", CodecProfile = "high" },
            new ConversionPlan { Container = "mp4", Level = "6.2" }
        };
        foreach (var plan in yanlis)
        {
            Assert.Contains(ConversionArguments.Validate(info, plan), hata => hata.Contains("setting", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => Kur(info, plan));
        }
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mkv", VideoCodec = "libx265", Tune = "grain" }));
    }

    /// <summary>
    /// İki geçiş yalnız bit hızı kipinde ve destekleyen kodekte: ilk geçiş sessiz ve boş çıktıya yazar,
    /// ikinci geçiş sesi ve gerçek çıktıyı taşır, ikisi aynı günlüğü okur.
    /// </summary>
    [Fact]
    public void IkiGecisYalnizBitHizindaVeDestekleyenKodekte()
    {
        var info = Ornek();
        var plan = new ConversionPlan { Container = "mp4", QualityMode = ConversionQualityMode.Bitrate, VideoBitrateK = 1800, TwoPass = true };

        Assert.True(ConversionArguments.UsesTwoPass(info, plan));
        var ilk = Kur(info, plan, 1);
        var ikinci = Kur(info, plan, 2);
        Assert.True(Ardisik(ilk, "-pass", "1", "-passlogfile", "gunluk"));
        Assert.True(Ardisik(ilk, "-an", "-sn", "-dn", "-f", "null"));
        Assert.DoesNotContain("-c:a", ilk);
        Assert.True(Ardisik(ikinci, "-pass", "2", "-passlogfile", "gunluk"));
        Assert.True(Ardisik(ikinci, "-c:a", "aac"));
        Assert.Equal("cikti.mp4", ikinci[^1]);

        Assert.False(ConversionArguments.UsesTwoPass(info, plan with { QualityMode = ConversionQualityMode.Crf }));
        Assert.False(ConversionArguments.UsesTwoPass(info, plan with { VideoCodec = "h264_nvenc" }));
        Assert.DoesNotContain("-pass", Kur(info, plan with { QualityMode = ConversionQualityMode.Crf }, 1));
        Assert.DoesNotContain("-pass", Kur(info, plan));
    }

    /// <summary>BT.601 sınırlı kaynaktan BT.2020 tam aralığa: zscale kaynağın matrisini ve aralığını okur, etiketler hedefi yazar.</summary>
    [Fact]
    public void Bt2020TamAralikSuzgeciVeEtiketleri()
    {
        var plan = new ConversionPlan
        {
            Container = "mp4",
            ColorRange = ColorRangeTarget.Full,
            Filters = ConversionPlan.DefaultFilters with { ColorMatrix = ColorMatrixTarget.Bt2020 }
        };
        var args = Kur(Ornek(), plan);

        Assert.Equal("zscale=min=170m:tin=601:pin=170m:rin=limited:m=2020_ncl:t=2020_10:p=2020:r=full:filter=lanczos", Sonraki(args, "-vf"));
        Assert.True(Ardisik(args, "-colorspace", "bt2020nc", "-color_primaries", "bt2020", "-color_trc", "bt2020-10"));
        Assert.True(Ardisik(args, "-color_range", "pc"));
    }

    /// <summary>BT.709 ve BT.601 kolu Küçült'ün hedef tablosundan; kaynak zaten hedefteyse süzgeç yazılmaz, etiket yazılır.</summary>
    [Fact]
    public void Bt709Bt601VeAyniHedef()
    {
        var info = Ornek();
        var plan709 = new ConversionPlan { Container = "mp4", Filters = ConversionPlan.DefaultFilters with { ColorMatrix = ColorMatrixTarget.Bt709 } };
        Assert.Equal("zscale=min=170m:tin=601:pin=170m:rin=limited:m=709:t=709:p=709:r=limited:filter=lanczos", Sonraki(Kur(info, plan709), "-vf"));

        var ayni = Kur(info, plan709 with { Filters = plan709.Filters with { ColorMatrix = ColorMatrixTarget.Bt601 } });
        Assert.DoesNotContain("-vf", ayni);
        Assert.True(Ardisik(ayni, "-colorspace", "smpte170m"));

        var tamKaynak = info with { ColorRange = "pc" };
        var sinirli = Kur(tamKaynak, new ConversionPlan { Container = "mp4", ColorRange = ColorRangeTarget.Limited });
        Assert.Equal("zscale=min=170m:tin=601:pin=170m:rin=full:m=170m:t=601:p=170m:r=limited:filter=lanczos", Sonraki(sinirli, "-vf"));
        Assert.True(Ardisik(sinirli, "-color_range", "tv"));
    }

    /// <summary>Akış kopyasında aralık seçimi görüntüyü yeniden kodlatır.</summary>
    [Fact]
    public void KopyadaAralikYenidenKodlatir()
    {
        var args = Kur(Ornek(), new ConversionPlan { Container = "mkv", VideoCodec = "copy", ColorRange = ColorRangeTarget.Full });
        Assert.True(Ardisik(args, "-c:v", "libx264"));
        Assert.Contains("r=full", Sonraki(args, "-vf"));
    }

    /// <summary>HDR kaynakta renk kolu yazılmaz: etiketleri ve ton eşlemeyi <see cref="HdrResolver"/> yazar.</summary>
    [Fact]
    public void HdrKaynaktaRenkKoluYazilmaz()
    {
        var plan = new ConversionPlan
        {
            Container = "mkv",
            VideoCodec = "libx265",
            HdrPolicy = HdrPolicy.TonemapToSdr,
            ColorRange = ColorRangeTarget.Full,
            Filters = ConversionPlan.DefaultFilters with { ColorMatrix = ColorMatrixTarget.Bt2020 }
        };
        var args = Kur(Hdr(), plan);

        Assert.Equal(HdrResolver.TonemapFilter, Sonraki(args, "-vf"));
        Assert.DoesNotContain("-color_range", args);
        Assert.True(Ardisik(args, "-colorspace", "bt709"));
    }

    /// <summary>Küçült'ün süzgeç metni BT.2020'yi taşır; Küçült zinciri aynı hedef tablosunu kullanır.</summary>
    [Fact]
    public void KucultSuzgecMetniBt2020Tasir()
    {
        var options = VideoFilterOptions.Default with { ColorMatrix = ColorMatrixTarget.Bt2020 };
        var metin = VideoFilterChain.Format(options);
        Assert.Contains("colorspace=bt2020", metin);
        Assert.Equal(ColorMatrixTarget.Bt2020, VideoFilterChain.Parse(metin).ColorMatrix);
        Assert.Equal("zscale=min=170m:tin=601:pin=170m:m=2020_ncl:t=2020_10:p=2020:filter=lanczos", VideoFilterChain.ColorMatrixFilter(Ornek(), ColorMatrixTarget.Bt2020));
        Assert.Null(VideoFilterChain.ColorMatrixFilter(Ornek() with { ColorSpace = "bt2020nc" }, ColorMatrixTarget.Bt2020));
    }

    /// <summary>
    /// Gerçek ffmpeg, iki geçiş ve renk kolu birlikte: BT.601 sınırlı 320x240 kaynak <see cref="EncodeRunner.ConvertAsync"/>
    /// ile BT.2020 tam aralığa iki geçişte çevrilir. Çıktının etiketleri ffprobe ile okunur, geçiş günlüğü geride kalmaz.
    /// </summary>
    [FfmpegFact]
    public async Task GercekIkiGecisRenkEtiketleriniYazar()
    {
        var klasor = Path.Combine(Folder, "don-p11");
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var cikti = Path.Combine(klasor, "cikti.mp4");
        var onceki = Directory.EnumerateFiles(Path.GetTempPath(), "vidshrink_pass_*").ToHashSet();
        try
        {
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-nostdin", "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-colorspace", "smpte170m",
                "-color_primaries", "smpte170m", "-color_trc", "smpte170m", "-color_range", "tv", "-threads", "2", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var info = await FfprobeClient.ProbeAsync(kaynak);
            var plan = new ConversionPlan
            {
                Container = "mp4",
                AudioCodec = null,
                QualityMode = ConversionQualityMode.Bitrate,
                VideoBitrateK = 300,
                TwoPass = true,
                EncoderPreset = "ultrafast",
                CodecProfile = "high",
                ColorRange = ColorRangeTarget.Full,
                Filters = ConversionPlan.DefaultFilters with { ColorMatrix = ColorMatrixTarget.Bt2020 },
                ExtraArgs = new[] { "-threads", "2" }
            };
            Assert.True(ConversionArguments.UsesTwoPass(info, plan));

            await new EncodeRunner().ConvertAsync(info, plan, cikti, null);

            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal("bt2020nc", sonuc.ColorSpace);
            Assert.Equal("bt2020", sonuc.ColorPrimaries);
            Assert.Equal("pc", sonuc.ColorRange);
            Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetTempPath(), "vidshrink_pass_*"), dosya => !onceki.Contains(dosya));
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Matris değişince zscale kroma düzlemini 4:4:4'e açıp yeniden 4:2:0'a indirir; varsayılan bilinear süzgeç keskin
    /// renk kenarında RGB PSNR'ı düşürür (<c>docs/olcumler/donustur-renk-kolu.md</c>). Kayıpsız kodlamayla kodlayıcı payı
    /// dışarıda kalır: BT.601'den BT.709'a çevrilen kare, kaynağa ortak RGB'de 33.5 dB'den yakın kalmalı.
    /// </summary>
    [TonemapFact]
    public async Task MatrisCevirisiKromaKenariniKorur()
    {
        var klasor = Path.Combine(Folder, "don-renk-kroma");
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var cikti = Path.Combine(klasor, "cikti.mp4");
        try
        {
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-nostdin", "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=24:d=0.25",
                "-c:v", "libx264", "-qp", "0", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-colorspace", "smpte170m",
                "-color_range", "tv", "-threads", "2", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var info = await FfprobeClient.ProbeAsync(kaynak);
            var plan = new ConversionPlan
            {
                Container = "mp4",
                AudioCodec = null,
                EncoderPreset = "ultrafast",
                Crf = 0,
                Filters = ConversionPlan.DefaultFilters with { ColorMatrix = ColorMatrixTarget.Bt709 },
                ExtraArgs = new[] { "-qp", "0", "-threads", "2" }
            };
            await new EncodeRunner().ConvertAsync(info, plan, cikti, null);
            Assert.Equal("bt709", (await FfprobeClient.ProbeAsync(cikti)).ColorSpace);

            var olcum = await FfmpegRunner.RunAsync(new[]
            {
                "-nostdin", "-hide_banner", "-threads", "2", "-i", kaynak, "-i", cikti, "-lavfi",
                "[0]zscale=min=170m:tin=601:pin=170m:rin=limited:t=709:p=709,format=gbrp[a];"
                + "[1]zscale=min=709:tin=709:pin=709:rin=limited:t=709:p=709,format=gbrp[b];[a][b]psnr",
                "-f", "null", "-"
            });
            Assert.True(olcum.Ok, olcum.StandardError);
            var eslesme = System.Text.RegularExpressions.Regex.Match(olcum.StandardError, @"average:([0-9.]+)");
            Assert.True(eslesme.Success, olcum.StandardError);
            var psnr = double.Parse(eslesme.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(psnr > 33.5, $"RGB PSNR {psnr:0.00} dB");
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Pencere kolu: kutular seçili kodeğin listesiyle dolar, seçimler plana ve komut önizlemesine girer, iki geçişte
    /// önizleme iki komut gösterir. HDR kaynakta Dönüştür'ün kendi HDR kutusu okunur, Küçült'ünki değil; renk kutuları gizlenir.
    /// </summary>
    [Fact]
    public void PanelSecimleriPlanaVeKomutaGirer()
    {
        var info = Ornek();
        var hdr = Hdr() with { FilePath = @"C:\Kayitlar\hdr.mkv" };
        var sonuc = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "mp4");
            window.CmbConvertCodec.SelectedItem = Oge(window.CmbConvertCodec, "libx264");
            var onAyarlar = window.CmbConvertSpeed.Items.OfType<ComboBoxItem>().Select(item => (string?)item.Tag).ToList();
            var ikiGecisKapali = window.ChkConvertTwoPass.IsVisible;
            window.CmbConvertSpeed.SelectedItem = Oge(window.CmbConvertSpeed, "slow");
            window.CmbConvertTune.SelectedItem = Oge(window.CmbConvertTune, "grain");
            window.CmbConvertCodecProfile.SelectedItem = Oge(window.CmbConvertCodecProfile, "main");
            window.CmbConvertLevel.SelectedItem = Oge(window.CmbConvertLevel, "4.0");
            window.CmbQualityMode.SelectedIndex = 1;
            var ikiGecisAcik = window.ChkConvertTwoPass.IsVisible;
            window.ChkConvertTwoPass.IsChecked = true;
            window.CmbConvertColorSpace.SelectedItem = Oge(window.CmbConvertColorSpace, "bt709");
            window.CmbConvertColorRange.SelectedItem = Oge(window.CmbConvertColorRange, "full");
            var plan = window.CurrentConversionPlan();
            var komut = window.TxtConvertCommand.Text ?? "";
            var hdrSatiriSdr = window.ConvertHdrRow.IsVisible;

            window.CmbConvertCodec.SelectedItem = Oge(window.CmbConvertCodec, "libx265");
            var x265 = window.CurrentConversionPlan();
            var profilGizli = !window.ConvertProfileRow.IsVisible;

            window.LoadWithoutProbing(hdr.FilePath, hdr);
            window.CmbConvertCodec.SelectedItem = Oge(window.CmbConvertCodec, "libx265");
            var hdrSatiri = window.ConvertHdrRow.IsVisible;
            var renkGizli = !window.ConvertColorRow.IsVisible && !window.ConvertRangeRow.IsVisible;
            window.RbConvertHdrSdr.IsChecked = true;
            var kucultSdr = window.RbHdrSdr.IsChecked == true;
            var hdrPlan = window.CurrentConversionPlan();
            return (onAyarlar, ikiGecisKapali, ikiGecisAcik, plan, komut, hdrSatiriSdr, x265, profilGizli, hdrSatiri, renkGizli, kucultSdr, hdrPlan);
        });

        Assert.Equal(new string?[] { "" }.Concat(FfmpegArguments.PresetLadder("libx264")), sonuc.onAyarlar);
        Assert.False(sonuc.ikiGecisKapali);
        Assert.True(sonuc.ikiGecisAcik);
        Assert.Equal("slow", sonuc.plan.EncoderPreset);
        Assert.Equal("grain", sonuc.plan.Tune);
        Assert.Equal("main", sonuc.plan.CodecProfile);
        Assert.Equal("4.0", sonuc.plan.Level);
        Assert.True(sonuc.plan.TwoPass);
        Assert.Equal(ColorMatrixTarget.Bt709, sonuc.plan.Filters.ColorMatrix);
        Assert.Equal(ColorRangeTarget.Full, sonuc.plan.ColorRange);
        Assert.Contains("-pass 1", sonuc.komut);
        Assert.Contains("-pass 2", sonuc.komut);
        Assert.Contains("-preset slow -tune grain -profile:v main -level:v 4.0", sonuc.komut);
        Assert.Contains("r=full", sonuc.komut);
        Assert.False(sonuc.hdrSatiriSdr);
        Assert.Null(sonuc.x265.EncoderPreset);
        Assert.Null(sonuc.x265.CodecProfile);
        Assert.True(sonuc.profilGizli);
        Assert.True(sonuc.hdrSatiri);
        Assert.True(sonuc.renkGizli);
        Assert.False(sonuc.kucultSdr);
        Assert.Equal(HdrPolicy.TonemapToSdr, sonuc.hdrPlan.HdrPolicy);
        Assert.Equal(ColorMatrixTarget.Keep, sonuc.hdrPlan.Filters.ColorMatrix);
        Assert.Equal(ColorRangeTarget.Keep, sonuc.hdrPlan.ColorRange);
    }

    private static readonly string[] Anahtarlar =
    {
        "main.convert.encoder.label", "main.convert.encoder.profile", "main.convert.encoder.level",
        "main.convert.encoder.two-pass", "main.convert.encoder.two-pass.tip", "main.convert.color.range",
        "main.convert.color.range.limited", "main.convert.color.range.full", "main.validation.encoder-setting"
    };

    [Fact]
    public void DokuzAnahtarKirkIkiDildeCevrili()
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
                else if (language != "en" && value == english[key] && key is not ("main.convert.encoder.profile" or "main.convert.encoder.level"))
                    eksik.Add($"{language} {key}: cevrilmemis");
            }
            if (values.TryGetValue("main.validation.encoder-setting", out var ileti) && !(ileti.Contains("{0}") && ileti.Contains("{1}")))
                eksik.Add($"{language}: yer tutucu eksik");
        }
        Assert.Empty(eksik);
    }

    private static ComboBoxItem Oge(ComboBox kutu, string etiket)
        => kutu.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-kodlayici", Guid.NewGuid().ToString("N"));
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
