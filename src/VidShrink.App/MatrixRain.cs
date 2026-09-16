using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using VidShrink.App.Playback;

namespace VidShrink.App;

/// <summary>
/// Güncelleme günlüğünün arkasındaki yağan karakter perdesi. Tek bir çizilen denetim:
/// alt öğe yok, yerleşim yok, kare başına yalnız sütun başlarının bir satır inmesi var.
///
/// <para>Ucuzluk üç yerden geliyor. Sütun sayısı boyut değiştiğinde bir kez hesaplanır
/// ve koşu boyunca sabittir. Karakterlerin biçimlenmiş metni rengine göre önbellekte
/// durur; kare başına yeni metin kurulmaz, yalnız renk değişince yeniden kurulur. Saat
/// yalnız <see cref="IsRunning"/> doğruyken, denetim ağaçta ve görünürken işler; panel
/// kapanınca durur.</para>
///
/// <para>Renk <see cref="Foreground"/> fırçasından okunur. Palet değişimi fırçanın aynı
/// örneğini yerinde boyadığı için fırçanın rengi izlenir; perde duruyorken de yeni renge
/// döner. Hareket azaltıldıysa (<see cref="HoverZone.MotionReduced"/>) saat hiç kurulmaz,
/// perde tek bir sabit kare çizer.</para>
///
/// <para>Ölçüler <c>Themes/Theme.axaml</c>'dan gelir: hücre <c>LineHeightBody</c>, karakter
/// <c>FontSizeSm</c> ve <c>FontMono</c>, kuyruk <c>MotionStaggerCount</c>, tik
/// <c>MotionStaggerMs</c>.</para>
/// </summary>
internal sealed class MatrixRain : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MatrixRain, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<MatrixRain, bool>(nameof(IsRunning));

    private readonly Random _random = Random.Shared;
    private readonly List<FormattedText> _glyphs = new();
    private DispatcherTimer? _timer;
    private int[] _heads = Array.Empty<int>();
    private int[] _cells = Array.Empty<int>();
    private int _columns;
    private int _rows;
    private double _cell;
    private double _fontSize;
    private int _trail;
    private TimeSpan _tick;
    private FontFamily _font = FontFamily.Default;
    private Color? _glyphColor;
    private SolidColorBrush? _watched;

    static MatrixRain()
    {
        AffectsRender<MatrixRain>(ForegroundProperty);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>Saatin şu an işleyip işlemediği; ölçüm bunu okuyup perdenin durduğunu doğrular.</summary>
    internal bool Ticking => _timer?.IsEnabled == true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _cell = Token("LineHeightBody");
        _fontSize = Token("FontSizeSm");
        _trail = Math.Max(1, (int)Token("MotionStaggerCount"));
        _tick = TimeSpan.FromMilliseconds(Token("MotionStaggerMs"));
        if (this.TryFindResource("FontMono", out var font) && font is FontFamily family) _font = family;
        _glyphColor = null;
        WatchBrush();
        Layout(Bounds.Size);
        UpdateClock();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopClock();
        Unwatch();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRunningProperty || change.Property == IsVisibleProperty) UpdateClock();
        else if (change.Property == ForegroundProperty) WatchBrush();
        else if (change.Property == BoundsProperty) Layout(Bounds.Size);
    }

    private double Token(string key)
    {
        if (!this.TryFindResource(key, out var value)) return 0;
        return value switch
        {
            double number => number,
            int whole => whole,
            _ => 0
        };
    }

    private void WatchBrush()
    {
        Unwatch();
        _watched = Foreground as SolidColorBrush;
        if (_watched is not null) _watched.PropertyChanged += OnBrushChanged;
        _glyphColor = null;
        InvalidateVisual();
    }

    private void Unwatch()
    {
        if (_watched is not null) _watched.PropertyChanged -= OnBrushChanged;
        _watched = null;
    }

    private void OnBrushChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != SolidColorBrush.ColorProperty) return;
        _glyphColor = null;
        InvalidateVisual();
    }

    private void Layout(Size size)
    {
        if (_cell <= 0) return;
        var columns = (int)(size.Width / _cell);
        var rows = (int)(size.Height / _cell);
        if (columns == _columns && rows == _rows) return;

        _columns = columns;
        _rows = rows;
        _heads = new int[columns];
        _cells = new int[Math.Max(0, columns * rows)];
        var span = rows + _trail;
        for (var c = 0; c < columns; c++) _heads[c] = span == 0 ? 0 : _random.Next(span) - _trail;
        for (var i = 0; i < _cells.Length; i++) _cells[i] = _random.Next(int.MaxValue);
        InvalidateVisual();
    }

    private void UpdateClock()
    {
        var run = IsRunning && IsVisible && VisualRoot is not null && !HoverZone.MotionReduced && _tick > TimeSpan.Zero;
        if (!run)
        {
            StopClock();
            return;
        }

        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = _tick };
            _timer.Tick += (_, _) => Step();
        }
        _timer.Start();
    }

    private void StopClock() => _timer?.Stop();

    /// <summary>Bir tik: her sütunun başı bir satır iner, dibi geçen sütun tepeden yeniden doğar.</summary>
    internal void Step()
    {
        if (_columns == 0 || _rows == 0) return;
        var span = _rows + _trail;
        for (var c = 0; c < _columns; c++)
        {
            _heads[c]++;
            if (_heads[c] - _trail >= _rows) _heads[c] = -_random.Next(span);
            var head = _heads[c];
            if (head >= 0 && head < _rows) _cells[head * _columns + c] = _random.Next(int.MaxValue);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_columns == 0 || _rows == 0) return;
        if (Foreground is not ISolidColorBrush brush) return;

        EnsureGlyphs(brush.Color);
        if (_glyphs.Count == 0) return;

        for (var c = 0; c < _columns; c++)
        {
            var head = _heads[c];
            for (var k = 0; k <= _trail; k++)
            {
                var row = head - k;
                if (row < 0 || row >= _rows) continue;

                var glyph = _glyphs[_cells[row * _columns + c] % _glyphs.Count];
                var origin = new Point(
                    c * _cell + (_cell - glyph.Width) / 2,
                    row * _cell + (_cell - glyph.Height) / 2);
                using (context.PushOpacity(1 - (double)k / (_trail + 1)))
                    context.DrawText(glyph, origin);
            }
        }
    }

    /// <summary>Karakter önbelleği: yalnız renk değişince kurulur.</summary>
    private void EnsureGlyphs(Color color)
    {
        if (_glyphColor == color && _glyphs.Count > 0) return;
        _glyphColor = color;
        _glyphs.Clear();

        var paint = new SolidColorBrush(color);
        var typeface = new Typeface(_font);
        var size = _fontSize > 0 ? _fontSize : _cell;
        for (var ch = '0'; ch <= '9'; ch++) _glyphs.Add(Glyph(ch, typeface, size, paint));
        for (var ch = 'A'; ch <= 'Z'; ch++) _glyphs.Add(Glyph(ch, typeface, size, paint));
    }

    private static FormattedText Glyph(char ch, Typeface typeface, double size, IBrush paint) =>
        new(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, paint);
}
