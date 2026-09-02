using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace VidShrink.App.Playback;

/// <summary>
/// K1: sunum yüzeyi. Tek <see cref="WriteableBitmap"/>, tek görsel. İki taraf ayrı yüzey
/// değil — çift genişlikli tek kare gelir, ayırıcı o karenin üstünde kırpma sınırıdır.
/// Airspace sorunu bu yüzden yok.
///
/// Yüzey kare kaynağını bilmez. Girdisi "elimde bir kare var"dır: <see cref="Rent"/> ile
/// havuzdan tampon alınır, doldurulur, <see cref="Submit"/> ile bırakılır.
///
/// Sayaç ve zamanlama <c>TopLevel.RequestAnimationFrame</c> üstünde (T37 ölçümü), tampon
/// havuzdan geliyor (soğuk tampon kopyayı 0,77 ms yerine 1,47 ms yapıyordu) ve halka üç
/// gözlü, çünkü tek gözlü tampon ölçümde karelerin yüzde dördünü düşürüyordu.
/// </summary>
internal sealed class ComparisonSurface : Control
{
    private const int RingCapacity = 3;

    /// <summary>Ayırıcının kendi başına açabileceği çizimler arasındaki en kısa süre.</summary>
    private static readonly TimeSpan SplitPaintInterval = TimeSpan.FromSeconds(1.0 / 60);

    /// <summary>Bu kadar süredir kare gelmiyorsa akış durmuş sayılır ve ayırıcı kendi çizimini açar.</summary>
    private static readonly TimeSpan Stalled = TimeSpan.FromMilliseconds(100);

    private readonly object _gate = new();
    private readonly Stack<byte[]> _pool = new();
    private readonly Queue<byte[]> _ring = new();

    private WriteableBitmap? _bitmap;
    private PixelSize _frame = PixelSize.Empty;
    private int _bufferBytes;
    private bool _hasFrame;
    private bool _running;
    private TopLevel? _top;

    private long _presented;
    private long _idleRounds;
    private long _repaints;

    private double _split = 0.5;
    private bool _splitMoved;
    private long _lastSplitPaintTicks;
    private long _lastPresentTicks;

    private readonly Func<long> _now;

    public ComparisonSurface() : this(Stopwatch.GetTimestamp)
    {
    }

