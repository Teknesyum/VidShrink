using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

[SupportedOSPlatform("windows")]
public sealed class KurucuExeTests : IDisposable
{
    private const string Rid = "win-x64";

    private static readonly string InstallerScript = Path.Combine(TipSources.Root, "Install-VidShrink.ps1");

    private readonly string _work;
    private readonly string _testKey;

    public KurucuExeTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TestPaths.OutputRoot, "kurucu-exe", id);
        _testKey = $@"Software\VidShrink-Test-{Environment.ProcessId}-{id}";
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Registry.CurrentUser.DeleteSubKeyTree(_testKey, false);
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    [Fact]
    public void BetikleAyniAnahtarlariYaziyorVeAyniSekildeSiliyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var install = Path.Combine(_work, "kurulum");
        Directory.CreateDirectory(install);
        var launcher = Path.Combine(install, "VidShrink.exe");
        File.WriteAllText(launcher, "baslatici");

        var scriptRoot = $@"{_testKey}\Betik\Classes";
        var engineRoot = $@"{_testKey}\Motor\Classes";

        var written = Script("-ShellMenuOnly", "-MenuLanguage", "tr", "-InstallRoot", install, "-RegistryRoot", $@"HKCU:\{scriptRoot}");
        Assert.True(written.Code == 0, written.Output);

        Assert.Equal(120, ShellRegistration.WriteMenus(engineRoot, launcher, "tr"));
        Assert.Equal(24, ShellRegistration.WriteFileAssociation(engineRoot, launcher));

        var scriptTree = Dump(scriptRoot);
        Assert.True(scriptTree.Count > 700, scriptTree.Count.ToString());
        Assert.Equal(scriptTree, Dump(engineRoot));
        var appCommand = $"\"{Path.Combine(install, "app", "VidShrink.App.exe")}\" \"%1\"";
        Assert.Equal(appCommand, Value($@"{scriptRoot}\Teknesyum.VidShrink.Video\shell\open\command"));
        Assert.Equal(appCommand, Value($@"{engineRoot}\Applications\VidShrink.exe\shell\open\command"));

        var removed = Script("-RemoveShellMenu", "-RemoveFileAssociation", "-RegistryRoot", $@"HKCU:\{scriptRoot}");
        Assert.True(removed.Code == 0, removed.Output);
        Assert.Equal(24, ShellRegistration.RemoveMenus(engineRoot));
        Assert.Equal(24, ShellRegistration.RemoveFileAssociation(engineRoot));

        Assert.Equal(Dump(scriptRoot), Dump(engineRoot));
        Assert.DoesNotContain(Dump(engineRoot), line => line.Contains("VidShrink", StringComparison.Ordinal));
    }

    [Fact]
    public void TestKonagiGercekKokeYazamaz()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.False(ShellRegistration.WriteAllowed(Environment.ProcessPath, @"Software\Classes"));
        Assert.True(ShellRegistration.WriteAllowed(@"C:\x\VidShrink-Setup.exe", @"Software\Classes\"));
        Assert.True(ShellRegistration.WriteAllowed(Environment.ProcessPath, $@"{_testKey}\Classes"));
        Assert.False(ShellRegistration.WriteAllowed(@"C:\x\VidShrink.exe", @"Software\Classes"));

        Assert.Throws<SetupException>(() => ShellRegistration.WriteMenus(@"Software\Classes", @"C:\x\VidShrink.exe", "tr"));
        Assert.Throws<SetupException>(() => ShellRegistration.WriteFileAssociation(@"Software\Classes", @"C:\x\VidShrink.exe"));
        Assert.Throws<SetupException>(() => ShellRegistration.RemoveMenus(@"Software\Classes"));
        Assert.Throws<SetupException>(() => ShellRegistration.RemoveFileAssociation(@"Software\Classes"));
    }

    [Fact]
    public async Task KilitKisaSurerseArtanBeklemeyleYenidenDeniyor()
    {
        var delays = new List<int>();
        var calls = 0;
        var host = new SetupHost { Delay = (span, _) => { delays.Add((int)span.TotalMilliseconds); return Task.CompletedTask; } };

        await LockedFolder.RunAsync(_work, _ =>
        {
            if (++calls < 3) throw new IOException("kilitli");
        }, host, CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal(new[] { 200, 400 }, delays);
    }

    [Fact]
    public async Task KilitHicAcilmazsaAltiDenemedeDuruyor()
    {
        var delays = new List<int>();
        var calls = 0;
        var host = new SetupHost { Delay = (span, _) => { delays.Add((int)span.TotalMilliseconds); return Task.CompletedTask; } };

        var failure = await Assert.ThrowsAsync<SetupException>(() => LockedFolder.RunAsync(_work, _ =>
        {
            calls++;
            throw new UnauthorizedAccessException("erisim yok");
        }, host, CancellationToken.None));

        Assert.Equal(6, calls);
        Assert.Equal(new[] { 200, 400, 800, 1600, 3200 }, delays);
        Assert.Equal(SetupText.Get("setup.lock.failed", 6, 6200, _work, "erisim yok"), failure.Message);
    }

    [Fact]
    public async Task IkiTurTutanVarsaKapatilipYenidenDeneniyor()
    {
        var holder = new FakeHolder();
        var calls = 0;
        var host = new SetupHost
        {
            Delay = (_, _) => Task.CompletedTask,
            FindHolders = _ => holder.Exited ? Array.Empty<IRootHolder>() : new IRootHolder[] { holder }
        };

        await LockedFolder.RunAsync(_work, _ =>
        {
            calls++;
            if (!holder.Exited) throw new IOException("VidShrink.App.exe acik");
        }, host, CancellationToken.None);

        Assert.Equal(1, holder.Kills);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task TutanKapanmazsaBeklemeSonundaDuruyor()
    {
        var holder = new FakeHolder { Stubborn = true };
        var host = new SetupHost
        {
            Delay = (_, _) => Task.CompletedTask,
            FindHolders = _ => new IRootHolder[] { holder },
            HolderWait = TimeSpan.FromSeconds(7)
        };

        var failure = await Assert.ThrowsAsync<SetupException>(() =>
            LockedFolder.RunAsync(_work, _ => throw new IOException("acik"), host, CancellationToken.None));

        Assert.Equal(1, holder.Kills);
        Assert.Equal(TimeSpan.FromSeconds(7), holder.Waited);
        Assert.Equal(SetupText.Get("setup.lock.still-open", 7, $"{holder.Name} (PID {holder.Id})", _work), failure.Message);
    }

    [Fact]
    public async Task CevrimdisiKurulumBetiginDuzeniniKuruyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var release = FakeRelease();
        var (options, host, shortcuts) = Setup(release);
        var old = Path.Combine(options.InstallRoot, "eski.txt");
        Directory.CreateDirectory(options.InstallRoot);
        File.WriteAllText(old, "eski surum");

        var result = await SetupRunner.InstallAsync(options, host, CancellationToken.None);

        var root = options.InstallRoot;
        Assert.Equal("1.2.3", result.Version);
        Assert.False(File.Exists(old));
        Assert.Equal("baslatici", File.ReadAllText(Path.Combine(root, "VidShrink.exe")));
        Assert.Equal("uygulama", File.ReadAllText(Path.Combine(root, "app", "VidShrink.App.exe")));
        Assert.Equal("veri", File.ReadAllText(Path.Combine(root, "app", "alt", "veri.txt")));
        Assert.Equal("1.2.3", File.ReadAllText(Path.Combine(root, "app", ".update-version")));
        Assert.Equal("1.2.3", File.ReadAllText(Path.Combine(root, ".launcher-version")));
        Assert.Equal("ffmpeg ikilisi", File.ReadAllText(Path.Combine(root, "tools", "ffmpeg", "ffmpeg.exe")));
        Assert.Equal("ffprobe ikilisi", File.ReadAllText(Path.Combine(root, "tools", "ffmpeg", "ffprobe.exe")));
        Assert.Equal("libmpv ikilisi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", "libmpv-2.dll")));
        Assert.True(result.FfmpegDownloaded);
        Assert.False(result.LibMpvReused);
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(root)!, "VidShrink.eski-*"));

        var exe = Path.Combine(root, "VidShrink.exe");
        Assert.Equal(new[] { exe, exe }, shortcuts.Targets.Values);
        Assert.Equal($"\"{exe}\" --kucult 100 \"%1\"", Value($@"{options.ClassesRoot}\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\100\command"));
        Assert.Equal($"\"{Path.Combine(root, "app", "VidShrink.App.exe")}\" \"%1\"", Value($@"{options.ClassesRoot}\Teknesyum.VidShrink.Video\shell\open\command"));
        Assert.Equal($"{exe},0", Value($@"{options.ClassesRoot}\Teknesyum.VidShrink.Video\DefaultIcon"));

        var again = await SetupRunner.InstallAsync(options, host, CancellationToken.None);
        Assert.True(again.LibMpvReused);
        Assert.Equal("libmpv ikilisi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", "libmpv-2.dll")));

        await SetupRunner.UninstallAsync(options, host, CancellationToken.None);
        Assert.False(Directory.Exists(root));
        Assert.DoesNotContain(shortcuts.Targets.Keys, File.Exists);
        Assert.Null(Value($@"{options.ClassesRoot}\SystemFileAssociations\.mp4\shell\VidShrink\command"));
        Assert.Null(Value($@"{options.ClassesRoot}\Teknesyum.VidShrink.Video\shell\open\command"));
    }

    [Fact]
    public async Task SaglamaTutmazsaEskiKurulumaDokunulmuyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var release = FakeRelease();
        var checksums = Path.Combine(release, $"checksums-{Rid}.txt");
        var tampered = File.ReadAllText(checksums).Replace(UpdateCheck.HashFile(Path.Combine(release, UpdateCheck.ArchiveAssetName(Rid))), new string('0', 64));
        File.WriteAllText(checksums, tampered);

        var (options, host, _) = Setup(release);
        var old = Path.Combine(options.InstallRoot, "VidShrink.exe");
        Directory.CreateDirectory(options.InstallRoot);
        File.WriteAllText(old, "eski baslatici");

        var failure = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, host, CancellationToken.None));

        Assert.StartsWith(Prefix("setup.checksum.mismatch"), failure.Message, StringComparison.Ordinal);
        Assert.Equal("eski baslatici", File.ReadAllText(old));
        Assert.Null(Value($@"{options.ClassesRoot}\SystemFileAssociations\.mp4\shell\VidShrink\command"));
    }

    [Fact]
    public async Task FfmpegSaglamasiTutmazsaDuruyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var release = FakeRelease();
        var (options, host, _) = Setup(release);
        var ffmpeg = options.Ffmpeg!;
        var entries = ffmpeg.Entries.Select(e => e.FileName == "ffprobe.exe" ? e with { Sha256 = new string('1', 64) } : e).ToArray();
        options = options with { Ffmpeg = ffmpeg with { Entries = entries } };
        Directory.CreateDirectory(options.InstallRoot);
        File.WriteAllText(Path.Combine(options.InstallRoot, "eski.txt"), "eski");

        var failure = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, host, CancellationToken.None));

        Assert.StartsWith(Prefix("setup.checksum.mismatch"), failure.Message, StringComparison.Ordinal);
        Assert.Contains("ffprobe.exe", failure.Message, StringComparison.Ordinal);
        Assert.Equal("eski", File.ReadAllText(Path.Combine(options.InstallRoot, "eski.txt")));
    }

    [Fact]
    public async Task YarimKalanKurulumEskiKlasoruGeriKoyuyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var release = FakeRelease(launcherExecutable: "Baska.exe");
        var (options, host, _) = Setup(release);
        Directory.CreateDirectory(options.InstallRoot);
        File.WriteAllText(Path.Combine(options.InstallRoot, "VidShrink.exe"), "eski baslatici");

        var failure = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, host, CancellationToken.None));

        Assert.Equal(SetupText.Get("setup.launcher.missing"), failure.Message);
        Assert.Equal("eski baslatici", File.ReadAllText(Path.Combine(options.InstallRoot, "VidShrink.exe")));
        Assert.False(File.Exists(Path.Combine(options.InstallRoot, "Baska.exe")));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(options.InstallRoot)!, "VidShrink.eski-*"));
    }

    [Fact]
    public async Task ProgramsDisinaKurulmuyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (options, host, _) = Setup(FakeRelease());
        options = options with { InstallRoot = Path.Combine(_work, "local", "Programs") };

        var failure = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, host, CancellationToken.None));
        Assert.Equal(SetupText.Get("setup.root.outside-programs", options.InstallRoot), failure.Message);
    }

    [Fact]
    public void SabitlerBetikleAyni()
    {
        var script = File.ReadAllText(InstallerScript);
        var pin = LibMpvPin.Default;

        foreach (var url in pin.Urls) Assert.Contains($"'{url}'", script);
        Assert.Contains($"$libMpvArchiveSha256 = '{pin.ArchiveSha256.ToUpperInvariant()}'", script);
        Assert.Contains($"$libMpvDllSha256 = '{pin.DllSha256.ToUpperInvariant()}'", script);
        Assert.Contains($"$libMpvFileName = '{pin.FileName}'", script);
        Assert.Contains($"$script:RemoveAttempts = {LockedFolder.Attempts}", script);
        Assert.Contains($"$script:RemoveFirstDelayMilliseconds = {LockedFolder.FirstDelayMilliseconds}", script);
        Assert.Contains($"$script:RemoveHolderWaitSeconds = {(int)LockedFolder.HolderWait.TotalSeconds}", script);
        Assert.Contains($"'{ShellRegistration.CommandClsid}'", script);
        Assert.Contains($"'{ShellRegistration.PackageName}'", script);
    }

    /// <summary>
    /// A4: arm64 pinleri iki yerde yaşıyor — Core ve betik. Ayrı düşerlerse arm64
    /// makinede biri x64 dosyasını, öteki aarch64 dosyasını indirir ve sağlama tutmaz.
    /// </summary>
    [Fact]
    public void Arm64SabitleriBetikleAyni()
    {
        var script = File.ReadAllText(InstallerScript);
        var libMpv = LibMpvPin.Arm64;
        var ffmpeg = FfmpegPin.Arm64;

        Assert.Contains($"$libMpvArm64Url = '{libMpv.Urls[0]}'", script);
        Assert.Contains($"$libMpvArm64FallbackUrl = '{libMpv.Urls[1]}'", script);
        Assert.Contains($"$libMpvArm64ArchiveSha256 = '{libMpv.ArchiveSha256.ToUpperInvariant()}'", script);
        Assert.Contains($"$libMpvArm64DllSha256 = '{libMpv.DllSha256.ToUpperInvariant()}'", script);
        Assert.Contains($"$ffmpegArm64Url = '{ffmpeg.Url}'", script);
        foreach (var entry in ffmpeg.Entries)
        {
            Assert.Contains($"Entry = '{entry.EntryPath}'", script);
            Assert.Contains($"Sha256 = '{entry.Sha256.ToUpperInvariant()}'", script);
        }
    }

    [Fact]
    public void EtiketVeSaglamaListesiOkunuyor()
    {
        Assert.Equal("v0.8.2", SetupDownloads.TagFromLocation(new Uri("https://github.com/Teknesyum/VidShrink/releases/tag/v0.8.2")));
        Assert.Throws<SetupException>(() => SetupDownloads.TagFromLocation(new Uri("https://github.com/Teknesyum/VidShrink/releases")));

        var hash = new string('a', 64);
        var table = SetupDownloads.ParseChecksums($"{hash.ToUpperInvariant()}  vidshrink-win-x64.zip\r\n{new string('b', 64)} *VidShrink-Setup.exe\nbozuk satir\n");
        Assert.Equal(2, table.Count);
        Assert.Equal(hash, table["vidshrink-win-x64.zip"]);
        Assert.Equal(new string('b', 64), table["VidShrink-Setup.exe"]);

        SetupDownloads.AssertChecksum(table, "vidshrink-win-x64.zip", hash.ToUpperInvariant());
        Assert.Equal(SetupText.Get("setup.checksum.missing", "yok.zip"), Assert.Throws<SetupException>(() => SetupDownloads.AssertChecksum(table, "yok.zip", hash)).Message);
        Assert.Equal(SetupText.Get("setup.checksum.mismatch", "VidShrink-Setup.exe", new string('b', 64), hash), Assert.Throws<SetupException>(() => SetupDownloads.AssertChecksum(table, "VidShrink-Setup.exe", hash)).Message);

        Assert.Equal(@"Software\Classes", SetupOptions.NormalizeRegistryRoot(@"HKCU:\Software\Classes\"));
        Assert.Throws<SetupException>(() => SetupOptions.NormalizeRegistryRoot(@"HKLM:\Software\Classes"));
    }

    [Fact]
    public async Task LibmpvBirincilKaynakDusunceYedektenKurulur()
    {
        if (!OperatingSystem.IsWindows()) return;

        var release = Path.Combine(_work, "libmpv-kaynak");
        Directory.CreateDirectory(release);
        var good = Path.Combine(release, "mpv-dev.zip");
        Zip(good, ("libmpv-2.dll", "libmpv ikilisi"));
        var bad = Path.Combine(release, "bozuk.zip");
        Zip(bad, ("libmpv-2.dll", "baska ikili"));
        var missing = Path.Combine(release, "yok.zip");
        var archiveSha = UpdateCheck.HashFile(good);
        var dllSha = Sha("libmpv ikilisi");

        async Task<(string Dll, List<string> Log)> Prepare(string name, params string[] urls)
        {
            var work = Path.Combine(_work, "libmpv-" + name);
            Directory.CreateDirectory(work);
            var log = new List<string>();
            var result = await SetupDownloads.PrepareLibMpvAsync(new HttpClient(), new LibMpvPin(urls, archiveSha, "libmpv-2.dll", dllSha), null, work, log.Add, CancellationToken.None);
            return (result.Path, log);
        }

        var primary = await Prepare("birincil", good, missing);
        Assert.Empty(primary.Log);
        Assert.Equal(dllSha, UpdateCheck.HashFile(primary.Dll));

        var afterMissing = await Prepare("yok", missing, good);
        Assert.Equal(new[] { SetupText.Get("setup.libmpv.source-failed", missing) }, afterMissing.Log);
        Assert.Equal(dllSha, UpdateCheck.HashFile(afterMissing.Dll));

        var afterBad = await Prepare("bozuk", bad, good);
        Assert.Equal(new[] { SetupText.Get("setup.libmpv.checksum-fallback", bad) }, afterBad.Log);
        Assert.Equal(dllSha, UpdateCheck.HashFile(afterBad.Dll));

        var none = await Assert.ThrowsAsync<SetupException>(() => Prepare("hicbiri", missing, bad));
        Assert.StartsWith(Prefix("setup.libmpv.archive-mismatch"), none.Message, StringComparison.Ordinal);
    }

    private (SetupOptions Options, SetupHost Host, FakeShortcuts Shortcuts) Setup(string release)
    {
        var local = Path.Combine(_work, "local");
        var ffmpegZip = Path.Combine(release, "ffmpeg.zip");
        var libMpvZip = Path.Combine(release, "mpv-dev.zip");
        var shortcuts = new FakeShortcuts();
        var options = new SetupOptions
        {
            InstallRoot = Path.Combine(local, "Programs", "VidShrink"),
            LocalAppData = local,
            WorkRoot = Path.Combine(_work, "tmp"),
            ClassesRoot = $@"{_testKey}\Classes",
            NoLaunch = true,
            MenuLanguage = "tr",
            ShortcutDirectory = Path.Combine(_work, "kisayol"),
            AssetSource = release,
            Tag = "v1.2.3",
            ForceFfmpegDownload = true,
            LibMpv = new LibMpvPin(new[] { libMpvZip }, UpdateCheck.HashFile(libMpvZip), "libmpv-2.dll", Sha("libmpv ikilisi")),
            Ffmpeg = new FfmpegPin(ffmpegZip, new[]
            {
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg.exe", Sha("ffmpeg ikilisi")),
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe.exe", Sha("ffprobe ikilisi"))
            })
        };
        Directory.CreateDirectory(options.WorkRoot);
        var host = new SetupHost
        {
            Architecture = () => new ArchitectureDecision(ArchitectureOutcome.Read, "x64", ""),
            Shortcuts = shortcuts,
            Delay = (_, _) => Task.CompletedTask,
            Launch = _ => throw new InvalidOperationException("NoLaunch verildi.")
        };
        return (options, host, shortcuts);
    }

    private string FakeRelease(string launcherExecutable = "VidShrink.exe")
    {
        var release = Path.Combine(_work, "yayin-" + Guid.NewGuid().ToString("n")[..6]);
        Directory.CreateDirectory(release);

        var app = Path.Combine(release, UpdateCheck.ArchiveAssetName(Rid));
        Zip(app, ("VidShrink.App.exe", "uygulama"), ("alt/veri.txt", "veri"));
        var launcher = Path.Combine(release, UpdateCheck.LauncherArchiveAssetName(Rid));
        Zip(launcher, (launcherExecutable, "baslatici"));
        File.WriteAllText(Path.Combine(release, $"checksums-{Rid}.txt"),
            $"{UpdateCheck.HashFile(app)}  {Path.GetFileName(app)}\n{UpdateCheck.HashFile(launcher)}  {Path.GetFileName(launcher)}\n");

        Zip(Path.Combine(release, "ffmpeg.zip"),
            ("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg ikilisi"),
            ("ffmpeg-9.0-full_build/bin/ffplay.exe", "ffplay ikilisi"),
            ("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe ikilisi"));
        Zip(Path.Combine(release, "mpv-dev.zip"), ("libmpv-2.dll", "libmpv ikilisi"), ("include/client.h", "baslik"));
        return release;
    }

    private static void Zip(string path, params (string Name, string Text)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    private static string Sha(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string? Value(string subKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        return key?.GetValue("") as string;
    }

    private static List<string> Dump(string root)
    {
        var lines = new List<string>();
        void Walk(string path, string relative)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            if (key is null) return;
            lines.Add($"[{relative}]");
            foreach (var name in key.GetValueNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                var kind = key.GetValueKind(name);
                var data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var text = (data is byte[] bytes ? Convert.ToHexString(bytes) : data?.ToString())?.Replace(root, "<kok>", StringComparison.OrdinalIgnoreCase);
                lines.Add($"{relative}|{name}|{kind}|{text}");
            }
            foreach (var child in key.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal))
                Walk($@"{path}\{child}", $@"{relative}\{child}");
        }
        Walk(root, "");
        return lines;
    }

    private static (int Code, string Output) Script(params string[] arguments)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = TipSources.Root
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", InstallerScript }.Concat(arguments))
            info.ArgumentList.Add(argument);
        info.Environment.Remove("PSModulePath");
        info.Environment.Remove("VIDSHRINK_INSTALL_ROOT");

        using var process = Process.Start(info) ?? throw new InvalidOperationException("powershell.exe başlatılamadı.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error.Result);
    }

    /// <summary>
    /// Cümlenin ilk yer tutucuya kadarki değişmez başı. Hangi anahtarın atıldığını
    /// dilden bağımsız pimliyor; cümlenin tamamı değişkenle dolduğunda karşılaştırılamıyor.
    /// </summary>
    private static string Prefix(string key)
    {
        var text = SetupText.Get(key);
        var brace = text.IndexOf('{', StringComparison.Ordinal);
        return brace < 0 ? text : text[..brace];
    }

    private sealed class FakeHolder : IRootHolder
    {
        public bool Stubborn { get; init; }

        public bool Exited { get; private set; }

        public int Kills { get; private set; }

        public TimeSpan Waited { get; private set; }

        public string Name => "VidShrink.App";

        public int Id => 4242;

        public void Kill()
        {
            Kills++;
            if (!Stubborn) Exited = true;
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            Waited = timeout;
            return Exited;
        }
    }

    private sealed class FakeShortcuts : IShortcutWriter
    {
        public Dictionary<string, string> Targets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Write(string shortcutPath, string target, string workingDirectory, string icon)
        {
            File.WriteAllText(shortcutPath, target);
            Targets[shortcutPath] = target;
        }

        public string? ReadTarget(string shortcutPath) => File.ReadAllText(shortcutPath);
    }
}
