using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    internal Func<string, MediaInfo?>? KnownInfo { get; set; }

    internal static double SourceFps(double? known, double engine)
    {
        if (known is { } fps && double.IsFinite(fps) && fps > 0) return fps;
        return double.IsFinite(engine) && engine > 0 ? engine : double.NaN;
    }

    internal void UseDriver(EdlPreviewDriver driver)
    {
        if (!ReferenceEquals(_driver, driver)) _driver?.Dispose();
        _driver = driver;
    }

    internal bool HandleKey(Key key, KeyModifiers modifiers) => Run(EditorKeymap.For(key, modifiers));

    internal bool Run(EditorCommand command)
    {
        if (command == EditorCommand.None || _model is not { } model) return false;
        switch (command)
        {
            case EditorCommand.PlayPause: PlayPause(); break;
            case EditorCommand.ShuttleBack: ShuttleBack(); break;
            case EditorCommand.ShuttleStop: ShuttleStop(); break;
            case EditorCommand.ShuttleForward: ShuttleForward(); break;
            case EditorCommand.FrameBack: StepFrame(-1); break;
            case EditorCommand.FrameForward: StepFrame(1); break;
            case EditorCommand.GoToStart: SeekTo(0); break;
            case EditorCommand.GoToEnd: SeekTo(model.Duration); break;
            case EditorCommand.MarkIn: MarkIn(); break;
            case EditorCommand.MarkOut: MarkOut(); break;
            case EditorCommand.AddMarker: Timeline.AddMarker(Timeline.Playhead); break;
            case EditorCommand.Split: Split(); break;
            case EditorCommand.DeleteSelected: DeleteSelected(); break;
            case EditorCommand.SelectAll: Timeline.SelectAll(); break;
            case EditorCommand.Undo: Undo(); break;
            case EditorCommand.Redo: Redo(); break;
            case EditorCommand.ToggleSnap: Timeline.SnapEnabled = !Timeline.SnapEnabled; break;
            case EditorCommand.TrimHead: TrimToPlayhead(true); break;
            case EditorCommand.TrimTail: TrimToPlayhead(false); break;
            case EditorCommand.PrevEdit: GoToEdit(false); break;
            case EditorCommand.NextEdit: GoToEdit(true); break;
            case EditorCommand.Back5: StepFrame(-5); break;
            case EditorCommand.Forward5: StepFrame(5); break;
            case EditorCommand.ZoomIn: Timeline.ZoomCentered(true); break;
            case EditorCommand.ZoomOut: Timeline.ZoomCentered(false); break;
            case EditorCommand.ZoomFit: Timeline.ZoomToFit(); break;
            case EditorCommand.GoToIn: GoToMark(true); break;
            case EditorCommand.GoToOut: GoToMark(false); break;
            case EditorCommand.FocusSpeed: FocusSpeed(); break;
            case EditorCommand.Extract: Extract(); break;
            default: return false;
        }

        return true;
    }

    private void PlayPause()
    {
        if (_driver is { Backward: true, Playing: true })
        {
            ShuttleStop();
            return;
        }

        if (_driver is { } driver && Math.Abs(driver.Rate - 1) > 1e-9) driver.SetRate(1);
        Preview.TogglePlay();
        SyncDriver();
    }

    private void ShuttleForward()
    {
        if (_driver is not { } driver) return;
        var rate = driver.Shuttle > 0 ? Math.Min(EditorKeymap.MaxShuttle, driver.Rate * 2) : 1;
        driver.SetRate(rate);
        if (driver.Backward || !driver.Playing) driver.Play();
        if (!Preview.IsPlaying) Preview.TogglePlay();
    }

    private void ShuttleBack()
    {
        if (_driver is not { } driver) return;
        var rate = driver.Shuttle < 0 ? Math.Min(EditorKeymap.MaxShuttle, driver.Rate * 2) : 1;
        if (Preview.IsPlaying) Preview.TogglePlay();
        driver.PlayBackward();
        driver.SetRate(rate);
    }

    private void ShuttleStop()
    {
        if (Preview.IsPlaying) Preview.TogglePlay();
        if (_driver is not { } driver) return;
        driver.Pause();
        driver.SetRate(1);
    }

    private void StepFrame(int direction)
    {
        ShuttleStop();
        SeekTo(Timeline.RoundToFrame(Timeline.Playhead + (long)Math.Round(direction * Timeline.FrameTicks)));
    }

    private void FocusSpeed()
    {
        if (!TxtSpeed.IsEnabled) return;
        TxtSpeed.Focus();
        TxtSpeed.SelectAll();
    }

    private void SeekTo(long time)
    {
        Timeline.Playhead = time;
        if (_driver is { } driver) _ = driver.SeekAsync(Timeline.Playhead);
    }

    private void SyncDriver()
    {
        if (_driver is not { } driver) return;
        if (driver.Backward)
        {
            if (Preview.IsPlaying) driver.Play();
            return;
        }

        if (Preview.IsPlaying && !driver.Playing) driver.Play();
        else if (!Preview.IsPlaying && driver.Playing) driver.Pause();
    }

    private void ShowGestures()
    {
        MnuSplit.InputGesture = Gesture(EditorCommand.Split);
        MnuDelete.InputGesture = Gesture(EditorCommand.DeleteSelected);
        MnuMarkIn.InputGesture = Gesture(EditorCommand.MarkIn);
        MnuMarkOut.InputGesture = Gesture(EditorCommand.MarkOut);
        ToolTip.SetTip(BtnSplit, Tip("editor.split", EditorCommand.Split));
        ToolTip.SetTip(BtnDelete, Tip("editor.delete", EditorCommand.DeleteSelected));
        ToolTip.SetTip(BtnUndo, Tip("editor.undo", EditorCommand.Undo));
        ToolTip.SetTip(BtnRedo, Tip("editor.redo", EditorCommand.Redo));
    }

    private static KeyGesture? Gesture(EditorCommand command)
        => EditorKeymap.First(command) is { } row ? new KeyGesture(row.Key, row.Modifiers) : null;

    internal static string Tip(string labelKey, EditorCommand command)
        => Strings.Get(labelKey) + " (" + EditorKeymap.Gesture(command) + ")";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        ShowGestures();
        if (TopLevel.GetTopLevel(this) is { } top) top.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        if (TopLevel.GetTopLevel(this) is { } top) top.RemoveHandler(KeyDownEvent, OnKey);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) ShowGestures();
        else Avalonia.Threading.Dispatcher.UIThread.Post(ShowGestures);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyVisible || e.Source is TextBox) return;
        if (HandleKey(e.Key, e.KeyModifiers)) e.Handled = true;
    }
}
