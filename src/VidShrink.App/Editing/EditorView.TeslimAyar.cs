using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

/// <summary>
/// Teslim acilirindaki ayarlar ve on ayarlar (DZB-54, DZB-55, DZB-58). Denetimlerin varsayilan secimi
/// <see cref="EditExportSettings.Default"/>'tur; ayar degismedikce teslim bugunku argumanlari yazar.
/// </summary>
internal partial class EditorView
{
    private const string UserPresetTag = "user:";
    private readonly List<EditExportPreset> _exportPresets = new();
    private bool _applyingExportPreset;
    private string? _exportOverwriteAsked;
    private string? _exportPresetPath;

    /// <summary>Kullanici on ayar dosyasinin yolu; testte gecici klasore cevrilir.</summary>
    internal string? ExportPresetPathOverride
    {
        get => _exportPresetPath;
        set
        {
            _exportPresetPath = value;
            LoadExportPresets();
        }
    }

    internal string ExportPresetError => TxtExportPresetError.Text ?? string.Empty;

    internal IReadOnlyList<EditExportPreset> UserExportPresets => _exportPresets;

    internal EditExportSettings ExportSettings
    {
        get
        {
            var size = (TagOf(CmbExportResolution) ?? "0x0").Split('x');
            return new EditExportSettings
            {
                Width = int.Parse(size[0], CultureInfo.InvariantCulture),
                Height = int.Parse(size[1], CultureInfo.InvariantCulture),
                Fps = double.Parse(TagOf(CmbExportFps) ?? "0", CultureInfo.InvariantCulture),
                VideoCodec = TagOf(CmbExportCodec) ?? EditExportSettings.DefaultCodec,
                Crf = int.Parse(TagOf(CmbExportQuality) ?? "18", CultureInfo.InvariantCulture),
                AudioKbps = int.Parse(TagOf(CmbExportAudioBitrate) ?? "192", CultureInfo.InvariantCulture),
                AudioOnly = ChkExportAudioOnly.IsChecked == true
            };
        }
        set
        {
            var was = _applyingExportPreset;
            _applyingExportPreset = true;
            try
            {
                SelectTag(CmbExportResolution, value.HasSize
                    ? value.Width.ToString(CultureInfo.InvariantCulture) + "x" + value.Height.ToString(CultureInfo.InvariantCulture)
                    : "0x0");
                SelectTag(CmbExportFps, value.Fps.ToString(CultureInfo.InvariantCulture));
                SelectTag(CmbExportCodec, value.VideoCodec);
                SelectTag(CmbExportQuality, value.Crf.ToString(CultureInfo.InvariantCulture));
                SelectTag(CmbExportAudioBitrate, value.AudioKbps.ToString(CultureInfo.InvariantCulture));
                ChkExportAudioOnly.IsChecked = value.AudioOnly;
            }
            finally
            {
                _applyingExportPreset = was;
            }

            _pendingPlan = null;
        }
    }

