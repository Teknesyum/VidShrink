using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal readonly record struct GhostBox(double X, double Width);

internal sealed class EditorTimeline : Panel, ICustomHitTest
{
    private const double FallbackFps = 30;
    private static readonly double[] Mantissas = { 1, 2, 5 };

    private readonly List<EditorClip> _pool = new();
    private readonly List<EditorClip> _live = new();
    private readonly EditorPlayhead _overlay;
    private readonly EditorTrackCanvas _canvas;
    private EditTimeline? _model;
    private long[] _starts = Array.Empty<long>();
    private long _viewStart;
    private double _ppt;
    private double _width;
    private double _fps = double.NaN;
    private int _selected = -1;
    private bool _all;
    private readonly List<long> _markers = new();
    private long _playhead;

    private Point _press;
    private int _pressIndex = -1;
    private bool _dragging;
    private int _dropIndex = -1;

    public EditorTimeline()
    {
        _canvas = new EditorTrackCanvas(this);
        _overlay = new EditorPlayhead(this);
        Children.Add(_canvas);
        Children.Add(_overlay);
        Focusable = true;
    }

    internal event Action<long, bool>? Seek;

    internal event Action<int, int>? MoveRequested;

    internal event Action? SelectionChanged;

    internal EditTimeline? Model => _model;

    internal EditorPlayhead Overlay => _overlay;

    internal IReadOnlyList<EditorClip> Realized => _live;

    internal bool Scrubbing { get; private set; }

    internal long? MarkIn { get; private set; }

    internal long? MarkOut { get; private set; }

    internal IReadOnlyList<long> Markers => _markers;

    internal bool AllSelected => _all;

    internal long? SnapLine { get; private set; }

    internal GhostBox? Ghost { get; private set; }

    internal bool GhostValid { get; private set; }

    internal double HeaderWidth => Metric("EditorTrackHeaderWidth");

    internal double RulerHeight => Metric("EditorRulerHeight");

    internal double VideoTop => RulerHeight;

    internal double VideoHeight => Metric("EditorVideoTrackHeight");

    internal double AudioTop => VideoTop + VideoHeight + Metric("EditorTrackGap");

    internal double AudioHeight => Metric("EditorAudioTrackHeight");

    internal double TracksBottom => AudioTop + AudioHeight;

    internal double TrackWidth => Math.Max(0, _width - HeaderWidth);

    internal double Fps
    {
        get => _fps;
        set
        {
            _fps = value;
            PixelsPerTick = _ppt;
        }
    }

    internal double FrameTicks => EditTime.TicksPerSecond / (double.IsFinite(_fps) && _fps > 0 ? _fps : FallbackFps);

    internal double MinPixelsPerTick => _model is { Duration: > 0 } m && TrackWidth > 0 ? TrackWidth / m.Duration : 0;

    internal double MaxPixelsPerTick => Math.Max(MinPixelsPerTick, Metric("EditorZoomMax") / FrameTicks);

    internal double PixelsPerTick
    {
        get => _ppt;
        set
        {
            var clamped = Math.Clamp(double.IsFinite(value) ? value : 0, MinPixelsPerTick, MaxPixelsPerTick);
            _ppt = clamped;
            ViewStart = _viewStart;
        }
    }

    internal long ViewStart
    {
        get => _viewStart;
        set
        {
            var span = _ppt > 0 ? (long)Math.Floor(TrackWidth / _ppt) : 0;
            var max = Math.Max(0, (_model?.Duration ?? 0) - span);
            _viewStart = Math.Clamp(value, 0, max);
            Realize();
            InvalidateArrange();
            _canvas.InvalidateVisual();
            _overlay.InvalidateVisual();
        }
    }

    internal long ViewEnd => _ppt > 0 ? _viewStart + (long)Math.Ceiling(TrackWidth / _ppt) : _viewStart;

    internal double SnapThresholdTicks => _ppt > 0 ? Metric("EditorSnapThresholdPx") / _ppt : 0;

    internal int SelectedIndex
    {
        get => _selected;
        set
        {
            var count = _model?.Clips.Count ?? 0;
            var next = value >= 0 && value < count ? value : -1;
            if (next == _selected && !_all) return;
            _selected = next;
            _all = false;
            foreach (var clip in _live) clip.Classes.Set("selected", clip.Index == _selected);
            SelectionChanged?.Invoke();
        }
    }

