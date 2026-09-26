using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Otomatik güncelleme: açılış bittikten sonra sessiz sahneleme, program gerçekten kapanırken
/// kurulum. Ağa çıkmaz, gerçek kurulum yapmaz, yeni süreç açmaz: açılış sinyali, indirici,
/// yerinde takas ve başlatıcı yerine sahte iş verilir. Ayar dosyası test klasöründe.
/// </summary>
public sealed class KapanistaGuncellemeTests
{
    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "kapanista-guncelleme");

    private static MainWindow Pencere()
    {
        Directory.CreateDirectory(Klasor);
        return new MainWindow { SettingsPathOverride = Path.Combine(Klasor, Guid.NewGuid().ToString("N") + ".json") };
    }

    private static StagedUpdate Sahne()
    {
        var dosyalar = new[] { new ManifestFile("a.txt", new string('0', 64), 2) };
        var manifest = new ReleaseManifest("9.9.9", "test", DateTimeOffset.UnixEpoch, UpdateCheck.Rid, dosyalar);
        return new StagedUpdate(manifest, Path.Combine(Klasor, "sahne-yok"), dosyalar, Array.Empty<ManifestFile>(), Array.Empty<ManifestFile>());
    }

    private static string KaynakGovdesi(string dosya, string imza)
    {
        var metin = File.ReadAllText(Path.Combine(TipSources.Root, dosya));
        var bas = metin.IndexOf(imza, StringComparison.Ordinal);
        Assert.True(bas >= 0, $"{dosya} içinde {imza} yok");
        var ac = metin.IndexOf('{', bas);
        var derinlik = 0;
        for (var i = ac; i < metin.Length; i++)
        {
            if (metin[i] == '{') derinlik++;
            else if (metin[i] == '}' && --derinlik == 0) return metin.Substring(bas, i - bas + 1);
        }
        throw new InvalidOperationException(imza);
    }

    [Fact]
    public void SessizIndirmeAcilisBitmedenBaslamaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                var acilis = new TaskCompletionSource();
                var indirme = 0;
                p.AcilisBittiBekle = () => acilis.Task;
                p.OtomatikGuncellemeAcik = () => true;
                p.SessizIndirici = () => indirme++;

                var is_ = p.OtomatikGuncellemeyiBaslatAsync();
                for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
                var once = $"once:{indirme}|bitti:{is_.IsCompleted}|rozet:{p.BtnUpdateBadge.IsVisible}";

                acilis.SetResult();
                for (var i = 0; i < 5 && !is_.IsCompleted; i++) Dispatcher.UIThread.RunJobs();
                return new[] { once, $"sonra:{indirme}|bitti:{is_.IsCompleted}" };
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[] { "once:0|bitti:False|rozet:False", "sonra:1|bitti:True" }, sonuc);
    }

    /// <summary>Olumsuz kontrol: otomatik güncelleme kapalıysa açılış bitse de indirilmez.</summary>
    [Fact]
    public void OtomatikKapaliykenIndirmez()
    {
        var indirme = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                var sayac = 0;
                p.AcilisBittiBekle = () => Task.CompletedTask;
                p.OtomatikGuncellemeAcik = () => false;
                p.SessizIndirici = () => sayac++;
                var is_ = p.OtomatikGuncellemeyiBaslatAsync();
                for (var i = 0; i < 5 && !is_.IsCompleted; i++) Dispatcher.UIThread.RunJobs();
                return sayac;
            }
            finally { p.Close(); }
        });

        Assert.Equal(0, indirme);
    }

    /// <summary>
    /// Açılış yolunda indirme yok: pencere yüklenince otomatik açıksa yalnız sahneleme bekleyişi
    /// kurulur, sürüm sorusu ve indirme ondan sonra gelir.
    /// </summary>
    [Fact]
    public void AcilisYoluIndirmeyiBeklemeyeBirakir()
    {
        var yukleme = KaynakGovdesi(Path.Combine("src", "VidShrink.App", "MainWindow.axaml.cs"), "private async void OnWindowLoaded(");
        Assert.Contains("_ = OtomatikGuncellemeyiBaslatAsync();", yukleme);
        Assert.DoesNotContain("SessizIndirmeyiBaslat", yukleme);
        Assert.DoesNotContain("SahneyiIndir", yukleme);

        var baslat = KaynakGovdesi(Path.Combine("src", "VidShrink.App", "MainWindow.KapanistaGuncelleme.cs"), "internal async Task OtomatikGuncellemeyiBaslatAsync(");
        var bekle = baslat.IndexOf("await (AcilisBittiBekle ?? AcilisiBekle)()", StringComparison.Ordinal);
        var indir = baslat.IndexOf("(SessizIndirici ?? SessizIndirmeyiBaslat)()", StringComparison.Ordinal);
        Assert.True(bekle > 0 && indir > bekle, "sahneleme açılış beklemesinden önce başlıyor");
    }

    [Fact]
    public void SessizIndirmeBitinceRozetKapanincaKurulacakDer()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Staged, 1, 1));
                p.SessizIndirmeBitti(Task.FromResult<bool?>(true));
                return (p.BtnUpdateBadge.IsVisible, Ipucu: ToolTip.GetTip(p.BtnUpdateBadge) as string);
            }
            finally { p.Close(); }
        });

        Assert.True(sonuc.IsVisible);
        Assert.Equal(LanguageCatalog.Display(Strings.Get("main.update.ready-on-exit")), sonuc.Ipucu);
    }

    /// <summary>Olumsuz kontrol: sahne çıkmadıysa (güncel, ulaşılamadı) hiçbir şey söylenmez.</summary>
    [Fact]
    public void SahneCikmayanSessizIndirmeSusar()
    {
        var gorunur = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                p.BtnUpdateBadge.IsVisible = false;
                p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Current, 1, 1));
                p.SessizIndirmeBitti(Task.FromResult<bool?>(false));
                return p.BtnUpdateBadge.IsVisible;
            }
            finally { p.Close(); }
        });

        Assert.False(gorunur);
    }

    [Fact]
    public void SahneVarkenKapanisYerindeTakasEderYeniSurumAcmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                var takas = new List<string>();
                var baslatici = 0;
                p.OtomatikGuncellemeAcik = () => true;
                p.YerindeTakas = s => { takas.Add(s.Manifest.Version); return true; };
                p.KapanisBaslaticisi = () => { baslatici++; return true; };
                p.SahnelenenGuncelleme = Sahne();

                var ilk = p.KapanistaGuncelle();
                var ikinci = p.KapanistaGuncelle();
                return $"{ilk}|{ikinci}|takas:{string.Join(",", takas)}|baslatici:{baslatici}|sahne:{p.SahnelenenGuncelleme is not null}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("Takas|Yok|takas:9.9.9|baslatici:0|sahne:False", sonuc);

        var govde = KaynakGovdesi(Path.Combine("src", "VidShrink.App", "MainWindow.KapanistaGuncelleme.cs"), "internal KapanisGuncellemesi KapanistaGuncelle(");
        Assert.DoesNotContain("YeniSurumuAc", govde);
        Assert.DoesNotContain("Process.Start", govde);
    }

    [Fact]
    public void TakasDuserseBaslaticiyaDuser()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                var baslatici = 0;
                p.OtomatikGuncellemeAcik = () => true;
                p.YerindeTakas = _ => false;
                p.KapanisBaslaticisi = () => { baslatici++; return true; };
                p.SahnelenenGuncelleme = Sahne();
                var ilk = p.KapanistaGuncelle();

                p.YerindeTakas = _ => throw new IOException("kilitli");
                p.KapanisBaslaticisi = () => false;
                var ikinci = p.KapanistaGuncelle();
                return $"{ilk}|{ikinci}|baslatici:{baslatici}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("Baslatici|Dustu|baslatici:1", sonuc);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SahneYokkenYaDaOtomatikKapaliykenKapanisDokunmaz(bool sahneVar, bool otomatik)
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = Pencere();
            try
            {
                var cagri = 0;
                p.OtomatikGuncellemeAcik = () => otomatik;
                p.YerindeTakas = _ => { cagri++; return true; };
                p.KapanisBaslaticisi = () => { cagri++; return true; };
                if (sahneVar) p.SahnelenenGuncelleme = Sahne();
                return $"{p.KapanistaGuncelle()}|cagri:{cagri}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("Yok|cagri:0", sonuc);
    }

    /// <summary>
    /// Tek sahneleme kilidi: kilit başkasındayken (elle indirme, ikinci pencere, önceki sürümün
    /// başlatıcısı) sessiz indirme hiçbir şey okumadan <c>null</c> döner. Kaynak yerel ve boş
    /// bir klasör; kilit bırakılırsa bile ağa çıkılmaz, ama ilk rapor kuyruğa düşer.
    /// </summary>
    [Fact]
    public void KilitTutulurkenIkinciIndirmeBaslamaz()
    {
        var kok = Path.Combine(Klasor, "kilit-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(kok, "app");
        Directory.CreateDirectory(app);
        var eski = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE");
        Environment.SetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE", Path.Combine(kok, "kaynak-yok"));
        using var tutuldu = new ManualResetEventSlim();
        using var birak = new ManualResetEventSlim();
        var tutucu = new Thread(() =>
        {
            using var kilit = new Mutex(initiallyOwned: false, UpdateStaging.MutexName);
            var tuttu = false;
            try { tuttu = kilit.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { tuttu = true; }
            tutuldu.Set();
            birak.Wait();
            if (tuttu) kilit.ReleaseMutex();
        }) { IsBackground = true };
        try
        {
            tutucu.Start();
            Assert.True(tutuldu.Wait(TimeSpan.FromSeconds(15)), "test sahneleme kilidini tutamadı");

            var sonuc = AppHost.Run(() =>
            {
                var p = Pencere();
                try
                {
                    var is_ = p.SahneyiIndir(kok, app, CancellationToken.None);
                    Assert.True(is_.Wait(TimeSpan.FromSeconds(20)), "sahneleme işi bitmedi");
                    return $"{is_.Result?.ToString() ?? "null"}|rapor:{p.UpdateReports.Count}|sahne:{p.SahnelenenGuncelleme is not null}";
                }
                finally { p.Close(); }
            });

            Assert.Equal("null|rapor:0|sahne:False", sonuc);
            Assert.False(Directory.Exists(Path.Combine(kok, UpdateStaging.StageDirectoryName)));
        }
        finally
        {
            birak.Set();
            tutucu.Join(TimeSpan.FromSeconds(10));
            Environment.SetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE", eski);
            try { Directory.Delete(kok, recursive: true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Kurulum yalnız gerçek çıkışta: uygulamanın <c>Exit</c> olayı ana pencerenin
    /// <see cref="MainWindow.KapanistaGuncelle"/>'sini çağırır; pencerenin kapanma sorusu
    /// (süren iş onayı) ondan önce gelir ve dokunulmadı.
    /// </summary>
    [Fact]
    public void KurulumUygulamaninCikisinaBagli()
    {
        var app = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "App.axaml.cs"));
        Assert.Matches(new Regex(@"desktop\.Exit \+= \(_, _\) =>\s*\{\s*try \{ main\.KapanistaGuncelle\(\); \}"), app);

        var pencere = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("KapanistaGuncelle", pencere);
    }

    /// <summary>
    /// Başlatıcının arka plan turu kapalı: otomatik güncelleme uygulamanın. Yalnız elle yol
    /// (<c>--update-now</c>, <c>--install-on-exit</c>) indirir; iki indirici aynı sahne için yarışmaz.
    /// </summary>
    [Fact]
    public void BaslaticiArkaPlandaIndirmez()
    {
        var run = KaynakGovdesi(Path.Combine("src", "VidShrink.Launcher", "Updater.cs"), "public static bool Run(");
        Assert.Contains("if (!force) return false;", run);

        var program = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Program.cs"));
        Assert.Contains("args[0] == LauncherUpdate.InstallOnExitArgument", program);
        var kapanis = KaynakGovdesi(Path.Combine("src", "VidShrink.Launcher", "Program.cs"), "private static int KapanistaKur(");
        Assert.DoesNotContain("StartApp", kapanis);
    }

    [Fact]
    public void KapanincaKurulacakMetniButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var ana = Locales.Domain(dil, "main");
            Assert.True(ana.TryGetValue("main.update.ready-on-exit", out var hazir) && !string.IsNullOrWhiteSpace(hazir), $"{dil}: main.update.ready-on-exit yok");
            Assert.NotEqual(ana["main.update.ready"], hazir);
        }
    }
}
