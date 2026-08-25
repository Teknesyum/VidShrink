using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

/// <summary>
/// Karşılaştırma paneli: sunum yüzeyi, ayırıcı, yakınlaştırma ve terfi.
///
/// Denetim şeridi T40'ta eklendi ve <see cref="ControlStrip"/> içinde yaşıyor; panel
/// ona yalnız yer ve fare konumu verir. Kare kaynağı burada değil: panel
/// kaynağı bilmez, <see cref="ComparisonSurface"/> üstünden "elimde bir kare var"
/// varsayımıyla çalışır.
/// </summary>
internal partial class ComparisonPanel : UserControl
{
    /// <summary>Klavyeyle ayırıcının küçük adımı: pano genişliğinin yüzde biri.</summary>
    private const double SplitKeyStep = 0.01;

    /// <summary>
    /// Sentetik kare ölçüsü. İki 720p taraf yan yana. T37 2x1080p'yi ölçtü ve sunum yolu
    /// orada da duvar değil; burada daha küçük olmasının sebebi sunum değil, üreteç —
    /// deseni yönetilen kodda pişirmek 2x1080p'de açılışı saniyelerce bekletirdi.
    /// </summary>
    private static readonly PixelSize DemoFrame = new(2560, 720);

    private readonly ZoomGesture _gesture = new();

    // Ayırıcı Margin ile değil, dönüşümle taşınıyor: Margin ölçüme girer ve uçlarda
    // ızgaranın istediği genişliği şişirip pencereye yatay kaydırma çubuğu getirir.
    private readonly TranslateTransform _separatorAt = new();

    private SyntheticFrameSource? _demo;
    private OverlayLayer? _overlay;
    private DispatcherTimer? _landing;

    private bool _turkish;
    private string? _notice;
    private string? _rightNotice;
    private bool _motionReduced;
    private bool _promoted;
    private bool _draggingSeparator;
    private bool _panning;
    private Point _panFrom;
    private double _split = 0.5;

    public ComparisonPanel()
    {
        InitializeComponent();

        // K5: azaltılmış hareket ayarını okuyan tek yer HoverZone. İkinci kopya yok.
        _motionReduced = HoverZone.MotionReduced;
        Surface.Gesture = _gesture;
        SeparatorGrip.RenderTransform = _separatorAt;

        Shell.PointerWheelChanged += OnWheel;
        Shell.KeyDown += OnShellKey;

        Stage.PointerPressed += OnStagePressed;
        Stage.PointerMoved += OnStageMoved;
        Stage.PointerReleased += OnStageReleased;

        SeparatorGrip.PointerPressed += OnSeparatorPressed;
        SeparatorGrip.PointerMoved += OnSeparatorMoved;
        SeparatorGrip.PointerReleased += OnSeparatorReleased;
        SeparatorGrip.PointerEntered += (_, _) => LightSeparator(true);
        SeparatorGrip.PointerExited += (_, _) => LightSeparator(_draggingSeparator || SeparatorGrip.IsFocused);
        SeparatorGrip.GotFocus += (_, _) => LightSeparator(true);
        SeparatorGrip.LostFocus += (_, _) => LightSeparator(_draggingSeparator);
        SeparatorGrip.KeyDown += OnSeparatorKey;

        Stage.SizeChanged += (_, _) => ApplySplit();
        Stage.PointerExited += (_, _) => Strip.PointerGone();

        SetLanguage(false);
        ApplySplit();
        RefreshReadout();
        RefreshEmptyState();

        if (Design.IsDesignMode) StartDemo();
    }

    /// <summary>Terfi hâli. Panel kök katmandaysa doğrudur.</summary>
    internal bool IsPromoted => _promoted;

    /// <summary>Ayırıcının konumu, sıfır ile bir arası. Uçlar geçerlidir.</summary>
    internal double Split
    {
        get => _split;
        set
        {
            var next = Math.Clamp(value, 0, 1);
            if (Math.Abs(next - _split) < 1e-9) return;
            _split = next;
            ApplySplit();
        }
    }

    /// <summary>Jest parametresi — dışarıdan okunur, tanılama ve test içindir.</summary>
    internal ZoomGesture Gesture => _gesture;

    internal ComparisonSurface Frames => Surface;

    /// <summary>Denetim şeridi. Kareyi süren taraf oynat/duraklat/atla isteklerini buradan alır.</summary>
    internal ControlStrip Controls => Strip;

