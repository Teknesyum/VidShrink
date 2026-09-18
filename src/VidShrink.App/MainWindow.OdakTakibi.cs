using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    private bool _following;
    private readonly CurrentMedia _media = new();
    private Task<MediaInfo>? _yoklamaUcusu;
    private string? _yoklamaUcusuYolu;

    /// <summary>Sekmelerin ortak odağı. D0'ın tek nesnesi.</summary>
    internal CurrentMedia Media => _media;

    /// <summary>
    /// Yoklama dikişi. Ölçüm sahte yoklayıcıyı buraya takar; üretimde ffprobe.
    /// </summary>
    internal Func<string, CancellationToken, Task<MediaInfo>> Prober { get; set; }
        = FfprobeClient.ProbeAsync;

    /// <summary>
    /// Ortak odakta o dosyanın tazeliği doğrulanmış çözümlemesi varsa ffprobe çağrılmaz.
    /// Aynı yol için uçuşta bir yoklama varsa çağıranlar onu paylaşır; oynatıcıdan
    /// Küçült'e geçişin iki kolu bu yüzden tek yoklamaya iner.
    /// </summary>
    private Task<MediaInfo> YoklaAsync(string path)
    {
        if (_media.InfoFor(path) is { } bilinen) return Task.FromResult(bilinen);
        if (_yoklamaUcusu is { IsCompleted: false } ucus
            && CurrentMedia.SamePath(_yoklamaUcusuYolu, path)) return ucus;

        var yeni = Prober(path, CancellationToken.None);
        _yoklamaUcusuYolu = path;
        _yoklamaUcusu = yeni;
        return yeni;
    }

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
            _media.Focus(path, MediaFocusOwner.Recorder);
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
        if (!File.Exists(path)) return;
        _media.Focus(path, MediaFocusOwner.Player);
        if (SamePath(ShrinkLoadedPath, path)) return;
        _ = (FollowShrinkLoader ?? LoadAsync)(path);
    }

    internal void PlayerOpenedForTest(string path) => OnPlayerOpened(path);

    private static bool SamePath(string? left, string? right)
        => left is not null && right is not null
           && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
