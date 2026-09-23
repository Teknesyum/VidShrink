using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using VidShrink.App.Playback;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// İki adımlı güncelleme: önce indir, sonra kullanıcı "Yükle"ye basınca kur.
///
/// <para><b>İndirme oynatmayı yavaşlatmaz.</b> Bütün iş <see cref="LowPriorityWork"/>'ün
/// en düşük öncelikli tek iş parçacığında koşar: ağdan okuma, arşivden açma, özet ve diske
/// yazma. Dosyalar tek şeritte, sırayla iner; iş parçacığı havuzu ve arayüz iş parçacığı
/// kullanılmaz. Gövde başlıklardan sonra 64 KB'lık parçalarla okunur ve oynatıcı oynarken
/// <see cref="DownloadThrottle"/> hızı sınırlar. İş tarafı arayüze hiçbir şey göndermez:
/// bildirimler bir kuyruğa düşer, arayüz onu yalnız panel açıkken kendi kare saatinde
/// boşaltır.</para>
///
/// <para>Kurulum burada yapılmaz. "Yükle" mevcut akışı (<see cref="OnInstallUpdate"/>)
/// çalıştırır; başlatıcı aynı sahneyi bulur, özeti tutan dosyaları yeniden indirmez.</para>
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Oynatıcı oynarken indirmenin saniyedeki bayt tavanı. Ölçülmüş bir sayı değil; boşta
    /// sınır yoktur.
    /// </summary>
    private const long PlaybackDownloadBytesPerSecond = 4L * 1024 * 1024;

    private readonly ConcurrentQueue<UpdateStageReport> _updateReports = new();
    private readonly Stopwatch _updateClock = new();
    private InstallProgress? _updateProgress;
    private DispatcherTimer? _updateFrame;
    private UpdateStagePhase? _updateLastPhase;
    private int _updateLinesShown;
    private bool _updateNoticeWatched;
    private CancellationTokenSource? _updateCancel;

    /// <summary>
    /// İndirmeyi başlatır. Başlatıcısı olmayan kurulumda indirilecek yer yok; o zaman
    /// hiçbir şey yapmaz ve panelin düğmesi eskisi gibi yayın sayfasını açar.
    /// </summary>
    private void StartUpdateDownload()
    {
        if (_updateBadgeState is UpdateBadgeState.Downloading or UpdateBadgeState.Ready or UpdateBadgeState.Installing) return;
        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (LauncherUpdate.LocateLauncher(appDirectory) is null) return;
        var baseDirectory = Path.GetDirectoryName(appDirectory);
        if (string.IsNullOrEmpty(baseDirectory)) return;

        while (_updateReports.TryDequeue(out _)) { }
        _updateLastPhase = null;
        SetUpdateBadge(UpdateBadgeState.Downloading);
        ShowUpdateProgress(new InstallProgress());

        var player = Player;
        var throttle = new DownloadThrottle(PlaybackDownloadBytesPerSecond, () => player.IsPlaying);
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE");
        var reports = _updateReports;
        _updateCancel?.Dispose();
        var cancel = new CancellationTokenSource();
        _updateCancel = cancel;
        var token = cancel.Token;

        var work = LowPriorityWork.Run<bool?>(nameof(StartUpdateDownload), async () =>
        {
            using var only = new Mutex(initiallyOwned: false, UpdateStaging.MutexName);
            var held = false;
            try { held = only.WaitOne(TimeSpan.Zero); }
            catch (AbandonedMutexException) { held = true; }
            if (!held) return null;

            try
            {
                var staged = await UpdateStaging.StageAsync(
                    baseDirectory, appDirectory, source, 1, throttle, nameof(VidShrink),
                    reports.Enqueue, token);
                return staged is not null;
            }
            catch (OperationCanceledException)
            {
                UpdateStaging.DiscardPartials(baseDirectory);
                throw;
            }
            finally
            {
                only.ReleaseMutex();
            }
        });

        work.ContinueWith(
            task => Dispatcher.UIThread.Post(() => OnUpdateDownloadFinished(task)),
            TaskScheduler.Default);
    }

    /// <summary>
    /// İnen güncellemeyi durdurur. İş parçacığı yarım dosyaları siler ve mutex'i bırakır;
    /// rozet "Güncelleme"ye döner, sonraki tık indirmeyi yeniden başlatır ve özeti tutan
    /// dosyaları atlar.
    /// </summary>
    internal void CancelUpdateDownload()
    {
        if (_updateBadgeState != UpdateBadgeState.Downloading) return;
        var cancel = _updateCancel;
        if (cancel is null || cancel.IsCancellationRequested) return;
        cancel.Cancel();
        RefreshUpdateNoticeButton();
    }

    /// <summary>
    /// Panelin öncü cümlesi ve birincil düğmesi rozetin durumunu izler: inmemişken "İndir",
    /// inerken kapalı, indikten sonra "Yükle".
    /// </summary>
    private void RefreshUpdateNoticeButton()
    {
        var state = _updateBadgeState;
        BtnNoticeInstall.Content = Say(state switch
        {
            UpdateBadgeState.Ready => "main.action.install",
            UpdateBadgeState.Downloading => "main.action.cancel",
            _ => "main.action.download"
        });
        BtnNoticeInstall.IsEnabled = state switch
        {
            UpdateBadgeState.Installing => false,
            UpdateBadgeState.Downloading => _updateCancel is { IsCancellationRequested: false },
            _ => true
        };
        TxtNoticeLead.Text = Say(state switch
        {
            UpdateBadgeState.Downloading => "main.update.downloading",
            UpdateBadgeState.Ready => "main.update.ready",
            _ => "main.update.available"
        });
        BtnNoticeDismiss.IsVisible = !UpdateNoticeLocked;
    }

    /// <summary>İndirme ya da kurulum sürerken panel kapanmaz; × gizlenir, kapatma yolları bekler.</summary>
    internal bool UpdateNoticeLocked => _updateBadgeState is UpdateBadgeState.Downloading or UpdateBadgeState.Installing;

    private void OnUpdateDownloadFinished(Task<bool?> task)
    {
        DrainUpdateReports();
        var progress = _updateProgress;
        if (progress is null) return;

        var staged = task.Status == TaskStatus.RanToCompletion && task.Result == true && _updateLastPhase == UpdateStagePhase.Staged;
        if (staged)
        {
            progress.Finish(true, Say("main.update.ready"));
            SetUpdateBadge(UpdateBadgeState.Ready);
        }
        else if (task.IsCanceled)
        {
            progress.Finish(false, Say("main.update.cancelled"));
            SetUpdateBadge(UpdateBadgeState.NewVersion);
        }
        else if (task.Status == TaskStatus.RanToCompletion && task.Result == false && _updateLastPhase == UpdateStagePhase.Current)
        {
            progress.Finish(true, Say("main.update.log.current"));
            SetUpdateBadge(UpdateBadgeState.UpToDate);
        }
        else
        {
            progress.Finish(false, Say("main.update.failed"));
            SetUpdateBadge(UpdateBadgeState.NewVersion);
        }

        StartUpdateFrames();
    }

    /// <summary>Kuyruktaki bildirimleri panelin cümlelerine çevirir; arayüz iş parçacığında.</summary>
    private void DrainUpdateReports()
    {
        var progress = _updateProgress;
        while (_updateReports.TryDequeue(out var report))
        {
            _updateLastPhase = report.Phase;
            if (progress is null) continue;

            var sentence = report.Phase switch
            {
                UpdateStagePhase.Manifest => Say("main.update.log.manifest"),
                UpdateStagePhase.Unreachable => Say("main.update.log.unreachable"),
                UpdateStagePhase.Current => Say("main.update.log.current"),
                UpdateStagePhase.Found => Say("main.update.log.found", report.Version, report.Total),
                UpdateStagePhase.Downloaded => Path.GetFileName(report.File) + "  "
                    + report.Done.ToString(CultureInfo.InvariantCulture) + "/"
                    + report.Total.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
            if (sentence is null) continue;
            progress.Step(report.Part * 100, report.Roof * 100, sentence);
        }
    }

    /// <summary>
    /// Paneli bir ilerleme köprüsüne bağlar: günlük alanı ve çubuk görünür olur, perde
    /// yağmaya başlar. Ölçüm de örnek bir köprüyü buradan verir.
    /// </summary>
    internal void ShowUpdateProgress(InstallProgress progress)
    {
        _updateProgress = progress;
        _updateLinesShown = 0;
        UpdateLogLines.Children.Clear();
        _updateBarPercent = 0;
        UpdateBarFill.Width = 0;
        UpdateLogArea.Height = InstallProgress.LogLines * Scalar("LineHeightBody", 0);
        UpdateLogArea.IsVisible = true;
        UpdateBarTrack.IsVisible = true;
        UpdateRain.IsRunning = true;
        UpdateNotice.IsVisible = true;
        if (!_updateNoticeWatched)
        {
            _updateNoticeWatched = true;
            Watch(UpdateNotice, IsVisibleProperty, () =>
            {
                if (UpdateNotice.IsVisible && _updateProgress is not null) StartUpdateFrames();
            });
            Watch(UpdateBarTrack, BoundsProperty, SizeUpdateBar);
        }
        StartUpdateFrames();
    }

    private double _updateBarPercent;

    /// <summary>Dolgu izin yüzdesidir, piksel değil: pencere daralınca iz de daralır, dolgu ondan taşmaz.</summary>
    private void SizeUpdateBar() => UpdateBarFill.Width = UpdateBarTrack.Bounds.Width * _updateBarPercent / 100;

    internal string BakimKlasoru { get; set; } = AppContext.BaseDirectory;

    private void BakimHatasiniBekle()
    {
        EventHandler? acildi = null;
        acildi = (_, _) =>
        {
            Opened -= acildi;
            BakimHatasiniGoster();
        };
        Opened += acildi;
    }

    internal bool BakimHatasiniGoster()
    {
        var klasor = BakimKlasoru;
        if (!File.Exists(Path.Combine(klasor, global::VidShrink.Launcher.UygulamaKlasoruKapisi.HataIsareti))) return false;
        global::VidShrink.Launcher.UygulamaKlasoruKapisi.HatayiSil(klasor);
        if (UpdateNoticeLocked) return false;

        var progress = new InstallProgress();
        ShowUpdateProgress(progress);
        progress.Finish(false, Say("main.update.maintenance-failed"));
        return true;
    }

    private void StartUpdateFrames()
    {
        if (_updateFrame is null)
        {
            _updateFrame = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds)
            };
            _updateFrame.Tick += (_, _) => UpdateFrame();
        }

        _updateClock.Restart();
        _updateFrame.Start();
    }

    /// <summary>
    /// Panelin bir karesi: kuyruk boşalır, yeni satırlar yumuşakça girer, çubuk geçen
    /// süreye göre ilerler. Panel kapalıysa ya da çubuk durmuşsa saat kendini durdurur.
    /// </summary>
    internal void UpdateFrame()
    {
        var elapsed = _updateClock.Elapsed;
        _updateClock.Restart();
        UpdateFrame(elapsed);
    }

    /// <summary>Geçen süresi verilen kare; ölçüm saati beklemeden kareleri sürer.</summary>
    internal void UpdateFrame(TimeSpan elapsed)
    {
        var progress = _updateProgress;
        if (progress is null || !UpdateNotice.IsVisible)
        {
            _updateFrame?.Stop();
            UpdateRain.IsRunning = false;
            return;
        }

        UpdateRain.IsRunning = true;
        DrainUpdateReports();

        if (!HoverZone.MotionReduced) progress.Advance(elapsed);
        var bar = HoverZone.MotionReduced ? progress.Percent : progress.Bar;
        _updateBarPercent = bar;
        SizeUpdateBar();
        PaintUpdateBar(progress.State);
        AppendUpdateLines(progress.History);

        if (progress.State != InstallState.Running && bar >= progress.Ceiling)
        {
            _updateFrame?.Stop();
            UpdateRain.IsRunning = false;
        }
    }

    private void PaintUpdateBar(InstallState state)
    {
        var key = state switch
        {
            InstallState.Done => "NeonSuccess",
            InstallState.Failed => "NeonEmber",
            _ => "AccentGradient"
        };
        if (this.TryFindResource(key, out var value) && value is IBrush brush && !ReferenceEquals(UpdateBarFill.Background, brush))
            UpdateBarFill.Background = brush;
    }

    private void AppendUpdateLines(IReadOnlyList<string> history)
    {
        if (history.Count == _updateLinesShown) return;

        var enter = Scalar("SpaceSm", 0);
        var motion = this.TryFindResource("MotionBase", out var duration) && duration is TimeSpan span ? span : TimeSpan.Zero;
        var dim = this.TryFindResource("TextDisabled", out var disabled) ? disabled as IBrush : null;

        for (var i = _updateLinesShown; i < history.Count; i++)
        {
            foreach (var child in UpdateLogLines.Children.OfType<TextBlock>())
                if (dim is not null) child.Foreground = dim;

            var line = new TextBlock
            {
                Text = history[i],
                Theme = Look("MonoValue"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ToolTip.SetTip(line, history[i]);

            if (!HoverZone.MotionReduced && motion > TimeSpan.Zero)
            {
                line.Opacity = 0;
                line.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translateY({enter}px)"));
                line.Transitions = new Transitions
                {
                    new DoubleTransition { Property = OpacityProperty, Duration = motion },
                    new TransformOperationsTransition { Property = RenderTransformProperty, Duration = motion }
                };
                var entering = line;
                Dispatcher.UIThread.Post(() =>
                {
                    entering.Opacity = 1;
                    entering.RenderTransform = TransformOperations.Parse("none");
                }, DispatcherPriority.Background);
            }

            UpdateLogLines.Children.Add(line);
            while (UpdateLogLines.Children.Count > InstallProgress.LogLines) UpdateLogLines.Children.RemoveAt(0);
        }

        _updateLinesShown = history.Count;
    }
}
