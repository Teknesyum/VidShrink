using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VidShrink.App.Recorder;

internal interface IMagnifier
{
    bool Running { get; }

    void Start();

    void Stop();
}

internal sealed class NoMagnifier : IMagnifier
{
    public bool Running { get; private set; }

    public void Start() => Running = true;

    public void Stop() => Running = false;
}

internal static class MagnifierPlace
{
    internal static PixelRect Source(PixelPoint cursor, int lens, double zoom, PixelRect screen)
    {
        var side = Math.Max(1, (int)Math.Round(lens / Math.Max(1, zoom)));
        var x = Math.Clamp(cursor.X - side / 2, screen.X, Math.Max(screen.X, screen.Right - side));
        var y = Math.Clamp(cursor.Y - side / 2, screen.Y, Math.Max(screen.Y, screen.Bottom - side));
        return new PixelRect(x, y, side, side);
    }

    internal static PixelPoint Window(PixelRect source, int lens, int gap, PixelRect screen)
    {
        var x = source.Right + gap + lens <= screen.Right ? source.Right + gap : source.X - gap - lens;
        var y = source.Bottom + gap + lens <= screen.Bottom ? source.Bottom + gap : source.Y - gap - lens;
        return new PixelPoint(x, y);
    }
}

internal static class ScreenGrab
{
    internal static bool Copy(PixelRect source, WriteableBitmap target)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        var memory = CreateCompatibleDC(screen);
        var info = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = source.Width,
            Height = -source.Height,
            Planes = 1,
            BitCount = 32
        };
        var section = CreateDIBSection(screen, ref info, 0, out var bits, IntPtr.Zero, 0);
        try
        {
            if (memory == IntPtr.Zero || section == IntPtr.Zero) return false;
            var old = SelectObject(memory, section);
            var ok = BitBlt(memory, 0, 0, source.Width, source.Height, screen, source.X, source.Y, 0x00CC0020);
            SelectObject(memory, old);
            if (!ok) return false;

            using var frame = target.Lock();
            var rows = Math.Min(source.Height, frame.Size.Height);
            var columns = Math.Min(source.Width, frame.Size.Width);
            var row = new byte[source.Width * 4];
            for (var r = 0; r < rows; r++)
            {
                Marshal.Copy(bits + r * source.Width * 4, row, 0, row.Length);
                Marshal.Copy(row, 0, frame.Address + r * frame.RowBytes, columns * 4);
            }

            return true;
        }
        finally
        {
            if (section != IntPtr.Zero) DeleteObject(section);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    internal static PixelPoint? Cursor()
        => OperatingSystem.IsWindows() && GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader info, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);
}

internal partial class RecorderMagnifier : Window
{
    private WriteableBitmap? _bitmap;

    public RecorderMagnifier()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } handle) OverlayPlace.ClickThrough(handle.Handle);
        };
    }

    internal PixelRect LastSource { get; private set; }

    internal bool LastGrab { get; private set; }

    internal void Follow(PixelPoint cursor)
    {
        var screen = Screens.ScreenFromPoint(cursor) ?? Screens.Primary;
        if (screen is null) return;
        var scaling = screen.Scaling;
        var lens = (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderMagnifierSize") * scaling);
        var gap = (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderMagnifierGap") * scaling);
        var zoom = OverlayPlace.Size(this, "RecorderMagnifierZoom");

        var source = MagnifierPlace.Source(cursor, lens, zoom, screen.Bounds);
        Position = MagnifierPlace.Window(source, lens, gap, screen.Bounds);
        LastSource = source;

        if (_bitmap is null || _bitmap.PixelSize != source.Size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(source.Size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        LastGrab = ScreenGrab.Copy(source, _bitmap);
        Lens.Source = null;
        Lens.Source = _bitmap;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Lens.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}

internal sealed class RecorderMagnifierHost : IMagnifier
{
    private RecorderMagnifier? _window;
    private DispatcherTimer? _timer;

    public bool Running => _window is not null;

    internal RecorderMagnifier? Window => _window;

    public void Start()
    {
        if (_window is not null) return;
        _window = new RecorderMagnifier();
        Tick();
        _window.Show();
        var interval = _window.TryFindResource("RecorderMagnifierInterval", out var value) && value is TimeSpan span
            ? span
            : TimeSpan.FromMilliseconds(33);
        _timer = new DispatcherTimer(interval, DispatcherPriority.Render, (_, _) => Tick());
        _timer.Start();
    }

    internal void Tick()
    {
        if (_window is not null && ScreenGrab.Cursor() is { } cursor) _window.Follow(cursor);
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _window?.Close();
        _window = null;
    }
}
