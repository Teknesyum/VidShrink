using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IReadOnlyList<string> _cameras = Array.Empty<string>();

    internal static readonly WebcamCorner[] WebcamCorners =
        { WebcamCorner.BottomRight, WebcamCorner.BottomLeft, WebcamCorner.TopRight, WebcamCorner.TopLeft };

    private static string CornerKey(WebcamCorner corner) => corner switch
    {
        WebcamCorner.BottomLeft => "recorder.webcam.bottom-left",
        WebcamCorner.TopRight => "recorder.webcam.top-right",
        WebcamCorner.TopLeft => "recorder.webcam.top-left",
        _ => "recorder.webcam.bottom-right"
    };

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

        var width = CmbWebcamSize.SelectedIndex >= 0 ? RecorderArguments.WebcamWidths[CmbWebcamSize.SelectedIndex] : _settings.WebcamWidth;
        CmbWebcamSize.ItemsSource = RecorderArguments.WebcamWidths.Select(w => Say("recorder.webcam.width", w.ToString(CultureInfo.InvariantCulture))).ToList();
        CmbWebcamSize.SelectedIndex = Math.Max(0, IndexOf(RecorderArguments.WebcamWidths, width));

        var corner = CmbWebcamCorner.SelectedIndex >= 0 ? WebcamCorners[CmbWebcamCorner.SelectedIndex] : _settings.WebcamCorner;
        CmbWebcamCorner.ItemsSource = WebcamCorners.Select(c => Say(CornerKey(c))).ToList();
        CmbWebcamCorner.SelectedIndex = Array.IndexOf(WebcamCorners, corner);

        var background = ChosenBackground;
        CmbWebcamBackground.ItemsSource = WebcamBackgrounds.Select(b => Say(BackgroundKey(b))).ToList();
        CmbWebcamBackground.SelectedIndex = Array.IndexOf(WebcamBackgrounds, background);
    }

    private static int IndexOf(IReadOnlyList<int> list, int value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return -1;
    }

    internal RecorderWebcam? ChosenWebcam()
    {
        if (CmbWebcam.SelectedIndex <= 0 || CmbWebcam.SelectedItem is not string device) return null;
        var width = CmbWebcamSize.SelectedIndex >= 0 ? RecorderArguments.WebcamWidths[CmbWebcamSize.SelectedIndex] : _settings.WebcamWidth;
        var corner = CmbWebcamCorner.SelectedIndex >= 0 ? WebcamCorners[CmbWebcamCorner.SelectedIndex] : _settings.WebcamCorner;
        return new RecorderWebcam(device, width, corner, ChosenBackground);
    }

    private void CollectWebcam()
    {
        if (CmbWebcam.SelectedIndex > 0) _settings.WebcamName = CmbWebcam.SelectedItem as string;
        else if (CmbWebcam.SelectedIndex == 0 && (_settings.WebcamName is null || _cameras.Contains(_settings.WebcamName, StringComparer.OrdinalIgnoreCase)))
            _settings.WebcamName = null;
        if (CmbWebcamSize.SelectedIndex >= 0) _settings.WebcamWidth = RecorderArguments.WebcamWidths[CmbWebcamSize.SelectedIndex];
        if (CmbWebcamCorner.SelectedIndex >= 0) _settings.WebcamCorner = WebcamCorners[CmbWebcamCorner.SelectedIndex];
        if (CmbWebcamBackground.SelectedIndex >= 0) _settings.WebcamBackground = WebcamBackgrounds[CmbWebcamBackground.SelectedIndex];
    }
}
