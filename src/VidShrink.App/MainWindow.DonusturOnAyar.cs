using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Dönüştür sekmesinin adlı ön ayarları (DON-05, DON-12). Küçült'ün yonga şeridinden ayrı:
/// dosya <see cref="ConversionPresetLibrary"/>, liste Dönüştür panelinin üstündeki açılır kutu.
/// Kaydetme, aynı ad sorusu, içe ve dışa aktarma Küçült'teki karşılıklarıyla aynı akışta
/// (<see cref="SavePreset"/>, <see cref="ImportPresets"/>, <see cref="ExportPresets"/>).
/// Kırpma aralığı ve HDR ilkesi ön ayara girmez: ikisi de açık dosyaya bağlı.
/// </summary>
public partial class MainWindow
{
    private readonly List<ConversionPreset> _convertPresets = new();
    private string? _convertOverwriteAsked;
    private bool _convertPresetRefreshing;

    /// <summary>Ölçüm ve deneme Dönüştür ön ayar dosyasını başka yere alır; gerçek dosya yazılmaz.</summary>
    internal string? ConvertPresetPathOverride { get; set; }

    internal IReadOnlyList<ConversionPreset> ConvertPresets => _convertPresets;

    internal bool ConvertPresetNoticeVisible => TxtConvertPresetNotice.IsVisible;

    private string ConvertPresetPath => ConvertPresetPathOverride ?? ConversionPresetLibrary.DefaultUserPath;

    internal void InitConvertPresets()
    {
        _convertPresets.Clear();
        try
        {
            _convertPresets.AddRange(ConversionPresetLibrary.LoadUser(ConvertPresetPath));
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
        }

        RefreshConvertPresetList();
    }

    private void RefreshConvertPresetList()
    {
        _convertPresetRefreshing = true;
        CmbConvertPreset.ItemsSource = _convertPresets.Select(p => p.Name ?? p.Id).ToList();
        CmbConvertPreset.SelectedIndex = -1;
        CmbConvertPreset.IsEnabled = _convertPresets.Count > 0;
        _convertPresetRefreshing = false;
    }

