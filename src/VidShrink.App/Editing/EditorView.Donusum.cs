using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    internal static readonly EditFrame[] FramePresets = { EditFrame.Source, new(16, 9), new(9, 16), new(1, 1) };

    private bool _showingFrame;

    private enum TransformField { Scale, OffsetX, OffsetY, Angle }

    private void InitTransform()
    {
        CmbProjectFrame.Items.Clear();
        foreach (var frame in FramePresets)
            CmbProjectFrame.Items.Add(new ComboBoxItem
            {
                Content = frame.IsSource
                    ? Strings.Get("editor.frame.source")
                    : frame.Num.ToString(CultureInfo.InvariantCulture) + ":" + frame.Den.ToString(CultureInfo.InvariantCulture)
            });
        CmbProjectFrame.SelectedIndex = 0;
        CmbProjectFrame.SelectionChanged += (_, _) =>
        {
            if (_showingFrame && CmbProjectFrame.SelectedIndex >= 0 && CmbProjectFrame.SelectedIndex < FramePresets.Length)
                SetFrame(FramePresets[CmbProjectFrame.SelectedIndex]);
        };
        _showingFrame = true;

        BtnClipKeyTransform.Click += (_, _) => AddTransformKey();
        foreach (var (box, field) in new[] { (TxtClipScale, TransformField.Scale), (TxtClipOffsetX, TransformField.OffsetX), (TxtClipOffsetY, TransformField.OffsetY), (TxtClipAngle, TransformField.Angle) })
        {
            box.LostFocus += (_, _) => CommitClipTransform(box, field);
            box.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitClipTransform(box, field));
        }
    }

    internal bool SetFrame(EditFrame frame)
    {
        if (_model is null || Exporting) return false;
        var done = Apply(model => model.SetFrame(frame), -1, reload: true);
        if (!done) ShowFrame();
        return done;
    }

    private void ShowFrame()
    {
        var frame = _model?.Frame ?? EditFrame.Source;
        _showingFrame = false;
        CmbProjectFrame.SelectedIndex = Math.Max(0, Array.IndexOf(FramePresets, frame));
        CmbProjectFrame.IsEnabled = _model is not null && !Exporting;
        _showingFrame = true;
    }

    private void ShowTransform(ClipEffects effects)
    {
        TxtClipScale.Text = (effects.Scale * 100).ToString("0.#", Strings.Culture);
        TxtClipOffsetX.Text = (effects.OffsetX * 100).ToString("0.#", Strings.Culture);
        TxtClipOffsetY.Text = (effects.OffsetY * 100).ToString("0.#", Strings.Culture);
        TxtClipAngle.Text = effects.Angle.ToString("0.#", Strings.Culture);
    }

    internal bool AddTransformKey()
    {
        if (!_showingClip) return false;
        var offset = KeyOffset();
        var ease = SelectedEase;
        return SetClipEffects(e => e with { TransformKeys = e.TransformKeys.With(new TransformKey(offset, e.Scale, e.OffsetX, e.OffsetY, e.Angle, ease)) });
    }

    private void CommitClipTransform(TextBox box, TransformField field)
    {
        if (!_showingClip) return;
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text)
            || (!double.TryParse(text, NumberStyles.Float, Strings.Culture, out var value)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            || !double.IsFinite(value))
        {
            ShowClipPanel();
            return;
        }

        var done = SetClipEffects(e => (field switch
        {
            TransformField.Scale => e with { Scale = value / 100 },
            TransformField.OffsetX => e with { OffsetX = value / 100 },
            TransformField.OffsetY => e with { OffsetY = value / 100 },
            _ => e with { Angle = value },
        }).Normalized());
        if (!done) ShowClipPanel();
    }
}
