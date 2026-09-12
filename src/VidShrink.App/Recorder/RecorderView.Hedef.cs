using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// Ne kaydedileceğinin seçimi ve istek üretimi. Motor argüman üretmeyi
/// <see cref="RecorderArguments"/>'a bırakıyor; burası yalnızca kullanıcının seçtiğini bir
/// <see cref="RecorderRequest"/>'e çeviriyor.
/// </summary>
internal partial class RecorderView
{
    /// <summary>
    /// Listelenen kodlayıcılar. Yetkili taraf motordur: bu listedeki her ad
    /// <see cref="RecorderArguments.Validate"/>'ten geçmek zorunda ve bunu
    /// <c>KaydediciArayuzTests</c> pimliyor. Uydurma bir ad motorca sessizce yutulmuyor,
    /// doğrulama satırıyla geri geliyor.
    /// </summary>
    internal static readonly string[] Codecs =
    {
        "libx264", "libx265", "libsvtav1", "libvpx-vp9",
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv",
        "h264_amf", "hevc_amf"
    };

    /// <summary>
    /// Ön ayarlar. Kayıt gerçek zamanlı: kodlama kareyi bekletemediği için liste hızlı
    /// uçtan başlıyor ve <c>medium</c>'da bitiyor.
    /// </summary>
    internal static readonly string[] Presets =
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium"
    };

    /// <summary>
    /// Koşan makinenin yakalama girdisi. <see cref="RecorderPlatform"/> istekten okunuyor;
    /// arayüz kendi makinesini bildiriyor.
    /// </summary>
    internal static RecorderPlatform HostPlatform =>
        OperatingSystem.IsWindows() ? RecorderPlatform.Windows
        : OperatingSystem.IsMacOS() ? RecorderPlatform.MacOs
        : RecorderPlatform.Linux;

    private void InitHedef()
    {
        CmbTarget.ItemsSource = TargetLabels();

        CmbCodec.ItemsSource = Codecs;
        CmbPreset.ItemsSource = Presets;

        CmbTarget.SelectedIndex = (int)_settings.Target;
        CmbCodec.SelectedItem = Codecs.Contains(_settings.Codec, StringComparer.Ordinal)
            ? _settings.Codec
            : RecorderArguments.DefaultVideoCodec;
        CmbPreset.SelectedItem = Presets.Contains(_settings.Preset, StringComparer.Ordinal)
            ? _settings.Preset
            : RecorderArguments.DefaultPreset;

        TxtFps.Text = _settings.Fps.ToString(CultureInfo.InvariantCulture);
        TxtQuality.Text = _settings.Quality.ToString("0.##", CultureInfo.InvariantCulture);
        ChkCursor.IsChecked = _settings.ShowCursor;
        TxtWindowTitle.Text = _settings.WindowTitle ?? string.Empty;
        TxtRegionX.Text = _settings.RegionX.ToString(CultureInfo.InvariantCulture);
        TxtRegionY.Text = _settings.RegionY.ToString(CultureInfo.InvariantCulture);
        TxtRegionWidth.Text = _settings.RegionWidth.ToString(CultureInfo.InvariantCulture);
        TxtRegionHeight.Text = _settings.RegionHeight.ToString(CultureInfo.InvariantCulture);
        TxtOutputFolder.Text = _settings.ResolveFolder();

        CmbTarget.SelectionChanged += (_, _) => RefreshTargetRows();
        RefreshTargetRows();
    }

    private static List<string> TargetLabels() => new[]
    {
        RecorderTargetKind.Screen,
        RecorderTargetKind.Window,
        RecorderTargetKind.Region
    }.Select(TargetLabel).ToList();

    /// <summary>
    /// Kutunun öğelerini o anki dilde yeniden üretir. Seçim indeksten korunuyor: liste sırası
    /// <see cref="RecorderTargetKind"/> ile aynı olduğu için dil değişimi seçimi kaydırmıyor.
    /// </summary>
    private void RefreshTargetLabels()
    {
        var selected = CmbTarget.SelectedIndex;
        CmbTarget.ItemsSource = TargetLabels();
        CmbTarget.SelectedIndex = selected >= 0 ? selected : (int)_settings.Target;
    }

    private static string TargetLabel(RecorderTargetKind kind) => kind switch
    {
        RecorderTargetKind.Window => Say("recorder.target.window"),
        RecorderTargetKind.Region => Say("recorder.target.region"),
        _ => Say("recorder.target.screen")
    };

    /// <summary>Seçilen hedef. Liste sırası <see cref="RecorderTargetKind"/> ile aynı.</summary>
    internal RecorderTargetKind SelectedTarget =>
        CmbTarget.SelectedIndex >= 0 ? (RecorderTargetKind)CmbTarget.SelectedIndex : RecorderTargetKind.Screen;

    private void RefreshTargetRows()
    {
        RowWindow.IsVisible = SelectedTarget == RecorderTargetKind.Window;
        RowRegion.IsVisible = SelectedTarget == RecorderTargetKind.Region;
    }

    /// <summary>
    /// Kullanıcının seçtiği istek. Sayı alanlarından biri okunamazsa istek üretilmiyor ve
    /// sebebi ekranda bildiriliyor; sessiz varsayılana düşmüyor.
    /// </summary>
    internal RecorderRequest? BuildRequest()
    {
        if (!int.TryParse(TxtFps.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fps))
        {
            ShowError(Say("recorder.error.number", Say("recorder.option.fps")));
            return null;
        }

        if (!double.TryParse(TxtQuality.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var quality))
        {
            ShowError(Say("recorder.error.number", Say("recorder.option.quality")));
            return null;
        }

        if (AudioPlan() is not { } audio) return null;

        var target = SelectedTarget;
        RecorderRegion? region = null;

        if (target == RecorderTargetKind.Region)
        {
            if (Rect() is not { } rect) return null;
            region = rect;
        }

        return new RecorderRequest
        {
            Platform = HostPlatform,
            Target = target,
            Fps = fps,
            Quality = quality,
            ShowCursor = ChkCursor.IsChecked ?? false,
            VideoCodec = CmbCodec.SelectedItem as string ?? RecorderArguments.DefaultVideoCodec,
            Preset = CmbPreset.SelectedItem as string ?? RecorderArguments.DefaultPreset,
            WindowTitle = string.IsNullOrWhiteSpace(TxtWindowTitle.Text) ? null : TxtWindowTitle.Text
        }
        with
        { Region = region, Audio = audio };
    }

    private RecorderRegion? Rect()
    {
        var boxes = new (TextBox Box, string LabelKey)[]
        {
            (TxtRegionX, "recorder.target.region-x"),
            (TxtRegionY, "recorder.target.region-y"),
            (TxtRegionWidth, "recorder.target.region-width"),
            (TxtRegionHeight, "recorder.target.region-height")
        };

        var read = new List<int>();
        foreach (var (box, labelKey) in boxes)
        {
            if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                ShowError(Say("recorder.error.number", Say(labelKey)));
                return null;
            }

            read.Add(value);
        }

        return new RecorderRegion(read[0], read[1], read[2], read[3]);
    }

    /// <summary>Seçimleri ayara yazar; bir sonraki açılış aynı yerden başlıyor.</summary>
    private void StoreChoices()
    {
        _settings.Target = SelectedTarget;
        _settings.Codec = CmbCodec.SelectedItem as string ?? _settings.Codec;
        _settings.Preset = CmbPreset.SelectedItem as string ?? _settings.Preset;
        _settings.ShowCursor = ChkCursor.IsChecked ?? false;
        _settings.WindowTitle = string.IsNullOrWhiteSpace(TxtWindowTitle.Text) ? null : TxtWindowTitle.Text;
        _settings.MicrophoneName = Chosen(AudioSourceRole.Microphone)?.Name;
        _settings.SystemAudioName = Chosen(AudioSourceRole.SystemAudio)?.Name;

        if (int.TryParse(TxtFps.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fps)) _settings.Fps = fps;
        if (double.TryParse(TxtQuality.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var quality)) _settings.Quality = quality;
        if (int.TryParse(TxtRegionX.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) _settings.RegionX = x;
        if (int.TryParse(TxtRegionY.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) _settings.RegionY = y;
        if (int.TryParse(TxtRegionWidth.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)) _settings.RegionWidth = w;
        if (int.TryParse(TxtRegionHeight.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)) _settings.RegionHeight = h;
        if (!string.IsNullOrWhiteSpace(TxtOutputFolder.Text)) _settings.OutputFolder = TxtOutputFolder.Text;

        _settings.Save(RecorderSettings.FilePath);
    }
}
