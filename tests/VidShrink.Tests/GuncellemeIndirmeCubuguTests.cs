using Avalonia.Controls;
using Avalonia.LogicalTree;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// İndirme sürerken panelde dosya listesi yok, yalnız çubuk ve yüzde var; çubuk
/// <see cref="MainWindow.UpdateBarRevealDelay"/> dolmadan görünmez, kısa işte hiç görünmez.
/// İş bitince günlük alanında yalnız sonuç cümlesi durur. Ağa çıkmaz: bildirimler kuyruğa
/// elle konur, kareler sahte saatle sürülür.
/// </summary>
public sealed class GuncellemeIndirmeCubuguTests
{
    private static readonly TimeSpan Kare = TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds);

    private static void Sur(MainWindow p, TimeSpan sure)
    {
        for (var gecen = TimeSpan.Zero; gecen < sure; gecen += Kare) p.UpdateFrame(Kare);
    }

    private static IReadOnlyList<string> Satirlar(MainWindow p) =>
        p.UpdateLogLines.Children.OfType<TextBlock>().Select(s => s.Text ?? "").ToList();

    [Fact]
    public void IndirmeSurerkenDosyaAdiGorunmez()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            p.ShowUpdateProgress(new InstallProgress());
            p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Found, 0.12, 0.20, "9.9.9", Total: 3));
            p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Receiving, 0.50, 0.80, "9.9.9", "vidshrink-update-win-x64.zip", 100, 200));
            p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Downloaded, 0.85, 0.90, "9.9.9", "VidShrink.App.dll", 1, 3));
            Sur(p, TimeSpan.FromSeconds(2));
            var metinler = p.UpdateNotice.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? "").ToList();
            return (satir: Satirlar(p).Count, alan: p.UpdateLogArea.IsVisible, cubuk: p.UpdateProgressRow.IsVisible,
                dosya: metinler.Any(m => m.Contains(".dll", StringComparison.Ordinal) || m.Contains(".zip", StringComparison.Ordinal)),
                yuzde: p.TxtUpdatePercent.Text ?? "");
        });

        Assert.Equal(0, sonuc.satir);
        Assert.False(sonuc.alan);
        Assert.True(sonuc.cubuk);
        Assert.False(sonuc.dosya);
        Assert.Contains("%", sonuc.yuzde);
        Assert.Matches(@"[1-9]", sonuc.yuzde);
    }

    [Fact]
    public void CubukBirSaniyeDolmadanGorunmez()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            var ilerleme = new InstallProgress();
            p.ShowUpdateProgress(ilerleme);
            ilerleme.Step(20, 80, "iniyor");
            Sur(p, MainWindow.UpdateBarRevealDelay - Kare - Kare);
            var once = p.UpdateProgressRow.IsVisible;
            Sur(p, Kare + Kare + Kare);
            return (once, sonra: p.UpdateProgressRow.IsVisible);
        });

        Assert.False(sonuc.once);
        Assert.True(sonuc.sonra);
    }

    [Fact]
    public void KisaIsteCubukHicGorunmezSonucCumlesiKalir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            var ilerleme = new InstallProgress();
            p.ShowUpdateProgress(ilerleme);
            ilerleme.Step(20, 80, "iniyor");
            Sur(p, TimeSpan.FromMilliseconds(300));
            ilerleme.Finish(true, "hazır");
            Sur(p, TimeSpan.FromSeconds(3));
            return (cubuk: p.UpdateProgressRow.IsVisible, alan: p.UpdateLogArea.IsVisible, satirlar: Satirlar(p));
        });

        Assert.False(sonuc.cubuk);
        Assert.True(sonuc.alan);
        Assert.Equal(new[] { "hazır" }, sonuc.satirlar);
    }

    [Fact]
    public void UzunIsBitinceYalnizSonCumleKalir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            var ilerleme = new InstallProgress();
            p.ShowUpdateProgress(ilerleme);
            foreach (var i in Enumerable.Range(1, 12)) ilerleme.Step(i * 5, i * 5 + 5, "adım " + i);
            Sur(p, TimeSpan.FromSeconds(2));
            var surerken = Satirlar(p).Count;
            ilerleme.Finish(false, "düştü");
            Sur(p, TimeSpan.FromSeconds(1));
            return (surerken, cubuk: p.UpdateProgressRow.IsVisible, satirlar: Satirlar(p));
        });

        Assert.Equal(0, sonuc.surerken);
        Assert.True(sonuc.cubuk);
        Assert.Equal(new[] { "düştü" }, sonuc.satirlar);
    }
}
