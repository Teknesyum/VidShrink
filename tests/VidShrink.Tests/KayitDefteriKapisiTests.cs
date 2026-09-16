using System.Runtime.Versioning;
using Microsoft.Win32;
using VidShrink.App;
using VidShrink.App.Integration;

namespace VidShrink.Tests;

/// <summary>
/// Test konağı gerçek HKCU\Software\Classes'a yazdığında kullanıcının sağ tık menüsünün 312
/// değeri testhost.exe'yi gösterdi. Yazma kökü süreç adına bağlı: yalnız VidShrink.App.exe
/// gerçek kayda yazar, gerisi ya enjekte edilen test köküne yazar ya hiç yazmaz.
/// </summary>
public class KayitDefteriKapisiTests
{
    [Theory]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\app\VidShrink.App.exe", true)]
    [InlineData(@"C:\repo\tests\bin\Debug\net8.0\testhost.exe", false)]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\VidShrink.exe", false)]
    [InlineData(@"C:\tools\VidShrink.Bench.exe", false)]
    [InlineData(@"C:\dotnet\dotnet.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void YalnizUygulamaSureciGercekKaydaYazar(string? processPath, bool allowed)
    {
        Assert.Equal(allowed, RegistryWriteGate.Allows(processPath));
        Assert.Equal(allowed ? string.Empty : null, ShellMenu.WriteRoot(processPath, null));
        Assert.Equal(allowed, FileAssociation.WriteAllowed(processPath, FileAssociation.ClassesRoot));
    }

    [Fact]
    public void TestKonagiGercekKokeYazamaz()
    {
        Assert.Null(ShellMenu.WriteRoot(Environment.ProcessPath, null));
        Assert.False(FileAssociation.WriteAllowed(Environment.ProcessPath, FileAssociation.ClassesRoot));
        Assert.True(FileAssociation.WriteAllowed(Environment.ProcessPath, @"Software\VidShrinkTest\Classes"));
        Assert.Equal(@"Software\VidShrinkTest\", ShellMenu.WriteRoot(Environment.ProcessPath, @"Software\VidShrinkTest"));
    }

    [Fact]
    public void KabukMenusuYazimlariKoktenGeciyor()
    {
        var code = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "ShellMenu.cs"));
        var writes = code.Split('\n')
            .Where(line => line.Contains("Registry.CurrentUser.", StringComparison.Ordinal))
            .Where(line => line.Contains("CreateSubKey(", StringComparison.Ordinal)
                || line.Contains("writable: true", StringComparison.Ordinal)
                || line.Contains("DeleteSubKeyTree(", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(writes);
        Assert.All(writes, line => Assert.True(
            line.Contains("(root + ", StringComparison.Ordinal) || line.Contains("DeleteSubKeyTree(path", StringComparison.Ordinal),
            line.Trim()));
        Assert.Contains("var path = root + Branch(extension)", code);

        foreach (var method in new[] { "InstallOpen(", "InstallShrink(", "Relabel(", "WriteLabel(", "Drop(", "RemoveOpen(" })
        {
            var at = System.Text.RegularExpressions.Regex.Match(code, @"static (int|void) " + System.Text.RegularExpressions.Regex.Escape(method)).Index;
            Assert.True(at > 0, method);
            var head = code.Substring(at, 160);
            Assert.Matches(@"if \(Root is (not \{ \} root|null)\) return", head);
        }

        var association = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Integration", "FileAssociation.cs"));
        Assert.Contains("WriteAllowed(Environment.ProcessPath, classesRoot)", association);
        Assert.Contains("!allowed || !Write(", association);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void EnjekteKokeYaziliyorGercekKokeDokunulmuyor()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = @"Software\Teknesyum\VidShrinkTest\KayitKapisi-" + Guid.NewGuid().ToString("N");
        var real = @"Software\Classes\SystemFileAssociations\.mp4\shell\" + ShellMenu.MenuKey + @"\command";
        string? Before()
        {
            using var key = Registry.CurrentUser.OpenSubKey(real);
            return key?.GetValue(string.Empty) as string;
        }

        var before = Before();
        ShellMenu.TestRoot = root;
        try
        {
            Assert.Equal(ShellMenu.Extensions.Length, ShellMenu.InstallOpen(@"C:\sahte\VidShrink.App.exe", "Ac"));
            using (var command = Registry.CurrentUser.OpenSubKey(root + @"\Software\Classes\SystemFileAssociations\.mp4\shell\" + ShellMenu.MenuKey + @"\command"))
                Assert.Equal("\"C:\\sahte\\VidShrink.App.exe\" \"%1\"", command?.GetValue(string.Empty));
            Assert.Equal(before, Before());
        }
        finally
        {
            ShellMenu.TestRoot = null;
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
    }
}
