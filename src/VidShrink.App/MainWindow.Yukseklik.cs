using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Interactivity;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// ARC-P1: kaynağın yüksekliği <c>ebur128</c> ile kaynak başına bir kez ölçülür ve LUFS ile gerçek tepe olarak
/// ses bölümünde yazılır. Ölçüm sürerken düğme iptale döner; sonuç gelince düğme kapanır, yeni kaynak sonucu siler.
/// Ölçülemeyen yükseklik bir satırla söylenir ve düğme yeniden denemeye açık kalır.
/// </summary>
public partial class MainWindow
{
    private CancellationTokenSource? _yukseklikCts;
    private LoudnessReport? _yukseklik;
    private bool _yukseklikOlculemedi;

    internal Func<MediaInfo, CancellationToken, Task<LoudnessReport?>> MeasureLoudness { get; set; }
        = (info, ct) => LoudnessProbe.RunAsync(info.FilePath, ct);

    internal bool LoudnessRunning => _yukseklikCts is not null;

    internal string? LoudnessStatus => TxtLoudness.IsVisible ? TxtLoudness.Text : null;

    private void YuksekligiSifirla()
    {
        _yukseklikCts?.Cancel();
        _yukseklikCts = null;
        _yukseklik = null;
        _yukseklikOlculemedi = false;
        YukseklikDurumu();
    }

    private void YukseklikDurumu()
    {
        var metin = LoudnessRunning ? Say("main.audio.loudness.running")
            : _yukseklik is { } rapor ? Say("main.audio.loudness.result", Desibel(rapor.IntegratedLufs), Desibel(rapor.TruePeakDbfs))
            : _yukseklikOlculemedi ? Say("main.audio.loudness.failed")
            : null;
        TxtLoudness.Text = metin ?? "";
        TxtLoudness.IsVisible = !string.IsNullOrEmpty(metin);
        var ad = Say(LoudnessRunning ? "main.action.cancel" : "main.audio.loudness.measure");
        BtnMeasureLoudness.Content = ad;
        AutomationProperties.SetName(BtnMeasureLoudness, ad);
        BtnMeasureLoudness.IsEnabled = _info is { HasAudio: true } && _yukseklik is null;
    }

    private static string Desibel(double? deger)
        => deger is double d ? d.ToString("0.0", CultureInfo.InvariantCulture) : "-inf";

    /// <summary>Ölçüm arka planda koşar; dönüşte ölçüm düşürülmüşse (iptal, yeni kaynak) sonucu yazılmaz.</summary>
    internal async Task MeasureLoudnessAsync()
    {
        if (_info is not { HasAudio: true } info || _yukseklik is not null || LoudnessRunning) return;
        var cts = new CancellationTokenSource();
        _yukseklikCts = cts;
        _yukseklikOlculemedi = false;
        YukseklikDurumu();
        LoudnessReport? rapor = null;
        try { rapor = await MeasureLoudness(info, cts.Token); }
        catch (Exception) { }
        var gecerli = ReferenceEquals(_yukseklikCts, cts);
        cts.Dispose();
        if (!gecerli) return;
        _yukseklikCts = null;
        _yukseklik = rapor;
        _yukseklikOlculemedi = rapor is null;
        YukseklikDurumu();
    }

    private async void OnMeasureLoudness(object? sender, RoutedEventArgs e)
    {
        if (LoudnessRunning)
        {
            _yukseklikCts?.Cancel();
            _yukseklikCts = null;
            YukseklikDurumu();
            return;
        }
        await MeasureLoudnessAsync();
    }
}
