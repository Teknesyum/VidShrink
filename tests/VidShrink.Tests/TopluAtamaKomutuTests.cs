using System.Diagnostics;
using VidShrink.App.Integration;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

public sealed class TopluAtamaKomutuTests
{
    private static string Komut()
        => BulkAssociationCommand.Build(FileAssociation.ProgId, ShellIntegration.BulkDefaultExtensions);

    [Fact]
    public void Genis_kumenin_tumunu_nokta_onekiyle_tasir()
    {
        var komut = Komut();
        Assert.Equal(60, ShellIntegration.BulkDefaultExtensions.Count);
        foreach (var uzanti in ShellIntegration.BulkDefaultExtensions)
        {
            Assert.Contains($"'.{uzanti}'", komut);
        }
    }

    [Fact]
    public void Oynatilamayan_turleri_tasimaz()
    {
        var yasak = new[] { "wmz", "wmd", "wms", "cda", "mid", "midi", "rmi" };
        foreach (var uzanti in yasak)
        {
            Assert.DoesNotContain(uzanti, ShellIntegration.BulkDefaultExtensions);
        }
    }

    [Fact]
    public void Progid_ve_set_fta_dongusu_var()
    {
        var komut = Komut();
        Assert.Contains(FileAssociation.ProgId, komut);
        Assert.Contains($"Set-FTA '{FileAssociation.ProgId}' $e[$i]", komut);
    }

    [Fact]
    public void Ilerleme_cubugu_gosterir()
    {
        var komut = Komut();
        Assert.Contains("Write-Progress", komut);
        Assert.Contains("-PercentComplete", komut);
    }

    [Fact]
    public void Sabitlenmis_url_ve_sha256_tasir()
    {
        var komut = Komut();
        Assert.Contains(BulkAssociationCommand.ScriptUrl, komut);
        Assert.Contains(BulkAssociationCommand.ScriptSha256, komut);
    }

    [Fact]
    public void Butunluk_dogrulamasi_zorunlu()
    {
        var komut = Komut();
        Assert.Contains("Unblock-File", komut);
        Assert.Contains("Get-FileHash -Algorithm SHA256", komut);
        Assert.Contains("-ne $h", komut);
        Assert.Contains("return", komut);
    }

    [Fact]
    public void Powershell_5_1_uyumlu_zincirleme()
    {
        var komut = Komut();
        Assert.DoesNotContain("&&", komut);
        Assert.DoesNotContain("||", komut);
    }

    [Fact]
    public void Eski_windowsta_da_indirir()
    {
        var komut = Komut();
        Assert.Contains("SecurityProtocol -bor 3072", komut);
        Assert.Contains("Invoke-WebRequest -UseBasicParsing", komut);
    }

    [Fact]
    public void Komut_ascii_kalir()
    {
        Assert.All(Komut(), karakter => Assert.True(karakter < 128, $"ASCII disi: {karakter}"));
    }

    [Fact]
    public void Windows_powershell_5_1_hatasiz_ayristirir()
    {
        if (!OperatingSystem.IsWindows()) return;

        var klasor = Path.Combine(TestPaths.OutputRoot, "toplu-atama", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "komut.ps1");
        File.WriteAllText(dosya, Komut());

        var betik = "$e=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('" + dosya + "', [ref]$null, [ref]$e); $e.Count";
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arguman in new[] { "-NoProfile", "-NonInteractive", "-Command", betik }) info.ArgumentList.Add(arguman);

        using var surec = Process.Start(info)!;
        var cikti = surec.StandardOutput.ReadToEnd();
        var hata = surec.StandardError.ReadToEnd();
        surec.WaitForExit();

        Assert.True(surec.ExitCode == 0, hata);
        Assert.Equal("0", cikti.Trim());
    }

    [Fact]
    public void Surec_kapsamli_bypass_ile_baslar()
    {
        var komut = Komut();
        Assert.StartsWith("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force", komut);
    }
}
