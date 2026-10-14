using Avalonia;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// DON-P5: Dönüştür panelinin seçimleri adla kaydedilir, listeden geri yüklenir, dosyaya
/// verilir ve başka yerde içe alınır. Depolama Küçült ön ayarından ayrı türdür
/// (<see cref="ConversionPresetLibrary"/>); iki dosya birbirinin yerine okunmaz. Her ölçüm
/// kendi geçici klasöründe; gerçek ayar klasörü okunmaz da yazılmaz da.
/// </summary>
public sealed class DonusturOnAyarTests : IDisposable
{
    private static readonly Size WindowSize = new(1560, 1060);

    private readonly string _folder = Path.Combine(TestPaths.OutputRoot, "donustur-onayar", Guid.NewGuid().ToString("N"));

    private string ConvertPath => Path.Combine(_folder, "convert-presets.json");

    private string ShrinkPath => Path.Combine(_folder, "presets.json");

    public DonusturOnAyarTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly ConversionPreset Sample = new()
    {
        Id = "arsiv-mkv",
        Name = "Arşiv MKV",
        Container = "mkv",
        VideoCodec = "libx265",
        QualityMode = ConversionQualityMode.Bitrate,
        Quality = 3000,
        Resolution = "custom",
        CustomResolution = "1920x800",
        Fps = "24",
        AudioCodec = ConversionPreset.NoAudio,
        AudioBitrateK = 192,
        DropMetadata = true
    };

