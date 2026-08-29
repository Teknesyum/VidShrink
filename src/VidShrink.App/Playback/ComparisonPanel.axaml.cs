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
using Avalonia.VisualTree;

namespace VidShrink.App.Playback;

/// <summary>
/// T73: bekleme göstergesinin halkası. Boş başlar, süre dolarken saat yönünde kapanır ve
/// tam kapandığı an panel büyür.
///
/// Süreyi kendi belirtecinden okumaz — <see cref="Run"/>'a dışarıdan verilir. Tek kaynak
/// bekleme sayacıdır (<see cref="HoverZone.ShowCountdown"/>); halka o sürenin ne olduğunu
/// bilmez, yalnız uygular.
///
/// Kapanma bir geçiştir, kare döngüsü değil: hareket azaltma açıkken geçiş hiç kurulmaz ve
/// halka kapalı hâlde durur — gösterge görünür, canlanmaz (K4). Panelin terfi geçişinde
/// kullanılan kalıbın aynısı.
/// </summary>
internal sealed class CountdownRing : Control
{
    /// <summary>Kapanmış halka. Derece; daire tanımının kendisi, uydurulmuş sayı değil.</summary>
    internal const double FullTurn = 360;

    internal static readonly StyledProperty<double> SweepProperty =
        AvaloniaProperty.Register<CountdownRing, double>(nameof(Sweep));

    internal static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<CountdownRing, IBrush?>(nameof(Stroke));

    internal static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<CountdownRing, IBrush?>(nameof(Track));

    internal static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<CountdownRing, double>(nameof(Thickness));

    static CountdownRing()
        => AffectsRender<CountdownRing>(SweepProperty, StrokeProperty, TrackProperty, ThicknessProperty);

    /// <summary>Kapanan yayın açısı, derece. Sıfırda halka boş, <see cref="FullTurn"/>'de kapalı.</summary>
    internal double Sweep
    {
        get => GetValue(SweepProperty);
        set => SetValue(SweepProperty, value);
    }

    internal IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Halkanın altındaki soluk tam daire: ne kadarının kaldığını gösterir.</summary>
    internal IBrush? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    internal double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Son kurulan kapanma süresi. Sayaçtan geldiği ölçümde okunabilsin diye açık.</summary>
    internal TimeSpan Duration { get; private set; }

    /// <summary>Halka canlanıyor mu. Hareket azaltma açıkken yanlış (K4).</summary>
    internal bool Animating => Transitions is { Count: > 0 };

    /// <summary>
    /// Geri sayımı baştan kurar. <paramref name="span"/> bekleme sayacının kurduğu sürenin
    /// kendisidir; <paramref name="still"/> doğruysa halka canlanmadan kapalı çizilir.
    /// </summary>
    internal void Run(TimeSpan span, bool still)
    {
        Transitions = null;
        Duration = span;

        if (still || span <= TimeSpan.Zero)
        {
            Sweep = FullTurn;
            return;
        }

        Sweep = 0;
        Transitions = new Transitions
        {
            new DoubleTransition { Property = SweepProperty, Duration = span, Easing = new LinearEasing() }
        };
        Sweep = FullTurn;
    }

