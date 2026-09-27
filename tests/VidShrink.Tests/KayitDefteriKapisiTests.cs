using System.Runtime.Versioning;
using Microsoft.Win32;
using VidShrink.App;
using VidShrink.App.Integration;

namespace VidShrink.Tests;

/// <summary>
/// Test konağı gerçek HKCU\Software\Classes'a yazdığında kullanıcının sağ tık menüsünün 312
/// değeri testhost.exe'yi gösterdi. Yazma kökü süreç adına bağlı: yalnız VidShrink.App.exe
/// gerçek kayda yazar, gerisi ya enjekte edilen test köküne yazar ya hiç yazmaz. Ad yetmez:
/// derleme çıktısındaki VidShrink.App.exe de kayda yazıyordu; izin yalnız kurulu düzende,
/// exe bir <c>app\</c> klasöründeyken ve bir üstte <c>VidShrink.exe</c> başlatıcısı varken.
/// </summary>
public class KayitDefteriKapisiTests
{
    [Theory]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\app\VidShrink.App.exe", true)]
    [InlineData(@"C:\repo\src\VidShrink.App\bin\Release\net8.0\VidShrink.App.exe", false)]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\VidShrink.App.exe", false)]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\app\VidShrink.exe", false)]
    [InlineData(@"C:\repo\tests\bin\Debug\net8.0\testhost.exe", false)]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\VidShrink\VidShrink.exe", false)]
    [InlineData(@"C:\tools\VidShrink.Bench.exe", false)]
    [InlineData(@"C:\dotnet\dotnet.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void YalnizUygulamaSureciGercekKaydaYazar(string? processPath, bool allowed)
    {
        Assert.Equal(allowed, RegistryWriteGate.Allows(processPath, _ => true));
        Assert.False(RegistryWriteGate.Allows(processPath, _ => false));
        Assert.False(RegistryWriteGate.Allows(processPath));
        Assert.Null(ShellMenu.WriteRoot(processPath, null));
        Assert.False(FileAssociation.WriteAllowed(processPath, FileAssociation.ClassesRoot));
    }

    [Fact]
    public void YalnizKuruluDuzendeGercekKaydaYazar()
    {
        var root = Path.Combine(TipSources.Root, ".calisma", "kayit-defteri-kapisi", Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "Programs", "VidShrink");
        var app = Path.Combine(install, "app", "VidShrink.App.exe");
        var launcher = Path.Combine(install, "VidShrink.exe");
        var build = Path.Combine(root, "src", "VidShrink.App", "bin", "Release", "net8.0", "VidShrink.App.exe");
        var inside = Path.Combine(root, "tek", "app", "VidShrink.App.exe");
        void Touch(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Array.Empty<byte>());
        }

        try
        {
            Touch(app);
            Touch(launcher);
            Touch(build);
            Touch(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(build)!)!, "VidShrink.exe"));
            Touch(Path.Combine(Path.GetDirectoryName(build)!, "VidShrink.exe"));
            Touch(inside);
            Touch(Path.Combine(Path.GetDirectoryName(inside)!, "VidShrink.exe"));

            Assert.True(RegistryWriteGate.Allows(app));
            Assert.Equal(string.Empty, ShellMenu.WriteRoot(app, null));
            Assert.True(FileAssociation.WriteAllowed(app, FileAssociation.ClassesRoot));

            Assert.False(RegistryWriteGate.Allows(build));
            Assert.Null(ShellMenu.WriteRoot(build, null));
            Assert.False(FileAssociation.WriteAllowed(build, FileAssociation.ClassesRoot));

            Assert.False(RegistryWriteGate.Allows(inside));

            File.Delete(launcher);
            Assert.False(RegistryWriteGate.Allows(app));
            Assert.Null(ShellMenu.WriteRoot(app, null));
            Assert.False(FileAssociation.WriteAllowed(app, FileAssociation.ClassesRoot));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
        Assert.Contains("if (allowed) RemoveStaleLegacy(classesRoot);", association);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(association, @"RemoveStaleLegacy\(classesRoot\)"));
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
