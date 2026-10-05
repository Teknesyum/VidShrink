using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// İş bitince haber (<see cref="IsBittiBildirimi"/>): küçültme, dönüştürme ya da kuyruk pencere
/// önde değilken biterse tek haber çıkar; pencere öndeyken, ayar kapalıyken ve iptalde çıkmaz.
/// Sistem yüzü hep sahtedir: gerçek pencere yanıp sönmez, gerçek bildirim gösterilmez.
/// </summary>
public sealed class IsBittiBildirimTests
{
    private sealed class SahteYuz : IIsBildirimYuzu
    {
        public List<IsBildirimi> Gelen { get; } = new();
        public void Bildir(Window pencere, IsBildirimi bildirim) => Gelen.Add(bildirim);
    }

    private static readonly string Kok = Path.Combine(TestPaths.OutputRoot, "is-bitti-bildirim");

    private static string Iz(string anahtar, object?[] degerler) => anahtar + "|" + string.Join(",", degerler);

    private static string TemizKlasor(string ad)
    {
        var klasor = Path.Combine(Kok, ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Sil(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
    }

    private static T Kuyrukta<T>(string dil, string ayarDosyasi, string[] yollar, Func<ShrinkJobWindow, SahteYuz, T> olcu)
    {
        return AppHost.Run(() =>
        {
            var kultur = CultureInfo.CurrentUICulture;
            var ayar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            CultureInfo.CurrentUICulture = new CultureInfo(dil);
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayarDosyasi);
            ShrinkJobWindow pencere;
            var yuz = new SahteYuz();
            try
            {
                pencere = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 25 }, false, null)
                {
                    Bildirim = yuz,
                    PencereEtkin = () => false
                };
            }
            finally
            {
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
                CultureInfo.CurrentUICulture = kultur;
            }

            try { return olcu(pencere, yuz); }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        });
    }

    /// <summary>Tek işte başarı ve hata ayrı cümle; cümleye yol değil dosya adı girer.</summary>
    [Fact]
    public void TekIsteBasariVeHataAyriCumle()
    {
        var basari = IsBittiBildirimi.Karar(new[] { new BitenIs(Path.Combine("C:", "Videolar", "kedi.mp4"), IsSonucu.Basarili) }, false, true, Iz);
        var hata = IsBittiBildirimi.Karar(new[] { new BitenIs(Path.Combine("C:", "Videolar", "kedi.mp4"), IsSonucu.Hatali) }, false, true, Iz);

        Assert.Equal("main.notify.done|kedi.mp4", basari!.Metin);
        Assert.False(basari.Hata);
        Assert.Equal("main.notify.failed|kedi.mp4", hata!.Metin);
        Assert.True(hata.Hata);
        Assert.Equal("VidShrink", basari.Baslik);
    }

    /// <summary>Pencere öndeyken, ayar kapalıyken ve iptalde haber yok; aynı iş arkada ve ayar açıkken haber verir.</summary>
    [Theory]
    [InlineData(0, false, true, true)]
    [InlineData(1, false, true, true)]
    [InlineData(0, true, true, false)]
    [InlineData(1, true, true, false)]
    [InlineData(0, false, false, false)]
    [InlineData(1, false, false, false)]
    [InlineData(2, false, true, false)]
    public void HaberYalnizArkadaAcikAyardaVeIptalsizIste(int sonuc, bool etkin, bool ayar, bool beklenen)
    {
        var karar = IsBittiBildirimi.Karar(new[] { new BitenIs("a.mp4", (IsSonucu)sonuc) }, etkin, ayar, Iz);

        Assert.Equal(beklenen, karar is not null);
    }

    /// <summary>Kuyruk tek haberde toplanır: sayılar cümleye girer, iptal edilen sayılmaz.</summary>
    [Fact]
    public void KuyrukSayilariCumleyeGirerIptalSayilmaz()
    {
        BitenIs Is(string ad, IsSonucu sonuc) => new(ad, sonuc);

        var ucBasari = IsBittiBildirimi.Karar(new[] { Is("a.mp4", IsSonucu.Basarili), Is("b.mp4", IsSonucu.Basarili), Is("c.mp4", IsSonucu.Basarili) }, false, true, Iz);
        var karisik = IsBittiBildirimi.Karar(new[] { Is("a.mp4", IsSonucu.Basarili), Is("b.mp4", IsSonucu.Hatali), Is("c.mp4", IsSonucu.Basarili), Is("d.mp4", IsSonucu.Iptal) }, false, true, Iz);
        var hepHata = IsBittiBildirimi.Karar(new[] { Is("a.mp4", IsSonucu.Hatali), Is("b.mp4", IsSonucu.Hatali) }, false, true, Iz);
        var birVeIptal = IsBittiBildirimi.Karar(new[] { Is("a.mp4", IsSonucu.Iptal), Is("b.mp4", IsSonucu.Basarili) }, false, true, Iz);
        var hepIptal = IsBittiBildirimi.Karar(new[] { Is("a.mp4", IsSonucu.Iptal), Is("b.mp4", IsSonucu.Iptal) }, false, true, Iz);
        var bos = IsBittiBildirimi.Karar(Array.Empty<BitenIs>(), false, true, Iz);

        Assert.Equal("main.notify.queue|3", ucBasari!.Metin);
        Assert.False(ucBasari.Hata);
        Assert.Equal("main.notify.queue-failed|2,1", karisik!.Metin);
        Assert.False(karisik.Hata);
        Assert.Equal("main.notify.queue-failed|0,2", hepHata!.Metin);
        Assert.True(hepHata.Hata);
        Assert.Equal("main.notify.done|b.mp4", birVeIptal!.Metin);
        Assert.Null(hepIptal);
        Assert.Null(bos);
    }

    /// <summary>Ayar dosyaya yazılıp geri okunur; alanı olmayan eski dosya açık okunur.</summary>
    [Fact]
    public void AyarGidisDonusVeEskiDosyaAcik()
    {
        var klasor = TemizKlasor("ayar");
        try
        {
            var kapali = Path.Combine(klasor, "kapali.json");
            var acik = Path.Combine(klasor, "acik.json");
            var eski = Path.Combine(klasor, "eski.json");
            new AppSettings { NotifyWhenDone = false }.Save(kapali);
            new AppSettings { NotifyWhenDone = true }.Save(acik);
            File.WriteAllText(eski, "{\"followRecording\": true, \"themeDefaultTeknesyum\": true}");

            Assert.False(AppSettings.Load(kapali).NotifyWhenDone);
            Assert.True(AppSettings.Load(acik).NotifyWhenDone);
            Assert.Contains("\"notifyWhenDone\": false", File.ReadAllText(kapali), StringComparison.Ordinal);
            var okunan = AppSettings.Load(eski);
            Assert.True(okunan.FollowRecording);
            Assert.True(okunan.NotifyWhenDone);
            Assert.True(new AppSettings().NotifyWhenDone);
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Kuyruk penceresi biten işleri tek habere toplar ve sahte yüze bir kez verir. Dosya adının
    /// harfleri cümlede değişmez; ikinci çağrıda aynı işler yeniden duyurulmaz.
    /// </summary>
    [Theory]
    [InlineData("en", "kedi videosu.mp4 is ready.", "Queue finished. Ready: 1, failed: 1.")]
    [InlineData("tr", "kedi videosu.mp4 hazır.", "Kuyruk bitti. Hazır: 1, başarısız: 1.")]
    public void KuyrukPenceresiSahteYuzeBirKezHaberVerir(string dil, string tek, string cift)
    {
        var klasor = TemizKlasor("kuyruk-" + dil);
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            var yol = Path.Combine(klasor, "kedi videosu.mp4");
            var (ilk, tekrar, ikinci) = Kuyrukta(dil, ayar, new[] { yol }, (pencere, yuz) =>
            {
                pencere.IsBitti(yol, IsSonucu.Basarili);
                pencere.HaberVer();
                var birinci = yuz.Gelen.ToArray();
                pencere.HaberVer();
                var sonra = yuz.Gelen.Count;
                pencere.IsBitti(yol, IsSonucu.Basarili);
                pencere.IsBitti(Path.Combine(klasor, "b.mp4"), IsSonucu.Hatali);
                pencere.IsBitti(Path.Combine(klasor, "c.mp4"), IsSonucu.Iptal);
                pencere.HaberVer();
                return (birinci, sonra, yuz.Gelen.ToArray());
            });

            var haber = Assert.Single(ilk);
            Assert.Equal(tek, haber.Metin);
            Assert.False(haber.Hata);
            Assert.Equal(1, tekrar);
            Assert.Equal(2, ikinci.Length);
            Assert.Equal(cift, ikinci[1].Metin);
        }
        finally { Sil(klasor); }
    }

    /// <summary>Pencere öndeyken ve ayar dosyasında kapalıyken kuyruk yüzü çağırmaz; aynı düzenek arkada ve açıkken çağırır.</summary>
    [Theory]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 0)]
    public void KuyrukPenceresiOndeVeKapaliAyardaSusar(bool etkin, bool ayarAcik, int beklenen)
    {
        var klasor = TemizKlasor($"kuyruk-sus-{etkin}-{ayarAcik}");
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            new AppSettings { NotifyWhenDone = ayarAcik }.Save(ayar);
            var yol = Path.Combine(klasor, "a.mp4");
            var sayi = Kuyrukta("en", ayar, new[] { yol }, (pencere, yuz) =>
            {
                pencere.PencereEtkin = () => etkin;
                pencere.IsBitti(yol, IsSonucu.Hatali);
                pencere.HaberVer();
                return yuz.Gelen.Count;
            });

            Assert.Equal(beklenen, sayi);
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Gerçek sıra: olmayan dosya yoklamada düşer, sıra boşalır ve pencere kendiliğinden hata
    /// haberi verir. Kodlama koşmaz.
    /// </summary>
    [Fact]
    public void SiraBosalincaPencereKendiHaberVerir()
    {
        var klasor = TemizKlasor("sira");
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            var yol = Path.Combine(klasor, "olmayan dosya.mp4");
            var (geldi, haberler) = Kuyrukta("en", ayar, new[] { yol }, (pencere, yuz) =>
            {
                pencere.Begin();
                var saat = Stopwatch.StartNew();
                while (yuz.Gelen.Count == 0 && saat.Elapsed < TimeSpan.FromSeconds(30))
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(20);
                }
                Dispatcher.UIThread.RunJobs();
                return (yuz.Gelen.Count > 0, yuz.Gelen.ToArray());
            });

            Assert.True(geldi, "sıra boşaldı ama haber gelmedi");
            var haber = Assert.Single(haberler);
            Assert.Equal("olmayan dosya.mp4 could not be finished.", haber.Metin);
            Assert.True(haber.Hata);
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Ana pencere: haber Ayarlar'daki kutuyu ve pencerenin etkinliğini okur. Kutu kapalıyken,
    /// pencere öndeyken ve iptalde yüz çağrılmaz; kutunun değeri ayara gidip döner.
    /// </summary>
    [Fact]
    public void AnaPencereKutuyuVeEtkinligiOkur()
    {
        var klasor = TemizKlasor("ana");
        try
        {
            var dosya = Path.Combine(klasor, "settings.json");
            var yol = Path.Combine(klasor, "kedi videosu.mp4");
            var sonuc = AppHost.Run(() =>
            {
                var yuz = new SahteYuz();
                var etkin = false;
                var pencere = new MainWindow { SettingsPathOverride = dosya, Bildirim = yuz, PencereEtkin = () => etkin };
                try
                {
                    var kutu = pencere.FindControl<CheckBox>("ChkNotifyWhenDone")!;
                    var varsayilan = kutu.IsChecked == true;
                    kutu.IsChecked = true;

                    pencere.IsBittiHaberi(new BitenIs(yol, IsSonucu.Basarili));
                    pencere.IsBittiHaberi(new BitenIs(yol, IsSonucu.Hatali));
                    var acik = yuz.Gelen.ToArray();

                    pencere.IsBittiHaberi(new BitenIs(yol, IsSonucu.Iptal));
                    var iptal = yuz.Gelen.Count;

                    etkin = true;
                    pencere.IsBittiHaberi(new BitenIs(yol, IsSonucu.Basarili));
                    var onde = yuz.Gelen.Count;
                    etkin = false;

                    kutu.IsChecked = false;
                    pencere.IsBittiHaberi(new BitenIs(yol, IsSonucu.Basarili));
                    var kapali = yuz.Gelen.Count;
                    var toplananKapali = pencere.CaptureAppSettingsForTest().NotifyWhenDone;

                    pencere.RestoreAppSettingsForTest(new AppSettings { NotifyWhenDone = true });
                    var geriAcik = kutu.IsChecked == true;
                    pencere.RestoreAppSettingsForTest(new AppSettings { NotifyWhenDone = false });
                    var geriKapali = kutu.IsChecked == true;

                    return (varsayilan, acik, iptal, onde, kapali, toplananKapali, geriAcik, geriKapali);
                }
                finally { pencere.Close(); }
            });

            Assert.True(sonuc.varsayilan);
            Assert.Equal(2, sonuc.acik.Length);
            Assert.Contains("kedi videosu.mp4", sonuc.acik[0].Metin, StringComparison.Ordinal);
            Assert.Contains("kedi videosu.mp4", sonuc.acik[1].Metin, StringComparison.Ordinal);
            Assert.NotEqual(sonuc.acik[0].Metin, sonuc.acik[1].Metin);
            Assert.False(sonuc.acik[0].Hata);
            Assert.True(sonuc.acik[1].Hata);
            Assert.Equal(2, sonuc.iptal);
            Assert.Equal(2, sonuc.onde);
            Assert.Equal(2, sonuc.kapali);
            Assert.False(sonuc.toplananKapali);
            Assert.True(sonuc.geriAcik);
            Assert.False(sonuc.geriKapali);
        }
        finally { Sil(klasor); }
    }

    /// <summary>Test konağında varsayılan yüz sessiz olandır: gerçek pencere yanıp sönmez.</summary>
    [Fact]
    public void TestKonagindaVarsayilanYuzSessiz()
    {
        var klasor = TemizKlasor("varsayilan");
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            var (yuz, kuyruk) = AppHost.Run(() =>
            {
                var eski = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
                ShrinkJobWindow pencere;
                try { pencere = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 25 }, false, null); }
                finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eski); }
                try { return (IsBittiBildirimi.Varsayilan(), pencere.Bildirim); }
                finally { pencere.Close(); }
            });

            Assert.IsType<SessizIsBildirimYuzu>(yuz);
            Assert.IsType<SessizIsBildirimYuzu>(kuyruk);
        }
        finally { Sil(klasor); }
    }

    private static string Govde(string dosya, string imza)
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", dosya)).Replace("\r\n", "\n");
        var bas = kaynak.IndexOf(imza, StringComparison.Ordinal);
        Assert.True(bas >= 0, $"{dosya}: {imza} yok");
        var son = kaynak.IndexOf("\n    }\n", bas, StringComparison.Ordinal);
        Assert.True(son > bas, $"{dosya}: {imza} gövdesi kapanmıyor");
        return kaynak[bas..son];
    }

    private static string IptalKolu(string govde)
    {
        var bas = govde.IndexOf("catch (OperationCanceledException)", StringComparison.Ordinal);
        Assert.True(bas >= 0, "iptal kolu yok");
        var son = govde.IndexOf("catch (Exception ex)", bas, StringComparison.Ordinal);
        Assert.True(son > bas, "iptal kolunun sonu yok");
        return govde[bas..son];
    }

    /// <summary>
    /// Üç iş yolu da haberi bitişte verir ve iptali ayırır. Gövde ayıklayıcının kör olmadığı,
    /// her gövdede duran başka bir satırla olumlu kontrollü.
    /// </summary>
    [Theory]
    [InlineData("MainWindow.axaml.cs", "private async void OnStart(", "IsBittiHaberi(biten);", "biten = biten with { Sonuc = IsSonucu.Iptal };", "SetRunning(false);")]
    [InlineData("MainWindow.axaml.cs", "private async void OnConvert(", "IsBittiHaberi(biten);", "biten = biten with { Sonuc = IsSonucu.Iptal };", "SetRunning(false);")]
    [InlineData("ShrinkJobWindow.axaml.cs", "private async Task RunOneAsync(", "IsBitti(request.Path, sonuc);", "sonuc = IsSonucu.Iptal;", "_finished++;")]
    public void IsYoluHaberiBitisteVerirVeIptaliAyirir(string dosya, string imza, string haber, string iptal, string kontrol)
    {
        var govde = Govde(dosya, imza);
        var sonKol = govde[govde.LastIndexOf("finally", StringComparison.Ordinal)..];

        Assert.Contains(kontrol, sonKol, StringComparison.Ordinal);
        Assert.Contains(haber, sonKol, StringComparison.Ordinal);
        Assert.Contains(iptal, IptalKolu(govde), StringComparison.Ordinal);
        Assert.DoesNotContain(haber, IptalKolu(govde), StringComparison.Ordinal);
    }

    /// <summary>Altı anahtar 42 dilde; ad ve sayı taşıyanlarda yer tutucu var.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "settings-tab.notify-done.label", "settings-tab.notify-done.hint",
            IsBittiBildirimi.TekBasarili, IsBittiBildirimi.TekHatali, IsBittiBildirimi.KuyrukBasarili, IsBittiBildirimi.KuyrukHatali
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            foreach (var key in anahtarlar[2..])
                Assert.Contains("{0}", values[key], StringComparison.Ordinal);
            Assert.Contains("{1}", values[IsBittiBildirimi.KuyrukHatali], StringComparison.Ordinal);
            Assert.NotEqual(values[IsBittiBildirimi.TekBasarili], values[IsBittiBildirimi.TekHatali]);
        }
    }
}
