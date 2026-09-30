using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Editing;

internal sealed class EditorPlayhead : RangeBase
{
    private readonly EditorTimeline _owner;

    internal EditorPlayhead(EditorTimeline owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Minimum = 0;
    }

    internal string TimeText => Saat.Kesit(TimeSpan.FromSeconds(Math.Max(0, Value)), Strings.Culture);

    protected override AutomationPeer OnCreateAutomationPeer() => new PlayheadAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_owner.Model is null || Bounds.Width <= _owner.HeaderWidth) return;

        var left = _owner.HeaderWidth;
        var top = _owner.VideoTop;
        var bottom = _owner.TracksBottom;
        using var clip = context.PushClip(new Rect(left, 0, Bounds.Width - left, Bounds.Height));

        DrawRange(context, top, bottom);
        DrawMarkers(context, bottom);
        DrawGhost(context, top);
        DrawSnap(context, bottom);
        DrawHead(context, bottom);
    }

    private void DrawRange(DrawingContext context, double top, double bottom)
    {
        if (_owner.MarkIn is not { } mark) return;
        var pen = new Pen(EditorTokens.Brush(this, "NeonPurpleBorder"), EditorTokens.Size(this, "EditorClipSelectedBorder"));
        var x0 = _owner.TimeToX(mark);
        if (_owner.MarkOut is { } end && end > mark)
        {
            context.DrawRectangle(null, pen, new Rect(x0, top, Math.Max(0, _owner.TimeToX(end) - x0), bottom - top));
            return;
        }

        context.DrawLine(pen, new Point(x0, top), new Point(x0, bottom));
    }

    private void DrawMarkers(DrawingContext context, double bottom)
    {
        if (_owner.Markers.Count == 0) return;
        var brush = EditorTokens.Brush(this, "NeonBlue");
        var pen = new Pen(brush, EditorTokens.Size(this, "EditorClipBorder"));
        var handle = EditorTokens.Size(this, "EditorPlayheadHandle") / 2;
        var ruler = _owner.RulerHeight;
        foreach (var marker in _owner.Markers)
        {
            var x = _owner.TimeToX(marker);
            context.DrawLine(pen, new Point(x, ruler), new Point(x, bottom));
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(x - handle / 2, ruler - handle), true);
                g.LineTo(new Point(x + handle / 2, ruler - handle));
                g.LineTo(new Point(x, ruler));
                g.EndFigure(true);
            }

            context.DrawGeometry(brush, null, geometry);
        }
    }

    private void DrawGhost(DrawingContext context, double top)
    {
        if (_owner.Ghost is not { } ghost) return;
        var brush = _owner.GhostValid ? EditorTokens.Brush(this, "NeonBlueBorder") : EditorTokens.Brush(this, "TextDisabled");
        var pen = new Pen(brush, EditorTokens.Size(this, "EditorClipSelectedBorder"));
        var radius = EditorTokens.Size(this, "EditorClipRadius");
        context.DrawRectangle(null, pen, new RoundedRect(new Rect(ghost.X, top, ghost.Width, _owner.VideoHeight), radius));
        if (_owner.TrimLabel is { } text) DrawTrimLabel(context, text, ghost, top, radius);
    }

    private void DrawTrimLabel(DrawingContext context, string text, GhostBox ghost, double top, double radius)
    {
        var pad = EditorTokens.Size(this, "EditorClipPadding");
        var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(EditorTokens.Font(this, "FontMono")), EditorTokens.Size(this, "FontSizeSm"), EditorTokens.Brush(this, "TextBody"));
        var width = label.Width + pad * 2;
        var height = label.Height + pad * 2;
        var x = Math.Clamp(ghost.X + pad, _owner.HeaderWidth, Math.Max(_owner.HeaderWidth, Bounds.Width - width));
        var box = new Rect(x, top + pad, width, height);
        context.DrawRectangle(EditorTokens.Brush(this, "AppBg"), new Pen(EditorTokens.Brush(this, "NeonBlueBorder"), EditorTokens.Size(this, "EditorClipBorder")), new RoundedRect(box, radius));
        context.DrawText(label, new Point(x + pad, top + pad * 2));
    }

    private void DrawSnap(DrawingContext context, double bottom)
    {
        if (_owner.SnapLine is not { } snap) return;
        var width = EditorTokens.Size(this, "EditorPlayheadWidth");
        var x = _owner.TimeToX(snap) - width / 2;
        context.DrawRectangle(EditorTokens.Brush(this, "NeonPurple"), null, new RoundedRect(new Rect(x, 0, width, bottom)), EditorTokens.Shadow(this, "GlowPurple"));
    }

    private void DrawHead(DrawingContext context, double bottom)
    {
        var width = EditorTokens.Size(this, "EditorPlayheadWidth");
        var handle = EditorTokens.Size(this, "EditorPlayheadHandle");
        var brush = EditorTokens.Brush(this, "NeonPink");
        var center = _owner.TimeToX(_owner.Playhead);
        context.DrawRectangle(brush, null, new RoundedRect(new Rect(center - width / 2, 0, width, bottom)), EditorTokens.Shadow(this, "GlowPink"));

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(center - handle / 2, 0), true);
            g.LineTo(new Point(center + handle / 2, 0));
            g.LineTo(new Point(center, handle / 2));
            g.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }
}

internal sealed class PlayheadAutomationPeer : RangeBaseAutomationPeer
{
    public PlayheadAutomationPeer(EditorPlayhead owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string GetClassNameCore() => nameof(EditorPlayhead);

    protected override string? GetNameCore()
        => Strings.Get("editor.playhead") + " " + ((EditorPlayhead)Owner).TimeText;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
