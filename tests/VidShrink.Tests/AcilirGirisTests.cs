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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MenuyeGiris(bool azalt)
    {
        var (saydamlik, animasyonlu) = AppHost.Run(() =>
        {
            var satir = new MenuItem { Header = "satır" };
            var menu = new MenuFlyout();
            menu.Items.Add(satir);
            var dugme = new Button { Content = "menü" };
            var pencere = new Window { Content = dugme, Width = 300, Height = 200 };
            if (azalt) pencere.Classes.Add("reduced-motion");
            pencere.Show();
            try
            {
                menu.ShowAt(dugme);
                Dispatcher.UIThread.RunJobs();
                var sunucu = satir.GetVisualAncestors().OfType<MenuFlyoutPresenter>().First();
                var sonuc = (sunucu.Opacity, sunucu.Opacity < 1);
                menu.Hide();
                return sonuc;
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(!azalt, animasyonlu);
        if (azalt) Assert.Equal(1, saydamlik);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IpucuBalonunaGiris(bool azalt)
    {
        var (saydamlik, animasyonlu) = AppHost.Run(() =>
        {
            var ipucu = new ToolTip { Content = "ipucu" };
            var dugme = new Button { Content = "düğme" };
            ToolTip.SetTip(dugme, ipucu);
            var pencere = new Window { Content = dugme, Width = 300, Height = 200 };
            if (azalt) pencere.Classes.Add("reduced-motion");
            pencere.Show();
            try
            {
                ToolTip.SetIsOpen(dugme, true);
                Dispatcher.UIThread.RunJobs();
                Assert.True(ipucu.IsVisible && TopLevel.GetTopLevel(ipucu) is not null);
                var sonuc = (ipucu.Opacity, ipucu.Opacity < 1);
                ToolTip.SetIsOpen(dugme, false);
                return sonuc;
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(!azalt, animasyonlu);
        if (azalt) Assert.Equal(1, saydamlik);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SekmeGecisindeEskiSayfa(bool azalt)
    {
        var (gorunen, saydamlik) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            if (azalt) pencere.Classes.Add("reduced-motion");
            else pencere.Classes.Remove("reduced-motion");
            pencere.Show();
            try
            {
                pencere.Tabs.SelectedIndex = 1;
                Dispatcher.UIThread.RunJobs();
                pencere.Tabs.SelectedIndex = 5;
                Dispatcher.UIThread.RunJobs();
                var ev = pencere.GetVisualDescendants().OfType<TransitioningContentControl>().First(d => d.Name == "SelectedContentHost");
                var sunucular = ev.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Where(s => s.TemplatedParent == ev && s.IsVisible && s.Content is not null)
                    .ToList();
                return (sunucular.Count, sunucular.Min(s => s.Opacity));
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(azalt ? 1 : 2, gorunen);
        if (azalt) Assert.Equal(1, saydamlik);
    }
}
