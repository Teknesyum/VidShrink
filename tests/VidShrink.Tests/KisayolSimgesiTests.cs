using VidShrink.Core;
using VidShrink.Core.Setup;

namespace VidShrink.Tests;

/// <summary>
/// Güncelleme ve kurulum sonunda kısayol simgesi kurulu exe'nin ilk simgesine yeniden yazılır,
/// kabuğa bir kez haber verilir; hedefi başka olan kısayola dokunulmaz. Gerçek kısayollara
/// yazmaz: sahte yazıcı ve gerçek COM kolu <c>.calisma/worktree-agent-ad02fec7ffeca98bb/</c>
/// altında, <see cref="ShortcutIcons.DirectoryVariable"/> modül başlatıcısından.
/// </summary>
public sealed class KisayolSimgesiTests : IDisposable
{
    private readonly string _root = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-ad02fec7ffeca98bb", "kisayol-simgesi", Guid.NewGuid().ToString("N"));

    public KisayolSimgesiTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private sealed class SahteYazici : IShortcutWriter
    {
        public Dictionary<string, string> Simgeler { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Okunan { get; } = new();

        public void Write(string shortcutPath, string target, string workingDirectory, string icon) => File.WriteAllText(shortcutPath, target);

        public string? ReadTarget(string shortcutPath)
        {
            Okunan.Add(shortcutPath);
            return File.ReadAllText(shortcutPath);
        }

        public void SetIcon(string shortcutPath, string icon) => Simgeler[shortcutPath] = icon;
    }

    private string Kurulum()
    {
        var kok = Path.Combine(_root, "kurulum");
        Directory.CreateDirectory(kok);
        File.WriteAllText(Path.Combine(kok, LauncherUpdate.ExecutableName), "baslatici");
        return kok;
    }

    private string Kisayol(string ad, string hedef)
    {
        var yol = Path.Combine(_root, ad);
        File.WriteAllText(yol, hedef);
        return yol;
    }

    [Fact]
    public void HedefKuruluExeIseSimgeYazilirVeBirKezHaberVerilir()
    {
        var exe = Path.Combine(Kurulum(), LauncherUpdate.ExecutableName);
        var yazici = new SahteYazici();
        var masaustu = Kisayol("masaustu.lnk", exe);
        var baslat = Kisayol("baslat.lnk", exe.ToUpperInvariant());
        var haber = 0;

        var yazilan = ShortcutIcons.Refresh(yazici, new[] { masaustu, baslat }, exe, () => haber++);

        Assert.Equal(2, yazilan);
        Assert.Equal(1, haber);
        Assert.Equal(exe + ",0", yazici.Simgeler[masaustu]);
        Assert.Equal(exe + ",0", yazici.Simgeler[baslat]);
    }

    [Fact]
    public void HedefBaskaExeIseDokunulmazHaberVerilmez()
    {
        var exe = Path.Combine(Kurulum(), LauncherUpdate.ExecutableName);
        var yazici = new SahteYazici();
        var baska = Kisayol("baska.lnk", Path.Combine(_root, "baska", LauncherUpdate.ExecutableName));
        var haber = 0;

        var yazilan = ShortcutIcons.Refresh(yazici, new[] { baska }, exe, () => haber++);

        Assert.Equal(0, yazilan);
        Assert.Equal(0, haber);
        Assert.Empty(yazici.Simgeler);
        Assert.Equal(new[] { baska }, yazici.Okunan);
    }

    [Fact]
    public void OlmayanKisayolOkunmazDigerleriYazilir()
    {
        var exe = Path.Combine(Kurulum(), LauncherUpdate.ExecutableName);
        var yazici = new SahteYazici();
        var yok = Path.Combine(_root, "yok.lnk");
        var mevcut = Kisayol("var.lnk", exe);
        var haber = 0;

        var yazilan = ShortcutIcons.Refresh(yazici, new[] { yok, mevcut }, exe, () => haber++);

        Assert.Equal(1, yazilan);
        Assert.Equal(1, haber);
        Assert.DoesNotContain(yok, yazici.Okunan);
        Assert.Single(yazici.Simgeler);
    }

    [Fact]
    public void KonumlarDegiskendenGelir()
    {
        var klasor = Environment.GetEnvironmentVariable(ShortcutIcons.DirectoryVariable);

        var konumlar = ShortcutIcons.Locations();

        Assert.False(string.IsNullOrWhiteSpace(klasor));
        Assert.Equal(new[]
        {
            Path.Combine(klasor!, ShortcutIcons.ShortcutName),
            Path.Combine(klasor!, "Programs", "VidShrink", ShortcutIcons.ShortcutName)
        }, konumlar);
    }

    [Fact]
    public void GercekKisayolunSimgesiYenidenYazilirBaskasininKalir()
    {
        if (!OperatingSystem.IsWindows()) return;
        var exe = Path.Combine(Kurulum(), LauncherUpdate.ExecutableName);
        var baskaExe = Path.Combine(_root, "baska.exe");
        File.WriteAllText(baskaExe, "baska");
        var eskiSimge = Path.Combine(Environment.SystemDirectory, "shell32.dll") + ",3";
        var yazici = new ShellShortcuts();
        var bizim = Path.Combine(_root, "bizim.lnk");
        var baska = Path.Combine(_root, "baska.lnk");
        yazici.Write(bizim, exe, _root, eskiSimge);
        yazici.Write(baska, baskaExe, _root, eskiSimge);
        var haber = 0;

        var yazilan = ShortcutIcons.Refresh(yazici, new[] { bizim, baska }, exe, () => haber++);

        Assert.Equal(1, yazilan);
        Assert.Equal(1, haber);
        Assert.Equal(exe + ",0", yazici.ReadIcon(bizim), ignoreCase: true);
        Assert.Equal(eskiSimge, yazici.ReadIcon(baska), ignoreCase: true);
        Assert.Equal(exe, yazici.ReadTarget(bizim), ignoreCase: true);
    }

    [Fact]
    public void KuruluKlasordenGelenYenilemeDegiskendekiKisayollariYazar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var kok = Kurulum();
        var exe = Path.Combine(kok, LauncherUpdate.ExecutableName);
        var eskiSimge = Path.Combine(Environment.SystemDirectory, "shell32.dll") + ",3";
        var yazici = new ShellShortcuts();
        var konumlar = ShortcutIcons.Locations();
        try
        {
            foreach (var konum in konumlar)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(konum)!);
                yazici.Write(konum, exe, kok, eskiSimge);
            }

            var yazilan = ShortcutIcons.RefreshInstalled(kok);

            Assert.Equal(2, yazilan);
            foreach (var konum in konumlar) Assert.Equal(exe + ",0", yazici.ReadIcon(konum), ignoreCase: true);
        }
        finally
        {
            foreach (var konum in konumlar) File.Delete(konum);
        }
    }
}
