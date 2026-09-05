using System.Diagnostics;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed record ShrinkEntry(
    string Extension,
    string ParentLabel,
    string SubCommands,
    string ParentMultiSelectModel,
    string Target,
    string TargetLabel,
    string MultiSelectModel,
    string Command);

public sealed record OpenEntry(string Extension, string Label, string Icon, string Command);

/// <summary>
/// T169 ölçüsünün tek düzeneği: kurulum betiği bir kez koşar, dökümler bir kez okunur,
/// bütün kollar aynı çıktıyı okur. Gerçek <c>HKCU:\Software\Classes</c> ağacına
/// dokunulmaz; betiğe <c>-RegistryRoot</c> ile geçici bir kök verilir ve sonunda silinir.
/// </summary>
public sealed class ShellShrinkMenuFixture : IDisposable
{
    public static readonly string InstallerScript =
        Path.Combine(TipSources.Root, "Install-VidShrink.ps1");

    private const string ShrinkProbe = @"param([string]$Root, [string]$Out)
$lines = @()
$associations = Join-Path $Root 'SystemFileAssociations'
if (Test-Path -LiteralPath $associations) {
    foreach ($association in Get-ChildItem -LiteralPath $associations) {
        $verbKey = Join-Path $association.PSPath 'shell\VidShrinkKucult'
        if (-not (Test-Path -LiteralPath $verbKey)) { continue }
        $verb = Get-ItemProperty -LiteralPath $verbKey
        foreach ($child in Get-ChildItem -LiteralPath (Join-Path $verbKey 'shell')) {
            $values = Get-ItemProperty -LiteralPath $child.PSPath
            $command = (Get-ItemProperty -LiteralPath (Join-Path $child.PSPath 'command')).'(default)'
            $lines += (@($association.PSChildName, $verb.MUIVerb, $verb.SubCommands, $verb.MultiSelectModel, $child.PSChildName, $values.MUIVerb, $values.MultiSelectModel, $command) -join ""`t"")
        }
    }
}
Set-Content -LiteralPath $Out -Value ($lines -join ""`n"") -Encoding UTF8
";

    private const string OpenProbe = @"param([string]$Root, [string]$Out)
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

    private readonly string _work;
    private readonly string _shrinkProbe;
    private readonly string _openProbe;

    public bool OnWindows { get; }
    public string Executable { get; }
    public string RegistryRoot { get; }

    public int WriteExitCode { get; }
    public string WriteOutput { get; } = "";
    public int RemoveExitCode { get; }
    public string RemoveOutput { get; } = "";

    public int KeysBeforeWrite { get; }
    public int KeysAfterWrite { get; }
    public int KeysAfterRemoval { get; }
    public int ExtensionKeysAfterWrite { get; }
    public int ExtensionKeysAfterRemoval { get; }

    public IReadOnlyList<ShrinkEntry> ShrinkEntries { get; } = Array.Empty<ShrinkEntry>();
    public IReadOnlyList<OpenEntry> OpenEntries { get; } = Array.Empty<OpenEntry>();
    public IReadOnlyList<ShrinkEntry> ShrinkEntriesAfterRemoval { get; } = Array.Empty<ShrinkEntry>();
    public IReadOnlyList<OpenEntry> OpenEntriesAfterRemoval { get; } = Array.Empty<OpenEntry>();

    public ShellShrinkMenuFixture()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TestPaths.OutputRoot, "shell-shrink-menu", id);
        var installRoot = Path.Combine(_work, "kurulum");
        Executable = Path.Combine(installRoot, "VidShrink.exe");
        _shrinkProbe = Path.Combine(_work, "oku-kucult.ps1");
        _openProbe = Path.Combine(_work, "oku-ac.ps1");
        RegistryRoot = $@"HKCU:\Software\VidShrinkKucult-Test-{Environment.ProcessId}-{id}";

        Directory.CreateDirectory(installRoot);
        File.WriteAllText(Executable, "kurulu baslatici");
        File.WriteAllText(_shrinkProbe, ShrinkProbe);
        File.WriteAllText(_openProbe, OpenProbe);

        OnWindows = OperatingSystem.IsWindows();
        if (!OnWindows) return;

        KeysBeforeWrite = CountKeys();

        var write = RunInstaller(
            "-ShellMenuOnly", "-InstallRoot", installRoot,
            "-RegistryRoot", RegistryRoot, "-MenuLanguage", "tr");
        WriteExitCode = write.Code;
        WriteOutput = write.Output;

        KeysAfterWrite = CountKeys();
        ExtensionKeysAfterWrite = CountExtensionKeys();
        ShrinkEntries = ReadShrink("yazma-sonrasi");
        OpenEntries = ReadOpen("yazma-sonrasi");

        var removal = RunInstaller("-RemoveShellMenu", "-RegistryRoot", RegistryRoot);
        RemoveExitCode = removal.Code;
        RemoveOutput = removal.Output;

        KeysAfterRemoval = CountKeys();
        ExtensionKeysAfterRemoval = CountExtensionKeys();
        ShrinkEntriesAfterRemoval = ReadShrink("geri-alma-sonrasi");
        OpenEntriesAfterRemoval = ReadOpen("geri-alma-sonrasi");
    }

    public void Dispose()
    {
        if (OnWindows)
        {
            PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                $"Remove-Item -LiteralPath '{RegistryRoot}' -Recurse -Force -ErrorAction SilentlyContinue");
        }

        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    private static (int Code, string Output) PowerShell(params string[] arguments)
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

    private static (int Code, string Output) RunInstaller(params string[] arguments)
    {
        var all = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", InstallerScript };
        all.AddRange(arguments);
        return PowerShell(all.ToArray());
    }

    private int CountKeys()
    {
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $"$a = Join-Path '{RegistryRoot}' 'SystemFileAssociations'; " +
            "if (-not (Test-Path -LiteralPath $a)) { Write-Output 0; return } " +
            "$n = 0; Get-ChildItem -LiteralPath $a -Recurse | ForEach-Object { $n++ }; Write-Output $n");
        return int.Parse(run.Output.Trim());
    }

