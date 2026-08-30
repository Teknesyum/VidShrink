using System.Diagnostics;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T68: kurulumun Gezgin sağ tık menüsüne yazdığı kayıt defteri girdileri.
///
/// Ölçü gerçek <c>HKCU:\Software\Classes</c> köküne dokunmaz; betiğe
/// <c>-RegistryRoot</c> ile kendi geçici anahtarı verilir, okunur ve silinir.
/// Uzantı listesi ölçüye elle kopyalanmaz: bir yandan
/// <see cref="ShellIntegration.MediaExtensions"/>, öbür yandan betiğin kendi
/// dosyasından okunan dizi karşılaştırılır.
/// </summary>
public sealed class ShellMenuTests : IDisposable
{
    private const string TurkishLabel = "Bu Videoyu VidShrink ile Aç";
    private const string EnglishLabel = "Open this video with VidShrink";

    private const string ProbeScript = @"param([string]$Root, [string]$Out)
$lines = @()
$associations = Join-Path $Root 'SystemFileAssociations'
if (Test-Path -LiteralPath $associations) {
    foreach ($association in Get-ChildItem -LiteralPath $associations) {
        $key = Join-Path $association.PSPath 'shell\VidShrink'
        if (-not (Test-Path -LiteralPath $key)) { continue }
        $values = Get-ItemProperty -LiteralPath $key
        $command = (Get-ItemProperty -LiteralPath (Join-Path $key 'command')).'(default)'
        $lines += (@($association.PSChildName, $values.MUIVerb, $values.Icon, $command) -join ""`t"")
    }
}
Set-Content -LiteralPath $Out -Value ($lines -join ""`n"") -Encoding UTF8
";

    private static readonly string InstallerScript =
        Path.Combine(TipSources.Root, "Install-VidShrink.ps1");

    private readonly string _work;
    private readonly string _installRoot;
    private readonly string _executable;
    private readonly string _probe;
    private readonly string _registryRoot;

    public ShellMenuTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TestPaths.OutputRoot, "shell-menu", id);
        _installRoot = Path.Combine(_work, "kurulum");
        _executable = Path.Combine(_installRoot, "VidShrink.exe");
        _probe = Path.Combine(_work, "oku.ps1");
        _registryRoot = @"HKCU:\Software\VidShrink-Test-" + id;

        Directory.CreateDirectory(_installRoot);
        File.WriteAllText(_executable, "kurulu baslatici");
        File.WriteAllText(_probe, ProbeScript);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            RunPowerShell(
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                $"Remove-Item -LiteralPath '{_registryRoot}' -Recurse -Force -ErrorAction SilentlyContinue");
        }

        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    private sealed record Entry(string Extension, string Label, string Icon, string Command);