    /// <summary>
    /// Kapalı panel bandını daraltır. İki ölçü de temadan gelir: açıkken sahne en az
    /// <c>PlaybackStageMinHeight</c>, kapalıyken bırakma alanı kadar (<c>DropZoneMinHeight</c>).
    /// </summary>
    internal void SetCompact(bool compact)
        => Shell.MinHeight = Scalar(compact ? "DropZoneMinHeight" : "PlaybackStageMinHeight", compact ? 144 : 256);

    /// <summary>
    /// Boş durumun ikinci satırı. Kare kaynağı bir sebep bildirdiğinde (motor yok, akış
    /// açılamadı) o sebep buraya yazılır; boş bırakılınca panel kendi varsayılanına döner.
    /// Metin İngilizce verilir, ekrana çalışan dilde çıkar.
    /// </summary>
    internal void SetNotice(string? english)
    {
        _notice = string.IsNullOrWhiteSpace(english) ? null : english;
        RefreshTexts();
    }

    /// <summary>
    /// Sağ tarafın perdesi. İşlenmiş dosya yokken görüntü değil sebep gösterilir —
    /// planın §6 dürüstlük kuralı. Boş bırakılınca perde kalkar.
    /// </summary>
    internal void SetRightNotice(string? english)
    {
        _rightNotice = string.IsNullOrWhiteSpace(english) ? null : english;
        RefreshTexts();
        RefreshEmptyState();
        ApplySplit();
    }

    /// <summary>
    /// K7: kare kaynağı bağlanmadan panel denenebilsin diye sentetik üreteci başlatır.
    /// Renkler temadan çözülüyor; üreteç kendi rengini uydurmuyor.
    /// </summary>
    internal void StartDemo()
    {
        if (_demo is not null) return;
        Surface.Configure(DemoFrame);
        _demo = new SyntheticFrameSource(Surface, DemoFrame, Palette(), BlockSize())
        {
            TargetFps = 60
        };
        _demo.Start();
        Strip.StartDemoClock();
        RefreshEmptyState();
        DispatcherTimer.RunOnce(RefreshEmptyState, Motion("MotionSlow", 360));
    }

    internal void StopDemo()
    {
        _demo?.Dispose();
        _demo = null;
        Strip.StopDemoClock();
    }

    /// <summary>
    /// Panelin kendi metinleri. Ana pencerenin ağaç yürüyüşü bu dizgeleri sözlüğünde
    /// bulamaz — sözlük bu sözleşmenin alanında değil — bu yüzden dil geçidi burada.
    /// Barındıran taraf dil değişiminde bunu çağırır.
    /// </summary>
    internal void SetLanguage(bool turkish)
    {
        _turkish = turkish;
        RefreshTexts();
        Strip.SetLanguage(turkish);
        RefreshReadout();
    }

    /// <summary>
    /// Panelin bütün metinleri tek geçitten. Kaynak dizge İngilizce, Türkçesi sözlükte;
    /// panel ikinci bir kopya taşımaz (K5).
    /// </summary>
    private void RefreshTexts()
    {
        EmptyTitle.Text = Say("Comparison panel");
        EmptyHint.Text = Say(_notice ?? "Load a file to see the two sides");
        PlaceholderText.Text = Say("The panel moved to the front");
        LeftBadgeText.Text = LanguageCatalog.Upper(Say("Original"), _turkish);
        RightBadgeText.Text = LanguageCatalog.Upper(Say("Processed"), _turkish);
        RightCurtainText.Text = _rightNotice is null ? string.Empty : Say(_rightNotice);
    }

    private string Say(string english) => LanguageCatalog.Localize(english, _turkish);

    // ---- ayırıcı (K2) -------------------------------------------------------------

    private void ApplySplit()
    {
        Surface.Split = _split;
        var width = Stage.Bounds.Width;
        if (width > 0)
        {
            _separatorAt.X = _split * width - SeparatorGrip.Width / 2;
            // Perde ayırıcıdan sağa uzanır. Genişlik panonun kendi ölçüsünden türüyor,
            // bu yüzden istenen genişlik hiçbir zaman panodan büyük olmaz.
            RightCurtain.Width = Math.Max(0, width - Math.Clamp(_split, 0, 1) * width);
        }
        // Sürükleme kare istemez: yalnız yeniden çizim, kopyalama yok, kod çözme yok.
        Surface.InvalidateVisual();
    }

    private void LightSeparator(bool lit)
    {
        SeparatorLine.Classes.Set("lit", lit);
        SeparatorHandle.Classes.Set("lit", lit);
    }

