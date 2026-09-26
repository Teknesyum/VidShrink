using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>Kapanışta kurma denemesinin sonucu; ölçü ve süre kaydı için.</summary>
internal enum KapanisGuncellemesi
{
    /// <summary>Kurulacak sahne yok ya da otomatik güncelleme kapalı; hiçbir şey yapılmadı.</summary>
    Yok,

    /// <summary>Yerinde takas oturdu; yeni sürüm bir sonraki açılışta koşar.</summary>
    Takas,

    /// <summary>Takas düştü; başlatıcı <see cref="LauncherUpdate.InstallOnExitArgument"/> ile bırakıldı.</summary>
    Baslatici,

    /// <summary>Takas da başlatıcı da olmadı; sahne diskte kalır, sonraki kapanış yeniden dener.</summary>
    Dustu
}

/// <summary>
/// Otomatik güncelleme (Windows): açılış bittikten sonra yeni sürüm sessizce sahnelenir,
/// program gerçekten kapanırken kurulur.
///
/// <para><b>Açılışı uzatmaz.</b> İndirme <see cref="Program.AcilisBitti"/>'yi (ilk boya ya da
/// ilk kare) ya da <see cref="Program.BakimYedekBeklemesi"/> yedeğini bekler, sonra
/// <see cref="LowPriorityWork"/>'ün iş parçacığında koşar. Ağ ve disk arayüz iş parçacığına
/// hiç girmez. Panel açılmaz; hazır olunca yalnız rozet "kapanınca kurulacak" der.</para>
///
/// <para><b>Kapanışta kurar.</b> <see cref="KapanistaGuncelle"/> uygulamanın <c>Exit</c>
/// olayından koşar: önce yerinde takas (<see cref="YerindeGuncelleme.Uygula"/>), uygulama
/// yeniden açılmaz. Takas düşerse başlatıcı <see cref="LauncherUpdate.InstallOnExitArgument"/>
/// ile ayrık bırakılır; o da çıkışı bekleyip kurar ve uygulamayı açmaz. Çökme ya da
/// öldürülmede sahne diskte kalır; sonraki oturum onu açılıştan sonra doğrular ve kapanışta
/// kurar, açılışta kurulum yok.</para>
/// </summary>
public partial class MainWindow
{
    private Task<bool?>? _sessizIndirme;
    private bool _kapanistaKurulacak;

    /// <summary>Ölçünün açılış sinyali yerine koyduğu bekleme; boşken ilk görüntü ya da 5 sn.</summary>
    internal Func<Task>? AcilisBittiBekle { get; set; }

    /// <summary>Ölçünün sessiz indirme yerine koyduğu iş; boşken gerçek sahneleme başlar.</summary>
    internal Action? SessizIndirici { get; set; }

    /// <summary>Ölçünün ayar dosyası yerine koyduğu karar; boşken Windows'ta ayar okunur.</summary>
    internal Func<bool>? OtomatikGuncellemeAcik { get; set; }

    /// <summary>Ölçünün yerinde takas yerine koyduğu iş; boşken <see cref="YerindeGuncelleme.Uygula"/>.</summary>
    internal Func<StagedUpdate, bool>? YerindeTakas { get; set; }

    /// <summary>Ölçünün başlatıcı yerine koyduğu iş; boşken başlatıcı kapanış kipinde açılır.</summary>
    internal Func<bool>? KapanisBaslaticisi { get; set; }

    /// <summary>Son kapanış denemesinde takasın sürdüğü zaman; ölçüm ve tanı için.</summary>
    internal TimeSpan SonTakasSuresi { get; private set; }

    /// <summary>Bu oturumda sahnelenmiş, kapanışta kurulacak güncelleme; ölçü sahte bir sahne koyar.</summary>
    internal StagedUpdate? SahnelenenGuncelleme
    {
        get => _stagedUpdate;
        set => _stagedUpdate = value;
    }

    /// <summary>Sessiz indirme sürüyor mu.</summary>
    internal bool SessizIndirmeSuruyor => _sessizIndirme is not null;

    /// <summary>
    /// Açılışta otomatik güncelleme açıkken çağrılır: rozet gizli kalır, sahneleme açılış
    /// bittikten sonra başlar. macOS'ta iş <see cref="MacUpdate"/>'in; burada yalnız rozet gizlenir.
    /// </summary>
    internal async Task OtomatikGuncellemeyiBaslatAsync()
    {
        BtnUpdateBadge.IsVisible = false;
        if (SessizIndirici is null && !OperatingSystem.IsWindows()) return;

        await (AcilisBittiBekle ?? AcilisiBekle)();
        if (!OtomatikAcik()) return;
        (SessizIndirici ?? SessizIndirmeyiBaslat)();
    }

