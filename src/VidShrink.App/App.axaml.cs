using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class App : Application
{
    private readonly string? _startupFile;

    public App() : this(null)
    {
    }

    public App(string? startupFile) => _startupFile = startupFile;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try { TempCleanup.CleanupStaleArtifacts(Path.GetTempPath()); } catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(_startupFile) { Icon = LoadAppIcon() };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

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
