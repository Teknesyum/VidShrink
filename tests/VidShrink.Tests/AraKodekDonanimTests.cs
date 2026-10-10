using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// K12: ProRes ve DNxHR yalnız Dönüştür yolunda profil seçimiyle, VAAPI ve VideoToolbox donanım
/// ailesinde. Ölçüm <c>docs/olcumler/k12-ara-kodekler.md</c>.
/// </summary>
public sealed class AraKodekDonanimTests
{
    private static readonly MediaInfo Source = new()
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

    private static EncodePlan BitHiziPlani(string codec) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = 2000,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        Preset = "default",
        PixelFormat = CodecModel.OutputPixelFormat(codec, "yuv420p"),
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static List<string> Donustur(ConversionPlan plan)
        => ConversionArguments.Build(Source, plan, "out." + plan.Container).ToList();

    private static string Sonraki(IReadOnlyList<string> args, string flag)
    {
        var i = args.ToList().IndexOf(flag);
        Assert.True(i >= 0 && i + 1 < args.Count, $"{flag} yok: {string.Join(' ', args)}");
        return args[i + 1];
    }

    private sealed class SahteKodlayicilar(params string[] eksik) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => !eksik.Contains(name);
        public bool WorksAsEncoder(string codec) => !eksik.Contains(codec);
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    [Theory]
    [InlineData("h264_vaapi", EncoderVendor.Vaapi, HostPlatform.Linux)]
    [InlineData("hevc_vaapi", EncoderVendor.Vaapi, HostPlatform.Linux)]
    [InlineData("av1_vaapi", EncoderVendor.Vaapi, HostPlatform.Linux)]
    [InlineData("h264_videotoolbox", EncoderVendor.VideoToolbox, HostPlatform.MacOS)]
    [InlineData("hevc_videotoolbox", EncoderVendor.VideoToolbox, HostPlatform.MacOS)]
    public void YeniDonanimKodlayicisiKendiPlatformundaDonanimAilesinde(string codec, EncoderVendor vendor, HostPlatform platform)
    {
        Assert.Equal(vendor, CodecModel.Vendor(codec));
        Assert.True(CodecModel.IsHardware(codec));
        Assert.True(CodecModel.SinglePassRateControl(codec));
        Assert.False(CodecModel.TakesPreset(codec));
        Assert.True(ParallelJobs.NeedsHardwareSlot(codec));
        Assert.Equal(platform, CodecModel.OnlyPlatform(codec));
        Assert.Contains(codec, FfmpegArguments.KnownCodecs);

        foreach (var host in Enum.GetValues<HostPlatform>())
        {
            Assert.Equal(host == platform, CodecModel.IsOfferedOn(codec, host));
            Assert.Equal(host == platform, FfmpegArguments.OfferedCodecs(host).Contains(codec));
            Assert.Equal(host == platform, PlanCalculator.IsLockableCodecOn(codec, host));
        }
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("hevc_nvenc")]
    [InlineData("libsvtav1")]
    public void PlatformaBagliOlmayanKodlayiciHerYerdeOnerilir(string codec)
    {
        Assert.Null(CodecModel.OnlyPlatform(codec));
        foreach (var host in Enum.GetValues<HostPlatform>())
            Assert.True(CodecModel.IsOfferedOn(codec, host));
    }

    [Fact]
    public void Av1VideoToolboxEklenmedi()
    {
        Assert.DoesNotContain("av1_videotoolbox", FfmpegArguments.KnownCodecs);
        Assert.False(PlanCalculator.IsLockableCodecOn("av1_videotoolbox", HostPlatform.MacOS));
    }

