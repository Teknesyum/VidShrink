using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private DispatcherTimer? _previewTimer;
    private string? _previewPath;
    private Bitmap? _previewImage;

    internal Func<string> PreviewLocation { get; set; } = () => Path.Combine(
        Path.GetTempPath(), "VidShrink", "recorder-preview-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".jpg");

    internal bool PreviewVisible => ImgPreview.IsVisible;

    internal Bitmap? PreviewImage => _previewImage;

    internal bool PreviewTicking => _previewTimer?.IsEnabled ?? false;

    private RecorderRequest WithPreview(RecorderRequest request)
        => ChkLivePreview.IsChecked == true && request.MaxMegabytes is null
            ? request with { PreviewPath = PreviewLocation() }
            : request with { PreviewPath = null };

    internal void PreparePreview(string? path)
    {
        ClearPreview();
        _previewPath = path;
        if (path is null) return;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        TryDeletePreview(path);
    }

    private void SyncPreview(bool running)
    {
        if (_session is null)
        {
            ClearPreview();
            return;
        }

        if (_previewPath is null) return;
        _previewTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(RecorderArguments.PreviewFps), DispatcherPriority.Background, (_, _) => RefreshPreview());
        if (running) _previewTimer.Start();
        else _previewTimer.Stop();
    }

    internal bool RefreshPreview()
    {
        if (_previewPath is null || !File.Exists(_previewPath)) return false;

        Bitmap next;
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(_previewPath));
            next = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            return false;
        }

        var old = _previewImage;
        _previewImage = next;
        ImgPreview.Source = next;
        ImgPreview.IsVisible = true;
        old?.Dispose();
        return true;
    }

    private void ClearPreview()
    {
        _previewTimer?.Stop();
        ImgPreview.IsVisible = false;
        ImgPreview.Source = null;
        _previewImage?.Dispose();
        _previewImage = null;
        if (_previewPath is { } path) TryDeletePreview(path);
        _previewPath = null;
    }

    private static void TryDeletePreview(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
