using Avalonia;

namespace VidShrink.Tests;

/// <summary>
/// Avalonia çalışma zamanını süreç başına bir kez kurar. Gömülü yazı tipini okumak,
/// XAML kaynak sözlüğünü yüklemek ve fırça çözmek platformun kurulu olmasını ister;
/// iki ayrı ölçüm birbirinden habersiz kurmaya kalkarsa yarışa girer.
/// </summary>
internal static class AppHost
{
    private static readonly object Gate = new();
    private static bool _ready;

    internal static void Ensure()
    {
        lock (Gate)
        {
            if (_ready) return;
            if (Application.Current is null)
                AppBuilder.Configure<Application>().UseSkia().UseWin32().SetupWithoutStarting();
            _ready = true;
        }
    }
}