    [Theory]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("av1_vaapi")]
    public void VaapiAygitiGirdidenOnceAcarVeKareyiYuzeyeYukler(string codec)
    {
        var args = FfmpegArguments.Build(Source, BitHiziPlani(codec), "out.mp4", 0, null).ToList();

        Assert.Equal(new[] { "-hide_banner", "-y", "-vaapi_device", CodecModel.VaapiDevice }, args.Take(4));
        Assert.True(args.IndexOf("-vaapi_device") < args.IndexOf("-i"));
        Assert.EndsWith("format=nv12,hwupload", Sonraki(args, "-vf"), StringComparison.Ordinal);
        Assert.DoesNotContain("-pix_fmt", args);
        Assert.Equal("VBR", Sonraki(args, "-rc_mode"));
        Assert.Equal("2000k", Sonraki(args, "-b:v"));
        Assert.DoesNotContain("-preset", args);
        Assert.DoesNotContain("-pass", args);
        Assert.DoesNotContain("-crf", args);
        Assert.False(FfmpegArguments.NeedsTwoPasses(codec));
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("h264_nvenc")]
    [InlineData("h264_videotoolbox")]
    public void VaapiDisindaAygitVeYuklemeYok(string codec)
    {
        var args = FfmpegArguments.Build(Source, BitHiziPlani(codec), "out.mp4", 0, null).ToList();

        Assert.DoesNotContain("-vaapi_device", args);
        Assert.DoesNotContain("-rc_mode", args);
        Assert.DoesNotContain(args, a => a.Contains("hwupload", StringComparison.Ordinal));
        Assert.Contains("-pix_fmt", args);
        Assert.Empty(CodecModel.DeviceArgs(codec));
        Assert.Empty(CodecModel.UploadFilters(codec, "yuv420p"));
        Assert.True(CodecModel.TakesPixelFormatFlag(codec));
    }

    [Fact]
    public void VaapiOnBitIstegiNv12yeIner()
    {
        Assert.Equal("nv12", CodecModel.OutputPixelFormat("hevc_vaapi", "yuv420p10le"));
        Assert.Equal("nv12", CodecModel.OutputPixelFormat("h264_vaapi", "yuv420p"));
        Assert.Equal("yuv420p10le", CodecModel.OutputPixelFormat("libx265", "yuv420p10le"));
    }

    [Theory]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("av1_vaapi")]
    public void VaapiYoklamasiUretiminAnahtarlariylaKosar(string codec)
    {
        var args = EncoderCapabilities.ProbeArguments(codec);

        Assert.Equal(CodecModel.VaapiDevice, Sonraki(args, "-vaapi_device"));
        Assert.True(Array.IndexOf(args, "-vaapi_device") < Array.IndexOf(args, "-i"));
        Assert.Equal("format=nv12,hwupload", Sonraki(args, "-vf"));
        Assert.Equal(codec, Sonraki(args, "-c:v"));
        Assert.DoesNotContain("-pix_fmt", args);
        Assert.DoesNotContain("-vaapi_device", EncoderCapabilities.ProbeArguments("h264_nvenc"));
        Assert.DoesNotContain("-vf", EncoderCapabilities.ProbeArguments("h264_videotoolbox"));
    }

    [Theory]
    [InlineData("h264_vaapi", HostPlatform.Linux, "Linux")]
    [InlineData("hevc_vaapi", HostPlatform.Linux, "Linux")]
    [InlineData("av1_vaapi", HostPlatform.Linux, "Linux")]
    [InlineData("h264_videotoolbox", HostPlatform.MacOS, "macOS")]
    [InlineData("hevc_videotoolbox", HostPlatform.MacOS, "macOS")]
    public void PlanAyristiriciKendiPlatformundaKabulEderBaskasindaReddeder(string codec, HostPlatform platform, string ad)
    {
        Assert.True(PlanParser.Parse(Plan(codec, "2pass"), Source, new PlanOptions(), platform).Ok);

        foreach (var host in Enum.GetValues<HostPlatform>().Where(h => h != platform))
        {
            var result = PlanParser.Parse(Plan(codec, "2pass"), Source, new PlanOptions(), host);
            Assert.False(result.Ok);
            Assert.Contains(result.Errors, e => e == $"Codec {codec} is only available on {ad}.");
        }

        var kalite = PlanParser.Parse(Plan(codec, "crf"), Source, new PlanOptions(), platform);
        Assert.False(kalite.Ok);
        Assert.Contains(kalite.Errors, e => e.Contains("2pass mode only", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(IntermediateCodecs.ProRes)]
    [InlineData(IntermediateCodecs.DnxHr)]
    public void HedefBoyutYoluAraKodegiAcikHataylaReddeder(string codec)
    {
        foreach (var host in Enum.GetValues<HostPlatform>())
        {
            var result = PlanParser.Parse(Plan(codec, "2pass"), Source, new PlanOptions(), host);
            Assert.False(result.Ok);
            Assert.Null(result.Plan);
            Assert.Contains(result.Errors, e => e.StartsWith($"Codec {codec} is an intermediate codec", StringComparison.Ordinal));
            Assert.False(PlanCalculator.IsLockableCodecOn(codec, host));
        }

        Assert.DoesNotContain(codec, FfmpegArguments.KnownCodecs);
        Assert.False(ParallelJobs.NeedsHardwareSlot(codec));
        Assert.Throws<ArgumentException>(() => PlanCalculator.Build(Source, new PlanOptions { TargetMb = 10, LockedCodec = codec }));
        Assert.True(PlanParser.Parse(Plan("libx264", "2pass"), Source, new PlanOptions(), HostPlatform.Windows).Ok);
    }

    [Fact]
    public void ProfilTablosuOnBasamak()
    {
        Assert.Equal(
            new[]
            {
                "prores_ks:proxy", "prores_ks:lt", "prores_ks:standard", "prores_ks:hq", "prores_ks:4444",
                "dnxhd:dnxhr_lb", "dnxhd:dnxhr_sq", "dnxhd:dnxhr_hq", "dnxhd:dnxhr_hqx", "dnxhd:dnxhr_444"
            },
            IntermediateCodecs.Profiles.Select(p => p.Id));

        Assert.Equal("yuv422p10le", IntermediateCodecs.Find("prores_ks", "hq")!.PixelFormat);
        Assert.Equal("yuv444p10le", IntermediateCodecs.Find("prores_ks", "4444")!.PixelFormat);
        Assert.Equal("yuv422p", IntermediateCodecs.Find("dnxhd", "dnxhr_sq")!.PixelFormat);
        Assert.Equal("yuv422p10le", IntermediateCodecs.Find("dnxhd", "dnxhr_hqx")!.PixelFormat);
        Assert.Null(IntermediateCodecs.Find("prores_ks", "dnxhr_sq"));
        Assert.Null(IntermediateCodecs.Find("libx264", "hq"));
        Assert.Null(IntermediateCodecs.FromId("prores_ks:uydurma"));
        Assert.Null(IntermediateCodecs.FromId("libx264"));
        Assert.False(IntermediateCodecs.IsIntermediate("libx264"));

        var xaml = File.ReadAllText(Path.Combine(Kok(), "src", "VidShrink.App", "MainWindow.axaml"));
        var kutu = xaml[xaml.IndexOf("x:Name=\"CmbConvertCodec\"", StringComparison.Ordinal)..];
        kutu = kutu[..kutu.IndexOf("</ComboBox>", StringComparison.Ordinal)];
        foreach (var profil in IntermediateCodecs.Profiles)
            Assert.Contains($"Content=\"{profil.Label}\" Tag=\"{profil.Id}\"", kutu, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mov", "prores_ks", "hq", "yuv422p10le")]
    [InlineData("mov", "prores_ks", "4444", "yuv444p10le")]
    [InlineData("mov", "dnxhd", "dnxhr_sq", "yuv422p")]
    [InlineData("mxf", "dnxhd", "dnxhr_hqx", "yuv422p10le")]
    public void AraKodekProfilVePikselBicimiAlirKaliteAnahtariAlmaz(string kap, string codec, string profil, string piksel)
    {
        var plan = new ConversionPlan
        {
            Container = kap,
            VideoCodec = codec,
            VideoProfile = profil,
            AudioCodec = "pcm_s16le",
            Crf = 18,
            VideoBitrateK = 900
        };

        Assert.Empty(ConversionArguments.Validate(Source, plan));
        var args = Donustur(plan);

        Assert.Equal(codec, Sonraki(args, "-c:v"));
        Assert.Equal(profil, Sonraki(args, "-profile:v"));
        Assert.Equal(piksel, Sonraki(args, "-pix_fmt"));
        Assert.DoesNotContain("-crf", args);
        Assert.DoesNotContain("-preset", args);
        Assert.DoesNotContain("-b:v", args);
        Assert.Equal("pcm_s16le", Sonraki(args, "-c:a"));
        Assert.Equal(kap == "mxf", args.Contains("-ar"));
        Assert.Equal(kap != "mxf", args.Contains("-movflags"));
        if (kap == "mxf") Assert.Equal("48000", Sonraki(args, "-ar"));
    }

    [Fact]
    public void AraKodekOlmayanDonusturmeProfilYazmazKaliteYazar()
    {
        var args = Donustur(new ConversionPlan { Container = "mov", VideoCodec = "libx264", VideoProfile = "hq", Crf = 20 });

        Assert.DoesNotContain("-profile:v", args);
        Assert.Equal("20", Sonraki(args, "-crf"));
        Assert.Contains("-preset", args);
        Assert.DoesNotContain("-ar", args);
    }

    [Theory]
    [InlineData("mov", "prores_ks", null, "Unsupported prores_ks profile: none")]
    [InlineData("mov", "prores_ks", "uydurma", "Unsupported prores_ks profile: uydurma")]
    [InlineData("mov", "dnxhd", "hq", "Unsupported dnxhd profile: hq")]
    public void TablodaOlmayanProfilKomutaInmez(string kap, string codec, string? profil, string hata)
    {
        var plan = new ConversionPlan { Container = kap, VideoCodec = codec, VideoProfile = profil, AudioCodec = "aac" };

        var atilan = Assert.Throws<ArgumentException>(() => Donustur(plan));
        Assert.StartsWith(hata, atilan.Message, StringComparison.Ordinal);
        Donustur(new ConversionPlan { Container = kap, VideoCodec = codec, VideoProfile = codec == "dnxhd" ? "dnxhr_hq" : "hq", AudioCodec = "aac" });
    }

    [Theory]
    [InlineData("mp4", "prores_ks", "hq", "aac", "The MP4 container does not support the selected prores_ks video encoder.")]
    [InlineData("mkv", "prores_ks", "hq", "aac", "The MKV container does not support the selected prores_ks video encoder.")]
    [InlineData("mkv", "dnxhd", "dnxhr_sq", "aac", "The MKV container does not support the selected dnxhd video encoder.")]
    [InlineData("mxf", "prores_ks", "hq", "pcm_s16le", "The MXF container does not support the selected prores_ks video encoder.")]
    [InlineData("mxf", "libx264", null, "pcm_s16le", "The MXF container does not support the selected libx264 video encoder.")]
    [InlineData("mxf", "dnxhd", "dnxhr_sq", "aac", "The MXF container does not support the selected aac audio encoder.")]
    public void AraKodekYanlisKaptaReddedilir(string kap, string codec, string? profil, string ses, string hata)
    {
        var plan = new ConversionPlan { Container = kap, VideoCodec = codec, VideoProfile = profil, AudioCodec = ses };

        Assert.Contains(hata, ConversionArguments.Validate(Source, plan));
        Assert.Throws<InvalidOperationException>(() => Donustur(plan));
    }

    [Theory]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    public void DonusturVaapideYuklerVePikselBayragiYazmaz(string codec)
    {
        var args = Donustur(new ConversionPlan { Container = "mkv", VideoCodec = codec, QualityMode = ConversionQualityMode.Bitrate });

        Assert.Equal(CodecModel.VaapiDevice, Sonraki(args, "-vaapi_device"));
        Assert.True(args.IndexOf("-vaapi_device") < args.IndexOf("-i"));
        Assert.EndsWith("format=nv12,hwupload", Sonraki(args, "-vf"), StringComparison.Ordinal);
        Assert.DoesNotContain("-pix_fmt", args);
        Assert.DoesNotContain("-preset", args);
    }

    [Fact]
    public void KodlayicisiOlmayanProfilListedeKalirPasifOlur()
    {
        var (sayiOnce, sayiSonra, proresPasif, dnxAcik, geriAcik) = Pencerede(window =>
        {
            var once = window.CmbConvertCodec.Items.Count;
            window.ApplyHardwareVerdict(new SahteKodlayicilar(IntermediateCodecs.ProRes), false, HardwareVerdict.NotProbed);
            var prores = Kodek(window, "prores_ks:hq");
            var pasif = prores.IsVisible && !prores.IsEnabled
                        && IntermediateCodecs.Profiles.Where(p => p.Encoder == IntermediateCodecs.ProRes).All(p => !Kodek(window, p.Id).IsEnabled);
            var dnx = Kodek(window, "dnxhd:dnxhr_sq").IsEnabled && Kodek(window, "libx264").IsEnabled;
            var sonra = window.CmbConvertCodec.Items.Count;
            window.ApplyHardwareVerdict(new SahteKodlayicilar(), false, HardwareVerdict.NotProbed);
            return (once, sonra, pasif, dnx, prores.IsEnabled);
        });

        Assert.Equal(sayiOnce, sayiSonra);
        Assert.True(proresPasif);
        Assert.True(dnxAcik);
        Assert.True(geriAcik);
    }

    [Fact]
    public void ProfilSecimiKomutaInerVeKaliteDenetimleriKapanir()
    {
        var (komut, kaliteKapali, yanlisKap, x264Komut, kaliteAcik) = Pencerede(window =>
        {
            window.LoadWithoutProbing(Source.FilePath, Source);
            window.CmbContainer.SelectedItem = Kap(window, "mov");
            window.CmbConvertCodec.SelectedItem = Kodek(window, "prores_ks:hq");
            var ilk = window.TxtConvertCommand.Text ?? "";
            var kapali = !window.CmbQualityMode.IsEnabled && !window.SliderQuality.IsEnabled && !window.TxtQuality.IsEnabled;

            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            var hata = window.TxtConvertCommand.Text ?? "";

            window.CmbConvertCodec.SelectedItem = Kodek(window, "libx264");
            var acik = window.CmbQualityMode.IsEnabled && window.SliderQuality.IsEnabled && window.TxtQuality.IsEnabled;
            return (ilk, kapali, hata, window.TxtConvertCommand.Text ?? "", acik);
        });

        Assert.Contains("-c:v prores_ks -profile:v hq -pix_fmt yuv422p10le", komut, StringComparison.Ordinal);
        Assert.DoesNotContain("-crf", komut, StringComparison.Ordinal);
        Assert.True(kaliteKapali);
        Assert.DoesNotContain("prores_ks -profile:v", yanlisKap, StringComparison.Ordinal);
        Assert.DoesNotContain("libx264", yanlisKap, StringComparison.Ordinal);
        Assert.Contains("-c:v libx264", x264Komut, StringComparison.Ordinal);
        Assert.DoesNotContain("-profile:v hq", x264Komut, StringComparison.Ordinal);
        Assert.True(kaliteAcik);
    }

    [FfmpegTheory]
    [InlineData("mov", "prores_ks", "lt", "prores", "yuv422p10le", "pcm_s16le")]
    [InlineData("mxf", "dnxhd", "dnxhr_lb", "dnxhd", "yuv422p", "pcm_s16le")]
    public void AraKodekGercektenKodlanirVeFfprobeOkur(string kap, string codec, string profil, string beklenenKodek, string piksel, string ses)
    {
        var klasor = Path.Combine(Kok(), ".calisma", "worktree-agent-ac8fd486218d4e715", "test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mkv");
            var (kod, hata) = Kos(ToolLocator.Ffmpeg, "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=2",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", kaynak);
            Assert.True(kod == 0, hata);

            var info = new MediaInfo
            {
                FilePath = kaynak,
                FileSizeBytes = new FileInfo(kaynak).Length,
                DurationSeconds = 2,
                Width = 320,
                Height = 240,
                Fps = 25,
                VideoCodec = "h264",
                TotalBitrateBps = 500_000,
                AudioCodec = "aac",
                AudioChannels = 1
            };
            var cikti = Path.Combine(klasor, "cikti." + kap);
            var plan = new ConversionPlan { Container = kap, VideoCodec = codec, VideoProfile = profil, AudioCodec = ses };
            var args = new List<string> { "-threads", "2" };
            args.AddRange(ConversionArguments.Build(info, plan, cikti));
            (kod, hata) = Kos(ToolLocator.Ffmpeg, args.ToArray());
            Assert.True(kod == 0, hata);

            var (probeKod, json) = Kos(ToolLocator.Ffprobe, true, "-v", "error", "-show_entries",
                "stream=codec_name,pix_fmt,sample_rate", "-of", "json", cikti);
            Assert.Equal(0, probeKod);
            using var belge = JsonDocument.Parse(json);
            var akislar = belge.RootElement.GetProperty("streams").EnumerateArray().ToList();
            var video = akislar.Single(a => a.TryGetProperty("pix_fmt", out _));
            Assert.Equal(beklenenKodek, video.GetProperty("codec_name").GetString());
            Assert.Equal(piksel, video.GetProperty("pix_fmt").GetString());
            var sesAkisi = akislar.Single(a => a.TryGetProperty("sample_rate", out _));
            Assert.Equal(ses, sesAkisi.GetProperty("codec_name").GetString());
            if (kap == "mxf") Assert.Equal("48000", sesAkisi.GetProperty("sample_rate").GetString());

            var uydurma = Donustur(plan);
            uydurma[uydurma.IndexOf("-profile:v") + 1] = "uydurma";
            uydurma[^1] = Path.Combine(klasor, "uydurma." + kap);
            uydurma[uydurma.IndexOf("-i") + 1] = kaynak;
            (kod, _) = Kos(ToolLocator.Ffmpeg, new[] { "-threads", "2" }.Concat(uydurma).ToArray());
            Assert.NotEqual(0, kod);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static string Plan(string codec, string mode)
        => JsonSerializer.Serialize(new
        {
            codec,
            mode,
            videoBitrateK = 1200,
            crf = mode == "crf" ? 23 : (int?)null,
            audioCodec = "aac",
            audioBitrateK = 128,
            width = 1920,
            height = 1080,
            fps = 30,
            preset = codec.StartsWith("lib", StringComparison.Ordinal) ? "slow" : "default",
            extraArgs = Array.Empty<string>(),
            reason = "test"
        });

    private static (int Kod, string Metin) Kos(string exe, params string[] args) => Kos(exe, false, args);

    private static (int Kod, string Metin) Kos(string exe, bool stdout, params string[] args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(exe, args) };
        process.Start();
        var cikti = process.StandardOutput.ReadToEndAsync();
        var hata = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            return (-1, "zaman asimi");
        }
        Task.WaitAll(cikti, hata);
        return (process.ExitCode, stdout ? cikti.Result : hata.Result);
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static ComboBoxItem Kodek(MainWindow window, string etiket)
        => window.CmbConvertCodec.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(Kok(), ".calisma", "test-ciktilari", "ara-kodek", Guid.NewGuid().ToString("N"));
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

    private static string Kok()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new InvalidOperationException("VidShrink.sln bulunamadı");
    }
}
