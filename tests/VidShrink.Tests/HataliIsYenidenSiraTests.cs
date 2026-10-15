using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P6, kuyruk penceresi: hata veren iş satırı tek tıkla yeniden sıraya girer (SIK-24), kısa ya da
/// çözülemeyen çıktının hatası satırda söylenir (SIK-18), çıktı adı sayaç, kare hızı ve klasörü taşır
/// (SIK-13), kaynak tarihlerini koruma ayarı varsayılan kapalıdır ve diske gidip döner (SIK-11).
/// </summary>
public sealed class HataliIsYenidenSiraTests
{
    private static string Klasor(string ad)
    {
        var yol = Path.Combine(TestPaths.OutputRoot, "worktree-agent-aaaec832ce0ef59b4", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Sil(string yol)
    {
        try { if (Directory.Exists(yol)) Directory.Delete(yol, true); } catch (IOException) { }
    }

    /// <summary>
    /// Olmayan dosya yoklamada düşer ve hata listesine girer; duraklatılmış sırada satırın düğmesi işi
    /// bekleyenlere geri koyar ve listeyi boşaltır. Kodlama koşmaz.
    /// </summary>
    [Fact]
    public void HataVerenIsTekTiklaYenidenSirayaGirer()
    {
        var klasor = Klasor("yeniden-sira");
        try
        {
            var yol = Path.Combine(klasor, "olmayan.mp4");
            var sonuc = AppHost.Run(() =>
            {
                var window = new ShrinkJobWindow(new[] { yol }, new PlanOptions { TargetMb = 25 }, false, null);
                try
                {
                    window.Begin();
                    var saat = Stopwatch.StartNew();
                    while (window.FailedJobs.Count == 0 && saat.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(20);
                    }
                    Dispatcher.UIThread.RunJobs();
                    var dusen = window.FailedJobs.Select(r => r.Path).ToArray();
                    var gorunur = window.FailedPanel.IsVisible;
                    var satirlar = window.FailedList.Children.Count;
                    var dugme = window.FailedList.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<Button>()).Single();
                    var dugmeAdi = AutomationProperties.GetName(dugme);

                    window.SetPaused(true);
                    dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    return (dusen, gorunur, satirlar, dugmeAdi,
                        bekleyen: window.Pending.Select(r => r.Path).ToArray(),
                        kalan: window.FailedJobs.Count,
                        sonraGorunur: window.FailedPanel.IsVisible,
                        kabul: window.AcceptedCount);
                }
                finally { window.Close(); }
            });

            Assert.Equal(new[] { yol }, sonuc.dusen);
            Assert.True(sonuc.gorunur);
            Assert.Equal(1, sonuc.satirlar);
            Assert.False(string.IsNullOrEmpty(sonuc.dugmeAdi));
            Assert.Equal(new[] { yol }, sonuc.bekleyen);
            Assert.Equal(0, sonuc.kalan);
            Assert.False(sonuc.sonraGorunur);
            Assert.Equal(2, sonuc.kabul);
        }
        finally { Sil(klasor); }
    }

    /// <summary>Sınır dışı satır numarası hiçbir şey yapmaz; hiç düşmemiş pencerede liste gizlidir.</summary>
    [Fact]
    public void SinirDisiYenidenDenemeEtkisiz()
    {
        var (bekleyen, gorunur) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 25 }, false, null);
            try
            {
                window.SetPaused(true);
                window.Begin();
                window.RetryFailed(0);
                window.RetryFailed(-1);
                return (window.Pending.Count, window.FailedPanel.IsVisible);
            }
            finally { window.Close(); }
        });

        Assert.Equal(1, bekleyen);
        Assert.False(gorunur);
    }

    /// <summary>Kısa çıktı okunan ve beklenen süreyi, çözülemeyen çıktı kendi cümlesini yazar; ikisi de genel hata cümlesi değildir.</summary>
    [Fact]
    public void HataSatiriKusuruSoyler()
    {
        var (kisa, cozulemedi, genel) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 25 }, false, null);
            try
            {
                var plan = new EncodePlan();
                EncodeResult Sonuc(CiktiKusuru? kusur) => new(false, "", 0, plan, 1, "x") { Kusur = kusur };
                return (window.HataSatiri(Sonuc(new CiktiKusuru(10, 2)), 25, plan),
                    window.HataSatiri(Sonuc(new CiktiKusuru(10, null)), 25, plan),
                    window.HataSatiri(Sonuc(null), 25, plan));
            }
            finally { window.Close(); }
        });

        Assert.Contains("2", kisa, StringComparison.Ordinal);
        Assert.Contains("10", kisa, StringComparison.Ordinal);
        Assert.NotEqual(genel, kisa);
        Assert.NotEqual(genel, cozulemedi);
        Assert.NotEqual(kisa, cozulemedi);
        Assert.DoesNotContain("{", kisa, StringComparison.Ordinal);
    }

    /// <summary>Kuyruğun çıktı adı yeni yer tutucuları açar: sayaç kuyruktaki sıradan, kare hızı plandan, klasör kaynağın klasöründen.</summary>
    [Fact]
    public void KuyrukAdiSayacKareHiziVeKlasoruTasir()
    {
        var klasor = Klasor("ad-deseni");
        try
        {
            var kaynak = Path.Combine(klasor, "Tatil", "klip.mp4");
            var ayar = new AppSettings { OutputNamePattern = "{ad}_{karehizi}_{klasor}_{sayac}" };
            var cikti = ShrinkJobWindow.UniqueOutputPath(kaynak, ayar, new EncodePlan { Fps = 29.97 }, 25, "mp4", null, 7);
            Assert.Equal("klip_29_97fps_Tatil_007.mp4", Path.GetFileName(cikti));

            var plansiz = ShrinkJobWindow.UniqueOutputPath(kaynak, ayar, null, 25, "mp4", null, 1);
            Assert.Equal("klip_Tatil_001.mp4", Path.GetFileName(plansiz));
        }
        finally { Sil(klasor); }
    }

    /// <summary>Tarih koruma ayarı varsayılan kapalı; açık değer yazılıp geri okunur, alanı olmayan eski dosya kapalı okunur.</summary>
    [Fact]
    public void TarihKorumaAyariGidipDoner()
    {
        var klasor = Klasor("tarih-ayari");
        try
        {
            Assert.False(new AppSettings().KeepSourceDates);
            var yol = Path.Combine(klasor, "settings.json");
            new AppSettings { KeepSourceDates = true }.Save(yol);
            using (var belge = System.Text.Json.JsonDocument.Parse(File.ReadAllText(yol)))
                Assert.True(belge.RootElement.GetProperty("keepSourceDates").GetBoolean());
            Assert.True(AppSettings.Load(yol).KeepSourceDates);

            File.WriteAllText(yol, "{ \"targetMb\": 25 }");
            Assert.False(AppSettings.Load(yol).KeepSourceDates);
        }
        finally { Sil(klasor); }
    }
}
