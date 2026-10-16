using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// later.md #42 adım 1, EKB-04: kayıt sürerken ekrana çizim. Model saf hesapla, pencere gerçek pencereyle ölçülür:
/// isabet testi Windows'un kendi <c>WindowFromPoint</c>'i, çizim pencerenin kendi çizimi. Kayıt koşmaz.
/// </summary>
public sealed class EkranaCizimTests
{
    private static SketchPoint N(double x, double y) => new(x, y);

    /// <summary>
    /// Kalem her noktayı tutar, ok ve kutu yalnız iki ucu; hiç uzamamış ok düşer, kalemin tek noktası kalır.
    /// Numara sırayla artar, geri alınan numara sırayı geri verir. Boş yazı eklenmez.
    /// </summary>
    [Fact]
    public void ModelIzleriSiraylaTutarGeriAlirVeNumaralar()
    {
        var cizim = new ScreenSketch();
        cizim.Press(N(1, 1));
        cizim.Move(N(2, 2));
        cizim.Move(N(2, 2));
        cizim.Move(N(3, 1));
        cizim.Release();
        cizim.Press(N(5, 5));
        cizim.Release();
        cizim.Tool = SketchTool.Arrow;
        cizim.Press(N(0, 0));
        cizim.Move(N(4, 4));
        cizim.Move(N(9, 0));
        cizim.Release();
        cizim.Press(N(3, 3));
        cizim.Release();
        cizim.Tool = SketchTool.Number;
        cizim.Press(N(10, 10));
        cizim.Press(N(20, 20));
        cizim.AddText(N(0, 0), "  ");
        cizim.AddText(N(7, 7), " not ");

        Assert.Equal(new[] { SketchTool.Pen, SketchTool.Pen, SketchTool.Arrow, SketchTool.Number, SketchTool.Number, SketchTool.Text },
            cizim.Strokes.Select(s => s.Tool));
        Assert.Equal(new[] { N(1, 1), N(2, 2), N(3, 1) }, cizim.Strokes[0].Vertices);
        Assert.Single(cizim.Strokes[1].Vertices);
        Assert.Equal(new[] { N(0, 0), N(9, 0) }, cizim.Strokes[2].Vertices);
        Assert.Equal(new[] { 1, 2 }, cizim.Strokes.Where(s => s.Tool == SketchTool.Number).Select(s => s.Number));
        Assert.Equal("not", cizim.Strokes[^1].Text);

        Assert.True(cizim.Undo());
        Assert.True(cizim.Undo());
        Assert.Equal(2, cizim.NextNumber);
        cizim.Clear();
        Assert.Empty(cizim.Strokes);
        Assert.False(cizim.Undo());
    }

    /// <summary>Kutu sürükleme yönünden bağımsızdır; ok başının kanatları uçtan geriye ve okun iki yanındadır, kısa okta okun boyuna iner.</summary>
    [Fact]
    public void KutuVeOkBasiGeometrisi()
    {
        Assert.Equal((N(2, 3), 8d, 4d), ScreenSketch.Box(N(10, 3), N(2, 7)));

        var (sol, sag) = ScreenSketch.ArrowHead(N(0, 0), N(100, 0), 20);
        Assert.InRange(sol.X, 82.6, 82.7);
        Assert.InRange(sag.X, 82.6, 82.7);
        Assert.True(sol.Y > 0 != sag.Y > 0);
        Assert.Equal(10, Math.Abs(sol.Y), 6);

        var (kisa, _) = ScreenSketch.ArrowHead(N(0, 0), N(5, 0), 20);
        Assert.Equal(5 - 5 * Math.Cos(Math.PI / 6), kisa.X, 6);
        Assert.Equal((N(3, 3), N(3, 3)), ScreenSketch.ArrowHead(N(3, 3), N(3, 3), 20));

        Assert.Equal(SketchTool.Box, ScreenSketch.ToolOf('r'));
        Assert.Equal(SketchTool.Text, ScreenSketch.ToolOf('T'));
        Assert.Null(ScreenSketch.ToolOf('x'));
    }

    /// <summary>
    /// Varsayılan tuş Ctrl+F2 ve öbür kısayollarla çakışmaz. Çizimden önceki yedi eylemli ayar okunur, çizim varsayılanını
    /// alır; varsayılan tuş eski bir atamadaysa yalnız çizim atanmamış kalır. Kayıt yokken kısayol katmanı açmaz.
    /// </summary>
    [Fact]
    public void KisayolVarsayilaniEskiAyarVeKayitsizBasis()
    {
        var bag = RecorderHotkeys.Of(RecorderHotkeys.All, HotkeyAction.Draw);
        Assert.Equal((Key.F2, KeyModifiers.Control, 0x71u), (bag.Key, bag.Modifiers, bag.VirtualKey));
        Assert.Equal(HotkeyAction.Draw, RecorderHotkeys.All[^1].Action);

        var eski = RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,0:F11,0:F6,2:F5")!;
        Assert.Equal(bag, RecorderHotkeys.Of(eski, HotkeyAction.Draw));
        var cakisan = RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,0:F11,0:F6,2:F2")!;
        Assert.False(RecorderHotkeys.IsAssigned(RecorderHotkeys.Of(cakisan, HotkeyAction.Draw)));
        Assert.Equal(KeyModifiers.Control, RecorderHotkeys.Of(cakisan, HotkeyAction.PushToTalk).Modifiers);

        using var ayar = new KaydediciAyarTests.OzelAyar();
        var sonuc = AppHost.Run(() =>
        {
            var katman = new SahteKatman();
            var view = new RecorderView(ayar.Yol) { SketchLayer = katman };
            var kostu = view.RunHotkeyAsync(HotkeyAction.Draw).GetAwaiter().GetResult();
            view.ToggleSketch();
            return (kostu, katman.Acilis);
        });
        Assert.False(sonuc.kostu);
        Assert.Equal(0, sonuc.Acilis);
    }

