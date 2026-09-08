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
                AdvCodecLock = 3
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
}
