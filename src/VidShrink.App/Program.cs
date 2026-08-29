using System;
using Avalonia;
using VidShrink.Core;

namespace VidShrink.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => Build(ShellIntegration.ResolveStartupPath(args)).StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => Build(null);

    private static AppBuilder Build(string? startupFile)
        => AppBuilder.Configure(() => new App(startupFile))
            .UsePlatformDetect()
            .LogToTrace();
}
