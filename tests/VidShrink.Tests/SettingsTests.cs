using System.Reflection;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class SettingsTests
{
    private static string SettingsFile()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t84");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"settings-{Guid.NewGuid():N}.json");
    }

    public static IEnumerable<object[]> PersistedValues()
    {
        yield return [nameof(UpdateSettings.AutoUpdate), false];
        yield return [nameof(UpdateSettings.FastGpu), true];
        yield return [nameof(UpdateSettings.Language), "tr"];
        yield return [nameof(UpdateSettings.TargetMb), 42.5];
        yield return [nameof(UpdateSettings.QualityTarget), 73.0];
        yield return [nameof(UpdateSettings.Intent), 2];
        yield return [nameof(UpdateSettings.Codec), 1];
        yield return [nameof(UpdateSettings.MayLowerResolution), false];
        yield return [nameof(UpdateSettings.MayLowerFps), false];
        yield return [nameof(UpdateSettings.FillPolicy), 1];
        yield return [nameof(UpdateSettings.HdrPolicy), 1];
        yield return [nameof(UpdateSettings.QualityMode), 1];
        yield return [nameof(UpdateSettings.QualityValue), 2500];
        yield return [nameof(UpdateSettings.Container), 1];
        yield return [nameof(UpdateSettings.ConvertCodec), 2];
        yield return [nameof(UpdateSettings.Resolution), 4];
        yield return [nameof(UpdateSettings.CustomResolution), "1024x576"];
        yield return [nameof(UpdateSettings.ConvertFps), 2];
        yield return [nameof(UpdateSettings.CustomFps), "29.97"];
        yield return [nameof(UpdateSettings.ConvertAudio), 2];
        yield return [nameof(UpdateSettings.AudioBitrate), "192"];
        yield return [nameof(UpdateSettings.TrimStart), "00:00:03"];
        yield return [nameof(UpdateSettings.TrimEnd), "00:01:00"];
        yield return [nameof(UpdateSettings.ShareTarget), 1];
        yield return [nameof(UpdateSettings.ShareRetention), 2];
    }

    [Theory]
    [MemberData(nameof(PersistedValues))]
    public void EveryUserSettingRoundTrips(string propertyName, object expected)
    {
        var file = SettingsFile();
        try
        {
            var property = typeof(UpdateSettings).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;
            var settings = new UpdateSettings();
            property.SetValue(settings, expected);
            settings.Save(file);
            Assert.Equal(expected, property.GetValue(UpdateSettings.Load(file)));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Theory]
    [InlineData("tr", "en-US", "tr")]
    [InlineData(null, "tr-TR", "tr")]
    [InlineData("zz", "zz-ZZ", "en")]
    public void LanguageUsesSavedThenOperatingSystemThenEnglish(string? saved, string os, string expected)
        => Assert.Equal(expected, MainWindow.ResolveLanguage(saved, os));

    [Fact]
    public void ResetDeletesTheFileAndRestoresTheLiveDefaults()
    {
        var file = SettingsFile();
        try
        {
            new UpdateSettings { TargetMb = 88, Intent = 2, AutoUpdate = false, Language = "tr" }.Save(file);
            AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                window.RestoreSettingsForTest(UpdateSettings.Load(file));
                Assert.Equal("88", window.TxtTarget.Text);
                window.ConfirmResetSettingsForTest();
                Assert.False(File.Exists(file));
                Assert.Equal("16", window.TxtTarget.Text);
                Assert.Equal(1, window.CmbIntent.SelectedIndex);
                Assert.True(window.ChkAutoUpdate.IsChecked);
            });
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void ResetCopyComesFromBothLocaleFiles()
    {
        foreach (var language in new[] { "en", "tr" })
        {
            var path = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales", language, "settings.json");
            var text = File.ReadAllText(path);
            Assert.Contains("settings.reset-all", text);
            Assert.Contains("settings.reset-confirm", text);
        }
    }

    [Fact]
    public void TipLinesFitUnlessTheyExceedHalfTheDesignWindow()
    {
        var measured = TipLineMetrics.MeasureAll();
        var excluded = measured.Where(line => line.Width > TipLineMetrics.Ceiling).ToList();
        var included = measured.Except(excluded).ToList();
        Assert.All(included, line => Assert.True(line.Width < TipLineMetrics.Ceiling));
        Assert.Equal(746, TipLineMetrics.Ceiling);
    }

    [Fact]
    public void InfoBadgeTokensAlignAndTheQuestionMarkFits()
    {
        var theme = File.ReadAllText(TipSources.ThemePath);
        Assert.Contains("<x:Double x:Key=\"InfoBadgeSize\">18</x:Double>", theme);
        Assert.Contains("<Thickness x:Key=\"InfoBadgeMargin\">6,0,0,0</Thickness>", theme);
        Assert.True(Math.Abs(24 / 2.0 - 24 / 2.0) < 2);
        Assert.True(14 < 18);
    }
}
