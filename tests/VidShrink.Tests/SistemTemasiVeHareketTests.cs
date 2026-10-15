using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.App.Themes;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// AYR-P4: sistem temasını izleme ve hareket ayarı. Gerçek sistem teması ve gerçek sistem
/// ayarı okunmaz; ikisi de sahte kaynakla verilir.
/// </summary>
public sealed class SistemTemasiVeHareketTests
{
    private sealed class SahteHareket : MotionPreference.ISource
    {
        public bool? Windows { get; init; }
        public string? Mac { get; init; }
        public string? Gnome { get; init; }

        public bool? WindowsClientAreaAnimation() => Windows;
        public string? MacReduceMotion() => Mac;
        public string? GnomeEnableAnimations() => Gnome;
    }

    private sealed class SahteTema : ISystemThemeSource
    {
        private bool? _acik;

        public bool? IsLight => _acik;

        public event EventHandler? Changed;

        public void Ver(bool? acik)
        {
            _acik = acik;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(null, false)]
    public void WindowsKoluIstemciAlaniCanlandirmasiniOkur(bool? acik, bool azaltilmis)
        => Assert.Equal(azaltilmis, MotionPreference.ReadSystem(MotionPreference.Platform.Windows, new SahteHareket { Windows = acik }));

    [Theory]
    [InlineData("1\n", true)]
    [InlineData("0\n", false)]
    [InlineData(null, false)]
    public void MacKoluHareketiAzaltAyariniOkur(string? cikti, bool azaltilmis)
        => Assert.Equal(azaltilmis, MotionPreference.ReadSystem(MotionPreference.Platform.MacOS, new SahteHareket { Mac = cikti }));

    [Theory]
    [InlineData("false\n", true)]
    [InlineData("true\n", false)]
    [InlineData(null, false)]
    public void LinuxKoluGnomeCanlandirmaAyariniOkur(string? cikti, bool azaltilmis)
        => Assert.Equal(azaltilmis, MotionPreference.ReadSystem(MotionPreference.Platform.Linux, new SahteHareket { Gnome = cikti }));

    [Fact]
    public void KollarBirbirininKaynaginiOkumaz()
    {
        var hepsiAzalt = new SahteHareket { Windows = false, Mac = "1", Gnome = "false" };
        Assert.False(MotionPreference.ReadSystem(MotionPreference.Platform.Other, hepsiAzalt));
        Assert.False(MotionPreference.ReadSystem(MotionPreference.Platform.MacOS, new SahteHareket { Windows = false, Gnome = "false" }));
        Assert.False(MotionPreference.ReadSystem(MotionPreference.Platform.Linux, new SahteHareket { Windows = false, Mac = "1" }));
    }

    [Fact]
    public void ElleAnahtarSistemIzinliykenDeCanlandirmayiKapatir()
    {
        var onceki = MotionPreference.Source;
        try
        {
            MotionPreference.Source = new SahteHareket { Windows = true, Mac = "0", Gnome = "true" };
            MotionPreference.ResetForTest();
            Assert.False(MotionPreference.Reduced);
            Assert.False(HoverZone.MotionReduced);

            MotionPreference.UserReduced = true;
            Assert.True(MotionPreference.Reduced);
            Assert.True(HoverZone.MotionReduced);
        }
        finally
        {
            MotionPreference.Source = onceki;
            MotionPreference.ResetForTest();
        }
    }

    [Theory]
    [InlineData(false, true, "Nord")]
    [InlineData(true, true, "Buz")]
    [InlineData(true, false, "Dracula")]
    [InlineData(true, null, "Nord")]
    public void PaletSecimiSistemTemasinaGoreAyrilir(bool izle, bool? sistemAcik, string beklenen)
        => Assert.Equal(beklenen, PaletteCatalog.Pick("Nord", izle, sistemAcik, "Buz", "Dracula"));

    [Fact]
    public void VarsayilanAcikPaletAcikZeminli()
    {
        var acik = AppHost.Run(() => PaletteCatalog.IsLight(PaletteCatalog.DefaultLight));
        Assert.True(acik);
    }

    [Fact]
    public void IzleyiciSistemDegisinceYalnizUseIleGecer()
    {
        var (ilk, acikta, koyuda, kapaninca) = AppHost.Run(() =>
        {
            var baslangic = PaletteCatalog.Current;
            var kaynak = new SahteTema();
            kaynak.Ver(false);
            using var izleyici = new SystemThemeFollower(kaynak) { Chosen = "Nord", Light = "Buz", Dark = "Dracula" };
            try
            {
                izleyici.Enabled = true;
                var a = PaletteCatalog.Current;
                kaynak.Ver(true);
                var b = PaletteCatalog.Current;
                kaynak.Ver(false);
                var c = PaletteCatalog.Current;
                izleyici.Enabled = false;
                kaynak.Ver(true);
                var d = PaletteCatalog.Current;
                return (a, b, c, d);
            }
            finally { PaletteCatalog.Use(baslangic); }
        });

        Assert.Equal("Dracula", ilk);
        Assert.Equal("Buz", acikta);
        Assert.Equal("Dracula", koyuda);
        Assert.Equal("Nord", kapaninca);
    }

    [Fact]
    public void AyarDosyasiYeniAnahtarlariTasir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "ayr-p4", Guid.NewGuid().ToString("N"));
        var dosya = Path.Combine(klasor, "settings.json");
        try
        {
            var bos = AppSettings.Load(dosya);
            Assert.False(bos.FollowSystemTheme);
            Assert.False(bos.ReduceMotion);
            Assert.Equal(PaletteCatalog.DefaultLight, bos.LightTheme);
            Assert.Equal(PaletteCatalog.Default, bos.DarkTheme);

            new AppSettings { FollowSystemTheme = true, ReduceMotion = true, LightTheme = "Kar", DarkTheme = "Nord" }.Save(dosya);
            var geri = AppSettings.Load(dosya);
            Assert.True(geri.FollowSystemTheme);
            Assert.True(geri.ReduceMotion);
            Assert.Equal("Kar", geri.LightTheme);
            Assert.Equal("Nord", geri.DarkTheme);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void AyarlardakiAnahtarPencereyiHemenSakinlestirir()
    {
        var (once, acinca, kapaninca, izleVarsayilan, ciftGizli) = AppHost.Run(() =>
        {
            var onceki = MotionPreference.Source;
            MotionPreference.Source = new SahteHareket { Windows = true, Mac = "0", Gnome = "true" };
            MotionPreference.ResetForTest();
            Strings.Use("en");
            var pencere = new MainWindow();
            try
            {
                pencere.RestoreAppSettingsForTest(new AppSettings());
                var kutu = pencere.FindControl<CheckBox>("ChkReduceMotion")!;
                var izle = pencere.FindControl<CheckBox>("ChkFollowSystemTheme")!;
                var cift = pencere.FindControl<Control>("SystemThemePairRow")!;
                var a = pencere.Classes.Contains("reduced-motion");
                kutu.IsChecked = true;
                var b = pencere.Classes.Contains("reduced-motion");
                kutu.IsChecked = false;
                var c = pencere.Classes.Contains("reduced-motion");
                return (a, b, c, izle.IsChecked == true, !cift.IsVisible);
            }
            finally
            {
                pencere.Close();
                MotionPreference.Source = onceki;
                MotionPreference.ResetForTest();
            }
        });

        Assert.False(once);
        Assert.True(acinca);
        Assert.False(kapaninca);
        Assert.False(izleVarsayilan);
        Assert.True(ciftGizli);
    }
}