    internal ComparisonSurface(Func<long> now)
    {
        _now = now;
        _lastPresentTicks = now();
        _lastSplitPaintTicks = now();
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    private TimeSpan Since(long ticks) => Stopwatch.GetElapsedTime(ticks, _now());

    internal ZoomGesture Gesture { get; set; } = new();

    /// <summary>
    /// Ayırıcının pano genişliğine oranı. Sıfır ve bir geçerli konumlardır.
    ///
    /// T53: bu değer farenin hızında yazılıyor, saniyede yüzlerce kez. Her yazışta yüzeyi
    /// geçersiz kılmak sunum döngüsünü ekranın değil farenin hızına bağlıyordu — gerçek
    /// pencerede tur sayısı 142/sn'den 700/sn'ye çıkıyor ve her tur tam boy yüzeyi baştan
    /// boyuyor (1902x988'de 3,16 ms). Yazış artık boyamıyor, yalnız işaretliyor; boyama
    /// bir sonraki sunum turunda bir kez yapılıyor. Sınırı aynı piksele düşüren yazış
    /// hiç işaretlenmiyor.
    /// </summary>
    internal double Split
    {
        get => _split;
        set
        {
            var next = Math.Clamp(value, 0, 1);
            if (next == _split) return;

            // Yarım pikselden az kayan sınır aynı pikselde çizilir. Yazış yutulmuyor,
            // biriktiriliyor: karşılaştırma son işaretlenen değerle yapılıyor.
            var width = Bounds.Width;
            if (width > 0 && Math.Abs(next - _split) * width < 0.5) return;

            _split = next;
            _splitMoved = true;
        }
    }

    /// <summary>Kaç kez yeniden çizim istendi — ayırıcı ölçümünün saydığı sayı.</summary>
    internal long Repaints => Interlocked.Read(ref _repaints);

    internal bool HasFrame
    {
        get { lock (_gate) return _hasFrame; }
    }

    /// <summary>Birleşik karenin tamamı. Sol yarı orijinal, sağ yarı işlenmiş.</summary>
    internal PixelSize FrameSize => _frame;

    /// <summary>Tek tarafın ölçüsü — ekranda görünen görüntünün gerçek çözünürlüğü.</summary>
    internal PixelSize SideSize => _frame == PixelSize.Empty
        ? PixelSize.Empty
        : new PixelSize(_frame.Width / 2, _frame.Height);

    internal long PresentedFrames => Interlocked.Read(ref _presented);

    /// <summary>Elinde yeni kare bulamadan geçen tur sayısı — kopyalama yapılmayan turlar.</summary>
    internal long IdleRounds => Interlocked.Read(ref _idleRounds);

    internal void Configure(PixelSize combined)
    {
        if (combined.Width <= 1 || combined.Height <= 0) throw new ArgumentOutOfRangeException(nameof(combined));
        if (combined.Width % 2 != 0) throw new ArgumentException("Combined frame width must be even.", nameof(combined));

        lock (_gate)
        {
            _frame = combined;
            _bufferBytes = combined.Width * 4 * combined.Height;
            _pool.Clear();
            _ring.Clear();
            _hasFrame = false;
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(combined, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        Gesture.SetSource(SideSize.Width, SideSize.Height);
        Repaint();
    }

    internal byte[] Rent()
    {
        lock (_gate)
        {
            if (_bufferBytes <= 0) throw new InvalidOperationException("Configure must run before a buffer is rented.");
            return _pool.Count > 0 ? _pool.Pop() : new byte[_bufferBytes];
        }
    }

    internal void Submit(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            if (buffer.Length != _bufferBytes) return;
            while (_ring.Count >= RingCapacity) _pool.Push(_ring.Dequeue());
            _ring.Enqueue(buffer);
        }
    }

    internal void Return(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            if (buffer.Length == _bufferBytes) _pool.Push(buffer);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is null || _running) return;
        _running = true;
        _top.RequestAnimationFrame(Round);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _running = false;
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(width, height);
    }

    /// <summary>
    /// Bir sunum turu. Elde yeni kare yoksa kopyalama yapılmaz ve yüzey geçersiz kılınmaz:
    /// son kare olduğu gibi durur, kararmaz, titremez.
    /// </summary>
    private void Round(TimeSpan _)
    {
        if (!_running) return;

        byte[]? buffer = null;
        lock (_gate)
        {
            // Sırayla alınıyor, en yenisi kazanmıyor. T38 bunu ölçtü: üretici gerçek
            // zamanda tempolu olduğu için kuyruktaki kare bayat değil, kendi anını
            // bekliyor; "en yenisi kazanır" karelerin dörtte birini çöpe atıyordu.
            // Gecikme yine sınırlı, çünkü halka dolduğunda Submit en eskisini düşürür.
            //
            // Ama tur başına tek kare almak, tur hızı besleme hızının altına düştüğünde
            // kalıcı geri kalma üretiyor. Kuyrukta birden çok kare biriktiyse geride
            // kalınmış demektir: sonuncusuna atlanıp aradakiler çizilmeden havuza döner.
            while (_ring.Count > 0)
            {
                if (buffer is not null) _pool.Push(buffer);
                buffer = _ring.Dequeue();
            }
        }

        if (buffer is null)
        {
            Interlocked.Increment(ref _idleRounds);
            // Akarken ayırıcı kendi çizimini hiç açmıyor: her sunulan kare zaten yüzeyi
            // yeniden çiziyor ve sınır o çizimde yerine oturuyor. Sürükleme böylece
            // ekrana tek bir fazladan çizim eklemiyor. Ölçüm bunu istedi — tur başına
            // bir çizim yetmiyordu, çünkü sunum turu kendini yeniden sıraya koyuyor ve
            // boşta bile 217 tur/sn dönüyor. Kare akmıyorsa (duraklatılmış, boru tıkalı)
            // sınırın takılı kalmaması için çizim açılır, en fazla 60 Hz.
            if (_splitMoved &&
                Since(_lastPresentTicks) >= Stalled &&
                Since(_lastSplitPaintTicks) >= SplitPaintInterval)
            {
                _splitMoved = false;
                Repaint();
            }
        }
        else
        {
            Blit(buffer);
            lock (_gate)
            {
                _pool.Push(buffer);
                _hasFrame = true;
            }
            Interlocked.Increment(ref _presented);
            _lastPresentTicks = _now();
            _splitMoved = false;
            Repaint();
        }

        _top?.RequestAnimationFrame(Round);
    }

    private void Repaint()
    {
        _lastSplitPaintTicks = _now();
        Interlocked.Increment(ref _repaints);
        InvalidateVisual();
    }

    private void Blit(byte[] source)
    {
        WriteableBitmap? bitmap;
        lock (_gate) bitmap = _bitmap;
        if (bitmap is null) return;

        using var locked = bitmap.Lock();
        var bytes = Math.Min(source.Length, locked.RowBytes * locked.Size.Height);
        Marshal.Copy(source, 0, locked.Address, bytes);
    }

    /// <summary>
    /// Tek kareyi iki kez çizer: solda kaynağın sol yarısı, sağda sağ yarısı. Aynı hedef
    /// dikdörtgen, farklı kırpma — ayırıcı bu iki kırpmanın sınırıdır.
    ///
    /// Bu çizim kod çözme tetiklemez. Ayırıcı sürükleme ve yakınlaştırma yalnız buradaki
    /// dikdörtgenleri oynatır, yeni kare istemez.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        WriteableBitmap? bitmap;
        bool ready;
        lock (_gate)
        {
            bitmap = _bitmap;
            ready = _hasFrame;
        }
        if (bitmap is null || !ready) return;

        var viewport = Bounds.Size;
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        var side = SideSize;
        if (side.Width <= 0 || side.Height <= 0) return;

        Gesture.SetViewport(viewport.Width, viewport.Height);
        Gesture.SetSource(side.Width, side.Height);

        var target = new Rect(Gesture.OffsetX, Gesture.OffsetY, Gesture.ContentWidth, Gesture.ContentHeight);
        var boundary = Math.Clamp(Split, 0, 1) * viewport.Width;

        var left = new Rect(0, 0, side.Width, side.Height);
        var right = new Rect(side.Width, 0, side.Width, side.Height);

        if (boundary > 0)
        {
            using (context.PushClip(new Rect(0, 0, boundary, viewport.Height)))
                context.DrawImage(bitmap, left, target);
        }

        if (boundary < viewport.Width)
        {
            using (context.PushClip(new Rect(boundary, 0, viewport.Width - boundary, viewport.Height)))
                context.DrawImage(bitmap, right, target);
        }
    }
}

