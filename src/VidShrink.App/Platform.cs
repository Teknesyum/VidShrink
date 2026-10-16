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

    /// <summary>
    /// Dosyayı çöpe taşır; kalıcı silmez. Windows'ta Geri Dönüşüm Kutusu, macOS'ta <c>~/.Trash</c>, Linux'ta
    /// <c>gio trash</c>. Taşınamazsa <see cref="IOException"/> atar, dosya yerinde kalır.
    /// </summary>
    public static void MoveToTrash(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        if (OperatingSystem.IsWindows())
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            Directory.CreateDirectory(trash);
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var target = Path.Combine(trash, name + extension);
            for (var index = 2; File.Exists(target); index++)
                target = Path.Combine(trash, name + " " + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + extension);
            File.Move(path, target);
            return;
        }

        var psi = Launch("gio", "trash", path);
        psi.RedirectStandardError = true;
        using var gio = Process.Start(psi) ?? throw new IOException(path);
        var error = gio.StandardError.ReadToEnd();
        if (!gio.WaitForExit(10_000) || gio.ExitCode != 0 || File.Exists(path)) throw new IOException(error.Length > 0 ? error.Trim() : path);
    }

    private static ProcessStartInfo Launch(string fileName, string? option, string target)
    {
        var psi = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
        if (option is not null) psi.ArgumentList.Add(option);
        psi.ArgumentList.Add(target);
        return psi;
    }
}