    private int CountExtensionKeys()
    {
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $"$a = Join-Path '{RegistryRoot}' 'SystemFileAssociations'; " +
            "if (-not (Test-Path -LiteralPath $a)) { Write-Output 0; return } " +
            "Write-Output @(Get-ChildItem -LiteralPath $a).Count");
        return int.Parse(run.Output.Trim());
    }

    private IReadOnlyList<string[]> ReadDump(string probe, string name, int fields)
    {
        var listing = Path.Combine(_work, $"{name}.txt");
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", probe,
            "-Root", RegistryRoot, "-Out", listing);
        if (run.Code != 0) throw new InvalidOperationException(run.Output);

        var rows = new List<string[]>();
        foreach (var line in File.ReadAllLines(listing))
        {
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (parts.Length != fields) throw new InvalidOperationException($"Beklenen {fields} alan, gelen {parts.Length}: {line}");
            rows.Add(parts);
        }
        return rows;
    }

    private IReadOnlyList<ShrinkEntry> ReadShrink(string name)
        => ReadDump(_shrinkProbe, $"kucult-{name}", 8)
            .Select(p => new ShrinkEntry(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]))
            .ToList();

    private IReadOnlyList<OpenEntry> ReadOpen(string name)
        => ReadDump(_openProbe, $"ac-{name}", 4)
            .Select(p => new OpenEntry(p[0], p[1], p[2], p[3]))
            .ToList();
}

/// <summary>
/// T169: sağ tık menüsündeki "VidShrink ile Küçült" girdisi, hızlı hedef alt menüsü ve
/// geri almanın simetrisi. Bütün kollar <see cref="ShellShrinkMenuFixture"/>'ın tek
/// koşumundan beslenir.
/// </summary>
public sealed class ShellShrinkMenuTests : IClassFixture<ShellShrinkMenuFixture>
{
    private const string TurkishOpenLabel = "Bu Videoyu VidShrink ile Aç";
    private const string TurkishShrinkLabel = "VidShrink ile Küçült";

    private readonly ShellShrinkMenuFixture _fixture;

    public ShellShrinkMenuTests(ShellShrinkMenuFixture fixture) => _fixture = fixture;

    private bool Skip => !_fixture.OnWindows;

    private static IEnumerable<T> Sorted<T>(IEnumerable<T> values) where T : IComparable<T>
        => values.OrderBy(value => value);

    private static IReadOnlyList<int> InstallerTargets()
    {
        var source = File.ReadAllText(ShellShrinkMenuFixture.InstallerScript);
        var matches = Regex.Matches(source, @"\$shellShrinkTargets\s*=\s*@\((?<body>[^)]*)\)");
        Assert.True(matches.Count == 1,
            $"Install-VidShrink.ps1 icinde $shellShrinkTargets dizisi {matches.Count} kez bulundu; tam bir kopya bekleniyor.");

        return Regex.Matches(matches[0].Groups["body"].Value, @"\d+")
            .Select(match => int.Parse(match.Value))
            .ToList();
    }

    [Fact]
    public void Installer_run_and_removal_both_succeed()
    {
        if (Skip) return;
        Assert.True(_fixture.WriteExitCode == 0, _fixture.WriteOutput);
        Assert.True(_fixture.RemoveExitCode == 0, _fixture.RemoveOutput);
    }

