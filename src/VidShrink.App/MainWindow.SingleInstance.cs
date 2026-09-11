using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Integration;

namespace VidShrink.App;

public partial class MainWindow
{
    internal const string InstanceTraceVariable = "VIDSHRINK_INSTANCE_TRACE";

    private WindowState _restoreState = WindowState.Maximized;

    internal void AcceptForwardedFiles(ForwardedFiles files)
    {
        PropertyChanged += (_, change) =>
        {
            if (change.Property == WindowStateProperty && WindowState != WindowState.Minimized)
                _restoreState = WindowState;
        };
        Opened += (_, _) => files.Attach(AcceptForwarded);
    }

    internal bool AcceptForwarded(string? path)
    {
        if (_cts is not null) return false;

        if (WindowState == WindowState.Minimized) WindowState = _restoreState;
        Activate();

        if (path is not null) _ = LoadForwardedAsync(path);
        return true;
    }

    internal Task? ForwardedLoad { get; private set; }

    private Task LoadForwardedAsync(string path)
    {
        ForwardedLoad = LoadAndTraceAsync(path);
        return ForwardedLoad;
    }

    private async Task LoadAndTraceAsync(string path)
    {
        try
        {
            await LoadStartupFileAsync(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.startup")}: {ex.Message}");
        }

        WriteInstanceTrace(path);
    }

    private void WriteInstanceTrace(string path)
    {
        var trace = Environment.GetEnvironmentVariable(InstanceTraceVariable);
        if (string.IsNullOrWhiteSpace(trace)) return;

        try
        {
            File.AppendAllText(trace, $"{path}\t{TxtFileName.Text}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