    /// <summary>Geri sayım kesildi: halka boşalır ve geçiş sökülür.</summary>
    internal void Stop()
    {
        Transitions = null;
        Duration = TimeSpan.Zero;
        Sweep = 0;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var radius = Math.Min(size.Width, size.Height) / 2 - Thickness / 2;
        if (radius <= 0 || Thickness <= 0) return;

        var centre = new Point(size.Width / 2, size.Height / 2);
        if (Track is { } track) context.DrawEllipse(null, new Pen(track, Thickness), centre, radius, radius);

        var sweep = Math.Clamp(Sweep, 0, FullTurn);
        if (Stroke is not { } stroke || sweep <= 0) return;

        var pen = new Pen(stroke, Thickness) { LineCap = PenLineCap.Round };
        if (sweep >= FullTurn)
        {
            context.DrawEllipse(null, pen, centre, radius, radius);
            return;
        }

        var figure = new PathFigure
        {
            StartPoint = OnRing(centre, radius, 0),
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments!.Add(new ArcSegment
        {
            Point = OnRing(centre, radius, sweep),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweep > FullTurn / 2
        });

        var path = new PathGeometry();
        path.Figures!.Add(figure);
        context.DrawGeometry(null, pen, path);
    }

    /// <summary>Halkanın üstündeki nokta. Sıfır derece tepe; saat yönünde artar.</summary>
    private static Point OnRing(Point centre, double radius, double degrees)
    {
        var radians = (degrees - FullTurn / 4) * Math.PI / (FullTurn / 2);
        return new Point(centre.X + radius * Math.Cos(radians), centre.Y + radius * Math.Sin(radians));
    }
}

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

    private readonly ZoomGesture _gesture;

    // Ayırıcı Margin ile değil, dönüşümle taşınıyor: Margin ölçüme girer ve uçlarda
    // ızgaranın istediği genişliği şişirip pencereye yatay kaydırma çubuğu getirir.
    private readonly TranslateTransform _separatorAt = new();

    private SyntheticFrameSource? _demo;
    private OverlayLayer? _overlay;
    private DispatcherTimer? _landing;

    // T44/K2 + T70: panelin büyüme/küçülme sayacı. Kuşak koruması HoverZone'da zaten var.
    // Bölge oranı burada anlamsız (panelin tamamı), sınır kararını hedef kademe veriyor.
    // Büyüme gecikmeli, küçülme anında: iki yön de temadan okunan ayrı belirteçler.
    private readonly HoverZone _descent;

    private ShelterStage _stage = ShelterStage.Band;
    private Rect _target;

    // T52: terfi anındaki band ölçüsü. Orta kademenin boyu bunun ölçekli katı olduğu için
    // saklanıyor: yer tutucunun sınırı terfi turunda henüz yerleşmemiş olabilir, _target ise
    // ilk kademe uygulandığı anda üstüne yazılır.
    private Size _band;

    private bool _turkish;
    private string? _notice;
    private string? _rightNotice;
    private string? _rightBadge;
    private bool _motionReduced;
    private bool _promoted;
    private bool _draggingSeparator;
    private bool _panning;
    private bool _overZoomButtons;
    private bool _hoverZoomMine;
    private Point _panFrom;
    private double _split = 0.5;

    public ComparisonPanel()
    {
        InitializeComponent();

        // T52: jestin aralığı da bandından çıkış eşiği de temadan gelir; sınıf hiçbir
        // sayı uydurmaz, buradaki yedekler belirteçlerle birebir aynı değerlerdir.
        _gesture = new ZoomGesture(Scalar("PlaybackMaxPanelScale", 4), Scalar("PlaybackHoverZoom", 2));

        // K5: azaltılmış hareket ayarını okuyan tek yer HoverZone. İkinci kopya yok.
        _motionReduced = HoverZone.MotionReduced;
        _descent = new HoverZone(
            share: 1.0,
            showDelay: () => Delay("PlaybackPanelRiseDelay"),
            hideDelay: () => Delay("PlaybackPanelFallDelay"),
            apply: open =>
            {
                if (open) HoverZoom(true);
                else if (_promoted) Descend();
                else HoverZoom(false);
            });

        // T73/K3: göstergenin süresi sayacın kendisinden geliyor. Panel burada
        // PlaybackPanelRiseDelay'i ikinci kez okumuyor — okusaydı belirteç değiştiğinde
        // iki süre ayrışır ve halka yanlış hızda kapanırdı.
        _descent.ShowCountdown = ShowCountdown;

        Surface.Gesture = _gesture;
        SeparatorGrip.RenderTransform = _separatorAt;

        Shell.PointerWheelChanged += OnWheel;
        Shell.KeyDown += OnShellKey;
        Shell.PointerEntered += (_, _) => HoverPanel(true);
        Shell.PointerExited += (_, _) =>
        {
            if (!Shell.IsPointerOver) HoverPanel(false);
        };

        BtnZoomIn.Click += (_, _) => Zoom(1, StageCentre());
        BtnZoomOut.Click += (_, _) => Zoom(-1, StageCentre());
        foreach (var button in new[] { BtnZoomIn, BtnZoomOut })
        {
            button.PointerEntered += (_, _) => SetZoomButtonHover(true);
            button.PointerExited += (_, _) => SetZoomButtonHover(false);
        }

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

        Shell.GotFocus += (_, _) => RefreshDescentHold();
        Shell.LostFocus += (_, _) => RefreshDescentHold();

        Stage.SizeChanged += (_, _) =>
        {
            ApplySplit();
            ClipStage();
        };
        Stage.PointerExited += (_, _) => Strip.PointerGone();

        SetLanguage(false);
        ApplySplit();
        ClipStage();
        RefreshReadout();
        RefreshEmptyState();

        if (Design.IsDesignMode) StartDemo();
    }

