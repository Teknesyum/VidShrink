using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderRegionPicker : Window
{
    private readonly TaskCompletionSource<PixelRect?> _done = new();
    private readonly double? _ratio;
    private PixelRect _desktop;
    private PixelPoint? _start;
    private PixelRect _current;

    public RecorderRegionPicker() : this(null)
    {
    }

    internal RecorderRegionPicker(double? ratio)
    {
        _ratio = ratio;
        InitializeComponent();
        Opened += (_, _) => Cover();
        Closed += (_, _) => _done.TrySetResult(null);
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            _done.TrySetResult(null);
            Close();
        };
    }

    internal Task<PixelRect?> Result => _done.Task;

    internal static async Task<PixelRect?> PickAsync(double? ratio)
    {
        var picker = new RecorderRegionPicker(ratio);
        picker.Show();
        picker.Activate();
        return await picker.Result;
    }

    private void Cover()
    {
        var placements = Screens.All
            .Select((s, i) => new ScreenPlacement(
                new ScreenBounds(i, s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height), s.Scaling))
            .ToList();

        _desktop = RegionDraw.Desktop(Screens.All.Select(s => s.Bounds));
        var cover = RecorderLayout.Cover(placements);
        var yedekOlcek = Screens.Primary?.Scaling ?? 1;

        Position = cover is null ? _desktop.Position : new PixelPoint(cover.X, cover.Y);
        Width = cover?.Width ?? _desktop.Width / yedekOlcek;
        Height = cover?.Height ?? _desktop.Height / yedekOlcek;
        Scrim.Width = Width;
        Scrim.Height = Height;
    }

    private PixelPoint ScreenPoint(PointerEventArgs e) => this.PointToScreen(e.GetPosition(this));

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _start = ScreenPoint(e);
        e.Pointer.Capture(Surface);
        Draw(_start.Value);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_start is not null) Draw(ScreenPoint(e));
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_start is null) return;
        Draw(ScreenPoint(e));
        _start = null;
        e.Pointer.Capture(null);
        if (!RegionDraw.Usable(_current)) return;
        _done.TrySetResult(_current);
        Close();
    }

    private void Draw(PixelPoint end)
    {
        if (_start is not { } start) return;
        _current = RegionDraw.FromDrag(start, end, _ratio, _desktop);
        var topLeft = this.PointToClient(_current.Position);
        var bottomRight = this.PointToClient(_current.BottomRight);
        Canvas.SetLeft(Selection, topLeft.X);
        Canvas.SetTop(Selection, topLeft.Y);
        Selection.Width = bottomRight.X - topLeft.X;
        Selection.Height = bottomRight.Y - topLeft.Y;
        Selection.IsVisible = true;
        TxtSize.Text = string.Format(CultureInfo.InvariantCulture, "{0} × {1}", _current.Width, _current.Height);
        Canvas.SetLeft(SizeTag, topLeft.X);
        Canvas.SetTop(SizeTag, bottomRight.Y);
        SizeTag.IsVisible = true;
    }
}