    internal long Playhead
    {
        get => _playhead;
        set
        {
            var duration = _model?.Duration ?? 0;
            _playhead = Math.Clamp(value, 0, duration);
            _overlay.SetCurrentValue(Avalonia.Controls.Primitives.RangeBase.MaximumProperty, EditTime.ToSeconds(duration));
            _overlay.SetCurrentValue(Avalonia.Controls.Primitives.RangeBase.ValueProperty, EditTime.ToSeconds(_playhead));
            _overlay.InvalidateVisual();
        }
    }

    internal void Show(EditTimeline? model)
    {
        _model = model;
        _selected = -1;
        _all = false;
        _markers.Clear();
        MarkIn = null;
        MarkOut = null;
        _viewStart = 0;
        _ppt = 0;
        Refresh();
        _ppt = MinPixelsPerTick;
        Refresh();
    }

    internal void Refresh()
    {
        Rebuild();
        if (_selected >= (_model?.Clips.Count ?? 0)) _selected = -1;
        if (MarkIn is { } a && a > (_model?.Duration ?? 0)) MarkIn = null;
        if (MarkOut is { } b && b > (_model?.Duration ?? 0)) MarkOut = null;
        _markers.RemoveAll(m => m > (_model?.Duration ?? 0));
        if (_model is not { Clips.Count: > 0 }) _all = false;
        Playhead = _playhead;
        PixelsPerTick = _ppt;
        SelectionChanged?.Invoke();
    }

    internal void SetMarks(long? markIn, long? markOut)
    {
        MarkIn = markIn;
        MarkOut = markOut;
        _overlay.InvalidateVisual();
    }

    internal void SelectAll()
    {
        if (_model is not { Clips.Count: > 0 }) return;
        _selected = -1;
        _all = true;
        foreach (var clip in _live) clip.Classes.Set("selected", true);
        SelectionChanged?.Invoke();
    }

    internal bool AddMarker(long time)
    {
        if (_model is not { } model) return false;
        var at = Math.Clamp(time, 0, model.Duration);
        var index = _markers.BinarySearch(at);
        if (index >= 0) _markers.RemoveAt(index);
        else _markers.Insert(~index, at);
        _overlay.InvalidateVisual();
        return index < 0;
    }

    internal double TimeToX(long time) => HeaderWidth + (time - _viewStart) * _ppt;

    internal long XToTime(double x)
    {
        if (_ppt <= 0) return 0;
        var time = _viewStart + (long)Math.Round((x - HeaderWidth) / _ppt);
        return Math.Clamp(time, 0, _model?.Duration ?? 0);
    }

    internal long RoundToFrame(long time)
    {
        var frame = FrameTicks;
        var rounded = (long)Math.Round(Math.Round(time / frame) * frame);
        return Math.Clamp(rounded, 0, _model?.Duration ?? 0);
    }

    internal void ZoomAt(double factor, double x)
    {
        if (_model is null || _ppt <= 0 || factor <= 0) return;
        var anchor = _viewStart + (x - HeaderWidth) / _ppt;
        PixelsPerTick = _ppt * factor;
        ViewStart = (long)Math.Round(anchor - (x - HeaderWidth) / _ppt);
    }

    internal void ZoomCentered(bool zoomIn)
    {
        var step = Metric("EditorZoomStep");
        if (step <= 0) return;
        ZoomAt(zoomIn ? step : 1 / step, HeaderWidth + TrackWidth / 2);
    }

    internal long? SnapTarget(long time, bool playhead = true, int exclude = -1)
    {
        if (_model is not { } model || _ppt <= 0) return null;
        var limit = Metric("EditorSnapThresholdPx");
        long? best = null;
        var bestDistance = double.PositiveInfinity;

        void Consider(long candidate)
        {
            var distance = Math.Abs(candidate - time) * _ppt;
            if (distance > limit || distance >= bestDistance) return;
            bestDistance = distance;
            best = candidate;
        }

        Consider(0);
        Consider(model.Duration);
        for (var i = 0; i < _starts.Length; i++)
        {
            if (i == exclude) continue;
            Consider(_starts[i]);
            Consider(_starts[i] + model.Clips[i].TimelineLength);
        }

        if (playhead) Consider(_playhead);
        if (MarkIn is { } a) Consider(a);
        if (MarkOut is { } b) Consider(b);
        foreach (var marker in _markers) Consider(marker);
        return best;
    }

