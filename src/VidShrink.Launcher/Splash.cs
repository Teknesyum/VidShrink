using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;

namespace VidShrink.Launcher;

/// <summary>
/// Açılışta beklenen sürenin açıklanması. İstenen şey panelin kendisi değil, beklemenin
/// açıklanmasıdır; açıklanacak bekleme yoksa panel de yoktur.
///
/// Bu yüzden eşikli: iş <see cref="Threshold"/> dolmadan biterse pencere yaratılmaz,
/// görüntü çözülmez, hiçbir çizim yapılmaz — panelin kodu hiç çalışmaz.
///
/// Panel çıplak Win32: başlatıcının tek referansı VidShrink.Core ve öyle kalıyor. Bir
/// arayüz çatısının kendi açılış maliyeti, telafi edilmek istenen gecikmenin üstüne
/// binerdi.
/// </summary>
internal sealed class SplashGate : IDisposable
{
    /// <summary>Bu süre dolmadan hiçbir şey çizilmez. Altındaki açılışlar panelsizdir.</summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Panelin kendi zaman aşımı. İş beklenenden uzun sürerse panel yine de kapanır;
    /// donmuş bir splash, gecikmeden çok daha kötüdür.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);

    /// <summary>Panelin gerçekten açıldığını dışarıdan görmek için; ölçüm ve testler kullanır.</summary>
    public const string TraceVariable = "VIDSHRINK_SPLASH_TRACE";

    private readonly Func<string> _status;
    private readonly ManualResetEventSlim _closing = new(false);
    private readonly object _sync = new();
    private Timer? _timer;
    private Thread? _thread;

    private SplashGate(Func<string> status) => _status = status;

    /// <summary>
    /// Sayacı kurar ve hemen döner. Panel ancak eşik dolarsa ve o ana kadar
    /// <see cref="Dispose"/> çağrılmadıysa oluşturulur.
    /// </summary>
    public static SplashGate Arm(Func<string> status)
    {
        var gate = new SplashGate(status);
        gate._timer = new Timer(_ => gate.Show(), null, Threshold, Timeout.InfiniteTimeSpan);
        return gate;
    }