/// <summary>
/// K7: sentetik kare üreteci. Panel kare kaynağı bağlanmadan denenebilsin diye var.
/// Sol yarı temiz bir desen, sağ yarı aynı desenin bloklara indirgenmiş hâli — yani
/// ayırıcının iki yanı gerçekten farklı görünür ve kırpma sınırı gözle doğrulanabilir.
///
/// Renkler dışarıdan geliyor: panel bunları Theme.axaml belirteçlerinden çözüp veriyor,
/// üreteç kendi rengini uydurmuyor.
/// </summary>
internal sealed class SyntheticFrameSource : IDisposable
{
    private readonly ComparisonSurface _surface;
    private readonly PixelSize _combined;
    private readonly uint[] _palette;
    private readonly int _blockSize;
    private readonly CancellationTokenSource _stop = new();
    private Thread? _worker;

    internal SyntheticFrameSource(ComparisonSurface surface, PixelSize combined, uint[] palette, int blockSize)
    {
        _surface = surface;
        _combined = combined;
        _palette = palette.Length > 0 ? palette : new uint[] { 0xFF00F3FFu };
        _blockSize = Math.Max(2, blockSize);
    }

    internal int TargetFps { get; init; } = 60;

    internal void Start()
    {
        if (_worker is not null) return;
        _worker = new Thread(Loop) { IsBackground = true, Name = "vidshrink-synthetic-frames" };
        _worker.Start();
    }

