using System;
using System.IO;
using System.Threading.Tasks;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-08: kuyruk ve çalan dosya geçmişin yanındaki <see cref="PlayerQueue.FileName"/> dosyasına yazılır;
/// geçmiş kaydı her yazıldığında (duraklatma, aralıklı kayıt, kapanış) ve her açılışta. Geçmiş kapalıysa
/// dosya silinir. Uygulama açılınca kuyruk bir kez, sorusuz ve duraklatılmış geri gelir.
/// </summary>
internal partial class PlayerView
{
    private string? _savedQueue;
    private bool _queueRestoreTried;
    private bool _openPaused;

    /// <summary>Test konağı açılışta kuyruğu geri getirmez; ayar klasörünü paylaşan pencere testleri birbirinin kuyruğunu açmasın.</summary>
    internal static bool QueueRestoreDisabled { get; set; }

    private string? QueueFile() => SettingsFolder() is { } folder ? Path.Combine(folder, PlayerQueue.FileName) : null;

    /// <summary>
    /// Çalan yerel dosya yokken (açılış arası, adres) dosyaya dokunulmaz, son kuyruk kalır; geçmiş kapalıysa silinir.
    /// </summary>
    internal void SaveQueue()
    {
        if (QueueFile() is not { } file) return;
        PlayerQueue? queue = null;
        if (Settings.KeepHistory)
        {
            if (_path is not { } path || MediaAddress.IsAddress(path)) return;
            queue = new PlayerQueue(_queue ?? Array.Empty<string>(), path, _queueArranged, QueueSource);
        }
        var text = queue?.Serialize() ?? "";
        if (string.Equals(text, _savedQueue, StringComparison.Ordinal)) return;
        _savedQueue = text;
        PlayerQueue.Save(file, queue);
    }

    /// <summary>
    /// Saklanan kuyruğu bir kez açar, duraklatılmış ve kaldığı yerden. Bir şey çalıyorsa, açılış sürüyorsa
    /// ya da saklanan kuyruk yoksa hiçbir şey yapmaz.
    /// </summary>
    internal bool RestoreQueue()
    {
        if (_queueRestoreTried) return false;
        _queueRestoreTried = true;
        if (_path is not null || !_navigation.IsCompleted || !Settings.KeepHistory) return false;
        if (PlayerQueue.Load(QueueFile()) is not { } saved) return false;

        _queue = saved.Items.Count > 0 ? saved.Items : null;
        _queueArranged = saved.Arranged && _queue is not null;
        QueueSource = _queue is null ? null : saved.Source;
        _trace.Add("queuerestore -> " + saved.Items.Count);
        _navigation = OpenPausedAsync(saved.Current);
        return true;
    }

    private async Task OpenPausedAsync(string path)
    {
        _openPaused = true;
        try { await OpenQuietlyAsync(path).ConfigureAwait(true); }
        finally { _openPaused = false; }
    }
}
