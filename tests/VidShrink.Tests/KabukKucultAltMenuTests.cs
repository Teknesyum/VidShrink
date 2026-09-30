using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// "VidShrink ile Küçült" alt menüsü: ön ayar kütüphanesinden seçilmiş altı hedef, üç yazıcı
/// (PowerShell kurucusu, kurucu exe, uygulama) aynı anahtarı, etiketi ve komutu yazıyor.
/// Kayıt defterine yalnız test anahtarı altında yazılır; gerçek sağ tık dalının değeri her
/// kayıt defteri testinin sonunda değişmemiş olmalı. Alt menü klasik kabuk yoludur ve
/// Windows 11'de yalnız "Daha fazla seçenek göster" altında görünür.
/// </summary>
public sealed class KabukKucultAltMenuTests : IDisposable
{
    private const string RealShrinkBranch =
        @"Software\Classes\SystemFileAssociations\.mp4\shell\" + ShellMenu.ShrinkMenuKey;

    private static string LocalesSource =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    private readonly string _work;
    private readonly string _testKey;

    public KabukKucultAltMenuTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-a7c5e9e38858ffd4c", id);
        _testKey = $@"Software\VidShrink-Test-{Environment.ProcessId}-{id}";
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Registry.CurrentUser.DeleteSubKeyTree(_testKey, false);
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    /// <summary>
    /// Betikteki <c>$shellShrinkTargets</c> tablosu, sırasıyla. Etiketler parantez taşıdığı
    /// için dizi kapanışı satır başındaki <c>)</c>'den bulunur.
    /// </summary>
    internal static (string Key, string PresetId, int Megabytes, string Fallback)[] ScriptTargets()
    {
        var source = File.ReadAllText(Path.Combine(TipSources.Root, "Install-VidShrink.ps1"));
        var blocks = Regex.Matches(source, @"\$shellShrinkTargets = @\((?<body>.*?)\r?\n\)", RegexOptions.Singleline);
        Assert.True(blocks.Count == 1, $"$shellShrinkTargets {blocks.Count} kez bulundu; tek kopya bekleniyor.");

        return Regex.Matches(blocks[0].Groups["body"].Value,
                @"@\{ Key = '(?<key>[^']+)'; Preset = '(?<preset>[^']+)'; Megabytes = (?<mb>\d+); Fallback = '(?<fb>[^']+)' \}")
            .Select(m => (m.Groups["key"].Value, m.Groups["preset"].Value, int.Parse(m.Groups["mb"].Value), m.Groups["fb"].Value))
            .ToArray();
    }

