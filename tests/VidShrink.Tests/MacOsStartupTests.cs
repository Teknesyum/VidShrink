using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Avalonia.Controls;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public class MacOsStartupTests
{
    private static string MacHostName()
    {
        var csproj = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "VidShrink.App.csproj"));
        var match = Regex.Match(csproj, @"<MacHostName>([^<]+)</MacHostName>");
        Assert.True(match.Success, "VidShrink.App.csproj içinde MacHostName yok.");
        return match.Groups[1].Value;
    }

    [Fact]
    public void MacHostNameIsNotAnAppBundleName()
    {
        Assert.False(MacHostName().EndsWith(".app", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InstallerLooksForTheNameTheBuildProduces()
    {
        var script = File.ReadAllText(Path.Combine(TipSources.Root, "install-vidshrink.sh"));
        Assert.Contains($"for candidate in {MacHostName()} ", script);
    }

    [Fact]
    public void MacOsFindsFfmpegOutsideThePath()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var installed = Array.Exists(
            ToolLocator.MacToolDirectories,
            directory => File.Exists(Path.Combine(directory, "ffmpeg")));
        if (!installed) return;

        Assert.True(File.Exists(ToolLocator.Locate("ffmpeg", "/usr/bin:/bin:/usr/sbin:/sbin")));
    }

    [Fact]
    public void TheRenameIsLimitedToMacPublishes()
    {
        var csproj = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "VidShrink.App.csproj"));
        var target = Regex.Match(csproj, @"<Target Name=""RenameMacHost""(.*?)>", RegexOptions.Singleline);
        Assert.True(target.Success, "RenameMacHost hedefi yok.");
        Assert.Contains("RuntimeIdentifier.StartsWith('osx')", target.Groups[1].Value);
    }

    [Fact]
    public void TheMacFallbackIsOffOutsideMacOs()
    {
        if (OperatingSystem.IsMacOS()) return;

        Assert.Throws<FileNotFoundException>(() => ToolLocator.Locate("ffmpeg", ""));
    }

    /// <summary>
    /// <see cref="AppHost"/> pencere arka ucunu platforma göre seçiyor. Ölçüm seçimi
    /// betikten değil kurulmuş çalışma zamanından okuyor: Windows'ta kurulan arka ucun
    /// hâlâ Win32 olduğunu, yani dalın Windows tarafında hiçbir şeyi değiştirmediğini
    /// bu doğruluyor. macOS ile Linux'ta Avalonia Native ile X11 pencereyi sürecin ana
    /// iş parçacığında kurmayı şart koşuyor, o iş parçacığı da xUnit koşucusunun elinde;
    /// oralarda başsız arka uç kuruluyor ve çizimi yine Skia yapıyor.
    /// </summary>
    [Fact]
    public void TheWindowingBackendIsWin32OnWindowsAndHeadlessElsewhere()
    {
        AppHost.Run(() => new Window());

        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .ToHashSet(StringComparer.Ordinal);

        var expected = OperatingSystem.IsWindows() ? "Avalonia.Win32" : "Avalonia.Headless";
        var other = OperatingSystem.IsWindows() ? "Avalonia.Headless" : "Avalonia.Win32";

        Assert.Contains(expected, loaded);
        Assert.DoesNotContain(other, loaded);
    }
}
