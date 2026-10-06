using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal readonly record struct StripTileBox(double X, double Width, long SourceTime, bool Ready);

internal sealed partial class EditorTimeline
{
    private const int ThumbnailBitmapLimit = 128;

    private readonly Dictionary<long, (byte[] Bytes, Bitmap? Picture)> _tiles = new();
    private long[] _keyframes = Array.Empty<long>();
    private double[]? _keyframeXs;
    private ThumbnailQueue? _thumbnails;
    private Action<long>? _thumbnailReady;
    private double _thumbnailAspect;
    private bool _stripPaused;

    internal double StripTop => TracksBottom + Metric("EditorTrackGap");

    internal double StripHeight => Metric("EditorThumbnailStripHeight");

    internal double StripBottom => StripTop + StripHeight;

    internal int StripTilesDrawn { get; private set; }

    /// <summary>Kaynagin anahtar kare anlari, sirali kaynak tick'i. Bos liste centik cizmez.</summary>
    internal IReadOnlyList<long> Keyframes
    {
        get => _keyframes;
        set
        {
            _keyframes = value is null ? Array.Empty<long>() : value.ToArray();
            _keyframeXs = null;
            _overlay.InvalidateVisual();
        }
    }

    internal ThumbnailQueue? Thumbnails
    {
        get => _thumbnails;
        set
        {
            if (_thumbnails is { } old && _thumbnailReady is { } handler) old.Ready -= handler;
            ForgetTiles();
            _thumbnails = value;
            _thumbnailReady = null;
            if (value is not null)
            {
                _thumbnailReady = _ => Dispatcher.UIThread.Post(_canvas.InvalidateVisual);
                value.Ready += _thumbnailReady;
            }

            RefreshStrip();
            _canvas.InvalidateVisual();
        }
    }

    /// <summary>Kaynak karesinin en/boy orani; bilinmiyorsa oynaticinin kucuk resim orani.</summary>
    internal double ThumbnailAspect
    {
        get => double.IsFinite(_thumbnailAspect) && _thumbnailAspect > 0
            ? _thumbnailAspect
            : Metric("PlaybackThumbnailWidth") / Math.Max(1, Metric("PlaybackThumbnailHeight"));
        set
        {
            _thumbnailAspect = value;
            RefreshStrip();
            _canvas.InvalidateVisual();
        }
    }

    internal double TileWidth => StripHeight * ThumbnailAspect;

    /// <summary>Gorunen araliktaki centiklerin x konumlari; birbirine degecek kadar sik olanlar seyreltilmis.</summary>
    internal IReadOnlyList<double> KeyframeXs()
    {
        if (_keyframeXs is { } cached) return cached;
        if (_model is not { } model || _ppt <= 0 || _keyframes.Length == 0) return _keyframeXs = Array.Empty<double>();
        var times = TimelineStrip.Thin(TimelineStrip.Keyframes(model, _keyframes, _viewStart, ViewEnd), _ppt, Metric("EditorKeyframeMinSpacing"));
        var xs = new double[times.Count];
        for (var i = 0; i < xs.Length; i++) xs[i] = TimeToX(times[i]);
        return _keyframeXs = xs;
    }

    internal IReadOnlyList<StripTileBox> StripTiles()
    {
        var tiles = VisibleTiles();
        var boxes = new StripTileBox[tiles.Count];
        var width = TileWidth;
        for (var i = 0; i < boxes.Length; i++)
            boxes[i] = new StripTileBox(TimeToX(tiles[i].Time), width, tiles[i].SourceTime, _thumbnails is { } queue && queue.TryGet(tiles[i].SourceTime, out _));
        return boxes;
    }

    internal void PauseStrip()
    {
        _stripPaused = true;
        _thumbnails?.Request(Array.Empty<long>());
    }

    internal void ResumeStrip()
    {
        _stripPaused = false;
        RefreshStrip();
    }

    internal void ForgetTiles()
    {
        foreach (var (_, image) in _tiles.Values) image?.Dispose();
        _tiles.Clear();
    }

    private IReadOnlyList<StripTile> VisibleTiles()
    {
        if (_model is not { } model || _ppt <= 0 || TrackWidth <= 0) return Array.Empty<StripTile>();
        return TimelineStrip.Tiles(model, _viewStart, ViewEnd, TimelineStrip.TileStep(_ppt, TileWidth));
    }

    private void RefreshStrip()
    {
        _keyframeXs = null;
        if (_thumbnails is not { } queue || _stripPaused) return;
        var tiles = VisibleTiles();
        var ticks = new long[tiles.Count];
        for (var i = 0; i < ticks.Length; i++) ticks[i] = tiles[i].SourceTime;
        queue.Request(ticks);
    }

    private void DrawStrip(DrawingContext context)
    {
        StripTilesDrawn = 0;
        if (_thumbnails is not { } queue || _model is not { } model) return;
        var tiles = VisibleTiles();
        if (tiles.Count == 0) return;
        var top = StripTop;
        var height = StripHeight;
        var width = TileWidth;
        var left = HeaderWidth;
        var right = Math.Min(Bounds.Width, TimeToX(model.Duration));
        using var clip = context.PushClip(new Rect(left, top, Math.Max(0, right - left), height));
        foreach (var tile in tiles)
        {
            if (Decode(queue, tile.SourceTime) is not { } image) continue;
            context.DrawImage(image, new Rect(TimeToX(tile.Time), top, width, height));
            StripTilesDrawn++;
        }
    }

    private Bitmap? Decode(ThumbnailQueue queue, long tick)
    {
        if (!queue.TryGet(tick, out var data)) return null;
        if (_tiles.TryGetValue(tick, out var held))
        {
            if (ReferenceEquals(held.Bytes, data)) return held.Picture;
            held.Picture?.Dispose();
            _tiles.Remove(tick);
        }

        if (_tiles.Count >= ThumbnailBitmapLimit) ForgetTiles();
        Bitmap? image = null;
        try
        {
            using var stream = new MemoryStream(data);
            image = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
        }

        _tiles[tick] = (data, image);
        return image;
    }
}
