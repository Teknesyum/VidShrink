using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VidShrink.App.Playback;

/// <summary>OYA-01: sistemin uyku ve ekran kararması engelini koyan ve kaldıran katman. Testte sahtesi takılır.</summary>
internal interface IUykuSaglayici
{
    string Ad { get; }

    bool Al();

    void Birak();
}

/// <summary>Hiçbir şey yapmayan sağlayıcı: test süreci ve desteklenmeyen sistem (Linux sahibe soruluyor).</summary>
internal sealed class BosUykuSaglayici : IUykuSaglayici
{
    public string Ad => "yok";

    public bool Al() => false;

    public void Birak()
    {
    }
}

/// <summary>
/// Windows: <c>SetThreadExecutionState</c>. Bayrak çağıran iş parçacığına bağlı; ikisi de arayüz iş
/// parçacığından çağrılır, bırakma yalnız <c>ES_CONTINUOUS</c> yazar.
/// </summary>
internal sealed class WindowsUykuSaglayici : IUykuSaglayici
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    public string Ad => "windows";

    public bool Al() => SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired) != 0;

    public void Birak() => SetThreadExecutionState(EsContinuous);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}

/// <summary>macOS: <c>IOPMAssertionCreateWithName</c> ile <c>PreventUserIdleDisplaySleep</c>; bırakınca kimlik salınır.</summary>
internal sealed class MacUykuSaglayici : IUykuSaglayici
{
    private const string IoKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint Utf8 = 0x08000100;
    private const uint LevelOn = 255;

    private uint _kimlik;
    private bool _tutuluyor;

    public string Ad => "macos";

    public bool Al()
    {
        if (_tutuluyor) return true;
        var tur = CFStringCreateWithCString(IntPtr.Zero, "PreventUserIdleDisplaySleep", Utf8);
        var ad = CFStringCreateWithCString(IntPtr.Zero, Core.AppIdentity.Name, Utf8);
        try
        {
            _tutuluyor = IOPMAssertionCreateWithName(tur, LevelOn, ad, out _kimlik) == 0;
            return _tutuluyor;
        }
        finally
        {
            if (tur != IntPtr.Zero) CFRelease(tur);
            if (ad != IntPtr.Zero) CFRelease(ad);
        }
    }

    public void Birak()
    {
        if (!_tutuluyor) return;
        _tutuluyor = false;
        IOPMAssertionRelease(_kimlik);
    }

    [DllImport(IoKit)]
    private static extern int IOPMAssertionCreateWithName(IntPtr type, uint level, IntPtr name, out uint id);

    [DllImport(IoKit)]
    private static extern int IOPMAssertionRelease(uint id);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);
}

/// <summary>
/// OYA-01: oynatma sürerken uyku engelini tutar. Birden çok oynatıcı (oynatıcı sekmesi, düzenleyici
/// önizlemesi) aynı engeli paylaşır: ilk isteyen alır, son bırakan kaldırır. Windows bayrağı iş parçacığı
/// başına tek değer olduğundan sayılmadan bırakmak öbür oynatıcının engelini de silerdi.
/// <para>Süreç varsayılanı boş sağlayıcıdır; gerçek sağlayıcıyı yalnız <c>Program.Main</c> bağlar, testler sistemi değiştirmez.</para>
/// </summary>
internal sealed class UykuEngeli
{
    private readonly HashSet<object> _sahipler = new(ReferenceEqualityComparer.Instance);

    internal UykuEngeli(IUykuSaglayici saglayici) => Saglayici = saglayici;

    internal static UykuEngeli Varsayilan { get; private set; } = new(new BosUykuSaglayici());

    internal IUykuSaglayici Saglayici { get; }

    internal bool Tutuluyor => _sahipler.Count > 0;

    internal int SahipSayisi => _sahipler.Count;

    internal static IUykuSaglayici SistemSaglayicisi()
    {
        if (OperatingSystem.IsWindows()) return new WindowsUykuSaglayici();
        if (OperatingSystem.IsMacOS()) return new MacUykuSaglayici();
        return new BosUykuSaglayici();
    }

    internal static void SistemiBagla() => Varsayilan = new UykuEngeli(SistemSaglayicisi());

    internal void Yansit(object sahip, bool iste)
    {
        if (iste)
        {
            if (_sahipler.Add(sahip) && _sahipler.Count == 1) Saglayici.Al();
            return;
        }

        if (_sahipler.Remove(sahip) && _sahipler.Count == 0) Saglayici.Birak();
    }
}
