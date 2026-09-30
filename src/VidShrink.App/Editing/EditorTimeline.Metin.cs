using System;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal sealed partial class EditorTimeline
{
    private const string TextTrackName = "T1";

    private int _textSelected = -1;
    private int _textPress = -1;
    private TextGrip _textGrip;
    private bool _textMoved;
    private long _textStart;
    private long _textEnd;

    internal event Action? TextEdited;

    internal event Action? TextSelectionChanged;

    internal double TextTop => RulerHeight;

    internal double TextHeight => Metric("EditorTextTrackHeight");

    internal bool TextDragging => _textPress >= 0;

    internal (long Start, long End)? TextGhost => _textPress >= 0 && _textMoved ? (_textStart, _textEnd) : null;

    internal int SelectedText
    {
        get => _textSelected;
        set
        {
            var count = _model?.Texts.Count ?? 0;
            var next = value >= 0 && value < count ? value : -1;
            if (next == _textSelected) return;
            _textSelected = next;
            if (next >= 0) SelectedIndex = -1;
            _canvas.InvalidateVisual();
            TextSelectionChanged?.Invoke();
        }
    }

    internal bool OnTextRow(Point position) => position.Y >= TextTop && position.Y < TextTop + TextHeight;

    internal int TextAt(double x)
    {
        if (_model is not { } model || _ppt <= 0) return -1;
        for (var i = model.Texts.Count - 1; i >= 0; i--)
        {
            var text = model.Texts[i];
            var left = TimeToX(text.Start);
            var right = left + text.Length * _ppt;
            if (x >= left && x < right) return i;
        }

        return -1;
    }

    internal bool TextEdgeAt(Point position, out int index, out bool head)
    {
        index = -1;
        head = false;
        if (_model is not { } model || _ppt <= 0 || position.X < HeaderWidth || !OnTextRow(position)) return false;
        var grip = Metric("EditorEdgeGripWidth");
        var best = double.PositiveInfinity;
        for (var i = model.Texts.Count - 1; i >= 0; i--)
        {
            var text = model.Texts[i];
            var left = TimeToX(text.Start);
            var right = left + text.Length * _ppt;
            var toStart = Math.Abs(position.X - left);
            var toEnd = Math.Abs(right - position.X);
            var near = Math.Min(toStart, toEnd);
            if (near > grip || near >= best) continue;
            best = near;
            index = i;
            head = toStart <= toEnd;
        }

        return index >= 0;
    }

    internal void RefreshTexts()
    {
        var count = _model?.Texts.Count ?? 0;
        if (_textSelected >= count)
        {
            _textSelected = -1;
            TextSelectionChanged?.Invoke();
        }

        _canvas.InvalidateVisual();
    }

    private void ResetTexts()
    {
        _textSelected = -1;
        EndText();
    }

    private bool PressText(Point position, bool plain)
    {
        if (!OnTextRow(position) || _model is null) return false;
        _press = position;
        if (plain && TextEdgeAt(position, out var edge, out var head))
        {
            BeginText(edge, head ? TextGrip.Head : TextGrip.Tail);
            SetCursor(StandardCursorType.SizeWestEast);
            return true;
        }

        var hit = TextAt(position.X);
        SelectedText = hit;
        if (hit < 0) return true;
        BeginText(hit, TextGrip.Body);
        return true;
    }

    private void BeginText(int index, TextGrip grip)
    {
        var text = _model!.Texts[index];
        SelectedText = index;
        _textPress = index;
        _textGrip = grip;
        _textMoved = false;
        _textStart = text.Start;
        _textEnd = text.End;
    }

    internal void DragText(double x)
    {
        if (_model is not { } model || _textPress < 0 || _textPress >= model.Texts.Count || _ppt <= 0) return;
        if (!_textMoved)
        {
            if (Math.Abs(x - _press.X) < Metric("EditorDragThresholdPx")) return;
            _textMoved = true;
        }

        var text = model.Texts[_textPress];
        var delta = (long)Math.Round((x - _press.X) / _ppt);
        var frame = (long)Math.Max(1, Math.Round(FrameTicks));
        switch (_textGrip)
        {
            case TextGrip.Body:
            {
                var start = Math.Max(0, Snapped(text.Start + delta, out var snapped));
                if (snapped is null)
                {
                    var end = Snapped(text.End + delta, out var tail);
                    if (tail is not null) start = Math.Max(0, end - text.Length);
                    SnapLine = tail;
                }
                else
                {
                    SnapLine = snapped;
                }

                _textStart = start;
                _textEnd = start + text.Length;
                break;
            }
            case TextGrip.Head:
                _textStart = Math.Clamp(Snapped(text.Start + delta, out var head), 0, text.End - frame);
                _textEnd = text.End;
                SnapLine = head;
                break;
            default:
                _textStart = text.Start;
                _textEnd = Math.Max(text.Start + frame, Snapped(text.End + delta, out var end2));
                SnapLine = end2;
                break;
        }

        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private long Snapped(long target, out long? snapped)
    {
        snapped = SnapTarget(target);
        if (snapped is { } at) return at;
        var frame = FrameTicks;
        return (long)Math.Round(Math.Round(target / frame) * frame);
    }

    private void CommitText()
    {
        var model = _model;
        var index = _textPress;
        var grip = _textGrip;
        var moved = _textMoved;
        var (start, end) = (_textStart, _textEnd);
        EndText();
        if (model is null || !moved || index < 0 || index >= model.Texts.Count) return;
        var text = model.Texts[index];
        var changed = grip switch
        {
            TextGrip.Body => start != text.Start && model.MoveText(index, start),
            TextGrip.Head => start != text.Start && model.TrimText(index, true, start),
            _ => end != text.End && model.TrimText(index, false, end),
        };
        if (!changed) return;
        _canvas.InvalidateVisual();
        TextEdited?.Invoke();
    }

    private void EndText()
    {
        if (_textPress < 0) return;
        _textPress = -1;
        _textMoved = false;
        SnapLine = null;
        SetCursor(StandardCursorType.Arrow);
        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void DrawTexts(DrawingContext context)
    {
        if (_model is not { } model || model.Texts.Count == 0) return;
        var top = TextTop;
        var height = TextHeight;
        var fill = Paint("NeonPinkFill");
        var pen = new Pen(Paint("NeonPurpleBorder"), Metric("EditorClipBorder"));
        var chosen = new Pen(Paint("NeonPink"), Metric("EditorClipSelectedBorder"));
        var radius = Metric("EditorClipRadius");
        var pad = Metric("EditorClipPadding");
        var typeface = new Typeface(EditorTokens.Font(this, "FontMono"));
        var size = Metric("FontSizeSm");
        var ink = Paint("TextBody");
        for (var i = 0; i < model.Texts.Count; i++)
        {
            var text = model.Texts[i];
            var (start, end) = i == _textPress && _textMoved ? (_textStart, _textEnd) : (text.Start, text.End);
            var x = TimeToX(start);
            var w = Math.Max(0, (end - start) * _ppt);
            var box = new Rect(x, top, w, height);
            context.DrawRectangle(fill, i == _textSelected ? chosen : pen, new RoundedRect(box, radius));
            if (w <= pad * 2) continue;
            var line = text.Text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')[0];
            var label = new FormattedText(line, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, ink);
            var fit = HeaderLabelScale(label.Height, height);
            using (context.PushClip(box.Deflate(pad)))
            using (context.PushTransform(Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation(x + pad, top + (height - label.Height * fit) / 2)))
                context.DrawText(label, default);
        }
    }

    private enum TextGrip
    {
        Body,
        Head,
        Tail
    }
}
