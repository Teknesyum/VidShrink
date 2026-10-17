using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VidShrink.Ffmpeg;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal enum TrayPhase
{
    Idle,
    Recording,
    Paused
}

internal interface IRecorderTrayHost
{
    void Update(TrayPhase phase, Color color, string tip);

    void Remove();
}

internal static class RecorderTray
{
    internal const int IconPixels = 32;

    internal static TrayPhase PhaseOf(bool hasSession, RecorderState state)
        => !hasSession ? TrayPhase.Idle
            : state == RecorderState.Paused ? TrayPhase.Paused
            : state == RecorderState.Running ? TrayPhase.Recording
            : TrayPhase.Idle;

    internal static string BrushKey(TrayPhase phase) => phase switch
    {
        TrayPhase.Recording => "NeonEmber",
        TrayPhase.Paused => "NeonBlue",
        _ => "TextDisabled"
    };

    internal static string StateKey(TrayPhase phase) => phase switch
    {
        TrayPhase.Recording => "recorder.strip.live",
        TrayPhase.Paused => "recorder.strip.paused",
        _ => "recorder.strip.idle"
    };

    internal static string Megabytes(double mb, CultureInfo culture) => Bicim.Boyut.Mb(Math.Max(0, mb), culture);

    internal static WindowIcon Render(Color color)
    {
        using var stream = new MemoryStream(RenderPng(color));
        return new WindowIcon(stream);
    }

    /// <summary>
    /// Simgenin PNG baytları. Ölçü buradan okur: <see cref="WindowIcon.Save"/> platformun simge
    /// nesnesinden geçiyor ve Linux'un başsız arka ucunda pikselleri geri vermiyor.
    /// </summary>
    internal static byte[] RenderPng(Color color)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(IconPixels, IconPixels));
        using (var context = bitmap.CreateDrawingContext())
        {
            var radius = IconPixels / 2.0;
            context.DrawEllipse(new SolidColorBrush(color), null, new Point(radius, radius), radius, radius);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}

internal sealed class RecorderTrayHost : IRecorderTrayHost
{
    private readonly Dictionary<Color, WindowIcon> _icons = new();
    private readonly TrayIconHost _host;

    internal RecorderTrayHost(Action clicked) => _host = new TrayIconHost(clicked);

    public void Update(TrayPhase phase, Color color, string tip)
    {
        if (!_icons.TryGetValue(color, out var icon))
        {
            icon = RecorderTray.Render(color);
            _icons[color] = icon;
        }

        _host.Show(icon, tip);
    }

    public void Remove() => _host.Remove();
}