    /// <summary>Secili on ayarin kimligi; elle degistirilmis ayarda bos.</summary>
    internal string ExportPresetId
    {
        get
        {
            var tag = TagOf(CmbExportPreset) ?? string.Empty;
            return tag.StartsWith(UserPresetTag, StringComparison.Ordinal) ? tag[UserPresetTag.Length..] : tag;
        }
        set
        {
            var tag = EditExportPresetLibrary.BuiltIn.Any(p => p.Id == value) || value.Length == 0 ? value : UserPresetTag + value;
            SelectTag(CmbExportPreset, tag);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> CodecNames = new Dictionary<string, string>
    {
        ["libx264"] = "H.264",
        ["libx265"] = "H.265 (HEVC)",
        ["libsvtav1"] = "AV1"
    };

    private void FillExportChoices()
    {
        foreach (var fps in EditExportSettings.FpsChoices)
        {
            var text = fps.ToString(CultureInfo.InvariantCulture);
            CmbExportFps.Items.Add(new ComboBoxItem { Content = text, Tag = text });
        }

        foreach (var codec in EditExportSettings.VideoCodecs)
            CmbExportCodec.Items.Add(new ComboBoxItem { Content = CodecNames.TryGetValue(codec, out var name) ? name : codec, Tag = codec });

        var quality = CmbExportQuality.Items.OfType<ComboBoxItem>().ToList();
        for (var i = 0; i < quality.Count && i < EditExportSettings.CrfChoices.Count; i++)
            quality[i].Tag = EditExportSettings.CrfChoices[i].ToString(CultureInfo.InvariantCulture);

        foreach (var kbps in EditExportSettings.AudioKbpsChoices)
            CmbExportAudioBitrate.Items.Add(new ComboBoxItem
            {
                Content = kbps.ToString(CultureInfo.InvariantCulture) + " kbps",
                Tag = kbps.ToString(CultureInfo.InvariantCulture)
            });

        var defaults = EditExportSettings.Default;
        SelectTag(CmbExportCodec, defaults.VideoCodec);
        SelectTag(CmbExportQuality, defaults.Crf.ToString(CultureInfo.InvariantCulture));
        SelectTag(CmbExportAudioBitrate, defaults.AudioKbps.ToString(CultureInfo.InvariantCulture));
    }

    private void InitExportSettings()
    {
        FillExportChoices();
        CmbExportPreset.SelectionChanged += (_, _) => ApplySelectedPreset();
        foreach (var combo in new[] { CmbExportResolution, CmbExportFps, CmbExportCodec, CmbExportQuality, CmbExportAudioBitrate })
            combo.SelectionChanged += (_, _) => ExportSettingChanged();
        ChkExportAudioOnly.IsCheckedChanged += (_, _) => ExportSettingChanged();
        BtnExportPresetSave.Click += (_, _) => SaveExportPreset(TxtExportPresetName.Text ?? string.Empty);
        LoadExportPresets();
    }

    private void ExportSettingChanged()
    {
        _pendingPlan = null;
        if (_applyingExportPreset) return;
        _applyingExportPreset = true;
        try
        {
            CmbExportPreset.SelectedIndex = 0;
        }
        finally
        {
            _applyingExportPreset = false;
        }
    }

    private void ApplySelectedPreset()
    {
        _pendingPlan = null;
        if (_applyingExportPreset) return;
        var tag = TagOf(CmbExportPreset) ?? string.Empty;
        if (tag.Length == 0) return;
        var preset = tag.StartsWith(UserPresetTag, StringComparison.Ordinal)
            ? _exportPresets.FirstOrDefault(p => p.Id == tag[UserPresetTag.Length..])
            : EditExportPresetLibrary.BuiltIn.FirstOrDefault(p => p.Id == tag);
        if (preset is not null) ExportSettings = preset.Settings;
    }

    internal bool SaveExportPreset(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            TxtExportPresetError.Text = Strings.Get("main.preset.name-empty");
            return false;
        }

        if (EditExportPresetLibrary.BuiltIn.Any(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            TxtExportPresetError.Text = string.Format(Strings.Culture, Strings.Get("main.preset.error.reserved-id"), trimmed);
            return false;
        }

        if (_exportPresets.Any(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(_exportOverwriteAsked, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            _exportOverwriteAsked = trimmed;
            TxtExportPresetError.Text = string.Format(Strings.Culture, Strings.Get("main.preset.overwrite"), trimmed);
            return false;
        }

        try
        {
            var stored = EditExportPresetLibrary.SaveUser(new EditExportPreset { Id = trimmed, Name = trimmed, Settings = ExportSettings }, _exportPresetPath);
            _exportPresets.Clear();
            _exportPresets.AddRange(stored);
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            TxtExportPresetError.Text = ex is PresetFileException preset
                ? string.Format(Strings.Culture, Strings.Get(preset.LocaleKey), preset.Detail)
                : ex.Message;
            return false;
        }

        _exportOverwriteAsked = null;
        TxtExportPresetName.Text = string.Empty;
        TxtExportPresetError.Text = string.Empty;
        RefreshExportPresetItems();
        ExportPresetId = trimmed;
        return true;
    }

    private void LoadExportPresets()
    {
        _exportPresets.Clear();
        try
        {
            _exportPresets.AddRange(EditExportPresetLibrary.LoadUser(_exportPresetPath));
            TxtExportPresetError.Text = string.Empty;
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            TxtExportPresetError.Text = ex is PresetFileException preset
                ? string.Format(Strings.Culture, Strings.Get(preset.LocaleKey), preset.Detail)
                : ex.Message;
        }

        RefreshExportPresetItems();
    }

    private void RefreshExportPresetItems()
    {
        var was = _applyingExportPreset;
        _applyingExportPreset = true;
        try
        {
            var selected = TagOf(CmbExportPreset);
            foreach (var old in CmbExportPreset.Items.OfType<ComboBoxItem>().Where(i => i.Tag is string t && t.StartsWith(UserPresetTag, StringComparison.Ordinal)).ToList())
                CmbExportPreset.Items.Remove(old);
            foreach (var preset in _exportPresets)
                CmbExportPreset.Items.Add(new ComboBoxItem { Content = preset.Name ?? preset.Id, Tag = UserPresetTag + preset.Id });
            if (!SelectTag(CmbExportPreset, selected ?? string.Empty)) CmbExportPreset.SelectedIndex = 0;
        }
        finally
        {
            _applyingExportPreset = was;
        }
    }

    private static string? TagOf(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private static bool SelectTag(ComboBox combo, string tag)
    {
        var item = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == tag);
        if (item is null) return false;
        combo.SelectedItem = item;
        return true;
    }

    private string OutputName(string source, Func<string, bool> exists)
    {
        if (!ExportSettings.AudioOnly) return EditOutputName.For(source, exists);
        var extension = EditExportSettings.AudioOnlyExtensions[0];
        return Path.ChangeExtension(EditOutputName.For(source, p => exists(Path.ChangeExtension(p, extension))), extension);
    }
}
