using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private CancellationTokenSource? _keyframes;
    private ThumbnailQueue? _thumbnails;

    /// <summary>Test konaginda varsayilan ffprobe ve ffmpeg okumalarini kapatir; verilen dikis yine kosar.</summary>
    internal static bool StripDisabled { get; set; }

    internal Func<string, CancellationToken, Task<IReadOnlyList<double>>>? KeyframeReader { get; set; }

    internal IThumbnailSource? ThumbnailSource { get; set; }

    internal Task KeyframeLoad { get; private set; } = Task.CompletedTask;

    internal ThumbnailQueue? ThumbnailQueue => _thumbnails;

    private void LoadStrip(string path)
    {
        _keyframes?.Cancel();
        _keyframes?.Dispose();
        _keyframes = new CancellationTokenSource();
        Timeline.Keyframes = Array.Empty<long>();
        KeyframeLoad = LoadKeyframesAsync(path, _keyframes.Token);
        OpenThumbnails(path);
    }

    private async Task LoadKeyframesAsync(string path, CancellationToken ct)
    {
        var reader = KeyframeReader;
        if (reader is null)
        {
            if (StripDisabled || !File.Exists(path) || !ToolLocator.IsAvailable(out _)) return;
            reader = static async (source, token) =>
                EditExport.ParseKeyframes(await EditExportRunner.ProbeKeyframesAsync(source, token).ConfigureAwait(false)).Keyframes;
        }

        long[] ticks;
        try
        {
            ticks = await Task.Run(async () => TimelineStrip.SourceTicks(await reader(path, ct).ConfigureAwait(false)), ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException or UnauthorizedAccessException or FormatException or System.ComponentModel.Win32Exception)
        {
            return;
        }

        if (ct.IsCancellationRequested || !CurrentMedia.SamePath(path, _source)) return;
        Timeline.Keyframes = ticks;
    }

    private void OpenThumbnails(string path)
    {
        var source = ThumbnailSource;
        if (source is null && (StripDisabled || !File.Exists(path) || !ToolLocator.IsAvailable(out _)))
        {
            _thumbnails?.Open(null, 0);
            Timeline.ForgetTiles();
            return;
        }

        if (_thumbnails is null)
        {
            _thumbnails = new ThumbnailQueue(source ?? new FfmpegThumbnailSource(() => ToolLocator.Ffmpeg));
            Timeline.Thumbnails = _thumbnails;
        }

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        Timeline.ForgetTiles();
        _thumbnails.Open(null, 0);
        _thumbnails.Open(path, Math.Max(1, (int)Math.Ceiling(Timeline.StripHeight * scale)));
        Timeline.ThumbnailAspect = KnownInfo?.Invoke(path) is { Width: > 0, Height: > 0 } info ? (double)info.Width / info.Height : 0;
    }
}
