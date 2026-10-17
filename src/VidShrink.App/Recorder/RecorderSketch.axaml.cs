using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>Kayıt sürerken ekrana çizim katmanı; testler sahtesini verir.</summary>
internal interface ISketchLayer
{
    bool Visible { get; }

    bool Drawing { get; }

    /// <summary>Kapalıysa <paramref name="area"/> üstünde çizim kipinde açar; açıksa çizim kipine girer ya da çıkar.</summary>
    void Toggle(PixelRect area);

    void Close();
}

/// <summary>Gerçek katman: pencereyi ilk basışta kurar, kapanınca bırakır.</summary>
internal sealed class RecorderSketchHost : ISketchLayer
{
    private RecorderSketch? _window;

    public bool Visible => _window is { IsVisible: true };

    public bool Drawing => Visible && _window!.Drawing;

    internal RecorderSketch? Window => _window;

    public void Toggle(PixelRect area)
    {
        if (!Visible)
        {
            _window = new RecorderSketch();
            _window.Dismissed += (_, _) => Close();
            _window.Place(area);
            _window.Show();
            _window.EnterDraw();
            return;
        }

        if (_window!.Drawing) _window.LeaveDraw();
        else _window.EnterDraw();
    }

    public void Close()
    {
        var window = _window;
        _window = null;
        window?.Close();
    }
}

/// <summary>
/// later.md #42, EKB-04: kayıt bölgesini kaplayan saydam çizim penceresi. Kayda girer; kayıttan çıkarılmaz.
/// Çizim kipinde fareyi ve klavyeyi alır: P kalem, A ok, B kutu, N numara, T yazı, Ctrl+Z geri al, Delete temizle,
/// Esc siler ve kapatır. Aynı seçimler kayda girmeyen araç panelinde de durur (<see cref="RecorderSketchTools"/>); panel
/// yalnız çizim kipinde görünür. Kipten çıkınca çizimler ekranda kalır ve pencere tıklamayı alttaki uygulamaya geçirir;
/// bu geçiş Windows'ta pencere biçimiyle yapılır, öbür sistemlerde kipten çıkmak çizimi siler ve pencereyi kapatır.
/// </summary>
internal sealed partial class RecorderSketch : Window
{
    private SketchPoint? _textAt;
    private PixelRect _area;
    private RecorderSketchTools? _tools;

    public RecorderSketch()
    {
        InitializeComponent();
        Ink.Sketch = Sketch;
        Sketch.Changed += Ink.InvalidateVisual;
        Surface.PointerPressed += (_, e) =>
        {
            var point = e.GetPosition(Surface);
            e.Pointer.Capture(Surface);
            Down(new SketchPoint(point.X, point.Y));
            e.Handled = true;
        };
        Surface.PointerMoved += (_, e) =>
        {
            if (!e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed) return;
            var point = e.GetPosition(Surface);
            Drag(new SketchPoint(point.X, point.Y));
        };
        Surface.PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            Up();
        };
        Entry.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitText();
            else if (e.Key == Key.Escape) CancelText();
            else return;
            e.Handled = true;
        };
        KeyDown += (_, e) => e.Handled = Press(e.Key, e.KeyModifiers);
        Closed += (_, _) =>
        {
            var tools = _tools;
            _tools = null;
            tools?.Close();
        };
    }

    /// <summary>Araç paneli; ilk çizim kipinde kurulur.</summary>
    internal RecorderSketchTools? Tools => _tools;

    internal ScreenSketch Sketch { get; } = new();

    internal bool Drawing { get; private set; }

    internal bool Editing => Entry.IsVisible;

    internal event EventHandler? Dismissed;

    internal void Place(PixelRect area)
    {
        _area = area;
        var scaling = (Screens.ScreenFromPoint(area.Center) ?? Screens.Primary)?.Scaling ?? 1;
        Position = area.Position;
        Width = area.Width / scaling;
        Height = area.Height / scaling;
        Ink.Width = Width;
        Ink.Height = Height;
    }

    internal void EnterDraw()
    {
        Drawing = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        SetPassThrough(false);
        _tools ??= new RecorderSketchTools(this);
        _tools.Show();
        _tools.Place(_area);
        Activate();
        Focus();
    }

    internal void LeaveDraw()
    {
        CommitText();
        Drawing = false;
        Cursor = Cursor.Default;
        _tools?.Hide();
        if (!OperatingSystem.IsWindows())
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            return;
        }

        SetPassThrough(true);
    }

    internal void Down(SketchPoint point)
    {
        if (!Drawing) return;
        CommitText();
        if (Sketch.Tool == SketchTool.Text)
        {
            _textAt = point;
            Canvas.SetLeft(Entry, point.X);
            Canvas.SetTop(Entry, point.Y);
            Entry.Text = string.Empty;
            Entry.Foreground = Ink.Brush(Sketch.Ink);
            Entry.IsVisible = true;
            Entry.Focus();
            return;
        }

        Sketch.Press(point);
    }

    internal void Drag(SketchPoint point)
    {
        if (Drawing) Sketch.Move(point);
    }

    internal void Up() => Sketch.Release();

    internal void Type(string text) => Entry.Text = text;

    internal bool Press(Key key, KeyModifiers modifiers)
    {
        if (!Drawing || Editing) return false;
        if (key == Key.Escape)
        {
            Sketch.Clear();
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (key == Key.Z && modifiers.HasFlag(KeyModifiers.Control)) return Sketch.Undo() || true;
        if (key == Key.Delete)
        {
            Sketch.Clear();
            return true;
        }

        if (modifiers != KeyModifiers.None || key < Key.A || key > Key.Z) return false;
        if (ScreenSketch.ToolOf((char)('A' + (key - Key.A))) is not { } tool) return false;
        Sketch.Tool = tool;
        return true;
    }

    internal void CommitText()
    {
        if (!Entry.IsVisible) return;
        Entry.IsVisible = false;
        if (_textAt is { } at) Sketch.AddText(at, Entry.Text ?? string.Empty);
        _textAt = null;
        Focus();
    }

    private void CancelText()
    {
        Entry.IsVisible = false;
        _textAt = null;
        Focus();
    }

    /// <summary>Pencerenin tıklama geçirip geçirmediği; Windows dışında hep <c>false</c>.</summary>
    internal bool PassesThrough
        => OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle
           && (GetWindowLongPtrW(handle.Handle, GwlExStyle).ToInt64() & RecorderFrame.ExTransparent) != 0;

    private void SetPassThrough(bool on)
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        var style = GetWindowLongPtrW(handle.Handle, GwlExStyle).ToInt64();
        var wanted = PassStyle(style, on);
        if (wanted != style) SetWindowLongPtrW(handle.Handle, GwlExStyle, new IntPtr(wanted));
    }

    internal static long PassStyle(long exStyle, bool on)
    {
        const long pass = RecorderFrame.ExTransparent | RecorderFrame.ExLayered | RecorderFrame.ExNoActivate;
        return on ? exStyle | pass | RecorderFrame.ExToolWindow : (exStyle & ~pass) | RecorderFrame.ExToolWindow;
    }

    private const int GwlExStyle = -20;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(nint hwnd, int index, IntPtr value);
}

