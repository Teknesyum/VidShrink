using System.Text.RegularExpressions;
using VidShrink.App;

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
            "CmbAdvMode", "CmbAdvCrf", "CmbAdvPreset", "CmbAdvAudioKbps", "CmbAdvAudioChannels",
            "CmbAdvMinResolution", "CmbAdvMinFps", "CmbAdvEncoderPath", "CmbAdvCodecLock"
        };
        var missing = boxes.Where(box => !code.Contains(box)).ToList();
        Assert.True(missing.Count == 0, "Kod arkasinda gecmeyen kutu: " + string.Join(", ", missing));

        var count = Regex.Matches(code, "Watch\\(box, SelectingItemsControl.SelectedIndexProperty, SaveSettings\\);").Count;
        Assert.True(count >= 1, "Dokuz gelismis kutuyu SaveSettings uzerinden kaydeden foreach dongusu bulunamadi.");
    }

    [Fact]
    public void AyarlarSekmesiDilDenetimiKalicidir()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains("SettingsLangSwitch.Children.Clear()", code);
        Assert.Contains("panel.Children.Add(button)", code);
        Assert.Contains("new[] { LangSwitch, SettingsLangSwitch }", code);
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
}
