using System;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public sealed class GelistiriciSekmesiTests
{
    [Fact]
    public void EsigeKadarAcilmaz()
    {
        var kilit = new DeveloperUnlock();
        var an = DateTimeOffset.UnixEpoch;
        for (var i = 1; i < DeveloperUnlock.Threshold; i++)
            Assert.False(kilit.Tap(an += TimeSpan.FromMilliseconds(200)));

        Assert.True(kilit.Tap(an + TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void AraAcilinca_SayacSifirlanir()
    {
        var kilit = new DeveloperUnlock();
        var an = DateTimeOffset.UnixEpoch;
        for (var i = 1; i < DeveloperUnlock.Threshold; i++)
            kilit.Tap(an += TimeSpan.FromMilliseconds(200));

        Assert.False(kilit.Tap(an + DeveloperUnlock.Window + TimeSpan.FromMilliseconds(1)));
        Assert.Equal(1, kilit.Count);
    }

    [Fact]
    public void AcildiktanSonra_SayacBastanBaslar()
    {
        var kilit = new DeveloperUnlock();
        var an = DateTimeOffset.UnixEpoch;
        for (var i = 0; i < DeveloperUnlock.Threshold; i++)
            kilit.Tap(an += TimeSpan.FromMilliseconds(200));

        Assert.Equal(0, kilit.Count);
        Assert.False(kilit.Tap(an + TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void PencereSiniri_TamEsittekiBasisiKabulEder()
    {
        var kilit = new DeveloperUnlock();
        var an = DateTimeOffset.UnixEpoch;
        kilit.Tap(an);
        kilit.Tap(an + DeveloperUnlock.Window);
        Assert.Equal(2, kilit.Count);
    }

    [Fact]
    public void SekmeGizliBaslar_VeKapatmaDugmesiVar()
    {
        var axaml = System.IO.File.ReadAllText(TipSources.WindowXamlPath);
        Assert.Contains("x:Name=\"TabAdvanced\" IsVisible=\"False\"", axaml);
        Assert.Contains("x:Name=\"BtnHideAdvanced\"", axaml);
        Assert.Contains("PointerPressed=\"OnSystemStatusTapped\"", axaml);
    }
}
