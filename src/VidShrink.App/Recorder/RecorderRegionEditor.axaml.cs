using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Automation;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// Düzenleyicinin araç paneli kaydın hangi anında: boştayken bölge düzenlenir ve kayıt
/// başlatılır; geri sayımda, kayıtta ve duraklatmada bölge kilitlenir, panelde durdur
/// (ve duraklat/sürdür) kalır.
/// </summary>
internal enum RegionEditorPhase
{
    Idle,
    Counting,
    Running,
    Paused
}

/// <summary>
/// Çizimden sonra ekranda kalan bölge. Kırmızı çerçeve, sekiz tutamak ve üstünde yalnız
/// simgeden oluşan araç paneli; kayıt sürerken bölge kilitlenir ve panelde durdurma kalır; kenardan sürüklenince taşınır, tutamaktan sürüklenince boyutlanır. Windows'ta
/// pencere biçimi yalnız halka, tutamaklar ve panelden oluşuyor, iç alan tıklamayı alttaki
/// uygulamaya bırakıyor. Hesapların hepsi <see cref="RegionEdit"/> içinde.
/// </summary>
internal partial class RecorderRegionEditor : Window
{
    private PixelRect _region;
    private double? _ratio;
    private PixelRect _desktop;
    private PixelRect _toolbar;
    private (RegionGrip Grip, PixelPoint Start, PixelRect Region)? _drag;
    private bool _closingQuietly;
    private RegionEditorPhase _phase = RegionEditorPhase.Idle;
    private readonly Dictionary<RegionGrip, Cursor> _cursors = new();

    internal event EventHandler<PixelRect>? RegionChanged;

    internal event EventHandler<PixelRect>? RegionCommitted;

    internal event EventHandler? StartRequested;

    internal event EventHandler? SettingsRequested;

    internal event EventHandler? PauseRequested;

    internal event EventHandler? ResumeRequested;

    internal event EventHandler? StopRequested;

    internal event EventHandler? Dismissed;

    public RecorderRegionEditor() : this(new PixelRect(0, 0, 640, 360), null)
    {
    }

    internal RecorderRegionEditor(PixelRect region, double? ratio)
    {
        _region = region;
        _ratio = ratio;
        InitializeComponent();
        Opened += (_, _) =>
        {
            _desktop = RecorderRegionPicker.Cover(this);
            _region = RegionEdit.Fit(_region, _desktop);
            MakeShaped();
            Arrange();
        };
        Closed += (_, _) =>
        {
            if (!_closingQuietly) Dismissed?.Invoke(this, EventArgs.Empty);
        };
        Surface.PointerPressed += OnPressed;
        Surface.PointerMoved += OnMoved;
        Surface.PointerReleased += OnReleased;
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        BtnStart.Click += (_, _) => StartRequested?.Invoke(this, EventArgs.Empty);
        BtnSettings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        BtnPause.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        BtnResume.Click += (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty);
        BtnStop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        BtnClose.Click += (_, _) => Close();
        ShowPhase();
        ShowSize();
    }

    internal PixelRect Region => _region;

    internal PixelRect ToolbarBounds => _toolbar;

    internal bool CaptureExcluded { get; private set; }

    internal bool Shaped { get; private set; }

    internal RegionEditorPhase Phase => _phase;

    /// <summary>Bölgenin boyu; panelde yazı yok, başlat düğmesinin ipucunda duruyor.</summary>
    internal string SizeText { get; private set; } = string.Empty;

    internal static bool Editable(RegionEditorPhase phase) => phase == RegionEditorPhase.Idle;

    internal void SetPhase(RegionEditorPhase phase)
    {
        if (phase == _phase) return;
        _phase = phase;
        if (!Editable(phase)) _drag = null;
        ShowPhase();
        if (_desktop.Width > 0) Arrange();
    }

    private void ShowPhase()
    {
        var editable = Editable(_phase);
        BtnStart.IsVisible = editable;
        BtnSettings.IsVisible = editable;
        BtnPause.IsVisible = _phase == RegionEditorPhase.Running;
        BtnResume.IsVisible = _phase == RegionEditorPhase.Paused;
        BtnStop.IsVisible = !editable;
        Edge.IsVisible = editable;
        foreach (var (_, handle) in HandleControls()) handle.IsVisible = editable;
        if (!editable) Surface.Cursor = CursorOf(RegionGrip.None);
    }

