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

    private void InitSes()
    {
        CmbWebcam.SelectionChanged += (_, _) => KameraSecenekleri();
        RefreshAudioBoxes();
    }

    /// <summary>
    /// İki kutuyu o anki cihaz listesi ve o anki dille yeniden üretir. Seçim addan
    /// korunuyor — indeksten değil, çünkü liste yenilenince sıra kayabiliyor. Ad cihazın
    /// kendi adıdır, kutuda görünen etiket değil: uygulamanın yakaladığı sistem sesi
    /// (<see cref="LoopbackAudio.LoopbackName"/>) her dilde başka yazılır.
    /// </summary>
    internal void RefreshAudioBoxes()
    {
        _fillingAudio = true;
        try
        {
            Quietly(() =>
            {
                _devices = ListAudioDevices();
                FillAudioBox(CmbMicrophone, AudioSourceRole.Microphone, _settings.MicrophoneName);
                FillAudioBox(CmbSystemAudio, AudioSourceRole.SystemAudio, _settings.SystemAudioName);
                RefreshWebcamBoxes();
            });
        }
        finally
        {
            _fillingAudio = false;
        }

        RefreshLevels();
    }

    private bool _fillingAudio;

    /// <summary>Ses cihazı listesinin kapısı; ölçüm sabit liste verir.</summary>
    internal Func<IReadOnlyList<AudioCaptureDevice>> ListAudioDevices { get; set; } = () => CaptureDevices.Instance.Audio;

    private void FillAudioBox(ComboBox box, AudioSourceRole role, string? remembered)
    {
        var wanted = SelectedName(box) ?? remembered;

        var names = new List<string> { string.Empty };
        names.AddRange(_devices.Where(d => d.Role == role).Select(d => d.Name));
        var items = new List<string> { Say("recorder.audio.none") };
        items.AddRange(names.Skip(1).Select(AudioLabel));
        box.Tag = names;
        box.ItemsSource = items;

        var index = 0;
        if (!string.IsNullOrEmpty(wanted))
        {
            for (var i = 1; i < names.Count; i++)
            {
                if (!string.Equals(names[i], wanted, StringComparison.OrdinalIgnoreCase)) continue;
                index = i;
                break;
            }
        }

        box.SelectedIndex = index;
    }

    /// <summary>Kutuda görünen yazı: uygulamanın yakaladığı sistem sesi çevrilir, cihaz adları olduğu gibi kalır.</summary>
    internal static string AudioLabel(string name)
        => string.Equals(name, LoopbackAudio.LoopbackName, StringComparison.Ordinal) ? Say("recorder.audio.loopback") : name;

    private static string? SelectedName(ComboBox box)
        => box.SelectedIndex > 0 && box.Tag is IReadOnlyList<string> names && box.SelectedIndex < names.Count
            ? names[box.SelectedIndex]
            : null;

    /// <summary>Kutudan seçilen cihaz; ilk öğe "sessiz" olduğu için indeks 0 seçimsizlik.</summary>
    internal AudioCaptureDevice? Chosen(AudioSourceRole role)
    {
        var box = role == AudioSourceRole.Microphone ? CmbMicrophone : CmbSystemAudio;
        if (SelectedName(box) is not { } name) return null;

        return _devices.FirstOrDefault(d => d.Role == role
            && string.Equals(d.Name, name, StringComparison.Ordinal));
    }

    private SystemAudioState? _audioShown;

    /// <summary>
    /// Sistem sesi kaynağının halini bildirim satırına yazar; yalnız hal değişince. Çıkış
    /// cihazı değişse ya da kaybolsa da kayıt sürüyor, kullanıcı ses izinde ne olduğunu
    /// buradan öğreniyor. Kaynak ilk cihazına dönünce kendi yazdığı satırı kaldırır.
    /// </summary>
    internal void ShowSystemAudio(SystemAudioState? state)
    {
        if (state is null || state == _audioShown) return;

        var before = _audioShown is { } shown ? AudioStateKey(shown) : null;
        _audioShown = state;
        if (AudioStateKey(state.Value) is { } key) ShowNotice(Say(key));
        else if (before is not null && NoticeText == Say(before)) HideNotice();
    }

    internal static string? AudioStateKey(SystemAudioState state) => state switch
    {
        SystemAudioState.Capturing => null,
        SystemAudioState.Switched => "recorder.audio.loopback.switched",
        SystemAudioState.Unavailable => "recorder.audio.loopback.lost",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

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
                selection, _devices, RecorderArguments.AudioFirstInputIndex,
                _settings.AudioLayout, _settings.AudioFilters, out var plan, out var reason))
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
        RestartLevels();
    }
}