    /// <summary>Terfi hâli. Panel kök katmandaysa doğrudur.</summary>
    internal bool IsPromoted => _promoted;

    /// <summary>Panelin uygulanmış boy kademesi. Ad çakışmasın diye pano <c>Stage</c> kaldı.</summary>
    internal ShelterStage Shelter => _stage;

    /// <summary>
    /// Hedef kademenin kök katmandaki sınırı. Fare testi buna karşı yapılır, panelin
    /// animasyon sırasındaki anlık sınırına karşı değil (T44/K2, ikinci tuzak).
    /// </summary>
    internal Rect StageTarget => _target;

    /// <summary>Gecikmeli iniş sayacı. Kuşak koruması ölçümden okunabilsin diye açık.</summary>
    internal HoverZone Descent => _descent;

    /// <summary>
    /// Hareket azaltma ayarı. Değeri <see cref="HoverZone.MotionReduced"/> veriyor; ölçüm
    /// işletim sistemi ayarını değiştiremediği için açık kapı (K4).
    /// </summary>
    internal bool MotionReduced
    {
        get => _motionReduced;
        set => _motionReduced = value;
    }

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
    /// <c>PlaybackStageMinHeight</c>, önizleme yokken <c>PlaybackIdleMinHeight</c>
    /// (T46/K2 — boş panel artık bırakma alanı ölçüsünde değil, panel tabanında).
    /// </summary>
    internal void SetCompact(bool compact)
        => Shell.MinHeight = Scalar(compact ? "PlaybackIdleMinHeight" : "PlaybackStageMinHeight", 256);

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
    /// T49/K1: sağ yarının yaklaşıklık rozeti. Barındıran taraf metni çalışan dilde verir
    /// ve panel onu olduğu gibi gösterir — sayı burada üretilmez, çeviri ikinci kez
    /// uygulanmaz (metin "Yaklaşık önizleme · CRF 21" gibi birleşik olduğu için sözlükte
    /// karşılığı yoktur). Boş bırakılınca rozet kalkar.
    ///
    /// Dil değiştiğinde metnin de değişmesi barındıran tarafın işidir: panel neyi
    /// gösterdiğini bilir, neyin çevirisi olduğunu bilmez.
    /// </summary>
    internal void SetRightBadge(string? text)
    {
        _rightBadge = string.IsNullOrWhiteSpace(text) ? null : text;
        ApproxBadgeText.Text = _rightBadge ?? string.Empty;
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
        LeftBadgeText.Text = LanguageCatalog.Title(Say("Original"), _turkish);
        RightBadgeText.Text = LanguageCatalog.Title(Say("Processed"), _turkish);
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
            var right = Math.Max(0, width - Math.Clamp(_split, 0, 1) * width);
            RightCurtain.Width = right;

            // T49/K4: rozetin tavanı sağ yarının kendi genişliği eksi kenar boşluğu.
            // Uydurulmuş sayı yok; sığmayan metin üç noktayla kırpılır ve panelin
            // istediği genişliği büyütmez.
            ApproxBadge.MaxWidth = Math.Max(0, right - Inset("PlaybackBadgeMargin", 24));
        }
    }

    /// <summary>
    /// K3 (T44): kabuk yuvarlak köşeli bir Border, ama içindeki kare arka plan ve kare
    /// çizilen yüzey dört köşeyi örtüyordu — kenarlık çiziliyor, köşede görünmüyordu.
    /// Pano aynı yarıçapla kesilince köşeler açılıyor; kesme panonun bütün çocuklarını
    /// kapsadığı için sağ perde de aynı köşeyi taşıyor. Yarıçap uydurulmuyor,
    /// <c>RadiusPanelScalar</c> belirtecinden geliyor.
    /// </summary>
    private void ClipStage()
    {
        var size = Stage.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            Stage.Clip = null;
            return;
        }

        var radius = Scalar("RadiusPanelScalar", 16);
        Stage.Clip = new RectangleGeometry(new Rect(size)) { RadiusX = radius, RadiusY = radius };
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
        RefreshDescentHold();
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
        RefreshDescentHold();
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

        // Kullanıcı kendi değerini seçti: giriş yakınlaştırmasının sahipliği düşer,
        // fare çıkarken bu değer silinmez (T46/K5, birinci tuzak).
        _hoverZoomMine = false;
        AfterZoom();
        return true;
    }

    /// <summary>Panonun ortası. Düğmeyle yakınlaştırmanın çıpası — fare imleci yok.</summary>
    private Point StageCentre() => new(Surface.Bounds.Width / 2, Surface.Bounds.Height / 2);

    private void AfterZoom()
    {
        SyncShelter();
        Surface.InvalidateVisual();
        RefreshReadout();
    }

    /// <summary>
    /// K3: düğmelerin üstündeyken iniş sayacı durur. Düğmeler kabuğun içinde olduğu için
    /// kullanıcı yakınlaştırmaya çalışırken panel altından inmemeli.
    /// </summary>
    private void SetZoomButtonHover(bool over)
    {
        _overZoomButtons = over;
        RefreshDescentHold();
    }

    /// <summary>
    /// T52/K2: panel <c>PlaybackHoverZoom</c> katına çıkar; büyüyen şey panelin boyu,
    /// görüntünün içi değil. İki kat panel bandına sığmadığı için bu aynı zamanda orta
    /// kademenin eşiğidir: panel kök katmana terfi eder.
    ///
    /// T70: bu yolu artık fare girişi doğrudan çağırmıyor. Çağıran <see cref="_descent"/>;
    /// giriş yalnız sayacı kurar, büyüme <c>PlaybackPanelRiseDelay</c> dolduğunda uygulanır.
    ///
    /// Kullanıcının seçimi ezilmiyor: giriş ölçeklemesi yalnız panel taban durumundayken
    /// uygulanır. Fare çıkışında panel terfi etmişse karar buraya değil
    /// <see cref="Descend"/>'e aittir; terfi olmadıysa değer hâlâ bizimse geri alınır.
    /// </summary>
    /// <summary>
    /// T73/K1, K2: bekleme göstergesi. Sayaç kurulunca süresiyle birlikte çağrılır ve halka
    /// o sürede kapanır; sayaç bittiğinde ya da kesildiğinde <c>null</c> gelir ve gösterge
    /// kaybolur. Fare erken çıkarsa yol yine buradan geçer — kesilen geri sayım ekranda
    /// donmuş bir halka bırakmaz.
    /// </summary>
    private void ShowCountdown(TimeSpan? span)
    {
        if (span is not { } left || left <= TimeSpan.Zero)
        {
            RiseRing.Stop();
            RiseCountdown.IsVisible = false;
            return;
        }

        RiseCountdown.IsVisible = true;
        RiseRing.Run(left, _motionReduced);
    }

    internal void HoverZoom(bool entering)
    {
        if (_promoted) return;
        var centre = StageCentre();

        if (entering)
        {
            if (!_gesture.AtFloor) return;
            if (!_gesture.ScaleTo(Scalar("PlaybackHoverZoom", 2), centre.X, centre.Y)) return;
            _hoverZoomMine = true;
            AfterZoom();
            return;
        }

        if (!_hoverZoomMine) return;
        _hoverZoomMine = false;
        if (_gesture.ScaleTo(1, centre.X, centre.Y)) AfterZoom();
    }

    private void OnStagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Stage).Properties.IsLeftButtonPressed) return;
        if (!_gesture.CanPan) return;
        _panning = true;
        RefreshDescentHold();
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
        RefreshDescentHold();
        e.Pointer.Capture(null);
        Stage.Cursor = Cursor.Default;
    }

    private void RefreshReadout()
    {
        // T52: okunan yüzde panelin boy ölçeği. %100 taban boy, %200 iki katı.
        var percent = _gesture.PanelScale * 100.0;
        ZoomText.Text = _turkish ? $"%{percent:0}" : $"{percent:0}%";
        BtnZoomIn.IsEnabled = !_gesture.AtCeiling;
        BtnZoomOut.IsEnabled = !_gesture.AtFloor;
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
        // Rozet yalnız gerçekten yaklaşık bir parça gösterilirken var: tam çıktıda
        // metin gelmez, perde inmişken sağ tarafta işaret edilecek bir görüntü yoktur.
        ApproxBadge.IsVisible = !empty && _rightNotice is null && _rightBadge is not null;
        ZoomRow.IsVisible = !empty;
        SeparatorGrip.IsVisible = !empty;
        Strip.IsVisible = !empty;
    }

    // ---- terfi ve yer tutucu (K4, K5) ---------------------------------------------

    /// <summary>
    /// Jest hangi kademeyi söylüyorsa panel oraya gider. Üç kademe de tek parametreden
    /// türüyor; panel kendi başına kademe kararı vermiyor (T44/K1).
    /// </summary>
    private void SyncShelter()
    {
        var stage = _gesture.Shelter;
        if (stage == _stage)
        {
            // T52: kademe aynı kalsa da ölçek değişmiş olabilir; orta kademenin boyu
            // sabit bir oran değil, jestin söylediği ölçektir.
            if (_promoted) ApplyStage();
            return;
        }

        if (stage == ShelterStage.Band)
        {
            Land();
            return;
        }

        if (!_promoted)
        {
            Promote(stage);
            return;
        }

        // İniş başlamış ama daha oturmamışken tekerlek geri yukarı çevrilebilir. Bekleyen
        // iniş iptal edilmezse panel az sonra kendiliğinden bandına düşerdi.
        _landing?.Stop();
        _landing = null;

        _stage = stage;
        _descent.Reset(true, Shell.IsPointerOver);
        RefreshDescentHold();
        ApplyStage();
    }

    private void Promote(ShelterStage stage)
    {
        var overlay = OverlayLayer.GetOverlayLayer(this);
        if (overlay is null) return;

        var origin = Shell.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var band = new Rect(origin, Shell.Bounds.Size);

        // Yer tutucu terfi anında bandını aynı boyutta tutar: altındaki düzen zıplamaz.
        // Kademe değişimlerinde bu boy bir daha yazılmaz — yer tutucu hep bandındadır.
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
        _stage = stage;
        _target = band;
        _band = band.Size;
        overlay.SizeChanged += OnOverlayResized;

        _descent.Reset(true, Shell.IsPointerOver);
        RefreshDescentHold();

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.AddHandler(KeyDownEvent, OnTopLevelKey, RoutingStrategies.Tunnel);
            top.AddHandler(PointerMovedEvent, OnGlobalPointerMoved, RoutingStrategies.Tunnel);
            top.AddHandler(PointerExitedEvent, OnGlobalPointerExited, RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct);
        }

        // K5: geçiş kabuğa uygulanıyor, görüntüye değil. Yüzey kendi turunda çizmeye
        // devam eder; geçişin kare hızıyla ilgisi yoktur.
        Shell.Transitions = _motionReduced ? null : ShellTransitions();
        if (_motionReduced) ApplyStage();
        else Dispatcher.UIThread.Post(ApplyStage, DispatcherPriority.Render);

        Shell.Focus();
    }

    /// <summary>
    /// Kademenin kök katmandaki sınırı. T52: orta kademenin boyu artık sabit bir oran değil,
    /// jestin söylediği ölçek — bandın boyu çarpı <c>PanelScale</c>. Tavanı
    /// <c>PlaybackMidShare</c>: panel pencereyi kaplamaz, kenarı bırakır. Tam kademe kök
    /// katmanın tamamıdır.
    ///
    /// T66: tavan istisnasızdır. Eskiden band pencerenin payından genişse tavan bandın
    /// kendisi oluyordu; kısa pencerede band pencereden uzun olduğu için orta kademe
    /// pencereye eşitleniyor ve tam kademeden ayırt edilemez hale geliyordu — üç kademe
    /// ikiye düşüyordu. Pay her boyutta uygulanınca orta kademenin çevresinde ölçülebilir
    /// bir kenar hep kalır. Bandın pencereye sığmadığı durumda orta kademe bandından kısa
    /// olabilir; terfi etmiş panel kök katmandan taşamaz, band ise kaydırılan sayfada durur.
    /// </summary>
    private Rect StageBounds(ShelterStage stage)
    {
        if (_overlay is null) return _target;

        var area = OverlayArea();
        var width = area.Width;
        var height = area.Height;
        if (stage != ShelterStage.Mid) return new Rect(0, 0, width, height);

        var share = Scalar("PlaybackMidShare", 0.9);
        var scale = _gesture.PanelScale;
        var midWidth = Math.Min(_band.Width * scale, width * share);
        var midHeight = Math.Min(_band.Height * scale, height * share);
        return new Rect((width - midWidth) / 2, (height - midHeight) / 2, midWidth, midHeight);
    }

    /// <summary>
    /// Kök katmanın kapladığı alan. Katmanın kendisi bir <see cref="Canvas"/> ve ölçüsünü
    /// çocuklarından almaz; yerleşim turu ona boy vermediğinde ölçü katman yöneticisinden
    /// okunur. Yöneticinin sınırı pencerenin içerik alanıdır ve katman onun başlangıcında
    /// durur, bu yüzden iki koordinat çakışır.
    /// </summary>
    private Size OverlayArea()
    {
        if (_overlay is null) return default;

        var area = _overlay.Bounds.Size;
        if (area.Width > 0 && area.Height > 0) return area;
        return (_overlay.GetVisualParent() as Visual)?.Bounds.Size ?? area;
    }

    private void ApplyStage()
    {
        if (_overlay is null) return;

        _target = StageBounds(_stage);
        Canvas.SetLeft(Shell, _target.X);
        Canvas.SetTop(Shell, _target.Y);
        Shell.Width = _target.Width;
        Shell.Height = _target.Height;
    }

    private void OnOverlayResized(object? sender, SizeChangedEventArgs e)
    {
        if (_promoted) ApplyStage();
    }

    /// <summary>
    /// Fare hedef kademenin içinde mi. Panel farenin altına doğru büyüyüp küçülürken
    /// kabuğun anlık sınırı yanıltıcıdır; karar hedefe göre verilir (T44/K2).
    /// </summary>
    internal bool TargetCovers(Point overlayPoint) => _target.Contains(overlayPoint);

    /// <summary>Fare konumunu iniş sayacına bildirir. Nokta kök katmanın koordinatındadır.</summary>
    internal void TrackPointer(Point overlayPoint) => _descent.PointerWithin(TargetCovers(overlayPoint));

    /// <summary>
    /// T70: fare panelin kabuğuna girdi ya da çıktı. Panel bandındayken hedef kademe sınırı
    /// yok, karar kabuğun kendi giriş/çıkışına dayanıyor; büyüme kararını sayaç veriyor.
    /// </summary>
    internal void HoverPanel(bool inside) => _descent.PointerWithin(inside);

    private void OnGlobalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_overlay is null) return;
        TrackPointer(e.GetPosition(_overlay));
    }

    /// <summary>
    /// Fare pencerenin dışına çıktı. Hareket olayı bunu söyleyemez — pencere dışında
    /// <c>PointerMoved</c> hiç atmaz, dolayısıyla yalnız ona bakan sayaç "fare içeride"
    /// hâlinde takılı kalır ve tam kademede panel kendi kendine hiç inemez. Çıkışı
    /// pencerenin kendisinden dinlemek bu boşluğu kapatan en kısa yol; kabuğun kendi
    /// çıkışını dinlemek yetmezdi, çünkü tam kademede kabuk zaten bütün pencereyi kaplar.
    ///
    /// Kaynak denetimi çocuktan kabaran çıkışları eler: fare panelin içindeki bir öğeden
    /// diğerine geçerken pencereyi terk etmiş sayılmaz.
    /// </summary>
    private void OnGlobalPointerExited(object? sender, PointerEventArgs e)
    {
        if (_overlay is null) return;
        if (!ReferenceEquals(e.Source, sender)) return;
        PointerLeftWindow();
    }

    /// <summary>
    /// Fare pencereyi terk etti: hedef kademenin dışındadır, iniş sayacı kurulur.
    /// Karar yine hedef sınıra göre — pencerenin dışı her kademenin de dışıdır.
    /// </summary>
    internal void PointerLeftWindow() => _descent.PointerWithin(false);

    /// <summary>
    /// İnişi engelleyen sebepler: kullanıcı ayırıcıyı ya da panoyu sürüklüyor, klavye odağı
    /// panelin içindeki bir öğede. Kabuğun kendisi sayılmaz — odağı oraya terfi anında
    /// panelin kendisi taşıyor, o odak kullanıcının bir işi değil.
    /// </summary>
    private void RefreshDescentHold()
        => _descent.Hold(_draggingSeparator || _panning || _overZoomButtons || (Shell.IsKeyboardFocusWithin && !Shell.IsFocused));

    private void Land()
    {
        if (!_promoted || _overlay is null) return;
        var overlay = _overlay;

        var origin = Placeholder.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var band = new Rect(origin, Placeholder.Bounds.Size);

        _stage = ShelterStage.Band;
        _target = band;
        _descent.Reset(false);

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
        _stage = ShelterStage.Band;

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.RemoveHandler(KeyDownEvent, OnTopLevelKey);
            top.RemoveHandler(PointerMovedEvent, OnGlobalPointerMoved);
            top.RemoveHandler(PointerExitedEvent, OnGlobalPointerExited);
        }

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
        _hoverZoomMine = false;
        _gesture.Demote();
        Land();
        Surface.InvalidateVisual();
        RefreshReadout();
    }

    // ---- tema ve ortam ------------------------------------------------------------

    private double Scalar(string key, double fallback)
        => this.TryFindResource(key, out var value) && value is double number ? number : fallback;

    private double Inset(string key, double fallback)
        => this.TryFindResource(key, out var value) && value is Thickness edge ? edge.Left + edge.Right : fallback;

    /// <summary>
    /// T70: bekleme süresi. Ölçü değil süre olduğu için yedeği sıfırdır — belirteç yoksa
    /// kod bir sayı uydurmaz, gecikmesiz davranır.
    /// </summary>
    private TimeSpan Delay(string key)
        => this.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;

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
