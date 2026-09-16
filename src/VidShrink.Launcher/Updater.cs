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
/// (<c>Program.cs</c>). Eskiden açılış kapısının içindeydi ve bu yüzden bütçesi 90
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

    /// <param name="force">
    /// Kullanıcı "Yükle" düğmesine bastı: kendiliğinden güncelleme ayarı okunmaz. Ayar yine
    /// yazılmaz, yani elle bir kez yüklemek tercihi değiştirmez.
    /// </param>
    /// <param name="progress">
    /// Paneli besleyen köprü. Verilmezse güncelleme sessiz koşar (açılıştan sonraki tur);
    /// verilirse her aşama kullanıcıya bir cümleyle görünür — indirilen dosya adı dahil.
    /// Kullanıcı elle "Yükle"ye bastığında ne olduğunu adım adım izlemesi bunun üstünden.
    /// </param>
    /// <param name="floor">Panelde bu işin başladığı yüzde.</param>
    /// <param name="ceiling">Panelde bu işin bitiş tavanı.</param>
    public static bool Run(
        string baseDirectory,
        string appDirectory,
        bool force = false,
        InstallProgress? progress = null,
        double floor = 0,
        double ceiling = 100)
    {
        // Ayar kapalıyken manifest bile çekilmez: kapatan kullanıcı ağ turunu da istemiyor.
        if (!force && !UpdateCheck.AutoUpdateEnabled()) return false;
        if (Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED") == "1") return false;

        using var only = new Mutex(initiallyOwned: false, MutexName);
        var held = false;
        try { held = only.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException) { held = true; }
        if (!held) return false;

        using var cancellation = new CancellationTokenSource(Budget);
        try { return RunAsync(baseDirectory, appDirectory, progress, floor, ceiling, cancellation.Token).GetAwaiter().GetResult(); }
        catch (Exception) { return false; }
        finally { try { only.ReleaseMutex(); } catch (ApplicationException) { } }
    }

    /// <summary>Prova kipi: panel birebir aynı koşar ama inen sahne yerine taşınmaz.</summary>
    internal const string RehearsalVariable = "VIDSHRINK_UPDATE_PROVA";

    internal static bool Rehearsing =>
        Environment.GetEnvironmentVariable(RehearsalVariable) == "1";

    private static async Task<bool> RunAsync(
        string baseDirectory,
        string appDirectory,
        InstallProgress? progress,
        double floor,
        double ceiling,
        CancellationToken cancellationToken)
    {
        void Step(double part, double roof, string sentence) =>
            progress?.Step(floor + (ceiling - floor) * part, floor + (ceiling - floor) * roof, sentence);

        var stage = Path.Combine(baseDirectory, StageDirectoryName);
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE");

        void Report(UpdateStageReport report)
        {
            switch (report.Phase)
            {
                case UpdateStagePhase.Manifest:
                    Step(report.Part, report.Roof, "Sürüm listesi alınıyor");
                    break;
                case UpdateStagePhase.Unreachable:
                    Step(report.Part, report.Roof, "Sürüm listesine ulaşılamadı, güncelleme atlandı");
                    break;
                case UpdateStagePhase.Current:
                    Step(report.Part, report.Roof, "Program zaten güncel");
                    break;
                case UpdateStagePhase.Found:
                    Step(report.Part, report.Roof, "Sürüm " + report.Version + " bulundu, " + report.Total + " dosya yenilenecek");
                    break;
                case UpdateStagePhase.Downloaded:
                    var sira = report.Done;
                    var total = report.Total;
                    Step(report.Part, report.Roof, Path.GetFileName(report.File) + " indi (" + sira + "/" + total + ")");
                    break;
            }
        }

        var staged = await UpdateStaging.StageAsync(
            baseDirectory, appDirectory, source, Lanes, null, "VidShrink-Launcher", Report, cancellationToken);
        if (staged is null) return false;

        var manifest = staged.Manifest;
        var changed = staged.App;
        var launcherChanged = staged.Launcher;
        var shellChanged = staged.Shell;

        if (Rehearsing)
        {
            Step(1, 1, "Prova kipi: " + staged.Total + " dosya indirildi, kurulum yapılmadı");
            return false;
        }

        // Başlatıcı yan klasörden çıkarılıyor: bir alttaki Apply yan klasörü siliyor.
        LauncherUpdate.Stage(stage, baseDirectory, launcherChanged);

        Step(0.92, 1, "Dosyalar yerine taşınıyor");
        var applied = UpdateRollout.Apply(stage, baseDirectory, appDirectory, changed, launcherChanged, manifest, shellChanged);
        Step(1, 1, "Sürüm " + manifest.Version + " kuruldu");
        return applied;
    }
}
