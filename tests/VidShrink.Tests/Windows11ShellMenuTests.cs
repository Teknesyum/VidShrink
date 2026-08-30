using System.Text.RegularExpressions;

namespace VidShrink.Tests;

public sealed class Windows11ShellMenuTests
{
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([TipSources.Root, .. parts]));

    [Fact]
    public void Installer_starts_with_param_and_has_no_utf8_bom()
    {
        var bytes = File.ReadAllBytes(Path.Combine(TipSources.Root, "Install-VidShrink.ps1"));
        Assert.True(bytes.Length >= 3);
        Assert.Equal(new byte[] { 0x70, 0x61, 0x72 }, bytes[..3]);
    }

    [Fact]
    public void Windows_11_uses_sparse_package_and_windows_10_keeps_legacy_verb()
    {
        var installer = Read("Install-VidShrink.ps1");
        Assert.Contains("OSVersion.Version.Build -ge 22000", installer);
        Assert.Contains("Add-AppxPackage -Register $manifestPath -ExternalLocation $InstallDirectory", installer);
        Assert.Contains("Write-ShellMenu $Root $Executable", installer);
        Assert.Contains("Write-Windows11ShellMenu $Root", installer);
    }

    [Fact]
    public void Removal_clears_legacy_entries_and_sparse_package()
    {
        var installer = Read("Install-VidShrink.ps1");
        var block = Regex.Match(installer, @"if \(\$RemoveShellMenu\) \{(?<body>[\s\S]*?)\n\}");
        Assert.True(block.Success);
        Assert.Contains("Remove-ShellMenu $RegistryRoot", block.Groups["body"].Value);
        Assert.Contains("Remove-Windows11ShellMenu $RegistryRoot", block.Groups["body"].Value);
        Assert.Contains("Remove-AppxPackage -Package $package.PackageFullName", installer);
    }

    [Fact]
    public void Reinstall_unregisters_the_external_package_before_replacing_its_directory()
    {
        var installer = Read("Install-VidShrink.ps1");
        var unregister = installer.LastIndexOf("Remove-Windows11ShellMenu $RegistryRoot | Out-Null", StringComparison.Ordinal);
        var removeRoot = installer.LastIndexOf("Remove-InstallRoot $resolvedInstallRoot", StringComparison.Ordinal);
        Assert.True(unregister >= 0);
        Assert.True(removeRoot > unregister);
    }

    [Fact]
    public void Modern_manifest_gets_item_types_from_the_installer_extension_array()
    {
        var installer = Read("Install-VidShrink.ps1");
        var manifest = Read("src", "VidShrink.ShellExtension", "AppxManifest.template.xml");
        Assert.Contains("foreach ($extension in $shellMenuExtensions)", installer);
        Assert.Contains("template.Replace('__ITEM_TYPES__'", installer);
        Assert.Contains("windows.fileExplorerContextMenus", manifest);
        Assert.Contains("__ITEM_TYPES__", manifest);
        Assert.DoesNotMatch("desktop5:ItemType Type=\"\\.\\w+", manifest);
    }

    [Fact]
    public void Native_command_and_manifest_share_the_com_class()
    {
        var source = Read("src", "VidShrink.ShellExtension", "VidShrink.ShellExtension.cpp");
        var manifest = Read("src", "VidShrink.ShellExtension", "AppxManifest.template.xml");
        const string clsid = "7B8B4A16-E3F5-4C4A-A8D2-26B2F895BE58";
        Assert.Contains("0x7b8b4a16, 0xe3f5, 0x4c4a", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(clsid, manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IShellItemArray", source);
        Assert.Contains("ShellExecuteW", source);
    }

    [Fact]
    public void Release_builds_and_places_the_shell_extension_in_the_launcher_archive()
    {
        var workflow = Read(".github", "workflows", "release.yml");
        Assert.Contains("msbuild src/VidShrink.ShellExtension/VidShrink.ShellExtension.vcxproj", workflow);
        Assert.Contains("name: shell-extension-win-x64", workflow);
        Assert.Contains("path: publish-launcher/shell", workflow);
    }
}
