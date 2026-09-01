using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

/// <summary>
/// Denetim şeridi: oynat/duraklat, başa dön, zaman çizgisi, kodlama ilerleme imleci.
///
/// Şerit kaynağı bilmez. Konum ve süre dışarıdan yazılır, atlama isteği olay olarak
/// dışarı verilir. Kodlama ilerlemesi de aynı biçimde dışarıdan gelir — bu turda
/// sahte bir değerle sürülüyor, gerçek <c>out_time_ms</c> bağlantısı T42'nin işi.
/// </summary>
internal partial class ControlStrip : UserControl
{
    /// <summary>Ok tuşunun adımı: sürenin yüzde biri. Ayırıcıdaki adım ölçeğiyle aynı.</summary>
    private const double SeekKeyStep = 0.01;

    /// <summary>PageUp/PageDown adımı: sürenin onda biri.</summary>
    private const double SeekPageStep = 0.10;

    private readonly TranslateTransform _thumbAt = new();
    private readonly TranslateTransform _cursorAt = new();
    private readonly HoverZone _zone;

    private DispatcherTimer? _demoClock;

    private bool _turkish;
    private bool _playing;
    private bool _scrubbing;
    private bool _pointerOnBar;
    private bool _focusInside;

    private TimeSpan? _duration;
    private TimeSpan _position;
    private double _encodeFraction = -1;
    private int _encodePass;
    private int _encodePassCount;
    private int _encodeAttempt;

