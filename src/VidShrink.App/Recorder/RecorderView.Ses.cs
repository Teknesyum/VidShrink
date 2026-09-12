using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Ses girdisinin seçimi. Liste ffmpeg'in kendisinden geliyor
/// (<see cref="CaptureDevices"/>), argümanı <see cref="AudioCaptureArguments"/> üretiyor;
/// burası yalnızca kullanıcının seçtiğini o ikisine bağlıyor.
///
/// <para>Seçilen cihaz artık listede yoksa kayıt sessize düşmüyor: <c>TryBuild</c>'in
/// sebebi ekranda kendi satırını alıyor ve istek üretilmiyor. Bu depoda SVT-AV1
/// tanımadığı anahtarı sessizce yutmuştu; cihaz adında aynı tuzak burada kapatılıyor.</para>
/// </summary>
internal partial class RecorderView
{
    private IReadOnlyList<AudioCaptureDevice> _devices = Array.Empty<AudioCaptureDevice>();

    private void InitSes() => RefreshAudioBoxes();

    /// <summary>
    /// İki kutuyu o anki cihaz listesi ve o anki dille yeniden üretir. Seçim addan
    /// korunuyor — indeksten değil, çünkü liste yenilenince sıra kayabiliyor.
    /// </summary>
    private void RefreshAudioBoxes()
    {
        _devices = CaptureDevices.Instance.Audio;
        FillAudioBox(CmbMicrophone, AudioSourceRole.Microphone, _settings.MicrophoneName);
        FillAudioBox(CmbSystemAudio, AudioSourceRole.SystemAudio, _settings.SystemAudioName);
    }

    private void FillAudioBox(ComboBox box, AudioSourceRole role, string? remembered)
    {
        var wanted = box.SelectedIndex > 0 ? box.SelectedItem as string : remembered;

        var items = new List<string> { Say("recorder.audio.none") };
        items.AddRange(_devices.Where(d => d.Role == role).Select(d => d.Name));
        box.ItemsSource = items;

        var index = 0;
        if (!string.IsNullOrEmpty(wanted))
        {
            for (var i = 1; i < items.Count; i++)
            {
                if (!string.Equals(items[i], wanted, StringComparison.OrdinalIgnoreCase)) continue;
                index = i;
                break;
            }
        }

        box.SelectedIndex = index;
    }

    /// <summary>Kutudan seçilen cihaz; ilk öğe "sessiz" olduğu için indeks 0 seçimsizlik.</summary>
    internal AudioCaptureDevice? Chosen(AudioSourceRole role)
    {
        var box = role == AudioSourceRole.Microphone ? CmbMicrophone : CmbSystemAudio;
        if (box.SelectedIndex <= 0) return null;

        return _devices.FirstOrDefault(d => d.Role == role
            && string.Equals(d.Name, box.SelectedItem as string, StringComparison.Ordinal));
    }

    internal AudioCaptureSelection Selection =>
        new(Chosen(AudioSourceRole.Microphone), Chosen(AudioSourceRole.SystemAudio));

    /// <summary>
    /// Motora verilecek ses kolu. Hiçbir şey seçilmemişse
    /// <see cref="AudioCapturePlan.Silent"/>, yani sessiz kayıt; seçim doğrulanamazsa
    /// <c>null</c> ve sebebi ekranda.
    /// </summary>
    private AudioCapturePlan? AudioPlan()
    {
        var selection = Selection;
        if (selection.Count == 0) return AudioCapturePlan.Silent;

        if (AudioCaptureArguments.TryBuild(
                selection, _devices, RecorderArguments.AudioFirstInputIndex, out var plan, out var reason))
            return plan;

        ShowError(Say("recorder.error.audio", reason ?? string.Empty));
        return null;
    }

    /// <summary>
    /// Listeyi yeniden okutur. Önbellek boşaltılıyor: programı açtıktan sonra mikrofon
    /// takan kullanıcı <c>CaptureDevices.ReloadAfterFailureMs</c> kadar beklemesin.
    /// </summary>
    private void OnAudioRefresh(object? sender, RoutedEventArgs e)
    {
        CaptureDevices.Invalidate();
        RefreshAudioBoxes();
    }
}
