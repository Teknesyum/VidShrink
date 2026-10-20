using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
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
        RefreshDeviceBox();
    }

    /// <summary>
    /// Ana kaynak olacak aygıtın kutusu: kamera katmanıyla aynı liste (Windows'ta dshow, macOS'ta
    /// avfoundation kameraları, Linux'ta v4l2 aygıt dosyaları). Seçim addan
    /// korunur; hatırlanan aygıt takılı değilse kutu boş kalır ve motor adı eksik isteği reddeder,
    /// başka bir kamera sessizce yerine geçmez.
    /// </summary>
    private void RefreshDeviceBox()
    {
        if (!_deviceLoaded)
        {
            _deviceLoaded = true;
            _chosenDevice = _settings.DeviceName;
            _chosenMode = CameraMode.Parse(_settings.DeviceMode);
            CmbDevice.SelectionChanged += (_, _) =>
            {
                if (!_fillingDevice && CmbDevice.SelectedItem is string device) _chosenDevice = device;
                RefreshDeviceModes();
            };
            CmbDeviceMode.SelectionChanged += (_, _) =>
            {
                if (!_fillingMode && CmbDeviceMode.SelectedIndex >= 0) _chosenMode = ModeAt(CmbDeviceMode.SelectedIndex);
            };
            CmbTarget.SelectionChanged += (_, _) => RefreshDeviceModes();
        }

        var list = _cameras.ToList();
        var index = string.IsNullOrEmpty(_chosenDevice) ? 0 : list.FindIndex(c => string.Equals(c, _chosenDevice, StringComparison.OrdinalIgnoreCase));
        _fillingDevice = true;
        try
        {
            CmbDevice.ItemsSource = list;
            CmbDevice.SelectedIndex = list.Count == 0 ? -1 : index;
        }
        finally
        {
            _fillingDevice = false;
        }

        RefreshDeviceModes();
    }

    internal Func<string, Task<IReadOnlyList<CameraMode>>> DeviceModeSource { get; set; }
        = device => Task.Run(() => CaptureDevices.ModesOf(device));

    private IReadOnlyList<CameraMode> _deviceModes = Array.Empty<CameraMode>();

    private string? _modesFor;

    private CameraMode? _chosenMode;

    private bool _fillingMode;

    private int _modeQuery;

    private CameraMode? ModeAt(int index) => index > 0 && index <= _deviceModes.Count ? _deviceModes[index - 1] : null;

    internal CameraMode? ChosenDeviceMode
        => _modesFor is not null && string.Equals(_modesFor, ChosenDevice, StringComparison.OrdinalIgnoreCase)
            ? ModeAt(CmbDeviceMode.SelectedIndex)
            : null;

    private void RefreshDeviceModes()
    {
        var query = ++_modeQuery;
        var device = SelectedTarget == RecorderTargetKind.Device ? ChosenDevice : null;
        if (device is null)
        {
            FillDeviceModes(null, Array.Empty<CameraMode>());
            return;
        }

        Task<IReadOnlyList<CameraMode>> pending;
        try { pending = DeviceModeSource(device); }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or System.ComponentModel.Win32Exception)
        {
            FillDeviceModes(device, Array.Empty<CameraMode>());
            return;
        }

        if (pending.IsCompleted)
        {
            ApplyDeviceModes(query, device, pending);
            return;
        }

        pending.ContinueWith(done => Dispatcher.UIThread.Post(() => ApplyDeviceModes(query, device, done)), TaskScheduler.Default);
    }

    private void ApplyDeviceModes(int query, string device, Task<IReadOnlyList<CameraMode>> done)
    {
        if (query != _modeQuery) return;
        FillDeviceModes(device, done.IsCompletedSuccessfully ? done.Result : Array.Empty<CameraMode>());
    }

    private void FillDeviceModes(string? device, IReadOnlyList<CameraMode> modes)
    {
        _deviceModes = modes;
        _modesFor = device;
        var items = new List<string> { Say("recorder.target.device-mode.auto") };
        items.AddRange(modes.Select(m => Say("recorder.target.device-mode.item", m.Width, m.Height, m.Rate)));
        var index = _chosenMode is null ? -1 : modes.ToList().IndexOf(_chosenMode);
        _fillingMode = true;
        try
        {
            CmbDeviceMode.ItemsSource = items;
            CmbDeviceMode.SelectedIndex = index + 1;
        }
        finally
        {
            _fillingMode = false;
        }

        RowDeviceMode.IsVisible = device is not null && modes.Count > 0;
    }

    private bool _deviceLoaded;

    private bool _fillingDevice;

    private string? _chosenDevice;

    internal string? ChosenDevice => CmbDevice.SelectedItem as string;

    private void CollectDevice()
    {
        if (CmbDevice.SelectedItem is string device) _settings.DeviceName = device;
        if (_modesFor is not null && string.Equals(_modesFor, ChosenDevice, StringComparison.OrdinalIgnoreCase))
            _settings.DeviceMode = ChosenDeviceMode?.ToString();
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
