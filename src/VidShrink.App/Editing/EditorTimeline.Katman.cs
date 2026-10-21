using System;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal sealed partial class EditorTimeline
{
    private const string LayerTrackName = "V2";

    private int _layerPress = -1;
    private bool _layerMoved;
    private long _layerStart;
    private bool _layerLane;

    internal event Action<int, long>? LayerMoved;

    internal int SelectedLayer { get; set; } = -1;

    internal double LayerTop => TextTop + TextHeight + Metric("EditorTrackGap");

    internal double LayerHeight => _model is { HasLayers: true } ? AudioHeight : 0;

    internal double LayerBottom => LayerTop + LayerHeight;

    internal bool LayerDragging => _layerPress >= 0;

    internal long? LayerGhost => _layerPress >= 0 && _layerMoved ? _layerStart : null;

    internal bool OnLayerRow(Point position) => LayerHeight > 0 && position.Y >= LayerTop && position.Y < LayerBottom;

    internal int LayerAt(double x)
    {
        if (_model is not { } model || _ppt <= 0) return -1;
        for (var i = model.Layers.Count - 1; i >= 0; i--)
        {
            var layer = model.Layers[i];
            var left = TimeToX(layer.Start);
            if (x >= left && x < left + layer.Length * _ppt) return i;
        }

        return -1;
    }

    private void RefreshLayerLane()
    {
        if (_model is null || SelectedLayer >= _model.Layers.Count) SelectedLayer = -1;
        var shown = LayerHeight > 0;
        if (shown == _layerLane) return;
        _layerLane = shown;
        InvalidateMeasure();
        InvalidateArrange();
    }

    private bool PressLayer(Point position)
    {
        if (!OnLayerRow(position) || _model is not { } model) return false;
        var hit = LayerAt(position.X);
        SelectedLayer = hit;
        _canvas.InvalidateVisual();
        if (hit < 0) return true;
        _press = position;
        _layerPress = hit;
        _layerMoved = false;
        _layerStart = model.Layers[hit].Start;
        return true;
    }

    internal void DragLayer(double x)
    {
        if (_layerPress < 0 || _model is not { } model || _layerPress >= model.Layers.Count || _ppt <= 0) return;
        if (!_layerMoved)
        {
            if (Math.Abs(x - _press.X) < Metric("EditorDragThresholdPx")) return;
            _layerMoved = true;
        }

        var layer = model.Layers[_layerPress];
        var delta = (long)Math.Round((x - _press.X) / _ppt);
        _layerStart = Math.Clamp(Snapped(layer.Start + delta, out var snapped), 0, Math.Max(0, model.Duration - 1));
        SnapLine = snapped;
        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void CommitLayer()
    {
        var index = _layerPress;
        var start = _layerStart;
        var moved = _layerMoved;
        var model = _model;
        EndLayer();
        if (!moved || model is null || index < 0 || index >= model.Layers.Count || start == model.Layers[index].Start) return;
        LayerMoved?.Invoke(index, start);
    }

    private void EndLayer()
    {
        if (_layerPress < 0) return;
        _layerPress = -1;
        _layerMoved = false;
        SnapLine = null;
        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void DrawLayerLane(DrawingContext context, double width)
    {
        if (LayerHeight <= 0) return;
        context.DrawRectangle(Paint("Surface"), null, new Rect(0, LayerTop, width, LayerHeight));
        context.DrawLine(new Pen(Paint("HeaderRestBorder"), Metric("EditorClipBorder")), new Point(0, LayerTop), new Point(width, LayerTop));
        DrawHeaderIcon(context, "IconPlayer", LayerTop, LayerHeight, LayerTrackName);
    }

    private void DrawLayers(DrawingContext context)
    {
        if (_model is not { HasLayers: true } model) return;
        var top = LayerTop;
        var height = LayerHeight;
        var pad = Metric("EditorClipPadding");
        for (var i = 0; i < model.Layers.Count; i++)
        {
            var layer = model.Layers[i];
            var start = i == _layerPress && _layerMoved ? _layerStart : layer.Start;
            var x = TimeToX(start);
            var w = Math.Max(0, layer.Length * _ppt);
            var box = new Rect(x, top, w, height);
            var active = i == SelectedLayer || i == _layerPress;
            var pen = new Pen(Paint(active ? "NeonPurple" : "NeonPurpleBorder"), Metric(active ? "EditorClipSelectedBorder" : "EditorClipBorder"));
            context.DrawRectangle(Paint("NeonBlueFill"), pen, new RoundedRect(box, Metric("EditorClipRadius")));
            if (w <= pad * 2) continue;
            var label = new FormattedText(System.IO.Path.GetFileName(layer.Path), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(EditorTokens.Font(this, "FontMono")), Metric("FontSizeSm"), Paint("TextBody"));
            var fit = HeaderLabelScale(label.Height, height);
            using (context.PushClip(box.Deflate(pad)))
            using (context.PushTransform(Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation(x + pad, top + (height - label.Height * fit) / 2)))
                context.DrawText(label, default);
        }
    }

    private void UpdateLayerCursor(Point position)
        => SetCursor(LayerAt(position.X) >= 0 ? StandardCursorType.SizeWestEast : StandardCursorType.Arrow);
}
