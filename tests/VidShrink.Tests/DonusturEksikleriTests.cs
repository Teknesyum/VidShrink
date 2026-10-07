using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür sekmesinin üç eksiği: kodlayıcısı olmayan kap listede sunulmaz, WebP'nin bit hızı
/// kipinde değerin yok sayıldığı söylenir, ses taşımayan GIF'te ses kopyası doğrulanmaz.
/// Ölçüm <c>docs/olcumler/webp-bit-hizi.md</c>.
/// </summary>
public sealed class DonusturEksikleriTests
{
    private const string NotAnahtari = "main.convert.note.webp-bitrate-ignored";

    private static readonly MediaInfo Sesli = new()
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

    private static readonly MediaInfo Sessiz = new()
    {
        FilePath = @"C:\media\source.mp4",
        FileSizeBytes = 20_000_000L,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000
    };

    private sealed class SahteKodlayicilar(params string[] eksik) : IEncoderAvailability
    {
        public bool HasEncoder(string name) => !eksik.Contains(name);
        public bool WorksAsEncoder(string codec) => !eksik.Contains(codec);
        public EncoderProbeState EncoderState(string codec) => EncoderProbeState.Unmeasured;
    }

    [Fact]
    public void GifteSesKopyasiDogrulanmaz()
    {
        Assert.Empty(ConversionArguments.Validate(Sesli, new ConversionPlan { Container = "gif", AudioCodec = "copy" }));
        Assert.Empty(ConversionArguments.Validate(Sessiz, new ConversionPlan { Container = "gif", AudioCodec = "copy" }));
        Assert.False(new ConversionPlan { Container = "gif" }.CarriesAudio);
        Assert.False(new ConversionPlan { Container = "webp" }.CarriesAudio);
        Assert.True(new ConversionPlan { Container = "mp4" }.CarriesAudio);
        Assert.True(new ConversionPlan { Container = "mp3" }.CarriesAudio);

        Assert.Contains(
            ConversionArguments.Validate(Sessiz, new ConversionPlan { Container = "mp4", AudioCodec = "copy" }),
            hata => hata == "The source has no audio stream to copy.");
        Assert.Contains(
            ConversionArguments.Validate(Sesli, new ConversionPlan { Container = "webm", VideoCodec = "libvpx-vp9", AudioCodec = "copy" }),
            hata => hata == "The WEBM container does not support copying the source aac audio stream.");
    }

    [Theory]
    [InlineData("webp", "libwebp_anim")]
    [InlineData("avif", "libsvtav1")]
    public void KodlayicisiOlmayanKapSunulmaz(string kap, string kodlayici)
    {
        Assert.Equal(kodlayici, ConversionArguments.RequiredEncoder(kap));
        Assert.False(ConversionArguments.ContainerAvailable(kap, new SahteKodlayicilar(kodlayici)));
        Assert.True(ConversionArguments.ContainerAvailable(kap, new SahteKodlayicilar()));
        Assert.True(ConversionArguments.ContainerAvailable(kap, new SahteKodlayicilar("uydurma")));
        Assert.True(ConversionArguments.ContainerAvailable(kap, null));
        Assert.True(ConversionArguments.ContainerAvailable("mp4", new SahteKodlayicilar(kodlayici)));
        Assert.True(ConversionArguments.ContainerAvailable("gif", new SahteKodlayicilar(kodlayici)));
    }

    [Fact]
    public void KapListesiYoklamayiIzlerVeGizlenenSecimMp4eDuser()
    {
        var (once, webpYok, secim, avifVar, geriGeldi) = Pencerede(window =>
        {
            var webp = Kap(window, "webp");
            var avif = Kap(window, "avif");
            window.CmbContainer.SelectedItem = webp;
            var baslangic = webp.IsVisible && avif.IsVisible;

            window.ApplyHardwareVerdict(new SahteKodlayicilar("libwebp_anim"), false, HardwareVerdict.NotProbed);
            var gizli = !webp.IsVisible && !webp.IsEnabled;
            var secili = (string?)(window.CmbContainer.SelectedItem as ComboBoxItem)?.Tag;
            var avifAcik = avif.IsVisible && avif.IsEnabled;

            window.ApplyHardwareVerdict(new SahteKodlayicilar(), false, HardwareVerdict.NotProbed);
            return (baslangic, gizli, secili, avifAcik, webp.IsVisible && webp.IsEnabled);
        });

        Assert.True(once);
        Assert.True(webpYok);
        Assert.Equal("mp4", secim);
        Assert.True(avifVar);
        Assert.True(geriGeldi);
    }

