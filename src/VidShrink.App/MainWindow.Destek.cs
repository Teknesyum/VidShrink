using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// AYR-P3: Hakkında'daki "şimdi denetle" ve bağlantılar, Yenilikler şeridinin sürüm notları
/// düğmesi, tanılama kartının hata bildirimi ve penceresi. Dış adresler <see cref="Destek"/>'ten.
///
/// <para><b>Elle denetim sessiz değildir:</b> sonucu Hakkında'daki satıra yazılır ve
/// <see cref="ElleDenetim"/> her sonucu <see cref="UpdateHealth"/>'e işler. Otomatik güncelleme
/// açıkken de koşar. Yeni sürümü indirmez; bulduğunu var olan yola verir: otomatik açıksa
/// sessiz sahneleme (düşüşünü kendi sayar), kapalıysa rozet ve bildirim şeridi.</para>
/// </summary>
public partial class MainWindow
{
    private ElleDenetimSonucu? _sonElleDenetim;
    private TanilamaWindow? _tanilama;

    /// <summary>Ölçünün sahte istemcisi; boşken yayının manifesti istenir.</summary>
    internal Func<CancellationToken, Task<string?>>? ManifestGetir { get; set; }

    /// <summary>Ölçünün günlük klasörü; boşken <see cref="Gunluk.Klasor"/>.</summary>
    internal string? GunlukKlasoru { get; set; }

    /// <summary>Bu oturumdaki son elle denetim.</summary>
    internal ElleDenetimSonucu? SonElleDenetim => _sonElleDenetim;

    /// <summary>Süren ya da biten son elle denetim işi.</summary>
    internal Task? SonDenetimIsi { get; private set; }

    /// <summary>Son açılan tanılama penceresi.</summary>
    internal TanilamaWindow? SonTanilamaPenceresi => _tanilama;

    internal static string DenetimZamani(DateTimeOffset zaman) =>
        zaman.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Hakkında'daki son denetim satırı: oturumdaki sonuç, yoksa sağlık kaydı.</summary>
    internal string SonDenetimMetni()
    {
        if (_sonElleDenetim is { } sonuc)
        {
            var zaman = DenetimZamani(sonuc.Zaman);
            return sonuc.Durum switch
            {
                ElleDenetimDurumu.Guncel => Say("main.about.check.current", zaman),
                ElleDenetimDurumu.YeniSurum => Say("main.about.check.new", sonuc.Surum, zaman),
                _ => Say("main.about.check.failed", zaman)
            };
        }

        var saglik = UpdateHealth.Load(SettingsPathOverride);
        if (saglik.Failures > 0) return Say("main.about.check.failing", saglik.Failures);
        if (saglik.LastSuccess is { } basari) return Say("main.about.check.last-success", DenetimZamani(basari));
        return Say("main.about.check.never");
    }

    private void SonDenetimiYaz()
    {
        TxtLastCheck.Text = SonDenetimMetni();
        var dustu = _sonElleDenetim?.Durum == ElleDenetimDurumu.Dustu
            || (_sonElleDenetim is null && UpdateHealth.Load(SettingsPathOverride).Failures > 0);
        if (this.TryFindResource(dustu ? "StatusError" : "Hint", out var tema) && tema is ControlTheme kontrol)
            TxtLastCheck.Theme = kontrol;
    }

    private void OnCheckNow(object? sender, RoutedEventArgs e)
    {
        if (SonDenetimIsi is { IsCompleted: false }) return;
        SonDenetimIsi = ElleDenetleAsync();
    }

    private async Task ElleDenetleAsync()
    {
        BtnCheckNow.IsEnabled = false;
        TxtLastCheck.Text = Say("main.update.checking");
        try
        {
            var sonuc = await ElleDenetim.CalistirAsync(
                ManifestGetir ?? ElleDenetim.ManifestiGetir, AppVersion(), SettingsPathOverride, DateTimeOffset.UtcNow, CancellationToken.None);
            _sonElleDenetim = sonuc;
            if (sonuc.Durum == ElleDenetimDurumu.YeniSurum && sonuc.Surum is { } surum) YeniSurumuDevret(surum);
        }
        finally
        {
            BtnCheckNow.IsEnabled = true;
            SonDenetimiYaz();
        }
    }

    private void YeniSurumuDevret(string surum)
    {
        if (OtomatikAcik())
        {
            (SessizIndirici ?? SessizIndirmeyiBaslat)();
            return;
        }
        if (_updateBadgeState is UpdateBadgeState.Downloading or UpdateBadgeState.Ready or UpdateBadgeState.Installing) return;
        SetUpdateBadge(UpdateBadgeState.NewVersion);
        _noticeVersion = surum;
        TxtNoticeVersion.Text = surum;
        UpdateNotice.IsVisible = true;
    }

    private void DisariAc(string? adres) => (DisAcici ?? OpenExternal)(adres);

    private void OnOpenReleaseNotes(object? sender, RoutedEventArgs e) => DisariAc(Destek.SurumNotlari(AppVersion()));

    private void OnOpenLicenses(object? sender, RoutedEventArgs e) => DisariAc(Destek.LisansAdresi);

    private void OnOpenPrivacy(object? sender, RoutedEventArgs e) => DisariAc(Destek.GizlilikAdresi);

    private void OnWhatsNew(object? sender, RoutedEventArgs e) =>
        DisariAc(Destek.SurumNotlari(string.IsNullOrWhiteSpace(TxtAppliedVersion.Text) ? AppVersion() : TxtAppliedVersion.Text));

    private static string HataAdresi() => Destek.HataBildir(AppVersion(), Destek.Isletim(), Destek.GunlukYolu());

    private void OnReportBug(object? sender, RoutedEventArgs e) => DisariAc(HataAdresi());

    /// <summary>Tanılama penceresini açar; günlük uygulama içinde okunur.</summary>
    internal TanilamaWindow ShowDiagnostics()
    {
        var sistem = Destek.SistemBilgisi(AppVersion(), Destek.Isletim(), Destek.GunlukYolu(), UpdateHealth.Load(SettingsPathOverride));
        var pencere = new TanilamaWindow(GunlukKlasoru ?? Gunluk.Klasor, sistem, HataAdresi(), DisariAc);
        _tanilama = pencere;
        pencere.Show(this);
        return pencere;
    }
}