    public ControlStrip()
    {
        InitializeComponent();

        _zone = new HoverZone(
            share: Scalar("PlaybackHoverZoneShare", 0.25),
            showDelay: () => Delay("PlaybackStripShowDelay"),
            hideDelay: () => Motion("PlaybackStripHideDelay", 360),
            apply: Reveal);

        Thumb.RenderTransform = _thumbAt;
        EncodeCursor.RenderTransform = _cursorAt;

        Bar.PointerEntered += (_, _) => { _pointerOnBar = true; UpdateHold(); };
        Bar.PointerExited += (_, _) => { _pointerOnBar = false; UpdateHold(); };

        PlayPause.Click += (_, _) => TogglePlay();
        Restart.Click += (_, _) => Restarted();

        Timeline.PointerPressed += OnTimelinePressed;
        Timeline.PointerMoved += OnTimelineMoved;
        Timeline.PointerReleased += OnTimelineReleased;
        Timeline.KeyDown += OnTimelineKey;
        Timeline.GotFocus += (_, _) => LightTimeline(true);
        Timeline.LostFocus += (_, _) => LightTimeline(_scrubbing);
        Timeline.PointerEntered += (_, _) => Thumb.Classes.Set("lit", true);
        Timeline.PointerExited += (_, _) => Thumb.Classes.Set("lit", _scrubbing || Timeline.IsFocused);
        Timeline.SizeChanged += (_, _) => Refresh();

        AddHandler(GotFocusEvent, (_, _) => { _focusInside = true; UpdateHold(); });
        AddHandler(LostFocusEvent, (_, _) =>
        {
            // Odak şeridin içindeki başka bir denetime geçiyor olabilir; kararı bir tur
            // sonraya bırak ki ara durumda şerit kaçmasın.
            Dispatcher.UIThread.Post(() =>
            {
                _focusInside = Bar.IsKeyboardFocusWithin;
                UpdateHold();
            }, DispatcherPriority.Input);
        });

        // K5: azaltılmış hareket açıksa geçiş yok, belirme anlık.
        Bar.Transitions = HoverZone.MotionReduced
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = Motion("MotionFast", 160),
                    Easing = new CubicEaseOut()
                }
            };

        SetLanguage(false);
        Refresh();
    }

    /// <summary>Kullanıcı oynat/duraklat istedi.</summary>
    internal event EventHandler? PlayPauseRequested;

    /// <summary>Kullanıcı başa dönmek istedi.</summary>
    internal event EventHandler? RestartRequested;

    /// <summary>
    /// Atlama isteği. Sürükleme sırasında gönderilmez — yalnız bırakışta ve klavye
    /// adımında. Her piksel için kaynağa istek gitmez.
    /// </summary>
    internal event EventHandler<TimeSpan>? SeekRequested;

    internal bool IsRevealed => _zone.IsVisible;

    internal bool IsPlaying
    {
        get => _playing;
        set
        {
            if (_playing == value) return;
            _playing = value;
            // K1: duraklamışken şerit görünür kalır.
            UpdateHold();
            Refresh();
        }
    }

    /// <summary>Kaynağın süresi. Boşsa çubuk belirsiz durumdadır, sıfır göstermez.</summary>
    internal TimeSpan? Duration
    {
        get => _duration;
        set
        {
            _duration = value is { } span && span > TimeSpan.Zero ? span : null;
            Refresh();
        }
    }

    internal TimeSpan Position
    {
        get => _position;
        set
        {
            var next = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (_duration is { } span && next > span) next = span;
            _position = next;
            if (!_scrubbing) Refresh();
        }
    }

    /// <summary>Şeridin dışarı verdiği bölge nesnesi — panel fare olaylarını buraya akıtır.</summary>
    internal HoverZone Zone => _zone;

    /// <summary>Panelin içindeki fare konumu. Alt bölgedeyse şerit belirir.</summary>
    internal void PointerAt(double y, double panelHeight) => _zone.PointerAt(y, panelHeight);

    internal void PointerGone() => _zone.PointerGone();

    /// <summary>
    /// K3: kodlama sürerken imleç ve etiket. Kesir sıfır ile bir arası; aralık dışıysa
    /// kodlama yok sayılır ve ikisi de gizlenir.
    /// </summary>
    internal void SetEncodeProgress(double fraction, int pass, int passCount, int attempt)
    {
        if (double.IsNaN(fraction) || fraction < 0 || fraction > 1)
        {
            ClearEncode();
            return;
        }

        _encodeFraction = fraction;
        _encodePass = pass;
        _encodePassCount = passCount;
        _encodeAttempt = attempt;
        EncodeText.Text = LanguageCatalog.EncodeMarker(_turkish, pass, passCount, attempt);
        Refresh();
    }

    internal void ClearEncode()
    {
        _encodeFraction = -1;
        Refresh();
    }

    /// <summary>Panelin boşluk tuşu buraya gelir.</summary>
    internal void TogglePlay()
    {
        IsPlaying = !_playing;
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void SetLanguage(bool turkish)
    {
        _turkish = turkish;

        // Düğme yüzleri metin değil işaret; ad erişilebilirlik adında duruyor.
        Restart.Content = "|◀";
        AutomationProperties.SetName(Restart, Text("playback.control.restart"));
        AutomationProperties.SetName(Timeline, Text("playback.control.timeline"));
        AutomationProperties.SetName(Bar, Text("playback.control.strip"));

        if (_encodeFraction >= 0)
            EncodeText.Text = LanguageCatalog.EncodeMarker(turkish, _encodePass, _encodePassCount, _encodeAttempt);

        Refresh();
    }

    // ---- görünürlük (K1) ------------------------------------------------------------

    private void UpdateHold() => _zone.Hold(_pointerOnBar || _focusInside || !_playing);

    /// <summary>Odak ve fare göstergesi: yol çerçevesi kalınlaşır, tutamaç parlar.</summary>
    private void LightTimeline(bool lit)
    {
        Track.Classes.Set("focused", lit);
        Thumb.Classes.Set("lit", lit);
    }

    private void Reveal(bool shown)
    {
        Bar.Opacity = shown ? 1 : 0;
        Bar.IsHitTestVisible = shown;
    }

    // ---- zaman çizgisi (K2) ---------------------------------------------------------

    private double TrackWidth => Math.Max(0, Timeline.Bounds.Width);

    private double Fraction
        => _duration is { } span && span > TimeSpan.Zero
            ? Math.Clamp(_position.TotalSeconds / span.TotalSeconds, 0, 1)
            : 0;

    private void OnTimelinePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_duration is null) return;
        if (!e.GetCurrentPoint(Timeline).Properties.IsLeftButtonPressed) return;

        _scrubbing = true;
        LightTimeline(true);
        e.Pointer.Capture(Timeline);
        Timeline.Focus();
        ScrubTo(e.GetPosition(Timeline).X);
        e.Handled = true;
    }

    private void OnTimelineMoved(object? sender, PointerEventArgs e)
    {
        if (!_scrubbing) return;
        // Konum anında gösterilir; atlama isteği yok.
        ScrubTo(e.GetPosition(Timeline).X);
        e.Handled = true;
    }

    private void OnTimelineReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        LightTimeline(Timeline.IsFocused);
        e.Pointer.Capture(null);
        SeekRequested?.Invoke(this, _position);
        e.Handled = true;
    }

    private void ScrubTo(double x)
    {
        if (_duration is not { } span || TrackWidth <= 0) return;
        var fraction = Math.Clamp(x / TrackWidth, 0, 1);
        _position = TimeSpan.FromSeconds(span.TotalSeconds * fraction);
        Refresh();
    }

    private void OnTimelineKey(object? sender, KeyEventArgs e)
    {
        if (_duration is not { } span) return;

        var step = e.Key switch
        {
            Key.Left => -SeekKeyStep,
            Key.Right => SeekKeyStep,
            Key.PageDown => -SeekPageStep,
            Key.PageUp => SeekPageStep,
            _ => 0.0
        };

        if (step != 0) Position = _position + TimeSpan.FromSeconds(span.TotalSeconds * step);
        else if (e.Key == Key.Home) Position = TimeSpan.Zero;
        else if (e.Key == Key.End) Position = span;
        else return;

        // Tuş basışı kesikli bir onaydır; sürükleme gibi biriktirmeye gerek yok.
        SeekRequested?.Invoke(this, _position);
        e.Handled = true;
    }

    private void Restarted()
    {
        Position = TimeSpan.Zero;
        RestartRequested?.Invoke(this, EventArgs.Empty);
        SeekRequested?.Invoke(this, TimeSpan.Zero);
    }

    // ---- çizim ----------------------------------------------------------------------

    private void Refresh()
    {
        PlayPause.Content = _playing ? "❚❚" : "▶";
        AutomationProperties.SetName(PlayPause, _playing
            ? Text("playback.control.pause")
            : Text("playback.control.play"));

        var known = _duration is not null;
        Track.Classes.Set("unknown", !known);
        Fill.IsVisible = known;
        Thumb.IsVisible = known;
        Timeline.IsEnabled = known;
        Timeline.Cursor = known ? new Cursor(StandardCursorType.Hand) : Cursor.Default;

        var width = TrackWidth;
        if (known)
        {
            var fraction = Fraction;
            Fill.Width = width * fraction;
            _thumbAt.X = width * fraction - Thumb.Width / 2;
            TimeText.Text = $"{Clock(_position, _duration!.Value)} / {Clock(_duration.Value, _duration.Value)}";
        }
        else
        {
            Fill.Width = 0;
            TimeText.Text = "--:-- / --:--";
        }

        var encoding = _encodeFraction >= 0;
        EncodeCursor.IsVisible = encoding;
        EncodeChip.IsVisible = encoding;
        if (encoding) _cursorAt.X = width * _encodeFraction - EncodeCursor.Width / 2;
    }

    /// <summary>
    /// K2: <c>00:12</c> biçimi. Kaynak bir saati geçiyorsa iki taraf da <c>01:02:12</c>
    /// biçimine geçer — okuma tek biçimde kalsın.
    /// </summary>
    internal static string Clock(TimeSpan value, TimeSpan scale)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return scale.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }

    // ---- deneme saati ---------------------------------------------------------------

    private const int DemoPass = 1;
    private const int DemoPassCount = 2;
    private const int DemoAttempt = 2;
    private const double DemoEncodeFraction = 0.22;

    /// <summary>
    /// Kare kaynağı da kodlama da bağlı değilken şeridi denenebilir tutan sahte saat.
    /// Süre planın §5 çizimindeki 01:30; kodlama kesri sabit bir yer tutucu.
    /// </summary>
    internal void StartDemoClock()
    {
        if (_demoClock is not null) return;

        Duration = TimeSpan.FromSeconds(90);
        IsPlaying = true;
        SetEncodeProgress(DemoEncodeFraction, DemoPass, DemoPassCount, DemoAttempt);

        _demoClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _demoClock.Tick += (_, _) =>
        {
            if (!_playing || _scrubbing || _duration is not { } span) return;
            var next = _position + TimeSpan.FromSeconds(1);
            Position = next > span ? TimeSpan.Zero : next;
        };
        _demoClock.Start();
    }

    internal void StopDemoClock()
    {
        _demoClock?.Stop();
        _demoClock = null;
    }

    // ---- tema -----------------------------------------------------------------------

    private string Text(string key)
        => LanguageCatalog.Title(
            Strings.GetIn(_turkish ? "tr" : Strings.FallbackLanguage, key),
            _turkish);

    private double Scalar(string key, double fallback)
        => this.TryFindResource(key, out var value) && value is double number ? number : fallback;

    private TimeSpan Motion(string key, double fallbackMs)
        => this.TryFindResource(key, out var value) && value is TimeSpan span
            ? span
            : TimeSpan.FromMilliseconds(fallbackMs);

    /// <summary>Bekleme süresi. Belirteç yoksa gecikme yoktur; kod sayı uydurmaz.</summary>
    private TimeSpan Delay(string key)
        => this.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
}
