using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// SIK-P1: Ayarlar'daki kodlama onceligi ve kodlayici is parcacigi siniri. Oncelik sureci
/// baslatan kosucuya plan uzerinden gider; is parcacigi siniri yalniz siniri dinleyen
/// kodlayicida (<see cref="FfmpegArguments.HonoursThreadLimit"/>) gorunur.
/// </summary>
public partial class MainWindow
{
    internal static readonly int[] EncoderThreadCandidates = { 1, 2, 4, 8, 16 };

    internal static bool EncoderThreadsApply(string? lockedCodec, int encoderPathIndex)
    {
        if (encoderPathIndex == 2) return false;
        return string.IsNullOrEmpty(lockedCodec) || FfmpegArguments.HonoursThreadLimit(lockedCodec);
    }

    internal EncodePriority SelectedEncodePriority
        => CmbEncodePriority.SelectedIndex is >= 0 and <= (int)EncodePriority.AboveNormal
            ? (EncodePriority)CmbEncodePriority.SelectedIndex
            : EncodePriority.Normal;

    internal int SelectedEncoderThreads
        => CmbEncoderThreads.SelectedIndex > 0 ? EncoderThreadCandidates[CmbEncoderThreads.SelectedIndex - 1] : 0;

    private void InitializeEncodeLoadUi()
    {
        FillEncodeLoadTexts();
        CmbEncodePriority.SelectedIndex = 0;
        CmbEncoderThreads.SelectedIndex = 0;
        Watch(CmbEncodePriority, SelectingItemsControl.SelectedIndexProperty, SaveAppSettings);
        Watch(CmbEncodePriority, SelectingItemsControl.SelectedIndexProperty, OnOptionChanged);
        Watch(CmbEncoderThreads, SelectingItemsControl.SelectedIndexProperty, SaveAppSettings);
        Watch(CmbEncoderThreads, SelectingItemsControl.SelectedIndexProperty, OnOptionChanged);
        Watch(CmbAdvCodecLock, SelectingItemsControl.SelectedIndexProperty, RefreshEncoderThreadsRow);
        foreach (var radio in new[] { RbAdvPathAuto, RbAdvPathSoftware, RbAdvPathHardware })
            Watch(radio, ToggleButton.IsCheckedProperty, RefreshEncoderThreadsRow);
        RefreshEncoderThreadsRow();
    }

    private void FillEncodeLoadTexts()
    {
        CmbEncodePriority.ItemsSource = new[]
        {
            Say("settings-tab.encode-priority.normal"),
            Say("settings-tab.encode-priority.below-normal"),
            Say("settings-tab.encode-priority.low"),
            Say("settings-tab.encode-priority.above-normal")
        };
        CmbEncoderThreads.ItemsSource = new[] { new ComboBoxItem { Content = Say("main.plan.automatic") } }
            .Concat(EncoderThreadCandidates.Select(n => new ComboBoxItem
            {
                Content = n.ToString(CultureInfo.InvariantCulture),
                IsEnabled = n <= Environment.ProcessorCount
            }))
            .ToArray();
    }

    private void RefreshEncodeLoadTexts()
    {
        var priority = CmbEncodePriority.SelectedIndex;
        var threads = CmbEncoderThreads.SelectedIndex;
        var wasSyncing = _syncing;
        var wasSettingsSyncing = _settingsSyncing;
        _syncing = _settingsSyncing = true;
        FillEncodeLoadTexts();
        CmbEncodePriority.SelectedIndex = Math.Max(0, priority);
        CmbEncoderThreads.SelectedIndex = Math.Max(0, threads);
        _syncing = wasSyncing;
        _settingsSyncing = wasSettingsSyncing;
    }

    private void RestoreEncodeLoad(AppSettings settings)
    {
        CmbEncodePriority.SelectedIndex = (int)settings.EncodePriority;
        var at = Array.IndexOf(EncoderThreadCandidates, settings.EncoderThreads);
        CmbEncoderThreads.SelectedIndex = at < 0 ? 0 : at + 1;
    }

    private void ApplyEncodeLoad(PlanOptions options)
    {
        options.Priority = SelectedEncodePriority;
        options.EncoderThreads = SelectedEncoderThreads > 0 ? SelectedEncoderThreads : null;
    }

    private void RefreshEncoderThreadsRow()
        => EncoderThreadsRow.IsVisible = EncoderThreadsApply(AdvancedText(CmbAdvCodecLock), AdvEncoderPathIndex);
}
