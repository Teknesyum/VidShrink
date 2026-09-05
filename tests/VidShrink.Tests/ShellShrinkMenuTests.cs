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
/// Koşum başında, aynı desene uyan (<c>VidShrinkKucult-Test-&lt;pid&gt;-&lt;guid&gt;</c>) ve ölü
/// bir PID taşıyan artık kökler toplanır: düşen bir koşumun bıraktığı kök bir sonrakinde
/// silinir, canlı PID taşıyan kök ise dokunulmadan bırakılır.
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

    private const string ReapProbe = @"param([string]$Prefix, [string]$Out)
$removed = @()
foreach ($item in Get-ChildItem -LiteralPath 'HKCU:\Software' -ErrorAction SilentlyContinue) {
    $name = $item.PSChildName
    if (-not $name.StartsWith($Prefix)) { continue }
    $owner = ($name.Substring($Prefix.Length) -split '-')[0]
    if ($owner -notmatch '^\d+$') { continue }
    if (Get-Process -Id ([int]$owner) -ErrorAction SilentlyContinue) { continue }
    Remove-Item -LiteralPath $item.PSPath -Recurse -Force -ErrorAction SilentlyContinue
    $removed += $name
}
Set-Content -LiteralPath $Out -Value ($removed -join ""`n"") -Encoding UTF8
";

    public const string RootPrefix = "VidShrinkKucult-Test-";

    private readonly string _work;
    private readonly string _shrinkProbe;
    private readonly string _openProbe;
    private readonly string _reapProbe;

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

    public string AbandonedRootName { get; } = "";
    public int AbandonedRootKeysBeforeReap { get; }
    public bool AbandonedRootExistsAfterReap { get; }
    public string LiveRootName { get; } = "";
    public bool LiveRootExistsAfterReap { get; }
    public IReadOnlyList<string> ReapedRoots { get; } = Array.Empty<string>();

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
        _reapProbe = Path.Combine(_work, "topla.ps1");
        RegistryRoot = $@"HKCU:\Software\{RootPrefix}{Environment.ProcessId}-{id}";

        Directory.CreateDirectory(installRoot);
        File.WriteAllText(Executable, "kurulu baslatici");
        File.WriteAllText(_shrinkProbe, ShrinkProbe);
        File.WriteAllText(_openProbe, OpenProbe);
        File.WriteAllText(_reapProbe, ReapProbe);

        OnWindows = OperatingSystem.IsWindows();
        if (!OnWindows) return;

        AbandonedRootName = $"{RootPrefix}{FindDeadProcessId()}-{Guid.NewGuid():n}";
        LiveRootName = $"{RootPrefix}{Environment.ProcessId}-{Guid.NewGuid():n}";
        SeedRoot(AbandonedRootName);
        SeedRoot(LiveRootName);
        AbandonedRootKeysBeforeReap = CountRootKeys(AbandonedRootName);

        ReapedRoots = ReapAbandonedRoots();
        AbandonedRootExistsAfterReap = RootExists(AbandonedRootName);
        LiveRootExistsAfterReap = RootExists(LiveRootName);
        RemoveRoot(LiveRootName);

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
            RemoveRoot(AbandonedRootName);
            RemoveRoot(LiveRootName);
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

    private static int FindDeadProcessId()
    {
        var live = Process.GetProcesses().Select(process => process.Id).ToHashSet();
        for (var candidate = 65532; candidate > 4; candidate -= 4)
        {
            if (!live.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Olculebilir olu PID bulunamadi.");
    }

    private static void SeedRoot(string name)
        => PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $@"New-Item -Path 'HKCU:\Software\{name}\SystemFileAssociations\.mp4\shell\VidShrinkKucult' -Force | Out-Null");

    private static void RemoveRoot(string name)
    {
        if (name.Length == 0) return;
        PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $@"Remove-Item -LiteralPath 'HKCU:\Software\{name}' -Recurse -Force -ErrorAction SilentlyContinue");
    }

    private static bool RootExists(string name)
    {
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $@"Write-Output (Test-Path -LiteralPath 'HKCU:\Software\{name}')");
        return run.Output.Trim() == "True";
    }

    private static int CountRootKeys(string name)
    {
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $@"$r = 'HKCU:\Software\{name}'; " +
            "if (-not (Test-Path -LiteralPath $r)) { Write-Output 0; return } " +
            "$n = 0; Get-ChildItem -LiteralPath $r -Recurse | ForEach-Object { $n++ }; Write-Output $n");
        return int.Parse(run.Output.Trim());
    }

    private IReadOnlyList<string> ReapAbandonedRoots()
    {
        var listing = Path.Combine(_work, "toplanan-kokler.txt");
        var run = PowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", _reapProbe,
            "-Prefix", RootPrefix, "-Out", listing);
        if (run.Code != 0) throw new InvalidOperationException(run.Output);

        return File.ReadAllLines(listing).Where(line => line.Length > 0).ToList();
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
    public void Registry_writers_never_hard_code_the_real_shell_root()
    {
        var source = File.ReadAllText(ShellShrinkMenuFixture.InstallerScript);
        foreach (var name in new[] { "Write-ShellMenu", "Write-ShellShrinkMenu", "Remove-ShellMenu" })
        {
            var body = FunctionBody(source, name);
            Assert.DoesNotContain("HKCU:", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("$Root", body, StringComparison.Ordinal);
        }
    }

    private static string FunctionBody(string source, string name)
    {
        var start = source.IndexOf($"function {name}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Install-VidShrink.ps1 icinde {name} fonksiyonu bulunamadi.");

        var open = source.IndexOf('{', start);
        Assert.True(open >= 0, $"{name} govdesi acilmiyor.");

        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0)
            {
                return source[open..(index + 1)];
            }
        }
        throw new InvalidOperationException($"{name} govdesi kapanmiyor.");
    }

    [Fact]
    public void Abandoned_root_from_a_dead_process_is_reaped_at_run_start()
    {
        if (Skip) return;
        Assert.True(_fixture.AbandonedRootKeysBeforeReap > 0,
            $"Yapay artik kok kurulamadi: {_fixture.AbandonedRootName}");
        Assert.Contains(_fixture.AbandonedRootName, _fixture.ReapedRoots);
        Assert.False(_fixture.AbandonedRootExistsAfterReap,
            $"Olu PID tasiyan artik kok toplanmadi: {_fixture.AbandonedRootName}");
    }

    [Fact]
    public void Reaping_leaves_the_root_of_a_live_process_alone()
    {
        if (Skip) return;
        Assert.DoesNotContain(_fixture.LiveRootName, _fixture.ReapedRoots);
        Assert.True(_fixture.LiveRootExistsAfterReap,
            $"Canli PID tasiyan kok yanlislikla silindi: {_fixture.LiveRootName}");
    }

    [Fact]
    public void Installer_still_defaults_to_the_real_shell_root()
    {
        var source = File.ReadAllText(ShellShrinkMenuFixture.InstallerScript);
        Assert.Contains(@"[string]$RegistryRoot = 'HKCU:\Software\Classes'", source, StringComparison.Ordinal);
    }
}
