using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;
using L = VidShrink.Tests.OynaticiListeTests;

namespace VidShrink.Tests;

public sealed class OynaticiListeMenusuTests
{
    private static readonly string[] Sira = { "play", "enqueue", "play-next", "reveal", "copy-path", "remove" };

    private static readonly string[] Anahtarlar =
    {
        "player.list.item.play", "player.list.item.enqueue", "player.list.item.play-next",
        "player.menu.reveal", "player.list.item.copy-path", "player.list.item.remove",
    };

    private static int Say(PlayerView view, string iz) => view.Trace.Count(t => t == iz);

    private static int Basla(PlayerView view, string on) => view.Trace.Count(t => t.StartsWith(on, StringComparison.Ordinal));

    private static MenuFlyout? Menu(PlayerView view)
        => (MenuFlyout?)typeof(PlayerView).GetField("_menu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view);

    private static bool Acik(TopLevel kok) => typeof(TopLevel).GetProperty("PlatformImpl", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(kok) is not null;

    private static void SagTik(TopLevel kok, Point nokta)
    {
        foreach (var (tur, tus) in new[]
                 {
                     (RawPointerEventType.Move, RawInputModifiers.None),
                     (RawPointerEventType.RightButtonDown, RawInputModifiers.RightMouseButton),
                     (RawPointerEventType.RightButtonUp, RawInputModifiers.None),
                 })
        {
            if (!Acik(kok)) return;
            L.Fareyle(kok, tur, nokta, tus);
        }
    }

    private static string[] Adlar(PlayerView view) => view.PlaylistFiles().Select(Path.GetFileName).ToArray()!;

    private static string[] Satirlar(PlayerView view)
        => view.Playlist.Items.OfType<ListBoxItem>().Select(i => Path.GetFileName((string)i.Tag!)).ToArray();

    private static Point BosNokta(PlayerView view, TopLevel kok)
    {
        var cerceve = view.PlaylistFrame;
        var liste = view.Playlist;
        var son = liste.ContainerFromIndex(liste.ItemCount - 1)!;
        DenetimSurucu.Pump(view, () => son.Bounds.Height > 0, 5);
        var alt = son.TranslatePoint(new Point(0, son.Bounds.Height), liste)!.Value.Y;
        var bosluk = Math.Ceiling(son.Bounds.Height) * 2;
        liste.Height = Math.Ceiling(alt + bosluk);

        var yerel = new Point(liste.Width / 2, alt + bosluk / 2);
        Point? bulunan = null;
        string? sonIsabet = null;
        DenetimSurucu.Pump(view, () =>
        {
            if (Math.Abs(liste.Bounds.Height - liste.Height) > 0.5) return false;
            if (liste.TranslatePoint(yerel, kok) is not { } aday) return false;
            if (aday.Y >= kok.ClientSize.Height || aday.X >= kok.ClientSize.Width) return false;
            if (kok.InputHitTest(aday) is not Visual isabet) return false;
            sonIsabet = isabet.GetType().Name;
            if (!cerceve.IsVisualAncestorOf(isabet) && !ReferenceEquals(cerceve, isabet)) return false;
            if (isabet is ListBoxItem || isabet.FindAncestorOfType<ListBoxItem>() is not null) return false;
            bulunan = aday;
            return true;
        }, 5);
        Assert.True(bulunan.HasValue, $"listede bos nokta bulunamadi (liste {liste.Bounds.Height:0.#}/{liste.Height:0.#}, kok {kok.ClientSize.Height:0.#}, isabet {sonIsabet ?? "yok"})");
        return bulunan!.Value;
    }

    private static void MenuyuAc(PlayerView view, Window window)
    {
        L.SagTik(window, L.Merkez(window, view));
        DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
        DenetimSurucu.Wait(view, 0.3);
        Assert.True(view.MenuOpen, "menu acilmadi");
        Assert.True(view.PlaylistOpen, "liste acilmadi");
    }

    private static void SatiraSagTik(PlayerView view, int sira)
    {
        var satir = view.Playlist.ContainerFromIndex(sira)!;
        var kok = TopLevel.GetTopLevel(satir)!;
        SagTik(kok, L.Merkez(kok, satir));
        DenetimSurucu.Pump(view, () => view.ItemMenuOpen, 5);
        DenetimSurucu.Wait(view, 0.2);
    }

    private static MenuItem Oge(PlayerView view, string etiket)
        => view.ItemMenu!.Items.OfType<MenuItem>().Single(i => (string?)i.Tag == etiket);

    private static void Tikla(PlayerView view, string etiket)
    {
        var oge = Oge(view, etiket);
        Assert.True(oge.IsEnabled, etiket + " kapali");
        oge.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = oge });
        DenetimSurucu.Wait(view, 0.2);
    }

