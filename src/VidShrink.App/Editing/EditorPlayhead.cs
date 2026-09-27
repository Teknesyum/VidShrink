using System;
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

    private void DrawGhost(DrawingContext context, double top)
    {
        if (_owner.Ghost is not { } ghost) return;
        var brush = _owner.GhostValid ? EditorTokens.Brush(this, "NeonBlueBorder") : EditorTokens.Brush(this, "TextDisabled");
        var pen = new Pen(brush, EditorTokens.Size(this, "EditorClipSelectedBorder"));
        var radius = EditorTokens.Size(this, "EditorClipRadius");
        context.DrawRectangle(null, pen, new RoundedRect(new Rect(ghost.X, top, ghost.Width, _owner.VideoHeight), radius));
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
