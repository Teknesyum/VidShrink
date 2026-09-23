using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Hareket azaltma kodla kurulan geçişlerde de geçerli: <c>Fade</c> hareket azaltılmış pencerede
/// yüzeyi hemen gizler ve geçiş kurmaz, iş penceresi de <c>reduced-motion</c> sınıfını alır.
/// </summary>
public sealed class HareketAzaltmaKapisiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FadeHareketAzaltmadaAnlik(bool azalt)
    {
        var (gizliHemen, gecisYok, saydamlik) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            try
            {
                if (azalt) pencere.Classes.Add("reduced-motion");
                else pencere.Classes.Remove("reduced-motion");
                var yuzey = new Border { IsVisible = true, Opacity = 1 };
                pencere.Fade(yuzey, false);
                var gizli = !yuzey.IsVisible;
                pencere.Fade(yuzey, true);
                Dispatcher.UIThread.RunJobs();
                return (gizli, yuzey.Transitions is null, yuzey.Opacity);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(azalt, gizliHemen);
        Assert.Equal(azalt, gecisYok);
        if (azalt) Assert.Equal(1, saydamlik);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsPenceresiHareketAzaltmaSinifiniAlir(bool azalt)
    {
        var sinifli = AppHost.Run(() =>
        {
            var onceki = HoverZone.MotionReduced;
            HoverZone.MotionReduced = azalt;
            try
            {
                var pencere = new ShrinkJobWindow();
                try { return pencere.Classes.Contains("reduced-motion"); }
                finally { pencere.Close(); }
            }
            finally { HoverZone.MotionReduced = onceki; }
        });

        Assert.Equal(azalt, sinifli);
    }
}
