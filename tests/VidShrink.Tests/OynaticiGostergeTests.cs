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
}
