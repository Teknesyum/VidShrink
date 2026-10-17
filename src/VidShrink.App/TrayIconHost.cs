using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace VidShrink.App;

/// <summary>
/// Tek bir tepsi simgesinin yaşamı: ilk gösterişte uygulamanın simge listesine eklenir, kaldırılınca
/// listeden çıkar ve atılır. Kaydedicinin simgesi (<see cref="Recorder.RecorderTrayHost"/>) ve ana
/// pencerenin tepsiye küçültmesi (<see cref="MainWindowTrayHost"/>) aynı parçayı kullanır.
/// </summary>
internal sealed class TrayIconHost
{
    private readonly Action _clicked;
    private TrayIcon? _tray;

    internal TrayIconHost(Action clicked) => _clicked = clicked;

    internal bool Visible => _tray is { IsVisible: true };

    internal void Show(WindowIcon? icon, string tip, NativeMenu? menu = null)
    {
        if (Application.Current is not { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime } app) return;

        if (_tray is null)
        {
            _tray = new TrayIcon();
            _tray.Clicked += (_, _) => _clicked();
            var icons = TrayIcon.GetIcons(app) ?? new TrayIcons();
            icons.Add(_tray);
            TrayIcon.SetIcons(app, icons);
        }

        if (icon is not null) _tray.Icon = icon;
        if (menu is not null && _tray.Menu is null) _tray.Menu = menu;
        _tray.ToolTipText = tip;
        _tray.IsVisible = true;
    }

    internal void Remove()
    {
        if (_tray is null) return;
        _tray.IsVisible = false;
        if (Application.Current is { } app) TrayIcon.GetIcons(app)?.Remove(_tray);
        _tray.Dispose();
        _tray = null;
    }
}
