using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private string _stillColor = StillClip.FromHsl(0, 0, 0.5);
    private bool _syncingStill;

    internal string StillColor
    {
        get => _stillColor;
        set
        {
            if (!StillClip.TryHsl(value, out var hue, out var saturation, out var lightness)) return;
            _stillColor = StillClip.FromHsl(hue, saturation, lightness);
            ShowStillColor(hue, saturation, lightness);
        }
    }

    private void InitStillPanel()
    {
        MnuColorPick.Click += (_, _) => ShowStillPanel(true);
        MnuStillSettings.Click += (_, _) => ShowStillPanel(!StillPanel.IsVisible);
        SldStillHue.ValueChanged += (_, _) => OnStillSlider();
        SldStillSaturation.ValueChanged += (_, _) => OnStillSlider();
        SldStillLightness.ValueChanged += (_, _) => OnStillSlider();
        TxtStillColor.LostFocus += (_, _) => CommitStillColor();
        TxtStillColor.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitStillColor);
        TxtStillImageSeconds.LostFocus += (_, _) => CommitStillSeconds();
        TxtStillImageSeconds.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitStillSeconds);
        TxtStillFreezeSeconds.LostFocus += (_, _) => CommitStillSeconds();
        TxtStillFreezeSeconds.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitStillSeconds);
        BtnStillAddColor.Click += (_, _) => _ = AddPickedColorAsync();
        StillColor = _stillColor;
        ShowStillSeconds();
    }

    internal void ShowStillPanel(bool open)
    {
        StillPanel.IsVisible = open;
        if (!open) return;
        StillColor = _stillColor;
        ShowStillSeconds();
    }

    internal Task<bool> AddPickedColorAsync()
    {
        CommitStillColor();
        CommitStillSeconds();
        return AddColorClipAsync(_stillColor);
    }

    private void OnStillSlider()
    {
        if (_syncingStill) return;
        _stillColor = StillClip.FromHsl(SldStillHue.Value, SldStillSaturation.Value / 100, SldStillLightness.Value / 100);
        _syncingStill = true;
        TxtStillColor.Text = _stillColor;
        _syncingStill = false;
        StillSwatch.Background = Swatch(_stillColor);
    }

    private void CommitStillColor()
    {
        var before = _stillColor;
        StillColor = TxtStillColor.Text ?? string.Empty;
        if (ReferenceEquals(before, _stillColor)) TxtStillColor.Text = _stillColor;
    }

    private void ShowStillColor(double hue, double saturation, double lightness)
    {
        _syncingStill = true;
        SldStillHue.Value = hue;
        SldStillSaturation.Value = saturation * 100;
        SldStillLightness.Value = lightness * 100;
        TxtStillColor.Text = _stillColor;
        _syncingStill = false;
        StillSwatch.Background = Swatch(_stillColor);
    }

    private static Avalonia.Media.IBrush? Swatch(string color)
        => KullaniciRengi.Coz(color, out var rgb) ? KullaniciRengi.Firca(rgb) : null;

    private void CommitStillSeconds()
    {
        var image = ParseSeconds(TxtStillImageSeconds.Text);
        var freeze = ParseSeconds(TxtStillFreezeSeconds.Text);
        if (image is { } i) _imageSeconds = StillClip.ClampSeconds(i);
        if (freeze is { } f) _freezeSeconds = StillClip.ClampSeconds(f);
        ShowStillSeconds();
    }

    private void ShowStillSeconds()
    {
        TxtStillImageSeconds.Text = ImageSeconds.ToString("0.##", Strings.Culture);
        TxtStillFreezeSeconds.Text = FreezeSeconds.ToString("0.##", Strings.Culture);
    }

    private static double? ParseSeconds(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (!double.TryParse(trimmed, NumberStyles.Float, Strings.Culture, out var value)
            && !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
        return double.IsFinite(value) && value > 0 ? value : null;
    }
}
