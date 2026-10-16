using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Dönüştür'ün etiket açılır paneli (başlık, sanatçı, yıl, açıklama, kapak) ve "Onar" hazır ayarı.
/// Alanlar dosyaya bağlıdır, ayar dosyasına yazılmaz; boş alan komuta anahtar yazmaz.
/// </summary>
public partial class MainWindow
{
    private string? _convertCoverPath;

    private void InitConvertTags()
    {
        Watch(TxtConvertTagTitle, TextBox.TextProperty, OnConvertChanged);
        Watch(TxtConvertTagArtist, TextBox.TextProperty, OnConvertChanged);
        Watch(TxtConvertTagYear, TextBox.TextProperty, OnConvertChanged);
        Watch(TxtConvertTagComment, TextBox.TextProperty, OnConvertChanged);
        Watch(ChkConvertRepair, ToggleButton.IsCheckedProperty, OnConvertChanged);
    }

    private ConversionPlan WithConvertTags(ConversionPlan plan) => plan with
    {
        Title = TxtConvertTagTitle.Text,
        Artist = TxtConvertTagArtist.Text,
        Year = TxtConvertTagYear.Text,
        Comment = TxtConvertTagComment.Text,
        CoverPath = _convertCoverPath,
        Repair = ChkConvertRepair.IsChecked == true,
        Chapters = ReadConvertChapters()
    };

    /// <summary>Kapağı seçer ya da (<c>null</c>) kaldırır; adı panelde gösterilir, komut tazelenir.</summary>
    internal void UseConvertCover(string? path)
    {
        _convertCoverPath = string.IsNullOrWhiteSpace(path) ? null : path;
        TxtConvertCoverName.Text = _convertCoverPath is null ? Say("main.convert.tags.cover.none") : Path.GetFileName(_convertCoverPath);
        OnConvertChanged();
    }

    /// <summary>"Onar": MKV'ye video ve ses kopyası, Onar kutusu açık. Meta silme seçimi korunur.</summary>
    internal void ApplyConvertRepairPreset()
    {
        var syncing = _syncing;
        _syncing = true;
        ChkConvertRepair.IsChecked = true;
        _syncing = syncing;
        ApplyConvertPreset(new ConversionPreset
        {
            Id = "repair",
            Container = "mkv",
            VideoCodec = "copy",
            AudioCodec = "copy",
            DropMetadata = ChkConvertDropMetadata.IsChecked == true
        });
    }

    private void OnConvertRepairPreset(object? sender, RoutedEventArgs e) => ApplyConvertRepairPreset();

    private void OnConvertCoverClear(object? sender, RoutedEventArgs e) => UseConvertCover(null);

    private async void OnConvertCoverPick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.convert.tags.cover")) { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
                }
            });
            if (files.Select(file => file.TryGetLocalPath()).OfType<string>().FirstOrDefault() is { } path) UseConvertCover(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }
}