    internal int ClipAt(long time)
    {
        if (_model is not { } model || _starts.Length == 0) return -1;
        var index = Array.BinarySearch(_starts, time);
        if (index < 0) index = ~index - 1;
        index = Math.Clamp(index, 0, _starts.Length - 1);
        return time < _starts[index] + model.Clips[index].TimelineLength ? index : -1;
    }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width) && Math.Abs(availableSize.Width - _width) > double.Epsilon)
        {
            var fitted = _ppt <= MinPixelsPerTick;
            _width = availableSize.Width;
            PixelsPerTick = fitted ? MinPixelsPerTick : _ppt;
        }

        foreach (var child in Children) child.Measure(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, TracksBottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var header = HeaderWidth;
        var top = VideoTop;
        var height = VideoHeight;
        foreach (var clip in _live)
        {
            if (clip.Data is not { } model) continue;
            var start = TimeToX(_starts[clip.Index]);
            var end = start + model.TimelineLength * _ppt;
            var left = Math.Max(start, header);
            var right = Math.Min(end, finalSize.Width);
            clip.Arrange(new Rect(left, top, Math.Max(0, right - left), height));
        }

        _canvas.Arrange(new Rect(finalSize));
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }

    internal void DrawTracks(DrawingContext context)
    {
        var width = Bounds.Width;
        var header = HeaderWidth;
        if (width <= 0) return;

        var ruler = RulerHeight;
        context.DrawRectangle(Paint("AppBg"), null, new Rect(0, 0, width, ruler));
        context.DrawRectangle(Paint("Surface"), null, new Rect(0, VideoTop, width, VideoHeight));
        context.DrawRectangle(Paint("PanelSurface"), null, new Rect(0, AudioTop, width, AudioHeight));

        var line = new Pen(Paint("HeaderRestBorder"), Metric("EditorClipBorder"));
        context.DrawLine(line, new Point(header, 0), new Point(header, TracksBottom));
        context.DrawLine(line, new Point(0, VideoTop), new Point(width, VideoTop));
        context.DrawLine(line, new Point(0, AudioTop), new Point(width, AudioTop));

        DrawHeaderIcon(context, "IconPlayer", VideoTop, VideoHeight);
        DrawHeaderIcon(context, "IconVolume", AudioTop, AudioHeight);

        if (_model is null || _ppt <= 0) return;
        using var clip = context.PushClip(new Rect(header, 0, Math.Max(0, width - header), TracksBottom));
        DrawRuler(context);
        DrawAudioMirror(context);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_model is null) return;
        var point = e.GetCurrentPoint(this);
        var position = point.Position;
        if (position.X < HeaderWidth) return;
        Focus();

        if (point.Properties.IsRightButtonPressed)
        {
            var hit = position.Y >= VideoTop && position.Y < TracksBottom ? ClipAt(XToTime(position.X)) : -1;
            if (hit >= 0) SelectedIndex = hit;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        e.Handled = true;

        if (position.Y < RulerHeight)
        {
            Scrubbing = true;
            ScrubTo(position.X, false);
            return;
        }

        _press = position;
        _pressIndex = position.Y < TracksBottom ? ClipAt(XToTime(position.X)) : -1;
        _dragging = false;
        SelectedIndex = _pressIndex;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (Scrubbing)
        {
            ScrubTo(position.X, false);
            return;
        }

        if (_pressIndex < 0 || _model is null) return;
        if (!_dragging)
        {
            var moved = Math.Abs(position.X - _press.X);
            if (moved < Metric("EditorDragThresholdPx")) return;
            _dragging = true;
        }

        AutoScroll(position.X);
        DragTo(position.X);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var position = e.GetPosition(this);
        if (Scrubbing)
        {
            Scrubbing = false;
            ScrubTo(position.X, true);
        }
        else if (_dragging && _pressIndex >= 0 && _dropIndex >= 0 && _dropIndex != _pressIndex)
        {
            var from = _pressIndex;
            var to = _dropIndex;
            EndDrag();
            MoveRequested?.Invoke(from, to);
        }

        EndDrag();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Scrubbing = false;
        EndDrag();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_model is null || _ppt <= 0) return;
        var x = e.GetPosition(this).X;
        var notches = Math.Abs(e.Delta.Y) > Math.Abs(e.Delta.X) ? e.Delta.Y : e.Delta.X;
        if (notches == 0) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var step = Metric("EditorZoomStep");
            ZoomAt(notches > 0 ? step : 1 / step, x);
        }
        else
        {
            ViewStart = _viewStart - (long)Math.Round(notches * Metric("EditorAutoScrollMargin") / _ppt);
        }

        e.Handled = true;
    }

    internal void ScrubTo(double x, bool final)
    {
        var time = XToTime(x);
        var snapped = SnapTarget(time, playhead: false);
        SnapLine = final ? null : snapped;
        var target = snapped ?? time;
        if (final && snapped is null) target = RoundToFrame(target);
        Playhead = target;
        Seek?.Invoke(target, final);
    }

    internal void DragTo(double x)
    {
        if (_model is not { } model || _pressIndex < 0 || _pressIndex >= model.Clips.Count) return;
        var length = model.Clips[_pressIndex].TimelineLength;
        var offset = (x - _press.X) / _ppt;
        var ghostStart = _starts[_pressIndex] + (long)Math.Round(offset);
        var pointer = XToTime(x);
        var over = ClipAt(pointer);
        if (over < 0) over = pointer <= 0 ? 0 : model.Clips.Count - 1;

        var overStart = _starts[over];
        var overEnd = overStart + model.Clips[over].TimelineLength;
        var before = pointer - overStart < overEnd - pointer;
        var boundary = before ? overStart : overEnd;
        int to;
        if (before) to = over > _pressIndex ? over - 1 : over;
        else to = over + 1 > _pressIndex ? over : over + 1;
        _dropIndex = Math.Clamp(to, 0, model.Clips.Count - 1);

        GhostValid = _dropIndex != _pressIndex;
        SnapLine = GhostValid ? boundary : null;
        Ghost = new GhostBox(TimeToX(ghostStart), length * _ppt);
        _overlay.InvalidateVisual();
    }

    private void EndDrag()
    {
        _pressIndex = -1;
        _dragging = false;
        _dropIndex = -1;
        Ghost = null;
        SnapLine = null;
        _overlay.InvalidateVisual();
    }

    private void AutoScroll(double x)
    {
        var margin = Metric("EditorAutoScrollMargin");
        var right = Bounds.Width - margin;
        var left = HeaderWidth + margin;
        if (x > right) ViewStart = _viewStart + (long)Math.Round((x - right) / _ppt);
        else if (x < left) ViewStart = _viewStart - (long)Math.Round((left - x) / _ppt);
    }

    private void Rebuild()
    {
        if (_model is not { } model)
        {
            _starts = Array.Empty<long>();
            return;
        }

        var starts = new long[model.Clips.Count];
        long at = 0;
        for (var i = 0; i < starts.Length; i++)
        {
            starts[i] = at;
            at += model.Clips[i].TimelineLength;
        }

        _starts = starts;
    }

    private void Realize()
    {
        var first = 0;
        var last = -1;
        if (_model is { } model && _starts.Length > 0 && _ppt > 0 && TrackWidth > 0)
        {
            var end = ViewEnd;
            first = ClipAt(_viewStart);
            if (first < 0) first = 0;
            last = ClipAt(Math.Max(_viewStart, end - 1));
            if (last < 0) last = _starts.Length - 1;
            first = Math.Max(0, first - 1);
            last = Math.Min(_starts.Length - 1, last + 1);
        }

        var needed = last - first + 1;
        while (_live.Count > needed)
        {
            var extra = _live[^1];
            _live.RemoveAt(_live.Count - 1);
            Children.Remove(extra);
            _pool.Add(extra);
        }

        while (_live.Count < needed)
        {
            EditorClip clip;
            if (_pool.Count > 0)
            {
                clip = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                clip = new EditorClip();
                clip.ApplyThemes(EditorTokens.Theme(this, "EditorClip"), EditorTokens.Theme(this, "PlaybackBadge"), EditorTokens.Theme(this, "EditorBadgeText"));
            }

            _live.Add(clip);
            Children.Insert(Children.Count - 1, clip);
        }

        if (_model is not { } shown) return;
        var minWidth = Metric("EditorClipMinWidth");
        for (var i = 0; i < _live.Count; i++)
        {
            var index = first + i;
            var clip = shown.Clips[index];
            _live[i].Show(index, clip, _all || index == _selected, clip.TimelineLength * _ppt < minWidth);
        }
    }

    private void DrawRuler(DrawingContext context)
    {
        var perSecond = _ppt * EditTime.TicksPerSecond;
        var step = RulerStep(perSecond, Metric("EditorRulerLabelMinSpacing"));
        var minor = step / Mantissas[^1];
        var ruler = RulerHeight;
        var major = new Pen(Paint("NeonBlueBorderStrong"), Metric("EditorClipBorder"));
        var small = new Pen(Paint("TextDisabled"), Metric("EditorClipBorder"));
        var majorTick = Metric("EditorRulerMajorTick");
        var minorTick = Metric("EditorRulerMinorTick");
        var pad = Metric("EditorClipPadding");
        var typeface = new Typeface(EditorTokens.Font(this, "FontMono"));
        var size = Metric("FontSizeSm");
        var text = Paint("TextBody");
        var culture = Strings.Culture;

        var startSeconds = EditTime.ToSeconds(_viewStart);
        var endSeconds = EditTime.ToSeconds(ViewEnd);
        var index = (long)Math.Floor(startSeconds / minor);
        var perMajor = (long)Math.Round(step / minor);
        for (; index * minor <= endSeconds; index++)
        {
            var seconds = index * minor;
            var x = TimeToX(EditTime.FromSeconds(seconds));
            if (index % perMajor == 0)
            {
                context.DrawLine(major, new Point(x, ruler - majorTick), new Point(x, ruler));
                var label = new FormattedText(Saat.Kesit(TimeSpan.FromSeconds(seconds), culture), culture, FlowDirection.LeftToRight, typeface, size, text);
                context.DrawText(label, new Point(x + pad, 0));
            }
            else
            {
                context.DrawLine(small, new Point(x, ruler - minorTick), new Point(x, ruler));
            }
        }
    }

    internal static double RulerStep(double pixelsPerSecond, double minSpacing)
    {
        if (pixelsPerSecond <= 0) return 1;
        var decade = Math.Pow(10, Math.Floor(Math.Log10(minSpacing / pixelsPerSecond)));
        for (var round = 0; round < Mantissas.Length + 1; round++)
        {
            foreach (var mantissa in Mantissas)
            {
                var step = mantissa * decade;
                if (step * pixelsPerSecond >= minSpacing) return step;
            }

            decade *= 10;
        }

        return decade;
    }

    private void DrawAudioMirror(DrawingContext context)
    {
        if (_model is not { } model) return;
        var fill = Paint("NeonBlueFill");
        var pen = new Pen(Paint("NeonBlueBorder"), Metric("EditorClipBorder"));
        var radius = Metric("EditorClipRadius");
        foreach (var clip in _live)
        {
            if (clip.Data is null) continue;
            var x = TimeToX(_starts[clip.Index]);
            var w = model.Clips[clip.Index].TimelineLength * _ppt;
            context.DrawRectangle(fill, pen, new RoundedRect(new Rect(x, AudioTop, Math.Max(0, w), AudioHeight), radius));
        }
    }

    private void DrawHeaderIcon(DrawingContext context, string key, double top, double height)
    {
        if (EditorTokens.Find(this, key) is not Geometry icon) return;
        var box = icon.Bounds;
        var pad = Metric("EditorClipPadding");
        var side = Math.Min(height - pad * 2, Metric("TargetMinSize"));
        if (side <= 0 || box.Width <= 0 || box.Height <= 0) return;
        var scale = side / Math.Max(box.Width, box.Height);
        var x = pad - box.X * scale;
        var y = top + (height - box.Height * scale) / 2 - box.Y * scale;
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y));
        context.DrawGeometry(Paint("TextBody"), null, icon);
    }

    private double Metric(string key) => EditorTokens.Size(this, key);

    private IBrush? Paint(string key) => EditorTokens.Brush(this, key);
}

internal sealed class EditorTrackCanvas : Control
{
    private readonly EditorTimeline _owner;

    internal EditorTrackCanvas(EditorTimeline owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _owner.DrawTracks(context);
    }
}
