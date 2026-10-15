using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VidShrink.App;

/// <summary>
/// AYR-P4: canlandırma kararının tek yeri. Sistemin "hareketi azalt" ayarı (Windows'ta istemci
/// alanı canlandırması, macOS'ta erişilebilirlik ayarı, Linux'ta GNOME'un canlandırma anahtarı)
/// ile Ayarlar'daki elle anahtar birleşir; biri azaltıyorsa canlandırma kapalıdır. Sistem ayarı
/// oturum boyunca bir kez okunur; elle anahtar değişince <see cref="Changed"/> yayılır.
/// </summary>
internal static class MotionPreference
{
    internal enum Platform { Windows, MacOS, Linux, Other }

    /// <summary>Sistem ayarının ham kaynağı. Testler sahtesini takar; gerçek okuma <see cref="SystemSource"/>.</summary>
    internal interface ISource
    {
        bool? WindowsClientAreaAnimation();

        string? MacReduceMotion();

        string? GnomeEnableAnimations();
    }

    private static ISource _source = new SystemSource();
    private static bool? _system;
    private static bool? _override;
    private static bool _user;

    internal static event EventHandler? Changed;

    internal static ISource Source
    {
        get => _source;
        set
        {
            _source = value;
            _system = null;
        }
    }

    /// <summary>Ayarlar'daki "canlandırmaları kapat" anahtarı.</summary>
    internal static bool UserReduced
    {
        get => _user;
        set
        {
            if (_user == value) return;
            _user = value;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    internal static bool SystemReduced => _system ??= ReadSystem(CurrentPlatform, _source);

    /// <summary>Canlandırma kapalı mı. Test setter'ı sistemi ve anahtarı ezer.</summary>
    internal static bool Reduced
    {
        get => _override ?? (_user || SystemReduced);
        set => _override = value;
    }

    internal static void ResetForTest()
    {
        _system = null;
        _override = null;
        _user = false;
    }

    internal static Platform CurrentPlatform
        => OperatingSystem.IsWindows() ? Platform.Windows
            : OperatingSystem.IsMacOS() ? Platform.MacOS
            : OperatingSystem.IsLinux() ? Platform.Linux
            : Platform.Other;

    /// <summary>Platformun kendi kaynağını okur; okunamayan ayar canlandırmaya izin verir.</summary>
    internal static bool ReadSystem(Platform platform, ISource source) => platform switch
    {
        Platform.Windows => source.WindowsClientAreaAnimation() == false,
        Platform.MacOS => source.MacReduceMotion()?.Trim() is "1" or "true",
        Platform.Linux => source.GnomeEnableAnimations()?.Trim() == "false",
        _ => false,
    };

    private sealed class SystemSource : ISource
    {
        private const uint ClientAreaAnimationQuery = 0x1042;

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfoW(uint action, uint param, ref int value, uint update);

        public bool? WindowsClientAreaAnimation()
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                var enabled = 1;
                return SystemParametersInfoW(ClientAreaAnimationQuery, 0, ref enabled, 0) ? enabled != 0 : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string? MacReduceMotion() => Run("defaults", "read", "com.apple.universalaccess", "reduceMotion");

        public string? GnomeEnableAnimations() => Run("gsettings", "get", "org.gnome.desktop.interface", "enable-animations");

        private static string? Run(string file, params string[] arguments)
        {
            try
            {
                var info = new ProcessStartInfo(file)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
                using var process = Process.Start(info);
                if (process is null) return null;
                var error = process.StandardError.ReadToEndAsync();
                var output = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(2000))
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    return null;
                }
                Task.WaitAll(output, error);
                return process.ExitCode == 0 ? output.Result : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
