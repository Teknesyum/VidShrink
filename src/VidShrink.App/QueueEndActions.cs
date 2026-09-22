using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace VidShrink.App;

/// <summary>Kuyruk boşalınca kullanıcının seçtiği eylem (C1-4).</summary>
internal enum QueueEndChoice
{
    Nothing,
    OpenFolder,
    Sleep,
    PowerOff
}

/// <summary>
/// Kuyruk sonu eyleminin sistem yüzü. Pencere bunu çağırır, testler sahtesini verir:
/// uyutma ve kapatma test sürecinde asla gerçek çağrıya ulaşmaz.
/// </summary>
internal interface IQueueEndActions
{
    void Reveal(string path);
    void Sleep();
    void PowerOff();
}

/// <summary>Gerçek sistem çağrıları; her işletim sisteminin kendi aracıyla.</summary>
internal sealed class SystemQueueEndActions : IQueueEndActions
{
    internal static SystemQueueEndActions Instance { get; } = new();

    public void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)!) { UseShellExecute = true });
    }

    public void Sleep()
    {
        if (OperatingSystem.IsWindows()) Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
        else if (OperatingSystem.IsMacOS()) Start("pmset", "sleepnow");
        else Start("systemctl", "suspend");
    }

    public void PowerOff()
    {
        if (OperatingSystem.IsWindows()) Start("shutdown.exe", "/s /t 0");
        else if (OperatingSystem.IsMacOS()) Start("osascript", "-e \"tell application \\\"System Events\\\" to shut down\"");
        else Start("systemctl", "poweroff");
    }

    private static void Start(string file, string arguments)
    {
        try { Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true }); }
        catch (Exception ex) when (ex is IOException or Win32Exception) { }
    }
}
