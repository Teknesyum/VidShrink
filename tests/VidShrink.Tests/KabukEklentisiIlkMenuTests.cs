using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Windows 11 ilk sağ tık menüsündeki "VidShrink ile Küçült" üst girdisi (later 81). Kabuk
/// eklentisi ikinci bir sınıfla alt komutlu girdi sunar; paket bildirimi her video türüne
/// iki fiil yazar; uygulama hedef etiketlerini <c>ShellLabels</c> anahtarına bırakır. Paket
/// kaydı <see cref="IShellPackage"/> arkasında sahte paketle, etiketler test kökünde ölçülür;
/// gerçek HKCU'ya ve paket deposuna yazılmaz.
/// </summary>
public sealed class KabukEklentisiIlkMenuTests : IDisposable
{
    private static readonly XNamespace Desktop5 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/5";
    private static readonly XNamespace Com = "http://schemas.microsoft.com/appx/manifest/com/windows10";

    private static string ExtensionFolder => Path.Combine(TipSources.Root, "src", "VidShrink.ShellExtension");
    private static string ExtensionSource => File.ReadAllText(Path.Combine(ExtensionFolder, "VidShrink.ShellExtension.cpp"));
    private static string TemplatePath => Path.Combine(ExtensionFolder, "AppxManifest.template.xml");
    private static string ScriptSource => File.ReadAllText(Path.Combine(TipSources.Root, "Install-VidShrink.ps1"));
    private static string LocalesSource => Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    private readonly string _work;
    private readonly string _testKey;

