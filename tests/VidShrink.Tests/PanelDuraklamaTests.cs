using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class PanelDuraklamaTests
{
    private static (HoverZone bolge, List<bool> gorunum) Kur()
    {
        var gorunum = new List<bool>();
        var bolge = new HoverZone(0.25, () => TimeSpan.Zero, () => TimeSpan.Zero, gorunum.Add);
        return (bolge, gorunum);
    }

    [Fact]
    public void DuraklamakPaneliAcmaz()
    {
        var (bolge, gorunum) = Kur();

        bolge.Keep(true);

        Assert.False(bolge.IsVisible);
        Assert.Empty(gorunum);
    }

    [Fact]
    public void FareBandaGelinceAcilir()
    {
        var (bolge, _) = Kur();
        bolge.Keep(true);

        bolge.PointerAt(95, 100);

        Assert.True(bolge.IsVisible);
    }

    [Fact]
    public void DurakliykenAcikPanelFareGidinceKapanmaz()
    {
        var (bolge, _) = Kur();
        bolge.Keep(true);
        bolge.PointerAt(95, 100);

        bolge.PointerAt(10, 100);

        Assert.True(bolge.IsVisible);
    }

    [Fact]
    public void OynarkenFareGidinceKapanir()
    {
        var (bolge, _) = Kur();
        bolge.PointerAt(95, 100);

        bolge.PointerAt(10, 100);

        Assert.False(bolge.IsVisible);
    }
}
