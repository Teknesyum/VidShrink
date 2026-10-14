using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Ayar yedeği: settings.json her başarılı okumadan sonra <c>.yedek</c>'e yazılır, bozuk dosya
/// silinmeden yanına ayrılır ve yedekten dönülür; Ayarlar'daki dışa/içe aktarma sıfırlamadan
/// sonra dosyaları geri getirir, gizli anahtarları pakete koymaz. Klasör hep test kökünde.
/// </summary>
public sealed class AyarYedegiTests
{
    private static string Folder()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "ayar-yedegi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void Cleanup(string folder)
    {
        try { Directory.Delete(folder, true); } catch (IOException) { }
    }

    [Fact]
    public void BasariliOkumaYedekYazar()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            const string original = "{\"outputNamePattern\":\"{name}-yedek\"}";
            File.WriteAllText(file, original);

            var settings = AppSettings.Load(file);

            Assert.Equal("{name}-yedek", settings.OutputNamePattern);
            Assert.Equal(original, File.ReadAllText(SettingsBackup.BackupPath(file)));
            File.Delete(SettingsBackup.BackupPath(file));
            AppSettings.Load(file);
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(SettingsBackup.BackupPath(file)));
            Assert.Equal(SettingsRecovery.None, SettingsBackup.TakePending(file));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void BozukDosyaYedektenDonerVeYaninaAyrilir()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-saglam\"}");
            AppSettings.Load(file);
            File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-bozuk\"");

            var settings = AppSettings.Load(file);

            Assert.Equal("{name}-saglam", settings.OutputNamePattern);
            var broken = Assert.Single(SettingsBackup.BrokenCopies(file));
            Assert.Equal("{\"outputNamePattern\":\"{name}-bozuk\"", File.ReadAllText(broken));
            Assert.Equal(SettingsRecovery.Restored, SettingsBackup.TakePending(file));
            Assert.Equal(SettingsRecovery.None, SettingsBackup.TakePending(file));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void YedeksizBozukDosyaVarsayilanaDonerAmaSilinmez()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "[1,2");

            var settings = AppSettings.Load(file);

            Assert.Equal(new AppSettings().OutputNamePattern, settings.OutputNamePattern);
            var broken = Assert.Single(SettingsBackup.BrokenCopies(file));
            Assert.Equal("[1,2", File.ReadAllText(broken));
            Assert.Equal(SettingsRecovery.Defaulted, SettingsBackup.TakePending(file));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void DisaAktarSifirlaIceAktarTuruDosyalariGeriGetirir()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-paket\",\"openSubtitlesApiKey\":\"gizli-anahtar\",\"openSubtitlesUser\":\"kullanici\"}");
            File.WriteAllText(Path.Combine(folder, "layout.json"), "{\"genislik\":321}");
            File.WriteAllText(Path.Combine(folder, "player-recent.json"), "[\"a.mp4\"]");
            File.WriteAllText(Path.Combine(folder, "opensubtitles-session.dat"), "oturum-belirteci");
            File.WriteAllText(Path.Combine(folder, "paylasimlar.json"), "[{\"ownerToken\":\"silme-jetonu\"}]");
            var zipPath = Path.Combine(folder, "paket.zip");

            var exported = AppDataArchive.Export(zipPath, folder, file);

            Assert.Equal(new[] { "layout.json", "player-recent.json", "settings.json" }, exported.OrderBy(x => x, StringComparer.Ordinal));
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var names = zip.Entries.Select(entry => entry.FullName).ToList();
                Assert.DoesNotContain("opensubtitles-session.dat", names);
                Assert.DoesNotContain("paylasimlar.json", names);
                using var reader = new StreamReader(zip.GetEntry("settings.json")!.Open());
                var packed = reader.ReadToEnd();
                Assert.DoesNotContain("gizli-anahtar", packed, StringComparison.Ordinal);
                Assert.DoesNotContain("openSubtitlesApiKey", packed, StringComparison.Ordinal);
                Assert.Contains("kullanici", packed, StringComparison.Ordinal);
            }

            AppDataReset.Run(folder, file);
            Assert.False(File.Exists(file));
            Assert.False(File.Exists(Path.Combine(folder, "layout.json")));
            File.WriteAllText(file, "{\"openSubtitlesApiKey\":\"yeni-anahtar\"}");

            var imported = AppDataArchive.Import(zipPath, folder, file);

            Assert.Equal(3, imported.Count);
            Assert.Equal("{\"genislik\":321}", File.ReadAllText(Path.Combine(folder, "layout.json")));
            Assert.Equal("[\"a.mp4\"]", File.ReadAllText(Path.Combine(folder, "player-recent.json")));
            Assert.False(File.Exists(Path.Combine(folder, "opensubtitles-session.dat")));
            Assert.False(File.Exists(Path.Combine(folder, "paylasimlar.json")));
            var settings = AppSettings.Load(file);
            Assert.Equal("{name}-paket", settings.OutputNamePattern);
            Assert.Equal("kullanici", settings.OpenSubtitlesUser);
            Assert.Equal("yeni-anahtar", settings.OpenSubtitlesApiKey);
        }
        finally { Cleanup(folder); }
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("ozel-ayar.json")]
    public void SifirlamaYedegiVeBozukKopyalariDaSiler(string name)
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, name);
            File.WriteAllText(file, "{}");
            AppSettings.Load(file);
            File.WriteAllText(file, "bozuk");
            AppSettings.Load(file);
            SettingsBackup.TakePending(file);
            Assert.True(File.Exists(SettingsBackup.BackupPath(file)));
            Assert.Single(SettingsBackup.BrokenCopies(file));

            AppDataReset.Run(folder, file);

            Assert.False(File.Exists(SettingsBackup.BackupPath(file)));
            Assert.Empty(SettingsBackup.BrokenCopies(file));
        }
        finally { Cleanup(folder); }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"yarim\":")]
    public void BozukPaketReddedilirDosyalaraDokunulmaz(string packedSettings)
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-yerinde\"}");
            File.WriteAllText(Path.Combine(folder, "layout.json"), "{\"eski\":1}");
            var zipPath = Path.Combine(folder, "bozuk.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("layout.json").Open())) writer.Write("{\"yeni\":1}");
                using (var writer = new StreamWriter(zip.CreateEntry("settings.json").Open())) writer.Write(packedSettings);
            }

            Assert.Throws<InvalidDataException>(() => AppDataArchive.Import(zipPath, folder, file));

            Assert.Equal("{\"outputNamePattern\":\"{name}-yerinde\"}", File.ReadAllText(file));
            Assert.Equal("{\"eski\":1}", File.ReadAllText(Path.Combine(folder, "layout.json")));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void ZipOlmayanPaketReddedilir()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "{}");
            var zipPath = Path.Combine(folder, "duz.zip");
            File.WriteAllText(zipPath, "zip degil");

            Assert.Throws<InvalidDataException>(() => AppDataArchive.Import(zipPath, folder, file));
            Assert.Equal("{}", File.ReadAllText(file));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void PaketYalnizSifirlamaListesindenUretilir()
    {
        Assert.All(AppDataArchive.EntryNames, name => Assert.Contains(name, AppDataReset.FileNames));
        Assert.Equal(AppDataReset.FileNames.Count - AppDataArchive.SecretFiles.Count, AppDataArchive.EntryNames.Count);
        Assert.DoesNotContain("opensubtitles-session.dat", AppDataArchive.EntryNames);
        Assert.DoesNotContain("paylasimlar.json", AppDataArchive.EntryNames);
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    /// <summary>
    /// Bilerek bozulmuş settings.json ile açılış: pencere yedekteki değerle açılır ve uyarı
    /// şeridi görünür. Ardından pencerenin kendi dışa/içe aktarma yolu bir tur koşar.
    /// </summary>
    [Fact]
    public void BozukAyarlaAcilisUyariGosterirVePencereTuruKosar()
    {
        var folder = Folder();
        try
        {
            var file = Path.Combine(folder, "settings.json");
            File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-pencere\"}");
            File.Copy(file, SettingsBackup.BackupPath(file));
            File.WriteAllText(file, "{bozuk");
            var zipPath = Path.Combine(folder, "pencere.zip");

            var (gorunur, metin, beklenen, exported, imported, desen, durum) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file, Width = 1280, Height = 900 };
                try
                {
                    window.Show();
                    Dongu(() => window.IsLoaded, 10);
                    Dongu(() => false, 0.3);
                    var gorunur = window.SettingsRecoveryNotice.IsVisible;
                    var metin = window.TxtSettingsRecovery.Text;
                    var beklenen = LanguageCatalog.Display(Strings.Get(MainWindow.SettingsRecoveryRestoredKey));
                    var exported = window.ExportSettings(zipPath);
                    File.WriteAllText(file, "{\"outputNamePattern\":\"{name}-degisti\"}");
                    var imported = window.ImportSettings(zipPath);
                    return (gorunur, metin, beklenen, exported, imported, AppSettings.Load(file).OutputNamePattern, window.TxtSettingsBackup.Text);
                }
                finally { window.Close(); }
            });

            Assert.True(gorunur);
            Assert.Equal(beklenen, metin);
            Assert.True(exported);
            Assert.True(imported, durum);
            Assert.Equal("{name}-pencere", desen);
            Assert.Single(SettingsBackup.BrokenCopies(file));
        }
        finally { Cleanup(folder); }
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "settings.backup.export", "settings.backup.import", "settings.backup.file-type",
            "settings.backup.exported", "settings.backup.imported", "settings.backup.invalid",
            "settings.error.export", "settings.error.import",
            MainWindow.SettingsRecoveryRestoredKey, MainWindow.SettingsRecoveryDefaultedKey,
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.Contains("{0}", values["settings.backup.exported"], StringComparison.Ordinal);
            Assert.Contains("{0}", values["settings.backup.imported"], StringComparison.Ordinal);
            Assert.NotEqual(values[MainWindow.SettingsRecoveryRestoredKey], values[MainWindow.SettingsRecoveryDefaultedKey]);
        }
    }
}
