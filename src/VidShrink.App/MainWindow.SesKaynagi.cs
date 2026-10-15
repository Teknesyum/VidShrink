using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// ARC-19: yalnız ses taşıyan dosya Dönüştür'ün kaynağı olur, Küçült'ün olmaz. Küçült hata satırını gösterir ve
/// kaynaksız kalır; Dönüştür'ün kap listesi ses kaplarına daralır. Video kaynağa dönülünce ses kaynağından önceki
/// kap seçimi geri gelir.
/// </summary>
public partial class MainWindow
{
    private MediaInfo? _sesKaynagi;
    private string? _sesOncesiKap;

    internal MediaInfo? AudioSourceForTest => _sesKaynagi;

    private void SesKaynaginiYukle(MediaInfo info)
    {
        _info = null;
        _probed = null;
        _sesKaynagi = info;
        ResetTracksForSource();
        BtnStart.IsEnabled = BtnConvert.IsEnabled = false;
        Fade(InfoGrid, false);
        Fade(DropZone, true);
        ResetPlanView();
        RefreshQualityPanels();
        RefreshQualityTargetAvailability();
        ShowSourceName();
        ReportSourceError(Say("main.error.audio-only-shrink"));
        KapSecenekleriniYokla();
        OzelDonusturAlanlari();
        RefreshConversion();
    }

    private bool KapKaynagaUyar(ComboBoxItem item)
        => ConversionArguments.ContainerFitsSource(item.Tag as string ?? "", ConvertInfo);

    private void SesOncesiKabiGeriKoy()
    {
        if (_sesOncesiKap is null || ConvertInfo is { IsAudioOnly: true }) return;
        var tag = _sesOncesiKap;
        _sesOncesiKap = null;
        var item = CmbContainer.Items.OfType<ComboBoxItem>().FirstOrDefault(candidate => candidate.Tag as string == tag && candidate.IsVisible);
        if (item is null || ReferenceEquals(CmbContainer.SelectedItem, item)) return;
        var wasSyncing = _syncing;
        _syncing = true;
        CmbContainer.SelectedItem = item;
        _syncing = wasSyncing;
        OzelDonusturAlanlari();
        RefreshConversion();
    }

    private int IlkGorunurKap()
    {
        var items = CmbContainer.Items.OfType<ComboBoxItem>().ToList();
        var index = items.FindIndex(item => item.IsVisible);
        return index < 0 ? 0 : index;
    }
}
