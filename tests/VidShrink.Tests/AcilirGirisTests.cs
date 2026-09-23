using System.Diagnostics;
using Avalonia;
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
    /// <summary>
    /// Saydamlık örneklenmez, her değişikliği kaydedilir: CI yükünde geçiş ilk
    /// <c>RunJobs</c> içinde bitiyor ve örnekleme döngüsü hiç ara kare görmüyordu.
    /// </summary>
    private sealed class SaydamlikKaydi<T> : IDisposable where T : Avalonia.Visual
    {
        private readonly IDisposable _abonelik;
        public double EnAz { get; private set; } = 1;

        public SaydamlikKaydi() => _abonelik = Avalonia.Visual.OpacityProperty.Changed
            .AddClassHandler<T>((gorsel, _) => EnAz = Math.Min(EnAz, gorsel.Opacity));

        public void Gor(T gorsel) => EnAz = Math.Min(EnAz, gorsel.Opacity);

        public void Dispose() => _abonelik.Dispose();
    }

    private static void Bekle(TimeSpan sure)
    {
        var saat = Stopwatch.StartNew();
        while (saat.Elapsed < sure)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static (double saydamlik, bool animasyonlu) Ac(Window pencere, Button dugme, bool azalt)
    {
        if (azalt) pencere.Classes.Add("reduced-motion");
        else pencere.Classes.Remove("reduced-motion");
        pencere.Show();
        var acilir = (Flyout)dugme.Flyout!;
        using var kayit = new SaydamlikKaydi<FlyoutPresenter>();
        acilir.ShowAt(dugme);
        Dispatcher.UIThread.RunJobs();
        kayit.Gor(((Control)acilir.Content!).GetVisualAncestors().OfType<FlyoutPresenter>().First());
        Bekle(TimeSpan.FromMilliseconds(400));
        acilir.Hide();
        return (kayit.EnAz, kayit.EnAz < 1);
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
            using var kayit = new SaydamlikKaydi<ToolTip>();
            try
            {
                ToolTip.SetIsOpen(dugme, true);
                Dispatcher.UIThread.RunJobs();
                Assert.True(ipucu.IsVisible && TopLevel.GetTopLevel(ipucu) is not null);
                kayit.Gor(ipucu);
                Bekle(TimeSpan.FromMilliseconds(400));
                var sonuc = (kayit.EnAz, kayit.EnAz < 1);
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
                List<Avalonia.Controls.Presenters.ContentPresenter> Gorunenler() => ev.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .Where(s => s.TemplatedParent == ev && s.IsVisible && s.Content is not null)
                    .ToList();
                var ilk = Gorunenler();
                var enCok = ilk.Count;
                var enAz = ilk.Min(s => s.Opacity);
                var saat = Stopwatch.StartNew();
                while (saat.Elapsed.TotalMilliseconds < 400)
                {
                    using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
                    Dispatcher.UIThread.MainLoop(dilim.Token);
                    var simdi = Gorunenler();
                    enCok = Math.Max(enCok, simdi.Count);
                    if (simdi.Count > 0) enAz = Math.Min(enAz, simdi.Min(s => s.Opacity));
                }
                return (enCok, enAz);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(azalt ? 1 : 2, gorunen);
        if (azalt) Assert.Equal(1, saydamlik);
    }
}
