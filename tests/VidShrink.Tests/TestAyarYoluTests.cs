using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class TestAyarYoluTests
{
    private static readonly string AppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), UpdateSettings.FolderName);

    [Fact]
    public void AyarYoluCalismaAltindaKullanicininAppDatasindaDegil()
    {
        var yol = Path.GetFullPath(UpdateSettings.DefaultPath);

        Assert.Equal(TestAyarYolu.Klasor, Path.GetDirectoryName(yol));
        Assert.StartsWith(Path.Combine(TipSources.Root, ".calisma"), yol, StringComparison.OrdinalIgnoreCase);
        Assert.False(yol.StartsWith(AppData, StringComparison.OrdinalIgnoreCase), yol);
        Assert.True(Directory.Exists(TestAyarYolu.Klasor));
    }

    [Fact]
    public void YapicidanGelenOynaticiGecmisiVeSonDosyalarCalismaAltinaYazilir()
    {
        var klasor = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var gecmis = window.PlayerTab.HistoryPath?.Invoke();
            Assert.NotNull(gecmis);
            return Path.GetDirectoryName(Path.GetFullPath(gecmis!));
        });

        Assert.Equal(TestAyarYolu.Klasor, klasor);
        var son = Path.Combine(klasor!, RecentFiles.FileName);
        new RecentFiles().Save(son);
        Assert.True(File.Exists(son), son);
        Assert.False(son.StartsWith(AppData, StringComparison.OrdinalIgnoreCase), son);
    }
}
