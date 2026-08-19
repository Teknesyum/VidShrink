using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { TempCleanup.CleanupStaleArtifacts(Path.GetTempPath()); } catch { }
    }
}

