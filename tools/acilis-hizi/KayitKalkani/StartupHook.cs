using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

internal static class StartupHook
{
    private const int CikisKodu = 97;
    private static readonly IntPtr Hkcu = new(unchecked((int)0x80000001));

    private static readonly string[] Kopyalar =
    {
        @"Software\Teknesyum\VidShrink\ShellLabels",
        @"Software\Classes\Teknesyum.VidShrink.Video",
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
    };

    public static void Initialize()
    {
        var dizin = Environment.GetEnvironmentVariable("VIDSHRINK_OLCUM_KOVAN");
        if (string.IsNullOrEmpty(dizin)) return;

        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        try { WerSetFlags(0x20 | 0x8); } catch (Exception) { }

        try
        {
            Directory.CreateDirectory(dizin);
            var dosya = Path.Combine(dizin, $"{Environment.ProcessId}-{Environment.TickCount64}.dat");
            var sonuc = RegLoadAppKey(dosya, out var kovan, 0xF003F, 0, 0);
            if (sonuc != 0) throw new InvalidOperationException("RegLoadAppKey " + sonuc);

            using (var hedef = RegistryKey.FromHandle(new SafeRegistryHandle(kovan, ownsHandle: false)))
            {
                foreach (var yol in Kopyalar.Concat(MenuYollari()))
                {
                    using var kaynak = Registry.CurrentUser.OpenSubKey(yol);
                    if (kaynak is not null) Kopyala(kaynak, hedef, yol);
                }
            }

            sonuc = RegOverridePredefKey(Hkcu, kovan);
            if (sonuc != 0) throw new InvalidOperationException("RegOverridePredefKey " + sonuc);
            File.WriteAllText(Path.Combine(dizin, $"{Environment.ProcessId}.kalkan"), Environment.ProcessPath ?? "");
        }
        catch (Exception e)
        {
            try { File.WriteAllText(Path.Combine(dizin, $"{Environment.ProcessId}.hata"), e.ToString()); } catch (Exception) { }
            Environment.Exit(CikisKodu);
        }
    }

    private static IEnumerable<string> MenuYollari()
    {
        const string kok = @"Software\Classes\SystemFileAssociations";
        using var k = Registry.CurrentUser.OpenSubKey(kok);
        if (k is null) yield break;
        foreach (var uzanti in k.GetSubKeyNames())
            foreach (var menu in new[] { "VidShrink", "VidShrinkKucult" })
                yield return $@"{kok}\{uzanti}\shell\{menu}";
    }

    private static void Kopyala(RegistryKey kaynak, RegistryKey hedefKok, string yol)
    {
        using var hedef = hedefKok.CreateSubKey(yol, writable: true);
        foreach (var ad in kaynak.GetValueNames())
            hedef.SetValue(ad, kaynak.GetValue(ad, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, kaynak.GetValueKind(ad));
        foreach (var alt in kaynak.GetSubKeyNames())
        {
            using var altKaynak = kaynak.OpenSubKey(alt);
            if (altKaynak is not null) Kopyala(altKaynak, hedefKok, yol + "\\" + alt);
        }
    }

    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll")] private static extern int WerSetFlags(uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegLoadAppKey(string file, out IntPtr key, int sam, int options, int reserved);
    [DllImport("advapi32.dll")] private static extern int RegOverridePredefKey(IntPtr key, IntPtr newKey);
}
