using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private int _exportRequests;
    private bool _canDelete;

    internal int ExportRequests => _exportRequests;

    internal EditorTool Tool => Timeline.Tool;

    internal double MonitorFps
        => double.IsFinite(Timeline.Fps) && Timeline.Fps > 0 ? Timeline.Fps : EditTime.TicksPerSecond / Timeline.FrameTicks;

    private void InitMonitor()
    {
        Preview.StripVisible = false;
        Wire(BtnMarkIn, EditorCommand.MarkIn);
        Wire(BtnMarkOut, EditorCommand.MarkOut);
        Wire(BtnGoIn, EditorCommand.GoToIn);
        Wire(BtnFrameBack, EditorCommand.FrameBack);
        Wire(BtnPlay, EditorCommand.PlayPause);
        Wire(BtnFrameForward, EditorCommand.FrameForward);
        Wire(BtnGoOut, EditorCommand.GoToOut);
        Wire(BtnSplit, EditorCommand.Split);
        Wire(BtnExtract, EditorCommand.Extract);
        Wire(BtnZoomFit, EditorCommand.ZoomFit);
        Wire(BtnToolSelection, EditorCommand.ToolSelection);
        Wire(BtnToolRazor, EditorCommand.ToolRazor);
        Wire(BtnToolRipple, EditorCommand.ToolRipple);
        Wire(BtnSnap, EditorCommand.ToggleSnap);
        TxtTimecode.KeyDown += OnTimecodeKey;
        TxtTimecode.LostFocus += (_, _) => RefreshMonitor();
        Preview.PlayingChanged += (_, _) => RefreshMonitor();
    }

    private void Wire(Button button, EditorCommand command) => button.Click += (_, _) =>
    {
        Run(command);
        RefreshMonitor();
    };

    private void UseTool(EditorTool tool)
    {
        Timeline.Tool = tool;
        RefreshTools();
    }

    private void RefreshTools()
    {
        BtnToolSelection.IsChecked = Timeline.Tool == EditorTool.Selection;
        BtnToolRazor.IsChecked = Timeline.Tool == EditorTool.Razor;
        BtnToolRipple.IsChecked = Timeline.Tool == EditorTool.Ripple;
        BtnSnap.IsChecked = Timeline.SnapEnabled;
    }

    private void RequestExport()
    {
        _exportRequests++;
        _ = ExportClickedAsync();
    }

    internal void RefreshMonitor()
    {
        var model = _model;
        var fps = MonitorFps;
        if (!TxtTimecode.IsFocused) ShowTimecode();
        TxtDuration.Text = model is null ? string.Empty : Stamp(model.Duration, fps);
        TxtTimecode.IsEnabled = model is not null;
        foreach (var button in new Button[] { BtnMarkIn, BtnMarkOut, BtnGoIn, BtnFrameBack, BtnPlay, BtnFrameForward, BtnGoOut, BtnExtract, BtnZoomFit })
            button.IsEnabled = model is not null;
        BtnGoIn.IsEnabled = Timeline.MarkIn is not null;
        BtnGoOut.IsEnabled = Timeline.MarkOut is not null;
        BtnExtract.IsEnabled = Timeline.MarkIn is { } a && Timeline.MarkOut is { } b && b > a;
        var playing = Preview.IsPlaying || _driver is { Playing: true };
        GlyphPlay.Data = this.TryFindResource(playing ? "IconPause" : "IconPlay", out var glyph) ? glyph as Geometry : null;
        ToolTip.SetTip(BtnPlay, Tip(playing ? "playback.control.pause" : "playback.control.play", EditorCommand.PlayPause));
        RefreshTools();
    }

    private void ShowTimecode() => TxtTimecode.Text = _model is null ? string.Empty : Stamp(Timeline.Playhead, MonitorFps);

    private static string Stamp(long time, double fps)
    {
        try
        {
            return EditTimecode.Format(time, fps);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }

    internal bool CommitTimecode(string? text)
    {
        if (_model is not { } model || text is null) return false;
        if (!EditTimecode.TryParse(text, MonitorFps, Timeline.Playhead, out var time))
        {
            ShowTimecode();
            return false;
        }

        SeekTo(Math.Clamp(time, 0, model.Duration));
        ShowTimecode();
        return true;
    }

    private void OnTimecodeKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = TxtTimecode.Text;
            LeaveTimecode();
            CommitTimecode(text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            LeaveTimecode();
            ShowTimecode();
            e.Handled = true;
        }
    }

    private void LeaveTimecode()
    {
        if (TxtTimecode.IsFocused) Timeline.Focus();
    }

    private void ShowMonitorTips()
    {
        ToolTip.SetTip(BtnMarkIn, Tip("editor.mark-in", EditorCommand.MarkIn));
        ToolTip.SetTip(BtnMarkOut, Tip("editor.mark-out", EditorCommand.MarkOut));
        ToolTip.SetTip(BtnGoIn, Tip("editor.key.go-in", EditorCommand.GoToIn));
        ToolTip.SetTip(BtnGoOut, Tip("editor.key.go-out", EditorCommand.GoToOut));
        ToolTip.SetTip(BtnFrameBack, Tip("main.player.menu.prevframe", EditorCommand.FrameBack));
        ToolTip.SetTip(BtnFrameForward, Tip("main.player.menu.nextframe", EditorCommand.FrameForward));
        ToolTip.SetTip(BtnExtract, Tip("editor.delete-range", EditorCommand.Extract));
        ToolTip.SetTip(BtnToolSelection, Tip("editor.tool.selection", EditorCommand.ToolSelection));
        ToolTip.SetTip(BtnToolRazor, Tip("editor.tool.razor", EditorCommand.ToolRazor));
        ToolTip.SetTip(BtnToolRipple, Tip("editor.tool.ripple", EditorCommand.ToolRipple));
        ToolTip.SetTip(BtnZoomIn, Tip("editor.zoom-in", EditorCommand.ZoomIn));
        ToolTip.SetTip(BtnZoomOut, Tip("editor.zoom-out", EditorCommand.ZoomOut));
        ToolTip.SetTip(BtnZoomFit, Tip("editor.key.zoom-fit", EditorCommand.ZoomFit));
        ToolTip.SetTip(BtnSnap, Tip("editor.snap", EditorCommand.ToggleSnap));
        ToolTip.SetTip(TxtTimecode, Strings.Get("editor.timecode"));
        ToolTip.SetTip(TxtDuration, Strings.Get("editor.duration"));
        RefreshMonitor();
    }
}
