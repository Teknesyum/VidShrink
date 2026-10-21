using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using VidShrink.App;
using VidShrink.App.Themes;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class YuksekKarsitlikVeYaziOlcegiTests
{
    private const double AaaEsik = 7.0;

    private readonly ITestOutputHelper _cikti;

    public YuksekKarsitlikVeYaziOlcegiTests(ITestOutputHelper cikti) => _cikti = cikti;

    private sealed class SahteKaynak : ISystemThemeSource
    {
        private bool _karsitlik;

        public bool? IsLight => false;

        public bool IsHighContrast => _karsitlik;

        public event EventHandler? Changed;

        public void Ver(bool karsitlik)
        {
            _karsitlik = karsitlik;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string Xaml => File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Palette", PaletteCatalog.HighContrast, "Theme.axaml"));

    private static string Renk(string xaml, string anahtar)
    {
        var m = Regex.Match(xaml, "x:Key=\"" + anahtar + "\"[^>]*>(#[0-9A-Fa-f]{8})<");
        Assert.True(m.Success, anahtar + " yok");
        return m.Groups[1].Value;
    }

    private static string Bindir(string ust, string alt)
    {
        var a = Convert.ToInt32(ust.Substring(1, 2), 16) / 255.0;
        string Kanal(int at)
        {
            var u = Convert.ToInt32(ust.Substring(at, 2), 16);
            var z = Convert.ToInt32(alt.Substring(at, 2), 16);
            return ((int)Math.Round(u * a + z * (1 - a))).ToString("X2");
        }
        return "#FF" + Kanal(3) + Kanal(5) + Kanal(7);
    }

    public static TheoryData<string, string> MetinZeminCiftleri()
    {
        var kollar = new TheoryData<string, string>();
        foreach (var metin in new[] { "TextBodyColor", "PinkTextColor", "EmberBlazeColor", "NeonSuccessColor", "NeonBlueColor", "NeonPinkColor", "NeonPurpleColor", "TextDisabledColor" })
            foreach (var zemin in new[] { "AppBgColor", "SurfaceToneColor" })
                kollar.Add(metin, zemin);
        foreach (var dolgu in new[] { "NeonBlueColor", "NeonPinkColor", "NeonPurpleColor", "NeonSuccessColor", "EmberBlazeColor" })
            kollar.Add("OnNeonColor", dolgu);
        foreach (var yari in new[] { "NeonBlueFillColor", "NeonBlueHoverColor", "NeonBlueActiveColor", "NeonPinkFillColor" })
            foreach (var zemin in new[] { "AppBgColor", "SurfaceToneColor" })
                kollar.Add("TextBodyColor", yari + "@" + zemin);
        return kollar;
    }

    [Theory]
    [MemberData(nameof(MetinZeminCiftleri))]
    public void YuksekKarsitlikPaletiAaaEsiginiGecer(string metin, string zemin)
    {
        var xaml = Xaml;
        var parca = zemin.Split('@');
        var zeminRengi = parca.Length == 2 ? Bindir(Renk(xaml, parca[0]), Renk(xaml, parca[1])) : Renk(xaml, zemin);
        var oran = PaletKarsitligiTests.Karsitlik(Renk(xaml, metin), zeminRengi);
        _cikti.WriteLine($"AAA\t{metin}\t{zemin}\t{zeminRengi}\t{oran:0.00}");
        Assert.True(oran >= AaaEsik, $"{metin} / {zemin}: {oran:0.00}:1 — esik {AaaEsik:0.0}");
    }

    [Fact]
    public void YuksekKarsitlikPaletiListedeVeYuklenir()
    {
        Assert.Contains(PaletteCatalog.HighContrast, PaletteCatalog.Names);
        var (ad, acik) = AppHost.Run(() => (PaletteCatalog.Label(PaletteCatalog.HighContrast), PaletteCatalog.IsLight(PaletteCatalog.HighContrast)));
        Assert.Equal("High Contrast", ad);
        Assert.False(acik);
    }

    [Theory]
    [InlineData(true, false, false, "HighContrast")]
    [InlineData(true, true, false, "Nord")]
    [InlineData(true, true, true, "HighContrast")]
    [InlineData(false, false, false, "Nord")]
    [InlineData(false, true, true, "Dracula")]
    public void SistemKarsitlikIsteyinceSecimYalnizKullaniciSecmediyseDegisir(bool karsitlik, bool kullaniciSecti, bool izle, string beklenen)
        => Assert.Equal(beklenen, PaletteCatalog.Pick("Nord", kullaniciSecti, izle, false, "Buz", "Dracula", karsitlik));

    [Fact]
    public void IzleyiciKarsitlikDegisiminiKullaniciSecmediyseUygular()
    {
        var (secilmemisAcik, secilmemisKapali, secilmisAcik) = AppHost.Run(() =>
        {
            var baslangic = PaletteCatalog.Current;
            var kaynak = new SahteKaynak();
            using var izleyici = new SystemThemeFollower(kaynak) { Chosen = "Nord", ChosenByUser = false, Light = "Buz", Dark = "Dracula" };
            try
            {
                izleyici.Enabled = false;
                kaynak.Ver(true);
                var a = PaletteCatalog.Current;
                kaynak.Ver(false);
                var b = PaletteCatalog.Current;
                izleyici.ChosenByUser = true;
                kaynak.Ver(true);
                var c = PaletteCatalog.Current;
                return (a, b, c);
            }
            finally { PaletteCatalog.Use(baslangic); }
        });

        Assert.Equal(PaletteCatalog.HighContrast, secilmemisAcik);
        Assert.Equal("Nord", secilmemisKapali);
        Assert.Equal("Nord", secilmisAcik);
    }

    [Fact]
    public void AyarDosyasiSecimBayraginiVeYaziOlceginiTasir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "later-147", Guid.NewGuid().ToString("N"));
        var dosya = Path.Combine(klasor, "settings.json");
        try
        {
            var bos = AppSettings.Load(dosya);
            Assert.False(bos.ThemeChosen);
            Assert.Equal(100, bos.FontScale);

            new AppSettings { Theme = "Nord", ThemeChosen = true, FontScale = 150 }.Save(dosya);
            var geri = AppSettings.Load(dosya);
            Assert.True(geri.ThemeChosen);
            Assert.Equal(150, geri.FontScale);

            Directory.CreateDirectory(klasor);
            File.WriteAllText(dosya, "{\"theme\":\"Nord\",\"themeDefaultTeknesyum\":true,\"fontScale\":175}");
            var eski = AppSettings.Load(dosya);
            Assert.True(eski.ThemeChosen);
            Assert.Equal(100, eski.FontScale);

            File.WriteAllText(dosya, "{\"theme\":\"Teknesyum\",\"themeDefaultTeknesyum\":true}");
            Assert.False(AppSettings.Load(dosya).ThemeChosen);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static (double Kaynak, double Etiket, double Satir) Olc()
    {
        var app = Application.Current!;
        var etiket = new TextBlock { Text = "x", Theme = (ControlTheme)app.FindResource("Label")! };
        var ipucu = new TextBlock { Text = "x", Theme = (ControlTheme)app.FindResource("TipText")! };
        var pencere = new Window { Content = new StackPanel { Children = { etiket, ipucu } } };
        pencere.Show();
        try
        {
            return ((double)app.FindResource("FontSize2")!, etiket.FontSize, ipucu.LineHeight);
        }
        finally
        {
            pencere.Close();
        }
    }

    [Fact]
    public void YaziOlcegiBelirteciVeBicemiCarparVeGeriAlinir()
    {
        var (once, olcekli, geri) = AppHost.Run(() =>
        {
            try
            {
                var a = Olc();
                FontScale.Apply(150);
                var b = Olc();
                FontScale.Apply(100);
                return (a, b, Olc());
            }
            finally { FontScale.Apply(100); }
        });

        _cikti.WriteLine($"FontSize2 {once.Kaynak} -> {olcekli.Kaynak}, Label {once.Etiket} -> {olcekli.Etiket}, TipText satir {once.Satir} -> {olcekli.Satir}");
        Assert.Equal(once.Kaynak * 1.5, olcekli.Kaynak, 3);
        Assert.Equal(once.Etiket * 1.5, olcekli.Etiket, 3);
        Assert.Equal(once.Satir * 1.5, olcekli.Satir, 3);
        Assert.Equal(once, geri);
    }
}
