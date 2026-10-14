using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// DON-04: Dönüştür'ün kodek listesinde NVENC, QSV ve AMF. Liste ile kap uyum tablosu eşlenir,
/// yoklama makinede çalışmayan kodlayıcıyı gizler, kayıtlı seçim sıra numarası yerine adla
/// saklanır ve eski sıra numaralı kayıt ada göçer.
/// </summary>
public sealed class DonanimKodlayiciListesiTests
{
    private static readonly string[] DonanimKodlayicilari =
    {
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    };

    private static readonly string[] EskiSira =
    {
        "libx264", "libx265", "libvpx-vp9", "libsvtav1", "copy",
        "prores_ks:proxy", "prores_ks:lt", "prores_ks:standard", "prores_ks:hq", "prores_ks:4444",
        "dnxhd:dnxhr_lb", "dnxhd:dnxhr_sq", "dnxhd:dnxhr_hq", "dnxhd:dnxhr_hqx", "dnxhd:dnxhr_444"
    };

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

    private sealed class SahteKodlayicilar(params string[] calisan) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => calisan.Contains(name) || !name.Contains('_');
        public bool WorksAsEncoder(string codec) => HasEncoder(codec);
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    private static string Xaml() => File.ReadAllText(Path.Combine(Kok(), "src", "VidShrink.App", "MainWindow.axaml"));

