using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Açılır pencereler (ön ayar kaydetme, kaydedici seçenekleri) saydamdan süzülerek girer;
/// hareketi azaltma açık pencerede ilk karede tam görünür.
/// </summary>
public sealed class AcilirGirisTests
{
    private static (double saydamlik, bool animasyonlu) Ac(Window pencere, Button dugme, bool azalt)
    {
        if (azalt) pencere.Classes.Add("reduced-motion");
        else pencere.Classes.Remove("reduced-motion");
        pencere.Show();
        var acilir = (Flyout)dugme.Flyout!;
        acilir.ShowAt(dugme);
        Dispatcher.UIThread.RunJobs();
        var sunucu = ((Control)acilir.Content!).GetVisualAncestors().OfType<FlyoutPresenter>().First();
        var sonuc = (sunucu.Opacity, sunucu.Opacity < 1);
        acilir.Hide();
        return sonuc;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnAyarAcilirinaGiris(bool azalt)
    {
        var (saydamlik, animasyonlu) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            try { return Ac(pencere, pencere.ChipAddPreset, azalt); }
            finally { pencere.Close(); }
        });

        Assert.Equal(!azalt, animasyonlu);
        if (azalt) Assert.Equal(1, saydamlik);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KaydediciSecenekAcilirinaGiris(bool azalt)
    {
        var (saydamlik, animasyonlu) = AppHost.Run(() =>
        {
            var mini = new VidShrink.App.Recorder.RecorderMini();
            try { return Ac(mini, mini.BtnOptions, azalt); }
            finally { mini.Close(); }
        });

        Assert.Equal(!azalt, animasyonlu);
        if (azalt) Assert.Equal(1, saydamlik);
    }
}