    private static Task AcilisiBekle() =>
        Task.WhenAny(Program.AcilisBitti.Task, Task.Delay(Program.BakimYedekBeklemesi));

    private bool OtomatikAcik()
    {
        if (OtomatikGuncellemeAcik is { } karar) return karar();
        return OperatingSystem.IsWindows() && UpdateCheck.AutoUpdateEnabled(UpdateSettings.Load(SettingsPathOverride));
    }

    /// <summary>
    /// Paneli ve rozeti açmadan sahneler. Kilit başka bir indiricideyse (ikinci pencere,
    /// elle indirme) hiçbir şey indirmez; hata ve ağ yokluğu sessizce geçer.
    /// </summary>
    internal void SessizIndirmeyiBaslat()
    {
        if (_sessizIndirme is not null) return;
        if (_updateBadgeState is UpdateBadgeState.Downloading or UpdateBadgeState.Ready or UpdateBadgeState.Installing) return;
        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (LauncherUpdate.LocateLauncher(appDirectory) is null) return;
        var baseDirectory = Path.GetDirectoryName(appDirectory);
        if (string.IsNullOrEmpty(baseDirectory)) return;

        while (_updateReports.TryDequeue(out _)) { }
        _updateLastPhase = null;
        _stagedUpdate = null;
        _updateCancel?.Dispose();
        var cancel = new CancellationTokenSource();
        _updateCancel = cancel;

        var work = SahneyiIndir(baseDirectory, appDirectory, cancel.Token);
        _sessizIndirme = work;
        work.ContinueWith(
            task => Dispatcher.UIThread.Post(() => SessizIndirmeBitti(task)),
            TaskScheduler.Default);
    }

    /// <summary>Sessiz indirmenin sonu: sahne hazırsa rozet belirir, değilse hiçbir şey söylenmez.</summary>
    internal void SessizIndirmeBitti(Task<bool?> task)
    {
        _sessizIndirme = null;
        DrainUpdateReports();
        var staged = task.Status == TaskStatus.RanToCompletion && task.Result == true && _updateLastPhase == UpdateStagePhase.Staged;
        if (!staged) return;
        _kapanistaKurulacak = true;
        SetUpdateBadge(UpdateBadgeState.Ready);
    }

    /// <summary>Rozetin hazır ipucu: sessiz sahnelemeden geldiyse "kapanınca kurulacak".</summary>
    private string HazirIpucu() => Say(_kapanistaKurulacak ? "main.update.ready-on-exit" : "main.update.ready");

    /// <summary>
    /// Program gerçekten çıkarken koşar (<c>App</c>'in <c>Exit</c> olayı). Sahne yoksa ya da
    /// otomatik güncelleme kapalıysa hiçbir şey yapmaz. Önce yerinde takas; oturursa yeni sürüm
    /// açılmaz. Düşerse başlatıcı ayrık bırakılır ve bu süreç çıkar.
    /// </summary>
    internal KapanisGuncellemesi KapanistaGuncelle()
    {
        var staged = _stagedUpdate;
        if (staged is null) return KapanisGuncellemesi.Yok;
        if (!OtomatikAcik()) return KapanisGuncellemesi.Yok;

        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var baseDirectory = Path.GetDirectoryName(appDirectory) ?? "";
        var takas = YerindeTakas ?? (s => YerindeGuncelleme.Uygula(baseDirectory, appDirectory, s));

        var saat = Stopwatch.StartNew();
        bool oturdu;
        try { oturdu = takas(staged); }
        catch (Exception) { oturdu = false; }
        SonTakasSuresi = saat.Elapsed;

        if (oturdu)
        {
            _stagedUpdate = null;
            return KapanisGuncellemesi.Takas;
        }

        try { return (KapanisBaslaticisi ?? BaslaticiyiKapanistaBirak)() ? KapanisGuncellemesi.Baslatici : KapanisGuncellemesi.Dustu; }
        catch (Exception) { return KapanisGuncellemesi.Dustu; }
    }

    /// <summary>
    /// Başlatıcıyı kapanış kipinde açar. Sürümü uygulamanınkinden eskiyse açılmaz: bayrağı
    /// tanımayan başlatıcı onu açılacak dosya sanıp uygulamayı yeniden doğururdu.
    /// </summary>
    private static bool BaslaticiyiKapanistaBirak()
    {
        var launcher = LauncherUpdate.MaintenanceLauncher(
            AppContext.BaseDirectory, null, UpdateCheck.CurrentVersion(), Program.FileVersionOf);
        if (launcher is null) return false;

        var start = new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher) ?? "",
            UseShellExecute = false
        };
        start.ArgumentList.Add(LauncherUpdate.InstallOnExitArgument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        using var surec = Process.Start(start);
        if (surec is null) return false;
        try { surec.PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch (Exception) { }
        return true;
    }
}