    private void OnConvertPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_convertPresetRefreshing) return;
        var index = CmbConvertPreset.SelectedIndex;
        if (index >= 0 && index < _convertPresets.Count) ApplyConvertPreset(_convertPresets[index]);
    }

    /// <summary>Panelin o anki seçimleri. Özel boyut ve kare hızı yalnız "Özel" seçiliyken girer.</summary>
    internal ConversionPreset CurrentConvertPreset(string name)
    {
        var resolution = SelectedTag(CmbResolution) ?? ConversionPreset.Source;
        var fps = SelectedTag(CmbConvertFps) ?? ConversionPreset.Source;
        return new ConversionPreset
        {
            Id = name.Trim(),
            Name = name.Trim(),
            Container = SelectedTag(CmbContainer) ?? ConversionDefaults.Container,
            VideoCodec = SelectedTag(CmbConvertCodec) ?? ConversionDefaults.VideoCodec,
            QualityMode = CmbQualityMode.SelectedIndex == 1 ? ConversionQualityMode.Bitrate : ConversionQualityMode.Crf,
            Quality = int.TryParse(TxtQuality.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) && q > 0
                ? q : new ConversionPreset().Quality,
            Resolution = resolution,
            CustomResolution = resolution == ConversionPreset.Custom ? TxtCustomResolution.Text : null,
            Fps = fps,
            CustomFps = fps == ConversionPreset.Custom ? TxtCustomFps.Text : null,
            AudioCodec = SelectedTag(CmbConvertAudio) ?? ConversionPreset.NoAudio,
            AudioBitrateK = int.TryParse(TxtAudioBitrate.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) && a > 0
                ? a : new ConversionPreset().AudioBitrateK,
            DropMetadata = ChkConvertDropMetadata.IsChecked == true
        };
    }

    /// <summary>
    /// Ön ayarı panele yazar. Bu makinede sunulmayan (gizli ya da kapalı) seçim yerinde
    /// bırakılır; ön ayar başka makineden gelmiş olabilir.
    /// </summary>
    internal void ApplyConvertPreset(ConversionPreset preset)
    {
        var syncing = _syncing;
        _syncing = true;
        SelectByTag(CmbContainer, preset.Container);
        SelectByTag(CmbConvertCodec, preset.VideoCodec);
        CmbQualityMode.SelectedIndex = preset.QualityMode == ConversionQualityMode.Bitrate ? 1 : 0;
        TxtQuality.Text = preset.Quality.ToString(CultureInfo.InvariantCulture);
        SelectByTag(CmbResolution, preset.Resolution == ConversionPreset.Source ? null : preset.Resolution);
        if (preset.CustomResolution is { } customResolution) TxtCustomResolution.Text = customResolution;
        SelectByTag(CmbConvertFps, preset.Fps == ConversionPreset.Source ? null : preset.Fps);
        if (preset.CustomFps is { } customFps) TxtCustomFps.Text = customFps;
        SelectByTag(CmbConvertAudio, preset.AudioCodec == ConversionPreset.NoAudio ? null : preset.AudioCodec);
        TxtAudioBitrate.Text = preset.AudioBitrateK.ToString(CultureInfo.InvariantCulture);
        ChkConvertDropMetadata.IsChecked = preset.DropMetadata;
        _syncing = syncing;

        OzelDonusturAlanlari();
        ApplyQualityRange();
        RefreshConversion();
    }

    private static void SelectByTag(SelectingItemsControl box, string? tag)
    {
        var item = box.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => i.IsVisible && i.IsEnabled && string.Equals(i.Tag as string, tag, StringComparison.Ordinal));
        if (item is not null) box.SelectedItem = item;
    }

    private void OnConvertPresetSave(object? sender, RoutedEventArgs e) => SaveConvertPreset(TxtConvertPresetName.Text ?? string.Empty);

    internal bool SaveConvertPreset(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            ShowConvertPresetNotice(Say("main.preset.name-empty"));
            return false;
        }

        if (_convertPresets.Any(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(_convertOverwriteAsked, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            _convertOverwriteAsked = trimmed;
            ShowConvertPresetNotice(Say("main.preset.overwrite", trimmed));
            return false;
        }

        try
        {
            var stored = ConversionPresetLibrary.SaveUser(CurrentConvertPreset(trimmed), ConvertPresetPath);
            _convertPresets.Clear();
            _convertPresets.AddRange(stored);
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            ShowConvertPresetNotice(ex is PresetFileException preset ? Say(preset.LocaleKey, preset.Detail) : ex.Message);
            return false;
        }

        _convertOverwriteAsked = null;
        TxtConvertPresetName.Text = string.Empty;
        TxtConvertPresetNotice.IsVisible = false;
        (BtnConvertPresetAdd.Flyout as Flyout)?.Hide();
        RefreshConvertPresetList();
        return true;
    }

    private void ShowConvertPresetNotice(string text)
    {
        TxtConvertPresetNotice.Text = text;
        TxtConvertPresetNotice.IsVisible = true;
    }

    private async void OnConvertPresetImport(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.preset.file-type")) { Patterns = new[] { "*.json" } },
                    FilePickerFileTypes.All
                }
            });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) ImportConvertPresets(path);
        }
        catch (Exception ex)
        {
            ShowConvertPresetNotice(ex.Message);
        }
    }

    /// <summary>
    /// İçe aktarma. Yalnız Dönüştür ön ayar dosyası okunur; Küçült dosyası
    /// <see cref="PresetFileError.NotPresetFile"/> ile reddedilir. Var olan ad ezilmez,
    /// gelen ön ayar <c>-2</c> ekiyle yanına yazılır.
    /// </summary>
    internal bool ImportConvertPresets(string path)
    {
        int count;
        try
        {
            var incoming = ConversionPresetLibrary.Parse(File.ReadAllText(path));
            var stored = _convertPresets.ToList();
            foreach (var preset in incoming)
                stored = ConversionPresetLibrary.SaveUser(UniqueConvert(preset, stored), ConvertPresetPath).ToList();
            count = incoming.Count;
            _convertPresets.Clear();
            _convertPresets.AddRange(stored);
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            ShowConvertPresetNotice(ex is PresetFileException preset ? Say(preset.LocaleKey, preset.Detail) : ex.Message);
            return false;
        }

        RefreshConvertPresetList();
        ShowConvertPresetNotice(Say("main.preset.imported", count));
        return true;
    }

    private static ConversionPreset UniqueConvert(ConversionPreset preset, IReadOnlyList<ConversionPreset> stored)
    {
        bool Taken(string id) => stored.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        if (!Taken(preset.Id)) return preset;
        var n = 2;
        while (Taken($"{preset.Id}-{n}")) n++;
        return preset with { Id = $"{preset.Id}-{n}", Name = $"{preset.Name ?? preset.Id} ({n})" };
    }

    private async void OnConvertPresetExport(object? sender, RoutedEventArgs e)
    {
        if (_convertPresets.Count == 0)
        {
            ShowConvertPresetNotice(Say("main.preset.export-empty"));
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "vidshrink-convert-presets.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType(Say("main.preset.file-type")) { Patterns = new[] { "*.json" } } }
            });
            if (file?.TryGetLocalPath() is { } path) ExportConvertPresets(path);
        }
        catch (Exception ex)
        {
            ShowConvertPresetNotice(ex.Message);
        }
    }

    internal bool ExportConvertPresets(string path)
    {
        if (_convertPresets.Count == 0)
        {
            ShowConvertPresetNotice(Say("main.preset.export-empty"));
            return false;
        }

        try
        {
            ConversionPresetLibrary.Export(_convertPresets, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowConvertPresetNotice(ex.Message);
            return false;
        }

        ShowConvertPresetNotice(Say("main.preset.exported", _convertPresets.Count));
        return true;
    }
}
