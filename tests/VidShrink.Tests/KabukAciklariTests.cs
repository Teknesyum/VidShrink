using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class KabukAciklariTests
{
    [Fact]
    public void IndirmeSurerkenGuncellemePaneliKapanmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var goruldu = new List<string>();

                pencere.SetUpdateBadge(UpdateBadgeState.Downloading);
                pencere.UpdateNotice.IsVisible = true;
                goruldu.Add($"inerken-x:{pencere.BtnNoticeDismiss.IsVisible}");
                pencere.BtnNoticeDismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                goruldu.Add($"inerken-panel:{pencere.UpdateNotice.IsVisible}");

                pencere.SetUpdateBadge(UpdateBadgeState.Installing);
                pencere.BtnNoticeDismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                goruldu.Add($"kurarken-panel:{pencere.UpdateNotice.IsVisible}");

                pencere.SetUpdateBadge(UpdateBadgeState.Ready);
                goruldu.Add($"hazir-x:{pencere.BtnNoticeDismiss.IsVisible}");
                pencere.BtnNoticeDismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                goruldu.Add($"hazir-panel:{pencere.UpdateNotice.IsVisible}");
                return goruldu;
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(new[]
        {
            "inerken-x:False", "inerken-panel:True", "kurarken-panel:True", "hazir-x:True", "hazir-panel:False"
        }, sonuc);
    }

    /// <summary>
    /// K4 ölçüsü, Fluent diline taşındı. Eski sürüm yolun metnindeki sayıları sayıyordu;
    /// Fluent'in <c>settings_24_filled</c> çizimi aynı şekli Bezier'le kurduğu için o sayım
    /// artık şekli değil yazımı ölçüyordu. Ölçü şimdi doğrudan dolguyu yokluyor:
    /// merkezde göbek deliği var, çevresinde dolu bir halka, halkanın dışında altı diş.
    /// Diş sayısı ışınsal örneklemede dolu/boş geçiş sayısının yarısıdır.
    /// </summary>
    [Fact]
    public void AyarlarSimgesiDisliCarktir()
    {
        var yol = (string)IkonKutusuTests.Ikonlar().Single(s => (string)s[0] == "IconSettings")[1];
        var govde = yol[IkonKutusuTests.Sabitleyici.Length..];

        AppHost.Ensure();
        var (delik, halka, disler, disUstu) = AppHost.Run(() =>
        {
            var geo = Geometry.Parse(govde);
            bool Dolu(double r, double a) =>
                geo.FillContains(new Avalonia.Point(12 + r * Math.Cos(a), 12 + r * Math.Sin(a)));
            double Oran(double r) => Ornek(r).Count(b => b) / 480.0;
            bool[] Ornek(double r) => Enumerable.Range(0, 480)
                .Select(i => Dolu(r, 2 * Math.PI * i / 480)).ToArray();
            int Gecis(double r)
            {
                var d = Ornek(r);
                return Enumerable.Range(0, d.Length).Count(i => d[i] != d[(i + 1) % d.Length]);
            }
            var disSayilari = new[] { 8.0, 8.5, 9.0, 9.5 }.Select(Gecis).ToArray();
            return (Oran(1.5), Oran(5.0), disSayilari, Oran(10.5));
        });

        Assert.Equal(0.0, delik);
        Assert.Equal(1.0, halka);
        Assert.Equal(0.0, disUstu);
        Assert.All(disler, g => Assert.Equal(12, g));
    }

    [Fact]
    public void HakkindaYayinlananHerPlatformuYazarVeBuKurulumuIsaretler()
    {
        var yayin = File.ReadAllText(Path.Combine(TipSources.Root, ".github", "workflows", "release.yml"));
        var matris = Regex.Match(yayin, @"rid:\s*\[(?<liste>[^\]]+)\]").Groups["liste"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(matris.OrderBy(r => r), UpdateCheck.ReleasedRids.OrderBy(r => r));

        var metin = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                pencere.RefreshPlatforms();
                return pencere.TxtPlatforms.Text ?? "";
            }
            finally { pencere.Close(); }
        });

        var satirlar = metin.Split('\n');
        Assert.Equal(matris.Length, satirlar.Length);
        foreach (var rid in matris)
            Assert.Single(satirlar, s => s.StartsWith(MainWindow.PlatformName(rid), StringComparison.Ordinal));
        var isaretli = satirlar.Where(s => s.Contains('←')).ToList();
        Assert.Single(isaretli);
        Assert.StartsWith(MainWindow.PlatformName(UpdateCheck.Rid), isaretli[0], StringComparison.Ordinal);
    }
}
