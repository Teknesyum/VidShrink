using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kayıt çerçevesinin X11 kolu. Windows'ta pencere <c>SetWindowRgn</c> ile kenar şeridine
/// indiriliyor ve <c>WS_EX_TRANSPARENT</c> ile tıklamayı geçiriyor; X11'de o kollar yoktu ve
/// pencere bölgenin tamamını kaplayan bir dikdörtgen kalıyordu. Birleştirici (compositor)
/// olmayan masaüstünde saydam zemin siyah çiziliyor: kayıt boyunca kaydedilen monitör
/// kullanıcıya kapanıyor, video da ilk saniyelerden sonra siyah kalıyordu.
/// Aynı şerit X Shape eklentisiyle çizim biçimine, boş küme girdi biçimine yazılır.
/// </summary>
internal partial class RecorderFrame
{
    private const int ShapeBounding = 0;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int Unsorted = 0;

    private static readonly Lazy<IntPtr> X11Display = new(() =>
    {
        try { return XOpenDisplay(IntPtr.Zero); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return IntPtr.Zero; }
    });

    /// <summary>Çerçeve bölgenin içine düştüyse (dışında yer yoksa) kenar şeridi kayda girer.</summary>
    internal bool Inside { get; private set; }

    /// <summary>
    /// Pencereyi şeride indirir, girdiyi tümden geçirir. Kitaplık ya da ekran bağlantısı
    /// yoksa <c>false</c>: çağıran çerçeveyi göstermez, siyah dikdörtgen bırakmaz.
    /// </summary>
    internal static bool ShapeX11(IntPtr window, IReadOnlyList<PixelRect> ring)
    {
        var display = X11Display.Value;
        if (display == IntPtr.Zero || window == IntPtr.Zero) return false;
        try
        {
            var rects = new XRectangle[ring.Count];
            for (var i = 0; i < rects.Length; i++)
                rects[i] = new XRectangle
                {
                    X = (short)ring[i].X,
                    Y = (short)ring[i].Y,
                    Width = (ushort)ring[i].Width,
                    Height = (ushort)ring[i].Height
                };
            XShapeCombineRectangles(display, window, ShapeBounding, 0, 0, rects, rects.Length, ShapeSet, Unsorted);
            XShapeCombineRectangles(display, window, ShapeInput, 0, 0, Array.Empty<XRectangle>(), 0, ShapeSet, Unsorted);
            XFlush(display);
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private void ApplyX11Shape()
    {
        if (!OperatingSystem.IsLinux() || _size.Width <= 0) return;
        if (TryGetPlatformHandle() is not { HandleDescriptor: "XID" } handle) return;
        Shaped = ShapeX11(handle.Handle, Ring(_size, _edge));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRectangle
    {
        public short X;
        public short Y;
        public ushort Width;
        public ushort Height;
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int destKind, int xOff, int yOff,
        XRectangle[] rectangles, int count, int op, int ordering);
}
