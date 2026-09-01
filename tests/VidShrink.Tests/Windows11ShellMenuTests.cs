using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VidShrink.Tests;

public sealed class Windows11ShellMenuTests
{
    private static readonly string Installer = Path.Combine(TipSources.Root, "Install-VidShrink.ps1");

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([TipSources.Root, .. parts]));

    private static (int Code, string Output) Run(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} başlatılamadı.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static (int Code, string Output) PowerShell(params string[] arguments)
    {
        var all = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass" };
        all.AddRange(arguments);
        return Run("powershell.exe", all.ToArray());
    }

    private static string? FindMsBuild()
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere)) return null;
        var found = Run(vswhere, "-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild",
            "-find", @"MSBuild\**\Bin\MSBuild.exe");
        return found.Code == 0
            ? found.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;
    }

    private static IReadOnlyList<string> InstallerExtensions()
    {
        var source = File.ReadAllText(Installer);
        var block = Regex.Match(source, @"\$shellMenuExtensions\s*=\s*@\((?<body>[^)]*)\)");
        Assert.True(block.Success);
        return Regex.Matches(block.Groups["body"].Value, @"'([^']+)'")
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    [Fact]
    public void Installer_starts_with_param_and_has_no_utf8_bom()
    {
        var bytes = File.ReadAllBytes(Installer);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(new byte[] { 0x70, 0x61, 0x72 }, bytes[..3]);
    }

    [Fact]
    public void Missing_modern_package_files_fall_back_to_a_working_classic_menu()
    {
        if (!OperatingSystem.IsWindows()) return;

        var id = Guid.NewGuid().ToString("n");
        var work = Path.Combine(TestPaths.OutputRoot, "shell-package-fallback", id);
        var install = Path.Combine(work, "install");
        var executable = Path.Combine(install, "VidShrink.exe");
        var registry = @"HKCU:\Software\VidShrink-Fallback-Test-" + id;
        Directory.CreateDirectory(install);
        File.WriteAllText(executable, "launcher");

        try
        {
            var run = PowerShell("-File", Installer, "-ShellMenuOnly", "-InstallRoot", install,
                "-RegistryRoot", registry, "-MenuLanguage", "en");
            Assert.True(run.Code == 0, run.Output);
            Assert.Contains("klasik", run.Output, StringComparison.OrdinalIgnoreCase);

            var key = registry + @"\SystemFileAssociations\.mp4\shell\VidShrink";
            var probe = PowerShell("-Command",
                $"$v=Get-ItemProperty -LiteralPath '{key}'; $c=(Get-Item -LiteralPath '{key}\\command').GetValue(''); Write-Output ($v.MUIVerb+'|'+$c)");
            Assert.True(probe.Code == 0, probe.Output);
            Assert.Contains("Open this video with VidShrink", probe.Output);
            Assert.Contains($"\"{executable}\" \"%1\"", probe.Output);
        }
        finally
        {
            PowerShell("-Command", $"Remove-Item -LiteralPath '{registry}' -Recurse -Force -ErrorAction SilentlyContinue");
            try { Directory.Delete(work, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Sparse_package_really_registers_and_removes_on_Windows_11()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var msbuild = FindMsBuild();
        if (msbuild is null) return;
        using var appxLock = new Mutex(false, @"Local\VidShrink-Sparse-Package-Test");
        Assert.True(appxLock.WaitOne(TimeSpan.FromMinutes(5)), "AppX test kilidi zaman aşımına uğradı.");
        try
        {
        var before = PowerShell("-Command", "@(Get-AppxPackage -Name Teknesyum.VidShrink.Shell).Count");
        Assert.True(before.Code == 0, before.Output);
        if (before.Output.Trim() != "0") return;

        var work = Path.Combine(TestPaths.OutputRoot, "shell-package", Guid.NewGuid().ToString("n"));
        var install = Path.Combine(work, "install");
        var shell = Path.Combine(install, "shell");
        var assets = Path.Combine(shell, "Assets");
        Directory.CreateDirectory(assets);

        try
        {
            var project = Path.Combine(TipSources.Root, "src", "VidShrink.ShellExtension", "VidShrink.ShellExtension.vcxproj");
            var intermediate = Path.Combine(work, "obj") + Path.DirectorySeparatorChar;
            var built = Run(msbuild, project, "/nologo", "/p:Configuration=Release", "/p:Platform=x64",
                $"/p:OutDir={shell}{Path.DirectorySeparatorChar}", $"/p:IntDir={intermediate}");
            Assert.True(built.Code == 0, built.Output);

            File.Copy(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                Path.Combine(install, "VidShrink.exe"));
            var template = Read("src", "VidShrink.ShellExtension", "AppxManifest.template.xml");
            const string clsid = "7B8B4A16-E3F5-4C4A-A8D2-26B2F895BE58";
            var verbs = InstallerExtensions().Select(extension =>
                $"            <desktop5:ItemType Type=\".{extension}\"><desktop5:Verb Id=\"VidShrink{extension}\" Clsid=\"{clsid}\" /></desktop5:ItemType>");
            var manifest = Path.Combine(shell, "AppxManifest.xml");
            File.WriteAllText(manifest, template.Replace("__ITEM_TYPES__", string.Join(Environment.NewLine, verbs)),
                new UTF8Encoding(false));

            var icon = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Assets", "VidShrink.png");
            var assetScript = "$s=[Drawing.Image]::FromFile($args[0]); foreach($i in @(@('StoreLogo.png',50),@('Square150x150Logo.png',150),@('Square44x44Logo.png',44))){$b=[Drawing.Bitmap]::new($i[1],$i[1]);$g=[Drawing.Graphics]::FromImage($b);$g.DrawImage($s,0,0,$i[1],$i[1]);$b.Save((Join-Path $args[1] $i[0]),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()};$s.Dispose()";
            var assetFile = Path.Combine(work, "assets.ps1");
            File.WriteAllText(assetFile, "Add-Type -AssemblyName System.Drawing;" + assetScript);
            var images = PowerShell("-File", assetFile, icon, assets);
            Assert.True(images.Code == 0, images.Output);

            var register = PowerShell("-Command",
                $"Add-AppxPackage -Register '{manifest}' -ExternalLocation '{install}'; @(Get-AppxPackage -Name Teknesyum.VidShrink.Shell).Count");
            Assert.True(register.Code == 0, register.Output);
            Assert.EndsWith("1", register.Output.Trim(), StringComparison.Ordinal);

            var remove = PowerShell("-Command",
                "$p=Get-AppxPackage -Name Teknesyum.VidShrink.Shell; Remove-AppxPackage -Package $p.PackageFullName; @(Get-AppxPackage -Name Teknesyum.VidShrink.Shell).Count");
            Assert.True(remove.Code == 0, remove.Output);
            Assert.EndsWith("0", remove.Output.Trim(), StringComparison.Ordinal);
        }
        finally
        {
            PowerShell("-Command", "$p=Get-AppxPackage -Name Teknesyum.VidShrink.Shell -ErrorAction SilentlyContinue; if($p){Remove-AppxPackage -Package $p.PackageFullName}");
            try { Directory.Delete(work, true); } catch (IOException) { }
        }
        }
        finally
        {
            appxLock.ReleaseMutex();
        }
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
    public void Release_uses_the_project_toolset_and_publish_waits_for_the_shell_job()
    {
        var project = XDocument.Load(Path.Combine(TipSources.Root, "src", "VidShrink.ShellExtension", "VidShrink.ShellExtension.vcxproj"));
        XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
        Assert.Equal("v145", Assert.Single(project.Descendants(ns + "PlatformToolset")).Value);

        var workflow = Read(".github", "workflows", "release.yml");
        var build = Regex.Match(workflow, @"run: msbuild src/VidShrink\.ShellExtension/VidShrink\.ShellExtension\.vcxproj(?<arguments>[^\r\n]*)");
        Assert.True(build.Success);
        Assert.DoesNotContain("PlatformToolset", build.Groups["arguments"].Value);
        Assert.Contains("needs: [version, test, shell-extension]", workflow);
        Assert.Contains("path: publish-launcher/shell", workflow);
    }
}
