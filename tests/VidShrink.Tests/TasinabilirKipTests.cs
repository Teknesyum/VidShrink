using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.App.Integration;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// AYR-P8: exe'nin yanındaki <see cref="PortableMode.MarkerFileName"/> ayarı ve günlüğü uygulama
/// klasörüne alır, sessiz güncellemeyi ve kayıt defteri/kısayol yazımını kapatır, güncelleme
/// kartının düğmesi indirme sayfasını açar. İşaret yalnız
/// <c>.calisma/worktree-agent-a7cce57401179f65c/tasinabilir/</c> altındaki geçici klasöre konur;
/// test konağının kendi klasörüne, gerçek HKCU'ya ve kullanıcı profiline yazılmaz.
/// </summary>
public sealed class TasinabilirKipTests
{
    private static string Klasor(bool isaretli)
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-a7cce57401179f65c",
            "tasinabilir", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        if (isaretli) File.WriteAllText(Path.Combine(klasor, PortableMode.MarkerFileName), "");
        return klasor;
    }

    private static void Sil(string klasor)
    {
        try { Directory.Delete(klasor, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void IsaretAyarYolunuUygulamaKlasorunAlir()
    {
        var isaretli = Klasor(true);
        var duz = Klasor(false);
        try
        {
            Assert.True(PortableMode.IsActive(isaretli));
            Assert.False(PortableMode.IsActive(duz));
            Assert.False(PortableMode.IsActive(null));

            Assert.Equal(Path.Combine(isaretli, PortableMode.DataFolderName, UpdateSettings.FileName),
                PortableMode.SettingsPath(null, isaretli));
            Assert.Equal(Path.Combine(isaretli, PortableMode.DataFolderName),
                PortableMode.UserFolder(Environment.SpecialFolder.LocalApplicationData, isaretli));

            var profil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                UpdateSettings.FolderName, UpdateSettings.FileName);
            Assert.Equal(profil, PortableMode.SettingsPath(null, duz));
            Assert.Equal(profil, PortableMode.SettingsPath("  ", duz));

            var olcum = Path.Combine(duz, "olcum.json");
            Assert.Equal(olcum, PortableMode.SettingsPath(olcum, isaretli));
        }
        finally { Sil(isaretli); Sil(duz); }
    }

    [Fact]
    public void TasinabilirKopyaKendiniGuncellemez()
    {
        var isaretli = Klasor(true);
        var duz = Klasor(false);
        try
        {
            Assert.False(UpdateCheck.CanSelfUpdateAt(isaretli));
            Assert.Equal(UpdateCheck.CanSelfUpdate, UpdateCheck.CanSelfUpdateAt(duz));
        }
        finally { Sil(isaretli); Sil(duz); }
    }

    [Fact]
    public void KayitDefteriKapisiIsaretliKlasordeKapanir()
    {
        var kok = Path.Combine("C:\\", "Uygulamalar", "VidShrink");
        var exe = Path.Combine(kok, "app", ShellRegistration.AppExecutableName);
        var baslatici = Path.Combine(kok, ShellRegistration.LauncherExecutableName);
        var isaret = Path.Combine(kok, "app", PortableMode.MarkerFileName);

        Assert.True(RegistryWriteGate.Allows(exe, yol => yol == baslatici));
        Assert.False(RegistryWriteGate.Allows(exe, yol => yol == baslatici || yol == isaret));
    }

    [Fact]
    public void KartDugmesiTasinabilirdeIndirmeSayfasiniAcar()
    {
        var isaretli = Klasor(true);
        var duz = Klasor(false);
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var satirlar = new List<string>();
                foreach (var klasor in new[] { isaretli, duz })
                {
                    var p = new MainWindow { UygulamaKlasoru = klasor };
                    try
                    {
                        var acilan = new List<string?>();
                        var indirme = 0;
                        p.DisAcici = acilan.Add;
                        p.UpdateDownloadStarter = () => indirme++;
                        p.TasinabilirKipiniYansit();
                        p.SetUpdateBadge(UpdateBadgeState.NewVersion);
                        var yazi = Equals(p.BtnNoticeUpdate.Content, LanguageCatalog.Display(Strings.Get("main.update.download-page")));
                        var etki = p.TxtAutoUpdateEffect.Text == LanguageCatalog.Display(Strings.Get("settings.update.portable-effect"));
                        p.BtnNoticeUpdate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        satirlar.Add($"tasinabilir:{p.Tasinabilir}|acilan:{string.Join(",", acilan)}|indirme:{indirme}|bayrak:{p.InstallAfterDownload}" +
                            $"|sayfa-yazisi:{yazi}|etki:{etki}|kabuk:{p.ShellMenuPanel.IsVisible}|otomatik:{p.AutoUpdateRow.IsVisible}");
                    }
                    finally { p.Close(); }
                }
                return satirlar;
            });

            Assert.Equal(new[]
            {
                $"tasinabilir:True|acilan:{UpdateCheck.ReleasesPageUrl}|indirme:0|bayrak:False|sayfa-yazisi:True|etki:True|kabuk:False|otomatik:False",
                $"tasinabilir:False|acilan:|indirme:1|bayrak:False|sayfa-yazisi:False|etki:False|kabuk:{ShellMenu.Supported}|otomatik:{UpdateCheck.CanSelfUpdate}",
            }, sonuc);
        }
        finally { Sil(isaretli); Sil(duz); }
    }

    [Fact]
    public void YayinTasinabilirArsiviIsaretleUretir()
    {
        var yayin = File.ReadAllText(Path.Combine(TipSources.Root, ".github", "workflows", "release.yml"));
        Assert.Contains("VidShrink-Portable-${{ matrix.rid }}.zip", yayin, StringComparison.Ordinal);
        Assert.Contains(PortableMode.MarkerFileName, yayin, StringComparison.Ordinal);
        Assert.Contains("checksummed=\"$checksummed VidShrink-Portable-${{ matrix.rid }}.zip\"", yayin, StringComparison.Ordinal);

        foreach (var belge in new[] { "README.md", "README.tr.md" })
        {
            var metin = File.ReadAllText(Path.Combine(TipSources.Root, belge));
            Assert.Contains("VidShrink-Portable-win-x64.zip", metin, StringComparison.Ordinal);
            Assert.Contains(PortableMode.MarkerFileName, metin, StringComparison.Ordinal);
        }
    }
}