    private T Read<T>(Func<MainWindow, T> read, string? convertPath = null) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow
            {
                SettingsPathOverride = Path.Combine(_folder, "settings.json"),
                PresetPathOverride = ShrinkPath,
                ConvertPresetPathOverride = convertPath ?? ConvertPath
            };
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();
            window.InitUserPresets();
            window.InitConvertPresets();
            return read(window);
        });

    [Fact]
    public void KaydetYukleGidisDonusVeAyniIdDegistirir()
    {
        Assert.Empty(ConversionPresetLibrary.LoadUser(ConvertPath));

        ConversionPresetLibrary.SaveUser(Sample, ConvertPath);
        ConversionPresetLibrary.SaveUser(Sample with { Id = "ikinci", Name = "İkinci", Container = "webm" }, ConvertPath);
        var loaded = ConversionPresetLibrary.LoadUser(ConvertPath);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(Sample, loaded[0]);

        var replaced = ConversionPresetLibrary.SaveUser(Sample with { Id = "ARSIV-mkv", Quality = 4000 }, ConvertPath);
        Assert.Equal(2, replaced.Count);
        Assert.Equal(new[] { "webm", "mkv" }, ConversionPresetLibrary.LoadUser(ConvertPath).Select(p => p.Container));

        Assert.Throws<PresetFileException>(() => ConversionPresetLibrary.SaveUser(Sample with { Id = " " }, ConvertPath));
        Assert.Throws<PresetFileException>(() => ConversionPresetLibrary.SaveUser(Sample with { Quality = 0 }, ConvertPath));
        Assert.Throws<PresetFileException>(() => ConversionPresetLibrary.SaveUser(Sample with { AudioBitrateK = -1 }, ConvertPath));
        Assert.Equal(2, ConversionPresetLibrary.LoadUser(ConvertPath).Count);
    }

    [Fact]
    public void DisaIceAktarmaGidisDonus()
    {
        var path = Path.Combine(_folder, "disa.json");
        ConversionPresetLibrary.Export(new[] { Sample }, path);
        var text = File.ReadAllText(path);
        Assert.Contains("\"kind\": \"vidshrink-convert-presets\"", text);
        Assert.Equal(Sample, Assert.Single(ConversionPresetLibrary.Parse(text)));
    }

    /// <summary>İki sekmenin dosyası ayrı: biri öbürünün yerine okunmuyor, varsayılan yolları da farklı.</summary>
    [Fact]
    public void KucultOnAyariIleKarismaz()
    {
        Assert.NotEqual(PresetLibrary.DefaultUserPath, ConversionPresetLibrary.DefaultUserPath);
        Assert.Equal(Path.GetDirectoryName(PresetLibrary.DefaultUserPath), Path.GetDirectoryName(ConversionPresetLibrary.DefaultUserPath));

        var shrink = PresetLibrary.Serialize(new[] { new PresetProfile { Id = "kucult" } });
        Assert.Equal(PresetFileError.NotPresetFile,
            Assert.Throws<PresetFileException>(() => ConversionPresetLibrary.Parse(shrink)).Error);

        var convert = ConversionPresetLibrary.Serialize(new[] { Sample });
        Assert.Equal(PresetFileError.NotPresetFile,
            Assert.Throws<PresetFileException>(() => PresetLibrary.Parse(convert)).Error);
    }

    /// <summary>
    /// Arayüz yolu: panelde seçilen değerler kaydedilir, panel varsayılana döner, listeden seçilen
    /// ön ayar değerleri geri getirir. Küçült'ün dosyası bu sırada oluşmaz.
    /// </summary>
    [Fact]
    public void PaneldenKaydedilirListedenGeriYuklenir()
    {
        var (kaydedildi, liste, sifir, geri) = Read(window =>
        {
            window.ApplyConvertPreset(Sample);
            var kaydedildi = window.SaveConvertPreset("Arşiv MKV");
            var liste = window.CmbConvertPreset.Items.Count;

            window.ApplyConvertPreset(new ConversionPreset { Id = "x" });
            var sifir = window.CurrentConvertPreset("x");

            window.CmbConvertPreset.SelectedIndex = 0;
            return (kaydedildi, liste, sifir, window.CurrentConvertPreset("Arşiv MKV"));
        });

        Assert.True(kaydedildi);
        Assert.Equal(1, liste);
        Assert.Equal(new ConversionPreset { Id = "x", Name = "x" }, sifir);
        Assert.Equal(Sample with { Id = "Arşiv MKV" }, geri);

        var dosyadan = Assert.Single(ConversionPresetLibrary.LoadUser(ConvertPath));
        Assert.Equal(Sample with { Id = "Arşiv MKV" }, dosyadan);
        Assert.False(File.Exists(ShrinkPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BosAdKaydedilmiyor(string ad)
    {
        var (kaydedildi, uyari) = Read(window => (window.SaveConvertPreset(ad), window.ConvertPresetNoticeVisible));

        Assert.False(kaydedildi);
        Assert.True(uyari);
        Assert.False(File.Exists(ConvertPath));
    }

    [Fact]
    public void AyniAdOnceSoruyorSonraYaziyor()
    {
        var (ilk, ikinci, ucuncu, sayi) = Read(window =>
            (window.SaveConvertPreset("Aynı"), window.SaveConvertPreset("Aynı"), window.SaveConvertPreset("Aynı"),
                window.ConvertPresets.Count));

        Assert.True(ilk);
        Assert.False(ikinci);
        Assert.True(ucuncu);
        Assert.Equal(1, sayi);
    }

    /// <summary>
    /// Dışa aktarılan dosya başka bir kurulumda (ayrı ön ayar yolu) içe alınır ve aynı değerleri
    /// panele getirir. Aynı ad ezilmiyor, <c>-2</c> ekiyle yanına yazılıyor.
    /// </summary>
    [Fact]
    public void DisaAktarilanBaskaKurulumdaIceAlinir()
    {
        var paylasim = Path.Combine(_folder, "paylasim.json");
        var (disa, bos) = Read(window =>
        {
            var bos = window.ExportConvertPresets(paylasim);
            window.ApplyConvertPreset(Sample);
            window.SaveConvertPreset("Arşiv MKV");
            return (window.ExportConvertPresets(paylasim), bos);
        });
        Assert.False(bos);
        Assert.True(disa);

        var oteki = Path.Combine(_folder, "oteki", "convert-presets.json");
        var (ice, ikinciIce, adlar, geri) = Read(window =>
        {
            var ice = window.ImportConvertPresets(paylasim);
            var ikinciIce = window.ImportConvertPresets(paylasim);
            window.ApplyConvertPreset(new ConversionPreset { Id = "x" });
            window.CmbConvertPreset.SelectedIndex = 0;
            return (ice, ikinciIce, window.ConvertPresets.Select(p => p.Id).ToList(), window.CurrentConvertPreset("Arşiv MKV"));
        }, oteki);

        Assert.True(ice);
        Assert.True(ikinciIce);
        Assert.Equal(new[] { "Arşiv MKV", "Arşiv MKV-2" }, adlar);
        Assert.Equal(Sample with { Id = "Arşiv MKV" }, geri);
        Assert.Equal(2, ConversionPresetLibrary.LoadUser(oteki).Count);
    }

    /// <summary>Küçült ön ayar dosyası Dönüştür'e alınmıyor; uyarı çıkıyor, iki liste de değişmiyor.</summary>
    [Fact]
    public void KucultDosyasiDonustureAlinmaz()
    {
        var kucult = Path.Combine(_folder, "kucult-disa.json");
        PresetLibrary.Export(new[] { new PresetProfile { Id = "kucult", TargetMb = 10 } }, kucult);

        var (alindi, uyari, donustur, kucultSayisi) = Read(window =>
            (window.ImportConvertPresets(kucult), window.ConvertPresetNoticeVisible, window.ConvertPresets.Count, window.UserPresets.Count));

        Assert.False(alindi);
        Assert.True(uyari);
        Assert.Equal(0, donustur);
        Assert.Equal(0, kucultSayisi);
        Assert.False(File.Exists(ConvertPath));
    }

    [Theory]
    [InlineData("main.convert.preset.label")]
    [InlineData("main.convert.preset.placeholder")]
    [InlineData("main.convert.preset.add.tip")]
    public void AnahtarlarButunDillerde(string key)
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
        }
    }
}
