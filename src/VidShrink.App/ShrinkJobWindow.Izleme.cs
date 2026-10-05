using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App;

public partial class ShrinkJobWindow
{
    private QueueWatch? _watch;
    private bool _watchSyncing;
    private string? _running;

    /// <summary>Klasör seçicinin yerine geçen iş; testler sahtesini verir.</summary>
    internal Func<Task<string?>>? PickWatchFolder { get; set; }

    internal TimeSpan? WatchPollInterval { get; set; }

    internal TimeSpan? WatchStableFor { get; set; }

    /// <summary>Kapalıyken tarama yalnız <see cref="WatchStep"/> ile yürür; ölçü zamana bağlı kalmaz.</summary>
    internal bool WatchInBackground { get; set; } = true;

    internal bool Watching => _watch is not null;

    internal bool WatchChecked => ChkWatch.IsChecked == true;

    internal string WatchFolderText => TxtWatchFolder.Text ?? "";

    /// <summary>İzleme kendiliğinden durduğunda basılan uyarı; satır görünmüyorsa boş.</summary>
    internal string WatchWarningText => WatchWarning.IsVisible ? TxtWatchWarning.Text ?? "" : "";

    private void WireWatch()
    {
        ChkWatch.IsCheckedChanged += (_, _) =>
        {
            if (!_watchSyncing) _ = SetWatchAsync(ChkWatch.IsChecked == true);
        };
        BtnWatchFolder.Click += (_, _) => _ = ChooseWatchFolderAsync();
    }

    /// <summary>Kayıtlı seçim açıksa ve klasör duruyorsa izlemeyi başlatır; klasör yoksa sessizce kapalı kalır.</summary>
    private void RestoreWatch()
    {
        if (_template is null) return;
        WatchPanel.IsVisible = true;
        if (_appSettings.WatchEnabled && Directory.Exists(_appSettings.WatchDirectory)) StartWatch(_appSettings.WatchDirectory);
        SyncWatch();
    }

    /// <summary>Anahtar. Klasör seçilmemişse ya da kaybolmuşsa önce seçici açılır; vazgeçilirse izleme kapalı kalır.</summary>
    internal async Task SetWatchAsync(bool on)
    {
        if (!on)
        {
            StopWatch();
            SaveWatch(false);
            SyncWatch();
            return;
        }

        var folder = _appSettings.WatchDirectory;
        if (!Directory.Exists(folder)) folder = await AskWatchFolderAsync() ?? "";
        if (Directory.Exists(folder))
        {
            _appSettings.WatchDirectory = folder;
            StartWatch(folder);
            SaveWatch(true);
        }
        SyncWatch();
    }

    internal async Task ChooseWatchFolderAsync()
    {
        if (await AskWatchFolderAsync() is not { } folder || !Directory.Exists(folder)) return;
        var on = Watching;
        _appSettings.WatchDirectory = folder;
        if (on) StartWatch(folder);
        SaveWatch(on);
        SyncWatch();
    }

    private async Task<string?> AskWatchFolderAsync()
    {
        if (PickWatchFolder is { } pick) return await pick();
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void StartWatch(string folder)
    {
        StopWatch();
        WatchWarning.IsVisible = false;
        var watch = new QueueWatch(folder, WatchPollInterval, WatchStableFor);
        _watch = watch;
        if (!WatchInBackground) return;
        _ = watch.Start(
            path => Dispatcher.UIThread.Post(() =>
            {
                if (_watch == watch) AcceptWatched(path);
            }),
            () => Dispatcher.UIThread.Post(() =>
            {
                if (_watch == watch) WatchLost();
            }));
    }

    private void StopWatch()
    {
        _watch?.Dispose();
        _watch = null;
    }

    /// <summary>Tek tarama, çağıranın iş parçacığında. İzleme kapalıysa bir şey yapmaz.</summary>
    internal void WatchStep()
    {
        if (_watch is not { } watch) return;
        if (!watch.Step(AcceptWatched)) WatchLost();
    }

    private void AcceptWatched(string path)
    {
        if (string.Equals(_running, path, WatchFolder.PathComparison)) return;
        if (_pending.Any(request => string.Equals(request.Path, path, WatchFolder.PathComparison))) return;
        CancelCountdown();
        Accept(new ShrinkRequest(0, path));
    }

    /// <summary>
    /// Klasör kayboldu: izleme durur ve satır bunu söyler. Kayıtlı seçime dokunulmaz; klasör
    /// geri gelince (çıkarılan sürücü) sonraki açılış izlemeyi kendiliğinden sürdürür.
    /// </summary>
    private void WatchLost()
    {
        StopWatch();
        SyncWatch();
        WatchWarning.IsVisible = true;
    }

    private void SaveWatch(bool on)
    {
        _appSettings.WatchEnabled = on;
        try { AppSettings.SaveWatch(on, _appSettings.WatchDirectory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void SyncWatch()
    {
        _watchSyncing = true;
        ChkWatch.IsChecked = Watching;
        _watchSyncing = false;

        var folder = _watch?.WatchedFolder ?? _appSettings.WatchDirectory;
        var none = string.IsNullOrEmpty(folder);
        var text = none ? Say("main.shrink-job.watch.none") : QueueWatch.Shorten(folder);
        TxtWatchFolder.Text = text;
        TxtWatchFolder.FlowDirection = none ? FlowDirection : FlowDirection.LeftToRight;
        ToolTip.SetTip(TxtWatchFolder, none ? text : folder);
        if (Watching) WatchWarning.IsVisible = false;
    }
}
