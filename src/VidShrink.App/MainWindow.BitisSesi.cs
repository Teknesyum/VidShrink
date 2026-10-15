using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// AYR-21: bitiş sesinin dosyası. Seçim "Bitince ses çal" kutusunun sağ tık menüsünden yapılır (Ayarlar
/// sekmesi tam sınırda, görünür satır eklenmez);
/// boşsa ya da dosya silinmişse sistem sesi çalar. Windows yalnız WAV çalar.
/// </summary>
public partial class MainWindow
{
    private string _finishSoundFile = "";

    internal string? FinishSoundFileOrNull => _finishSoundFile.Length > 0 ? _finishSoundFile : null;

    internal void SetFinishSoundFile(string? path)
    {
        _finishSoundFile = path ?? "";
        RefreshFinishSoundFile();
    }

    private void RefreshFinishSoundFile()
    {
        BtnFinishSoundFile.Header = _finishSoundFile.Length > 0
            ? Path.GetFileName(_finishSoundFile)
            : Say("settings-tab.finish-sound-file.choose");
        BtnFinishSoundReset.IsVisible = _finishSoundFile.Length > 0;
    }

    private async void OnChooseFinishSound(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.audio.external.file-type"))
                    {
                        Patterns = OperatingSystem.IsWindows() ? new[] { "*.wav" } : ExternalAudio.Extensions.Select(x => "*" + x).ToArray()
                    },
                    FilePickerFileTypes.All
                }
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is null) return;
            SetFinishSoundFile(path);
            SaveAppSettings();
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }

    private void OnResetFinishSound(object? sender, RoutedEventArgs e)
    {
        SetFinishSoundFile(null);
        SaveAppSettings();
    }
}
