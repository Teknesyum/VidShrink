using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// AYR-P3 arayüzü: Hakkında'daki "şimdi denetle" ve bağlantılar, Yenilikler şeridinin sürüm
/// notları düğmesi, tanılama kartının hata bildirimi ve tanılama penceresi. Ağa çıkmaz,
/// tarayıcı açmaz: manifest sahte istemciden gelir, açılan adres <see cref="MainWindow.DisAcici"/>'ya düşer.
/// Ayar ve günlük klasörü test klasöründe.
/// </summary>
public sealed class TanilamaTests
{
    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "tanilama");

    private static string Manifest(string surum) =>
        $$"""{"version":"{{surum}}","commit":"x","built":"2026-10-01T00:00:00Z","rid":"win-x64","files":[]}""";

    private static string Surum => MainWindow.DisplayVersion(UpdateCheck.CurrentVersion(typeof(MainWindow).Assembly));

    private static MainWindow Pencere(List<string?> acilan)
    {
        Directory.CreateDirectory(Klasor);
        var p = new MainWindow { SettingsPathOverride = Path.Combine(Klasor, Guid.NewGuid().ToString("N") + ".json") };
        p.DisAcici = acilan.Add;
        return p;
    }

    private static void Tikla(Button dugme) => dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Bekle(Task is_)
    {
        for (var i = 0; i < 50 && !is_.IsCompleted; i++) Dispatcher.UIThread.RunJobs();
        Assert.True(is_.IsCompleted);
    }

    private static string Metin(string anahtar, params object?[] degerler) => LanguageCatalog.Display(Strings.Get(anahtar, degerler));

    [Fact]
    public void YeniOgelerHakkindaVeTanilamaKartinda()
    {
        var hakkinda = SettingsTabTests.Panel("AboutPanel");
        var govdeBas = hakkinda.IndexOf("x:Name=\"AboutBody\"", StringComparison.Ordinal);
        var durumBas = hakkinda.IndexOf("x:Name=\"AboutStatus\"", StringComparison.Ordinal);
        Assert.True(govdeBas > 0 && durumBas > govdeBas);
        var govde = hakkinda[govdeBas..durumBas];
        var durum = hakkinda[durumBas..];

        Assert.Contains("x:Name=\"BtnCheckNow\"", durum);
        Assert.Contains("x:Name=\"TxtLastCheck\"", durum);
        foreach (var ad in new[] { "BtnReleaseNotes", "BtnLicenses", "BtnPrivacy" }) Assert.Contains($"x:Name=\"{ad}\"", govde);
        Assert.Contains("x:Name=\"BtnReportBug\"", SettingsTabTests.Panel("DiagnosticLogPanel"));
        Assert.DoesNotContain("BtnCheckNow", SettingsTabTests.Panel("UpdatePanel"));
    }

    /// <summary>Otomatik güncelleme açıkken de denetler; güncel sonuç sağlığa başarı yazar.</summary>
    [Fact]
    public void SimdiDenetleOtomatikAcikkenDeCalisir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var acilan = new List<string?>();
            var p = Pencere(acilan);
            try
            {
                var istek = 0;
                p.OtomatikGuncellemeAcik = () => true;
                p.ManifestGetir = _ => { istek++; return Task.FromResult<string?>(Manifest(Surum)); };
                Tikla(p.BtnCheckNow);
                Bekle(p.SonDenetimIsi!);
                var zaman = MainWindow.DenetimZamani(p.SonElleDenetim!.Zaman);
                return new[]
                {
                    $"istek:{istek}|durum:{p.SonElleDenetim.Durum}|basari:{UpdateHealth.Load(p.SettingsPathOverride).LastSuccess is not null}",
                    p.TxtLastCheck.Text == Metin("main.about.check.current", zaman) ? "metin-tamam" : p.TxtLastCheck.Text ?? ""
                };
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[] { "istek:1|durum:Guncel|basari:True", "metin-tamam" }, sonuc);
    }

    /// <summary>Yeni sürüm: otomatik açıkken sessiz indirme, kapalıyken bildirim şeridi.</summary>
    [Fact]
    public void YeniSurumMevcutYolaDevredilir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var satirlar = new List<string>();
            foreach (var otomatik in new[] { true, false })
            {
                var p = Pencere(new List<string?>());
                try
                {
                    var sessiz = 0;
                    p.OtomatikGuncellemeAcik = () => otomatik;
                    p.SessizIndirici = () => sessiz++;
                    p.ManifestGetir = _ => Task.FromResult<string?>(Manifest("999.0.0"));
                    Tikla(p.BtnCheckNow);
                    Bekle(p.SonDenetimIsi!);
                    var zaman = MainWindow.DenetimZamani(p.SonElleDenetim!.Zaman);
                    var metin = p.TxtLastCheck.Text == Metin("main.about.check.new", "999.0.0", zaman);
                    satirlar.Add($"otomatik:{otomatik}|sessiz:{sessiz}|serit:{p.UpdateNotice.IsVisible}|surum:{p.TxtNoticeVersion.Text}|metin:{metin}");
                }
                finally { p.Close(); }
            }
            return satirlar;
        });

        Assert.Equal(new[]
        {
            "otomatik:True|sessiz:1|serit:False|surum:|metin:True",
            "otomatik:False|sessiz:0|serit:True|surum:999.0.0|metin:True"
        }, sonuc);
    }

    [Fact]
    public void DusenDenetimSayilirVeSoylenir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere(new List<string?>());
            try
            {
                p.OtomatikGuncellemeAcik = () => true;
                p.ManifestGetir = _ => Task.FromResult<string?>(null);
                Tikla(p.BtnCheckNow);
                Bekle(p.SonDenetimIsi!);
                var zaman = MainWindow.DenetimZamani(p.SonElleDenetim!.Zaman);
                var hata = p.TryFindResource("StatusError", out var tema) && ReferenceEquals(tema, p.TxtLastCheck.Theme);
                return $"dusus:{UpdateHealth.Load(p.SettingsPathOverride).Failures}|metin:{p.TxtLastCheck.Text == Metin("main.about.check.failed", zaman)}|hata-temasi:{hata}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("dusus:1|metin:True|hata-temasi:True", sonuc);
    }

    /// <summary>Oturumda denetim yokken satır sağlık kaydından yazılır.</summary>
    [Fact]
    public void SonDenetimSaglikKaydindanOkunur()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere(new List<string?>());
            try
            {
                var satirlar = new List<string> { p.SonDenetimMetni() == Metin("main.about.check.never") ? "hic" : p.SonDenetimMetni() };
                var an = new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero);
                UpdateHealth.RecordSuccess(p.SettingsPathOverride, an);
                satirlar.Add(p.SonDenetimMetni() == Metin("main.about.check.last-success", MainWindow.DenetimZamani(an)) ? "basari" : p.SonDenetimMetni());
                UpdateHealth.RecordFailure(p.SettingsPathOverride, an, "x");
                UpdateHealth.RecordFailure(p.SettingsPathOverride, an, "x");
                satirlar.Add(p.SonDenetimMetni() == Metin("main.about.check.failing", 2) ? "dusus" : p.SonDenetimMetni());
                return satirlar;
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[] { "hic", "basari", "dusus" }, sonuc);
    }

    [Fact]
    public void BaglantilarDogruAdresleriAcar()
    {
        var acilan = AppHost.Run(() =>
        {
            var liste = new List<string?>();
            var p = Pencere(liste);
            try
            {
                Tikla(p.BtnReleaseNotes);
                Tikla(p.BtnLicenses);
                Tikla(p.BtnPrivacy);
                p.TxtAppliedVersion.Text = "1.2.18";
                Tikla(p.BtnWhatsNew);
                Tikla(p.BtnReportBug);
                return liste;
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[]
        {
            Destek.SurumNotlari(Surum),
            Destek.LisansAdresi,
            Destek.GizlilikAdresi,
            "https://github.com/Teknesyum/VidShrink-Player/releases/tag/v1.2.18",
            Destek.HataBildir(Surum, Destek.Isletim(), Destek.GunlukYolu())
        }, acilan);
    }

    /// <summary>
    /// Günlüğü aç artık uygulama içi pencereyi açar: günlük okunur, kopya metni sistem özetiyle
    /// başlar, paket seçilen yere zip olarak iner, pencerenin hata düğmesi aynı adresi açar.
    /// </summary>
    [Fact]
    public void TanilamaPenceresiGunluguGosterirKopyalarVePaketler()
    {
        var kok = Path.Combine(Klasor, Guid.NewGuid().ToString("N"));
        var gunluk = Path.Combine(kok, Gunluk.KlasorAdi);
        Directory.CreateDirectory(gunluk);
        File.WriteAllText(Path.Combine(gunluk, Gunluk.DosyaAdi), "=== kosum ===\ncikis: 0\n");
        var paket = Path.Combine(kok, "paket.zip");

        var sonuc = AppHost.Run(() =>
        {
            var acilan = new List<string?>();
            var p = Pencere(acilan);
            p.GunlukKlasoru = gunluk;
            try
            {
                p.Show();
                Tikla(p.BtnOpenLog);
                var w = p.SonTanilamaPenceresi!;
                try
                {
                    var gosterilen = w.TxtLog.Text ?? "";
                    var kopya = w.KopyaMetni();
                    w.PaketHedefiSec = _ => Task.FromResult<string?>(paket);
                    var kayit = w.PaketKaydetAsync();
                    Bekle(kayit);
                    Tikla(w.BtnReport);
                    return new[]
                    {
                        $"gorunur:{w.IsVisible}|gunluk:{gosterilen.Contains("cikis: 0")}|sistem:{(w.TxtSystem.Text ?? "").StartsWith("vidshrink: ")}",
                        $"kopya:{kopya.StartsWith(w.TxtSystem.Text ?? "?")}&{kopya.Contains("=== kosum ===")}",
                        $"paket:{((Task<bool>)kayit).Result}|durum:{w.TxtStatus.Text == Metin("settings.diag.saved", "paket.zip")}",
                        $"acilan:{acilan.SingleOrDefault() == Destek.HataBildir(Surum, Destek.Isletim(), Destek.GunlukYolu())}"
                    };
                }
                finally { w.Close(); }
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[] { "gorunur:True|gunluk:True|sistem:True", "kopya:True&True", "paket:True|durum:True", "acilan:True" }, sonuc);
        using var zip = ZipFile.OpenRead(paket);
        Assert.Equal(new[] { Destek.SistemDosyasi, Gunluk.DosyaAdi }, zip.Entries.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public void BosGunlukPenceredeSoylenir()
    {
        var kok = Path.Combine(Klasor, Guid.NewGuid().ToString("N"));
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere(new List<string?>());
            p.GunlukKlasoru = Path.Combine(kok, "yok");
            try
            {
                p.Show();
                var w = p.ShowDiagnostics();
                try { return w.TxtLog.Text == Metin("settings.log.empty"); }
                finally { w.Close(); }
            }
            finally { p.Close(); }
        });

        Assert.True(sonuc);
    }
}
