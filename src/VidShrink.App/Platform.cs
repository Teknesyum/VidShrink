using System;
using System.Diagnostics;
using System.IO;

namespace VidShrink.App;

internal static class Platform
{
    public static void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(Launch("open", "-R", path));
            return;
        }

        var folder = Path.GetDirectoryName(path);
        if (folder is null) throw new DirectoryNotFoundException(path);
        Process.Start(Launch("xdg-open", null, folder));
    }

    private static ProcessStartInfo Launch(string fileName, string? option, string target)
    {
        var psi = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
        if (option is not null) psi.ArgumentList.Add(option);
        psi.ArgumentList.Add(target);
        return psi;
    }
}
