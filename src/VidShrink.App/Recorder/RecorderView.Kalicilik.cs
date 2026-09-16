using System;
using Avalonia.Controls;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private bool _persistReady;

    private int _quiet;

    private void InitKalicilik()
    {
        foreach (var box in new[]
                 {
                     TxtFps, TxtQuality, TxtWindowTitle, TxtRegionX, TxtRegionY, TxtRegionWidth, TxtRegionHeight,
                     TxtOutputFolder, TxtScaleWidth, TxtScaleHeight, TxtKeyframe, TxtBitrate, TxtMaxBitrate, TxtBuffer,
                     TxtMaxDuration, TxtSplitSeconds, TxtSplitMegabytes, TxtAudioGain
                 })
            box.TextChanged += (_, _) => PersistChoices();

        foreach (var box in new[]
                 {
                     CmbTarget, CmbCodec, CmbPreset, CmbContainer, CmbProfile, CmbTune, CmbPixelFormat, CmbRateControl,
                     CmbColorSpace, CmbColorRange, CmbAudioLayout, CmbMicrophone, CmbSystemAudio
                 })
            box.SelectionChanged += (_, _) => PersistChoices();

        foreach (var check in new[] { ChkCursor, ChkOpenFolder, ChkNoiseGate, ChkNoiseSuppression })
            check.IsCheckedChanged += (_, _) => PersistChoices();

        _persistReady = true;
    }

    private void PersistChoices()
    {
        if (!_persistReady || _quiet > 0 || _fillingAdvanced || _session is not null) return;
        StoreChoices();
    }

    private void Quietly(Action refresh)
    {
        _quiet++;
        try { refresh(); }
        finally { _quiet--; }
    }
}
