using System;
using System.IO;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

public sealed class KaydediciMasaustuSimgeTests : IDisposable
{
    private readonly string _klasor = Path.Combine(GirdiKanit.Root, ".calisma", "masaustu-simge", Guid.NewGuid().ToString("n"));

    private string Durum => Path.Combine(_klasor, DesktopIconGuard.StateFileName);

    public void Dispose()
    {
        try { if (Directory.Exists(_klasor)) Directory.Delete(_klasor, true); }
        catch (IOException) { }
    }

    private sealed class SahteMasaustu : IDesktopIcons
    {
        internal bool? Gorunur { get; set; } = true;

        internal int Cevirme { get; private set; }

        internal bool Atar { get; set; }

        public bool? Visible() => Gorunur;

        public void Toggle()
        {
            Cevirme++;
            if (Atar) throw new InvalidOperationException("sahte");
            if (Gorunur is { } g) Gorunur = !g;
        }
    }

    [Fact]
    public void GorunurSimgelerGizlenirDurumDiskeYazilirBitinceGeriGelir()
    {
        var masaustu = new SahteMasaustu();
        var bekci = new DesktopIconGuard(masaustu, Durum);

        Assert.True(bekci.Hide());
        Assert.False(masaustu.Gorunur);
        Assert.True(File.Exists(Durum));
        Assert.True(bekci.Hide());
        Assert.Equal(1, masaustu.Cevirme);

        Assert.True(bekci.Restore());
        Assert.True(masaustu.Gorunur);
        Assert.False(File.Exists(Durum));
        Assert.Equal(2, masaustu.Cevirme);
        Assert.False(bekci.Restore());
        Assert.Equal(2, masaustu.Cevirme);
    }

    [Fact]
    public void KullaniciZatenGizlediyseDokunulmaz()
    {
        var masaustu = new SahteMasaustu { Gorunur = false };
        var bekci = new DesktopIconGuard(masaustu, Durum);

        Assert.False(bekci.Hide());
        Assert.False(File.Exists(Durum));
        Assert.False(bekci.Restore());
        Assert.False(masaustu.Gorunur);
        Assert.Equal(0, masaustu.Cevirme);
    }

    [Fact]
    public void CokmedenSonrakiAcilisEskiDurumuGeriYukler()
    {
        var masaustu = new SahteMasaustu();
        Assert.True(new DesktopIconGuard(masaustu, Durum).Hide());

        var sonrakiAcilis = new DesktopIconGuard(masaustu, Durum);
        Assert.True(sonrakiAcilis.HiddenByUs);
        Assert.True(sonrakiAcilis.Restore());
        Assert.True(masaustu.Gorunur);
        Assert.False(File.Exists(Durum));

        var ucuncuAcilis = new DesktopIconGuard(masaustu, Durum);
        Assert.False(ucuncuAcilis.Restore());
        Assert.Equal(2, masaustu.Cevirme);
    }

    [Fact]
    public void KayitSirasindaKullaniciGeriAcarsaIkinciKezCevrilmez()
    {
        var masaustu = new SahteMasaustu();
        var bekci = new DesktopIconGuard(masaustu, Durum);
        Assert.True(bekci.Hide());
        masaustu.Gorunur = true;

        Assert.True(bekci.Restore());
        Assert.True(masaustu.Gorunur);
        Assert.Equal(1, masaustu.Cevirme);
        Assert.False(File.Exists(Durum));
    }

    [Fact]
    public void MasaustuBulunamazsaGizlenmezGeriYuklemeDurumuKorur()
    {
        var masaustu = new SahteMasaustu { Gorunur = null };
        var bekci = new DesktopIconGuard(masaustu, Durum);
        Assert.False(bekci.Hide());
        Assert.False(File.Exists(Durum));

        masaustu.Gorunur = true;
        Assert.True(bekci.Hide());
        masaustu.Gorunur = null;
        Assert.False(bekci.Restore());
        Assert.True(File.Exists(Durum));

        masaustu.Gorunur = false;
        Assert.True(bekci.Restore());
        Assert.True(masaustu.Gorunur);
    }

    [Fact]
    public void CevirmeAtarsaDurumKalmaz()
    {
        var masaustu = new SahteMasaustu { Atar = true };
        var bekci = new DesktopIconGuard(masaustu, Durum);

        Assert.False(bekci.Hide());
        Assert.False(File.Exists(Durum));
        Assert.False(bekci.HiddenByUs);
    }

