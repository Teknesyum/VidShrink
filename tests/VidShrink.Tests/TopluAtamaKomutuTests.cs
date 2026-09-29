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
    public void Surec_kapsamli_bypass_ile_baslar()
    {
        var komut = Komut();
        Assert.StartsWith("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force", komut);
    }
}
