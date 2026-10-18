using VidShrink.Core;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.Core.Setup;

namespace VidShrink.Tests;

/// <summary>
/// DAG paketleri (later 107): winget manifestleri <c>packaging/winget/</c>, Scoop manifesti
/// <c>docs/dagitim/scoop/</c>. Ağa çıkmaz, kurucuyu koşturmaz; manifestin söylediği bayrak,
/// sürüm ve varlık adı kodda ve yayın iş akışında aynen var mı, ona bakar.
/// <para>
/// Sessiz kurulumun şartı: winget'in verdiği bayraklar kurucunun tanıdığı bayraklar olmalı
/// (tanınmayan bayrak <c>setup.arg.unknown-option</c> ile düşer) ve panel açılmamalı
/// (<c>SetupLaunch.UsePanel</c> false). <c>--tag</c> sürümü sabitler, yoksa kurucu o günün en
/// son sürümünü kurar.
/// </para>
/// </summary>
public sealed class DagitimManifestTests
{
    private static readonly string Root = TipSources.Root;
    private static string Winget(string name) => File.ReadAllText(Path.Combine(Root, "packaging", "winget", name));
    private static string Installer => Winget("Teknesyum.VidShrinkPlayer.installer.yaml");
    private static string ReleaseWorkflow => File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
    private static string SetupProgram => File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Setup", "Program.cs"));

    private static string Field(string yaml, string key)
    {
        var match = Regex.Match(yaml, $@"(?m)^\s*{Regex.Escape(key)}:\s*(.+?)\s*$");
        Assert.True(match.Success, $"{key} yok");
        return match.Groups[1].Value;
    }

    private static string[] Flags(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(part => part.StartsWith('-')).ToArray();

    private static string ProductVersion =>
        Regex.Match(File.ReadAllText(Path.Combine(Root, "Directory.Build.props")), @"<Version>([^<]+)</Version>").Groups[1].Value;

    private static string Job(string name)
    {
        var text = ReleaseWorkflow;
        var start = Regex.Match(text, $@"(?m)^  {name}:\s*$");
        Assert.True(start.Success, $"{name} isi yok");
        var next = Regex.Match(text[(start.Index + start.Length)..], @"(?m)^  [a-z0-9-]+:\s*$");
        return next.Success ? text.Substring(start.Index, start.Length + next.Index) : text[start.Index..];
    }

    [Fact]
    public void WingetBayraklariniKurucuTaniyorVePanelAcilmiyor()
    {
        var known = Regex.Matches(SetupProgram, @"case ""(--[a-z-]+)""").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Contains("--no-launch", known);

        foreach (var key in new[] { "Silent", "SilentWithProgress", "Custom" })
        {
            var flags = Flags(Field(Installer, key));
            Assert.NotEmpty(flags);
            Assert.All(flags, flag => Assert.Contains(flag, known));
        }

        var silent = Field(Installer, "Silent") + " " + Field(Installer, "Custom");
        var args = silent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.False(SetupLaunch.UsePanel(args));
        Assert.Contains("--no-launch", args);
        Assert.True(SetupLaunch.UsePanel(Array.Empty<string>()));
    }

    [Fact]
    public void WingetSurumuEtiketeVeAdreseSabit()
    {
        var version = ProductVersion;
        Assert.False(string.IsNullOrEmpty(version));
        foreach (var name in new[] { "Teknesyum.VidShrinkPlayer.yaml", "Teknesyum.VidShrinkPlayer.installer.yaml", "Teknesyum.VidShrinkPlayer.locale.en-US.yaml" })
        {
            var yaml = Winget(name);
            Assert.Equal("Teknesyum.VidShrinkPlayer", Field(yaml, "PackageIdentifier"));
            Assert.Equal(version, Field(yaml, "PackageVersion"));
        }

        Assert.Equal($"--tag v{version}", Field(Installer, "Custom"));
        Assert.Equal($"https://github.com/Teknesyum/VidShrink-Player/releases/download/v{version}/{AppIdentity.SetupExecutable}", Field(Installer, "InstallerUrl"));
        Assert.Equal("user", Field(Installer, "Scope"));
        Assert.Matches("^[0-9A-Fa-f]{64}$", Field(Installer, "InstallerSha256"));
        Assert.Contains("Custom: --tag $tag", Job("winget"));
    }

    [Fact]
    public void ScoopVarligiYayinlananArsivVeCliAdiyla()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "docs", "dagitim", "scoop", "vidshrink-player.json")));
        var root = doc.RootElement;
        var version = ProductVersion;
        Assert.Equal(version, root.GetProperty("version").GetString());

        var url = root.GetProperty("architecture").GetProperty("64bit").GetProperty("url").GetString()!;
        Assert.Equal($"https://github.com/Teknesyum/VidShrink-Player/releases/download/v{version}/vidshrink-win-x64.zip", url);
        var auto = root.GetProperty("autoupdate").GetProperty("architecture").GetProperty("64bit");
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player/releases/download/v$version/vidshrink-win-x64.zip", auto.GetProperty("url").GetString());
        Assert.Equal("$baseurl/checksums-win-x64.txt", auto.GetProperty("hash").GetProperty("url").GetString());

        var release = ReleaseWorkflow;
        Assert.Contains("assets/vidshrink-${{ matrix.rid }}.zip", release);
        Assert.Contains("checksums-${{ matrix.rid }}.txt", release);
        Assert.Contains("sha256sum", release);

        var cli = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Cli", "VidShrink.Cli.csproj"));
        var windowsName = Regex.Match(cli, @"<AssemblyName>([^<]+)</AssemblyName>").Groups[1].Value;
        Assert.Equal(windowsName + ".exe", root.GetProperty("bin").GetString());
        Assert.Equal(AppIdentity.AppExecutable, root.GetProperty("shortcuts")[0][0].GetString());
    }

    [Theory]
    [InlineData("winget", "WINGET_TOKEN", "secrets.WINGET_TOKEN")]
    [InlineData("scoop", "SCOOP_TOKEN", "secrets.SCOOP_BUCKET_TOKEN")]
    public void SirYoksaIsYesilAtlar(string job, string variable, string secret)
    {
        var text = Job(job);
        Assert.Contains($"{variable}: ${{{{ {secret} }}}}", text);
        Assert.Contains("needs: [version, release]", text);
        Assert.Contains($"if: env.{variable} == ''", text);

        var steps = Regex.Matches(text, @"(?m)^      - ").Count;
        var guarded = Regex.Matches(text, $@"(?m)^\s+if: env\.{variable} (==|!=) ''\s*$").Count;
        Assert.Equal(steps, guarded);
    }
}