    private static List<string> KutuEtiketleri(string ad)
    {
        var xaml = Xaml();
        var kutu = xaml[xaml.IndexOf($"x:Name=\"{ad}\"", StringComparison.Ordinal)..];
        kutu = kutu[..kutu.IndexOf("</ComboBox>", StringComparison.Ordinal)];
        return Regex.Matches(kutu, "Tag=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
    }

    [Fact]
    public void ListedekiDonanimKodlayicilariMotorunSurduguKumeyleAyni()
    {
        var liste = KutuEtiketleri("CmbConvertCodec");

        Assert.Equal(DonanimKodlayicilari, liste.Where(CodecModel.IsHardware).ToArray());
        Assert.Equal(DonanimKodlayicilari, ConversionCodecChoice.Hardware.ToArray());
        Assert.Equal(liste.Count, liste.Distinct().Count());
    }

    [Fact]
    public void ListedekiHerKodlayiciListedekiEnAzBirKapaYazilir()
    {
        var kaplar = KutuEtiketleri("CmbContainer");
        var kodekler = KutuEtiketleri("CmbConvertCodec").Where(t => t != "copy").ToList();
        var yetim = new List<string>();
        foreach (var kodek in kodekler)
        {
            var ara = IntermediateCodecs.FromId(kodek);
            var kabul = kaplar.Where(kap => Uyumlu(kap, ara?.Encoder ?? kodek, ara?.Profile)).ToList();
            if (kabul.Count == 0) yetim.Add(kodek);
        }

        Assert.Empty(yetim);
    }

    [Theory]
    [InlineData("h264_nvenc", "mp4,mkv,mov")]
    [InlineData("hevc_nvenc", "mp4,mkv,mov")]
    [InlineData("av1_nvenc", "mp4,mkv")]
    [InlineData("h264_qsv", "mp4,mkv,mov")]
    [InlineData("hevc_qsv", "mp4,mkv,mov")]
    [InlineData("av1_qsv", "mp4,mkv")]
    [InlineData("h264_amf", "mp4,mkv,mov")]
    [InlineData("hevc_amf", "mp4,mkv,mov")]
    [InlineData("av1_amf", "mp4,mkv")]
    public void DonanimKodlayicisininKapTablosu(string kodek, string beklenen)
    {
        var videoKaplari = new[] { "mp4", "mkv", "mov", "webm", "avi", "mxf" };
        var kabul = videoKaplari.Where(kap => Uyumlu(kap, kodek, null));

        Assert.Equal(beklenen.Split(','), kabul.ToArray());
    }

    [Theory]
    [InlineData(null, 0, "libx264")]
    [InlineData(null, 3, "libsvtav1")]
    [InlineData(null, 4, "copy")]
    [InlineData(null, 9, "prores_ks:4444")]
    [InlineData(null, 14, "dnxhd:dnxhr_444")]
    [InlineData(null, 15, "libx264")]
    [InlineData(null, -1, "libx264")]
    [InlineData("hevc_nvenc", 4, "hevc_nvenc")]
    [InlineData("", 4, "copy")]
    public void KayitliSecimAdaGocer(string? ad, int eskiSira, string beklenen)
        => Assert.Equal(beklenen, ConversionCodecChoice.FromSaved(ad, eskiSira));

    [Fact]
    public void EskiSiraTablosuEskiListeyleAyni()
    {
        Assert.Equal(EskiSira, ConversionCodecChoice.LegacyOrder.ToArray());
        for (var i = 0; i < EskiSira.Length; i++) Assert.Equal(i, ConversionCodecChoice.LegacyIndex(EskiSira[i]));
        Assert.Equal(0, ConversionCodecChoice.LegacyIndex("av1_amf"));
    }

    [Theory]
    [InlineData(4, "copy")]
    [InlineData(9, "prores_ks:4444")]
    [InlineData(14, "dnxhd:dnxhr_444")]
    [InlineData(2, "libvpx-vp9")]
    public void EskiSiraNumaraliKayitPenceredeAyniKodegiSecer(int eskiSira, string beklenen)
    {
        var (secili, kayit) = Pencerede((window, klasor) =>
        {
            var dosya = Path.Combine(klasor, "eski.json");
            File.WriteAllText(dosya, $"{{\"convertCodec\": {eskiSira}}}");
            window.RestoreSettingsForTest(UpdateSettings.Load(dosya));
            return ((string?)((ComboBoxItem)window.CmbConvertCodec.SelectedItem!).Tag, window.CaptureSettingsForTest());
        });

        Assert.Equal(beklenen, secili);
        Assert.Equal(beklenen, kayit.ConvertCodecName);
        Assert.Equal(eskiSira, kayit.ConvertCodec);
    }

    [Fact]
    public void AdlaKayitliDonanimSecimiYoklamaBeklerkenKorunur()
    {
        var (secili, gorunur) = Pencerede((window, klasor) =>
        {
            window.RestoreSettingsForTest(new UpdateSettings { ConvertCodecName = "hevc_nvenc", ConvertCodec = 0 });
            var item = (ComboBoxItem)window.CmbConvertCodec.SelectedItem!;
            return ((string?)item.Tag, item.IsVisible);
        });

        Assert.Equal("hevc_nvenc", secili);
        Assert.False(gorunur);
    }

    [Fact]
    public void AdKaydiDosyadaKalir()
    {
        var klasor = Path.Combine(Kok(), ".calisma", "test-ciktilari", "donanim-liste", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "s.json");
            new UpdateSettings { ConvertCodecName = "av1_qsv", ConvertCodec = 0 }.Save(dosya);
            using var belge = JsonDocument.Parse(File.ReadAllText(dosya));
            Assert.Equal("av1_qsv", belge.RootElement.GetProperty("convertCodecName").GetString());
            Assert.Equal("av1_qsv", UpdateSettings.Load(dosya).ConvertCodecName);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void YoklamaCalismayanDonanimKodlayicisiniGizlerSeciliyseH264eDoner()
    {
        var (gorunen, secili, yazilimAcik) = Pencerede((window, _) =>
        {
            window.CmbConvertCodec.SelectedItem = Kodek(window, "hevc_qsv");
            var onceGizli = DonanimKodlayicilari.All(k => !Kodek(window, k).IsVisible);
            Assert.True(onceGizli);

            window.ApplyHardwareVerdict(new SahteKodlayicilar("h264_nvenc", "av1_nvenc"), false, HardwareVerdict.NotProbed);
            var son = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < son)
            {
                Dispatcher.UIThread.RunJobs();
                if (!window.PlanProbePending && Kodek(window, "av1_nvenc").IsVisible && Kodek(window, "h264_nvenc").IsVisible
                    && (string?)((ComboBoxItem)window.CmbConvertCodec.SelectedItem!).Tag != "hevc_qsv") break;
                Thread.Sleep(20);
            }
            Thread.Sleep(100);
            Dispatcher.UIThread.RunJobs();

            var acik = DonanimKodlayicilari.Where(k => Kodek(window, k).IsVisible && Kodek(window, k).IsEnabled).ToArray();
            var yazilim = new[] { "libx264", "libx265", "libvpx-vp9", "libsvtav1" }.All(k => Kodek(window, k).IsVisible);
            return (acik, (string?)((ComboBoxItem)window.CmbConvertCodec.SelectedItem!).Tag, yazilim);
        });

        Assert.Equal(new[] { "h264_nvenc", "av1_nvenc" }, gorunen);
        Assert.Equal("libx264", secili);
        Assert.True(yazilimAcik);
    }

    [Fact]
    public void DonanimSecimiKomutaIner()
    {
        var komut = Pencerede((window, _) =>
        {
            window.LoadWithoutProbing(Kaynak.FilePath, Kaynak);
            window.CmbConvertCodec.SelectedItem = Kodek(window, "hevc_nvenc");
            return window.TxtConvertCommand.Text ?? "";
        });

        Assert.Contains("-c:v hevc_nvenc", komut, StringComparison.Ordinal);
        Assert.Contains("-cq", komut, StringComparison.Ordinal);
    }

    private static bool Uyumlu(string kap, string kodek, string? profil)
    {
        var plan = new ConversionPlan { Container = kap, VideoCodec = kodek, VideoProfile = profil, AudioCodec = "copy" };
        if (plan.AudioOnly || plan.Gif || plan.AnimatedImage) return false;
        return !ConversionArguments.Validate(Kaynak, plan).Any(h => h.Contains("video encoder", StringComparison.Ordinal));
    }

    private static ComboBoxItem Kodek(MainWindow window, string etiket)
        => window.CmbConvertCodec.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, string, T> govde)
    {
        var klasor = Path.Combine(Kok(), ".calisma", "test-ciktilari", "donanim-liste", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json") };
                try { return govde(window, klasor); }
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