/// <summary>
/// Çizimin kendisi: izler sırayla, sürmekte olan iz en üstte. Her iz kendi mürekkebiyle: <c>NeonEmber</c>, <c>NeonBlue</c>,
/// <c>NeonPink</c>, <c>NeonSuccess</c>; numaranın yazısı <c>OnNeon</c>.
/// </summary>
internal sealed class SketchCanvas : Control
{
    internal ScreenSketch? Sketch { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Sketch is null) return;
        foreach (var stroke in Sketch.Strokes) Draw(context, stroke);
        if (Sketch.Active is { } active) Draw(context, active);
    }

    internal static string BrushKey(SketchInk ink) => ink switch
    {
        SketchInk.Blue => "NeonBlue",
        SketchInk.Pink => "NeonPink",
        SketchInk.Green => "NeonSuccess",
        _ => "NeonEmber"
    };

    internal IBrush Brush(SketchInk ink) => Brush(BrushKey(ink));

    private void Draw(DrawingContext context, SketchStroke stroke)
    {
        var ink = Brush(stroke.Ink);
        var pen = new Pen(ink, Token("RecorderSketchStroke"), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var points = stroke.Vertices.Select(p => new Point(p.X, p.Y)).ToArray();
        switch (stroke.Tool)
        {
            case SketchTool.Pen when points.Length == 1:
                context.DrawEllipse(ink, null, points[0], pen.Thickness / 2, pen.Thickness / 2);
                break;
            case SketchTool.Pen:
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(points[0], false);
                    foreach (var point in points.Skip(1)) g.LineTo(point);
                    g.EndFigure(false);
                }
                context.DrawGeometry(null, pen, geometry);
                break;
            case SketchTool.Arrow when points.Length == 2:
                context.DrawLine(pen, points[0], points[1]);
                var (left, right) = ScreenSketch.ArrowHead(stroke.Vertices[0], stroke.Vertices[1], Token("RecorderSketchArrowHead"));
                context.DrawLine(pen, points[1], new Point(left.X, left.Y));
                context.DrawLine(pen, points[1], new Point(right.X, right.Y));
                break;
            case SketchTool.Box when points.Length == 2:
                var (corner, width, height) = ScreenSketch.Box(stroke.Vertices[0], stroke.Vertices[1]);
                context.DrawRectangle(null, pen, new Rect(corner.X, corner.Y, width, height));
                break;
            case SketchTool.Number:
                var radius = Token("RecorderSketchNumberSize") / 2;
                context.DrawEllipse(ink, null, points[0], radius, radius);
                var number = Text(stroke.Number.ToString(Strings.Culture), Token("FontSizeMd"), Brush("OnNeon"));
                context.DrawText(number, new Point(points[0].X - number.Width / 2, points[0].Y - number.Height / 2));
                break;
            case SketchTool.Text:
                context.DrawText(Text(stroke.Text, Token("RecorderSketchFont"), ink), points[0]);
                break;
        }
    }

    private FormattedText Text(string text, double size, IBrush brush)
        => new(text, Strings.Culture, FlowDirection.LeftToRight,
            new Typeface(this.TryFindResource("FontSans", out var font) && font is FontFamily family ? family : FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            size, brush);

    private IBrush Brush(string key) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brushes.Transparent;

    private double Token(string key) => this.TryFindResource(key, out var value) && value is double number ? number : 0;
}
