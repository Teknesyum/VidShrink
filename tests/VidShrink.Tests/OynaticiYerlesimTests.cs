using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

/// <summary>
/// Kesit B pimi: oynatıcı sekmesinin yerleşimi.
///
/// Kullanıcının cümlesi altı şey istiyordu — başlık kalksın, altyazı/ses parçası/üç nokta
/// düğmeleri kalksın, ses ve hız kaydırıcı olsun ve sayısı görünsün, −10/+10 simge olsun,
/// oynat ortada ve daha büyük dursun, oynatıcı tüm alanı kapsasın, uyarı oynatıcının
/// üstünde çıksın. Beşi burada ölçülüyor; altıncısı (alt şeridin fare aşağı gelince
/// belirmesi) zaten <c>OynaticiDenetimTests</c>'te pimli, bu tur ona dokunulmadı.
///
/// Ölçüm görüntüye değil ağaca bakıyor: denetimin var/yok olması, oynat düğmesinin
/// ortadaki öbekte durması, kenarın sıfırlanması. Sayılar belirteçten okunuyor, teste
/// sabit yazılmıyor — belirteç değişirse ölçü onunla birlikte kayar, yalancı kırmızı
/// üretmez.
/// </summary>
public sealed class OynaticiYerlesimTests
{
    private static object Belirtec(string anahtar)
    {
        var uygulama = Application.Current;
        Assert.True(uygulama is not null, "Uygulama ayakta degil.");
        var deger = uygulama!.FindResource(anahtar);
        Assert.True(deger is not null, $"{anahtar} belirteci yok.");
        return deger!;
    }

    private static T Bul<T>(PlayerView view, string ad) where T : Control
    {
        var denetim = view.FindControl<T>(ad);
        Assert.True(denetim is not null, $"{ad} biçimlemede yok.");
        return denetim!;
    }

    [Fact]
    public void BaslikSatiriVeUcNoktaKalkti()
    {
        var kalanlar = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var view = pencere.FindControl<PlayerView>("Player");
            Assert.True(view is not null, "Player biçimlemede yok.");

            return new[] { "BtnPlayerMenu", "TrackBar" }
                .Where(ad => view!.FindControl<Control>(ad) is not null)
                .ToList();
        });

        Assert.True(kalanlar.Count == 0,
            "Oynatıcı başlığından kalkması gereken denetim duruyor: " + string.Join(", ", kalanlar));
    }

    [Fact]
    public void SesVeHizKaydiriciVeSayisiGorunur()
    {
        var (sesGenislik, hizGenislik, sesYazi, hizYazi) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var view = pencere.FindControl<PlayerView>("Player")!;

            var ses = Bul<Slider>(view, "SliderSeritVolume");
            var hiz = Bul<Slider>(view, "SliderSeritSpeed");

            return (ses.Width, hiz.Width,
                Bul<TextBlock>(view, "TxtSeritVolume") is not null,
                Bul<TextBlock>(view, "TxtSeritSpeed") is not null);
        });

        var beklenen = (double)Belirtec("PlaybackSliderWidth")!;
        Assert.Equal(beklenen, sesGenislik);
        Assert.Equal(beklenen, hizGenislik);
        Assert.True(sesYazi && hizYazi, "Kaydırıcının yanındaki sayı yazısı yok.");
    }

    [Fact]
    public void OynatDugmesiOrtadaVeDigerlerindenBuyuk()
    {
        var (ortaSutun, oynatEn, oynatBoy, atlaEn) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var view = pencere.FindControl<PlayerView>("Player")!;

            var oynat = Bul<Button>(view, "BtnSeritPlay");
            var geri = Bul<Button>(view, "BtnSeritBack");
            var ileri = Bul<Button>(view, "BtnSeritForward");

            var obek = oynat.Parent as StackPanel;
            Assert.True(obek is not null, "Oynat düğmesi bir öbeğin içinde değil.");
            Assert.Same(obek, geri.Parent);
            Assert.Same(obek, ileri.Parent);

            return (Grid.GetColumn((Control)obek!), oynat.MinWidth, oynat.MinHeight, geri.MinWidth);
        });

        var barButonu = (double)Belirtec("PlaybackBarButtonSize")!;
        var oynatBoyu = (double)Belirtec("PlaybackPlayButtonSize")!;
        var oynatEni = (double)Belirtec("PlaybackPlayButtonWidth")!;

        Assert.Equal(1, ortaSutun);
        Assert.Equal(oynatEni, oynatEn);
        Assert.Equal(oynatBoyu, oynatBoy);
        Assert.Equal(barButonu, atlaEn);
        Assert.True(oynatEn > atlaEn, "Oynat düğmesi atlama düğmesinden geniş değil.");
        Assert.True(oynatBoy > barButonu, "Oynat düğmesi atlama düğmesinden yüksek değil.");
    }

    [Fact]
    public void AtlamaDugmeleriYaziDegilSimge()
    {
        var simgeler = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var view = pencere.FindControl<PlayerView>("Player")!;

            return new[] { "BtnSeritBack", "BtnSeritForward", "BtnSeritPlay", "BtnSeritMute" }
                .Select(ad => Bul<Button>(view, ad).Content is Avalonia.Controls.Shapes.Path)
                .ToList();
        });

        Assert.All(simgeler, tasiyor => Assert.True(tasiyor, "Şerit düğmesinin içeriği bir simge değil."));
    }

    [Fact]
    public void OynaticiSekmesindeCalismaKenariSifir()
    {
        var (oynatici, digeri) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var serit = pencere.FindControl<TabControl>("Tabs")!;

            Thickness Oku(int sira)
            {
                serit.SelectedIndex = sira;
                pencere.Measure(new Size(1600, 1000));
                pencere.Arrange(new Rect(0, 0, 1600, 1000));
                var host = serit.GetVisualDescendants()
                    .OfType<TransitioningContentControl>()
                    .First(denetim => denetim.Name == "SelectedContentHost");
                return host.Margin;
            }

            return (Oku(pencere.PlayerTabIndex), Oku(pencere.PlayerTabIndex + 1));
        });

        var calisma = (Thickness)Belirtec("WorkspaceMargin")!;

        Assert.Equal(new Thickness(0), oynatici);
        Assert.Equal(calisma, digeri);
    }

    [Fact]
    public void UyariKatmaniIcerigiAsagiItmiyor()
    {
        var (ayniGoz, ustte) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            var serit = pencere.FindControl<TabControl>("Tabs")!;
            pencere.Measure(new Size(1600, 1000));
            pencere.Arrange(new Rect(0, 0, 1600, 1000));

            var katman = serit.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .First(denetim => denetim.Name == "NoticeLayer");
            var host = serit.GetVisualDescendants()
                .OfType<TransitioningContentControl>()
                .First(denetim => denetim.Name == "SelectedContentHost");

            return (Grid.GetRow(katman) == Grid.GetRow(host), katman.VerticalAlignment);
        });

        Assert.True(ayniGoz, "Uyarı katmanı içerikle aynı ızgara gözünde değil, hâlâ yer kaplıyor.");
        Assert.Equal(VerticalAlignment.Top, ustte);
    }
}
