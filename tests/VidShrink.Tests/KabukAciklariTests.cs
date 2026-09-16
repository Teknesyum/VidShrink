using System.Globalization;
using System.Text.RegularExpressions;
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

    [Fact]
    public void AyarlarSimgesiDisliCarktir()
    {
        var yol = IkonKutusuTests.Ikonlar().Single(s => (string)s[0] == "IconSettings")[1] as string;
        var govde = yol!["M 0,0 M 24,24 ".Length..];

        var altYollar = Regex.Matches(govde, @"\bM\s").Count;
        var yalnizNoktalar = Regex.Replace(govde, @"A\s+[\d.]+,[\d.]+\s+-?[\d.]+\s+[01],[01]\s+", "A ");
        var noktalar = Regex.Matches(yalnizNoktalar,@"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)")
            .Select(m => (X: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Y: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .Select(p => Math.Sqrt((p.X - 12) * (p.X - 12) + (p.Y - 12) * (p.Y - 12)))
            .ToList();

        var disUclari = noktalar.Count(r => r > 9);
        var disDipleri = noktalar.Count(r => r is > 6 and < 8);

        Assert.Equal(2, altYollar);
        Assert.Equal(16, disUclari);
        Assert.True(disDipleri >= 16, $"diş dibi {disDipleri}");
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
