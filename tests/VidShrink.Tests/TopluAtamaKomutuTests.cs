using VidShrink.App.Integration;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

public sealed class TopluAtamaKomutuTests
{
    private static string Komut()
        => BulkAssociationCommand.Build(FileAssociation.ProgId, ShellIntegration.MediaExtensions);

    [Fact]
    public void Yirmi_dort_uzantinin_tumunu_nokta_onekiyle_tasir()
    {
        var komut = Komut();
        Assert.Equal(24, ShellIntegration.MediaExtensions.Count);
        foreach (var uzanti in ShellIntegration.MediaExtensions)
        {
            Assert.Contains($"'.{uzanti}'", komut);
        }
    }

    [Fact]
    public void Progid_ve_set_fta_dongusu_var()
    {
        var komut = Komut();
        Assert.Contains(FileAssociation.ProgId, komut);
        Assert.Contains($"Set-FTA '{FileAssociation.ProgId}' $_", komut);
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
}
