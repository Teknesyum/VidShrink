using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Sağ tık menüsünün iki tarafı: uygulamanın Ayarlar sekmesinden yazdığı kayıt defteri
/// düzeni ile kurucunun yazdığı düzen. İkisi ayrılırsa betikle kurulan menü uygulamadan
/// silinemez; ölçülen şey bu eşitlik ve kutunun kullanıcıya kod bırakmaması.
/// </summary>
public class KabukMenusuTests
{
    private static string Script() => File.ReadAllText(
        Path.Combine(TipSources.Root, "Install-VidShrink.ps1"));

    private static string Code() => File.ReadAllText(
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "ShellMenu.cs"));

    /// <summary>Anahtar adları ve paket adı iki tarafta birebir aynı.</summary>
    [Theory]
    [InlineData("VidShrink", "shellMenuKeyName")]
    [InlineData("VidShrinkKucult", "shellShrinkMenuKeyName")]
    [InlineData("Teknesyum.VidShrink.Shell", "shellPackageName")]
    [InlineData("--kucult", "shellShrinkFlag")]
    public void AnahtarAdlariIkiTaraftaAyni(string value, string scriptVariable)
    {
        Assert.Contains($"${scriptVariable} = '{value}'", Script());
        Assert.Contains($"\"{value}\"", Code());
    }

    /// <summary>Uzantı listesi iki tarafta aynı kümeyi taşıyor.</summary>
    [Fact]
    public void UzantiListesiAyni()
    {
        var script = Regex.Match(Script(), @"\$shellMenuExtensions = @\((?<body>[^)]*)\)").Groups["body"].Value;
        var beklenen = Regex.Matches(script, "'(?<ad>[^']+)'").Select(m => m.Groups["ad"].Value).ToArray();

        var code = Regex.Match(Code(), @"Extensions =\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        var bizim = Regex.Matches(code, "\"(?<ad>[^\"]+)\"").Select(m => m.Groups["ad"].Value).ToArray();

        Assert.NotEmpty(beklenen);
        Assert.Equal(beklenen, bizim);
    }

    /// <summary>Hızlı küçültme hedefleri iki tarafta aynı.</summary>
    [Fact]
    public void HedefListesiAyni()
    {
        var script = Regex.Match(Script(), @"\$shellShrinkTargets = @\((?<body>[^)]*)\)").Groups["body"].Value;
        var beklenen = Regex.Matches(script, @"\d+").Select(m => m.Value).ToArray();

        var code = Regex.Match(Code(), @"ShrinkTargets = \{(?<body>[^}]*)\}").Groups["body"].Value;
        var bizim = Regex.Matches(code, @"\d+").Select(m => m.Value).ToArray();

        Assert.Equal(beklenen, bizim);
    }

    /// <summary>
    /// Silme kolu Windows 11'in Appx paketini de kaldırıyor: kullanıcıya menüde kalan
    /// tek girdi bırakılmıyor. Issue #1'in kusuru buydu.
    /// </summary>
    [Fact]
    public void SilmeAppxPaketiniDeKaldiriyor()
    {
        var code = Code();

        Assert.Contains("RemovePackage();", code);
        Assert.Contains("Remove-AppxPackage", code);
    }

    /// <summary>Kutu Ayarlar sekmesinde ve kullanıcıya komut satırı bırakmıyor.</summary>
    [Fact]
    public void KutuAyarlardaDuruyor()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);

        Assert.Contains("x:Name=\"ShellMenuPanel\"", xaml);
        Assert.Contains("x:Name=\"ChkShellMenu\"", xaml);
        Assert.Contains("{loc:Text settings-tab.shell-menu.hint}", xaml);
    }

    /// <summary>Menü etiketi arayüz dilini izliyor: dil değişince yeniden yazılıyor.</summary>
    [Fact]
    public void EtiketArayuzDiliniIzliyor()
    {
        var code = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.KabukMenusu.cs"));

        Assert.Contains("Say(\"shell.menu.open\")", code);
        Assert.Contains("Say(\"shell.menu.shrink\")", code);
        Assert.Contains("private void RelabelShellMenu()", code);
        Assert.Contains("RelabelShellMenu();", File.ReadAllText(TipSources.WindowCodePath));
    }

    /// <summary>Sekiz yeni anahtar 42 dilin hepsinde var.</summary>
    [Theory]
    [InlineData("settings-tab.shell-menu.title")]
    [InlineData("settings-tab.shell-menu.label")]
    [InlineData("settings-tab.shell-menu.hint")]
    [InlineData("settings-tab.shell-menu.done")]
    [InlineData("settings-tab.shell-menu.removed")]
    [InlineData("settings-tab.shell-menu.error")]
    [InlineData("shell.menu.open")]
    [InlineData("shell.menu.shrink")]
    public void YeniAnahtarlarButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
        {
            var path = Path.Combine(
                TipSources.Root, "src", "VidShrink.App", "Locales", language, "main.json");

            Assert.True(File.Exists(path), path);
            Assert.Contains($"\"{key}\"", File.ReadAllText(path));
        }
    }
}