    public KabukEklentisiIlkMenuTests()
    {
        var id = Guid.NewGuid().ToString("n");
        _work = Path.Combine(TipSources.Root, ".calisma", "kabuk-eklentisi-ilk-menu", id);
        _testKey = $@"Software\VidShrink-Test-{Environment.ProcessId}-{id}";
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Registry.CurrentUser.DeleteSubKeyTree(_testKey, false);
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    /// <summary>Eklentinin <c>QuickShrinkMenu</c> dizisi, sırasıyla: anahtar, megabayt, yedek etiket.</summary>
    internal static (string Key, int Megabytes, string Fallback)[] ExtensionTargets()
    {
        var block = Regex.Match(ExtensionSource, @"QuickShrinkMenu\[\] =\s*\{(?<body>.*?)\};", RegexOptions.Singleline);
        Assert.True(block.Success, "Eklentide QuickShrinkMenu dizisi bulunamadı.");
        return Regex.Matches(block.Groups["body"].Value, @"\{ L""(?<key>[^""]+)"", (?<mb>\d+), L""(?<fb>[^""]+)"" \}")
            .Select(m => (m.Groups["key"].Value, int.Parse(m.Groups["mb"].Value), m.Groups["fb"].Value))
            .ToArray();
    }

    private static Guid ExtensionClsid(string name)
    {
        var match = Regex.Match(ExtensionSource,
            name + @" =\s*\{ 0x(?<a>[0-9a-f]{8}), 0x(?<b>[0-9a-f]{4}), 0x(?<c>[0-9a-f]{4}), \{ (?<d>(?:0x[0-9a-f]{2}, ){7}0x[0-9a-f]{2}) \} \};");
        Assert.True(match.Success, $"{name} eklentide bulunamadı.");
        var tail = match.Groups["d"].Value.Split(", ").Select(b => Convert.ToByte(b[2..], 16)).ToArray();
        return new Guid(Convert.ToUInt32(match.Groups["a"].Value, 16), Convert.ToUInt16(match.Groups["b"].Value, 16),
            Convert.ToUInt16(match.Groups["c"].Value, 16), tail[0], tail[1], tail[2], tail[3], tail[4], tail[5], tail[6], tail[7]);
    }

    [Fact]
    public void EklentiSiniflariKurucuSabitleriyleAyni()
    {
        Assert.Equal(Guid.Parse(ShellRegistration.CommandClsid), ExtensionClsid("CLSID_VidShrinkCommand"));
        Assert.Equal(Guid.Parse(ShellRegistration.ShrinkCommandClsid), ExtensionClsid("CLSID_VidShrinkShrinkCommand"));
        Assert.NotEqual(ShellRegistration.CommandClsid, ShellRegistration.ShrinkCommandClsid);
        Assert.Contains($"$shellCommandClsid = '{ShellRegistration.CommandClsid}'", ScriptSource);
        Assert.Contains($"$shellShrinkCommandClsid = '{ShellRegistration.ShrinkCommandClsid}'", ScriptSource);
    }

    /// <summary>
    /// Üst girdi alt komut taşır, alt girdiler başlatıcıyı uygulamanın kabul ettiği bayrakla
    /// çağırır ve etiketi uygulamanın yazdığı değer adından okur.
    /// </summary>
    [Fact]
    public void EklentiAltKomutluGirdiSunar()
    {
        var source = ExtensionSource;
        Assert.Contains("kind == CommandKind::Shrink ? ECF_HASSUBCOMMANDS : ECF_DEFAULT", source);
        Assert.Contains("new (std::nothrow) TargetEnumerator(0)", source);
        Assert.Equal(ShellIntegration.ShrinkFlag, Regex.Match(source, @"ShrinkFlag = L""(?<f>[^""]+)""").Groups["f"].Value);
        Assert.Contains("std::wstring name = L\"target.\";", source);
        Assert.Contains("MenuLabel(L\"shrink\"", source);
        foreach (var target in ShellIntegration.QuickShrinkMenu)
            Assert.Equal("target." + target.Key, target.ShellLabelName);
    }

    [Fact]
    public void SablonIkiSinifiBildirir()
    {
        var classes = XDocument.Load(TemplatePath).Descendants(Com + "Class")
            .Select(c => (string?)c.Attribute("Id")).ToArray();
        Assert.Equal(new[] { ShellRegistration.CommandClsid, ShellRegistration.ShrinkCommandClsid }, classes);
    }

    /// <summary>
    /// Bildirim her video türüne açma ve küçültme fiilini yazar; her fiilin sınıfı bildirimde
    /// tanımlı. Olumsuz kontrol: ses ve çalma listesi türleri bildirime girmez.
    /// </summary>
    [Fact]
    public void BildirimHerVideoTurunaIkiFiilYazar()
    {
        var manifest = XDocument.Parse(ShellRegistration.RenderPackageManifest(File.ReadAllText(TemplatePath)));
        var declared = manifest.Descendants(Com + "Class").Select(c => (string?)c.Attribute("Id")).ToHashSet();
        var types = manifest.Descendants(Desktop5 + "ItemType").ToArray();

        Assert.Equal(ShellIntegration.MediaExtensions.Select(e => "." + e), types.Select(t => (string?)t.Attribute("Type")));
        foreach (var type in types)
        {
            var extension = ((string)type.Attribute("Type")!)[1..];
            var verbs = type.Elements(Desktop5 + "Verb").Select(v => ((string?)v.Attribute("Id"), (string?)v.Attribute("Clsid"))).ToArray();
            Assert.Equal(new (string?, string?)[]
            {
                ("VidShrink" + extension, ShellRegistration.CommandClsid),
                (ShellRegistration.ShrinkMenuKey + extension, ShellRegistration.ShrinkCommandClsid)
            }, verbs);
            Assert.All(verbs, verb => Assert.Contains(verb.Item2, declared));
        }

        Assert.DoesNotContain(types, t => ShellIntegration.PlayerMenuExtensions.Contains(((string)t.Attribute("Type")!)[1..]));
    }

    /// <summary>
    /// PowerShell kurucusunun bildirimi kurucu exe'ninkiyle bayt bayt aynı. Betiğin işlevi
    /// tek başına koşulur; paket kaydı ve kayıt defteri kolu çağrılmaz.
    /// </summary>
    [Fact]
    public void BetikBildirimiKurucuBildirimiyleAyni()
    {
        if (!OperatingSystem.IsWindows()) return;

        var source = ScriptSource;
        var lines = new List<string>
        {
            Regex.Match(source, @"\$shellMenuExtensions = @\([^)]*\)").Value,
            Regex.Match(source, @"\$shellCommandClsid = '[^']+'").Value,
            Regex.Match(source, @"\$shellShrinkCommandClsid = '[^']+'").Value,
            Regex.Match(source, @"\$shellShrinkMenuKeyName = '[^']+'").Value,
            "function Get-Windows11ShellManifest([string]$template) " + FunctionBody(source, "Get-Windows11ShellManifest"),
            "[IO.File]::WriteAllText($args[1], (Get-Windows11ShellManifest ([IO.File]::ReadAllText($args[0]))), [Text.UTF8Encoding]::new($false))"
        };
        Assert.All(lines, line => Assert.False(string.IsNullOrEmpty(line)));

        var harness = Path.Combine(_work, "bildirim.ps1");
        var output = Path.Combine(_work, "AppxManifest.xml");
        File.WriteAllText(harness, string.Join(Environment.NewLine, lines), new System.Text.UTF8Encoding(true));

        var run = PowerShell(harness, TemplatePath, output);
        Assert.True(run.Code == 0, run.Output);

        Assert.Equal(ShellRegistration.RenderPackageManifest(File.ReadAllText(TemplatePath)), File.ReadAllText(output));
    }

    private sealed class FakePackage : IShellPackage
    {
        public bool IsRegistered { get; init; }
        public bool Accepts { get; init; } = true;
        public int Removals { get; private set; }
        public List<(string Manifest, string External)> Registrations { get; } = new();

        public bool Registered() => IsRegistered;

        public void Remove() => Removals++;

        public bool Register(string manifestPath, string externalLocation)
        {
            Registrations.Add((manifestPath, externalLocation));
            return Accepts;
        }
    }

    private string FakeShellFolder()
    {
        var shell = Path.Combine(_work, "kurulum", "shell");
        Directory.CreateDirectory(shell);
        File.Copy(TemplatePath, Path.Combine(shell, "AppxManifest.template.xml"));
        return shell;
    }

    /// <summary>
    /// Kayıt yolu sahte paketle: bildirim kabuk klasörüne düşer, eski paket önce kaldırılır,
    /// kayıt bildirimi ve kurulum kökünü alır. Olumsuz kontroller: kayıtlı paket yoksa kaldırma
    /// çağrılmaz; kayıt reddedilirse sonuç yanlış döner.
    /// </summary>
    [Fact]
    public void PaketKaydiSahtePaketleBildirimiTasir()
    {
        var shell = FakeShellFolder();
        var root = Path.GetDirectoryName(shell)!;

        var registered = new FakePackage { IsRegistered = true };
        Assert.True(ShellRegistration.RegisterPackage(shell, root, registered));
        Assert.Equal(1, registered.Removals);
        var (manifest, external) = Assert.Single(registered.Registrations);
        Assert.Equal(Path.Combine(shell, "AppxManifest.xml"), manifest);
        Assert.Equal(root, external);
        Assert.Equal(ShellRegistration.RenderPackageManifest(File.ReadAllText(TemplatePath)), File.ReadAllText(manifest));
        Assert.Equal(ShellIntegration.MediaExtensions.Count,
            Regex.Matches(File.ReadAllText(manifest), $"Clsid=\"{ShellRegistration.ShrinkCommandClsid}\"").Count);

        var fresh = new FakePackage();
        Assert.True(ShellRegistration.RegisterPackage(shell, root, fresh));
        Assert.Equal(0, fresh.Removals);
        Assert.Single(fresh.Registrations);

        var refused = new FakePackage { Accepts = false };
        Assert.False(ShellRegistration.RegisterPackage(shell, root, refused));
    }

    private static string Catalog(string language, string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(LocalesSource, language, "main.json")));
        return document.RootElement.GetProperty(key).GetString()!;
    }

    [SupportedOSPlatform("windows")]
    private static Dictionary<string, string?> Labels(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key is null
            ? new Dictionary<string, string?>()
            : key.GetValueNames().ToDictionary(n => n, n => key.GetValue(n) as string);
    }

    /// <summary>
    /// Uygulama küçültme girdisini yazınca eklentinin okuduğu altı hedef etiketi de
    /// <c>ShellLabels</c>'a düşer; dil değişince yenilenir, aynı dille ikinci yenileme bir şey
    /// yazmaz. Gerçek <c>ShellLabels</c> dokunulmadan kalır.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void UygulamaHedefEtiketleriniEklentiyeBirakir()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string launcher = @"C:\sahte\VidShrink.exe";
        var labelPath = $@"{_testKey}\{ShellMenu.LabelKey}";
        var realBefore = Labels(ShellMenu.LabelKey);
        var before = Strings.Language;
        Dictionary<string, string?> fr, tr;
        int first, second;
        ShellMenu.TestRoot = _testKey;
        try
        {
            Strings.Use("fr");
            ShellMenu.InstallShrink(launcher, Strings.Get("shell.menu.shrink"));
            fr = Labels(labelPath);

            Strings.Use("tr");
            first = ShellMenu.Relabel(ShellMenu.ShrinkMenuKey, Strings.Get("shell.menu.shrink"));
            second = ShellMenu.Relabel(ShellMenu.ShrinkMenuKey, Strings.Get("shell.menu.shrink"));
            tr = Labels(labelPath);
        }
        finally
        {
            ShellMenu.TestRoot = null;
            Strings.Use(before);
        }

        Assert.Equal(Catalog("fr", "shell.menu.shrink"), fr["shrink"]);
        foreach (var target in ShellIntegration.QuickShrinkMenu)
        {
            Assert.Equal(Catalog("fr", target.LabelKey), fr[target.ShellLabelName]);
            Assert.Equal(Catalog("tr", target.LabelKey), tr[target.ShellLabelName]);
        }

        Assert.Equal(1 + ShellIntegration.QuickShrinkMenu.Count, fr.Count);
        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.Equal(realBefore, Labels(ShellMenu.LabelKey));
    }

    private static (int Code, string Output) PowerShell(params string[] arguments)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File" }.Concat(arguments))
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("powershell.exe başlatılamadı.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error.Result);
    }

    private static string FunctionBody(string source, string name)
    {
        var start = source.IndexOf($"function {name}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Install-VidShrink.ps1 içinde {name} bulunamadı.");
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return source[open..(index + 1)];
        }
        throw new InvalidOperationException($"{name} gövdesi kapanmıyor.");
    }
}