    private static string Catalog(string language, string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(LocalesSource, language, "main.json")));
        return document.RootElement.GetProperty(key).GetString()!;
    }

    [Fact]
    public void AltMenuEnFazlaAltiBoyutluOnAyardanGelir()
    {
        var menu = ShellIntegration.QuickShrinkMenu;
        Assert.InRange(menu.Count, 1, 6);

        foreach (var target in menu)
        {
            var preset = PresetLibrary.BuiltIn.Find(target.PresetId);
            Assert.True(preset is not null, $"{target.PresetId} kütüphanede yok");
            Assert.Contains(preset!.Kind, new[] { PresetKind.General, PresetKind.Platform });
            Assert.True(preset.SizeCapped, target.PresetId);
            Assert.Equal((double)target.Megabytes, preset.TargetMb);
            Assert.Contains(target.Megabytes.ToString(), target.Fallback);
        }

        Assert.Equal(menu.Count, menu.Select(t => t.Megabytes).Distinct().Count());
        Assert.Equal(menu.Count, menu.Select(t => t.Key).Distinct().Count());
    }

    /// <summary>Kabuk alt girdileri ada göre dizer; ad sırası menü sırasıyla aynı olmalı.</summary>
    [Fact]
    public void AnahtarAdiSirasiMenuSirasi()
    {
        var keys = ShellIntegration.QuickShrinkMenu.Select(t => t.Key).ToArray();
        Assert.Equal(keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray(), keys);
        Assert.All(keys, key => Assert.Matches(@"^\d{2}-[a-z0-9-]+$", key));
    }

    [Fact]
    public void AltiAnahtarButunDillerdeDolu()
    {
        var languages = Directory.GetDirectories(LocalesSource).Select(Path.GetFileName).ToArray();
        Assert.True(languages.Length >= 42, $"dil sayısı {languages.Length}");

        foreach (var language in languages)
        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            var text = Catalog(language!, target.LabelKey);
            Assert.False(string.IsNullOrWhiteSpace(text), $"{language}/{target.LabelKey}");
            Assert.Contains(target.Megabytes.ToString(), text);
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ru")]
    [InlineData("ar")]
    public void KurucuEtiketiCeviridenOkur(string language)
    {
        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            var label = ShellRegistration.TargetLabel(target, language, LocalesSource);
            Assert.Equal(Catalog(language, target.LabelKey), label);
            Assert.NotEqual(target.Fallback, label);
        }
    }

    /// <summary>Olumsuz kontrol: çeviri klasörü yoksa ya da dil klasörü yoksa yedek yazılır.</summary>
    [Fact]
    public void CevirisizKurucuYedegeDuser()
    {
        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            Assert.Equal(target.Fallback, ShellRegistration.TargetLabel(target, "fr", null));
            Assert.Equal(target.Fallback, ShellRegistration.TargetLabel(target, "fr", Path.Combine(LocalesSource, "yok")));
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ru")]
    [InlineData("tr")]
    public void UygulamaEtiketiKurucuEtiketiyleAyni(string language)
    {
        var before = Strings.Language;
        try
        {
            Strings.Use(language);
            foreach (var target in ShellIntegration.QuickShrinkMenu)
                Assert.Equal(ShellRegistration.TargetLabel(target, language, LocalesSource), ShellMenu.EntryLabel(target));
        }
        finally
        {
            Strings.Use(before);
        }
    }

    /// <summary>
    /// Üç yazıcı aynı dilde aynı alt menüyü yazar: anahtar adı, <c>MUIVerb</c>,
    /// <c>MultiSelectModel</c> ve komut. Kurulum klasöründe çeviri vardır, etiket
    /// Fransızcadır; yedek etiketle eşitlik bu ölçüyü geçemez. Betik yalnız <c>tr</c>/<c>en</c>/<c>auto</c>
    /// kabul ettiği için sahte kurulumun <c>en</c> kataloğu Fransızca kataloğun kopyasıdır.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void UcYaziciAyniAltMenuyuYazar()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (install, launcher) = FakeInstall("tr");
        CopyCatalog(install, from: "fr", to: "en");
        var scriptRoot = $@"{_testKey}\Betik\Classes";
        var engineRoot = $@"{_testKey}\Motor\Classes";
        var appRoot = $@"{_testKey}\Uygulama";
        var realBefore = RealShrinkTree();

        var script = Script("-ShellMenuOnly", "-MenuLanguage", "en", "-InstallRoot", install, "-RegistryRoot", $@"HKCU:\{scriptRoot}");
        Assert.True(script.Code == 0, script.Output);
        ShellRegistration.WriteMenus(engineRoot, launcher, "en", ShellRegistration.LocalesFolder(install));
        WithApp(appRoot, "fr", () => ShellMenu.InstallShrink(launcher, Strings.Get("shell.menu.shrink")));

        var fromScript = Entries(scriptRoot + @"\SystemFileAssociations");
        var fromEngine = Entries(engineRoot + @"\SystemFileAssociations");
        var fromApp = Entries(appRoot + @"\Software\Classes\SystemFileAssociations");

        Assert.Equal(ShellIntegration.MediaExtensions.Count * ShellIntegration.QuickShrinkMenu.Count, fromScript.Count);
        Assert.Equal(fromScript, fromEngine);
        Assert.Equal(fromScript, fromApp);

        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            var row = fromEngine.Single(e => e.StartsWith($".mp4|{target.Key}|", StringComparison.Ordinal));
            Assert.Equal($".mp4|{target.Key}|{Catalog("fr", target.LabelKey)}|Player|\"{launcher}\" --kucult {target.Megabytes} \"%1\"", row);
        }

        Assert.Equal(realBefore, RealShrinkTree());
    }

    /// <summary>
    /// Kaldırma alt menüyü bütünüyle siler: üç yazıcıdan sonra hiçbir uzantıda
    /// <c>VidShrinkKucult</c> kalmaz. Olumsuz kontrol: silmeden önce anahtar vardı.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void KaldirmaAltMenudenIzBirakmaz()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (install, launcher) = FakeInstall("tr");
        var scriptRoot = $@"{_testKey}\Betik\Classes";
        var engineRoot = $@"{_testKey}\Motor\Classes";
        var appRoot = $@"{_testKey}\Uygulama";
        var realBefore = RealShrinkTree();

        Assert.True(Script("-ShellMenuOnly", "-MenuLanguage", "tr", "-InstallRoot", install, "-RegistryRoot", $@"HKCU:\{scriptRoot}").Code == 0);
        ShellRegistration.WriteMenus(engineRoot, launcher, "tr", ShellRegistration.LocalesFolder(install));
        WithApp(appRoot, "tr", () => ShellMenu.InstallShrink(launcher, "Kucult"));

        var roots = new[]
        {
            scriptRoot + @"\SystemFileAssociations",
            engineRoot + @"\SystemFileAssociations",
            appRoot + @"\Software\Classes\SystemFileAssociations"
        };
        Assert.All(roots, root => Assert.NotEmpty(Entries(root)));

        Assert.True(Script("-RemoveShellMenu", "-RegistryRoot", $@"HKCU:\{scriptRoot}").Code == 0);
        ShellRegistration.RemoveMenus(engineRoot);
        WithApp(appRoot, "tr", () => ShellMenu.RemoveShrink());

        Assert.All(roots, root => Assert.Empty(Entries(root)));
        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            Assert.Null(Open($@"{engineRoot}\SystemFileAssociations\.{extension}\shell\{ShellMenu.ShrinkMenuKey}"));
            Assert.Null(Open($@"{appRoot}\Software\Classes\SystemFileAssociations\.{extension}\shell\{ShellMenu.ShrinkMenuKey}"));
        }

        Assert.Equal(realBefore, RealShrinkTree());
    }

    /// <summary>
    /// Dil değişince girdilerin etiketi de değişir; komut ve komutun altındaki işaret
    /// yerinde kalır (anahtar silinip kurulmaz). Aynı dille ikinci yenileme 0 döner.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void DilDegisinceGirdiEtiketiYenilenirKomutKalir()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string launcher = @"C:\sahte\VidShrink.exe";
        var appRoot = $@"{_testKey}\Uygulama";
        var associations = appRoot + @"\Software\Classes\SystemFileAssociations";
        var realBefore = RealShrinkTree();

        var (first, second, entriesFr) = WithApp(appRoot, "tr", () =>
        {
            ShellMenu.InstallShrink(launcher, Strings.Get("shell.menu.shrink"));
            foreach (var extension in ShellIntegration.MediaExtensions)
            foreach (var target in ShellIntegration.QuickShrinkMenu)
            {
                using var command = Registry.CurrentUser.OpenSubKey(
                    $@"{associations}\.{extension}\shell\{ShellMenu.ShrinkMenuKey}\shell\{target.Key}\command", writable: true);
                command!.SetValue("Isaret", target.Key, RegistryValueKind.String);
            }

            Strings.Use("fr");
            var label = Strings.Get("shell.menu.shrink");
            var once = ShellMenu.Relabel(ShellMenu.ShrinkMenuKey, label);
            var twice = ShellMenu.Relabel(ShellMenu.ShrinkMenuKey, label);
            return (once, twice, Entries(associations));
        });

        var changedEntries = ShellIntegration.QuickShrinkMenu.Count(t => Catalog("fr", t.LabelKey) != Catalog("tr", t.LabelKey));
        Assert.True(changedEntries > 0, "fr ile tr etiketleri ayrışmıyor; ölçü kör");
        Assert.Equal(1 + ShellIntegration.MediaExtensions.Count * (1 + changedEntries), first);
        Assert.Equal(0, second);

        foreach (var extension in ShellIntegration.MediaExtensions)
        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            var path = $@"{associations}\.{extension}\shell\{ShellMenu.ShrinkMenuKey}\shell\{target.Key}";
            using var entry = Open(path);
            using var command = Open(path + @"\command");
            Assert.Equal(Catalog("fr", target.LabelKey), entry?.GetValue("MUIVerb"));
            Assert.Equal(target.Key, command?.GetValue("Isaret"));
            Assert.Equal($"\"{launcher}\" --kucult {target.Megabytes} \"%1\"", command?.GetValue(string.Empty));
        }

        Assert.Equal(ShellIntegration.MediaExtensions.Count * ShellIntegration.QuickShrinkMenu.Count, entriesFr.Count);
        Assert.Equal(realBefore, RealShrinkTree());
    }

    /// <summary>
    /// Menünün her girdisinin komutu kuyruğa kendi hedefiyle düşer; çoklu seçimde her dosya
    /// aynı hedefi alır. Kuyruk duraklatılmış açılır, kodlama başlamaz.
    /// </summary>
    [Fact]
    public void HerGirdiKuyrugaKendiHedefiyleDuser()
    {
        var files = new[] { "a.mp4", "b b.mkv", "c.mov" }.Select(name => Path.Combine(_work, name)).ToArray();
        foreach (var file in files) File.WriteAllBytes(file, new byte[16]);
        var info = new MediaInfo
        {
            FilePath = files[0], FileSizeBytes = 16, DurationSeconds = 10, Width = 640, Height = 360,
            Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1_000_000
        };

        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            var args = new List<string> { ShellIntegration.ShrinkFlag, target.Megabytes.ToString() };
            args.AddRange(files);
            var startup = Program.StartupFor(args);

            Assert.NotNull(startup);
            Assert.Null(startup!.Problem);

            var (paths, targets, planTargets) = AppHost.Run(() =>
            {
                var window = new ShrinkJobWindow(startup, null);
                try
                {
                    window.SetPaused(true);
                    window.Begin();
                    return (window.Pending.Select(r => r.Path).ToArray(),
                        window.Pending.Select(r => r.TargetMegabytes).ToArray(),
                        window.Pending.Select(r => window.OptionsFor(r, info).TargetMb).ToArray());
                }
                finally { window.Close(); }
            });

            Assert.Equal(files, paths);
            Assert.All(targets, mb => Assert.Equal(target.Megabytes, mb));
            Assert.All(planTargets, mb => Assert.Equal(target.Megabytes, mb));
        }
    }

    /// <summary>Olumsuz kontrol: menüde olmayan hedef kuyruğa girmez, gerekçe taşır.</summary>
    [Theory]
    [InlineData(17)]
    [InlineData(99)]
    public void MenudeOlmayanHedefReddedilir(int megabytes)
    {
        var file = Path.Combine(_work, "a.mp4");
        File.WriteAllBytes(file, new byte[16]);

        var startup = Program.StartupFor(new[] { ShellIntegration.ShrinkFlag, megabytes.ToString(), file });

        Assert.NotNull(startup);
        Assert.Empty(startup!.Items);
        Assert.Equal(ShrinkArgumentProblem.TargetNotInQuickList, startup.Problem);
    }

    private static void CopyCatalog(string install, string from, string to)
    {
        var folder = Path.Combine(ShellRegistration.LocalesFolder(install), to);
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(LocalesSource, from, "main.json"), Path.Combine(folder, "main.json"));
    }

    private (string Install, string Launcher) FakeInstall(params string[] languages)
    {
        var install = Path.Combine(_work, "kurulum");
        foreach (var language in languages)
        {
            var folder = Path.Combine(ShellRegistration.LocalesFolder(install), language);
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(LocalesSource, language, "main.json"), Path.Combine(folder, "main.json"));
        }

        var launcher = Path.Combine(install, "VidShrink.exe");
        File.WriteAllText(launcher, "baslatici");
        return (install, launcher);
    }

    [SupportedOSPlatform("windows")]
    private static T WithApp<T>(string root, string language, Func<T> body)
    {
        var before = Strings.Language;
        ShellMenu.TestRoot = root;
        try
        {
            Strings.Use(language);
            return body();
        }
        finally
        {
            ShellMenu.TestRoot = null;
            Strings.Use(before);
        }
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey? Open(string path) => Registry.CurrentUser.OpenSubKey(path);

    /// <summary>Alt menü girdileri, uzantı ve anahtar sırasıyla: <c>uzantı|anahtar|etiket|çoklu seçim|komut</c>.</summary>
    [SupportedOSPlatform("windows")]
    private static List<string> Entries(string associations)
    {
        var rows = new List<string>();
        using var root = Registry.CurrentUser.OpenSubKey(associations);
        if (root is null) return rows;

        foreach (var extension in root.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            using var shell = root.OpenSubKey($@"{extension}\shell\{ShellMenu.ShrinkMenuKey}\shell");
            if (shell is null) continue;
            foreach (var name in shell.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                using var entry = shell.OpenSubKey(name);
                using var command = shell.OpenSubKey(name + @"\command");
                rows.Add($"{extension}|{name}|{entry?.GetValue("MUIVerb")}|{entry?.GetValue("MultiSelectModel")}|{command?.GetValue(string.Empty)}");
            }
        }

        return rows;
    }

    [SupportedOSPlatform("windows")]
    private static string RealShrinkTree()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RealShrinkBranch);
        if (key is null) return "";
        var names = string.Join(",", key.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal));
        using var shell = key.OpenSubKey("shell");
        var entries = shell is null ? "" : string.Join(",", shell.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal));
        return $"{key.GetValue("MUIVerb")}|{names}|{entries}";
    }

    private static (int Code, string Output) Script(params string[] arguments)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = TipSources.Root
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(TipSources.Root, "Install-VidShrink.ps1") }.Concat(arguments))
            info.ArgumentList.Add(argument);
        info.Environment.Remove("PSModulePath");
        info.Environment.Remove("VIDSHRINK_INSTALL_ROOT");

        using var process = Process.Start(info) ?? throw new InvalidOperationException("powershell.exe başlatılamadı.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error.Result);
    }
}
