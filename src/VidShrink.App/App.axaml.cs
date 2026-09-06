using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class App : Application
{
    private readonly string? _startupFile;
    private readonly ShellShrinkStartup? _shrink;
    private readonly ShrinkRequestQueue? _queue;

    public App() : this(null)
    {
    }

    public App(string? startupFile) => _startupFile = startupFile;

    internal App(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _shrink = startup;
        _queue = queue;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try { TempCleanup.CleanupStaleArtifacts(Path.GetTempPath()); } catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MacUpdate.Begin();
            desktop.Exit += (_, _) => MacUpdate.Finish();

            if (_shrink is not null) desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var window = StartupWindow();
            window.Icon = LoadAppIcon();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Baslangicta acilan pencere. Ayri durmasinin sebebi olculebilir olmasi: kabuk istegi
    /// varken burasi <see cref="MainWindow"/> dondururse ana pencere geri gelir ve
    /// <c>KabukIstegiTests.KabukIstegiAnaPencereyiAcmaz</c> kirmizi olur.
    /// </summary>
    internal Window StartupWindow()
        => _shrink is not null
            ? new ShrinkJobWindow(_shrink, _queue)
            : new MainWindow(_startupFile);

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://VidShrink.App/Assets/VidShrink.png"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
