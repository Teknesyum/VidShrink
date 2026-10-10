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

    private readonly Dictionary<(int Source, long Tick), (byte[] Bytes, Bitmap? Picture)> _tiles = new();
    private readonly Dictionary<int, long[]> _extraKeyframes = new();
    private readonly Dictionary<int, ThumbnailQueue> _extraThumbnails = new();
    private long[] _keyframes = Array.Empty<long>();
    private double[]? _keyframeXs;
    private ThumbnailQueue? _thumbnails;
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
            if (_thumbnails is { } old) old.Ready -= ThumbnailReady;
            ForgetTiles();
            _thumbnails = value;
            if (value is not null) value.Ready += ThumbnailReady;

            RefreshStrip();
            _canvas.InvalidateVisual();
        }
    }

    /// <summary>Ek kaynagin anahtar kare anlari; sira 0 ilk kaynaktir (<see cref="Keyframes"/>).</summary>
    internal void SetKeyframes(int source, IReadOnlyList<long> ticks)
    {
        if (source == 0)
        {
            Keyframes = ticks;
            return;
        }

        _extraKeyframes[source] = ticks.ToArray();
        _keyframeXs = null;
        _overlay.InvalidateVisual();
    }

    /// <summary>Ek kaynagin kucuk resim kuyrugu; sira 0 ilk kaynaktir (<see cref="Thumbnails"/>).</summary>
    internal void SetThumbnails(int source, ThumbnailQueue queue)
    {
        if (source == 0)
        {
            Thumbnails = queue;
            return;
        }

        if (_extraThumbnails.TryGetValue(source, out var old)) old.Ready -= ThumbnailReady;
        _extraThumbnails[source] = queue;
        queue.Ready += ThumbnailReady;
        RefreshStrip();
        _canvas.InvalidateVisual();
    }

    private void ThumbnailReady(long tick) => Dispatcher.UIThread.Post(_canvas.InvalidateVisual);

    private ThumbnailQueue? QueueOf(int source) => source == 0 ? _thumbnails : _extraThumbnails.GetValueOrDefault(source);

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
        if (_model is not { } model || _ppt <= 0 || (_keyframes.Length == 0 && _extraKeyframes.Count == 0)) return _keyframeXs = Array.Empty<double>();
        IReadOnlyList<long> seen = TimelineStrip.Keyframes(model, _keyframes, _viewStart, ViewEnd);
        if (_extraKeyframes.Count > 0)
        {
            var all = new List<long>(seen);
            foreach (var (source, ticks) in _extraKeyframes) all.AddRange(TimelineStrip.Keyframes(model, ticks, _viewStart, ViewEnd, source));
            all.Sort();
            seen = all;
        }

        var times = TimelineStrip.Thin(seen, _ppt, Metric("EditorKeyframeMinSpacing"));
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
            boxes[i] = new StripTileBox(TimeToX(tiles[i].Time), width, tiles[i].SourceTime, QueueOf(tiles[i].Source) is { } queue && queue.TryGet(tiles[i].SourceTime, out _));
        return boxes;
    }

    internal void PauseStrip()
    {
        _stripPaused = true;
        _thumbnails?.Request(Array.Empty<long>());
        foreach (var queue in _extraThumbnails.Values) queue.Request(Array.Empty<long>());
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
        if ((_thumbnails is null && _extraThumbnails.Count == 0) || _stripPaused) return;
        var tiles = VisibleTiles();
        _thumbnails?.Request(TicksOf(tiles, 0));
        foreach (var (source, queue) in _extraThumbnails) queue.Request(TicksOf(tiles, source));
    }

    private static long[] TicksOf(IReadOnlyList<StripTile> tiles, int source)
    {
        var ticks = new List<long>(tiles.Count);
        foreach (var tile in tiles)
            if (tile.Source == source) ticks.Add(tile.SourceTime);
        return ticks.ToArray();
    }

    private void DrawStrip(DrawingContext context)
    {
        StripTilesDrawn = 0;
        if ((_thumbnails is null && _extraThumbnails.Count == 0) || _model is not { } model) return;
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
            if (QueueOf(tile.Source) is not { } queue || Decode(queue, tile.Source, tile.SourceTime) is not { } image) continue;
            var box = new Rect(TimeToX(tile.Time), top, width, height);
            if (tile.Source == 0) context.DrawImage(image, box);
            else context.DrawImage(image, Filling(image.Size, width / Math.Max(1, height)), box);
            StripTilesDrawn++;
        }
    }

    /// <summary>Ek kaynagin karesi ilk kaynagin oranindaki kutuyu doldurur: ortadan, orani bozmadan kirpilir.</summary>
    internal static Rect Filling(Size image, double aspect)
    {
        var width = Math.Min(image.Width, image.Height * aspect);
        var height = Math.Min(image.Height, image.Width / Math.Max(double.Epsilon, aspect));
        return new Rect((image.Width - width) / 2, (image.Height - height) / 2, width, height);
    }

    private Bitmap? Decode(ThumbnailQueue queue, int source, long time)
    {
        var tick = (source, time);
        if (!queue.TryGet(time, out var data)) return null;
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
