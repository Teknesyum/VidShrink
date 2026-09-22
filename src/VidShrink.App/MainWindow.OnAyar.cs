using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Kullanıcının kendi ön ayarları. Motor tarafı (<see cref="PresetLibrary"/>) kaydetmeyi,
/// okumayı ve dosyaya vermeyi baştan beri biliyordu; bu dosya onu yonga şeridine bağlıyor.
///
/// <para><b>Yerleşim.</b> Gömülü sekiz yonga, ince bir ayırıcı, kullanıcının yongaları,
/// en sonda "+". Sıra fable'ın kararı (<c>docs/netlestirme/020-onayar-arayuzu.md</c>):
/// kullanıcı kendi ön ayarını zaten yonga olarak kullanıyor, bu yüzden kaydetme de aynı
/// şeritte duruyor; Ayarlar sekmesine konsa yol "Küçült'ten çık, geri dön" olurdu.</para>
///
/// <para><b>Ayrımın biçimi.</b> Fable kesikli kenarlık önerdi; Avalonia'nın
/// <c>Border</c>'ında kesikli kenarlık yok (12.1.2 yüzeyinde <c>BorderDashArray</c>
/// bulunmuyor). Ayrım bu yüzden yerleşim (ayırıcı + sıra) ve yazı biçimiyle kuruluyor:
/// kullanıcı yongasının adı italik. Renk uydurulmadı.</para>
///
/// <para><b>Silme.</b> Onay penceresi yok: yonga kalkıyor, altındaki satır "silindi" diyor
/// ve geri alma silinen profili yeniden yazıyor. Tek yonga için pencere ağır kaçardı.</para>
/// </summary>
public partial class MainWindow
{
    private readonly List<PresetProfile> _userPresets = new();
    private PresetProfile? _deletedPreset;
    private string? _overwriteAsked;

    /// <summary>Ölçüm ve deneme ön ayar dosyasını başka yere alır; gerçek dosya yazılmaz.</summary>
    internal string? PresetPathOverride { get; set; }

    internal IReadOnlyList<PresetProfile> UserPresets => _userPresets;

    internal bool PresetNoticeVisible => TxtPresetNotice.IsVisible;

    private string PresetPath => PresetPathOverride ?? PresetLibrary.DefaultUserPath;

    /// <summary>
    /// Şeridi ilk kuruş. Okuma hatası kullanıcıyı durdurmuyor: bozuk dosya varsa şerit
    /// gömülü yongalarla açılıyor ve durum satırı sebebi yazıyor.
    /// </summary>
    internal void InitUserPresets()
    {
        _userPresets.Clear();
        try
        {
            _userPresets.AddRange(PresetLibrary.LoadUser(PresetPath));
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
        }

        RefreshUserChips();
    }

    /// <summary>
    /// Şeridi baştan çiziyor. Eski kullanıcı yongaları adlarından değil işaretlerinden
    /// bulunuyor, böylece gömülü yongalara hiç dokunulmuyor.
    /// </summary>
    private void RefreshUserChips()
    {
        foreach (var old in ChipStrip.Children.OfType<Button>().Where(IsUserChip).ToList())
            ChipStrip.Children.Remove(old);

        ChipUserSeparator.IsVisible = _userPresets.Count > 0;

        var at = ChipStrip.Children.IndexOf(ChipAddPreset);
        foreach (var preset in _userPresets)
            ChipStrip.Children.Insert(at++, BuildUserChip(preset));
    }

    private static bool IsUserChip(Button button) => button.Tag is PresetProfile;

    private Button BuildUserChip(PresetProfile preset)
    {
        var name = new TextBlock
        {
            Text = preset.Name ?? preset.Id,
            FontStyle = FontStyle.Italic,
            VerticalAlignment = VerticalAlignment.Center
        };

        var remove = new Button
        {
            Content = "×",
            Theme = Application.Current?.FindResource("ChipRemoveButton") as ControlTheme,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(remove, Say("main.preset.delete.name", preset.Name ?? preset.Id));
        remove.Click += (_, e) =>
        {
            e.Handled = true;
            DeletePreset(preset);
        };

        var chip = new Button
        {
            Theme = Application.Current?.FindResource("ChipButton") as ControlTheme,
            Margin = Application.Current?.FindResource("WrapItemMargin") is Thickness margin ? margin : default,
            Tag = preset,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { name, remove }
            }
        };
        ToolTip.SetTip(chip, Say("main.preset.chip.tip"));
        chip.Click += (_, _) => ApplyUserPreset(preset);
        return chip;
    }

