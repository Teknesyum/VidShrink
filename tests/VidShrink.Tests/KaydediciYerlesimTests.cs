using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciYerlesimTests
{
    private static readonly ScreenBounds Birincil = new(0, 0, 0, 1920, 1080);
    private static readonly ScreenBounds SoldakiAyniOlcek = new(1, -1280, 0, 1280, 1024);
    private static readonly ScreenBounds SoldakiYuksekOlcek = new(1, -2880, 0, 2880, 1620);

    private static readonly IReadOnlyList<ScreenBounds> TekEkran = new[] { Birincil };
    private static readonly IReadOnlyList<ScreenBounds> IkiEkran = new[] { Birincil, SoldakiAyniOlcek };
    private static readonly IReadOnlyList<ScreenBounds> IkiEkranKarisikOlcek = new[] { Birincil, SoldakiYuksekOlcek };

    private static string Cikti => Path.Combine(Path.GetTempPath(), "yerlesim.mp4");

    private static RecorderRequest Ekran(IReadOnlyList<ScreenBounds> ekranlar, int indeks) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen,
        ScreenIndex = indeks,
        Screens = ekranlar
    };

    private static RecorderRequest Bolge(IReadOnlyList<ScreenBounds> ekranlar, RecorderRegion bolge) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = bolge,
        Screens = ekranlar
    };

    private static string? Arguman(IReadOnlyList<string> uretilen, string ad)
    {
        for (var i = 0; i + 1 < uretilen.Count; i++)
            if (uretilen[i] == ad)
                return uretilen[i + 1];
        return null;
    }

    private static string Olcu(int genislik, int yukseklik)
        => genislik.ToString(CultureInfo.InvariantCulture) + "x" + yukseklik.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void IkiEkranliMasaustundeIlkEkranSecimiTekMonitoreDaralir()
    {
        var uretilen = RecorderArguments.Build(Ekran(IkiEkran, 0), Cikti);
        var birlesim = RecorderLayout.Union(IkiEkran)!;

        Assert.Equal(Olcu(Birincil.Width, Birincil.Height), Arguman(uretilen, "-video_size"));
        Assert.Equal(Birincil.X.ToString(CultureInfo.InvariantCulture), Arguman(uretilen, "-offset_x"));
        Assert.Equal(Birincil.Y.ToString(CultureInfo.InvariantCulture), Arguman(uretilen, "-offset_y"));
        Assert.NotEqual(Olcu(birlesim.Width, birlesim.Height), Arguman(uretilen, "-video_size"));
    }

    [Fact]
    public void NegatifXtekiMonitorSecimiNegatifOfsetleYakalanir()
    {
        var uretilen = RecorderArguments.Build(Ekran(IkiEkran, 1), Cikti);

        Assert.Equal(SoldakiAyniOlcek.X.ToString(CultureInfo.InvariantCulture), Arguman(uretilen, "-offset_x"));
        Assert.Equal(Olcu(SoldakiAyniOlcek.Width, SoldakiAyniOlcek.Height), Arguman(uretilen, "-video_size"));
        Assert.True(int.Parse(Arguman(uretilen, "-offset_x")!, CultureInfo.InvariantCulture) < 0);
    }

    [Fact]
    public void TekEkranliMasaustundeEkranHedefiOfsetsizKalir()
    {
        var uretilen = RecorderArguments.Build(Ekran(TekEkran, 0), Cikti);

        Assert.Null(Arguman(uretilen, "-offset_x"));
        Assert.Null(Arguman(uretilen, "-video_size"));
        Assert.Equal("desktop", Arguman(uretilen, "-i"));
    }

    [Fact]
    public void MonitorListesindeOlmayanIndeksSifirOlsaDaReddedilir()
    {
        var eksik = new[] { SoldakiAyniOlcek };
        var hatalar = RecorderArguments.Validate(Ekran(eksik, 0), Cikti);

        Assert.Contains(hatalar, h => h.Contains("monitor 0", StringComparison.Ordinal));
        Assert.Empty(RecorderArguments.Validate(Ekran(eksik, SoldakiAyniOlcek.Index), Cikti));
    }

    [Fact]
    public void NegatifXtekiMonitordeCizilenBolgeKabulEdilir()
    {
        var bolge = new RecorderRegion(SoldakiAyniOlcek.X + 80, 100, 640, 480);

        Assert.True(RecorderLayout.Covered(IkiEkran, bolge));
        Assert.Empty(RecorderArguments.Validate(Bolge(IkiEkran, bolge), Cikti));
        Assert.Contains("Region offsets cannot be negative.",
            RecorderArguments.Validate(Bolge(Array.Empty<ScreenBounds>(), bolge), Cikti));
    }

    [Fact]
    public void MonitorlerArasiBosluktakiBolgeSiyahDiyeReddedilir()
    {
        var bosluk = new RecorderRegion(
            SoldakiAyniOlcek.X + SoldakiAyniOlcek.Width - 640,
            SoldakiAyniOlcek.Y + SoldakiAyniOlcek.Height,
            640,
            Birincil.Height - SoldakiAyniOlcek.Height);

        Assert.False(RecorderLayout.Covered(IkiEkran, bosluk));
        Assert.Contains(RecorderArguments.Validate(Bolge(IkiEkran, bosluk), Cikti),
            h => h.Contains("not fully covered", StringComparison.Ordinal));
    }

    [Fact]
    public void MasaustuDisinaTasanBolgeReddedilir()
    {
        var tasan = new RecorderRegion(Birincil.X + Birincil.Width - 320, 100, 640, 480);

        Assert.False(RecorderLayout.Covered(TekEkran, tasan));
        Assert.Contains(RecorderArguments.Validate(Bolge(TekEkran, tasan), Cikti),
            h => h.Contains("not fully covered", StringComparison.Ordinal));
    }

    [Fact]
    public void IkiMonitoreYayilanBolgeKabulEdilir()
    {
        var yayilan = new RecorderRegion(SoldakiAyniOlcek.X + SoldakiAyniOlcek.Width - 320, 100, 640, 480);

        Assert.True(RecorderLayout.Covered(IkiEkran, yayilan));
        Assert.Empty(RecorderArguments.Validate(Bolge(IkiEkran, yayilan), Cikti));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void TekEkranliOrtuMasaustunuTamKapliyor(double olcek)
    {
        var ekran = new ScreenBounds(0, 0, 0, (int)(1920 * olcek), (int)(1080 * olcek));
        var yerlesim = new[] { new ScreenPlacement(ekran, olcek) };
        var ortu = RecorderLayout.Cover(yerlesim)!;
        var birlesim = RecorderLayout.Union(new[] { ekran })!;

        var kaplama = ortu.Physical(RecorderLayout.ScaleAt(yerlesim, birlesim.X, birlesim.Y)!.Value);

        Assert.Equal(birlesim.Width, kaplama.Width);
        Assert.Equal(birlesim.Height, kaplama.Height);
        Assert.Equal(birlesim.X, kaplama.X);
    }

    [Fact]
    public void KarisikOlcekliOrtuKosedekiMonitorunCarpaniniKullanir()
    {
        var yerlesim = new[]
        {
            new ScreenPlacement(Birincil, 1.0),
            new ScreenPlacement(SoldakiYuksekOlcek, 1.5)
        };

        var birlesim = RecorderLayout.Union(IkiEkranKarisikOlcek)!;
        var kosedekiOlcek = RecorderLayout.ScaleAt(yerlesim, birlesim.X, birlesim.Y)!.Value;
        var ortu = RecorderLayout.Cover(yerlesim)!;
        var kaplama = ortu.Physical(kosedekiOlcek);

        Assert.Equal(kosedekiOlcek, ortu.Scale);
        Assert.NotEqual(yerlesim[0].Scale, ortu.Scale);
        Assert.Equal(birlesim.Width, kaplama.Width);
        Assert.Equal(birlesim.Height, kaplama.Height);

        var birincilinCarpaniyla = new OverlayCover(
            birlesim.X, birlesim.Y,
            birlesim.Width / yerlesim[0].Scale,
            birlesim.Height / yerlesim[0].Scale,
            yerlesim[0].Scale).Physical(kosedekiOlcek);

        Assert.NotEqual(birlesim.Width, birincilinCarpaniyla.Width);
    }

    [Fact]
    public void KosedekiMonitorEnGenisDegilkenDeKosedekininCarpaniSecilir()
    {
        var genisBirincil = new ScreenBounds(0, 0, 0, 3840, 2160);
        var darDizustu = new ScreenBounds(1, -1920, 0, 1920, 1200);
        var yerlesim = new[]
        {
            new ScreenPlacement(genisBirincil, 1.0),
            new ScreenPlacement(darDizustu, 1.5)
        };

        var birlesim = RecorderLayout.Union(new[] { genisBirincil, darDizustu })!;
        var ortu = RecorderLayout.Cover(yerlesim)!;

        Assert.Equal(darDizustu.X, birlesim.X);
        Assert.True((long)genisBirincil.Width * genisBirincil.Height
                    > (long)darDizustu.Width * darDizustu.Height);

        Assert.Equal(1.5, ortu.Scale);
        Assert.NotEqual(1.0, ortu.Scale);

        var kaplama = ortu.Physical(1.5);
        Assert.Equal(birlesim.Width, kaplama.Width);
        Assert.Equal(birlesim.Height, kaplama.Height);
    }

    [Fact]
    public void BirlesimKosesiBoslugaDusunceEnBuyukAlanliMonitorunCarpaniAlinir()
    {
        var ustSagdaki = new ScreenBounds(0, 0, 0, 1920, 1080);
        var altSoldaki = new ScreenBounds(1, -2880, 1080, 2880, 1620);
        var yerlesim = new[]
        {
            new ScreenPlacement(ustSagdaki, 1.0),
            new ScreenPlacement(altSoldaki, 1.5)
        };

        var birlesim = RecorderLayout.Union(new[] { ustSagdaki, altSoldaki })!;

        Assert.Null(RecorderLayout.Containing(new[] { ustSagdaki, altSoldaki }, birlesim.X, birlesim.Y));
        Assert.Null(RecorderLayout.ScaleAt(yerlesim, birlesim.X, birlesim.Y));

        var ortu = RecorderLayout.Cover(yerlesim)!;

        Assert.Equal(1.5, ortu.Scale);
        Assert.NotEqual(yerlesim[0].Scale, ortu.Scale);
        Assert.Equal(birlesim.Width / 1.5, ortu.Width);
        Assert.Equal(birlesim.Height / 1.5, ortu.Height);
    }

    [Fact]
    public void BirlesimNegatifXtekiMonitoruIceriyor()
    {
        var birlesim = RecorderLayout.Union(IkiEkran)!;

        Assert.Equal(SoldakiAyniOlcek.X, birlesim.X);
        Assert.Equal(Birincil.X + Birincil.Width - SoldakiAyniOlcek.X, birlesim.Width);
        Assert.Equal(Math.Max(Birincil.Height, SoldakiAyniOlcek.Height), birlesim.Height);
    }

    [Fact]
    public void NoktaTasiyanMonitorSinirdaSecilir()
    {
        Assert.Equal(SoldakiAyniOlcek.Index,
            RecorderLayout.Containing(IkiEkran, SoldakiAyniOlcek.X, 0)!.Index);
        Assert.Equal(Birincil.Index,
            RecorderLayout.Containing(IkiEkran, SoldakiAyniOlcek.X + SoldakiAyniOlcek.Width, 0)!.Index);
        Assert.Null(RecorderLayout.Containing(IkiEkran, SoldakiAyniOlcek.X - 1, 0));
    }

    [Fact]
    public void MacOstaNegatifBolgeOfsetiHalaReddedilir()
    {
        var istek = Bolge(IkiEkran, new RecorderRegion(SoldakiAyniOlcek.X + 80, 100, 640, 480))
            with { Platform = RecorderPlatform.MacOs };

        Assert.Contains("Region offsets cannot be negative.", RecorderArguments.Validate(istek, Cikti));
    }
}
