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
        => ConversionArguments.ContainerFitsSource(item.Tag as string ?? "", ConvertInfo, SesGoruntusu());

    private bool SesKaynagiAcik => _sesKaynagi is not null && _info is null && _sequence is null;

    /// <summary>ARC-20: ses kaynağında seçilen görüntü; ses kaynağı yoksa hep <see cref="AudioVisual.None"/>.</summary>
    private AudioVisual SesGoruntusu() => !SesKaynagiAcik ? AudioVisual.None : SelectedTag(CmbConvertAudioVisual) switch
    {
        "cover" => AudioVisual.Cover,
        "waveform" => AudioVisual.Waveform,
        _ => AudioVisual.None
    };

    /// <summary>Görüntü seçilince video kapları açılır; seçili kap ses kabıysa MP4'e geçilir. Görüntü kalkınca ses kabına dönülür.</summary>
    private void OnSesGoruntusuDegisti()
    {
        if (_syncing) return;
        KapSecenekleriniYokla();
        if (SesGoruntusu() != AudioVisual.None && new ConversionPlan { Container = SelectedTag(CmbContainer) ?? "" }.AudioOnly
            && CmbContainer.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == "mp4" && item.IsVisible) is { } mp4)
        {
            var wasSyncing = _syncing;
            _syncing = true;
            CmbContainer.SelectedItem = mp4;
            _syncing = wasSyncing;
        }
        OnConvertChanged();
    }

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
