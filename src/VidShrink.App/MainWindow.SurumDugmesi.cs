using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private readonly DispatcherTimer _surumBildirimZamani = new();

    internal Task? SurumDenetimIsi { get; private set; }

    internal static string SurumYazisi() => "v" + AppVersion();

    private void SurumDugmesiniKur()
    {
        BtnVersion.Content = SurumYazisi();
        _surumBildirimZamani.Tick += (_, _) => SurumBildiriminiKapat();
    }

    private void OnVersionClicked(object? sender, RoutedEventArgs e)
    {
        if (SurumDenetimIsi is { IsCompleted: false }) return;
        SurumDenetimIsi = SurumDenetleAsync();
    }

    private async Task SurumDenetleAsync()
    {
        ElleDenetimSonucu sonuc;
        try
        {
            sonuc = await ElleDenetim.CalistirAsync(
                ManifestGetir ?? ElleDenetim.ManifestiGetir, AppVersion(), SettingsPathOverride, DateTimeOffset.UtcNow, CancellationToken.None);
        }
        catch (Exception hata)
        {
            SurumBildir(HataSatiri(hata.Message));
            return;
        }

        _sonElleDenetim = sonuc;
        SonDenetimiYaz();
        if (sonuc.Durum == ElleDenetimDurumu.Guncel)
            SurumBildir($"{Etiketler.Get("update.current")} ({SurumYazisi()})");
        else if (sonuc.Durum == ElleDenetimDurumu.YeniSurum && sonuc.Surum is { } surum)
            YeniSurumuDevret(surum);
        else
            SurumBildir(HataSatiri(sonuc.Sebep));
    }

    private static string HataSatiri(string? sebep)
    {
        var baslik = Etiketler.Get("update.error");
        if (string.IsNullOrWhiteSpace(sebep)) return baslik;
        var satir = sebep.ReplaceLineEndings(" ").Trim();
        return $"{baslik}: {satir}";
    }

    private void SurumBildir(string metin)
    {
        TxtVersionToast.Text = metin;
        VersionToast.IsVisible = true;
        _surumBildirimZamani.Interval = this.TryFindResource("ToastLifetime", out var omur) && omur is TimeSpan sure
            ? sure
            : TimeSpan.FromSeconds(6);
        _surumBildirimZamani.Stop();
        _surumBildirimZamani.Start();
    }

    internal void SurumBildiriminiKapat()
    {
        _surumBildirimZamani.Stop();
        VersionToast.IsVisible = false;
    }
}
