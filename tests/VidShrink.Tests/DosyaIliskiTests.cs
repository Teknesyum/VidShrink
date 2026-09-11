using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using VidShrink.App.Integration;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public sealed class DosyaIliskiTests : IDisposable
{
    private const string ForeignProgId = "Baska.Oynatici.Video";
    private const string ForeignDefault = "Baska.Oynatici.Varsayilan";

    private static readonly string InstallerScript = Path.Combine(TipSources.Root, "Install-VidShrink.ps1");

    private readonly string _work;
    private readonly string _installRoot;
    private readonly string _launcher;
    private readonly string _testKey;
    private readonly string _registryRoot;

    public DosyaIliskiTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TestPaths.OutputRoot, "dosya-iliski", id);
        _installRoot = Path.Combine(_work, "kurulum");
        _launcher = Path.Combine(_installRoot, "VidShrink.exe");
        _testKey = $@"Software\VidShrink-Test-{Environment.ProcessId}-{id}";
        _registryRoot = $@"HKCU:\{_testKey}";

        Directory.CreateDirectory(_installRoot);
        File.WriteAllText(_launcher, "kurulu baslatici");
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Registry.CurrentUser.DeleteSubKeyTree(_testKey, false);
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    private static IEnumerable<string> Sorted(IEnumerable<string> values)
        => values.OrderBy(value => value, StringComparer.Ordinal);

    private static (int Code, string Output) Run(string file, IEnumerable<string> arguments,
        string? workingDirectory = null, IDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? TipSources.Root
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (name, value) in environment) info.Environment[name] = value;
        info.Environment.Remove("VIDSHRINK_INSTALL_ROOT");

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} başlatılamadı.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error.Result);
    }

    private (int Code, string Output) Installer(params string[] arguments)
    {
        var all = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", InstallerScript };
        all.AddRange(arguments);
        return Run("powershell.exe", all);
    }

    private (int Code, string Output) WriteAll()
        => Installer("-ShellMenuOnly", "-InstallRoot", _installRoot, "-RegistryRoot", _registryRoot);

    [SupportedOSPlatform("windows")]
    private RegistryKey? Open(string relative) => Registry.CurrentUser.OpenSubKey($@"{_testKey}\{relative}");

    [SupportedOSPlatform("windows")]
    private string? Read(string relative, string name = "")
    {
        using var key = Open(relative);
        return key?.GetValue(name) as string;
    }

    [SupportedOSPlatform("windows")]
    private IReadOnlyList<string> ExtensionsListingUs()
        => ShellIntegration.MediaExtensions
            .Where(extension =>
            {
                using var key = Open($@".{extension}\OpenWithProgids");
                return key is not null
                       && key.GetValueNames().Contains(FileAssociation.ProgId)
                       && key.GetValueKind(FileAssociation.ProgId) == RegistryValueKind.None;
            })
            .ToList();

    [SupportedOSPlatform("windows")]
    private bool ShellMenuPresent(string extension)
    {
        using var key = Open($@"SystemFileAssociations\.{extension}\shell\VidShrink\command");
        return key is not null;
    }

    [SupportedOSPlatform("windows")]
    private void PlantForeignEntries()
    {
        using var list = Registry.CurrentUser.CreateSubKey($@"{_testKey}\.mp4\OpenWithProgids");
        list.SetValue(ForeignProgId, Array.Empty<byte>(), RegistryValueKind.None);
        using var extension = Registry.CurrentUser.CreateSubKey($@"{_testKey}\.mp4");
        extension.SetValue("", ForeignDefault);
    }

    [SupportedOSPlatform("windows")]
    private void AssertForeignEntriesSurvive()
    {
        using var list = Open(@".mp4\OpenWithProgids");
        Assert.NotNull(list);
        Assert.Contains(ForeignProgId, list!.GetValueNames());
        Assert.Equal(ForeignDefault, Read(".mp4"));
    }

    [SupportedOSPlatform("windows")]
    private void AssertNothingOfOursLeft()
    {
        Assert.Empty(ExtensionsListingUs());
        Assert.Null(Open(FileAssociation.ProgId));
        Assert.Null(Open(@"Applications\VidShrink.exe"));
        Assert.Null(Open(@"Teknesyum\VidShrink\Capabilities"));
        using var registered = Open("RegisteredApplications");
        Assert.True(registered is null || !registered.GetValueNames().Contains("VidShrink"));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Uygulama_kaydi_verilen_koke_yaziyor_ve_baskasinin_listesini_bozmuyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        PlantForeignEntries();

        var failed = FileAssociation.Register(_launcher, _testKey);

        Assert.Empty(failed);
        Assert.Equal($"\"{_launcher}\" \"%1\"", Read($@"{FileAssociation.ProgId}\shell\open\command"));
        Assert.Equal($"{_launcher},0", Read($@"{FileAssociation.ProgId}\DefaultIcon"));
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(ExtensionsListingUs()));
        AssertForeignEntriesSurvive();
        using var real = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{FileAssociation.ProgId}\shell\open\command");
        Assert.DoesNotContain(_launcher, real?.GetValue("") as string ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baslatici_varsa_kayit_onu_gosteriyor()
    {
        var app = Path.Combine(_installRoot, "app");
        Directory.CreateDirectory(app);
        var appExe = Path.Combine(app, "VidShrink.App.exe");
        File.WriteAllText(appExe, "uygulama");

        Assert.Equal(_launcher, FileAssociation.LaunchTarget(appExe));

        File.Delete(_launcher);
        Assert.Equal(appExe, FileAssociation.LaunchTarget(appExe));

        var loose = Path.Combine(_work, "gevsek", "VidShrink.App.exe");
        Assert.Equal(loose, FileAssociation.LaunchTarget(loose));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Kurucu_iliskilendirmeyi_baslaticiya_ve_varsayilan_uygulamalar_sayfasina_yaziyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var run = WriteAll();
        Assert.True(run.Code == 0, run.Output);

        var command = $"\"{_launcher}\" \"%1\"";
        Assert.Equal(command, Read($@"{FileAssociation.ProgId}\shell\open\command"));
        Assert.Equal(command, Read(@"Applications\VidShrink.exe\shell\open\command"));
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(ExtensionsListingUs()));

        using var associations = Open(@"Teknesyum\VidShrink\Capabilities\FileAssociations");
        Assert.NotNull(associations);
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions.Select(extension => "." + extension)),
            Sorted(associations!.GetValueNames()));
        Assert.All(associations.GetValueNames(), name => Assert.Equal(FileAssociation.ProgId, associations.GetValue(name)));

        var capabilities = Read("RegisteredApplications", "VidShrink");
        Assert.Equal($@"{_testKey}\Teknesyum\VidShrink\Capabilities", capabilities);
        using var target = Registry.CurrentUser.OpenSubKey(capabilities!);
        Assert.NotNull(target);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Iliskilendirme_ve_sag_tik_menusu_ayri_ayri_kaldiriliyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        PlantForeignEntries();

        Assert.True(WriteAll().Code == 0);
        Assert.All(ShellIntegration.MediaExtensions, extension => Assert.True(ShellMenuPresent(extension), extension));

        var removal = Installer("-RemoveFileAssociation", "-RegistryRoot", _registryRoot);
        Assert.True(removal.Code == 0, removal.Output);
        AssertNothingOfOursLeft();
        AssertForeignEntriesSurvive();
        Assert.All(ShellIntegration.MediaExtensions, extension => Assert.True(ShellMenuPresent(extension), extension));

        Assert.True(WriteAll().Code == 0);
        var menuRemoval = Installer("-RemoveShellMenu", "-RegistryRoot", _registryRoot);
        Assert.True(menuRemoval.Code == 0, menuRemoval.Output);
        Assert.All(ShellIntegration.MediaExtensions, extension => Assert.False(ShellMenuPresent(extension), extension));
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(ExtensionsListingUs()));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Ikinci_yazim_cogaltmiyor_ve_iliski_kaldirmasi_kokte_iz_birakmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.True(WriteAll().Code == 0);
        var second = WriteAll();
        Assert.True(second.Code == 0, second.Output);
        using (var list = Open(@".mkv\OpenWithProgids"))
            Assert.Equal(new[] { FileAssociation.ProgId }, list!.GetValueNames());

        var removal = Installer("-RemoveFileAssociation", "-RegistryRoot", _registryRoot);
        Assert.True(removal.Code == 0, removal.Output);

        using var root = Registry.CurrentUser.OpenSubKey(_testKey);
        Assert.NotNull(root);
        Assert.Equal(new[] { "SystemFileAssociations" }, root!.GetSubKeyNames());
        Assert.Equal(0, root.ValueCount);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Kaldirma_kaydi_temizliyor_ama_Programs_disindaki_klasoru_silmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        PlantForeignEntries();
        Assert.True(WriteAll().Code == 0);

        var run = Installer("-Uninstall", "-SkipShortcuts", "-InstallRoot", _installRoot, "-RegistryRoot", _registryRoot);

        Assert.True(run.Code == 0, run.Output);
        AssertNothingOfOursLeft();
        AssertForeignEntriesSurvive();
        Assert.All(ShellIntegration.MediaExtensions, extension => Assert.False(ShellMenuPresent(extension), extension));
        Assert.True(File.Exists(_launcher));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Kucult_atlaninca_iliskilendirme_de_yazilmiyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var run = Installer("-ShellMenuOnly", "-SkipShortcuts", "-InstallRoot", _installRoot, "-RegistryRoot", _registryRoot);

        Assert.True(run.Code == 0, run.Output);
        Assert.Null(Registry.CurrentUser.OpenSubKey(_testKey));
    }

    private static string Shell()
    {
        if (!OperatingSystem.IsWindows()) return "/bin/sh";

        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles") })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var candidate = Path.Combine(root, "Git", "bin", "sh.exe");
            if (File.Exists(candidate)) return candidate;
        }

        Assert.Fail("Git sh bulunamadı; kabuk betikleri Windows'ta onunla ölçülüyor.");
        return "";
    }

    private (int Code, string Output) Sh(string home, params string[] arguments)
        => Run(Shell(), arguments, TipSources.Root, new Dictionary<string, string> { ["HOME"] = home });

    private static IReadOnlyDictionary<string, string> LinuxMediaTypes()
    {
        var source = File.ReadAllText(Path.Combine(TipSources.Root, "install-vidshrink.sh"));
        var block = Regex.Match(source, @"media_types='(?<body>[^']*)'");
        Assert.True(block.Success, "install-vidshrink.sh içinde media_types tablosu yok.");

        return block.Groups["body"].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }

    [Fact]
    public void Linux_masaustu_kaydi_her_uzantinin_turunu_tasiyor_ve_kaldirma_siliyor()
    {
        var home = Path.Combine(_work, "ev");
        Directory.CreateDirectory(home);
        var entry = Path.Combine(home, ".local", "share", "applications", "vidshrink.desktop");
        var mimePackage = Path.Combine(home, ".local", "share", "mime", "packages", "vidshrink.xml");
        const string executable = "/opt/vid shrink/VidShrink.App";

        var write = Sh(home, "install-vidshrink.sh", "--desktop-entry", executable);
        Assert.True(write.Code == 0, write.Output);

        var lines = File.ReadAllLines(entry);
        Assert.Equal("[Desktop Entry]", lines[0]);
        Assert.Contains($"Exec=\"{executable}\" %F", lines);

        var declared = lines.Single(line => line.StartsWith("MimeType=", StringComparison.Ordinal))["MimeType=".Length..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var table = LinuxMediaTypes();
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(table.Keys));
        Assert.Equal(Sorted(table.Values.Distinct()), Sorted(declared));

        var package = XDocument.Load(mimePackage);
        XNamespace mime = "http://www.freedesktop.org/standards/shared-mime-info";
        Assert.Contains(package.Descendants(mime + "glob"), glob => (string?)glob.Attribute("pattern") == "*.dav");
        Assert.Equal("video/x-dav", table["dav"]);

        var removal = Sh(home, "install-vidshrink.sh", "--uninstall");
        Assert.True(removal.Code == 0, removal.Output);
        Assert.False(File.Exists(entry));
        Assert.False(File.Exists(mimePackage));
    }

    private static XElement? ValueAfter(XElement dict, string key)
        => dict.Elements("key").FirstOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault();

    [Fact]
    public void MacOS_paketi_her_uzantiyi_goruntuleyici_olarak_bildiriyor()
    {
        var sandbox = Path.Combine(_work, "paket");
        var payload = Path.Combine(sandbox, "yuk");
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, "VidShrink"), "#!/bin/sh\nexit 0\n");
        var bundle = Path.Combine(sandbox, "VidShrink.app");

        var run = Sh(sandbox, "macos-app-bundle.sh", payload.Replace('\\', '/'), "VidShrink", "0.0.0", bundle.Replace('\\', '/'));
        Assert.True(run.Code == 0, run.Output);

        using var reader = XmlReader.Create(Path.Combine(bundle, "Contents", "Info.plist"),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        var plist = XDocument.Load(reader);
        var top = plist.Root!.Element("dict")!;
        var types = ValueAfter(top, "CFBundleDocumentTypes");
        Assert.NotNull(types);

        var documents = types!.Elements("dict").ToList();
        Assert.Equal(2, documents.Count);
        Assert.All(documents, document =>
        {
            Assert.Equal("Viewer", ValueAfter(document, "CFBundleTypeRole")?.Value);
            Assert.Equal("Alternate", ValueAfter(document, "LSHandlerRank")?.Value);
        });

        var contentTypes = ValueAfter(documents[0], "LSItemContentTypes")!.Elements("string").Select(element => element.Value);
        Assert.Contains("public.movie", contentTypes);

        var extensions = ValueAfter(documents[1], "CFBundleTypeExtensions")!.Elements("string").Select(element => element.Value);
        Assert.Equal(Sorted(ShellIntegration.MediaExtensions), Sorted(extensions));
    }
}