    [Fact]
    public void DurumYoluAyarDosyasininYanindadir()
    {
        var ayar = Path.Combine(_klasor, "recorder-settings.json");
        Assert.Equal(Path.Combine(_klasor, "masaustu-simgeleri.gizlendi"), DesktopIconGuard.DefaultStatePath(ayar));
    }

    [Fact]
    public void AyarVarsayilanKapaliDosyadanOkunur()
    {
        Directory.CreateDirectory(_klasor);
        var yol = Path.Combine(_klasor, "recorder-settings.json");
        Assert.False(RecorderSettings.Load(yol).HideDesktopIcons);

        new RecorderSettings { HideDesktopIcons = true }.Save(yol);
        Assert.True(RecorderSettings.Load(yol).HideDesktopIcons);
        Assert.Equal("true", DosyadakiDeger(yol, "hideDesktopIcons"));
    }

    [Fact]
    public void KutuVarsayilanKapaliKapaliykenMasaustuneDokunulmaz()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var masaustu = new SahteMasaustu();
            var view = new RecorderView(ayarYolu) { DesktopIcons = new DesktopIconGuard(masaustu, Durum) };
            view.ApplyDesktopIconsSupport(true);
            var kutu = Bul<CheckBox>(view, "ChkHideIcons");
            return (isaretli: kutu.IsChecked, etkin: kutu.IsEnabled, gizlendi: view.HideDesktopIconsForRecording(), masaustu.Cevirme);
        }));

        Assert.False(olcu.isaretli);
        Assert.True(olcu.etkin);
        Assert.False(olcu.gizlendi);
        Assert.Equal(0, olcu.Cevirme);
        Assert.False(File.Exists(Durum));
    }

    [Fact]
    public void AcikAyarKayittaGizlerBitinceGeriYukler()
    {
        var olcu = AyarDosyasiyla(ayarYolu =>
        {
            new RecorderSettings { HideDesktopIcons = true }.Save(ayarYolu);
            return AppHost.Run(() =>
            {
                var masaustu = new SahteMasaustu();
                var view = new RecorderView(ayarYolu) { DesktopIcons = new DesktopIconGuard(masaustu, Durum) };
                view.ApplyDesktopIconsSupport(true);
                var isaretli = Bul<CheckBox>(view, "ChkHideIcons").IsChecked;
                var gizlendi = view.HideDesktopIconsForRecording();
                var kayitta = masaustu.Gorunur;
                var dosyaKayitta = File.Exists(Durum);
                var geri = view.RestoreDesktopIcons();
                return (isaretli, gizlendi, kayitta, dosyaKayitta, geri, sonra: masaustu.Gorunur);
            });
        });

        Assert.True(olcu.isaretli);
        Assert.True(olcu.gizlendi);
        Assert.False(olcu.kayitta);
        Assert.True(olcu.dosyaKayitta);
        Assert.True(olcu.geri);
        Assert.True(olcu.sonra);
        Assert.False(File.Exists(Durum));
    }

    [Fact]
    public void WindowsDisindaKutuKapaliVeIpucluGizlemeYapilmaz()
    {
        var olcu = AyarDosyasiyla(ayarYolu =>
        {
            new RecorderSettings { HideDesktopIcons = true }.Save(ayarYolu);
            return AppHost.Run(() =>
            {
                var masaustu = new SahteMasaustu();
                var view = new RecorderView(ayarYolu) { DesktopIcons = new DesktopIconGuard(masaustu, Durum) };
                view.ApplyDesktopIconsSupport(false);
                var kutu = Bul<CheckBox>(view, "ChkHideIcons");
                return (etkin: kutu.IsEnabled, isaretli: kutu.IsChecked, ipucu: ToolTip.GetTip(kutu) as string,
                    gizlendi: view.HideDesktopIconsForRecording(), masaustu.Cevirme);
            });
        });

        Assert.False(olcu.etkin);
        Assert.False(olcu.isaretli);
        Assert.Contains("Windows", olcu.ipucu ?? string.Empty, StringComparison.Ordinal);
        Assert.False(olcu.gizlendi);
        Assert.Equal(0, olcu.Cevirme);
    }
}
