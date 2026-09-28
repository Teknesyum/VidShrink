using System;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal Func<string, Task>? OpenInEditor { get; set; }

    private async void OnToEditor(object? sender, RoutedEventArgs e)
    {
        if (Delivered() is { } path && OpenInEditor is { } gate) await gate(path);
    }
}