    private void ShowSize()
    {
        SizeText = Bicim.Cozunurluk(_region.Width, _region.Height);
        var start = LanguageCatalog.Display(Strings.Get("recorder.region.start"));
        ToolTip.SetTip(BtnStart, $"{start} · {SizeText}");
        AutomationProperties.SetName(BtnStart, $"{start} {SizeText}");
    }

    /// <summary>Kapanışı <see cref="Dismissed"/> olayı çıkarmadan yapar; kapatan taraf zaten biliyor.</summary>
    internal void CloseQuietly()
    {
        _closingQuietly = true;
        Close();
    }

    internal void Update(PixelRect region, double? ratio)
    {
        _ratio = ratio;
        if (_drag is not null) return;
        _region = _desktop.Width > 0 ? RegionEdit.Fit(region, _desktop) : region;
        if (_desktop.Width > 0) Arrange();
    }

    internal static StandardCursorType CursorFor(RegionGrip grip) => grip switch
    {
        RegionGrip.Move => StandardCursorType.SizeAll,
        RegionGrip.Left or RegionGrip.Right => StandardCursorType.SizeWestEast,
        RegionGrip.Top or RegionGrip.Bottom => StandardCursorType.SizeNorthSouth,
        RegionGrip.TopLeft => StandardCursorType.TopLeftCorner,
        RegionGrip.TopRight => StandardCursorType.TopRightCorner,
        RegionGrip.BottomLeft => StandardCursorType.BottomLeftCorner,
        RegionGrip.BottomRight => StandardCursorType.BottomRightCorner,
        _ => StandardCursorType.Arrow
    };

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && Editable(_phase))
        {
            e.Handled = true;
            StartRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private double Resource(string key)
        => this.TryFindResource(key, out var value) && value is double d ? d : 0;

    private int Pixels(string key) => Math.Max(1, (int)Math.Ceiling(Resource(key) * RenderScaling));

    private int EdgePixels => RecorderFrame.EdgePixels(Resource("RecorderFrameThickness"), RenderScaling);

    private int Band => Math.Max(EdgePixels, Pixels("RecorderRegionGrip"));

    private int HandlePixels => Pixels("RecorderRegionHandle");

    private PixelPoint ScreenPoint(PointerEventArgs e) => this.PointToScreen(e.GetPosition(this));

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Editable(_phase) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = ScreenPoint(e);
        var grip = RegionEdit.Hit(point, _region, Band, HandlePixels);
        if (grip == RegionGrip.None) return;
        _drag = (grip, point, _region);
        e.Pointer.Capture(Surface);
        e.Handled = true;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!Editable(_phase)) return;
        var point = ScreenPoint(e);
        if (_drag is not { } drag)
        {
            Surface.Cursor = CursorOf(RegionEdit.Hit(point, _region, Band, HandlePixels));
            return;
        }

        var delta = new PixelVector(point.X - drag.Start.X, point.Y - drag.Start.Y);
        var next = RegionEdit.Drag(drag.Region, drag.Grip, delta, _ratio, _desktop);
        if (next == _region) return;
        _region = next;
        Arrange();
        RegionChanged?.Invoke(this, _region);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_drag is null) return;
        _drag = null;
        e.Pointer.Capture(null);
        RegionCommitted?.Invoke(this, _region);
    }

    private Cursor CursorOf(RegionGrip grip)
    {
        if (!_cursors.TryGetValue(grip, out var cursor)) _cursors[grip] = cursor = new Cursor(CursorFor(grip));
        return cursor;
    }

    private void Place(Control control, PixelPoint screenTopLeft, PixelPoint screenBottomRight)
    {
        var topLeft = this.PointToClient(screenTopLeft);
        var bottomRight = this.PointToClient(screenBottomRight);
        Canvas.SetLeft(control, topLeft.X);
        Canvas.SetTop(control, topLeft.Y);
        control.Width = Math.Max(0, bottomRight.X - topLeft.X);
        control.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
    }

    private IEnumerable<(RegionGrip Grip, Control Handle)> HandleControls() => new (RegionGrip, Control)[]
    {
        (RegionGrip.TopLeft, HandleTopLeft), (RegionGrip.Top, HandleTop), (RegionGrip.TopRight, HandleTopRight),
        (RegionGrip.Right, HandleRight), (RegionGrip.BottomRight, HandleBottomRight), (RegionGrip.Bottom, HandleBottom),
        (RegionGrip.BottomLeft, HandleBottomLeft), (RegionGrip.Left, HandleLeft)
    };

    private void Arrange()
    {
        var outer = RecorderFrame.Outer(_region, EdgePixels);
        Place(Edge, outer.Position, outer.BottomRight);

        foreach (var (grip, handle) in HandleControls())
        {
            var center = this.PointToClient(RegionEdit.HandleCenter(_region, grip));
            Canvas.SetLeft(handle, center.X - handle.Width / 2);
            Canvas.SetTop(handle, center.Y - handle.Height / 2);
        }

        ShowSize();
        Toolbar.Measure(Size.Infinity);
        var scaling = RenderScaling;
        var bar = new PixelSize(
            (int)Math.Ceiling(Toolbar.DesiredSize.Width * scaling),
            (int)Math.Ceiling(Toolbar.DesiredSize.Height * scaling));
        var gap = Math.Max(HandlePixels / 2, Band) + Pixels("RecorderRegionToolbarGap");
        var screen = RegionEdit.ScreenOf(_region, Screens.All.Select(s => s.Bounds), _desktop);
        var at = RegionEdit.Toolbar(_region, bar, gap, screen);
        _toolbar = new PixelRect(at, bar);
        var client = this.PointToClient(at);
        Canvas.SetLeft(Toolbar, client.X);
        Canvas.SetTop(Toolbar, client.Y);

        ApplyShape();
    }

    private void MakeShaped()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        CaptureExcluded = SetWindowDisplayAffinity(handle.Handle, RecorderFrame.ExcludeFromCaptureAffinity);
    }

    private void ApplyShape()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        var parts = RegionEdit.Shape(_region, Band, HandlePixels, _toolbar, Position, Editable(_phase));
        var shape = CreateRectRgn(0, 0, 0, 0);
        if (shape == IntPtr.Zero) return;
        foreach (var part in parts)
        {
            var piece = CreateRectRgn(part.X, part.Y, part.Right, part.Bottom);
            if (piece == IntPtr.Zero) continue;
            CombineRgn(shape, shape, piece, RgnOr);
            DeleteObject(piece);
        }

        Shaped = SetWindowRgn(handle.Handle, shape, true) != 0;
        if (!Shaped) DeleteObject(shape);
    }

    private const int RgnOr = 2;

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}

