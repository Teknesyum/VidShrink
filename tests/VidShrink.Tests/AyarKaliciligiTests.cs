using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class AyarKaliciligiTests
{
    private static string SettingsFile()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t173");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    public static IEnumerable<object[]> AppSettingsValues()
    {
        yield return new object[] { nameof(AppSettings.AdvMode), 1 };
        yield return new object[] { nameof(AppSettings.AdvCrf), 2 };
        yield return new object[] { nameof(AppSettings.AdvPreset), 3 };
        yield return new object[] { nameof(AppSettings.AdvAudioKbps), 4 };
        yield return new object[] { nameof(AppSettings.AdvAudioChannels), 1 };
        yield return new object[] { nameof(AppSettings.AdvMinResolution), 2 };
        yield return new object[] { nameof(AppSettings.AdvMinFps), 1 };
        yield return new object[] { nameof(AppSettings.AdvEncoderPath), 2 };
        yield return new object[] { nameof(AppSettings.AdvCodecLock), 3 };
    }

    [Theory]
    [MemberData(nameof(AppSettingsValues))]
    public void DokuzGelismisSecimGeriYuklenir(string propertyName, int expected)
    {
        var file = SettingsFile();
        try
        {
            var property = typeof(AppSettings).GetProperty(propertyName)!;
            var settings = new AppSettings();
            property.SetValue(settings, expected);
            settings.Save(file);

            var loaded = AppSettings.Load(file);
            Assert.Equal(expected, (int)property.GetValue(loaded)!);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void UpdateSettingsKaydiAppSettingsAnahtarlariniSilmez()
    {
        var file = SettingsFile();
        try
        {
            new VidShrink.Core.UpdateSettings { TargetMb = 77 }.Save(file);
            new AppSettings { AdvCrf = 5 }.Save(file);

            var appSettings = AppSettings.Load(file);
            Assert.Equal(5, appSettings.AdvCrf);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void YongaTagDegerleriArtanSiradadir()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);
        var matches = Regex.Matches(xaml, "x:Name=\"Chip[A-Za-z0-9]*\"[^>]*Tag=\"(\\d+)\"");
        var tags = matches.Select(m => int.Parse(m.Groups[1].Value)).ToList();

        Assert.True(tags.Count >= 5, "En az bes sayisal yonga bekleniyordu, " + tags.Count + " bulundu.");
        Assert.Equal(tags.OrderBy(t => t), tags);
    }

    [Fact]
    public void GelismisKutularArayuzdeKalicidir()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        var boxes = new[]
        {
            "CmbAdvCrf", "CmbAdvPreset", "CmbAdvAudioKbps", "CmbAdvAudioChannels",
            "CmbAdvMinResolution", "CmbAdvMinFps", "CmbAdvCodecLock",
            "RbAdvModeAuto", "RbAdvModeCrf", "RbAdvModeTwoPass",
            "RbAdvPathAuto", "RbAdvPathSoftware", "RbAdvPathHardware"
        };
        var missing = boxes.Where(box => !code.Contains(box)).ToList();
        Assert.True(missing.Count == 0, "Kod arkasinda gecmeyen kalem: " + string.Join(", ", missing));

        var count = Regex.Matches(code, "Watch\\(box, SelectingItemsControl.SelectedIndexProperty, SaveSettings\\);").Count;
        Assert.True(count >= 1, "Gelismis kutulari SaveSettings uzerinden kaydeden foreach dongusu bulunamadi.");

        var strips = Regex.Matches(code, "Watch" + Regex.Escape("(toggle, ToggleButton.IsCheckedProperty, SaveSettings);")).Count;
        Assert.True(strips >= 1, "Gelismis seritleri SaveSettings uzerinden kaydeden foreach dongusu bulunamadi.");
    }

    [Fact]
    public void AyarlarSekmesiDilDenetimiKalicidir()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains("LangSwitch.Children.Clear()", code);
        Assert.Contains("LangSwitch.Children.Add(button)", code);
        Assert.Contains("Strings.ShortcutLanguages", code);
        Assert.Contains("CmbLanguage.ItemsSource", code);
        Assert.Contains("Watch(CmbLanguage, SelectingItemsControl.SelectedIndexProperty, OnLanguageChosen);", code);
        Assert.Contains("SaveSettings();", code);
    }

    [Fact]
    public void SifirlaDugmesiDokuzGelismisKutuyuDaVarsayilanaDondurur()
    {
        var file = SettingsFile();
        try
        {
            new AppSettings
            {
                AdvMode = 1,
                AdvCrf = 2,
                AdvPreset = 3,
                AdvAudioKbps = 4,
                AdvAudioChannels = 1,
                AdvMinResolution = 2,
                AdvMinFps = 1,
                AdvEncoderPath = 2,
                AdvCodecLock = 3,
                AdvKeepTracks = true
            }.Save(file);

            var result = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreAppSettingsForTest(AppSettings.Load(file));
                    var beforeReset = window.CaptureAppSettingsForTest();
                    window.ConfirmResetSettingsForTest();
                    var afterReset = window.CaptureAppSettingsForTest();
                    return (beforeReset, afterReset);
                }
                finally { window.Close(); }
            });

            Assert.NotEqual(0, result.beforeReset.AdvCrf);
            Assert.Equal(0, result.afterReset.AdvMode);
            Assert.Equal(0, result.afterReset.AdvCrf);
            Assert.Equal(0, result.afterReset.AdvPreset);
            Assert.Equal(0, result.afterReset.AdvAudioKbps);
            Assert.Equal(0, result.afterReset.AdvAudioChannels);
            Assert.Equal(0, result.afterReset.AdvMinResolution);
            Assert.Equal(0, result.afterReset.AdvMinFps);
            Assert.Equal(0, result.afterReset.AdvEncoderPath);
            Assert.Equal(0, result.afterReset.AdvCodecLock);
            Assert.True(result.beforeReset.AdvKeepTracks);
            Assert.False(result.afterReset.AdvKeepTracks);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void IzleriKoruKapanipAcilanPenceredeGeriGelir()
    {
        var file = SettingsFile();
        var previous = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", file);
        try
        {
            var reading = AppHost.Run(() =>
            {
                var fresh = new MainWindow();
                bool freshValue;
                try
                {
                    fresh.RestoreAppSettingsForTest(AppSettings.Load());
                    freshValue = fresh.ChkAdvKeepTracks.IsChecked == true;
                }
                finally { fresh.Close(); }

                var first = new MainWindow();
                try { first.ChkAdvKeepTracks.IsChecked = true; }
                finally { first.Close(); }

                var second = new MainWindow();
                try
                {
                    second.RestoreAppSettingsForTest(AppSettings.Load());
                    return (Fresh: freshValue, Reopened: second.ChkAdvKeepTracks.IsChecked == true);
                }
                finally { second.Close(); }
            });

            Assert.False(reading.Fresh);
            Assert.True(File.Exists(file), "Ayar VIDSHRINK_SETTINGS_PATH'in gosterdigi dosyaya yazilmadi.");
            Assert.Contains("\"advKeepTracks\": true", File.ReadAllText(file));
            Assert.True(reading.Reopened, "Izleri koru acik kapanan pencere kapali acildi.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", previous);
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void IzleriKoruYalnizGercekMantiksalDegerdenOkunur()
    {
        var file = SettingsFile();
        try
        {
            File.WriteAllText(file, "{ \"advKeepTracks\": \"true\", \"advCrf\": 2 }");
            var yazili = AppSettings.Load(file);
            Assert.False(yazili.AdvKeepTracks);
            Assert.Equal(2, yazili.AdvCrf);

            File.WriteAllText(file, "{ \"advKeepTracks\": false }");
            Assert.False(AppSettings.Load(file).AdvKeepTracks);

            File.WriteAllText(file, "{ \"advKeepTracks\": true }");
            Assert.True(AppSettings.Load(file).AdvKeepTracks);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    /// <summary>
    /// T177 tur 2 borcu: <c>Intent</c> kaydediliyordu, tavansızlık bayrağı kaydedilmiyordu.
    /// Arşiv seçip uygulamayı kapatan kullanıcı, niyeti Arşiv ama türetme satırı
    /// "Hedef X MB" olan bir pencereyle açılıyordu. İki alan da aynı dosyadan dönmeli.
    /// </summary>
    [Fact]
    public void ArsivSeciliKapananUygulamaArsivIleAciliyor()
    {
        var file = SettingsFile();
        try
        {
            var reading = AppHost.Run(() =>
            {
                var first = new MainWindow { SettingsPathOverride = file };
                try
                {
                    first.UseTurkish();
                    first.ChipArchive.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                finally { first.Close(); }

                var saved = UpdateSettings.Load(file);

                var second = new MainWindow { SettingsPathOverride = file };
                try
                {
                    second.UseTurkish();
                    second.RestoreSettingsForTest(saved);
                    return (Saved: saved, second.SelectedIntentIndex, Line: second.TxtChipDerivation.Text ?? "");
                }
                finally { second.Close(); }
            });

            Assert.False(
                reading.Saved.ChipSizeCapped,
                "Arşiv seçiliyken tavansızlık bayrağı ayar dosyasına yazılmadı.");
            Assert.Equal((int)Intent.Archive, reading.SelectedIntentIndex);
            Assert.Contains("tavanı yok", reading.Line, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    /// <summary>
    /// Tavansızlık kalıcı değil: hedefe dokunan kullanıcı tavanı geri getirir ve o hâl de
    /// kaydedilir. Tek yönlü kaydeden bir alan Arşiv'i hapse çevirirdi.
    /// </summary>
    [Fact]
    public void HedefeDokunulduktanSonraTavanKaydediliyor()
    {
        var file = SettingsFile();
        try
        {
            var saved = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.UseTurkish();
                    window.ChipArchive.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.TxtTarget.Text = "12";
                }
                finally { window.Close(); }

                return UpdateSettings.Load(file);
            });

            Assert.True(saved.ChipSizeCapped, "Hedefe dokunulduktan sonra tavan ayara geri yazılmadı.");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    private static MediaInfo BuyukKaynak() => new()
    {
        FilePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv",
        FileSizeBytes = 420_000_000L,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    /// <summary>
    /// Hipersürüş G: 60 kaliteyle kaydedilmiş ayar dosyası bir video açılıp dil
    /// değiştirilince 78,3 olarak geri yazılıyordu. Kaynağın önerdiği hedef ve ondan
    /// türeyen kalite ekranda kalır, dosyaya kullanıcının kendi sayıları gider.
    /// </summary>
    [Fact]
    public void KaynakYuklemekKayitliHedefVeKaliteyiDegistirmez()
    {
        var file = SettingsFile();
        try
        {
            var (ekranHedef, ekranKalite, saved) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreSettingsForTest(new UpdateSettings { TargetMb = 24, QualityTarget = 60 });
                    var info = BuyukKaynak();
                    window.LoadWithoutProbing(info.FilePath, info);
                    window.UseTurkish();
                    return (window.TxtTarget.Text ?? "", window.TxtQualityTarget.Text ?? "", UpdateSettings.Load(file));
                }
                finally { window.Close(); }
            });

            Assert.Equal("16", ekranHedef);
            Assert.NotEqual("60", ekranKalite);
            Assert.True(File.Exists(file), "Dil değişimi ayar dosyasını yazmadı; ölçü boşa koştu.");
            Assert.Equal(24, saved.TargetMb);
            Assert.Equal(60, saved.QualityTarget);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    /// <summary>
    /// Negatif kontrol: kullanıcının kendi yazdığı kalite ve hedef dosyaya gider. Kaliteden
    /// türeyen hedef ve hedeften türeyen kalite ise gitmez.
    /// </summary>
    [Fact]
    public void ElleYazilanHedefVeKaliteKaydediliyor()
    {
        var file = SettingsFile();
        try
        {
            var (kalite, hedef) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreSettingsForTest(new UpdateSettings { TargetMb = 24, QualityTarget = 60 });
                    var info = BuyukKaynak();
                    window.LoadWithoutProbing(info.FilePath, info);
                    window.TxtQualityTarget.Text = "73";
                    var birinci = UpdateSettings.Load(file);
                    window.TxtTarget.Text = "30";
                    return (birinci, UpdateSettings.Load(file));
                }
                finally { window.Close(); }
            });

            Assert.Equal(73, kalite.QualityTarget);
            Assert.Equal(24, kalite.TargetMb);
            Assert.Equal(30, hedef.TargetMb);
            Assert.Equal(73, hedef.QualityTarget);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
