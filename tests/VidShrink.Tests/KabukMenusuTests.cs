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
        Assert.Contains("x:Name=\"ChkShellMenuOpen\"", xaml);
        Assert.Contains("x:Name=\"ChkShellMenuShrink\"", xaml);
        Assert.Contains("{loc:Text settings-tab.shell-menu.hint}", xaml);
    }

    /// <summary>
    /// İki girdi iki ayrı kutu. Kullanıcı açma girdisini isteyip küçültme alt menüsünü
    /// istemeyebilir; tek kutu ikisini birden dayatıyordu. Ölçülen şey kolların gerçekten
    /// ayrı olması: her kutu kendi kayıt defteri anahtarını yazıyor ve siliyor.
    /// </summary>
    [Fact]
    public void IkiGirdiAyriKollardan()
    {
        var code = Code();
        var panel = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.KabukMenusu.cs"));

        foreach (var member in new[] { "InstallOpen", "InstallShrink", "RemoveOpen", "RemoveShrink" })
            Assert.Contains(member, code);

        Assert.DoesNotContain("Install(string executable, string openLabel", code);
        Assert.Contains("ShellMenu.InstallOpen", panel);
        Assert.Contains("ShellMenu.InstallShrink", panel);
        Assert.Contains("ShellMenu.RemoveOpen", panel);
        Assert.Contains("ShellMenu.RemoveShrink", panel);
        Assert.Contains("Installed(ShellMenu.MenuKey)", panel);
        Assert.Contains("Installed(ShellMenu.ShrinkMenuKey)", panel);
    }

    /// <summary>
    /// Windows 11'in üst düzey girdisi ayrı bir süreçte, C++ tarafında çizilir ve
    /// uygulamanın dil ayarını göremez. Eskiden başlık Windows arayüz diline bakıp
    /// Türkçe ya da İngilizce yazıyordu: Fransızca kullanan biri İngilizce görüyordu,
    /// oysa 42 dilin hepsinde <c>shell.menu.open</c> çevrilmiş duruyor. Artık uygulama
    /// seçili dildeki etiketi kayıt defterine bırakıyor, uzantı başlığı oradan okuyor.
    /// </summary>
    [Fact]
    public void UstDuzeyGirdiUygulamaninDiliniOkuyor()
    {
        var extension = File.ReadAllText(Path.Combine(
            TipSources.Root, "src", "VidShrink.ShellExtension", "VidShrink.ShellExtension.cpp"));

        Assert.DoesNotContain("LANG_TURKISH", extension);
        Assert.DoesNotContain("GetUserDefaultUILanguage", extension);
        Assert.Contains("RegGetValueW(HKEY_CURRENT_USER, LabelKey", extension);
        Assert.Contains("ShellLabels", extension);
        Assert.Contains("ShellLabels", Code());
        Assert.Contains("WriteLabel(\"open\", label);", Code());
        Assert.Contains("WriteLabel(\"shrink\", label);", Code());
    }

    /// <summary>Her girdi ikonu taşıyor: açma, küçültme başlığı ve her hedef.</summary>
    [Fact]
    public void HerGirdideIkonVar()
    {
        var code = Code();
        var icons = Regex.Matches(code, @"SetValue\(""Icon"", executable").Count;

        Assert.Equal(3, icons);
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

    /// <summary>
    /// Etiket yenilemesi her açılışta yapıcıda koşuyor. Eskiden girdileri silip yeniden kuruyordu:
    /// silme kolu Appx paketini kaldırmak için PowerShell başlatıyordu (açılışta 867 ms) ve komut
    /// yolu o an koşan exe'ye yeniden yazılıyordu. Yenileme yalnız farklı olan etiketi yazar.
    /// Kurulan Windows 11 paketini yalnız kutunun boşaltılması kaldırır; açma girdisini yeniden
    /// yazmak paketi kaldırmaz.
    /// </summary>
    [Fact]
    public void EtiketYenilemesiGirdiyiYenidenKurmuyor()
    {
        var panel = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.KabukMenusu.cs"));
        var relabel = panel[panel.IndexOf("private void RelabelShellMenu()", StringComparison.Ordinal)..];
        relabel = relabel[..relabel.IndexOf("private void ShowShellMenuStatus", StringComparison.Ordinal)];

        Assert.Contains("ShellMenu.Relabel(ShellMenu.MenuKey", relabel);
        Assert.Contains("ShellMenu.Relabel(ShellMenu.ShrinkMenuKey", relabel);
        Assert.DoesNotContain("Install", relabel);
        Assert.DoesNotContain("ProcessPath", relabel);

        var code = Code();
        var body = code[code.IndexOf("internal static int Relabel(", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("internal static int RemoveOpen", StringComparison.Ordinal)];
        Assert.DoesNotContain("RemovePackage", body);
        Assert.DoesNotContain("Icon", body);
        Assert.DoesNotContain("command", body);
        Assert.Contains("== label) continue;", body);

        var install = code[code.IndexOf("internal static int InstallOpen(", StringComparison.Ordinal)..];
        install = install[..install.IndexOf("internal static int InstallShrink(", StringComparison.Ordinal)];
        Assert.DoesNotContain("RemoveOpen()", install);
        Assert.DoesNotContain("RemovePackage", install);
        Assert.Contains("Drop(MenuKey);", install);

        var removeOpen = code[code.IndexOf("internal static int RemoveOpen()", StringComparison.Ordinal)..];
        removeOpen = removeOpen[..removeOpen.IndexOf("internal static int RemoveShrink()", StringComparison.Ordinal)];
        Assert.Contains("RemovePackage();", removeOpen);
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
