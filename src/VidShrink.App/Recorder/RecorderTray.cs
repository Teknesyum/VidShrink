using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VidShrink.Ffmpeg;

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

    internal static string Megabytes(double mb, CultureInfo culture) => Math.Max(0, mb).ToString("0.0", culture);

    internal static WindowIcon Render(Color color)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(IconPixels, IconPixels));
        using (var context = bitmap.CreateDrawingContext())
        {
            var radius = IconPixels / 2.0;
            context.DrawEllipse(new SolidColorBrush(color), null, new Point(radius, radius), radius, radius);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}

internal sealed class RecorderTrayHost : IRecorderTrayHost
{
    private readonly Dictionary<Color, WindowIcon> _icons = new();
    private readonly Action _clicked;
    private TrayIcon? _tray;

    internal RecorderTrayHost(Action clicked) => _clicked = clicked;

    public void Update(TrayPhase phase, Color color, string tip)
    {
        if (Application.Current is not { ApplicationLifetime: Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime } app) return;

        if (_tray is null)
        {
            _tray = new TrayIcon();
            _tray.Clicked += (_, _) => _clicked();
            var icons = TrayIcon.GetIcons(app) ?? new TrayIcons();
            icons.Add(_tray);
            TrayIcon.SetIcons(app, icons);
        }

        if (!_icons.TryGetValue(color, out var icon))
        {
            icon = RecorderTray.Render(color);
            _icons[color] = icon;
        }

        _tray.Icon = icon;
        _tray.ToolTipText = tip;
        _tray.IsVisible = true;
    }

    public void Remove()
    {
        if (_tray is null) return;
        _tray.IsVisible = false;
        if (Application.Current is { } app) TrayIcon.GetIcons(app)?.Remove(_tray);
        _tray.Dispose();
        _tray = null;
    }
}
