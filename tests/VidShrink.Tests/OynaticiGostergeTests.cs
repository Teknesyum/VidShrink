using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Hız ve ses göstergesi (<c>PlayerView.Gosterge.cs</c>): her hız ve ses komutundan sonra sol üstteki
/// rozet beklenen metinle görünür, <c>PlaybackOsdHold</c>'dan önce kaybolmaz, sonra solarak
/// kaybolur; ardışık değişim sayacı yeniler; hareketi azaltılmış pencerede ara saydamlık yoktur.
/// Bekleme gerçek saate değil Avalonia'nın saat vuruşuna bağlı pompalamayla (<see cref="KareSayaci"/>).
/// </summary>
public sealed class OynaticiGostergeTests
{
    private static (PlayerView View, Window Window) Ac(string dil, bool azalt = false)
    {
        Strings.Use(dil);
        var view = new PlayerView();
        var window = new Window { Width = 960, Height = 540, Content = view };
        if (azalt) window.Classes.Add("reduced-motion");
        window.Show();
        KareSayaci.Pompala(window, k => k >= 2);
        return (view, window);
    }

    private static void Kapat(PlayerView view, Window window)
    {
        view.Close();
        window.Close();
        Strings.Reset();
    }

    private static Border Rozet(PlayerView view) => view.FindControl<Border>("OsdBadge")!;

    public static TheoryData<string, string, string> Komutlar() => new()
    {
        { "tr", "hiz+", "Hız 1,25×" },
        { "tr", "hiz-reset", "Hız 1×" },
        { "tr", "hiz-ab", "Hız 1,8×" },
        { "tr", "ses-", "Ses %80" },
        { "tr", "ses-tavan", "Ses %100" },
        { "tr", "ses-boost", "Ses %130" },
        { "tr", "sessiz", "Sessiz" },
        { "tr", "sessiz-geri", "Ses %80" },
        { "tr", "sessizken-ses", "Sessiz (%75)" },
        { "en", "hiz+", "Speed 1.25×" },
        { "en", "ses-", "Volume 80%" },
        { "de", "ses-", "Lautstärke 80 %" },
        { "de", "hiz+", "Geschwindigkeit 1,25×" },
    };