    [Fact]
    public void KodlayiciVarkenSecimYerindeKalir()
    {
        var (secim, avifGizli) = Pencerede(window =>
        {
            window.CmbContainer.SelectedItem = Kap(window, "webp");
            window.ApplyHardwareVerdict(new SahteKodlayicilar("libsvtav1"), false, HardwareVerdict.NotProbed);
            return ((string?)(window.CmbContainer.SelectedItem as ComboBoxItem)?.Tag, !Kap(window, "avif").IsVisible);
        });

        Assert.Equal("webp", secim);
        Assert.True(avifGizli);
    }

    [Theory]
    [InlineData("webp", ConversionQualityMode.Bitrate, true)]
    [InlineData("webp", ConversionQualityMode.Crf, false)]
    [InlineData("avif", ConversionQualityMode.Bitrate, false)]
    [InlineData("mp4", ConversionQualityMode.Bitrate, false)]
    [InlineData("gif", ConversionQualityMode.Bitrate, false)]
    public void YalnizWebpBitHiziKipiNotAlir(string kap, ConversionQualityMode kip, bool beklenen)
    {
        var notlar = ConversionArguments.Notes(new ConversionPlan { Container = kap, QualityMode = kip, VideoBitrateK = 900 });

        Assert.Equal(beklenen, notlar.Contains(ConversionNote.WebpBitrateIgnored));
        Assert.Equal(beklenen ? 1 : 0, notlar.Count);
    }

    [Fact]
    public void WebpBitHiziNotuDonusturDurumundaGorunur()
    {
        var tr = Locales.Domain("tr", "main");
        var (bitHizi, kalite, mp4) = Pencerede(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(Sesli.FilePath, Sesli);
            window.CmbContainer.SelectedItem = Kap(window, "webp");
            window.CmbQualityMode.SelectedIndex = 1;
            var ilk = window.ConvertStatusForTest ?? "";
            window.CmbQualityMode.SelectedIndex = 0;
            var ikinci = window.ConvertStatusForTest ?? "";
            window.CmbQualityMode.SelectedIndex = 1;
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            return (ilk, ikinci, window.ConvertStatusForTest ?? "");
        });

        Assert.Equal(tr["main.convert.ready"] + "\n" + tr[NotAnahtari], bitHizi);
        Assert.Equal(tr["main.convert.ready"], kalite);
        Assert.Equal(tr["main.convert.ready"], mp4);
    }

    [Fact]
    public void NotAnahtariKirkIkiDildeCevrili()
    {
        var anahtarlar = Enum.GetValues<ConversionNote>().Select(MainWindow.ConversionNoteKey).ToList();
        Assert.Equal(new[] { NotAnahtari }, anahtarlar);

        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            if (!values.TryGetValue(NotAnahtari, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language}: yok");
            else if (language != "en" && value == english[NotAnahtari]) eksik.Add($"{language}: cevrilmemis");
            else if (!value.Contains("WebP", StringComparison.Ordinal)) eksik.Add($"{language}: WebP adi yok");
        }

        Assert.True(eksik.Count == 0, string.Join(Environment.NewLine, eksik));
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var kok = Path.Combine(HareketliKok(), ".calisma", "test-ciktilari", "donustur-eksikleri");
        try { if (Directory.Exists(kok)) Directory.Delete(kok, true); }
        catch (IOException) { }
        var klasor = Path.Combine(kok, Guid.NewGuid().ToString("N"));
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

    private static string HareketliKok()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new InvalidOperationException("VidShrink.sln bulunamadi.");
    }
}
