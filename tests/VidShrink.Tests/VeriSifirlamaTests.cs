using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// "Tüm verileri sıfırla" yalnız settings.json'u değil, VidShrink'in klasöre yazdığı her
/// veri dosyasını siler. Klasör hep test kökünde: gerçek %APPDATA%\VidShrink'e dokunulmaz.
/// </summary>
public sealed class VeriSifirlamaTests
{
    private static string Folder()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "veri-sifirlama", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void Touch(string path) => File.WriteAllText(path, "{}");

    [Fact]
    public void TestlerGercekAppDataKlasorunuGormez()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink");
        var folder = AppDataReset.DefaultFolder;

        Assert.NotNull(folder);
        Assert.NotEqual(Path.GetFullPath(real), Path.GetFullPath(folder!), StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(TestPaths.OutputRoot), Path.GetFullPath(folder!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListeKullanicininSaydigiHerDosyayiTasir()
    {
        string[] expected =
        [
            "settings.json", "player-settings.json", "player-advanced.json", "player-recent.json",
            "player-tools.json", "player-history.json", "layout.json", "recorder-settings.json",
            "dismissed-update.txt", "paylasimlar.json", "opensubtitles-session.dat",
        ];

        Assert.Equal(expected.OrderBy(x => x), AppDataReset.FileNames.OrderBy(x => x));
    }

    [Fact]
    public void SifirlamaHerVeriDosyasiniSilerBaskasinaDokunmaz()
    {
        var folder = Folder();
        foreach (var name in AppDataReset.FileNames) Touch(Path.Combine(folder, name));
        var broken = Path.Combine(folder, "paylasimlar.json.bozuk-20260917120000000");
        Touch(broken);
        var log = Path.Combine(folder, "update-log.txt");
        var journal = Path.Combine(folder, ".update-pending.json");
        Touch(log);
        Touch(journal);

        var removed = AppDataReset.Run(folder);

        var left = AppDataReset.FileNames.Where(name => File.Exists(Path.Combine(folder, name))).ToList();
        Assert.Empty(left);
        Assert.False(File.Exists(broken), "bozuk paylasim defteri kopyasi kaldi");
        Assert.Equal(AppDataReset.FileNames.Count + 1, removed.Count);
        Assert.True(File.Exists(log), "guncelleme gunlugu silindi");
        Assert.True(File.Exists(journal), "yarim guncelleme kaydi silindi");
    }

    [Fact]
    public void AyarlardakiOnayDugmesiOynaticiVeKaydediciVerisiniDeSiler()
    {
        var folder = Folder();
        var settings = Path.Combine(folder, "settings-ozel.json");
        new UpdateSettings { AutoUpdate = true }.Save(settings);
        string[] others = ["player-settings.json", "player-history.json", "layout.json", "recorder-settings.json", "paylasimlar.json"];
        foreach (var name in others) Touch(Path.Combine(folder, name));
        var foreign = Path.Combine(folder, "kullanicinin-videosu.mp4");
        Touch(foreign);

        var (settingsLeft, othersLeft, foreignLeft) = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = settings };
            try
            {
                window.ConfirmResetSettingsForTest();
                return (File.Exists(settings),
                    others.Where(name => File.Exists(Path.Combine(folder, name))).ToList(),
                    File.Exists(foreign));
            }
            finally { window.Close(); }
        });

        Assert.False(settingsLeft, "ayar dosyasi kaldi");
        Assert.Empty(othersLeft);
        Assert.True(foreignLeft, "veri olmayan dosya silindi");
    }
}
