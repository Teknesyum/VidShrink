using System;
using System.Collections.Generic;
using System.Linq;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private IReadOnlyList<EditClip> _clipboard = Array.Empty<EditClip>();

    /// <summary>Uygulama içi pano; sistem panosuna yazılmaz, kaynak değişince boşalır.</summary>
    internal IReadOnlyList<EditClip> Clipboard => _clipboard;

    private void InitClipboard()
    {
        MnuCopy.Click += (_, _) => CopySelected();
        MnuCut.Click += (_, _) => CutSelected();
        MnuPaste.Click += (_, _) => PasteClipboard();
        MnuDuplicate.Click += (_, _) => DuplicateSelected();
        MnuGroup.Click += (_, _) => GroupSelected();
        MnuUngroup.Click += (_, _) => UngroupSelected();
        TimelineMenu.Opening += (_, _) =>
        {
            var any = ClipSelection().Count > 0;
            MnuCopy.IsEnabled = any;
            MnuCut.IsEnabled = any;
            MnuDuplicate.IsEnabled = any;
            MnuGroup.IsEnabled = any;
            MnuUngroup.IsEnabled = any;
            MnuPaste.IsEnabled = _model is not null && _clipboard.Count > 0;
        };
    }

    private void ForgetClipboard() => _clipboard = Array.Empty<EditClip>();

    private IReadOnlyList<int> ClipSelection()
    {
        if (_model is not { } model || Timeline.SelectedText >= 0) return Array.Empty<int>();
        var selected = Timeline.SelectedIndices;
        return selected.Count > 0 && selected.All(i => i >= 0 && i < model.Clips.Count) ? selected : Array.Empty<int>();
    }

    internal bool CopySelected()
    {
        if (_model is not { } model || ClipSelection() is not { Count: > 0 } selected) return false;
        _clipboard = model.Copy(selected);
        return true;
    }

    internal bool CutSelected()
    {
        if (ClipSelection() is not { Count: > 0 } selected) return false;
        var all = Timeline.AllSelected;
        return Apply(model =>
        {
            if (!all && model.WithGroups(selected).Count >= model.Clips.Count) return false;
            var cut = model.Cut(selected);
            if (cut.Count == 0) return false;
            _clipboard = cut;
            return true;
        }, Math.Max(0, selected.Min() - 1));
    }

    internal bool PasteClipboard()
    {
        if (_model is null || _clipboard.Count == 0) return false;
        var clips = _clipboard;
        var first = -1;
        if (!Apply(model => (first = model.Paste(clips, Math.Min(Timeline.Playhead, model.Duration))) >= 0, -1)) return false;
        Timeline.SelectedIndex = first;
        return true;
    }

    internal bool DuplicateSelected()
    {
        if (_model is not { } model || ClipSelection() is not { Count: > 0 } selected) return false;
        _clipboard = model.Copy(selected);
        return PasteClipboard();
    }

    internal bool GroupSelected()
    {
        if (ClipSelection() is not { Count: > 0 } selected) return false;
        return Apply(model => model.Group(selected), Timeline.SelectedIndex);
    }

    internal bool UngroupSelected()
    {
        if (ClipSelection() is not { Count: > 0 } selected) return false;
        return Apply(model => model.Ungroup(selected), Timeline.SelectedIndex);
    }
}