    private sealed class SahteKatman : ISketchLayer
    {
        public int Acilis;

        public bool Visible { get; private set; }

        public bool Drawing => Visible;

        public void Toggle(PixelRect area)
        {
            Acilis++;
            Visible = true;
        }

        public void Close() => Visible = false;
    }

    /// <summary>
    /// Gerçek pencere bölgeyi kaplar. Çizim kipinde Windows'un isabet testi pencereyi bulur ve fare izi çizer; araç tuşu
    /// kutuya, numaraya ve yazıya geçirir, Ctrl+Z geri alır. Kısayola yeniden basınca izler kalır, isabet testi alttaki
    /// pencereye geçer; üçüncü basış yine çizer. Esc izleri siler ve katmanı kapatır. Çizilen kutu pencerenin kendi
    /// çiziminde görünür, içi boş kalır.
    /// </summary>
    [Fact]
    public void GercekPencereCizerGecirirVeEscIleKapanir()
    {
        if (!OperatingSystem.IsWindows()) return;
        var alan = new PixelRect(200, 160, 400, 300);
        var sonuc = AppHost.Run(() =>
        {
            var katman = new RecorderSketchHost();
            katman.Toggle(alan);
            var pencere = katman.Window!;
            Bekle(300);
            var tutamak = pencere.TryGetPlatformHandle()!.Handle;
            var konum = pencere.Position;
            var cizimde = (Gecirir: pencere.PassesThrough, Bizim: Bizim(400, 300, tutamak));

            pencere.Down(N(10, 10));
            pencere.Drag(N(40, 12));
            pencere.Up();
            pencere.Press(Key.B, KeyModifiers.None);
            pencere.Down(N(60, 60));
            pencere.Drag(N(120, 110));
            pencere.Up();
            pencere.Press(Key.N, KeyModifiers.None);
            pencere.Down(N(200, 200));
            pencere.Press(Key.T, KeyModifiers.None);
            pencere.Down(N(220, 40));
            var yaziAcik = pencere.Editing;
            pencere.Type("bura");
            pencere.CommitText();
            var araclar = pencere.Sketch.Strokes.Select(s => s.Tool).ToArray();
            pencere.Press(Key.Z, KeyModifiers.Control);
            var geriAlinca = pencere.Sketch.Strokes.Count;

            var resim = Ciz(pencere.Ink, (int)Math.Ceiling(pencere.Width), (int)Math.Ceiling(pencere.Height));

            katman.Toggle(alan);
            Bekle(150);
            var gecis = (Ciziyor: katman.Drawing, Gecirir: pencere.PassesThrough, Bizim: Bizim(400, 300, tutamak), Iz: pencere.Sketch.Strokes.Count,
                Yok: pencere.Press(Key.Escape, KeyModifiers.None));
            katman.Toggle(alan);
            Bekle(150);
            var yine = (Ciziyor: katman.Drawing, Gecirir: pencere.PassesThrough, Bizim: Bizim(400, 300, tutamak));
            pencere.Press(Key.Escape, KeyModifiers.None);
            Bekle(150);
            return (konum, cizimde, araclar, yaziAcik, geriAlinca, resim, gecis, yine, Acik: katman.Visible, Iz: pencere.Sketch.Strokes.Count);
        });

        Assert.Equal(alan.Position, sonuc.konum);
        Assert.False(sonuc.cizimde.Gecirir);
        Assert.True(sonuc.cizimde.Bizim);
        Assert.True(sonuc.yaziAcik);
        Assert.Equal(new[] { SketchTool.Pen, SketchTool.Box, SketchTool.Number, SketchTool.Text }, sonuc.araclar);
        Assert.Equal(3, sonuc.geriAlinca);
        Assert.True(sonuc.resim.Kenar > 0, "kutunun kenarı çizilmedi");
        Assert.Equal(0, sonuc.resim.Ic);
        Assert.False(sonuc.gecis.Ciziyor);
        Assert.True(sonuc.gecis.Gecirir);
        Assert.False(sonuc.gecis.Bizim);
        Assert.Equal(3, sonuc.gecis.Iz);
        Assert.False(sonuc.gecis.Yok);
        Assert.True(sonuc.yine.Ciziyor);
        Assert.False(sonuc.yine.Gecirir);
        Assert.True(sonuc.yine.Bizim);
        Assert.False(sonuc.Acik);
        Assert.Equal(0, sonuc.Iz);
    }

    private static (int Kenar, int Ic) Ciz(Avalonia.Visual cizim, int w, int h)
    {
        using var hedef = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        hedef.Render(cizim);
        var piksel = new byte[w * h * 4];
        var tutamak = GCHandle.Alloc(piksel, GCHandleType.Pinned);
        try { hedef.CopyPixels(new PixelRect(0, 0, w, h), tutamak.AddrOfPinnedObject(), piksel.Length, w * 4); }
        finally { tutamak.Free(); }

        int Alfa(int x, int y) => piksel[(y * w + x) * 4 + 3];
        var kenar = Enumerable.Range(65, 50).Count(y => Alfa(60, y) > 0);
        var ic = Enumerable.Range(70, 30).Sum(y => Enumerable.Range(70, 40).Count(x => Alfa(x, y) > 0));
        return (kenar, ic);
    }

    private static void Bekle(int ms)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms) Dispatcher.UIThread.RunJobs();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Nokta
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Nokta nokta);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    private static bool Bizim(int x, int y, IntPtr pencere)
        => GetAncestor(WindowFromPoint(new Nokta { X = x, Y = y }), 2) == pencere;
}
