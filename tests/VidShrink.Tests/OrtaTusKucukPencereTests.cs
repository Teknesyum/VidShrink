using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class OrtaTusKucukPencereTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1040)]
    [InlineData(0, 0, 2560, 1400)]
    [InlineData(-1920, 0, 1920, 1080)]
    [InlineData(2560, 200, 1280, 984)]
    public void KucukPencereAlaninUcteBiriVeTamOrtada(int x, int y, int w, int h)
    {
        var r = CompactWindow.Fit(x, y, w, h, 1.0 / 3);

        Assert.InRange((double)r.Width * r.Height / ((double)w * h), 0.33, 0.337);
        Assert.InRange(Math.Abs(r.X + r.Width / 2.0 - (x + w / 2.0)), 0, 1);
        Assert.InRange(Math.Abs(r.Y + r.Height / 2.0 - (y + h / 2.0)), 0, 1);
        Assert.InRange(Math.Abs((double)r.Width / r.Height - (double)w / h), 0, 0.01);
    }

    [Fact]
    public void PayBelirtectenOkunur()
    {
        var pay = AppHost.Run(() => Application.Current!.FindResource("WindowCompactAreaShare"));
        Assert.Equal(0.3333, Assert.IsType<double>(pay), 4);
    }

    [Fact]
    public void OrtaTusTamEkrandanKucukPencereyeIkincisiTamEkrana()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView();
            view.CurrentTabIndex = () => 2;
            view.PlayerTabIndex = () => 5;
            var secilen = new List<int>();
            view.SelectTab = secilen.Add;
            var window = new Window { Width = 1300, Height = 800, MinWidth = 1136, MinHeight = 720, Content = view };
            window.Show();

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            var ilk = (view.Fullscreen.IsFullscreen, window.WindowState);

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            var ekran = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            var kucuk = (view.Fullscreen.IsFullscreen, window.WindowState, window.Width, window.Height, window.MinWidth, window.Position, view.IsCompact, ekran?.WorkingArea, ekran?.Scaling);

            GirdiSurucu.Press(view, PointerUpdateKind.MiddleButtonPressed, RawInputModifiers.MiddleMouseButton);
            var ucuncu = (view.Fullscreen.IsFullscreen, window.WindowState);

            view.RestoreWindowMin();
            var geri = (window.MinWidth, window.MinHeight, view.IsCompact);
            window.Close();
            return (ilk, kucuk, ucuncu, geri, secilen, iz: view.Trace.ToArray());
        });

        Assert.True(sonuc.ilk.IsFullscreen);
        Assert.Equal(WindowState.FullScreen, sonuc.ilk.WindowState);

        Assert.False(sonuc.kucuk.IsFullscreen);
        Assert.Equal(WindowState.Normal, sonuc.kucuk.WindowState);
        Assert.Empty(sonuc.secilen);
        Assert.Contains("compact -> False", sonuc.iz);
        if (sonuc.kucuk.WorkingArea is { } alan && sonuc.kucuk.Scaling is { } olcek)
        {
            var r = CompactWindow.Fit(alan.X, alan.Y, alan.Width, alan.Height, 0.3333);
            Assert.True(sonuc.kucuk.IsCompact);
            Assert.Equal(r.Width / olcek, sonuc.kucuk.Width, 3);
            Assert.Equal(r.Height / olcek, sonuc.kucuk.Height, 3);
            Assert.Equal(new PixelPoint(r.X, r.Y), sonuc.kucuk.Position);
            Assert.True(sonuc.kucuk.MinWidth <= sonuc.kucuk.Width);
        }

        Assert.True(sonuc.ucuncu.IsFullscreen);
        Assert.Equal(WindowState.FullScreen, sonuc.ucuncu.WindowState);
        Assert.Equal((1136d, 720d, false), sonuc.geri);
    }

    [Fact]
    public void FTusuHalaOncekiBoyutaDoner()
    {
        Assert.Equal(PlayerCommandKind.ToggleFullscreen, Keymap.ForKey(Key.F, KeyModifiers.None, "f").Kind);
        Assert.Equal(PlayerCommandKind.CompactOrFullscreen, Keymap.ForPress(PlayerButton.Middle).Kind);
    }
}