    private void Show()
    {
        lock (_sync)
        {
            if (_closing.IsSet || _thread is not null) return;
            _thread = new Thread(Run) { IsBackground = true, Name = "VidShrink splash" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    private void Run()
    {
        try
        {
            Trace("shown");
            using var window = SplashWindow.Create();
            var started = DateTime.UtcNow;
            do
            {
                window.Render(_status(), DateTime.UtcNow - started);
            }
            while (!_closing.Wait(FrameInterval) && DateTime.UtcNow - started < Lifetime);
        }
        catch (Exception exception)
        {
            // Panel bir süs; çizilemiyorsa açılış yine de sürer.
            Trace("failed: " + exception.GetType().Name);
        }
        finally
        {
            Trace("closed");
        }
    }

    /// <summary>Kare aralığı temanın hareket adımından geliyor.</summary>
    private static TimeSpan FrameInterval => TimeSpan.FromMilliseconds(SplashArt.Instance.FrameMilliseconds);

    /// <summary>
    /// Paneli kapatır. İş bittiğinde de, yarıda kaldığında da aynı yol işler: panel
    /// kapanır ve uygulama açılır.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
            _closing.Set();
        }
        // Beklemede olan kare, olay nesnesi bırakılırsa ona dokunabilir; kapatılmıyor.
        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private static void Trace(string line)
    {
        var path = Environment.GetEnvironmentVariable(TraceVariable);
        if (string.IsNullOrWhiteSpace(path)) return;
        try { File.AppendAllText(path, line + Environment.NewLine); }
        catch (Exception) { }
    }
}

/// <summary>
/// Derleme sırasında <c>Theme.axaml</c>'dan üretilip gömülen panel görüntüsü ve onun
/// içinde taşınan belirteç listesi. Renkler ve ölçüler burada sabit değil; görüntü
/// hangi belirteçlerle üretildiyse çalışma anında da onlar kullanılır.
/// </summary>
internal sealed class SplashArt
{
    public const string ResourceName = "VidShrink.Launcher.splash.png";
    public const string TokenChunkKeyword = "vidshrink-tokens";

    private static SplashArt? _instance;
    public static SplashArt Instance => _instance ??= Load();

    private readonly Dictionary<string, string> _tokens;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Düz alfalı BGRA pikseller.</summary>
    public byte[] Pixels { get; }

    private SplashArt(int width, int height, byte[] pixels, Dictionary<string, string> tokens)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        _tokens = tokens;
    }

    private static SplashArt Load()
    {
        using var stream = typeof(SplashArt).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new MissingManifestResourceException(ResourceName);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Decode(buffer.ToArray());
    }

    /// <summary>
    /// Kendi ürettiğimiz PNG'yi çözer. Bir görüntü kütüphanesi eklemek başlatıcıyı
    /// büyütürdü; okunan biçim de kendi yazdığımız biçimin kendisi.
    /// </summary>
    public static SplashArt Decode(byte[] png)
    {
        var width = 0;
        var height = 0;
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        using var data = new MemoryStream();

        var offset = 8; // PNG imzası
        while (offset + 8 <= png.Length)
        {
            var length = ReadInt(png, offset);
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var body = offset + 8;

            switch (type)
            {
                case "IHDR":
                    width = ReadInt(png, body);
                    height = ReadInt(png, body + 4);
                    break;
                case "tEXt":
                    var text = Encoding.ASCII.GetString(png, body, length);
                    var split = text.IndexOf('\0');
                    if (split > 0 && text[..split] == TokenChunkKeyword) ReadTokens(text[(split + 1)..], tokens);
                    break;
                case "IDAT":
                    data.Write(png, body, length);
                    break;
            }

            offset = body + length + 4;
            if (type == "IEND") break;
        }

        data.Position = 0;
        using var inflate = new ZLibStream(data, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        inflate.CopyTo(raw);
        return new SplashArt(width, height, Unfilter(raw.ToArray(), width, height), tokens);
    }

    private static void ReadTokens(string text, Dictionary<string, string> tokens)
    {
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            if (equals > 0) tokens[pair[..equals]] = pair[(equals + 1)..];
        }
    }

    /// <summary>PNG süzgeçlerini geri alır ve RGBA'yı GDI'nın beklediği BGRA'ya çevirir.</summary>
    private static byte[] Unfilter(byte[] raw, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var source = (y * (stride + 1)) + 1;
            var line = y * stride;
            for (var x = 0; x < stride; x++)
            {
                int left = x >= 4 ? pixels[line + x - 4] : 0;
                int up = y > 0 ? pixels[line - stride + x] : 0;
                int upLeft = y > 0 && x >= 4 ? pixels[line - stride + x - 4] : 0;
                var value = raw[source + x] + filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
                pixels[line + x] = (byte)value;
            }

            for (var x = 0; x < stride; x += 4)
            {
                (pixels[line + x], pixels[line + x + 2]) = (pixels[line + x + 2], pixels[line + x]);
            }
        }
        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int ReadInt(byte[] buffer, int offset) =>
        (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];

    public string Token(string key) => _tokens.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"Panel görüntüsü {key} belirtecini taşımıyor.");

    /// <summary>#AARRGGBB metnini GDI'nın COLORREF biçimine (0x00BBGGRR) çevirir.</summary>
    public uint ColorRef(string key)
    {
        var value = uint.Parse(Token(key).TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((value & 0xFF) << 16) | (value & 0xFF00) | ((value >> 16) & 0xFF);
    }

    public int Number(string key) =>
        (int)Math.Round(double.Parse(Token(key).Split(',')[0], CultureInfo.InvariantCulture));

    /// <summary>Görüntüye gömülen "x,y,genişlik,yükseklik" yerleşim kutusu.</summary>
    public (int X, int Y, int Width, int Height) Box(string key)
    {
        var parts = Token(key).Split(',');
        int Part(int index) => (int)Math.Round(double.Parse(parts[index], CultureInfo.InvariantCulture));
        return (Part(0), Part(1), Part(2), Part(3));
    }

    /// <summary>Tema yazı yığınından bu makinede kurulu ilk aileyi seçer.</summary>
    public string FontFamily
    {
        get
        {
            var families = Token("FontMono").Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var family in families)
            {
                if (SplashWindow.FontInstalled(family.Trim())) return family.Trim();
            }
            return families[^1].Trim();
        }
    }

    public int FrameMilliseconds => Number("MotionStaggerMs");
}

