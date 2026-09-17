using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Sessiz fark güncellemesi. Manifest tek başına çekilir, yalnız özeti tutmayan dosyalar
/// arşivden aralık isteğiyle indirilir, hepsi doğrulandıktan sonra uygulama klasörüne
/// geçer. Hiçbir hata açılışı engellemez.
///
/// Manifestin <c>launcher</c> alanı kurulum kökündeki başlatıcıyı da sayar; o satırlar
/// kendi arşivinden inip <see cref="LauncherUpdate"/> üzerinden yerine geçer. Uygulama
/// dosyaları önce yerleşir, başlatıcı en son kurulur: sıra tersine dönerse yeni başlatıcı
/// eski uygulamayı açar. Sıranın kendisi <see cref="UpdateRollout"/> içinde.
///
/// Manifestin <c>shell</c> alanı kurulum kökündeki kabuk klasörünü sayar. O satırlar da
/// başlatıcının arşivinden iner ama geçiş dansına girmez: çalışan süreç onları tutmadığı
/// için <see cref="ShellUpdate"/> doğrudan üstlerine yazar.
///
/// Başlatıcı geçişi burada yapılmaz, yalnız kurulur. Geçişi çıkışta yerine geçecek ikili
/// yapar; döndürülen değer o çağrının gerekip gerekmediğidir.
///
/// Bu çağrı açılış yolunda değil: uygulama ekrana geldikten sonra koşar
/// (<c>Program.cs</c>). İndirme hemen, kurulum klasörden koşan uygulama kapanınca. Eskiden açılış kapısının içindeydi ve bu yüzden bütçesi 90
/// saniyeydi; ölçülen 0.3.0 → 0.4.1 farkı 375 dosya ve 134,8 MB, yani o bütçede
/// bitmesi mümkün değildi. Yarıda kalan sahne de silindiği için her açılış sıfırdan
/// başlıyor, kurulum hiç yakınsamıyordu.
/// </summary>
internal static class Updater
{
    /// <summary>
    /// İndirme dahil tüm güncellemenin üst sınırı. Açılış yolunda olmadığı için geniş:
    /// yavaş hatta yarım kalan iş sahnede kalır, sonraki tur kaldığı yerden sürer.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Aynı anda inen dosya sayısı. Dosyalar tek tek inerken hattın kendisi değil her
    /// isteğin gidiş dönüşü sınırdı: ölçülen 0.3.0 → 0.4.1 farkı 375 dosya, yani 375 ayrı
    /// tur. Şerit sayısı bunu böler; sayı bant genişliğini doyurmaya değil gecikmeyi
    /// örtmeye yetecek kadar, sunucuya yüklenmeyecek kadar küçük.
    /// </summary>
    private const int Lanes = UpdateStaging.LauncherLanes;

    private const string StageDirectoryName = UpdateStaging.StageDirectoryName;

    private const string MutexName = UpdateStaging.MutexName;

    private static readonly TimeSpan ForceWait = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan AppExitWait = TimeSpan.FromDays(7);

    public static bool Run(string baseDirectory, string appDirectory, bool force = false)
    {
        if (!force && !UpdateCheck.AutoUpdateEnabled()) return false;
        if (Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED") == "1") return false;

        StagedUpdate? staged;
        using (var only = new Mutex(initiallyOwned: false, MutexName))
        {
            if (!Hold(only, force ? ForceWait : TimeSpan.Zero)) return false;
            using var cancellation = new CancellationTokenSource(Budget);
            try
            {
                staged = UpdateStaging.StageAsync(
                    baseDirectory, appDirectory, Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE"),
                    Lanes, null, "VidShrink-Launcher", null, cancellation.Token).GetAwaiter().GetResult();
            }
            catch (Exception) { return false; }
            finally { try { only.ReleaseMutex(); } catch (ApplicationException) { } }
        }

        if (staged is null) return false;
        if (Rehearsing) return false;
        return Install(baseDirectory, appDirectory, staged);
    }

    internal const string RehearsalVariable = "VIDSHRINK_UPDATE_PROVA";

    internal static bool Rehearsing =>
        Environment.GetEnvironmentVariable(RehearsalVariable) == "1";

    private static bool Hold(Mutex mutex, TimeSpan wait)
    {
        try { return mutex.WaitOne(wait); }
        catch (AbandonedMutexException) { return true; }
    }

    private static bool Install(string baseDirectory, string appDirectory, StagedUpdate staged)
    {
        var gate = UygulamaKlasoruKapisi.BosalincaAl(appDirectory, AppExitWait);
        if (gate is null) return false;
        try
        {
            using var only = new Mutex(initiallyOwned: false, MutexName);
            if (!Hold(only, ForceWait)) return false;
            try
            {
                var stage = Path.Combine(baseDirectory, StageDirectoryName);
                LauncherUpdate.Stage(stage, baseDirectory, staged.Launcher);
                var applied = UpdateRollout.Apply(stage, baseDirectory, appDirectory, staged.App, staged.Launcher, staged.Manifest, staged.Shell);
                UygulamaKlasoruKapisi.HatayiSil(appDirectory);
                return applied;
            }
            catch (Exception exception)
            {
                UygulamaKlasoruKapisi.HataYaz(appDirectory, exception.Message);
                return false;
            }
            finally { try { only.ReleaseMutex(); } catch (ApplicationException) { } }
        }
        finally
        {
            UygulamaKlasoruKapisi.Birak(gate);
        }
    }
}

