using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// Dönüştür sekmesinin kap listesi ffmpeg derlemesini izler: kodlayıcısı olmayan kap (hareketli WebP,
/// AVIF) listede görünmez. Liste öğesi silinmez, gizlenir: ayar kabı sıra numarasıyla saklıyor.
/// Yoklama okunamadıysa (<see cref="EncoderCapabilities.Loaded"/> false) kap açık kalır.
/// Ara kodek profilleri (ProRes, DNxHR) kodek listesinde hep durur; kodlayıcısı yoksa yalnız kapanır.
/// </summary>
public partial class MainWindow
{
    internal static ComboBoxItem[] ParallelJobChoices(int logicalProcessors) =>
        Enumerable.Range(1, ParallelJobs.Ceiling)
            .Select(n => new ComboBoxItem
            {
                Content = n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IsEnabled = n <= ParallelJobs.Max(logicalProcessors)
            })
            .ToArray();

    internal static string ConversionNoteKey(ConversionNote note) => note switch
    {
        ConversionNote.WebpBitrateIgnored => "main.convert.note.webp-bitrate-ignored",
        _ => "main.convert.ready"
    };

    internal string? ConvertStatusForTest => TxtConvertValidation.Text;

    private void KapSecenekleriniYokla()
    {
        var encoders = _encoders is EncoderCapabilities { Loaded: false } ? null : _encoders;
        foreach (var item in CmbContainer.Items.OfType<ComboBoxItem>())
        {
            var available = ConversionArguments.ContainerAvailable(item.Tag as string ?? "", encoders);
            item.IsVisible = available;
            item.IsEnabled = available;
        }

        foreach (var item in CmbConvertCodec.Items.OfType<ComboBoxItem>())
            if (IntermediateCodecs.FromId(item.Tag as string) is { } profile)
                item.IsEnabled = encoders is null || encoders.HasEncoder(profile.Encoder);

        if (CmbContainer.SelectedItem is not ComboBoxItem { IsVisible: false }) return;
        var wasSyncing = _syncing;
        _syncing = true;
        CmbContainer.SelectedIndex = 0;
        _syncing = wasSyncing;
        OzelDonusturAlanlari();
        RefreshConversion();
    }
}
