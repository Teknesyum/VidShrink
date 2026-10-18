using System;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private Point? _blurStart;

    private void InitBlur()
    {
        TxtClipBlur.LostFocus += (_, _) => CommitClipBlur();
        TxtClipBlur.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitClipBlur);
        BtnClipBlurWhole.Click += (_, _) =>
        {
            if (!_showingClip) return;
            BtnClipBlurRegion.IsChecked = false;
            SetClipEffects(e => (e with
            {
                BlurSigma = e.HasBlur ? e.BlurSigma : ClipEffects.DefaultBlurSigma,
                BlurX = 0,
                BlurY = 0,
                BlurWidth = 0,
                BlurHeight = 0,
            }).Normalized());
        };
        AddHandler(PointerPressedEvent, OnMonitorBlurPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnMonitorBlurReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, _) => _blurStart = null, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
    }

    private void ShowClipBlur(ClipEffects effects)
    {
        TxtClipBlur.Text = effects.BlurSigma.ToString("0.#", Strings.Culture);
        if (!effects.HasBlur) BtnClipBlurRegion.IsChecked = false;
    }

    internal bool SetClipBlurRegion(double x1, double y1, double x2, double y2)
    {
        if (!_showingClip) return false;
        var done = SetClipEffects(e => e.WithBlurRegion(Math.Clamp(x1, 0, 1), Math.Clamp(y1, 0, 1), Math.Clamp(x2, 0, 1), Math.Clamp(y2, 0, 1)));
        BtnClipBlurRegion.IsChecked = false;
        return done;
    }

    private void CommitClipBlur()
    {
        if (!_showingClip) return;
        var text = TxtClipBlur.Text?.Trim();
        double sigma = 0;
        if (!string.IsNullOrEmpty(text)
            && (!double.TryParse(text, NumberStyles.Float, Strings.Culture, out sigma)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out sigma)
                || !double.IsFinite(sigma)))
        {
            ShowClipPanel();
            return;
        }

        if (!SetClipEffects(e => (e with { BlurSigma = sigma }).Normalized())) ShowClipPanel();
    }

    private void OnMonitorBlurPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_showingClip || BtnClipBlurRegion.IsChecked != true) return;
        if (e.Source is not Visual source || !(ReferenceEquals(source, Preview.Surface) || Preview.Surface.IsVisualAncestorOf(source))) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !MonitorFraction(e, out var fx, out var fy)) return;
        _blurStart = new Point(fx, fy);
        e.Pointer.Capture(Preview.Surface);
        e.Handled = true;
    }

    private void OnMonitorBlurReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_blurStart is not { } start) return;
        _blurStart = null;
        e.Pointer.Capture(null);
        if (MonitorFraction(e, out var fx, out var fy)) SetClipBlurRegion(start.X, start.Y, fx, fy);
        e.Handled = true;
    }
}
