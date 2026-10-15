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
/// Donanım kodlayıcıları (NVENC, QSV, AMF) yalnız yoklamada çalıştıysa görünür; kodek seçimi adla
/// saklandığı için (<see cref="ConversionCodecChoice"/>) listeye girmeleri eski kaydı kaydırmaz.
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
        ConversionNote.GifFitsTarget => "main.convert.note.gif-target",
        ConversionNote.FiltersReencode => "main.convert.note.filters-reencode",
        _ => "main.convert.ready"
    };

    internal string? ConvertStatusForTest => TxtConvertValidation.Text;

    private void KapSecenekleriniYokla()
    {
        var encoders = _encoders is EncoderCapabilities { Loaded: false } ? null : _encoders;
        foreach (var item in CmbContainer.Items.OfType<ComboBoxItem>())
        {
            var available = ConversionArguments.ContainerAvailable(item.Tag as string ?? "", encoders) && KapKaynagaUyar(item);
            item.IsVisible = available;
            item.IsEnabled = available;
        }

        foreach (var item in CmbConvertCodec.Items.OfType<ComboBoxItem>())
            if (IntermediateCodecs.FromId(item.Tag as string) is { } profile)
                item.IsEnabled = encoders is null || encoders.HasEncoder(profile.Encoder);

        DonanimKodlayicilariniYokla();

        if (CmbContainer.SelectedItem is not ComboBoxItem { IsVisible: false } hidden)
        {
            SesOncesiKabiGeriKoy();
            return;
        }
        if (ConvertInfo is { IsAudioOnly: true }) _sesOncesiKap ??= hidden.Tag as string;
        var wasSyncing = _syncing;
        _syncing = true;
        CmbContainer.SelectedIndex = IlkGorunurKap();
        _syncing = wasSyncing;
        OzelDonusturAlanlari();
        RefreshConversion();
    }

    /// <summary>
    /// Donanım kodlayıcılarını gecikmeli yoklamadan okur. Kodlayıcılar sırayla sorulur: biri ölçülmeden
    /// sonrakine geçilmez, böylece aynı anda tek yoklama süreci koşar. Ölçüm bitince geçit bu yöntemi
    /// yeniden çağırır. Seçili kodlayıcı ölçülüp çalışmıyorsa seçim H.264'e döner; ölçülmemişse yerinde kalır.
    /// </summary>
    private void DonanimKodlayicilariniYokla()
    {
        var gate = _planEncoders;
        var waiting = false;
        foreach (var item in CmbConvertCodec.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is not string tag || !ConversionCodecChoice.Hardware.Contains(tag)) continue;
            var state = gate is null || waiting ? EncoderProbeState.Unmeasured : gate.EncoderState(tag);
            if (gate is not null && gate.Pending) waiting = true;
            var works = state == EncoderProbeState.Working;
            item.IsVisible = works;
            item.IsEnabled = works;
            if (state == EncoderProbeState.NotWorking && ReferenceEquals(CmbConvertCodec.SelectedItem, item))
                KodekSec(ConversionCodecChoice.Default);
        }
    }

    private void KodekSec(string tag)
    {
        var items = CmbConvertCodec.Items.OfType<ComboBoxItem>().ToList();
        CmbConvertCodec.SelectedItem = items.FirstOrDefault(item => item.Tag as string == tag) ?? items.FirstOrDefault();
    }
}