/// <summary>
/// Katmanlı, çerçevesiz, tıklama almayan Win32 penceresi. İçeriği gömülü görüntü ve
/// üzerine çizilen ilerleme parçası.
/// </summary>
internal sealed class SplashWindow : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopMost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int SwShowNoActivate = 4;
    private const uint UlwAlpha = 0x00000002;
    private const int AcSrcOver = 0x00;
    private const int AcSrcAlpha = 0x01;
    private const int BiRgb = 0;
    private const int DibRgbColors = 0;
    private const int TransparentBackground = 1;
    private const int PmRemove = 0x0001;

    private readonly SplashArt _art = SplashArt.Instance;
    private readonly IntPtr _window;
    private readonly IntPtr _screenDc;
    private readonly IntPtr _memoryDc;
    private readonly IntPtr _bitmap;
    private readonly IntPtr _previousBitmap;
    private readonly IntPtr _bits;
    private readonly IntPtr _titleFont;
    private readonly IntPtr _statusFont;
    private readonly WndProc _procedure = (window, message, w, l) => DefWindowProcW(window, message, w, l);

    private SplashWindow()
    {
        var className = "VidShrinkSplash";
        var wndClass = new WNDCLASSEXW
        {
            cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_procedure),
            hInstance = GetModuleHandleW(null),
            lpszClassName = className,
        };
        // Sınıf bu süreçte bir kez kaydedilir; ikinci çağrı zaten var diye başarısız olur.
        if (RegisterClassExW(ref wndClass) == 0)
        {
            const int classAlreadyExists = 1410;
            var error = Marshal.GetLastWin32Error();
            if (error != classAlreadyExists) throw new Win32Exception(error);
        }

        var x = (GetSystemMetrics(0) - _art.Width) / 2;
        var y = (GetSystemMetrics(1) - _art.Height) / 2;

        _window = CreateWindowExW(
            WsExLayered | WsExTransparent | WsExToolWindow | WsExTopMost | WsExNoActivate,
            className, null, WsPopup,
            x, y, _art.Width, _art.Height,
            IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
        if (_window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

        _screenDc = GetDC(IntPtr.Zero);
        _memoryDc = CreateCompatibleDC(_screenDc);

        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = _art.Width,
            biHeight = -_art.Height, // yukarıdan aşağı
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BiRgb,
        };
        _bitmap = CreateDIBSection(_memoryDc, ref header, DibRgbColors, out _bits, IntPtr.Zero, 0);
        _previousBitmap = SelectObject(_memoryDc, _bitmap);

        var family = _art.FontFamily;
        _titleFont = CreateFont(family, _art.Number("FontSizeMd"), bold: true);
        _statusFont = CreateFont(family, _art.Number("FontSizeSm"), bold: false);

        ShowWindow(_window, SwShowNoActivate);
    }

    public static SplashWindow Create() => new();

    /// <summary>Bir kare çizer ve pencereyi tazeler.</summary>
    public void Render(string status, TimeSpan elapsed)
    {
        Pump();

        // Duruk zemin her karede yeniden serilir; üstüne çizilenler böylece birikmez.
        Marshal.Copy(Premultiplied, 0, _bits, _art.Pixels.Length);

        var panel = _art.Box("panel");
        var title = _art.Box("title");
        var statusBox = _art.Box("status");

        SetBkMode(_memoryDc, TransparentBackground);
        DrawLine(title, "VIDSHRINK", _titleFont, _art.ColorRef("TextBodyColor"));
        DrawLine(statusBox, status, _statusFont, _art.ColorRef("TextDisabledColor"));
        DrawSweep(elapsed);

        // GDI alfa kanalını sıfırlıyor. Yazının ve parçanın düştüğü alan panelin tümüyle
        // donuk iç bölgesi olduğu için o dikdörtgende alfa geri 255'e çekiliyor; yuvarlak
        // köşeler ve dış parıltı bu alanın dışında kalıyor, onlara dokunulmuyor.
        var inset = _art.Number("RadiusPanelScalar");
        RestoreAlpha(panel.X + inset, panel.Y + inset, panel.Width - (inset * 2), panel.Height - (inset * 2));

        var size = new SIZE { cx = _art.Width, cy = _art.Height };
        var source = new POINT { x = 0, y = 0 };
        var blend = new BLENDFUNCTION
        {
            BlendOp = AcSrcOver,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AcSrcAlpha,
        };
        if (!UpdateLayeredWindow(_window, _screenDc, IntPtr.Zero, ref size, _memoryDc, ref source, 0, ref blend, UlwAlpha))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private byte[]? _premultiplied;

    /// <summary>UpdateLayeredWindow alfası çarpılmış piksel ister.</summary>
    private byte[] Premultiplied
    {
        get
        {
            if (_premultiplied is not null) return _premultiplied;
            var pixels = (byte[])_art.Pixels.Clone();
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                var alpha = pixels[offset + 3];
                if (alpha == 255) continue;
                for (var channel = 0; channel < 3; channel++)
                    pixels[offset + channel] = (byte)(pixels[offset + channel] * alpha / 255);
            }
            return _premultiplied = pixels;
        }
    }

    private void DrawLine((int X, int Y, int Width, int Height) box, string text, IntPtr font, uint color)
    {
        SelectObject(_memoryDc, font);
        SetTextColor(_memoryDc, color);
        var rect = new RECT { left = box.X, top = box.Y, right = box.X + box.Width, bottom = box.Y + box.Height };
        // DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_NOPREFIX
        DrawTextW(_memoryDc, text, text.Length, ref rect, 0x20 | 0x04 | 0x00 | 0x800);
    }

    /// <summary>
    /// İlerleme yolu görüntüde duruk; üzerinde gezen parça burada çiziliyor. Süre
    /// bilinmediği için belirsiz kip: parça yolu bir uçtan diğerine tarar.
    /// </summary>
    private void DrawSweep(TimeSpan elapsed)
    {
        var track = _art.Box("track");
        var height = _art.Number("ProgressBarHeight");
        var width = track.Width / 4;
        var period = _art.FrameMilliseconds * 36.0; // tam bir tarama
        var phase = (elapsed.TotalMilliseconds % period) / period;

        // Gidiş dönüş: parça uçta durup geri döner, kesik bir sıçrama olmaz.
        var travel = phase < 0.5 ? phase * 2 : (1 - phase) * 2;
        var left = track.X + (int)((track.Width - width) * travel);

        var brush = CreateSolidBrush(_art.ColorRef("NeonBlueColor"));
        var region = CreateRoundRectRgn(left, track.Y, left + width, track.Y + height, height, height);
        FillRgn(_memoryDc, region, brush);
        DeleteObject(region);
        DeleteObject(brush);
    }

    private void RestoreAlpha(int x, int y, int width, int height)
    {
        GdiFlush();

        // Alfa baytları dörtlü adımla duruyor; satır satır yazmak için tek bir satırı
        // okuyup yalnız alfa baytlarını değiştiriyoruz.
        var row = new byte[width * 4];
        for (var line = 0; line < height; line++)
        {
            var address = _bits + (((y + line) * _art.Width) + x) * 4;
            Marshal.Copy(address, row, 0, row.Length);
            for (var index = 3; index < row.Length; index += 4) row[index] = 255;
            Marshal.Copy(row, 0, address, row.Length);
        }
    }

    /// <summary>
    /// Pencerenin kendi iletileri işlenmezse kabuk onu yanıt vermiyor sayar. Panel
    /// tıklama almadığı için kuyrukta beklenmiyor, yalnız boşaltılıyor.
    /// </summary>
    private void Pump()
    {
        while (PeekMessageW(out var message, IntPtr.Zero, 0, 0, PmRemove))
        {
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private static IntPtr CreateFont(string family, int size, bool bold) =>
        CreateFontW(-size, 0, 0, 0, bold ? 700 : 400, 0, 0, 0, 1, 0, 0, 5, 0, family);

    public static bool FontInstalled(string family)
    {
        var dc = GetDC(IntPtr.Zero);
        try
        {
            var logFont = new LOGFONTW { lfCharSet = 1, lfFaceName = family };
            var found = false;
            EnumFontFamiliesExW(dc, ref logFont, (_, _, _, _) => { found = true; return 0; }, IntPtr.Zero, 0);
            return found;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    public void Dispose()
    {
        if (_titleFont != IntPtr.Zero) DeleteObject(_titleFont);
        if (_statusFont != IntPtr.Zero) DeleteObject(_statusFont);
        if (_memoryDc != IntPtr.Zero)
        {
            SelectObject(_memoryDc, _previousBitmap);
            DeleteDC(_memoryDc);
        }
        if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
        if (_screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _screenDc);
        if (_window != IntPtr.Zero) DestroyWindow(_window);
    }

    private delegate IntPtr WndProc(IntPtr window, uint message, IntPtr w, IntPtr l);

    private delegate int EnumFontProc(IntPtr logFont, IntPtr textMetric, uint type, IntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONTW
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        int exStyle, string className, string? windowName, int style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr w, IntPtr l);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PeekMessageW(out MSG message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessageW(ref MSG message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr window, IntPtr screenDc, IntPtr position, ref SIZE size,
        IntPtr sourceDc, ref POINT source, int colorKey, ref BLENDFUNCTION blend, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(IntPtr dc, string text, int length, ref RECT rect, uint format);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr dc, ref BITMAPINFOHEADER header, int usage, out IntPtr bits, IntPtr section, int offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr dc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr dc, uint color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern int FillRgn(IntPtr dc, IntPtr region, IntPtr brush);

    [DllImport("gdi32.dll")]
    private static extern bool GdiFlush();

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(
        int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strikeOut, uint charSet,
        uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumFontFamiliesExW(
        IntPtr dc, ref LOGFONTW logFont, EnumFontProc callback, IntPtr param, uint flags);
}
