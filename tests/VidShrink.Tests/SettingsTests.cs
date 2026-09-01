using System.Globalization;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Kök nesne olmayan ayar dosyası açılışı yarıda bırakmaz.
    ///
    /// <para><c>UpdateSettings.Load</c> yalnız <c>JsonException</c>,
    /// <c>IOException</c> ve <c>UnauthorizedAccessException</c> yakalıyordu. Geçerli
    /// JSON ama kök nesne değilse — <c>null</c>, sayı, dizi, dizge —
    /// <c>TryGetProperty</c> <c>InvalidOperationException</c> atıyor ve o tür o
    /// listede yok. Çağıranın genel <c>catch</c>'i çökmeyi engelliyordu, ama açılış
    /// o noktada kesiliyor: ayarlar da, güncelleme arayüzü de, açılışta verilen
    /// dosya da yüklenmiyordu.</para>
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"metin\"")]
    [InlineData("true")]
    public void ASettingsFileThatIsNotAnObjectFallsBackToTheDefaults(string content)
    {
        var file = SettingsFile();
        try
        {
            File.WriteAllText(file, content);

            var settings = UpdateSettings.Load(file);

            Assert.Equal(new UpdateSettings().TargetMb, settings.TargetMb);
            Assert.Equal(new UpdateSettings().Intent, settings.Intent);
            Assert.True(settings.AutoUpdate);
            Assert.Null(settings.FastGpu);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    private sealed record Restored(
        string Name,
        Action<UpdateSettings> Change,
        Func<MainWindow, object?> Read,
        object? Default);

    /// <summary>
    /// Sıfırlamanın dokunması gereken denetimlerin tamamı; yanlarında varsayılan değerleri.
    /// Varsayılanlar <see cref="UpdateSettings"/> alan değerlerinden bağımsız yazılıyor:
    /// aynı kaynaktan okunsaydı ölçü, sıfırlamanın kendi hatasını da doğru sayardı.
    /// </summary>
    private static IReadOnlyList<Restored> RestoredControls() =>
    [
        new("TxtTarget", s => s.TargetMb = 88, w => w.TxtTarget.Text, "16"),
        new("TxtQualityTarget", s => s.QualityTarget = 91, w => w.TxtQualityTarget.Text, "60"),
        new("CmbIntent", s => s.Intent = 2, w => w.CmbIntent.SelectedIndex, 1),
        new("CmbCodec", s => s.Codec = 1, w => w.CmbCodec.SelectedIndex, 0),
        new("ChkResolution", s => s.MayLowerResolution = false, w => w.ChkResolution.IsChecked, true),
        new("ChkFps", s => s.MayLowerFps = false, w => w.ChkFps.IsChecked, true),
        new("ChkFastGpu", s => s.FastGpu = true, w => w.ChkFastGpu.IsChecked, false),
        new("CmbFillPolicy", s => s.FillPolicy = 1, w => w.CmbFillPolicy.SelectedIndex, 0),
        new("CmbHdrPolicy", s => s.HdrPolicy = 1, w => w.CmbHdrPolicy.SelectedIndex, 0),
        new("CmbQualityMode", s => s.QualityMode = 1, w => w.CmbQualityMode.SelectedIndex, 0),
        new("TxtQuality", s => s.QualityValue = 2500, w => w.TxtQuality.Text, "23"),
        new("CmbContainer", s => s.Container = 1, w => w.CmbContainer.SelectedIndex, 0),
        new("CmbConvertCodec", s => s.ConvertCodec = 2, w => w.CmbConvertCodec.SelectedIndex, 0),
        new("CmbResolution", s => s.Resolution = 4, w => w.CmbResolution.SelectedIndex, 0),
        new("TxtCustomResolution", s => s.CustomResolution = "1024x576", w => w.TxtCustomResolution.Text, "1280x720"),
        new("CmbConvertFps", s => s.ConvertFps = 2, w => w.CmbConvertFps.SelectedIndex, 0),
        new("TxtCustomFps", s => s.CustomFps = "29.97", w => w.TxtCustomFps.Text, "25"),
        new("CmbConvertAudio", s => s.ConvertAudio = 2, w => w.CmbConvertAudio.SelectedIndex, 0),
        new("TxtAudioBitrate", s => s.AudioBitrate = "192", w => w.TxtAudioBitrate.Text, "128"),
        new("TxtTrimStart", s => s.TrimStart = "00:00:03", w => w.TxtTrimStart.Text, ""),
        new("TxtTrimEnd", s => s.TrimEnd = "00:01:00", w => w.TxtTrimEnd.Text, ""),
        new("CmbShareTarget", s => s.ShareTarget = 1, w => w.CmbShareTarget.SelectedIndex, 0),
        new("CmbShareRetention", s => s.ShareRetention = 2, w => w.CmbShareRetention.SelectedIndex, 0),
        new("ChkAutoUpdate", s => s.AutoUpdate = false, w => w.ChkAutoUpdate.IsChecked, true),
    ];

    /// <summary>
    /// Sıfırlama yirmi dört denetimin hepsini varsayılana döndürür.
    ///
    /// <para>Önceki hali üçüne bakıyordu ve <c>ChkFastGpu</c> sıfırlanmadan geçiyordu:
    /// <c>RestoreSettings</c> içindeki <c>HasValue</c> kapısı boş ayarda kutuya hiç
    /// dokunmuyordu, kullanıcının değeri sıfırlamadan sağ çıkıp bir sonraki kayıtta
    /// geri yazılıyordu.</para>
    ///
    /// <para>Ölçü iki yönlü: önce her denetimin varsayılandan gerçekten uzaklaştığı
    /// doğrulanır — uzaklaşmayan denetim için sıfırlama iddiası boştur — sonra hepsinin
    /// geri döndüğü. Bir denetim eklenip tabloya yazılmazsa ölçü onu hiç görmez; tablo
    /// bu yüzden denetim adlarını taşıyor.</para>
    /// </summary>
    [Fact]
    public void ResetRestoresEveryControlToItsDefault()
    {
        var controls = RestoredControls();
        var file = SettingsFile();
        try
        {
            var changed = new UpdateSettings { Language = "tr" };
            foreach (var control in controls) control.Change(changed);
            changed.Save(file);

            var (unmoved, stuck) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreSettingsForTest(UpdateSettings.Load(file));

                    var same = controls
                        .Where(control => Equals(control.Read(window), control.Default))
                        .Select(control => control.Name)
                        .ToList();

                    window.ConfirmResetSettingsForTest();

                    var left = controls
                        .Where(control => !Equals(control.Read(window), control.Default))
                        .Select(control => $"{control.Name} = {control.Read(window)}, beklenen {control.Default}")
                        .ToList();

                    return (same, left);
                }
                finally { window.Close(); }
            });

            Assert.False(File.Exists(file));

            Assert.True(
                unmoved.Count == 0,
                "Bu denetimler varsayılandan hiç uzaklaşmadı, sıfırlama iddiası onlar için boş: "
                + string.Join(", ", unmoved));

            Assert.True(
                stuck.Count == 0,
                $"{stuck.Count} denetim sıfırlanmadı:" + Environment.NewLine
                + string.Join(Environment.NewLine, stuck));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    /// <summary>
    /// Sıfırlama dili de varsayılana döndürür. Denetim tablosu dili kapsamıyor —
    /// <c>ChkFastGpu</c> tam bu boşlukta sessizce kalmıştı.
    /// </summary>
    [Fact]
    public void ResetReturnsTheLanguageToTheOperatingSystemDefault()
    {
        var file = SettingsFile();
        try
        {
            var expected = MainWindow.ResolveLanguage(null, CultureInfo.CurrentUICulture.Name);
            var other = expected == "en" ? "tr" : "en";
            new UpdateSettings { Language = other }.Save(file);

            var (before, after) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreSettingsForTest(UpdateSettings.Load(file));
                    var moved = VidShrink.App.Localization.Strings.Language;
                    window.ConfirmResetSettingsForTest();
                    return (moved, VidShrink.App.Localization.Strings.Language);
                }
                finally { window.Close(); }
            });

            Assert.Equal(other, before);
            Assert.Equal(expected, after);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    /// <summary>
    /// Sıfırlama akışının dört metni var: başlık, onay sorusu, onay düğmesi, vazgeç.
    /// Ölçü ikisine bakıyordu; kalan ikisi bir dilden düşse sessiz geçerdi.
    /// </summary>
    [Fact]
    public void ResetCopyComesFromBothLocaleFiles()
    {
        string[] keys =
        [
            "settings.reset-all",
            "settings.reset-confirm",
            "settings.reset-confirm-button",
            "settings.reset-cancel",
        ];

        foreach (var language in new[] { "en", "tr" })
        {
            var path = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales", language, "settings.json");
            // Alt dizge araması yetmiyor: "settings.reset-cancel-x" da
            // "settings.reset-cancel" içeriyor. Anahtar kümesi okunur, eşitlik aranır.
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var present = document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in keys)
                Assert.True(present.Contains(key), $"{language}/settings.json: {key} yok.");
        }
    }

    /// <summary>
    /// İpucu ölçüsünün iki eşiği <b>ayrı</b> okunur ve ikisi de temadan gelir.
    ///
    /// <para>Eskiden ölçü tek eşikle çalışıyordu: dışarıda bırakılan küme "tavanı
    /// aşanlar", içeride kalan küme "aşmayanlar" olarak tanımlanıyor ve iddia her zaman
    /// doğru çıkıyordu. Ayrıca tavan ölçüye <c>746</c> diye elle yazılmıştı — belirteç
    /// değişse ölçü eski sayıyı savunurdu.</para>
    ///
    /// <para>Bugünkü ölçü şunu söylüyor: sarma genişliği balondan dar olmalı, ikisi de
    /// <c>Theme.axaml</c>'den bağımsız hesapla aynı çıkmalı, ve <b>sarılamayan</b> satır
    /// bulunmamalı. Sarılamayan satır tek görsel satırda kalıp sarma genişliğini aşandır:
    /// içinde balona sığmayan tek bir kelime vardır, sessizce kırpılır. Böyle satır varsa
    /// adıyla yazılır.</para>
    /// </summary>
    [Fact]
    public void TipLineThresholdsComeFromTheThemeAndEveryLineCanWrap()
    {
        var theme = File.ReadAllText(TipSources.ThemePath);

        var balloon = ThemeNumber(theme, "TooltipMaxWidth");
        var padding = double.Parse(
            ThemeText(theme, "TooltipPadding").Split(',')[0], CultureInfo.InvariantCulture);
        var border = ThemeNumber(theme, "BorderThin");

        Assert.Equal(balloon, TipLineMetrics.Balloon);
        Assert.Equal(balloon - (2 * padding) - (2 * border), TipLineMetrics.Ceiling);

        Assert.True(
            TipLineMetrics.Ceiling < TipLineMetrics.Balloon,
            $"Sarma genişliği {TipLineMetrics.Ceiling} ile balon genişliği {TipLineMetrics.Balloon} "
            + "aynı; eşik kendi kendini doldurur.");

        var measured = TipLineMetrics.MeasureAll();
        Assert.NotEmpty(measured);

        var unwrappable = measured
            .Where(line => line.VisualLines == 1 && line.Width > TipLineMetrics.Ceiling)
            .Select(line => $"{line.Source} [{line.Language}] satır {line.LineIndex}: "
                + $"{line.Width:0} px, sarma genişliği {TipLineMetrics.Ceiling:0} px · {line.Text}")
            .ToList();

        Assert.True(
            unwrappable.Count == 0,
            $"{unwrappable.Count} satır balona sarılamıyor, kırpılır:" + Environment.NewLine
            + string.Join(Environment.NewLine, unwrappable));
    }

    private static double ThemeNumber(string theme, string key) =>
        double.Parse(ThemeText(theme, key), CultureInfo.InvariantCulture);

    private static string ThemeText(string theme, string key)
    {
        var match = Regex.Match(theme, $"x:Key=\"{Regex.Escape(key)}\"[^>]*>([^<]+)<");
        Assert.True(match.Success, $"{key} belirteci Theme.axaml içinde yok.");
        return match.Groups[1].Value.Trim();
    }
}
