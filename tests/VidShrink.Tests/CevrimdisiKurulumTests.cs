using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

[SupportedOSPlatform("windows")]
public sealed class CevrimdisiKurulumTests : IDisposable
{
    private const string Rid = "win-x64";

    private readonly string _work;

    public CevrimdisiKurulumTests()
    {
        _work = Path.Combine(TestPaths.OutputRoot, "cevrimdisi-kurulum", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    [Fact]
    public void IsaretDosyasiEtiketiVeHedefiOkuyor()
    {
        var bundle = Path.Combine(_work, "arsiv");
        Directory.CreateDirectory(bundle);
        Assert.Null(OfflineBundle.Read(bundle));

        File.WriteAllText(Path.Combine(bundle, OfflineBundle.MarkerFileName), "tag=v1.2.3\nrid=win-arm64\n");
        var read = OfflineBundle.Read(bundle)!;
        Assert.Equal("v1.2.3", read.Tag);
        Assert.Equal("win-arm64", read.Rid);
        Assert.Equal(Path.Combine(Path.GetFullPath(bundle), "tools"), read.ToolsDirectory);

        var applied = read.Apply(new SetupOptions { InstallRoot = "k", LocalAppData = "l", WorkRoot = "w" });
        Assert.Equal(Path.GetFullPath(bundle), applied.AssetSource);
        Assert.Equal("v1.2.3", applied.Tag);
        Assert.Equal(read.ToolsDirectory, applied.OfflineTools);
        Assert.Equal("win-arm64", applied.OfflineRid);

        var explicitTag = read.Apply(new SetupOptions { InstallRoot = "k", LocalAppData = "l", WorkRoot = "w", Tag = "v9.9.9" });
        Assert.Equal("v9.9.9", explicitTag.Tag);
        Assert.Null(explicitTag.OfflineTools);

        File.WriteAllText(Path.Combine(bundle, OfflineBundle.MarkerFileName), "tag=v1.2.3\n");
        Assert.Throws<SetupException>(() => OfflineBundle.Read(bundle));
    }

    [Fact]
    public async Task AraclarArsivdenKuruluyorIndirmeDenenmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var bundle = Bundle();
        var log = new List<string>();
        var options = Options(bundle, Path.Combine(_work, "yok"));

        var result = await SetupRunner.InstallAsync(options, Host(log), CancellationToken.None);

        var root = options.InstallRoot;
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal("ffmpeg ikilisi", File.ReadAllText(Path.Combine(root, "tools", "ffmpeg", "ffmpeg.exe")));
        Assert.Equal("ffprobe ikilisi", File.ReadAllText(Path.Combine(root, "tools", "ffmpeg", "ffprobe.exe")));
        Assert.Equal("libmpv ikilisi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", "libmpv-2.dll")));
        Assert.Equal("vulkan ikilisi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", VulkanLoaderPin.FileName)));
        Assert.Equal("vulkan lisansi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", VulkanLoaderPin.LicenseFileName)));
        Assert.Equal("uygulama", File.ReadAllText(Path.Combine(root, SetupRunner.AppFolder, AppIdentity.AppExecutable)));
        Assert.Contains(log, line => line.Contains(options.OfflineTools!, StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.Contains("indirmeye", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(bundle, "tools", "libmpv", "libmpv-2.dll")));
        Assert.True(File.Exists(Path.Combine(bundle, "tools", "ffmpeg", "ffmpeg.exe")));
    }

    [Fact]
    public async Task SaglamasiTutmayanAracIndirmeyeDusuyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var bundle = Bundle();
        File.WriteAllText(Path.Combine(bundle, "tools", "libmpv", "libmpv-2.dll"), "bozuk");
        File.Delete(Path.Combine(bundle, "tools", "ffmpeg", "ffprobe.exe"));
        var log = new List<string>();
        var options = Options(bundle, Sources());

        await SetupRunner.InstallAsync(options, Host(log), CancellationToken.None);

        var root = options.InstallRoot;
        Assert.Equal("libmpv ikilisi", File.ReadAllText(Path.Combine(root, "tools", "libmpv", "libmpv-2.dll")));
        Assert.Equal("ffprobe ikilisi", File.ReadAllText(Path.Combine(root, "tools", "ffmpeg", "ffprobe.exe")));
        Assert.Contains(log, line => line.Contains("libmpv-2.dll", StringComparison.Ordinal) && line.Contains(Sha("bozuk"), StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("ffprobe.exe", StringComparison.Ordinal) && line.Contains("indirmeye", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaglamaTutmazVeKaynakYoksaKurulumAcikHatayiVeriyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var bundle = Bundle();
        File.WriteAllText(Path.Combine(bundle, "tools", "libmpv", "libmpv-2.dll"), "bozuk");
        var options = Options(bundle, Path.Combine(_work, "yok"));

        var error = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, Host(new List<string>()), CancellationToken.None));
        Assert.Contains("libmpv", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(options.InstallRoot, SetupRunner.AppFolder, AppIdentity.AppExecutable)));
    }

    [Fact]
    public async Task BaskaMimarininArsiviAcikHatayiVeriyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var options = Options(Bundle(), Path.Combine(_work, "yok")) with { OfflineRid = "win-arm64" };

        var error = await Assert.ThrowsAsync<SetupException>(() => SetupRunner.InstallAsync(options, Host(new List<string>()), CancellationToken.None));
        Assert.Contains(OfflineBundle.ArchiveName(Rid), error.Message, StringComparison.Ordinal);
        Assert.Contains("win-arm64", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void YayinArsiviKurucununPinleriyleAyniDosyalariTasiyor()
    {
        var yayin = File.ReadAllText(Path.Combine(TipSources.Root, ".github", "workflows", "release.yml"));
        var start = yayin.IndexOf("\n  offline:", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yayin[start..yayin.IndexOf("    steps:", start, StringComparison.Ordinal)];
        var rows = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (Match match in Regex.Matches(block, @"^\s+(- )?([a-z0-9-]+): (\S+)\r?$", RegexOptions.Multiline))
        {
            if (match.Groups[1].Success) rows[match.Groups[3].Value] = current = new Dictionary<string, string>();
            current?.Add(match.Groups[2].Value, match.Groups[3].Value);
        }

        Assert.Equal(new[] { "win-x64", "win-arm64" }, rows.Keys);
        foreach (var (rid, ffmpeg, libmpv) in new[] { ("win-x64", FfmpegPin.X64, LibMpvPin.X64), ("win-arm64", FfmpegPin.Arm64, LibMpvPin.Arm64) })
        {
            var row = rows[rid];
            Assert.Equal(ffmpeg.Url, row["ffmpeg-url"]);
            Assert.Equal(ffmpeg.Entries.Single(e => e.FileName == "ffmpeg.exe").Sha256, row["ffmpeg-sha256"]);
            Assert.Equal(ffmpeg.Entries.Single(e => e.FileName == "ffprobe.exe").Sha256, row["ffprobe-sha256"]);
            Assert.All(ffmpeg.Entries, e => Assert.Equal($"{row["ffmpeg-dir"]}/bin/{e.FileName}", e.EntryPath));
            Assert.Equal(libmpv.ZipUrl, row["libmpv-url"]);
            Assert.Equal(libmpv.ZipSha256, row["libmpv-zip-sha256"]);
            Assert.Equal(libmpv.DllSha256, row["libmpv-sha256"]);
            Assert.Equal(libmpv.Vulkan!.Url, row["vulkan-url"]);
            Assert.Equal(libmpv.Vulkan.ZipSha256, row["vulkan-zip-sha256"]);
            Assert.Equal(libmpv.Vulkan.DllSha256, row["vulkan-sha256"]);
        }

        Assert.Contains(OfflineBundle.MarkerFileName, yayin, StringComparison.Ordinal);
        Assert.Contains("VidShrinkPlayer-Offline-$RID.zip", yayin, StringComparison.Ordinal);
        Assert.Contains("licenses/ffmpeg-LICENSE.txt", yayin, StringComparison.Ordinal);
        Assert.Contains("licenses/libmpv-LICENSE.GPL.txt", yayin, StringComparison.Ordinal);
        Assert.Contains("needs: [version, publish, offline]", yayin, StringComparison.Ordinal);
        Assert.Equal("VidShrinkPlayer-Offline-win-x64.zip", OfflineBundle.ArchiveName("win-x64"));

        foreach (var belge in new[] { "README.md", "README.tr.md" })
            Assert.Contains(OfflineBundle.ArchiveName("win-x64"), File.ReadAllText(Path.Combine(TipSources.Root, belge)), StringComparison.Ordinal);
    }

    private string Bundle()
    {
        var bundle = Path.Combine(_work, "arsiv-" + Guid.NewGuid().ToString("n")[..6]);
        Directory.CreateDirectory(Path.Combine(bundle, "tools", "ffmpeg"));
        Directory.CreateDirectory(Path.Combine(bundle, "tools", "libmpv"));
        var app = Path.Combine(bundle, UpdateCheck.ArchiveAssetName(Rid));
        Zip(app, (AppIdentity.AppExecutable, "uygulama"));
        var launcher = Path.Combine(bundle, UpdateCheck.LauncherArchiveAssetName(Rid));
        Zip(launcher, (AppIdentity.LauncherExecutable, "baslatici"));
        File.WriteAllText(Path.Combine(bundle, $"checksums-{Rid}.txt"),
            $"{UpdateCheck.HashFile(app)} *{Path.GetFileName(app)}\n{UpdateCheck.HashFile(launcher)}  {Path.GetFileName(launcher)}\n");
        File.WriteAllText(Path.Combine(bundle, OfflineBundle.MarkerFileName), $"tag=v1.2.3\nrid={Rid}\n");
        File.WriteAllText(Path.Combine(bundle, "tools", "ffmpeg", "ffmpeg.exe"), "ffmpeg ikilisi");
        File.WriteAllText(Path.Combine(bundle, "tools", "ffmpeg", "ffprobe.exe"), "ffprobe ikilisi");
        File.WriteAllText(Path.Combine(bundle, "tools", "libmpv", "libmpv-2.dll"), "libmpv ikilisi");
        File.WriteAllText(Path.Combine(bundle, "tools", "libmpv", VulkanLoaderPin.FileName), "vulkan ikilisi");
        File.WriteAllText(Path.Combine(bundle, "tools", "libmpv", VulkanLoaderPin.LicenseFileName), "vulkan lisansi");
        return bundle;
    }

    private string Sources()
    {
        var sources = Path.Combine(_work, "kaynak");
        Directory.CreateDirectory(sources);
        Zip(Path.Combine(sources, "ffmpeg.zip"),
            ("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg ikilisi"),
            ("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe ikilisi"));
        Zip(Path.Combine(sources, "libmpv.zip"), ("libmpv-2.dll", "libmpv ikilisi"));
        Zip(Path.Combine(sources, "vulkan-1.zip"), (VulkanLoaderPin.FileName, "vulkan ikilisi"));
        return sources;
    }

    private SetupOptions Options(string bundle, string sources)
    {
        var local = Path.Combine(_work, "local");
        var libMpvZip = Path.Combine(sources, "libmpv.zip");
        var vulkanZip = Path.Combine(sources, "vulkan-1.zip");
        var options = new SetupOptions
        {
            InstallRoot = Path.Combine(local, "Programs", AppIdentity.FolderName),
            LocalAppData = local,
            WorkRoot = Path.Combine(_work, "tmp"),
            ClassesRoot = $@"Software\VidShrink-Test-{Environment.ProcessId}-cevrimdisi\Classes",
            NoLaunch = true,
            SkipShortcuts = true,
            RegisterUninstall = false,
            MenuLanguage = "tr",
            ForceFfmpegDownload = true,
            LibMpv = new LibMpvPin(new[] { Path.Combine(sources, "mpv-dev.7z") }, new string('0', 64), "libmpv-2.dll", Sha("libmpv ikilisi"),
                libMpvZip, File.Exists(libMpvZip) ? UpdateCheck.HashFile(libMpvZip) : new string('0', 64),
                new VulkanLoaderPin(vulkanZip, File.Exists(vulkanZip) ? UpdateCheck.HashFile(vulkanZip) : new string('0', 64), Sha("vulkan ikilisi"))),
            Ffmpeg = new FfmpegPin(Path.Combine(sources, "ffmpeg.zip"), new[]
            {
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffmpeg.exe", "ffmpeg.exe", Sha("ffmpeg ikilisi")),
                new PinnedEntry("ffmpeg-9.0-full_build/bin/ffprobe.exe", "ffprobe.exe", Sha("ffprobe ikilisi"))
            })
        };
        Directory.CreateDirectory(options.WorkRoot);
        return OfflineBundle.Read(bundle)!.Apply(options);
    }

    private static SetupHost Host(List<string> log) => new()
    {
        Architecture = () => new ArchitectureDecision(ArchitectureOutcome.Read, "x64", ""),
        Delay = (_, _) => Task.CompletedTask,
        Log = line => { lock (log) log.Add(line); },
        Launch = _ => throw new InvalidOperationException("NoLaunch verildi."),
        EnvironmentChanged = () => throw new InvalidOperationException("Test kökünde ortam yayını yapılmaz."),
        AssociationChanged = () => throw new InvalidOperationException("Test kökünde ilişkilendirme yayını yapılmaz.")
    };

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
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
