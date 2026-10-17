using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private enum GradeField { CurveShadows, CurveMids, CurveHighlights, ShadowHue, ShadowAmount, MidHue, MidAmount, HighlightHue, HighlightAmount }

    private void InitClipGrade()
    {
        BtnClipAutoColor.Click += (_, _) => { if (_showingClip) SetClipEffects(e => e with { AutoColor = BtnClipAutoColor.IsChecked == true }); };
        foreach (var (box, field) in new[]
                 {
                     (TxtClipCurveShadows, GradeField.CurveShadows), (TxtClipCurveMids, GradeField.CurveMids), (TxtClipCurveHighlights, GradeField.CurveHighlights),
                     (TxtClipShadowHue, GradeField.ShadowHue), (TxtClipShadowAmount, GradeField.ShadowAmount),
                     (TxtClipMidHue, GradeField.MidHue), (TxtClipMidAmount, GradeField.MidAmount),
                     (TxtClipHighlightHue, GradeField.HighlightHue), (TxtClipHighlightAmount, GradeField.HighlightAmount),
                 })
        {
            box.LostFocus += (_, _) => CommitClipGrade(box, field);
            box.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitClipGrade(box, field));
        }
    }

    /// <summary>Ileri renk kutusunu yazar; bos ya da okunamayan giris eski degere doner, sinir disi <see cref="ClipEffects.Normalized"/> ile kirpilir.</summary>
    private void CommitClipGrade(TextBox box, GradeField field)
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

        var hue = (int)Math.Round(Math.Clamp(value, -100_000, 100_000));
        var done = SetClipEffects(e => (field switch
        {
            GradeField.CurveShadows => e with { CurveShadows = value },
            GradeField.CurveMids => e with { CurveMids = value },
            GradeField.CurveHighlights => e with { CurveHighlights = value },
            GradeField.ShadowHue => e with { ShadowHue = hue },
            GradeField.ShadowAmount => e with { ShadowAmount = value },
            GradeField.MidHue => e with { MidHue = hue },
            GradeField.MidAmount => e with { MidAmount = value },
            GradeField.HighlightHue => e with { HighlightHue = hue },
            _ => e with { HighlightAmount = value },
        }).Normalized());
        if (!done) ShowClipPanel();
    }

    private void ShowClipGrade(ClipEffects effects)
    {
        BtnClipAutoColor.IsChecked = effects.AutoColor;
        TxtClipCurveShadows.Text = effects.CurveShadows.ToString("0.##", Strings.Culture);
        TxtClipCurveMids.Text = effects.CurveMids.ToString("0.##", Strings.Culture);
        TxtClipCurveHighlights.Text = effects.CurveHighlights.ToString("0.##", Strings.Culture);
        TxtClipShadowHue.Text = effects.ShadowHue.ToString(CultureInfo.InvariantCulture);
        TxtClipShadowAmount.Text = effects.ShadowAmount.ToString("0.##", Strings.Culture);
        TxtClipMidHue.Text = effects.MidHue.ToString(CultureInfo.InvariantCulture);
        TxtClipMidAmount.Text = effects.MidAmount.ToString("0.##", Strings.Culture);
        TxtClipHighlightHue.Text = effects.HighlightHue.ToString(CultureInfo.InvariantCulture);
        TxtClipHighlightAmount.Text = effects.HighlightAmount.ToString("0.##", Strings.Culture);
    }
}
