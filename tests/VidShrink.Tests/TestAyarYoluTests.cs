using System.Text;
using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.Tests;

public sealed class TestAyarYoluTests
{
    private static readonly string AppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), UpdateSettings.FolderName);

    private static string Damga(string ad)
    {
        var dosya = new FileInfo(Path.Combine(AppData, ad));
        return dosya.Exists ? $"{dosya.Length}@{dosya.LastWriteTimeUtc.Ticks}" : "yok";
    }

    private static void Kanit(string ad, string metin)
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "ayar-yolu");
        Directory.CreateDirectory(klasor);
        File.WriteAllText(Path.Combine(klasor, ad), metin, new UTF8Encoding(false));
    }

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

    [Fact]
    public void AnaPenceredeAcilanDosyaSonDosyalaraCalismaAltindaYazilirAppDataDegismez()
    {
        var klip = Path.Combine(GorunumKanit.Gecici("ayar-yolu"), "son-dosya.mp4");
        File.Copy(MotorKlipleri.Kucuk, klip);
        var once = Damga(RecentFiles.FileName);

        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var view = window.PlayerTab;
            var gecmis = Path.GetFullPath(view.HistoryPath!.Invoke());
            Assert.Equal(TestAyarYolu.Klasor, Path.GetDirectoryName(gecmis));
            view.EngineFactory = () =>
            {
                var engine = new MpvEngine();
                engine.SetProperty("ao", "null");
                return engine;
            };

            var open = view.OpenAsync(klip);
            DenetimSurucu.Pump(view, () => open.IsCompleted, 20);
            open.GetAwaiter().GetResult();
            DenetimSurucu.Pump(view, () => view.LoadedPath is not null, 5);
            var acilan = view.LoadedPath;
            view.Close();
            window.Close();
            return acilan;
        });

        var son = Path.Combine(TestAyarYolu.Klasor, RecentFiles.FileName);
        var sonra = Damga(RecentFiles.FileName);
        Kanit("son-dosyalar.txt", $"acilan: {sonuc}\nyazilan: {son}\nAppData once: {once}\nAppData sonra: {sonra}\niceriginde klip: {RecentFiles.Load(son).Items.Contains(klip)}\n");

        Assert.Equal(klip, sonuc);
        Assert.Contains(klip, RecentFiles.Load(son).Items);
        Assert.Equal(once, sonra);
    }

    [Fact]
    public void KaydediciAyariCalismaAltinaYazilirAppDataDegismez()
    {
        var yol = RecorderSettings.FilePath;
        Assert.NotNull(yol);
        Assert.Equal(TestAyarYolu.Klasor, Path.GetDirectoryName(Path.GetFullPath(yol!)));
        var once = Damga(RecorderSettings.FileName);
        if (File.Exists(yol)) File.Delete(yol);

        AppHost.Run(() =>
        {
            var view = new RecorderView();
            var window = new Avalonia.Controls.Window { Content = view };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            view.TxtTargetMegabytes.Text = "37";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
            return 0;
        });

        var sonra = Damga(RecorderSettings.FileName);
        var icerik = File.Exists(yol) ? File.ReadAllText(yol!) : "";
        Kanit("kaydedici-ayari.txt", $"yazilan: {yol}\nAppData once: {once}\nAppData sonra: {sonra}\nicerik: {icerik}\n");

        Assert.Equal(37d, RecorderSettings.Load(yol).TargetMegabytes);
        Assert.Equal(once, sonra);
    }
}