internal interface IRegionEditorHost
{
    bool IsOpen { get; }

    event EventHandler<PixelRect>? Changed;

    event EventHandler<PixelRect>? Committed;

    event EventHandler? StartRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? PauseRequested;

    event EventHandler? ResumeRequested;

    event EventHandler? StopRequested;

    event EventHandler? Dismissed;

    void Show(PixelRect region, double? ratio, RegionEditorPhase phase);

    void Hide();

    void Close();
}

internal sealed class RegionEditorHost : IRegionEditorHost
{
    private RecorderRegionEditor? _editor;

    public bool IsOpen => _editor is not null;

    public event EventHandler<PixelRect>? Changed;

    public event EventHandler<PixelRect>? Committed;

    public event EventHandler? StartRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? ResumeRequested;

    public event EventHandler? StopRequested;

    public event EventHandler? Dismissed;

    public void Show(PixelRect region, double? ratio, RegionEditorPhase phase)
    {
        if (_editor is null)
        {
            var editor = new RecorderRegionEditor(region, ratio);
            editor.RegionChanged += (_, r) => Changed?.Invoke(this, r);
            editor.RegionCommitted += (_, r) => Committed?.Invoke(this, r);
            editor.StartRequested += (_, _) => StartRequested?.Invoke(this, EventArgs.Empty);
            editor.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            editor.PauseRequested += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
            editor.ResumeRequested += (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty);
            editor.StopRequested += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
            editor.Dismissed += (_, _) =>
            {
                if (!ReferenceEquals(_editor, editor)) return;
                _editor = null;
                Dismissed?.Invoke(this, EventArgs.Empty);
            };
            _editor = editor;
            editor.SetPhase(phase);
            editor.Show();
            editor.Activate();
            return;
        }

        if (!_editor.IsVisible) _editor.Show();
        _editor.SetPhase(phase);
        _editor.Update(region, ratio);
    }

    public void Hide() => _editor?.Hide();

    public void Close()
    {
        var editor = _editor;
        _editor = null;
        editor?.CloseQuietly();
    }
}
