using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using VidShrink.Core.Setup;

namespace VidShrink.Setup;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var ownConsole = GetConsoleProcessList(new uint[2], 2) == 1;
        try
        {
            SetupText.Use(UserLocaleName());
            var parsed = Parse(args);
            if (parsed is null)
            {
                Console.WriteLine(SetupText.Get("setup.help"));
                return 0;
            }
            if (SetupLaunch.UsePanel(args)) return RunPanel(parsed.Value.Options, ownConsole);
            Run(parsed.Value.Options, parsed.Value.Uninstall, parsed.Value.Timings).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception exception) when (exception is SetupException or IOException or UnauthorizedAccessException or HttpRequestException or ArgumentException)
        {
            Write(exception.Message, ConsoleColor.Red);
            if (ownConsole)
            {
                Console.WriteLine(SetupText.Get("setup.console.press-enter"));
                Console.ReadLine();
            }
            return 1;
        }
    }

    private static int RunPanel(SetupOptions options, bool ownConsole)
    {
        var rehearsal = SetupLaunch.RehearsalRequested(Environment.GetEnvironmentVariable(SetupLaunch.RehearsalVariable));
        var logRoot = options.LocalAppData;
        if (rehearsal)
        {
            var scratch = Path.Combine(Path.GetTempPath(), "vidshrink-prova-" + Guid.NewGuid().ToString("N")[..8]);
            options = SetupLaunch.Rehearsal(options, scratch);
            logRoot = scratch;
        }
        options = SetupLaunch.ForPanel(options);
        SetupText.Use(ShellRegistration.ResolveLanguage(options.MenuLanguage, UserLocaleName));
        if (ownConsole) Native.ShowWindow(Native.GetConsoleWindow(), Native.SW_HIDE);
        return Panel.Run(options, rehearsal, SetupLaunch.LogPath(logRoot), (log, step) => CreateHost(log, step, rehearsal));
    }

    private static async Task Run(SetupOptions options, bool uninstall, bool timings)
    {
        var host = CreateHost(message => Write(message, ConsoleColor.Cyan), (_, _, _) => { }, rehearsal: false);
        var clock = Stopwatch.StartNew();
        if (uninstall)
        {
            await SetupRunner.UninstallAsync(options, host, CancellationToken.None);
        }
        else
        {
            var result = await SetupRunner.InstallAsync(options, host, CancellationToken.None);
            if (timings)
            {
                foreach (var step in result.Steps) Console.WriteLine($"sure {step.Name} {step.Milliseconds}");
            }
        }
        if (timings) Console.WriteLine($"sure toplam {clock.ElapsedMilliseconds}");
    }

    private static (SetupOptions Options, bool Uninstall, bool Timings)? Parse(string[] args)
    {
        var localAppData = LocalAppData();
        string? installRoot = null;
        var registryRoot = @"HKCU:\Software\Classes";
        var language = "auto";
        bool skip = false, noLaunch = false, uninstall = false, timings = false, downloadFfmpeg = false;
        string? tag = null, assetSource = null, shortcutDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new SetupException(SetupText.Get("setup.arg.value-expected", args[i]));
            switch (args[i].ToLowerInvariant())
            {
                case "--help" or "-h" or "/?": return null;
                case "--uninstall": uninstall = true; break;
                case "--install-root": installRoot = Next(); break;
                case "--registry-root": registryRoot = Next(); break;
                case "--menu-language":
                    language = Next();
                    if (language is not ("auto" or "tr" or "en")) throw new SetupException(SetupText.Get("setup.arg.bad-menu-language", language));
                    break;
                case "--skip-shortcuts": skip = true; break;
                case "--no-launch": noLaunch = true; break;
                case "--shortcut-dir": shortcutDirectory = Path.GetFullPath(Next()); break;
                case "--tag": tag = Next(); break;
                case "--asset-source": assetSource = Path.GetFullPath(Next()); break;
                case "--download-ffmpeg": downloadFfmpeg = true; break;
                case "--timings": timings = true; break;
                case "--console" or "--panel": break;
                default: throw new SetupException(SetupText.Get("setup.arg.unknown-option", args[i]));
            }
        }

        var options = new SetupOptions
        {
            InstallRoot = installRoot ?? Path.Combine(localAppData, "Programs", "VidShrink"),
            LocalAppData = localAppData,
            WorkRoot = Path.GetTempPath(),
            ClassesRoot = SetupOptions.NormalizeRegistryRoot(registryRoot),
            MenuLanguage = language,
            SkipShortcuts = skip,
            NoLaunch = noLaunch,
            Tag = tag,
            AssetSource = assetSource,
            ShortcutDirectory = shortcutDirectory,
            ForceFfmpegDownload = downloadFfmpeg
        };
        return (options, uninstall, timings);
    }

    private static SetupHost CreateHost(Action<string> log, Action<int, int, string> step, bool rehearsal) => new()
    {
        Log = log,
        Step = step,
        FindHolders = LockedFolder.FindProcesses,
        FindTool = FindTool,
        Shortcuts = rehearsal ? null : new ShellShortcuts(),
        ShellPackage = rehearsal ? null : new PowerShellPackage(),
        Windows11 = Environment.OSVersion.Version.Build >= 22000,
        UiLanguage = UserLocaleName,
        Launch = path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) })?.Dispose(),
        AssociationChanged = ShellRegistration.NotifyAssociationChanged,
        DesktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        ProgramsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs)
    };

    private static string? FindTool(string name)
    {
        var file = name + ".exe";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), file);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        var link = Path.Combine(LocalAppData(), "Microsoft", "WinGet", "Links", file);
        return File.Exists(link) ? link : null;
    }

    private static string LocalAppData() =>
        Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static void Write(string message, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetUserDefaultLocaleName(System.Text.StringBuilder name, int size);

    /// <summary>
    /// İşletim sisteminin dil etiketi (<c>de-DE</c>, <c>zh-Hans-CN</c>). Kurucu
    /// <c>InvariantGlobalization</c> ile derlendiği için <c>CultureInfo</c> burada
    /// güvenilir değil; etiket doğrudan Win32'den alınıp klasör adıyla eşlenir.
    /// Çağrı başarısız olursa eski iki dilli kola düşülür.
    /// </summary>
    private static string UserLocaleName()
    {
        try
        {
            var tampon = new System.Text.StringBuilder(85);
            if (GetUserDefaultLocaleName(tampon, tampon.Capacity) > 0) return tampon.ToString();
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException)
        {
        }

        return (GetUserDefaultUILanguage() & 0x3FF) == 0x1F ? "tr" : "en";
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList(uint[] processes, uint count);

    private sealed class PowerShellPackage : IShellPackage
    {
        public bool Registered() => ShellRegistration.PackageRegistered();

        public void Remove()
        {
            if (Invoke($"Get-AppxPackage -Name '{ShellRegistration.PackageName}' | Remove-AppxPackage -ErrorAction Stop") != 0)
                throw new SetupException(SetupText.Get("setup.shell.package-remove-failed"));
        }

        public bool Register(string manifestPath, string externalLocation) =>
            Invoke($"Add-AppxPackage -Register '{Quote(manifestPath)}' -ExternalLocation '{Quote(externalLocation)}' -ErrorAction Stop") == 0;

        private static string Quote(string value) => value.Replace("'", "''");

        private static int Invoke(string command)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.Environment.Remove("PSModulePath");
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new SetupException(SetupText.Get("setup.powershell.start-failed"));
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(output, error);
            return process.ExitCode;
        }
    }
}