    [Fact]
    public void Shrink_entry_is_written_for_every_media_extension()
    {
        if (Skip) return;
        var extensions = _fixture.ShrinkEntries.Select(e => e.Extension.TrimStart('.')).Distinct();
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(extensions));
    }

    [Fact]
    public void Open_entry_survives_the_second_entry_untouched()
    {
        if (Skip) return;
        var open = _fixture.OpenEntries;
        Assert.Equal(ShellIntegration.MediaExtensions.Count, open.Count);
        Assert.All(open, entry =>
        {
            Assert.Equal(TurkishOpenLabel, entry.Label);
            Assert.Equal(_fixture.Executable, entry.Icon);
            Assert.Equal($"\"{_fixture.Executable}\" \"%1\"", entry.Command);
        });
    }

    [Fact]
    public void Two_extension_dump_shows_the_parent_verb_and_its_subcommands()
    {
        if (Skip) return;
        foreach (var extension in new[] { "mp4", "mkv" })
        {
            var rows = _fixture.ShrinkEntries
                .Where(e => e.Extension.TrimStart('.') == extension)
                .ToList();

            Assert.Equal(ShellIntegration.QuickShrinkTargetsMegabytes.Count, rows.Count);
            Assert.All(rows, row => Assert.Equal(TurkishShrinkLabel, row.ParentLabel));
            Assert.All(rows, row => Assert.Equal(string.Empty, row.SubCommands));
        }
    }

    [Fact]
    public void Installer_target_list_is_the_application_target_list()
    {
        Assert.Equal(
            Sorted(ShellIntegration.QuickShrinkTargetsMegabytes),
            Sorted(InstallerTargets()));
    }

    [Fact]
    public void Written_targets_are_the_application_target_list()
    {
        if (Skip) return;
        var written = _fixture.ShrinkEntries.Select(e => int.Parse(e.Target)).Distinct();
        Assert.Equal(Sorted(ShellIntegration.QuickShrinkTargetsMegabytes), Sorted(written));
    }

    [Fact]
    public void Every_target_calls_the_launcher_with_its_size_and_the_path()
    {
        if (Skip) return;
        Assert.NotEmpty(_fixture.ShrinkEntries);
        Assert.All(_fixture.ShrinkEntries, entry => Assert.Equal(
            $"\"{_fixture.Executable}\" {ShellIntegration.ShrinkFlag} {entry.Target} \"%1\"",
            entry.Command));
    }

    [Fact]
    public void Target_labels_come_from_the_shared_formatter()
    {
        if (Skip) return;
        Assert.All(_fixture.ShrinkEntries, entry => Assert.Equal(
            ShellIntegration.FormatQuickShrinkLabel(int.Parse(entry.Target)),
            entry.TargetLabel));
    }

    [Fact]
    public void Both_menu_levels_declare_the_single_process_multi_select_model()
    {
        if (Skip) return;
        Assert.NotEmpty(_fixture.ShrinkEntries);
        Assert.All(_fixture.ShrinkEntries, entry =>
        {
            Assert.Equal("Player", entry.ParentMultiSelectModel);
            Assert.Equal("Player", entry.MultiSelectModel);
        });
    }

    [Fact]
    public void Key_count_goes_from_zero_up_and_back_to_zero()
    {
        if (Skip) return;
        Assert.Equal(0, _fixture.KeysBeforeWrite);
        Assert.True(_fixture.KeysAfterWrite > 0, "Yazma sonrası hiç anahtar sayılmadı.");
        Assert.Equal(0, _fixture.KeysAfterRemoval);
    }

    [Fact]
    public void Removal_erases_both_entries_and_the_emptied_parents()
    {
        if (Skip) return;
        Assert.NotEmpty(_fixture.ShrinkEntries);
        Assert.NotEmpty(_fixture.OpenEntries);
        Assert.Empty(_fixture.ShrinkEntriesAfterRemoval);
        Assert.Empty(_fixture.OpenEntriesAfterRemoval);
        Assert.Equal(ShellIntegration.MediaExtensions.Count, _fixture.ExtensionKeysAfterWrite);
        Assert.Equal(0, _fixture.ExtensionKeysAfterRemoval);
    }

    [Fact]
    public void Measurement_root_is_never_the_real_shell_root()
    {
        Assert.StartsWith(@"HKCU:\Software\VidShrinkKucult-Test-", _fixture.RegistryRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes", _fixture.RegistryRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_still_defaults_to_the_real_shell_root()
    {
        var source = File.ReadAllText(ShellShrinkMenuFixture.InstallerScript);
        Assert.Contains(@"[string]$RegistryRoot = 'HKCU:\Software\Classes'", source, StringComparison.Ordinal);
    }
}
