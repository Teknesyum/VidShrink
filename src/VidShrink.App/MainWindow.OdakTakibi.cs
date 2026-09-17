using System;
using System.IO;
using System.Threading.Tasks;

namespace VidShrink.App;

public partial class MainWindow
{
    private bool _following;

    internal Func<string, Task>? FollowShrinkLoader { get; set; }

    internal Func<string, Task>? FollowPlayerOpener { get; set; }

    internal string? ShrinkLoadedPath => _info?.FilePath;

    /// <summary>
    /// "Kaydı izle" açıkken biten kayıt küçültme sekmesine yüklenir ve oynatıcıda duraklatılmış
    /// açılır; seçili sekme değişmez. Kapalıyken hiçbir sekme dosyasını bırakmaz.
    /// </summary>
    internal async Task FollowRecordingAsync(string path)
    {
        if (ChkFollowRecording.IsChecked != true || !File.Exists(path)) return;

        _following = true;
        try
        {
            if (!SamePath(ShrinkLoadedPath, path)) await (FollowShrinkLoader ?? LoadAsync)(path);
            if (SamePath(Player.LoadedPath, path)) return;

            var playerShown = Tabs.SelectedIndex == PlayerTabIndex;
            try
            {
                await (FollowPlayerOpener ?? (p => Player.OpenAsync(p)))(path);
                if (!playerShown && Player.IsPlaying) Player.TogglePlay();
            }
            catch (Exception ex) { ReportPlayerOpenFailure(ex); }
        }
        finally
        {
            _following = false;
        }
    }

    /// <summary>
    /// Aynı seçenek oynatıcıda açılan yerel dosya için de geçerli: küçültme sekmesi o dosyaya
    /// döner. Seçenek kapalıyken küçültme sekmesi kendi dosyasında kalır.
    /// </summary>
    private void OnPlayerOpened(string path)
    {
        if (_following || ChkFollowRecording.IsChecked != true) return;
        if (!File.Exists(path) || SamePath(ShrinkLoadedPath, path)) return;
        _ = (FollowShrinkLoader ?? LoadAsync)(path);
    }

    internal void PlayerOpenedForTest(string path) => OnPlayerOpened(path);

    private static bool SamePath(string? left, string? right)
        => left is not null && right is not null
           && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