    private static void Kosum(string[] adlar, string calan, Action<PlayerView, Window, string> is_)
    {
        var klasor = L.Klasor(adlar);
        try
        {
            AppHost.Run(() =>
            {
                var view = L.Ac(Path.Combine(klasor, calan), out var window);
                try
                {
                    is_(view, window, klasor);
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
    public void AcikMenudeSagTikKapatirYenidenAcmaz()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4" }, "a1.mp4", (view, window, _) =>
        {
            var nokta = L.Merkez(window, view);
            MenuyuAc(view, window);
            Assert.Equal(1, Say(view, "menu"));

            SagTik(window, nokta + new Point(40, 20));
            DenetimSurucu.Wait(view, 0.5);
            Assert.False(view.MenuOpen, "acik menude sag tik menuyu kapatmadi ya da yeniden acti");
            Assert.False(view.PlaylistOpen, "liste acik kaldi");
            Assert.Equal(1, Say(view, "menu"));

            SagTik(window, nokta);
            DenetimSurucu.Pump(view, () => view.MenuOpen, 5);
            Assert.True(view.MenuOpen, "kapali menude sag tik menuyu acmadi");
            Assert.Equal(2, Say(view, "menu"));
        });
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("liste")]
    public void AcikMenununUstundeSagTikIkinciMenuAcmaz(string yer)
    {
        Kosum(new[] { "a1.mp4", "a2.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            var ilk = Menu(view);

            Visual hedef = yer == "menu" ? L.Sunucu(view) : view.PlaylistFrame;
            var kok = TopLevel.GetTopLevel(hedef)!;
            var nokta = yer == "menu" ? L.Merkez(kok, hedef) : BosNokta(view, kok);
            SagTik(kok, nokta);
            DenetimSurucu.Wait(view, 0.5);
            Assert.Equal(1, Say(view, "menu"));
            Assert.Same(ilk, Menu(view) ?? ilk);
            Assert.False(view.ItemMenuOpen, "bos alanda oge menusu acildi");
            if (yer == "liste")
            {
                Assert.False(view.MenuOpen, "listede sag tik menuyu kapatmadi");
                Assert.False(view.PlaylistOpen, "liste acik kaldi");
            }
        });
    }

    [Fact]
    public void OgeMenusununOgeleriDogru()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 1);
            Assert.True(view.ItemMenuOpen, "oge menusu acilmadi");
            Assert.True(view.MenuOpen, "oge menusu ana menuyu kapatti");
            Assert.True(view.PlaylistOpen, "oge menusu listeyi kapatti");
            Assert.Equal(1, Say(view, "menu"));

            var ogeler = view.ItemMenu!.Items.OfType<MenuItem>().ToArray();
            Assert.Equal(Sira, ogeler.Select(i => (string)i.Tag!).ToArray());
            Assert.Equal(Anahtarlar.Select(Strings.Get).ToArray(), ogeler.Select(i => (string)i.Header!).ToArray());
            Assert.All(ogeler, i => Assert.True(i.IsEnabled, (string)i.Tag! + " kapali"));
            Assert.All(Anahtarlar, k => Assert.NotEqual(k, Strings.Get(k)));

            SatiraSagTik(view, 0);
            DenetimSurucu.Wait(view, 0.3);
            Assert.False(view.ItemMenuOpen, "acik oge menusunde sag tik menuyu kapatmadi ya da yeniden acti");
            Assert.Equal(1, Basla(view, "itemmenu a"));
            Assert.True(view.MenuOpen, "oge menusunu kapatan sag tik ana menuyu de kapatti");

            SatiraSagTik(view, 0);
            Assert.True(view.ItemMenuOpen, "calan dosyada oge menusu acilmadi");
            var calan = view.ItemMenu!.Items.OfType<MenuItem>().ToDictionary(i => (string)i.Tag!, i => i.IsEnabled);
            Assert.True(calan["play"]);
            Assert.False(calan["enqueue"]);
            Assert.False(calan["play-next"]);
            Assert.False(calan["remove"]);
            Assert.True(calan["reveal"]);
            Assert.True(calan["copy-path"]);
        });
    }

    [Fact]
    public void BundanSonraOynatCalaninHemenArkasinaKoyar()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4", "a4.mp4" }, "a2.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 3);
            Tikla(view, "play-next");
            Assert.False(view.ItemMenuOpen);
            Assert.True(view.PlaylistOpen, "duzenleme listeyi kapatti");
            Assert.Equal(new[] { "a1.mp4", "a2.mp4", "a4.mp4", "a3.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
            Assert.Equal(1, view.Playlist.SelectedIndex);

            SatiraSagTik(view, 0);
            Tikla(view, "play-next");
            Assert.Equal(new[] { "a2.mp4", "a1.mp4", "a4.mp4", "a3.mp4" }, Adlar(view));
        });
    }

    [Fact]
    public void SirayaAlSonaKoyar()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4", "a4.mp4" }, "a2.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 0);
            Tikla(view, "enqueue");
            Assert.Equal(new[] { "a2.mp4", "a3.mp4", "a4.mp4", "a1.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
            Assert.Equal(0, view.Playlist.SelectedIndex);
        });
    }

    [Fact]
    public void ListedenKaldirCikarir()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4" }, "a1.mp4", (view, window, klasor) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 2);
            Tikla(view, "remove");
            Assert.Equal(new[] { "a1.mp4", "a2.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
            Assert.True(File.Exists(Path.Combine(klasor, "a3.mp4")), "kaldir dosyayi sildi");
        });
    }

    [Fact]
    public void OynatKonumVeYolKapidanGecer()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4" }, "a1.mp4", (view, window, klasor) =>
        {
            var konum = new List<ProcessStartInfo>();
            var kopya = new List<string>();
            view.RevealLauncher = start => konum.Add(start);
            view.PathCopier = (_, metin) =>
            {
                kopya.Add(metin);
                return Task.CompletedTask;
            };
            var hedef = Path.Combine(klasor, "a2.mp4");

            MenuyuAc(view, window);
            SatiraSagTik(view, 1);
            Tikla(view, "copy-path");
            Assert.Equal(new[] { hedef }, kopya);

            SatiraSagTik(view, 1);
            Tikla(view, "reveal");
            var cagri = Assert.Single(konum);
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal("explorer.exe", cagri.FileName);
                Assert.Equal(new[] { "/select,", hedef }, cagri.ArgumentList.ToArray());
            }

            SatiraSagTik(view, 1);
            Tikla(view, "play");
            DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted && view.LoadedPath == hedef && !view.MenuOpen, 5);
            Assert.Equal(hedef, view.LoadedPath);
            Assert.False(view.MenuOpen);
            Assert.False(view.PlaylistOpen);
            Assert.False(view.ItemMenuOpen);
        });
    }

    [Fact]
    public void AcikOgeMenusundeListeyeTiklamakDosyaAcmaz()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4" }, "a1.mp4", (view, window, klasor) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 1);
            Assert.True(view.ItemMenuOpen);
            var satir = view.Playlist.ContainerFromIndex(2)!;
            var kok = TopLevel.GetTopLevel(satir)!;
            L.SolTik(kok, L.Merkez(kok, satir), view);
            DenetimSurucu.Wait(view, 1.5);
            Assert.False(view.ItemMenuOpen, "sol tik oge menusunu kapatmadi");
            Assert.Equal(Path.Combine(klasor, "a1.mp4"), view.LoadedPath);
            Assert.True(view.PlaylistOpen, "oge menusunu kapatan tik listeyi kapatti");
        });
    }

    [Theory]
    [InlineData("a4", "a1,a2,a4,a3")]
    [InlineData("a1", "a2,a1,a3,a4")]
    [InlineData("a2", null)]
    public void KuyrukBundanSonra(string dosya, string? beklenen)
        => Assert.Equal(beklenen, Birlestir(QueueEdit.InsertNext(Liste(), Yol("a2"), Yol(dosya))));

    [Theory]
    [InlineData("a1", "a2,a3,a4,a1")]
    [InlineData("a4", "a1,a2,a3,a4")]
    [InlineData("a5", "a1,a2,a3,a4,a5")]
    [InlineData("a2", null)]
    public void KuyrukSonaEkler(string dosya, string? beklenen)
        => Assert.Equal(beklenen, Birlestir(QueueEdit.Append(Liste(), Yol("a2"), Yol(dosya))));

    [Theory]
    [InlineData("a3", "a1,a2,a4")]
    [InlineData("a5", null)]
    [InlineData("a2", null)]
    public void KuyruktanCikarir(string dosya, string? beklenen)
        => Assert.Equal(beklenen, Birlestir(QueueEdit.Remove(Liste(), Yol("a2"), Yol(dosya))));

    private static string Yol(string ad) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuyruk", ad + ".mp4"));

    private static IReadOnlyList<string> Liste() => new[] { "a1", "a2", "a3", "a4" }.Select(Yol).ToArray();

    private static string? Birlestir(IReadOnlyList<string>? liste)
        => liste is null ? null : string.Join(",", liste.Select(Path.GetFileNameWithoutExtension));
}
