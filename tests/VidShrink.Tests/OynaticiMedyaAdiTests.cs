using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;
using L = VidShrink.Tests.OynaticiListeTests;

namespace VidShrink.Tests;

public sealed class OynaticiMedyaAdiTests
{
    private const string Ad = "Yaz Tatili 2026 — kıyı.mp4";

    private static Border Baslik(PlayerView view) => view.FindControl<Border>("MediaTitle")!;

    private static void Kosum(Action<PlayerView, Window, string> govde)
    {
        var klasor = L.Klasor(Ad);
        var yol = Path.Combine(klasor, Ad);
        try
        {
            AppHost.Run(() =>
            {
                var view = L.Ac(yol, out var window);
                try
                {
                    govde(view, window, yol);
                }
                finally
                {
                    L.Kapat(view, window);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Fact]
    public void AcilincaAdGorunurVeDogru()
        => Kosum((view, window, yol) =>
        {
            var baslik = Baslik(view);
            DenetimSurucu.Pump(view, () => baslik.IsEffectivelyVisible && baslik.Bounds.Width > 0, 5);
            Assert.True(view.MediaTitleShown);
            Assert.Equal(Ad, view.MediaTitleText);
            Assert.Equal(yol, ToolTip.GetTip(baslik));
            var metin = view.FindControl<TextBlock>("TxtMediaTitle")!;
            Assert.Equal(TextTrimming.CharacterEllipsis, metin.TextTrimming);
            Assert.Equal(TextWrapping.NoWrap, metin.TextWrapping);
            Assert.True(baslik.Bounds.Width <= view.Bounds.Width, $"baslik {baslik.Bounds.Width:0.#} > pano {view.Bounds.Width:0.#}");
        });

    [Fact]
    public void UstPanelGizleninceAdDaGizlenir()
        => Kosum((view, window, _) =>
        {
            var baslik = Baslik(view);
            DenetimSurucu.Pump(view, () => baslik.IsEffectivelyVisible, 5);
            window.Classes.Add("chrome-hidden");
            DenetimSurucu.Pump(view, () => !baslik.IsEffectivelyVisible, 5);
            Assert.False(view.MediaTitleShown);
            window.Classes.Remove("chrome-hidden");
            DenetimSurucu.Pump(view, () => baslik.IsEffectivelyVisible, 5);
            Assert.True(view.MediaTitleShown);
        });

    [Fact]
    public void DosyasizGorunumdeAdYok()
        => AppHost.Run(() =>
        {
            var view = L.Ac(null, out var window);
            try
            {
                Assert.False(Baslik(view).IsEffectivelyVisible);
                Assert.Null(view.MediaTitleText);
            }
            finally
            {
                L.Kapat(view, window);
            }
        });

    [Fact]
    public void KapanincaAdKalkar()
        => Kosum((view, _, _) =>
        {
            DenetimSurucu.Pump(view, () => view.MediaTitleShown, 5);
            view.Close();
            DenetimSurucu.Wait(view, 0.1);
            Assert.False(view.MediaTitleShown);
            Assert.Null(view.MediaTitleText);
        });

    [Fact]
    public void SagTikMenuAcarAdiKopyalaAdiYazar()
        => Kosum((view, window, yol) =>
        {
            var kopya = new List<string>();
            view.PathCopier = (_, metin) =>
            {
                kopya.Add(metin);
                return Task.CompletedTask;
            };
            var baslik = Baslik(view);
            DenetimSurucu.Pump(view, () => baslik.IsEffectivelyVisible && baslik.Bounds.Width > 0, 5);

            L.SagTik(window, L.Merkez(window, baslik));
            DenetimSurucu.Pump(view, () => view.MediaTitleMenuOpen, 5);
            Assert.True(view.MediaTitleMenuOpen);
            Assert.False(view.MenuOpen);

            var satirlar = view.MediaTitleMenu!.Items.OfType<MenuItem>().ToList();
            Assert.Equal(Strings.Get("player.title.copy-name"), satirlar[0].Header);
            Assert.Equal(Strings.Get("player.list.item.copy-path"), satirlar[1].Header);
            Assert.Equal(Strings.Get("player.menu.reveal"), satirlar[2].Header);
            Assert.Equal(Strings.Get(Keymap.Info.LabelKey), satirlar[3].Header);

            satirlar[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = satirlar[0] });
            DenetimSurucu.Pump(view, () => kopya.Count > 0, 5);
            Assert.Equal(new[] { Ad }, kopya);

            DenetimSurucu.Pump(view, () => !view.MediaTitleMenuOpen, 5);
            view.OpenTitleMenu();
            DenetimSurucu.Pump(view, () => view.MediaTitleMenuOpen, 5);
            var yolSatiri = view.MediaTitleMenu!.Items.OfType<MenuItem>().ElementAt(1);
            yolSatiri.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = yolSatiri });
            DenetimSurucu.Pump(view, () => kopya.Count > 1, 5);
            Assert.Equal(Path.GetFullPath(yol), kopya[1]);
        });

    [Fact]
    public void BilgiSatiriBilgiPanosunuAcar()
        => Kosum((view, _, _) =>
        {
            DenetimSurucu.Pump(view, () => view.MediaTitleShown, 5);
            var panel = view.FindControl<Border>("InfoPanel")!;
            var once = panel.IsVisible;
            view.OpenTitleMenu();
            DenetimSurucu.Pump(view, () => view.MediaTitleMenuOpen, 5);
            var bilgi = view.MediaTitleMenu!.Items.OfType<MenuItem>().Last();
            bilgi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = bilgi });
            DenetimSurucu.Pump(view, () => panel.IsVisible != once, 5);
            Assert.NotEqual(once, panel.IsVisible);
        });

    [Fact]
    public void SolTikOynatmayiDegistirmez()
        => Kosum((view, window, _) =>
        {
            var baslik = Baslik(view);
            DenetimSurucu.Pump(view, () => baslik.IsEffectivelyVisible && baslik.Bounds.Width > 0, 5);
            var oynuyor = view.IsPlaying;
            var tik = view.LeftClicks;

            var ad = L.Merkez(window, baslik);
            L.Vurulur(window, ad, baslik, view);
            L.SolTik(window, ad, view);
            DenetimSurucu.Wait(view, 0.4);
            Assert.Equal(oynuyor, view.IsPlaying);
            Assert.Equal(tik, view.LeftClicks);
            Assert.Equal("", view.DragMode);

            var surface = view.FindControl<Panel>("Surface")!;
            var bos = surface.TranslatePoint(new Point(surface.Bounds.Width / 2, surface.Bounds.Height / 2), window)!.Value;
            L.Vurulur(window, bos, surface, view);
            L.SolTik(window, bos, view);
            DenetimSurucu.Pump(view, () => view.IsPlaying != oynuyor, 2);
            Assert.NotEqual(oynuyor, view.IsPlaying);
        });
}
