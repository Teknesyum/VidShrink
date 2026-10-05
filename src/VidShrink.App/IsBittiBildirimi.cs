using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace VidShrink.App;

/// <summary>Biten bir işin sonucu; iptal kullanıcının kendi kararıdır ve haber verilmez.</summary>
internal enum IsSonucu
{
    Basarili,
    Hatali,
    Iptal
}

/// <summary>Biten tek iş: dosyanın yolu ve sonucu.</summary>
internal readonly record struct BitenIs(string Yol, IsSonucu Sonuc);

/// <summary>Kullanıcıya gidecek haber: başlık, tek satır metin ve hata mı olduğu.</summary>
internal sealed record IsBildirimi(string Baslik, string Metin, bool Hata);

/// <summary>
/// "İş bitince haber ver" kararı. Platform çağrısından ayrı ve saf: haber verilecek mi,
/// verilecekse hangi cümleyle. Pencere etkinken, ayar kapalıyken ve yalnız iptal edilen
/// işlerde <c>null</c> döner. Dosya adı cümleye şablon çevrildikten sonra girer; başlık
/// kuralı adın harflerine dokunmaz.
/// </summary>
internal static class IsBittiBildirimi
{
    internal const string Baslik = "VidShrink";
    internal const string TekBasarili = "main.notify.done";
    internal const string TekHatali = "main.notify.failed";
    internal const string KuyrukBasarili = "main.notify.queue";
    internal const string KuyrukHatali = "main.notify.queue-failed";

    internal static IsBildirimi? Karar(IReadOnlyList<BitenIs> isler, bool pencereEtkin, bool ayarAcik, Func<string, object?[], string> say)
    {
        if (!ayarAcik || pencereEtkin) return null;

        var sayilan = isler.Where(i => i.Sonuc != IsSonucu.Iptal).ToList();
        if (sayilan.Count == 0) return null;

        var hatali = sayilan.Count(i => i.Sonuc == IsSonucu.Hatali);
        var basarili = sayilan.Count - hatali;
        if (sayilan.Count == 1)
        {
            var ad = Path.GetFileName(sayilan[0].Yol);
            return new IsBildirimi(Baslik, say(hatali == 1 ? TekHatali : TekBasarili, new object?[] { ad }), hatali == 1);
        }

        return hatali == 0
            ? new IsBildirimi(Baslik, say(KuyrukBasarili, new object?[] { basarili }), false)
            : new IsBildirimi(Baslik, say(KuyrukHatali, new object?[] { basarili, hatali }), basarili == 0);
    }

    /// <summary>
    /// Haberin sistem yüzü. Gerçek çağrı yalnız masaüstü yaşam döngüsünde koşan uygulamada
    /// seçilir; test konağında sessiz olan gelir, gerçek pencere yanıp sönmez.
    /// </summary>
    internal static IIsBildirimYuzu Varsayilan()
        => OperatingSystem.IsWindows()
           && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            ? SistemIsBildirimYuzu.Instance
            : SessizIsBildirimYuzu.Instance;
}

/// <summary>Haberin sistem yüzü. Pencere bunu çağırır, testler sahtesini verir.</summary>
internal interface IIsBildirimYuzu
{
    void Bildir(Window pencere, IsBildirimi bildirim);
}

/// <summary>Platform çağrısı olmayan yerde ve test konağında kullanılan boş yüz.</summary>
internal sealed class SessizIsBildirimYuzu : IIsBildirimYuzu
{
    internal static SessizIsBildirimYuzu Instance { get; } = new();

    public void Bildir(Window pencere, IsBildirimi bildirim)
    {
    }
}

/// <summary>
/// Windows: görev çubuğu düğmesi pencere öne gelene dek yanıp söner ve bildirim alanında bir
/// balon çıkar. Balonun simgesi pencere öne gelince ya da kapanırken kaldırılır. Hiçbir hata
/// dışarı sızmaz: haber verilemeyen iş yine bitmiştir.
/// </summary>
internal sealed class SistemIsBildirimYuzu : IIsBildirimYuzu
{
    internal static SistemIsBildirimYuzu Instance { get; } = new();

    public void Bildir(Window pencere, IsBildirimi bildirim)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var tutamac = pencere.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (tutamac == IntPtr.Zero) return;

            WindowsDikkat.YanipSon(tutamac);
            if (!WindowsDikkat.BalonGoster(tutamac, bildirim.Baslik, bildirim.Metin, bildirim.Hata)) return;

            void Kaldir(object? sender, EventArgs e)
            {
                pencere.Activated -= Kaldir;
                pencere.Closing -= Kaldir;
                try { WindowsDikkat.BalonuKaldir(tutamac); }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
            }

            pencere.Activated += Kaldir;
            pencere.Closing += Kaldir;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
        }
    }
}

/// <summary>
/// <c>FlashWindowEx</c> ve <c>Shell_NotifyIcon</c> balonu. Avalonia'ya bağlı değildir; yalnız
/// pencere tutamacı alır.
/// </summary>
internal static class WindowsDikkat
{
    private const uint FlashTray = 0x2;
    private const uint FlashUntilForeground = 0xC;
    private const int NimAdd = 0;
    private const int NimDelete = 2;
    private const int NifIcon = 0x2;
    private const int NifTip = 0x4;
    private const int NifInfo = 0x10;
    private const int NiifInfo = 0x1;
    private const int NiifError = 0x3;
    private const int WmGetIcon = 0x7F;
    private const int IdiApplication = 32512;
    private const int BalonKimligi = 0x5653;

    internal static bool YanipSon(IntPtr tutamac)
    {
        var bilgi = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = tutamac,
            dwFlags = FlashTray | FlashUntilForeground,
            uCount = uint.MaxValue,
            dwTimeout = 0
        };
        return FlashWindowEx(ref bilgi);
    }

    internal static bool BalonGoster(IntPtr tutamac, string baslik, string metin, bool hata)
    {
        BalonuKaldir(tutamac);
        var veri = Veri(tutamac);
        veri.uFlags = NifIcon | NifTip | NifInfo;
        veri.hIcon = Simge(tutamac);
        veri.szTip = Kirp(baslik, 127);
        veri.szInfoTitle = Kirp(baslik, 63);
        veri.szInfo = Kirp(metin, 255);
        veri.dwInfoFlags = hata ? NiifError : NiifInfo;
        return Shell_NotifyIconW(NimAdd, ref veri);
    }

    internal static bool BalonuKaldir(IntPtr tutamac)
    {
        var veri = Veri(tutamac);
        return Shell_NotifyIconW(NimDelete, ref veri);
    }

    private static NOTIFYICONDATAW Veri(IntPtr tutamac) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = tutamac,
        uID = BalonKimligi,
        szTip = "",
        szInfo = "",
        szInfoTitle = ""
    };

    private static IntPtr Simge(IntPtr tutamac)
    {
        foreach (var tur in new[] { 2, 0, 1 })
        {
            var simge = SendMessageW(tutamac, WmGetIcon, new IntPtr(tur), IntPtr.Zero);
            if (simge != IntPtr.Zero) return simge;
        }
        return LoadIconW(IntPtr.Zero, new IntPtr(IdiApplication));
    }

    private static string Kirp(string metin, int uzunluk) => metin.Length <= uzunluk ? metin : metin[..uzunluk];

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO bilgi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr tutamac, int ileti, IntPtr w, IntPtr l);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr ornek, IntPtr ad);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(int ileti, ref NOTIFYICONDATAW veri);
}