    private static void Uygula(PlayerView view, string komut)
    {
        switch (komut)
        {
            case "hiz+":
                view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
                break;
            case "hiz-reset":
                view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.5));
                view.Apply(new PlayerCommand(PlayerCommandKind.SpeedReset, 0));
                break;
            case "hiz-ab":
                view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
                break;
            case "ses-":
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -20));
                break;
            case "ses-tavan":
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, 10));
                break;
            case "ses-boost":
                view.ToggleBoost();
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, 30));
                break;
            case "sessiz":
                view.Apply(new PlayerCommand(PlayerCommandKind.ToggleMute, 0));
                break;
            case "sessiz-geri":
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -20));
                view.Apply(new PlayerCommand(PlayerCommandKind.ToggleMute, 0));
                view.Apply(new PlayerCommand(PlayerCommandKind.ToggleMute, 0));
                break;
            case "sessizken-ses":
                view.Apply(new PlayerCommand(PlayerCommandKind.ToggleMute, 0));
                view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -25));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(komut), komut, null);
        }
    }

    [Theory]
    [MemberData(nameof(Komutlar))]
    public void KomuttanSonraRozetBeklenenMetinleGorunur(string dil, string komut, string beklenen)
    {
        var (gorunurOnce, metin, ad, canli, isabet) = AppHost.Run(() =>
        {
            var (view, window) = Ac(dil);
            var rozet = Rozet(view);
            var once = rozet.IsVisible;
            Uygula(view, komut);
            KareSayaci.Pompala(window, k => k >= 1);
            var sonuc = (once, view.OsdText, AutomationProperties.GetName(rozet), AutomationProperties.GetLiveSetting(rozet), rozet.IsHitTestVisible);
            Kapat(view, window);
            return sonuc;
        });

        Assert.False(gorunurOnce, "komuttan once rozet gorunmemeli");
        Assert.Equal(beklenen, metin);
        Assert.Equal(beklenen, ad);
        Assert.Equal(AutomationLiveSetting.Polite, canli);
        Assert.False(isabet, "rozet fare girdisini engellememeli");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TutmaSuresiBoyuncaKalirSonraKaybolur(bool azalt)
    {
        var (tutma, solma, gecen, araSaydamlik, solmaSuresi, enAz, ilkSaydamlik, gizlendi) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr", azalt);
            var rozet = Rozet(view);
            var hold = view.OsdHold;
            var hizli = view.FindResource("MotionFast") is TimeSpan t ? t : throw new InvalidOperationException("MotionFast yok");
            var ara = 0;
            var enDusuk = 1.0;
            double ilkAra = double.NaN;
            var saat = Stopwatch.StartNew();
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
            var ilk = rozet.Opacity;
            double kayboldu = double.NaN;
            KareSayaci.Pompala(window, _ => !rozet.IsVisible, () =>
            {
                if (!rozet.IsVisible)
                {
                    if (double.IsNaN(kayboldu)) kayboldu = saat.Elapsed.TotalMilliseconds;
                    return;
                }

                if (rozet.Opacity > 0.001 && rozet.Opacity < 0.999)
                {
                    ara++;
                    if (double.IsNaN(ilkAra) && saat.Elapsed >= hold) ilkAra = saat.Elapsed.TotalMilliseconds;
                }

                enDusuk = Math.Min(enDusuk, rozet.Opacity);
            });
            var sonuc = (hold.TotalMilliseconds, hizli.TotalMilliseconds, kayboldu, ara, kayboldu - ilkAra, enDusuk, ilk, !rozet.IsVisible);
            Kapat(view, window);
            return sonuc;
        });

        Assert.True(gizlendi, "rozet tutma suresinden sonra gizlenmedi");
        Assert.True(gecen >= tutma, $"rozet {gecen} ms'de kayboldu, tutma {tutma} ms");
        if (azalt)
        {
            Assert.Equal(1, ilkSaydamlik);
            Assert.Equal(0, araSaydamlik);
            Assert.Equal(1, enAz);
        }
        else
        {
            Assert.True(araSaydamlik > 0, "solma canlandirmasi gorulmedi");
            Assert.False(double.IsNaN(solmaSuresi), "kaybolma solmasinda ara saydamlik gorulmedi");
            Assert.True(solmaSuresi >= solma - 16, $"solma {solmaSuresi} ms surdu, MotionFast {solma} ms: gizleme solmayi kesti");
        }
    }

    [Fact]
    public void ArdisikDegisimMetniGunceller_SayaciYeniler()
    {
        var (tutma, ikinciMetin, ikinciden, gizlendi) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            var rozet = Rozet(view);
            var hold = view.OsdHold;
            var saat = Stopwatch.StartNew();
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
            KareSayaci.Pompala(window, _ => saat.Elapsed >= hold * 0.6);
            view.Apply(new PlayerCommand(PlayerCommandKind.Volume, -10));
            var metin = view.OsdText;
            var ikinci = Stopwatch.StartNew();
            double kayboldu = double.NaN;
            KareSayaci.Pompala(window, _ => !rozet.IsVisible, () =>
            {
                if (!rozet.IsVisible && double.IsNaN(kayboldu)) kayboldu = ikinci.Elapsed.TotalMilliseconds;
            });
            var sonuc = (hold.TotalMilliseconds, metin, kayboldu, !rozet.IsVisible);
            Kapat(view, window);
            return sonuc;
        });

        Assert.Equal("Ses %90", ikinciMetin);
        Assert.True(gizlendi);
        Assert.True(ikinciden >= tutma, $"ikinci degisimden {ikinciden} ms sonra kayboldu, tutma {tutma} ms: sayac yenilenmedi");
    }

    [Fact]
    public void RozetSolUsttePngOlarakCizilir()
    {
        var (yol, sol, ust, genislik) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            var rozet = Rozet(view);
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.25));
            KareSayaci.Pompala(window, _ => rozet.Opacity >= 0.999);
            var konum = rozet.TranslatePoint(new Point(0, 0), window)!.Value;
            var klasor = Path.Combine(TipSources.Root, ".calisma", "oynatici-gosterge");
            Directory.CreateDirectory(klasor);
            var dosya = Path.Combine(klasor, "osd-rozet.png");
            var w = (int)window.Bounds.Width;
            var h = (int)window.Bounds.Height;
            using (var kare = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96)))
            {
                kare.Render(window);
                kare.Save(dosya, PngBitmapEncoderOptions.Default);
            }

            var sonuc = (dosya, konum.X, konum.Y, w);
            Kapat(view, window);
            return sonuc;
        });

        Assert.True(File.Exists(yol));
        Assert.True(sol < genislik / 4.0, $"rozet solda degil: x {sol}");
        Assert.True(ust < 100, $"rozet ustte degil: y {ust}");
    }

    private sealed record HizSimgesi(bool Hizli, bool UcGorunur, string Ad, bool SeritHizli, bool SeritUc, bool? Isaretli, bool GlifGorunur, string Metin);

    private static HizSimgesi Oku(PlayerView view)
    {
        var hizliVeri = view.FindResource("IconSpeedFast");
        var yavasVeri = view.FindResource("IconSpeedSlow");
        var govde = view.FindControl<Avalonia.Controls.Shapes.Path>("GlyphOsdSpeed")!;
        var seritGovde = view.FindControl<Avalonia.Controls.Shapes.Path>("GlyphSeritSpeed")!;
        Assert.True(ReferenceEquals(govde.Data, hizliVeri) || ReferenceEquals(govde.Data, yavasVeri), "rozet hiz simgesi tanidik degil");
        Assert.True(ReferenceEquals(seritGovde.Data, hizliVeri) || ReferenceEquals(seritGovde.Data, yavasVeri), "serit hiz simgesi tanidik degil");
        return new HizSimgesi(
            ReferenceEquals(govde.Data, hizliVeri),
            view.FindControl<Avalonia.Controls.Shapes.Path>("GlyphOsdSpeedTip")!.IsVisible,
            AutomationProperties.GetName(view.FindControl<Grid>("OsdSpeedGlyph")!) ?? "",
            ReferenceEquals(seritGovde.Data, hizliVeri),
            view.FindControl<Avalonia.Controls.Shapes.Path>("GlyphSeritSpeedTip")!.IsVisible,
            view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("BtnSeritSpeedAb")!.IsChecked,
            view.FindControl<Grid>("OsdSpeedGlyph")!.IsVisible,
            view.OsdText ?? "");
    }

    private static void AKipineDon(PlayerView view)
    {
        view.Settings.SpeedA = PlayerSettings.DefaultSpeedA;
        view.Settings.SpeedB = PlayerSettings.DefaultSpeedB;
        if (view.SpeedModeB) view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
        view.Apply(new PlayerCommand(PlayerCommandKind.SpeedReset, 0));
    }

    [Fact]
    public void HizliKipHizliSimgeYavasKipYavasSimgeGosterir()
    {
        var (yavas, hizli) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            AKipineDon(view);
            KareSayaci.Pompala(window, k => k >= 1);
            var a = Oku(view);
            view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
            KareSayaci.Pompala(window, k => k >= 1);
            var b = Oku(view);
            Kapat(view, window);
            return (a, b);
        });

        Assert.Equal(new HizSimgesi(false, false, "Yavaş", false, false, false, true, "Hız 1×"), yavas);
        Assert.Equal(new HizSimgesi(true, true, "Hızlı", true, true, true, true, "Hız 1,8×"), hizli);
    }

    [Fact]
    public void HizliYavasiGecinceSimgelerinSahibiDegisir()
    {
        var (aYavas, aHizli, bYavas, bHizli) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            AKipineDon(view);
            var ilk = Oku(view);
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 1.0));
            var gecti = Oku(view);
            view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
            var bYavasKip = Oku(view);
            view.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.5));
            var bGecti = Oku(view);
            Kapat(view, window);
            return (ilk, gecti, bYavasKip, bGecti);
        });

        Assert.False(aYavas.Hizli);
        Assert.Equal("Hız 1×", aYavas.Metin);
        Assert.True(aHizli.Hizli, "A kipi 2× olunca B'nin 1,8×'ini gecti, hizli simge onda olmali");
        Assert.True(aHizli.UcGorunur);
        Assert.Equal("Hız 2×", aHizli.Metin);
        Assert.False(bYavas.Hizli, "B kipi 1,8× iken A 2×: B yavas simgeyi almali");
        Assert.False(bYavas.UcGorunur);
        Assert.False(bYavas.SeritHizli);
        Assert.False(bYavas.Isaretli);
        Assert.Equal("Hız 1,8×", bYavas.Metin);
        Assert.True(bHizli.Hizli, "B kipi 2,3× olunca A'nin 2×'ini gecti");
        Assert.True(bHizli.Isaretli);
        Assert.Equal("Hız 2,3×", bHizli.Metin);
    }

    [Theory]
    [InlineData(1.8, 1.0, true)]
    [InlineData(1.0, 1.8, false)]
    [InlineData(1.5, 1.5, true)]
    [InlineData(1.0, 1.0, false)]
    [InlineData(0.75, 0.75, false)]
    public void EsitlikteBireGoreKararVerilir(double etkin, double diger, bool beklenen)
        => Assert.Equal(beklenen, PlayerView.HizliMi(etkin, diger));

    [Fact]
    public void HizGostergesindeAyaBHarfiYok()
    {
        var yazilar = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            var liste = new List<string>();
            foreach (var adim in new[] { 0, 1 })
            {
                view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
                KareSayaci.Pompala(window, k => k >= 1);
                foreach (var kok in new Control[] { Rozet(view), view.FindControl<Control>("BtnSeritSpeedAb")! })
                    liste.AddRange(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(kok).OfType<TextBlock>()
                        .Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? ""));
            }

            Kapat(view, window);
            return liste;
        });

        Assert.Contains("Hız 1,8×", yazilar);
        Assert.DoesNotContain(yazilar, t => t.Trim() is "A" or "B");
    }

    [Fact]
    public void HizliIbreninUcuTuruncuCizilir()
    {
        var (beklenen, hizliUc, yavasAyniYer) = AppHost.Run(() =>
        {
            var (view, window) = Ac("tr");
            AKipineDon(view);
            var renk = view.FindResource("EmberFlameColor") is Avalonia.Media.Color c ? c : throw new InvalidOperationException("EmberFlameColor yok");
            var klasor = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-a4c1b6704a933cfea");
            Directory.CreateDirectory(klasor);

            view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
            KareSayaci.Pompala(window, _ => Rozet(view).Opacity >= 0.999);
            Kaydet(window, Path.Combine(klasor, "osd-hiz-hizli.png"));
            var hizli = UcPikseli(view, Path.Combine(klasor, "osd-hiz-hizli-glif.png"));

            view.Apply(new PlayerCommand(PlayerCommandKind.SpeedAb, 0));
            KareSayaci.Pompala(window, _ => Rozet(view).Opacity >= 0.999);
            Kaydet(window, Path.Combine(klasor, "osd-hiz-yavas.png"));
            var yavas = UcPikseli(view, Path.Combine(klasor, "osd-hiz-yavas-glif.png"));

            Kapat(view, window);
            return (renk, hizli, yavas);
        });

        Assert.True(Yakin(beklenen, hizliUc), $"hizli ibrenin ucu {hizliUc}, beklenen EmberFlameColor {beklenen}");
        Assert.False(Yakin(beklenen, yavasAyniYer), $"yavas kipte ayni yerde turuncu var: {yavasAyniYer}");
    }

    private static bool Yakin(Avalonia.Media.Color a, Avalonia.Media.Color b)
        => b.A >= 250 && Math.Abs(a.R - b.R) <= 8 && Math.Abs(a.G - b.G) <= 8 && Math.Abs(a.B - b.B) <= 8;

    private static void Kaydet(Window window, string dosya)
    {
        using var kare = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
        kare.Render(window);
        kare.Save(dosya, PngBitmapEncoderOptions.Default);
    }

    private static Avalonia.Media.Color UcPikseli(PlayerView view, string dosya)
    {
        const int Olcek = 4;
        var glif = view.FindControl<Grid>("OsdSpeedGlyph")!;
        var w = (int)Math.Ceiling(glif.Bounds.Width * Olcek);
        var h = (int)Math.Ceiling(glif.Bounds.Height * Olcek);
        var pikseller = new byte[w * h * 4];
        using (var kare = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96 * Olcek, 96 * Olcek)))
        {
            kare.Render(glif);
            kare.Save(dosya, PngBitmapEncoderOptions.Default);
            var tutamak = System.Runtime.InteropServices.GCHandle.Alloc(pikseller, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { kare.CopyPixels(new PixelRect(0, 0, w, h), tutamak.AddrOfPinnedObject(), pikseller.Length, w * 4); }
            finally { tutamak.Free(); }
        }

        var k = glif.Bounds.Width / 24 * Olcek;
        var aci = 60 * Math.PI / 180;
        var x = (int)Math.Round((12 + 5.2 * Math.Sin(aci)) * k);
        var y = (int)Math.Round((12 - 5.2 * Math.Cos(aci)) * k);
        var i = (y * w + x) * 4;
        return Avalonia.Media.Color.FromArgb(pikseller[i + 3], pikseller[i + 2], pikseller[i + 1], pikseller[i]);
    }
}
