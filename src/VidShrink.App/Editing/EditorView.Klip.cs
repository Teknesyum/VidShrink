using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core.Editing;
using VidShrink.Player;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    internal static readonly (int Num, int Den)[] CropPresets = { (0, 0), (16, 9), (9, 16), (1, 1), (4, 5), (4, 3) };

    private bool _showingClip;
    private int _restyles;

    internal int Restyles => _restyles;

    private void InitClip()
    {
        CmbClipCrop.Items.Clear();
        foreach (var (num, den) in CropPresets)
            CmbClipCrop.Items.Add(new ComboBoxItem
            {
                Content = num == 0 ? Strings.Get("editor.clip.crop.none") : num.ToString(CultureInfo.InvariantCulture) + ":" + den.ToString(CultureInfo.InvariantCulture)
            });

        Timeline.SelectionChanged += ShowClipPanel;
        Timeline.TextSelectionChanged += ShowClipPanel;
        BtnClipRotate.Click += (_, _) => RotateClip();
        BtnClipFlipH.Click += (_, _) => { if (_showingClip) SetClipEffects(e => e with { FlipH = BtnClipFlipH.IsChecked == true }); };
        BtnClipFlipV.Click += (_, _) => { if (_showingClip) SetClipEffects(e => e with { FlipV = BtnClipFlipV.IsChecked == true }); };
        BtnClipMute.Click += (_, _) => { if (_showingClip) SetClipEffects(e => e with { Muted = BtnClipMute.IsChecked == true }); };
        CmbClipCrop.SelectionChanged += (_, _) => { if (_showingClip && CmbClipCrop.SelectedIndex >= 0) CropClip(CmbClipCrop.SelectedIndex); };
        BtnClipReset.Click += (_, _) => { if (_showingClip) SetClipEffects(_ => ClipEffects.None); };
        TxtClipVolume.LostFocus += (_, _) => CommitClipVolume();
        TxtClipVolume.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitClipVolume);
        TxtClipFadeIn.LostFocus += (_, _) => CommitClipFade(true);
        TxtClipFadeIn.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitClipFade(true));
        TxtClipFadeOut.LostFocus += (_, _) => CommitClipFade(false);
        TxtClipFadeOut.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitClipFade(false));
        ShowClipPanel();
    }

    /// <summary>Panelin yazdigi klipler: hepsi seciliyse hepsi, degilse secim; secim bossa etkin klip.</summary>
    private IReadOnlyList<int> ClipTargets()
    {
        if (_model is not { } model || model.Clips.Count == 0) return Array.Empty<int>();
        if (Timeline.AllSelected) return Enumerable.Range(0, model.Clips.Count).ToArray();
        var selected = Timeline.SelectedIndices.Where(i => i >= 0 && i < model.Clips.Count).ToArray();
        if (selected.Length > 0) return selected;
        var active = ActiveIndex;
        return active >= 0 && active < model.Clips.Count ? new[] { active } : Array.Empty<int>();
    }

    /// <summary>Secili kliplerin ayarini yazar; tek geri alma adimi. Degisiklik yoksa <c>false</c>.</summary>
    internal bool SetClipEffects(Func<ClipEffects, ClipEffects> change)
    {
        var targets = ClipTargets();
        if (targets.Count == 0) return false;
        var done = Apply(model => model.SetEffects(targets, change), -1);
        if (!done) ShowClipPanel();
        return done;
    }

    internal bool RotateClip()
    {
        if (!_showingClip) return false;
        var (width, height, _, _) = SourceGeometry();
        return SetClipEffects(e =>
        {
            var preset = CropPresetIndex(e, width, height);
            var turned = e with { Rotation = (e.Rotation + 90) % 360 };
            return preset > 0 ? turned.WithAspect(CropPresets[preset].Num, CropPresets[preset].Den, width, height) : turned;
        });
    }

    internal bool CropClip(int preset)
    {
        if (preset < 0 || preset >= CropPresets.Length) return false;
        var (width, height, _, _) = SourceGeometry();
        var (num, den) = CropPresets[preset];
        return SetClipEffects(e => e.WithAspect(num, den, width, height));
    }

    private void CommitClipVolume()
    {
        if (!_showingClip) return;
        var text = TxtClipVolume.Text?.Trim();
        double db = 0;
        if (!string.IsNullOrEmpty(text)
            && !double.TryParse(text, NumberStyles.Float, Strings.Culture, out db)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out db))
        {
            ShowClipPanel();
            return;
        }

        if (!double.IsFinite(db) || !SetClipEffects(e => e with { VolumeDb = db })) ShowClipPanel();
    }

    private void CommitClipFade(bool fadeIn)
    {
        if (!_showingClip) return;
        var box = fadeIn ? TxtClipFadeIn : TxtClipFadeOut;
        var text = box.Text?.Trim();
        double seconds = 0;
        if (!string.IsNullOrEmpty(text)
            && !double.TryParse(text, NumberStyles.Float, Strings.Culture, out seconds)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            ShowClipPanel();
            return;
        }

        if (!double.IsFinite(seconds) || seconds < 0)
        {
            ShowClipPanel();
            return;
        }

        var ticks = EditTime.FromSeconds(Math.Round(seconds, 2));
        if (!SetClipEffects(e => fadeIn ? e with { FadeIn = ticks } : e with { FadeOut = ticks })) ShowClipPanel();
    }

    private void ShowClipPanel()
    {
        var index = ActiveIndex;
        if (_model is not { } model || index < 0 || index >= model.Clips.Count || Timeline.SelectedText >= 0)
        {
            _showingClip = false;
            ClipPanel.IsVisible = false;
            return;
        }

        var effects = model.Clips[index].Effects;
        var (width, height, _, _) = SourceGeometry();
        _showingClip = false;
        BtnClipFlipH.IsChecked = effects.FlipH;
        BtnClipFlipV.IsChecked = effects.FlipV;
        BtnClipMute.IsChecked = effects.Muted;
        CmbClipCrop.SelectedIndex = CropPresetIndex(effects, width, height);
        TxtClipVolume.Text = effects.VolumeDb.ToString("0.#", Strings.Culture);
        TxtClipFadeIn.Text = EditTime.ToSeconds(effects.FadeIn).ToString("0.##", Strings.Culture);
        TxtClipFadeOut.Text = EditTime.ToSeconds(effects.FadeOut).ToString("0.##", Strings.Culture);
        ClipPanel.IsVisible = true;
        _showingClip = true;
    }

    /// <summary>Ayarin hangi oran secenegine denk geldigi; kirpma yoksa 0, elle kirpilmissa -1.</summary>
    internal static int CropPresetIndex(ClipEffects effects, int width, int height)
    {
        if (!effects.HasCrop) return 0;
        for (var i = 1; i < CropPresets.Length; i++)
        {
            var candidate = effects.WithAspect(CropPresets[i].Num, CropPresets[i].Den, width, height);
            if (Math.Abs(candidate.CropLeft - effects.CropLeft) < 1e-9 && Math.Abs(candidate.CropRight - effects.CropRight) < 1e-9
                && Math.Abs(candidate.CropTop - effects.CropTop) < 1e-9 && Math.Abs(candidate.CropBottom - effects.CropBottom) < 1e-9)
                return i;
        }

        return -1;
    }

    /// <summary>Kaynagin gorunen boyutu ve piksel orani; bilinmiyorsa motordan, o da yoksa 1920x1080.</summary>
    private (int Width, int Height, int ParNum, int ParDen) SourceGeometry()
    {
        if (_source is { } source && KnownInfo?.Invoke(source) is { Width: > 0, Height: > 0 } info)
            return (ClipFilters.DisplayWidth(info.Width, info.ParNum, info.ParDen), info.Height, info.ParNum, info.ParDen);
        if (Preview.Engine is { Details: { Width: > 0, Height: > 0 } details }) return (details.Width, details.Height, 1, 1);
        return (FallbackWidth, FallbackHeight, 1, 1);
    }

    private EditLook? LookFor(EdlPreview preview, IPlaybackEngine engine)
    {
        if (!preview.Parts.Any(p => !p.Clip.Effects.IsNeutral)) return null;
        if (_source is { } source && KnownInfo?.Invoke(source) is { Width: > 0, Height: > 0 } info)
            return new EditLook(preview, info.Width, info.Height, info.ParNum, info.ParDen);
        var (width, height) = FrameSize(engine);
        return new EditLook(preview, width, height);
    }

    private async Task RestyleAsync()
    {
        if (_model is not { } model || _source is not { } source || _driver is not { } driver || Preview.Engine is not { } engine) return;
        try
        {
            var preview = new EdlPreview(SourcePaths, model);
            await driver.RestyleAsync(preview, LookFor(preview, engine)).ConfigureAwait(true);
            _restyles++;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private static bool SameCuts(IReadOnlyList<EditClip> before, IReadOnlyList<EditClip> after)
        => before.Count == after.Count
           && before.Zip(after).All(p => p.First with { Effects = ClipEffects.None } == p.Second with { Effects = ClipEffects.None });
}
