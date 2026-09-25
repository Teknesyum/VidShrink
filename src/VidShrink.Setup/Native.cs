using System.Runtime.InteropServices;

namespace VidShrink.Setup;

internal static class Native
{
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_NCHITTEST = 0x0084;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_PRINTCLIENT = 0x0318;

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;

    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SPACE = 0x20;
    public const int VK_LEFT = 0x25;
    public const int VK_RIGHT = 0x27;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int IDC_ARROW = 32512;
    public const int IDC_HAND = 32649;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SPI_GETWORKAREA = 0x0030;

    public const uint DT_LEFT = 0x0000;
    public const uint DT_CENTER = 0x0001;
    public const uint DT_RIGHT = 0x0002;
    public const uint DT_VCENTER = 0x0004;
    public const uint DT_SINGLELINE = 0x0020;
    public const uint DT_NOPREFIX = 0x0800;
    public const uint DT_END_ELLIPSIS = 0x8000;
    public const uint DT_PATH_ELLIPSIS = 0x4000;
    public const uint DT_CALCRECT = 0x0400;

    public const int TRANSPARENT = 1;
    public const uint SRCCOPY = 0x00CC0020;
    public const int FW_NORMAL = 400;
    public const int FW_BOLD = 700;
    public const uint CLEARTYPE_QUALITY = 5;
    public const uint DI_NORMAL = 0x0003;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        public long reserved0, reserved1, reserved2, reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr> lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx, cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiplusStartupInput
    {
        public uint GdiplusVersion;
        public IntPtr DebugEventCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectF
    {
        public float X, Y, Width, Height;

        public RectF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetMessageW(out MSG message, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern int TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessageW(ref MSG message);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    public static extern int ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    public static extern int UpdateWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern int InvalidateRect(IntPtr hwnd, IntPtr rect, int erase);

    [DllImport("user32.dll")]
    public static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT paint);

    [DllImport("user32.dll")]
    public static extern int EndPaint(IntPtr hwnd, ref PAINTSTRUCT paint);

    [DllImport("user32.dll")]
    public static extern int GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint milliseconds, IntPtr callback);

    [DllImport("user32.dll")]
    public static extern int KillTimer(IntPtr hwnd, UIntPtr id);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("user32.dll")]
    public static extern int ScreenToClient(IntPtr hwnd, ref POINT point);

    [DllImport("user32.dll")]
    public static extern int GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern int SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern int SystemParametersInfoW(uint action, uint param, out RECT rect, uint winIni);

    [DllImport("user32.dll")]
    public static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int DrawTextW(IntPtr hdc, string text, int length, ref RECT rect, uint format);

    [DllImport("user32.dll")]
    public static extern int DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);

    [DllImport("user32.dll")]
    public static extern int DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint PrivateExtractIconsW(string file, int index, int width, int height, IntPtr[] icons, uint[]? ids, uint count, uint flags);

    [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
    public static extern int SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    public static extern int DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    public static extern int DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern int BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateSolidBrush(uint colorRef);

    [DllImport("gdi32.dll")]
    public static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    public static extern uint SetTextColor(IntPtr hdc, uint colorRef);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision,
        uint quality, uint pitchAndFamily, string face);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetTextExtentPoint32W(IntPtr hdc, string text, int length, out SIZE size);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandleW(IntPtr name);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("gdiplus.dll")]
    public static extern int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);

    [DllImport("gdiplus.dll")]
    public static extern void GdiplusShutdown(IntPtr token);

    [DllImport("gdiplus.dll")]
    public static extern int GdipCreateFromHDC(IntPtr hdc, out IntPtr graphics);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDeleteGraphics(IntPtr graphics);

    [DllImport("gdiplus.dll")]
    public static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);

    [DllImport("gdiplus.dll")]
    public static extern int GdipCreateLineBrushFromRect(ref RectF rect, uint color1, uint color2, int mode, int wrapMode, out IntPtr brush);

    [DllImport("gdiplus.dll")]
    public static extern int GdipSetLinePresetBlend(IntPtr brush, uint[] colors, float[] positions, int count);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDeleteBrush(IntPtr brush);

    [DllImport("gdiplus.dll")]
    public static extern int GdipFillRectangle(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    public static extern int GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDeletePen(IntPtr pen);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDrawRectangle(IntPtr graphics, IntPtr pen, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    public static extern int GdipSetClipRect(IntPtr graphics, float x, float y, float width, float height, int combineMode);

    [DllImport("gdiplus.dll")]
    public static extern int GdipResetClip(IntPtr graphics);

    [DllImport("gdiplus.dll")]
    public static extern int GdipSetPixelOffsetMode(IntPtr graphics, int mode);

    [DllImport("gdiplus.dll")]
    public static extern int GdipSetSmoothingMode(IntPtr graphics, int mode);

    [DllImport("gdiplus.dll")]
    public static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDrawEllipse(IntPtr graphics, IntPtr pen, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    public static extern int GdipDrawLine(IntPtr graphics, IntPtr pen, float x1, float y1, float x2, float y2);

    [DllImport("gdiplus.dll")]
    public static extern int GdipSetPenLineCap197819(IntPtr pen, int startCap, int endCap, int dashCap);

    public const int SmoothingModeDefault = 0;
    public const int SmoothingModeAntiAlias = 4;
    public const int LineCapRound = 2;

    public const uint WM_APP = 0x8000;
    public const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
    public const uint BIF_RETURNONLYFSDIRS = 0x0001;
    public const uint BIF_NEWDIALOGSTYLE = 0x0040;
    public const int MAX_PATH = 260;

    [StructLayout(LayoutKind.Sequential)]
    public struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public IntPtr lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    public static extern int SystemParametersInfoFlag(uint action, uint param, out int value, uint winIni);

    [DllImport("user32.dll")]
    public static extern int PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll")]
    public static extern IntPtr SHBrowseForFolderW(ref BROWSEINFOW info);

    [DllImport("shell32.dll")]
    public static extern int SHGetPathFromIDListW(IntPtr pidl, IntPtr path);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr ILCreateFromPathW(string path);

    [DllImport("shell32.dll")]
    public static extern void ILFree(IntPtr pidl);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr memory);
}