    private void OnSeparatorPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Stage).Properties.IsLeftButtonPressed) return;
        _draggingSeparator = true;
        LightSeparator(true);
        e.Pointer.Capture(SeparatorGrip);
        SeparatorGrip.Focus();
        e.Handled = true;
    }

    private void OnSeparatorMoved(object? sender, PointerEventArgs e)
    {
        if (!_draggingSeparator) return;
        var width = Stage.Bounds.Width;
        if (width <= 0) return;
        Split = e.GetPosition(Stage).X / width;
        e.Handled = true;
    }

    private void OnSeparatorReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_draggingSeparator) return;
        _draggingSeparator = false;
        e.Pointer.Capture(null);
        LightSeparator(SeparatorGrip.IsFocused || SeparatorGrip.IsPointerOver);
        e.Handled = true;
    }

    private void OnSeparatorKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: Split -= SplitKeyStep; break;
            case Key.Right: Split += SplitKeyStep; break;
            case Key.Home: Split = 0; break;
            case Key.End: Split = 1; break;
            default: return;
        }
        e.Handled = true;
    }

    // ---- yakınlaştırma ve pano (K3) ----------------------------------------------

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        Zoom(e.Delta.Y, e.GetPosition(Surface));
        e.Handled = true;
    }

    /// <summary>
    /// Tek jest girişi. Tekerlek de, dışarıdan gelen çağrı da buradan geçer; tavanda
    /// yanlış döner, yani tekerlek durur ve panel daha büyümez.
    /// </summary>
    internal bool Zoom(double notches, Point anchor)
    {
        if (!_gesture.Wheel(notches, anchor.X, anchor.Y)) return false;

        SyncShelter();
        Surface.InvalidateVisual();
        RefreshReadout();
        return true;
    }

    private void OnStagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Stage).Properties.IsLeftButtonPressed) return;
        if (!_gesture.CanPan) return;
        _panning = true;
        _panFrom = e.GetPosition(Stage);
        e.Pointer.Capture(Stage);
        Stage.Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void OnStageMoved(object? sender, PointerEventArgs e)
    {
        // T40: şerit panelin alt bölgesinde belirir; bölge oranı Stage yüksekliğinden.
        var here = e.GetPosition(Stage);
        Strip.PointerAt(here.Y, Stage.Bounds.Height);

        if (!_panning) return;
        var now = e.GetPosition(Stage);
        if (_gesture.Drag(now.X - _panFrom.X, now.Y - _panFrom.Y)) Surface.InvalidateVisual();
        _panFrom = now;
    }

    private void OnStageReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        e.Pointer.Capture(null);
        Stage.Cursor = Cursor.Default;
    }

    private void RefreshReadout()
    {
        var percent = _gesture.ContentZoom * 100.0;
        ZoomText.Text = _turkish ? $"%{percent:0}" : $"{percent:0}%";
    }

    internal void RefreshEmptyState()
    {
        var empty = !Surface.HasFrame;
        EmptyState.IsVisible = empty;
        LeftBadge.IsVisible = !empty;
        // Perde varken sağ tarafta "işlenmiş" diye bir şey yok; yonga da sebebi söyleyen
        // perdenin üstünde durmaz.
        RightCurtain.IsVisible = !empty && _rightNotice is not null;
        RightBadge.IsVisible = !empty && _rightNotice is null;
        ZoomBadge.IsVisible = !empty;
        SeparatorGrip.IsVisible = !empty;
        Strip.IsVisible = !empty;
    }

    // ---- terfi ve yer tutucu (K4, K5) ---------------------------------------------

    private void SyncShelter()
    {
        if (_gesture.Promoted && !_promoted) Promote();
        else if (!_gesture.Promoted && _promoted) Land();
    }

    private void Promote()
    {
        var overlay = OverlayLayer.GetOverlayLayer(this);
        if (overlay is null) return;

        var origin = Shell.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var band = new Rect(origin, Shell.Bounds.Size);

        // Yer tutucu terfi anında bandını aynı boyutta tutar: altındaki düzen zıplamaz.
        Placeholder.Height = band.Height;
        Placeholder.IsVisible = true;

        Root.Children.Remove(Shell);
        Shell.Width = band.Width;
        Shell.Height = band.Height;
        Canvas.SetLeft(Shell, band.X);
        Canvas.SetTop(Shell, band.Y);
        overlay.Children.Add(Shell);

        _overlay = overlay;
        _promoted = true;
        overlay.SizeChanged += OnOverlayResized;

        if (TopLevel.GetTopLevel(this) is { } top)
            top.AddHandler(KeyDownEvent, OnTopLevelKey, RoutingStrategies.Tunnel);

        // K5: geçiş kabuğa uygulanıyor, görüntüye değil. Yüzey kendi turunda çizmeye
        // devam eder; geçişin kare hızıyla ilgisi yoktur.
        Shell.Transitions = _motionReduced ? null : ShellTransitions();
        if (_motionReduced) Fill();
        else Dispatcher.UIThread.Post(Fill, DispatcherPriority.Render);

        Shell.Focus();
    }

    private void Fill()
    {
        if (_overlay is null) return;
        Canvas.SetLeft(Shell, 0);
        Canvas.SetTop(Shell, 0);
        Shell.Width = _overlay.Bounds.Width;
        Shell.Height = _overlay.Bounds.Height;
    }

    private void OnOverlayResized(object? sender, SizeChangedEventArgs e)
    {
        if (_promoted) Fill();
    }

    private void Land()
    {
        if (!_promoted || _overlay is null) return;
        var overlay = _overlay;

        var origin = Placeholder.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var band = new Rect(origin, Placeholder.Bounds.Size);

        if (_motionReduced)
        {
            Settle();
            return;
        }

        Canvas.SetLeft(Shell, band.X);
        Canvas.SetTop(Shell, band.Y);
        Shell.Width = band.Width;
        Shell.Height = band.Height;

        _landing?.Stop();
        _landing = new DispatcherTimer { Interval = Motion("MotionBase", 240) };
        _landing.Tick += (_, _) =>
        {
            _landing?.Stop();
            _landing = null;
            Settle();
        };
        _landing.Start();
    }

    private void Settle()
    {
        if (_overlay is null) return;
        _overlay.SizeChanged -= OnOverlayResized;
        _overlay.Children.Remove(Shell);
        _overlay = null;
        _promoted = false;

        if (TopLevel.GetTopLevel(this) is { } top)
            top.RemoveHandler(KeyDownEvent, OnTopLevelKey);

        Shell.Transitions = null;
        Shell.ClearValue(WidthProperty);
        Shell.ClearValue(HeightProperty);
        Shell.ClearValue(Canvas.LeftProperty);
        Shell.ClearValue(Canvas.TopProperty);

        if (!Root.Children.Contains(Shell)) Root.Children.Add(Shell);

        Placeholder.IsVisible = false;
        Placeholder.ClearValue(HeightProperty);
        ApplySplit();
    }

    private Transitions ShellTransitions()
    {
        var duration = Motion("MotionBase", 240);
        var easing = new CubicEaseOut();
        return new Transitions
        {
            new DoubleTransition { Property = Canvas.LeftProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = Canvas.TopProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = WidthProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = HeightProperty, Duration = duration, Easing = easing }
        };
    }

    private void OnShellKey(object? sender, KeyEventArgs e)
    {
        // K4: boşluk tuşu oynat/duraklat, panel odaktayken.
        if (e.Key == Key.Space)
        {
            Strip.TogglePlay();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape || !_promoted) return;
        Descend();
        e.Handled = true;
    }

    private void OnTopLevelKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_promoted) return;
        Descend();
        e.Handled = true;
    }

    /// <summary>Esc: jest parametresi tabana iner, panel bandına döner.</summary>
    internal void Descend()
    {
        _gesture.Demote();
        Land();
        Surface.InvalidateVisual();
        RefreshReadout();
    }

    // ---- tema ve ortam ------------------------------------------------------------

    private double Scalar(string key, double fallback)
        => this.TryFindResource(key, out var value) && value is double number ? number : fallback;

    private TimeSpan Motion(string key, double fallbackMs)
        => this.TryFindResource(key, out var value) && value is TimeSpan span
            ? span
            : TimeSpan.FromMilliseconds(fallbackMs);

    private uint[] Palette()
    {
        var keys = new[] { "NeonBlueColor", "NeonPurpleColor", "NeonPinkColor", "NeonSuccessColor" };
        var set = new List<uint>();
        foreach (var key in keys)
            if (this.TryFindResource(key, out var value) && value is Color colour)
                set.Add(colour.ToUInt32());
        return set.Count > 0 ? set.ToArray() : new[] { 0xFF00F3FFu };
    }

    /// <summary>
    /// Sağ tarafın blok boyu. Uydurulmuş bir sayı değil: yarıçap ölçeğinin en büyük
    /// basamağı (RadiusPanel, 16). Sentetik "işlenmiş" taraf gözle ayırt edilsin diye var.
    /// </summary>
    private int BlockSize()
        => this.TryFindResource("RadiusPanelScalar", out var value) && value is double scalar
            ? (int)Math.Max(2, scalar)
            : 16;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopDemo();
        base.OnDetachedFromVisualTree(e);
    }
}
