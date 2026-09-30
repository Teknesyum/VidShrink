using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.VisualTree;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiListeTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Fare = Yeni(typeof(MouseDevice), new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true));

    private static object Yeni(Type tur, params object?[] args)
        => Activator.CreateInstance(tur, Her, null, args, null)!;

    private static object Impl(TopLevel top) => typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top)!;

    private static IInputRoot Kok(TopLevel top) => (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(top)!;

    internal static void Ham(TopLevel top, RawInputEventArgs args)
    {
        var impl = Impl(top);
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        giris(args);
    }

    internal static void Fareyle(TopLevel top, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
        => Ham(top, (RawInputEventArgs)Yeni(typeof(RawPointerEventArgs), Fare, (ulong)Environment.TickCount64, Kok(top), tur, nokta, tuslar));

    internal static void Tus(TopLevel top, Key key)
    {
        var klavye = typeof(KeyboardDevice).GetProperty("Instance", Her)!.GetValue(null)!;
        foreach (var tur in new[] { RawKeyEventType.KeyDown, RawKeyEventType.KeyUp })
            Ham(top, (RawInputEventArgs)Yeni(typeof(RawKeyEventArgs), klavye, (ulong)Environment.TickCount64, Kok(top), tur, key, RawInputModifiers.None, PhysicalKey.None, null, KeyDeviceType.Keyboard));
    }

    internal static Point Merkez(TopLevel top, Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), top)!.Value;

    internal static void SagTik(Window window, Point nokta)
    {
        Fareyle(window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
        Fareyle(window, RawPointerEventType.RightButtonDown, nokta, RawInputModifiers.RightMouseButton);
        Fareyle(window, RawPointerEventType.RightButtonUp, nokta, RawInputModifiers.None);
    }

    internal static void SolTik(TopLevel top, Point nokta, PlayerView view)
    {
        Fareyle(top, RawPointerEventType.Move, nokta, RawInputModifiers.None);
        DenetimSurucu.Wait(view, 0.05);
        Fareyle(top, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
        DenetimSurucu.Wait(view, 0.05);
        Fareyle(top, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
    }

    internal static MenuFlyoutPresenter Sunucu(PlayerView view)
    {
        var menu = (MenuFlyout)typeof(PlayerView).GetField("_menu", Her)!.GetValue(view)!;
        return ((Visual)menu.Items[0]!).FindAncestorOfType<MenuFlyoutPresenter>()!;
    }

    private static PixelRect Ekranda(Visual control)
    {
        var top = TopLevel.GetTopLevel(control)!;
        var sol = control.PointToScreen(new Point(0, 0));
        return new PixelRect(sol, PixelSize.FromSize(control.Bounds.Size, top.RenderScaling));
    }

    internal static string Klasor(params string[] adlar)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-liste", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        foreach (var ad in adlar) File.WriteAllBytes(Path.Combine(klasor, ad), Array.Empty<byte>());
        return klasor;
    }

    internal static void Sil(string klasor)
    {
        try
        {
            Directory.Delete(klasor, true);
        }
        catch (IOException)
        {
        }
    }

    internal static PlayerView Ac(string? path, out Window window)
    {
        var motor = new YolMotoru();
        var view = new PlayerView { EngineFactory = () => motor };
        window = new Window { Width = 960, Height = 540, Content = view, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Show();
        if (path is not null)
        {
            var open = view.OpenAsync(path);
            DenetimSurucu.Pump(view, () => open.IsCompleted, 10);
            open.GetAwaiter().GetResult();
        }

        DenetimSurucu.Wait(view, 0.3);
        return view;
    }

    internal static void Kapat(PlayerView view, Window window)
    {
        view.Close();
        window.Close();
        DenetimSurucu.Wait(view, 0.1);
    }

    [Theory]
    [InlineData(1000, 300, 280, 0, 1920, 720)]
    [InlineData(1600, 300, 280, 0, 1920, 1320)]
    [InlineData(1500, 500, 280, 0, 1920, 1500)]
    [InlineData(1800, 300, 280, 0, 1920, 1220)]
    [InlineData(100, 300, 280, 0, 1920, 400)]
    [InlineData(-500, 300, 280, -1920, 0, -780)]
    [InlineData(-400, 500, 280, -1920, 0, -400)]
    public void YerlesimImlecinAynasinda(double imlec, double menu, double liste, double sol, double sag, double beklenen)
    {
        Assert.Equal(beklenen, PlaylistPlacement.ListX(imlec, menu, liste, sol, sag));
    }

    [Fact]
    public void YerlesimMenuSagdaysaListeninSagKenariImlecte()
    {
        var menuSol = PlaylistPlacement.MenuLeft(1000, 300, 0, 1920);
        Assert.Equal(1000, menuSol);
        Assert.Equal(1000, PlaylistPlacement.ListX(1000, 300, 280, 0, 1920) + 280);

        var donmus = PlaylistPlacement.MenuLeft(1800, 300, 0, 1920);
        Assert.Equal(1500, donmus);
        Assert.Equal(1800, PlaylistPlacement.ListX(1800, donmus, 300, 100, 0, 1920));
    }

    [Fact]
    public void YerlesimEkrandanTasmaz()
    {
        var x = PlaylistPlacement.ListX(50, 0, 1900, 280, 0, 1920);
        Assert.InRange(x, 0, 1920 - 280);
    }

    [Theory]
    [InlineData(1500, 1200, 1800, 920)]
    [InlineData(100, 100, 700, 700)]
    public void YerlesimAcikAltMenuyuSayar(double imlec, double doluSol, double doluSag, double beklenen)
    {
        var menuSol = PlaylistPlacement.MenuLeft(imlec, 300, 0, 1920);
        var x = PlaylistPlacement.ListX(imlec, menuSol, 300, doluSol, doluSag, 280, 0, 1920, 0);
        Assert.Equal(beklenen, x);
        Assert.True(x + 280 <= doluSol || x >= doluSag, $"liste {x} alt menuyle cakisiyor [{doluSol}, {doluSag}]");
        Assert.InRange(x, 0, 1920 - 280);
    }

    [Fact]
    public void YerlesimIkiYanaSigmayincaEkrandaKalir()
    {
        var x = PlaylistPlacement.ListX(900, 900, 300, 100, 1800, 280, 0, 1920, 8);
        Assert.InRange(x, 8, 1920 - 280 - 8);
    }

    [Fact]
    public void KlavyeCapasiOdakliOgedir()
    {
        Assert.Equal(600, PlaylistPlacement.AnchorX(null, 600, 400));
        Assert.Equal(1000, PlaylistPlacement.AnchorX(1000, 600, 400));
        Assert.Equal(400, PlaylistPlacement.AnchorX(null, null, 400));

        var anchor = PlaylistPlacement.AnchorX(null, 600, 400);
        var x = PlaylistPlacement.ListX(anchor, 400, 300, 400, 700, 280, 0, 1920, 0);
        Assert.Equal(700, x);
    }

    [Theory]
    [InlineData(1000, 712)]
    [InlineData(290, 598)]
    [InlineData(1640, 1052)]
    public void YerlesimGolgeyiSayar(double imlec, double beklenen)
    {
        const double golge = 8;
        var menuSol = PlaylistPlacement.MenuLeft(imlec, 300, 0, 1920);
        var x = PlaylistPlacement.ListX(imlec, menuSol, 300, menuSol, menuSol + 300, 280, 0, 1920, golge);
        Assert.Equal(beklenen, x);
        Assert.True(x + 280 + golge <= menuSol || x - golge >= menuSol + 300, $"liste {x} golgesi menuye degiyor [{menuSol}, {menuSol + 300}]");
        Assert.InRange(x, golge, 1920 - 280 - golge);
    }

    [Theory]
    [InlineData(100, 400, 100)]
    [InlineData(900, 400, 672)]
    [InlineData(2, 400, 8)]
    public void YerlesimGolgeyiDikeydeSayar(double menuUst, double liste, double beklenen)
    {
        Assert.Equal(beklenen, PlaylistPlacement.ListY(menuUst, liste, 0, 1080, 8));
    }

    [Fact]
    public void SiraKlasordenVeKaristirmaTohumundanGelir()
    {
        var klasor = Klasor("b10.mp4", "a2.mkv", "a10.mp4", "not.txt", "clip1.mp4");
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a2.mkv"), out var window);
                var adlar = view.PlaylistFiles().Select(Path.GetFileName).ToArray();
                Assert.Equal(new[] { "a2.mkv", "a10.mp4", "b10.mp4", "clip1.mp4" }, adlar);

                view.Settings.Shuffle = true;
                var tohum = (int)typeof(PlayerView).GetField("_shuffleSeed", Her)!.GetValue(view)!;
                var beklenen = FolderNavigator.Order(FolderNavigator.Siblings(Path.Combine(klasor, "a2.mkv")), true, tohum);
                Assert.Equal(beklenen, view.PlaylistFiles());
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void SagTikMenuVeAynadakiListeyiAcar()
    {
        var klasor = Klasor("a1.mp4", "a2.mp4", "a3.mp4");
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a2.mp4"), out var window);
                SagTik(window, Merkez(window, view));
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.4);
                Assert.True(view.MenuOpen, "menu acilmadi");
                Assert.True(view.PlaylistOpen, "liste acilmadi");

                var imlec = view.MenuPointer!.Value;
                var menu = Ekranda(Sunucu(view));
                var liste = Ekranda(view.PlaylistFrame);
                Assert.InRange(menu.X, imlec.X - 2, imlec.X + 2);
                var olcek = TopLevel.GetTopLevel(view)!.RenderScaling;
                var golge = Math.Max((double)view.FindResource("MenuShadowExtent")!, (double)view.FindResource("PlaylistMenuGap")!) * olcek;
                Assert.True((double)view.FindResource("PlaylistMenuGap")! > (double)view.FindResource("MenuShadowExtent")!, "bosluk golgeden genis degil");
                Assert.True(golge > 0, "golge belirteci yok");
                Assert.InRange(liste.Right, imlec.X - golge - 2, imlec.X - golge + 2);
                Assert.InRange(liste.Y, menu.Y - 2, menu.Y + 2);

                Assert.Equal(new[] { "a1.mp4", "a2.mp4", "a3.mp4" },
                    view.Playlist.Items.OfType<ListBoxItem>().Select(i => Path.GetFileName((string)i.Tag!)).ToArray());
                Assert.Equal(1, view.Playlist.SelectedIndex);
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void ListeMenununGorunusunuVeSeciliBicemiTasir()
    {
        var uzun = "uzun-ad-" + new string('x', 80) + ".mp4";
        var klasor = Klasor("a1.mp4", "a2.mp4", uzun);
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a2.mp4"), out var window);
                SagTik(window, Merkez(window, view));
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.4);

                var sunucu = Sunucu(view);
                var cerceve = view.PlaylistFrame;
                Assert.NotNull(cerceve.Background);
                Assert.Same(sunucu.Background, cerceve.Background);
                Assert.Same(sunucu.BorderBrush, cerceve.BorderBrush);
                Assert.Equal(sunucu.BorderThickness, cerceve.BorderThickness);
                Assert.Equal(sunucu.Padding, cerceve.Padding);
                Assert.Equal(sunucu.CornerRadius, cerceve.CornerRadius);

                var secili = (ListBoxItem)view.Playlist.ContainerFromIndex(1)!;
                Assert.True(secili.IsSelected);
                var zemin = secili.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ItemBorder");
                Assert.Same(view.FindResource("NeonBlueFill"), zemin.Background);
                var diger = (ListBoxItem)view.Playlist.ContainerFromIndex(0)!;
                var digerZemin = diger.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ItemBorder");
                Assert.NotSame(view.FindResource("NeonBlueFill"), digerZemin.Background);

                Assert.Equal((double)view.FindResource("PlaylistWidth")!, view.Playlist.Bounds.Width);
                var uzunSatir = view.Playlist.Items.OfType<ListBoxItem>().First(i => Path.GetFileName((string)i.Tag!) == uzun);
                var yazi = (TextBlock)uzunSatir.Content!;
                Assert.Equal(TextTrimming.CharacterEllipsis, yazi.TextTrimming);
                Assert.Single(yazi.TextLayout.TextLines);
                Assert.True(yazi.TextLayout.TextLines[0].HasCollapsed, "uzun ad kisaltilmadi");
                Assert.Equal(uzun, ToolTip.GetTip(uzunSatir));
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void CalanDosyaGorunureKaydirilirVeListeTavandaKalir()
    {
        var adlar = Enumerable.Range(1, 40).Select(i => $"klip{i:00}.mp4").ToArray();
        var klasor = Klasor(adlar);
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "klip40.mp4"), out var window);
                SagTik(window, Merkez(window, view));
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.4);

                var liste = view.Playlist;
                Assert.Equal(39, liste.SelectedIndex);
                Assert.Equal((double)view.FindResource("PlaylistMaxHeight")!, liste.Bounds.Height);
                var satir = liste.ContainerFromIndex(39);
                Assert.NotNull(satir);
                var ust = satir!.TranslatePoint(new Point(0, 0), liste)!.Value.Y;
                Assert.InRange(ust, 0, liste.Bounds.Height - satir.Bounds.Height + 1);
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void ListeyeTiklamakMenuyuKapatmazDosyaTiklamakAcar()
    {
        var klasor = Klasor("a1.mp4", "a2.mp4", "a3.mp4");
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a1.mp4"), out var window);
                SagTik(window, Merkez(window, view));
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.4);

                var kok = TopLevel.GetTopLevel(view.PlaylistFrame)!;
                var cerceve = view.PlaylistFrame;
                var kenar = cerceve.TranslatePoint(new Point(cerceve.Bounds.Width / 2, cerceve.Bounds.Height - 1), kok)!.Value;
                SolTik(kok, kenar, view);
                DenetimSurucu.Wait(view, 0.3);
                Assert.True(view.MenuOpen, "listeye tik menuyu kapatti");
                Assert.True(view.PlaylistOpen, "listeye tik listeyi kapatti");
                Assert.Equal(Path.Combine(klasor, "a1.mp4"), view.LoadedPath);

                var hedef = view.Playlist.ContainerFromIndex(2)!;
                SolTik(kok, Merkez(kok, hedef), view);
                DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted && view.LoadedPath == Path.Combine(klasor, "a3.mp4") && !view.MenuOpen && !view.PlaylistOpen, 5);
                Assert.Equal(Path.Combine(klasor, "a3.mp4"), view.LoadedPath);
                Assert.False(view.MenuOpen, "dosya tiki menuyu kapatmadi");
                Assert.False(view.PlaylistOpen, "dosya tiki listeyi kapatmadi");
                Assert.Contains("playlist -> a3.mp4", view.Trace);
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void MenuKapaninListeDeKapanir()
    {
        var klasor = Klasor("a1.mp4", "a2.mp4");
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a1.mp4"), out var window);
                SagTik(window, Merkez(window, view));
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.3);
                Assert.True(view.PlaylistOpen);

                SolTik(window, new Point(4, 4), view);
                DenetimSurucu.Pump(view, () => !view.MenuOpen && !view.PlaylistOpen, 5);
                Assert.False(view.MenuOpen, "disari tik menuyu kapatmadi");
                Assert.False(view.PlaylistOpen, "menu kapaninca liste acik kaldi");
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void KlavyeKisayoluAyniListeyiAcar()
    {
        var klasor = Klasor("a1.mp4", "a2.mp4");
        try
        {
            AppHost.Run(() =>
            {
                var view = Ac(Path.Combine(klasor, "a2.mp4"), out var window);
                view.Focus();
                Tus(window, Key.Apps);
                DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
                DenetimSurucu.Wait(view, 0.4);
                Assert.True(view.MenuOpen, "kisayol menuyu acmadi");
                Assert.True(view.PlaylistOpen, "kisayol listeyi acmadi");
                Assert.Equal("surface", view.MenuAnchor);

                var menu = Ekranda(Sunucu(view));
                var liste = Ekranda(view.PlaylistFrame);
                Assert.True(liste.Right <= menu.X + 2 || liste.X >= menu.Right - 2, $"liste {liste} menuyle cakisiyor {menu}");
                var ekran = window.Screens.ScreenFromVisual(view)!.WorkingArea;
                Assert.True(Math.Abs(liste.Y - menu.Y) <= 2, $"ust kenar: liste {liste}, menu {menu}, ekran {ekran}");
                Assert.Equal(1, view.Playlist.SelectedIndex);
                Kapat(view, window);
            });
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void DosyaYokkenYalnizMenuAcilir()
    {
        AppHost.Run(() =>
        {
            var view = Ac(null, out var window);
            SagTik(window, Merkez(window, view));
            DenetimSurucu.Pump(view, () => view.MenuOpen, 5);
            DenetimSurucu.Wait(view, 0.3);
            Assert.True(view.MenuOpen, "menu acilmadi");
            Assert.False(view.PlaylistOpen, "dosya yokken liste acildi");
            Kapat(view, window);
        });
    }
}