    private static (int Code, string Output) RunPowerShell(params string[] arguments)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("powershell.exe başlatılamadı.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private (int Code, string Output) RunInstaller(params string[] arguments)
    {
        var all = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", InstallerScript };
        all.AddRange(arguments);
        return RunPowerShell(all.ToArray());
    }

    private (int Code, string Output) WriteMenu(params string[] arguments)
    {
        var all = new List<string>
        {
            "-ShellMenuOnly", "-InstallRoot", _installRoot, "-RegistryRoot", _registryRoot
        };
        all.AddRange(arguments);
        return RunInstaller(all.ToArray());
    }

    private IReadOnlyList<Entry> ReadMenu()
    {
        var listing = Path.Combine(_work, "girdiler.txt");
        var run = RunPowerShell(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", _probe,
            "-Root", _registryRoot, "-Out", listing);
        Assert.True(run.Code == 0, run.Output);

        var entries = new List<Entry>();
        foreach (var line in File.ReadAllLines(listing))
        {
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            entries.Add(new Entry(parts[0], parts[1], parts[2], parts[3]));
        }
        return entries;
    }

    private bool RegistryRootExists()
    {
        var run = RunPowerShell(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $"if (Test-Path -LiteralPath '{_registryRoot}') {{ 'VAR' }} else {{ 'YOK' }}");
        return run.Output.Contains("VAR", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> InstallerExtensions()
    {
        var source = File.ReadAllText(InstallerScript);
        var block = Regex.Match(source, @"\$shellMenuExtensions\s*=\s*@\((?<body>[^)]*)\)");
        Assert.True(block.Success, "Install-VidShrink.ps1 içinde $shellMenuExtensions dizisi bulunamadı.");

        return Regex.Matches(block.Groups["body"].Value, @"'([^']+)'")
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    private static IEnumerable<string> Sorted(IEnumerable<string> values)
        => values.OrderBy(value => value, StringComparer.Ordinal);

    [Fact]
    public void Installer_list_is_the_application_list()
    {
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(InstallerExtensions()));
    }

    [Fact]
    public void Written_extensions_are_the_application_list()
    {
        if (!OperatingSystem.IsWindows()) return;

        var run = WriteMenu();
        Assert.True(run.Code == 0, run.Output);

        var written = ReadMenu().Select(entry => entry.Extension.TrimStart('.'));
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(written));
    }

    [Fact]
    public void Every_command_calls_the_installed_launcher_with_the_path()
    {
        if (!OperatingSystem.IsWindows()) return;

        var run = WriteMenu();
        Assert.True(run.Code == 0, run.Output);

        var entries = ReadMenu();
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            Assert.Equal($"\"{_executable}\" \"%1\"", entry.Command);
            Assert.Equal(_executable, entry.Icon);
        }
    }

    [Fact]
    public void Remove_switch_leaves_no_entry()
    {
        if (!OperatingSystem.IsWindows()) return;

        WriteMenu();
        Assert.NotEmpty(ReadMenu());

        var removal = RunInstaller("-RemoveShellMenu", "-RegistryRoot", _registryRoot);
        Assert.True(removal.Code == 0, removal.Output);
        Assert.Empty(ReadMenu());
    }

    [Fact]
    public void Skip_shortcuts_writes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;

        var run = WriteMenu("-SkipShortcuts");
        Assert.True(run.Code == 0, run.Output);
        Assert.False(RegistryRootExists());
    }

    [Fact]
    public void Second_run_neither_fails_nor_duplicates()
    {
        if (!OperatingSystem.IsWindows()) return;

        var first = WriteMenu();
        Assert.True(first.Code == 0, first.Output);
        var afterFirst = ReadMenu().Count;

        var second = WriteMenu();
        Assert.True(second.Code == 0, second.Output);

        Assert.Equal(ShellIntegration.MediaExtensions.Count, afterFirst);
        Assert.Equal(afterFirst, ReadMenu().Count);
    }

    [Fact]
    public void Forced_language_decides_the_label()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.True(WriteMenu("-MenuLanguage", "tr").Code == 0);
        Assert.All(ReadMenu(), entry => Assert.Equal(TurkishLabel, entry.Label));

        Assert.True(WriteMenu("-MenuLanguage", "en").Code == 0);
        Assert.All(ReadMenu(), entry => Assert.Equal(EnglishLabel, entry.Label));
    }

    [Fact]
    public void Automatic_language_follows_the_system_interface()
    {
        if (!OperatingSystem.IsWindows()) return;

        var systemLanguage = RunPowerShell(
            "-NoProfile", "-Command", "(Get-UICulture).TwoLetterISOLanguageName");
        Assert.True(systemLanguage.Code == 0, systemLanguage.Output);
        var expected = systemLanguage.Output.Trim().Equals("tr", StringComparison.OrdinalIgnoreCase)
            ? TurkishLabel
            : EnglishLabel;

        Assert.True(WriteMenu().Code == 0);
        Assert.All(ReadMenu(), entry => Assert.Equal(expected, entry.Label));
    }
}
