using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IReadOnlyList<string> _cameras = Array.Empty<string>();

    internal static readonly WebcamShape[] WebcamShapes = { WebcamShape.Rectangle, WebcamShape.Rounded, WebcamShape.Circle };

    private static string ShapeKey(WebcamShape shape) => shape switch
    {
        WebcamShape.Rounded => "recorder.webcam.shape.rounded",
        WebcamShape.Circle => "recorder.webcam.shape.circle",
        _ => "recorder.webcam.shape.rectangle"
    };

    private WebcamShape ChosenShape
        => CmbWebcamShape.SelectedIndex >= 0 ? WebcamShapes[CmbWebcamShape.SelectedIndex] : _settings.WebcamShape;

    private WebcamPlacement ChosenPlacement => new(_settings.WebcamX, _settings.WebcamY, _settings.WebcamSize);

    internal static readonly WebcamBackground[] WebcamBackgrounds =
        { WebcamBackground.Keep, WebcamBackground.Static, WebcamBackground.Green };

    private static string BackgroundKey(WebcamBackground background) => background switch
    {
        WebcamBackground.Static => "recorder.webcam.background.static",
        WebcamBackground.Green => "recorder.webcam.background.green",
        _ => "recorder.webcam.background.keep"
    };

    private WebcamBackground ChosenBackground
        => CmbWebcamBackground.SelectedIndex >= 0 ? WebcamBackgrounds[CmbWebcamBackground.SelectedIndex] : _settings.WebcamBackground;

    private Func<IReadOnlyList<string>> _cameraSource = () => CaptureDevices.Instance.Video;

    internal Func<IReadOnlyList<string>> CameraSource
    {
        get => _cameraSource;
        set
        {
            _cameraSource = value;
            Quietly(RefreshWebcamBoxes);
        }
    }

    private void RefreshWebcamBoxes()
    {
        PanelWebcam.IsVisible = OperatingSystem.IsWindows();
        _cameras = CameraSource();

        var wanted = CmbWebcam.SelectedIndex > 0 ? CmbWebcam.SelectedItem as string : _settings.WebcamName;
        var items = new List<string> { Say("recorder.webcam.none") };
        items.AddRange(_cameras);
        CmbWebcam.ItemsSource = items;
        var index = string.IsNullOrEmpty(wanted) ? 0 : items.FindIndex(1, i => string.Equals(i, wanted, StringComparison.OrdinalIgnoreCase));
        CmbWebcam.SelectedIndex = Math.Max(0, index);

        var shape = ChosenShape;
        CmbWebcamShape.ItemsSource = WebcamShapes.Select(s => Say(ShapeKey(s))).ToList();
        CmbWebcamShape.SelectedIndex = Array.IndexOf(WebcamShapes, shape);
        if (!_webcamLoaded)
        {
            _webcamLoaded = true;
            ChkWebcamMirror.IsChecked = _settings.WebcamMirror;
            CmbWebcam.SelectionChanged += (_, _) => SyncRegionEditor();
            CmbWebcamShape.SelectionChanged += (_, _) => SyncRegionEditor();
        }

        var background = ChosenBackground;
        CmbWebcamBackground.ItemsSource = WebcamBackgrounds.Select(b => Say(BackgroundKey(b))).ToList();
        CmbWebcamBackground.SelectedIndex = Array.IndexOf(WebcamBackgrounds, background);
        KameraSecenekleri();
    }

    /// <summary>
    /// Kamera seçilmemişken biçim, ayna ve arka plan kapalı: <see cref="ChosenWebcam"/> o durumda
    /// bindirme kurmuyor, seçilebilir görünen kutular kayda hiçbir şey katmıyordu.
    /// </summary>
    private void KameraSecenekleri()
    {
        var acik = CmbWebcam.SelectedIndex > 0;
        CmbWebcamShape.IsEnabled = acik;
        ChkWebcamMirror.IsEnabled = acik;
        CmbWebcamBackground.IsEnabled = acik;
    }

    private bool _webcamLoaded;

    /// <summary>
    /// Yer ve boy köşe/genişlik kutusundan değil, bölge düzenleyicisinde sürüklenen kutunun
    /// oranlarından gelir (<see cref="OnCameraCommitted"/>); kayıt çıktısının boyutundan bağımsızdır.
    /// </summary>
    internal RecorderWebcam? ChosenWebcam()
    {
        if (CmbWebcam.SelectedIndex <= 0 || CmbWebcam.SelectedItem is not string device) return null;
        return new RecorderWebcam(device, RecorderArguments.DefaultWebcamWidth, WebcamCorner.BottomRight, ChosenBackground)
        {
            Shape = ChosenShape,
            Mirror = ChkWebcamMirror.IsChecked ?? false,
            Placement = ChosenPlacement
        };
    }

    private void OnCameraCommitted(object? sender, WebcamPlacement placement)
    {
        _settings.WebcamX = placement.X;
        _settings.WebcamY = placement.Y;
        _settings.WebcamSize = placement.Size;
        StoreChoices();
    }

    private void CollectWebcam()
    {
        if (CmbWebcam.SelectedIndex > 0) _settings.WebcamName = CmbWebcam.SelectedItem as string;
        else if (CmbWebcam.SelectedIndex == 0 && (_settings.WebcamName is null || _cameras.Contains(_settings.WebcamName, StringComparer.OrdinalIgnoreCase)))
            _settings.WebcamName = null;
        if (CmbWebcamShape.SelectedIndex >= 0) _settings.WebcamShape = WebcamShapes[CmbWebcamShape.SelectedIndex];
        _settings.WebcamMirror = ChkWebcamMirror.IsChecked ?? false;
        if (CmbWebcamBackground.SelectedIndex >= 0) _settings.WebcamBackground = WebcamBackgrounds[CmbWebcamBackground.SelectedIndex];
    }
}
