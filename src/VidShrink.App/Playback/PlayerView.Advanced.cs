using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal enum PictureKnob
{
    Brightness,
    Contrast,
    Saturation,
    Gamma,
    Hue,
    Sharpness,
    Crop
}

internal partial class PlayerView
{
    internal const int PictureStep = 5;
    internal const double SubtitleStyleStep = 0.5;
    private const string ResetTrace = " -> reset";
    internal const string SubtitleBackgroundOn = "#C0000000";
    internal const string SubtitleBackgroundOff = "#00000000";

    internal static readonly IReadOnlyList<string> SubtitleFonts = new[] { "sans-serif", "serif", "monospace" };

    internal static readonly IReadOnlyList<(string Key, string Value)> SubtitleColors = new[]
    {
        ("player.advanced.color-white", "#FFFFFFFF"),
        ("player.advanced.color-yellow", "#FFFFFF00"),
        ("player.advanced.color-green", "#FF00FF00")
    };

    internal static readonly IReadOnlyList<(string Key, int[] Bands)> EqualizerPresets = new[]
    {
        ("player.advanced.equalizer-flat", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("player.advanced.equalizer-bass", new[] { 6, 5, 3, 1, 0, 0, 0, 0, 0, 0 }),
        ("player.advanced.equalizer-voice", new[] { -3, -2, 0, 3, 4, 3, 1, 0, 0, 0 }),
        ("player.advanced.equalizer-treble", new[] { 0, 0, 0, 0, 0, 2, 4, 5, 6, 6 })
    };

    private PlayerAdvanced _advanced = new();
    private bool _advancedRead;
    private string? _advancedFolder;

    internal event EventHandler? AdvancedChanged;

    internal PlayerAdvanced Advanced
    {
        get
        {
            EnsureAdvanced();
            return _advanced;
        }
    }

    internal static IReadOnlyList<PictureKnob> Knobs { get; } = new[]
    {
        PictureKnob.Brightness,
        PictureKnob.Contrast,
        PictureKnob.Saturation,
        PictureKnob.Gamma,
        PictureKnob.Hue,
        PictureKnob.Sharpness,
        PictureKnob.Crop
    };

    internal static string KnobKey(PictureKnob knob) => knob switch
    {
        PictureKnob.Brightness => "player.advanced.brightness",
        PictureKnob.Contrast => "player.advanced.contrast",
        PictureKnob.Saturation => "player.advanced.saturation",
        PictureKnob.Gamma => "player.advanced.gamma",
        PictureKnob.Hue => "player.advanced.hue",
        PictureKnob.Sharpness => "player.advanced.sharpness",
        _ => "player.advanced.crop"
    };

    internal int KnobValue(PictureKnob knob)
    {
        var picture = Advanced.Picture;
        return knob switch
        {
            PictureKnob.Brightness => picture.Brightness,
            PictureKnob.Contrast => picture.Contrast,
            PictureKnob.Saturation => picture.Saturation,
            PictureKnob.Gamma => picture.Gamma,
            PictureKnob.Hue => picture.Hue,
            PictureKnob.Sharpness => picture.Sharpness,
            _ => picture.Crop
        };
    }

    internal string KnobLabel(PictureKnob knob)
        => Strings.Get(KnobKey(knob), PlayerAdvanced.Signed(KnobValue(knob)));

    internal void NudgePicture(PictureKnob knob, int steps)
    {
        EnsureAdvanced();
        var picture = _advanced.Picture;
        var delta = steps * PictureStep;
        _advanced.Picture = (knob switch
        {
            PictureKnob.Brightness => picture with { Brightness = picture.Brightness + delta },
            PictureKnob.Contrast => picture with { Contrast = picture.Contrast + delta },
            PictureKnob.Saturation => picture with { Saturation = picture.Saturation + delta },
            PictureKnob.Gamma => picture with { Gamma = picture.Gamma + delta },
            PictureKnob.Hue => picture with { Hue = picture.Hue + delta },
            PictureKnob.Sharpness => picture with { Sharpness = picture.Sharpness + delta },
            _ => picture with { Crop = picture.Crop + delta }
        }).Clamped();

        _engine?.SetPicture(_advanced.Picture);
        Settle("picture " + knob.ToString().ToLowerInvariant() + " -> " + KnobValue(knob).ToString(CultureInfo.InvariantCulture));
    }

    internal void ToggleDeinterlace()
    {
        EnsureAdvanced();
        _advanced.Picture = _advanced.Picture with { Deinterlace = !_advanced.Picture.Deinterlace };
        _engine?.SetPicture(_advanced.Picture);
        Settle("deinterlace -> " + _advanced.Picture.Deinterlace);
    }

    internal void ResetPicture()
    {
        EnsureAdvanced();
        _advanced.Picture = PictureAdjust.Neutral;
        _engine?.SetPicture(_advanced.Picture);
        Settle("picture" + ResetTrace);
    }

    internal void UseEqualizer(IReadOnlyList<int> bands)
    {
        EnsureAdvanced();
        var wanted = new SoundAdjust(bands.ToArray(), _advanced.Sound.Normalize, _advanced.Sound.Boost).Clamped();
        if (!wanted.Valid) return;
        _advanced.Sound = wanted;
        _engine?.SetSound(_advanced.Sound);
        Settle("equalizer -> " + string.Join(",", _advanced.Sound.Bands));
    }

    internal void ToggleNormalize()
    {
        EnsureAdvanced();
        _advanced.Sound = _advanced.Sound with { Normalize = !_advanced.Sound.Normalize };
        _engine?.SetSound(_advanced.Sound);
        Settle("normalize -> " + _advanced.Sound.Normalize);
    }

    internal void ToggleBoost()
    {
        EnsureAdvanced();
        _advanced.Sound = _advanced.Sound with { Boost = !_advanced.Sound.Boost };
        _engine?.SetSound(_advanced.Sound);
        if (_volume > VolumeCeiling())
        {
            _volume = VolumeCeiling();
            _engine?.SetVolume(_volume);
        }

        Settle("boost -> " + _advanced.Sound.Boost);
    }

    internal void ResetSound()
    {
        EnsureAdvanced();
        _advanced.Sound = SoundAdjust.Neutral;
        _engine?.SetSound(_advanced.Sound);
        if (_volume > VolumeCeiling())
        {
            _volume = VolumeCeiling();
            _engine?.SetVolume(_volume);
        }

        Settle("sound" + ResetTrace);
    }

    internal void UseSubtitleFont(string font)
    {
        EnsureAdvanced();
        _advanced.Style = _advanced.Style with { Font = font };
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("subfont -> " + font);
    }

    internal void UseSubtitleColor(string color)
    {
        EnsureAdvanced();
        _advanced.Style = _advanced.Style with { Color = color };
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("subcolor -> " + color);
    }

    internal void NudgeSubtitleOutline(double steps)
    {
        EnsureAdvanced();
        var now = _advanced.Style.Outline ?? 0;
        _advanced.Style = _advanced.Style with { Outline = Math.Clamp(now + steps * SubtitleStyleStep, 0, 8) };
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("suboutline -> " + Measure(_advanced.Style.Outline));
    }

    internal void NudgeSubtitleShadow(double steps)
    {
        EnsureAdvanced();
        var now = _advanced.Style.Shadow ?? 0;
        _advanced.Style = _advanced.Style with { Shadow = Math.Clamp(now + steps * SubtitleStyleStep, 0, 8) };
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("subshadow -> " + Measure(_advanced.Style.Shadow));
    }

    internal void ToggleSubtitleBackground()
    {
        EnsureAdvanced();
        var on = string.Equals(_advanced.Style.Background, SubtitleBackgroundOn, StringComparison.OrdinalIgnoreCase);
        _advanced.Style = _advanced.Style with { Background = on ? SubtitleBackgroundOff : SubtitleBackgroundOn };
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("subback -> " + (_advanced.Style.Background ?? "default"));
    }

    internal void ResetSubtitleStyle()
    {
        EnsureAdvanced();
        _advanced.Style = SubtitleStyle.Inherited;
        _engine?.SetSubtitleStyle(_advanced.Style);
        Settle("substyle" + ResetTrace);
    }

    internal void ResetAdvanced()
    {
        EnsureAdvanced();
        _advanced.Reset();
        if (_engine is { } engine) _advanced.ApplyTo(engine);
        if (_volume > VolumeCeiling())
        {
            _volume = VolumeCeiling();
            _engine?.SetVolume(_volume);
        }

        Settle("advanced" + ResetTrace);
    }

    internal void SaveAdvanced()
    {
        EnsureAdvanced();
        _advanced.Save(AdvancedFile());
    }

    private double VolumeCeiling()
    {
        EnsureAdvanced();
        return _advanced.Sound.Boost ? SoundAdjust.BoostCeiling : SoundAdjust.PlainCeiling;
    }

    private void Settle(string trace)
    {
        SaveAdvanced();
        _trace.Add(trace);
        RefreshState();
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Measure(double? value)
        => (value ?? 0).ToString("0.##", CultureInfo.InvariantCulture);

    private void EnsureAdvanced()
    {
        var folder = SettingsFolder();
        if (_advancedRead && string.Equals(folder, _advancedFolder, StringComparison.Ordinal)) return;
        _advancedRead = true;
        _advancedFolder = folder;
        _advanced = PlayerAdvanced.Load(AdvancedFile());
    }

    private string? AdvancedFile()
        => SettingsFolder() is { } folder ? Path.Combine(folder, PlayerAdvanced.FileName) : null;

    private void ApplyAdvanced(IPlaybackEngine engine)
    {
        EnsureAdvanced();
        _advanced.ApplyTo(engine);
    }

    /// <summary>
    /// Anahtar adi parcadan birlestirilmez: dizgeden kurulan anahtari olu ceviri taramasi
    /// goremiyor, bu yuzden ucu de duz yazili.
    /// </summary>
    internal static string StateKey(string part) => part switch
    {
        "picture" => "player.advanced.state-picture",
        "sound" => "player.advanced.state-sound",
        _ => "player.advanced.state-subtitle"
    };

    private void AppendAdvancedState(List<string> parts)
    {
        EnsureAdvanced();
        var shown = _advanced.State()
            .Select(part => Strings.Get(StateKey(part)))
            .ToList();
        if (shown.Count > 0) parts.Add(Strings.Get("player.advanced.state", string.Join(", ", shown)));
    }

    /// <summary>
    /// Sag tik menusunun **sonuna** eklenir; basligi duz metin ve <c>Tag</c>'i bos oldugu
    /// icin menu olculeri onu kisayol satiri saymaz.
    /// </summary>
    private void AppendAdvancedMenu(MenuFlyout flyout)
    {
        EnsureAdvanced();
        flyout.Items.Add(new Separator());
        flyout.Items.Add(Submenu(Strings.Get("player.advanced.menu"), new List<Control>
        {
            Submenu(Strings.Get("player.advanced.picture"), PictureItems()),
            Submenu(Strings.Get("player.advanced.sound"), SoundItems()),
            Submenu(Strings.Get("player.advanced.subtitle"), SubtitleStyleItems()),
            new Separator(),
            Plain(Strings.Get("player.advanced.reset"), ResetAdvanced)
        }));
    }

    internal List<Control> PictureItems()
    {
        var items = new List<Control>();
        foreach (var knob in Knobs)
        {
            var which = knob;
            items.Add(Submenu(KnobLabel(which), new List<Control>
            {
                Plain(Strings.Get("player.advanced.more"), () => NudgePicture(which, 1)),
                Plain(Strings.Get("player.advanced.less"), () => NudgePicture(which, -1))
            }));
        }

        items.Add(new Separator());
        items.Add(Switch(Strings.Get("player.advanced.deinterlace"), Advanced.Picture.Deinterlace, ToggleDeinterlace));
        items.Add(Plain(Strings.Get("player.advanced.picture-reset"), ResetPicture));
        return items;
    }

    internal List<Control> SoundItems()
    {
        var items = new List<Control>();
        var bands = Advanced.Sound.Bands;
        foreach (var (key, preset) in EqualizerPresets)
        {
            var wanted = preset;
            items.Add(Choice(Strings.Get(key), bands.SequenceEqual(wanted), () => UseEqualizer(wanted)));
        }

        items.Add(new Separator());
        items.Add(Switch(Strings.Get("player.advanced.normalize"), Advanced.Sound.Normalize, ToggleNormalize));
        items.Add(Switch(Strings.Get("player.advanced.boost"), Advanced.Sound.Boost, ToggleBoost));
        items.Add(Plain(Strings.Get("player.advanced.sound-reset"), ResetSound));
        return items;
    }

    internal List<Control> SubtitleStyleItems()
    {
        var style = Advanced.Style;
        var items = new List<Control>
        {
            Submenu(Strings.Get("player.advanced.font"), SubtitleFonts
                .Select(font => (Control)Choice(font, string.Equals(style.Font, font, StringComparison.OrdinalIgnoreCase), () => UseSubtitleFont(font)))
                .ToList()),
            Submenu(Strings.Get("player.advanced.color"), SubtitleColors
                .Select(pair => (Control)Choice(Strings.Get(pair.Key), string.Equals(style.Color, pair.Value, StringComparison.OrdinalIgnoreCase), () => UseSubtitleColor(pair.Value)))
                .ToList()),
            Submenu(Strings.Get("player.advanced.outline", Measure(style.Outline)), new List<Control>
            {
                Plain(Strings.Get("player.advanced.more"), () => NudgeSubtitleOutline(1)),
                Plain(Strings.Get("player.advanced.less"), () => NudgeSubtitleOutline(-1))
            }),
            Submenu(Strings.Get("player.advanced.shadow", Measure(style.Shadow)), new List<Control>
            {
                Plain(Strings.Get("player.advanced.more"), () => NudgeSubtitleShadow(1)),
                Plain(Strings.Get("player.advanced.less"), () => NudgeSubtitleShadow(-1))
            }),
            Switch(Strings.Get("player.advanced.background"), string.Equals(style.Background, SubtitleBackgroundOn, StringComparison.OrdinalIgnoreCase), ToggleSubtitleBackground),
            Plain(Strings.Get("player.advanced.subtitle-reset"), ResetSubtitleStyle)
        };
        return items;
    }

    private static MenuItem Switch(string header, bool on, Action act)
    {
        var item = new MenuItem { Header = header, ToggleType = MenuItemToggleType.CheckBox, IsChecked = on };
        item.Click += (_, _) => act();
        return item;
    }
}