    /// <summary>
    /// Yonganın uyguladığı şey gömülü yonganınkiyle aynı yüzey: hedef, niyet, kodek,
    /// doldurma ve kısa kenar; bir de CLI'nın <c>--profil</c> kolunun uyguladığı ses bit hızı
    /// (HandBrake içe aktarımı taşıyor). Kaydedilmemiş alan uygulanmıyor, yoksa ön ayar
    /// taşımadığı bir değeri varsayılana çekerdi. Ses bit hızı Gelişmiş panelin merdiveninde
    /// yoksa en yakın basamağa, eşitlikte yukarıdakine iner.
    /// </summary>
    internal void ApplyUserPreset(PresetProfile preset)
    {
        _intent = preset.Intent;
        _chipSizeCapped = preset.SizeCapped;
        _platformChip = preset.TargetMb is not null;
        SetCodecIndex(preset.Codec switch
        {
            CodecPreference.Compatible => 1,
            CodecPreference.MaxCompression => 2,
            _ => 0
        });
        SetFillIndex(preset.Fill == FillPolicy.FillTarget ? 0 : 1);

        if (preset.TargetMb is { } mb)
        {
            if (mb > SliderTarget.Maximum) SliderTarget.Maximum = mb;
            TxtTarget.Text = mb.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (preset.MaxShortEdge is { } edge)
        {
            var index = FixedResolutionCandidates.ToList().IndexOf(edge);
            if (index >= 0)
            {
                ChkResolution.IsChecked = false;
                FixedResolutionIndex = index + 1;
            }
        }

        if (preset.AudioKbps is { } kbps)
            CmbAdvAudioKbps.SelectedIndex = 1 + Array.IndexOf(AdvancedAudioKbpsCandidates,
                AdvancedAudioKbpsCandidates.OrderBy(c => Math.Abs(c - kbps)).ThenByDescending(c => c).First());

        RefreshChipDerivation();
        RefreshSectionSummaries();
    }

    /// <summary>
    /// O anki arayüzün ön ayar karşılığı. Tavansız yongada hedef kutusundaki sayı planı
    /// kurmuyor, bu yüzden tavansız ön ayara boyut yazılmıyor.
    /// </summary>
    internal PresetProfile CurrentPreset(string name) => new()
    {
        Id = name.Trim(),
        Name = name.Trim(),
        Kind = PresetKind.User,
        TargetMb = _chipSizeCapped ? ParseTargetMb() : null,
        SizeCapped = _chipSizeCapped,
        Intent = _intent,
        Codec = CodecFromIndex(EffectiveCodecIndex),
        Fill = FillPolicyIndex == 1 ? FillPolicy.QualityCeiling : FillPolicy.FillTarget,
        MaxShortEdge = FixedResolutionShortSide,
        AudioKbps = CmbAdvAudioKbps.SelectedIndex > 0 ? AdvancedAudioKbpsCandidates[CmbAdvAudioKbps.SelectedIndex - 1] : null
    };

    private void OnPresetSave(object? sender, RoutedEventArgs e) => SavePreset(TxtPresetName.Text ?? string.Empty);

    /// <summary>
    /// Kaydetme. Boş ad kaydedilmiyor; var olan ad sessizce ezilmiyor, bir kez soruluyor
    /// ve ikinci basış üstüne yazıyor.
    /// </summary>
    internal bool SavePreset(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            ShowPresetNotice(Say("main.preset.name-empty"));
            return false;
        }

        if (_userPresets.Any(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(_overwriteAsked, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            _overwriteAsked = trimmed;
            ShowPresetNotice(Say("main.preset.overwrite", trimmed));
            return false;
        }

        try
        {
            var stored = PresetLibrary.SaveUser(CurrentPreset(trimmed), PresetPath);
            _userPresets.Clear();
            _userPresets.AddRange(stored);
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            ShowPresetNotice(ex is PresetFileException { Error: PresetFileError.ReservedId }
                ? Say("main.preset.error.reserved-id", trimmed)
                : ex.Message);
            return false;
        }

        _overwriteAsked = null;
        TxtPresetName.Text = string.Empty;
        TxtPresetNotice.IsVisible = false;
        (ChipAddPreset.Flyout as Flyout)?.Hide();
        RefreshUserChips();
        return true;
    }

    private void ShowPresetNotice(string text)
    {
        TxtPresetNotice.Text = text;
        TxtPresetNotice.IsVisible = true;
    }

    /// <summary>Silme. Onay yok; silinen profil geri alma için elde tutuluyor.</summary>
    internal void DeletePreset(PresetProfile preset)
    {
        var kalan = _userPresets.Where(p => !string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        try
        {
            PresetLibrary.Export(kalan, PresetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
            return;
        }

        _userPresets.Clear();
        _userPresets.AddRange(kalan);
        _deletedPreset = preset;
        TxtPresetUndo.Text = Say("main.preset.deleted", preset.Name ?? preset.Id);
        PresetUndoBar.IsVisible = true;
        RefreshUserChips();
    }

    private void OnPresetUndo(object? sender, RoutedEventArgs e) => UndoPresetDelete();

    /// <summary>Geri alma, silinen profili olduğu gibi geri yazıyor.</summary>
    internal bool UndoPresetDelete()
    {
        if (_deletedPreset is not { } preset) return false;

        try
        {
            var stored = PresetLibrary.SaveUser(preset, PresetPath);
            _userPresets.Clear();
            _userPresets.AddRange(stored);
        }
        catch (Exception ex) when (ex is PresetFileException or IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
            return false;
        }

        _deletedPreset = null;
        PresetUndoBar.IsVisible = false;
        RefreshUserChips();
        return true;
    }
}
