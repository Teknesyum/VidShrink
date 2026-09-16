using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal static readonly RecorderContainer[] Containers =
    {
        RecorderContainer.Mkv, RecorderContainer.Mp4, RecorderContainer.Mov, RecorderContainer.Gif
    };

    private bool _fillingAdvanced;

    private IReadOnlyList<string> _profileItems = [];

    private IReadOnlyList<string> _tuneItems = [];

    private IReadOnlyList<string> _pixelItems = [];

    private void InitGelismis()
    {
        TxtScaleWidth.Text = Blank(_settings.ScaleWidth);
        TxtScaleHeight.Text = Blank(_settings.ScaleHeight);
        TxtKeyframe.Text = _settings.KeyframeSeconds.ToString(CultureInfo.InvariantCulture);
        TxtBitrate.Text = Blank(_settings.BitrateKbps);
        TxtMaxBitrate.Text = Blank(_settings.MaxBitrateKbps);
        TxtBuffer.Text = Blank(_settings.BufferKbits);
        TxtMaxDuration.Text = Blank(_settings.MaxDurationSeconds);
        TxtSplitSeconds.Text = Blank(_settings.SplitSeconds);
        TxtSplitMegabytes.Text = Blank(_settings.SplitMegabytes);
        TxtAudioGain.Text = _settings.AudioGainDb.ToString("0.##", CultureInfo.InvariantCulture);
        ChkNoiseGate.IsChecked = _settings.AudioNoiseGate;
        ChkNoiseSuppression.IsChecked = _settings.AudioNoiseSuppression;

        RefreshAdvancedLabels();

        CmbCodec.SelectionChanged += (_, _) => RefreshCodecChoices();
        CmbRateControl.SelectionChanged += (_, _) => ApplyRateRows();
        ApplyRateRows();
    }

    private static string Blank(double value)
        => value > 0 ? value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    private string SelectedCodec => CmbCodec.SelectedItem as string ?? RecorderArguments.DefaultVideoCodec;

    internal RecorderContainer SelectedContainer =>
        CmbContainer.SelectedIndex >= 0 && CmbContainer.SelectedIndex < Containers.Length
            ? Containers[CmbContainer.SelectedIndex]
            : _settings.Container;

    internal RecorderRateControl SelectedRateControl =>
        CmbRateControl.SelectedIndex == (int)RecorderRateControl.Bitrate ? RecorderRateControl.Bitrate
        : CmbRateControl.SelectedIndex < 0 ? _settings.RateControl
        : RecorderRateControl.Quality;

    internal AudioTrackLayout SelectedAudioLayout =>
        CmbAudioLayout.SelectedIndex == (int)AudioTrackLayout.SeparateTracks ? AudioTrackLayout.SeparateTracks
        : CmbAudioLayout.SelectedIndex < 0 ? _settings.AudioLayout
        : AudioTrackLayout.MixedSingleTrack;

    private string? SelectedProfile => Optional(CmbProfile, _profileItems, _settings.Profile);

    private string? SelectedTune => Optional(CmbTune, _tuneItems, _settings.Tune);

    private string? SelectedColorSpace => Optional(CmbColorSpace, RecorderArguments.ColorSpaces, _settings.ColorSpace);

    private string? SelectedColorRange => Optional(CmbColorRange, RecorderArguments.ColorRanges, _settings.ColorRange);

    private string SelectedPixelFormat
    {
        get
        {
            var index = CmbPixelFormat.SelectedIndex;
            return index >= 0 && index < _pixelItems.Count ? _pixelItems[index] : _settings.PixelFormat;
        }
    }

    private static string? Optional(ComboBox box, IReadOnlyList<string> values, string? fallback)
        => box.SelectedIndex switch
        {
            < 0 => fallback,
            0 => null,
            var index when index - 1 < values.Count => values[index - 1],
            _ => fallback
        };

    private void RefreshAdvancedLabels()
    {
        _fillingAdvanced = true;
        try
        {
            var container = SelectedContainer;
            var rate = SelectedRateControl;
            var layout = SelectedAudioLayout;
            var space = SelectedColorSpace;
            var range = SelectedColorRange;

            CmbContainer.ItemsSource = Containers.Select(c => RecorderArguments.Extension(c).ToUpperInvariant()).ToList();
            CmbContainer.SelectedIndex = Array.IndexOf(Containers, container);

            CmbRateControl.ItemsSource = new List<string> { Say("recorder.advanced.rate-quality"), Say("recorder.advanced.rate-bitrate") };
            CmbRateControl.SelectedIndex = (int)rate;

            CmbAudioLayout.ItemsSource = new List<string> { Say("recorder.advanced.audio-mixed"), Say("recorder.advanced.audio-separate") };
            CmbAudioLayout.SelectedIndex = (int)layout;

            FillOptional(CmbColorSpace, RecorderArguments.ColorSpaces, space);
            FillOptional(CmbColorRange, RecorderArguments.ColorRanges, range);
        }
        finally
        {
            _fillingAdvanced = false;
        }

        RefreshCodecChoices();
    }

    private void RefreshCodecChoices()
    {
        var profile = SelectedProfile;
        var tune = SelectedTune;
        var pixel = SelectedPixelFormat;
        var codec = SelectedCodec;

        _fillingAdvanced = true;
        try
        {
            _profileItems = RecorderArguments.ProfilesFor(codec);
            _tuneItems = RecorderArguments.TunesFor(codec);
            _pixelItems = RecorderArguments.PixelFormatsFor(codec);
            FillOptional(CmbProfile, _profileItems, profile);
            FillOptional(CmbTune, _tuneItems, tune);

            var formats = _pixelItems;
            CmbPixelFormat.ItemsSource = formats.ToList();
            var index = IndexOf(formats, pixel);
            CmbPixelFormat.SelectedIndex = index >= 0 ? index : IndexOf(formats, RecorderArguments.DefaultPixelFormat);
        }
        finally
        {
            _fillingAdvanced = false;
        }

        CmbProfile.IsEnabled = _profileItems.Count > 0;
        CmbTune.IsEnabled = _tuneItems.Count > 0;
    }

    private static int IndexOf(IReadOnlyList<string> values, string? value)
    {
        if (value is null) return -1;
        for (var i = 0; i < values.Count; i++)
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static void FillOptional(ComboBox box, IReadOnlyList<string> values, string? chosen)
    {
        box.ItemsSource = new[] { Say("recorder.advanced.none") }.Concat(values).ToList();
        box.SelectedIndex = IndexOf(values, chosen) + 1;
    }

    private void ApplyRateRows()
    {
        if (_fillingAdvanced) return;
        var bitrate = SelectedRateControl == RecorderRateControl.Bitrate;
        TxtBitrate.IsEnabled = bitrate;
        TxtMaxBitrate.IsEnabled = bitrate;
        TxtBuffer.IsEnabled = bitrate;
    }

    private bool Whole(TextBox box, string labelKey, int fallback, bool report, Action<int> store)
    {
        var value = fallback;
        if (string.IsNullOrWhiteSpace(box.Text)
            || (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0))
        {
            store(value);
            return true;
        }

        if (report) ShowError(Say("recorder.error.number", Say(labelKey)));
        return !report;
    }

    private bool Real(TextBox box, string labelKey, bool signed, bool report, Action<double> store)
    {
        var value = 0d;
        if (string.IsNullOrWhiteSpace(box.Text)
            || ((double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                && double.IsFinite(value) && (signed || value >= 0)))
        {
            store(value);
            return true;
        }

        if (report) ShowError(Say("recorder.error.number", Say(labelKey)));
        return !report;
    }

    internal bool ReadAdvanced(bool report = true)
    {
        if (report && !AdvancedMode) return true;

        if (!Whole(TxtScaleWidth, "recorder.advanced.scale-width", 0, report, v => _settings.ScaleWidth = v)
            || !Whole(TxtScaleHeight, "recorder.advanced.scale-height", 0, report, v => _settings.ScaleHeight = v)
            || !Whole(TxtKeyframe, "recorder.advanced.keyframe", RecorderArguments.DefaultKeyframeSeconds, report, v => _settings.KeyframeSeconds = v)
            || !Whole(TxtBitrate, "recorder.advanced.bitrate", 0, report, v => _settings.BitrateKbps = v)
            || !Whole(TxtMaxBitrate, "recorder.advanced.max-bitrate", 0, report, v => _settings.MaxBitrateKbps = v)
            || !Whole(TxtBuffer, "recorder.advanced.buffer", 0, report, v => _settings.BufferKbits = v)
            || !Real(TxtMaxDuration, "recorder.advanced.max-duration", false, report, v => _settings.MaxDurationSeconds = v)
            || !Real(TxtSplitSeconds, "recorder.advanced.split-seconds", false, report, v => _settings.SplitSeconds = v)
            || !Real(TxtSplitMegabytes, "recorder.advanced.split-megabytes", false, report, v => _settings.SplitMegabytes = v)
            || !Real(TxtAudioGain, "recorder.advanced.audio-gain", true, report, v => _settings.AudioGainDb = v))
            return false;

        _settings.Container = SelectedContainer;
        _settings.Profile = SelectedProfile;
        _settings.Tune = SelectedTune;
        _settings.RateControl = SelectedRateControl;
        _settings.PixelFormat = SelectedPixelFormat;
        _settings.ColorSpace = SelectedColorSpace;
        _settings.ColorRange = SelectedColorRange;
        _settings.AudioLayout = SelectedAudioLayout;
        _settings.AudioNoiseGate = ChkNoiseGate.IsChecked ?? false;
        _settings.AudioNoiseSuppression = ChkNoiseSuppression.IsChecked ?? false;
        return true;
    }

    internal static RecorderRequest FitToCodec(RecorderRequest request)
    {
        var formats = RecorderArguments.PixelFormatsFor(request.VideoCodec);
        var pixel = formats.Count == 0 || IndexOf(formats, request.PixelFormat) >= 0
            ? request.PixelFormat
            : IndexOf(formats, RecorderArguments.DefaultPixelFormat) >= 0 ? RecorderArguments.DefaultPixelFormat : formats[0];

        return request with
        {
            Profile = IndexOf(RecorderArguments.ProfilesFor(request.VideoCodec), request.Profile) >= 0 ? request.Profile : null,
            Tune = IndexOf(RecorderArguments.TunesFor(request.VideoCodec), request.Tune) >= 0 ? request.Tune : null,
            PixelFormat = pixel
        };
    }
}