    /// <summary>
    /// Kareler bir kez üretilip dönüşümlü kopyalanıyor — T37 ölçüm aracının yaptığı işin
    /// aynısı. Her turda deseni yeniden hesaplamak yönetilen kodda 60 fps'i tutamaz ve
    /// ölçülen şey sunum yolu değil, üretecin kendisi olurdu.
    /// </summary>
    private void Loop()
    {
        var frames = Bake(8);
        var period = TimeSpan.FromSeconds(1.0 / Math.Max(1, TargetFps));
        var index = 0;
        var next = DateTime.UtcNow;
        while (!_stop.IsCancellationRequested)
        {
            byte[] buffer;
            try { buffer = _surface.Rent(); }
            catch (InvalidOperationException) { return; }

            var frame = frames[index++ % frames.Length];
            Buffer.BlockCopy(frame, 0, buffer, 0, Math.Min(frame.Length, buffer.Length));
            _surface.Submit(buffer);

            next += period;
            var wait = next - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) _stop.Token.WaitHandle.WaitOne(wait);
            else next = DateTime.UtcNow;
        }
    }

    private byte[][] Bake(int count)
    {
        var set = new byte[count][];
        var size = _combined.Width * 4 * _combined.Height;
        for (var i = 0; i < count; i++)
        {
            var frame = new byte[size];
            // Desen kendine kapansın diye faz, sinüsün tam bir turuna bölünüyor.
            Paint(frame, i * (2.0 * Math.PI / Wave) / count);
            set[i] = frame;
        }
        return set;
    }

    private const double Wave = 0.01;

    private void Paint(byte[] buffer, double sweep)
    {
        var width = _combined.Width;
        var height = _combined.Height;
        var half = width / 2;
        var stride = width * 4;

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < half; x++)
            {
                var clean = Sample(x, y, sweep, half, height);

                var bx = x / _blockSize * _blockSize + _blockSize / 2;
                var by = y / _blockSize * _blockSize + _blockSize / 2;
                var coarse = Quantise(Sample(Math.Min(bx, half - 1), Math.Min(by, height - 1), sweep, half, height));

                Write(buffer, row + x * 4, clean);
                Write(buffer, row + (half + x) * 4, coarse);
            }
        }
    }

    private uint Sample(int x, int y, double sweep, int width, int height)
    {
        // Palet boyunca düzgün bir geçiş: bant sınırlarında sert kenar bırakmıyor,
        // çünkü sert kenar ayırıcının kendi sınırıyla karışırdı.
        var ramp = x / (double)Math.Max(1, width) * (_palette.Length - 1);
        var index = Math.Clamp((int)ramp, 0, _palette.Length - 1);
        var near = _palette[index];
        var far = _palette[Math.Min(index + 1, _palette.Length - 1)];

        var down = y / (double)Math.Max(1, height);
        var pulse = 0.5 + 0.5 * Math.Sin(x * Wave + sweep + down * 3.0);

        return Mix(Mix(near, far, ramp - index), 0xFF000000u, 1.0 - pulse * 0.85);
    }

    private static uint Quantise(uint colour)
    {
        var b = (byte)(((colour >> 0) & 0xFF) & 0xE0);
        var g = (byte)(((colour >> 8) & 0xFF) & 0xE0);
        var r = (byte)(((colour >> 16) & 0xFF) & 0xE0);
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static uint Mix(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var blue = (byte)(((a >> 0) & 0xFF) * (1 - t) + ((b >> 0) & 0xFF) * t);
        var green = (byte)(((a >> 8) & 0xFF) * (1 - t) + ((b >> 8) & 0xFF) * t);
        var red = (byte)(((a >> 16) & 0xFF) * (1 - t) + ((b >> 16) & 0xFF) * t);
        return 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
    }

    private static void Write(byte[] buffer, int index, uint bgra)
    {
        buffer[index + 0] = (byte)(bgra >> 0);
        buffer[index + 1] = (byte)(bgra >> 8);
        buffer[index + 2] = (byte)(bgra >> 16);
        buffer[index + 3] = 0xFF;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _worker?.Join(TimeSpan.FromMilliseconds(500));
        _stop.Dispose();
    }
}
