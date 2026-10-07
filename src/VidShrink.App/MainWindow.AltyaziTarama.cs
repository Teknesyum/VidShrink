using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// CLI'daki <c>--altyazi-tara</c>'nın arayüzdeki karşılığı: düğme <see cref="ForeignAudioSearch"/>'ün
/// aynı iki adımını koşar (önce <c>forced</c> bayrağı, yoksa paket sayımı) ve bulunan izi yakma
/// listesinde seçer. Karar burada verilmez; iz seçilmeyen her sonuç bir satırla söylenir. Sayım
/// sürerken düğme iptale döner; yeni kaynak, başlık değişimi ve elle seçim taramayı düşürür.
/// </summary>
public partial class MainWindow
{
    private CancellationTokenSource? _taramaCts;

    internal Func<MediaInfo, CancellationToken, Task<IReadOnlyDictionary<int, int>?>> CountSubtitlePackets { get; set; }
        = (info, ct) => FfprobeClient.CountSubtitlePacketsAsync(info.FilePath, ct);

    internal bool SubtitleScanRunning => _taramaCts is not null;

    internal string? SubtitleScanStatus => TxtSubtitleScan.IsVisible ? TxtSubtitleScan.Text : null;

    private void TaramayiSifirla()
    {
        _taramaCts?.Cancel();
        _taramaCts = null;
        TaramaDurumu(null);
        TaramaDugmesi();
    }

    private void TaramaDurumu(string? metin)
    {
        TxtSubtitleScan.Text = metin ?? "";
        TxtSubtitleScan.IsVisible = !string.IsNullOrEmpty(metin);
    }

    private void TaramaDugmesi()
    {
        var ad = Say(SubtitleScanRunning ? "main.action.cancel" : "main.subtitles.scan");
        BtnScanSubtitle.Content = ad;
        AutomationProperties.SetName(BtnScanSubtitle, ad);
        BtnScanSubtitle.IsEnabled = _burnChoices.Count > 0;
    }

    /// <summary>
    /// Bayraklı iz sayımsız seçilir; yakılabilir altyazı yoksa sayım hiç başlamaz. Sayım arka planda
    /// koşar: dönüşte tarama düşürülmüşse (iptal, yeni kaynak, elle seçim) sonucu yazılmaz.
    /// </summary>
    internal async Task ScanSubtitlesAsync()
    {
        if (_info is not { } info) return;
        var dil = Strings.Language;
        var karar = ForeignAudioSearch.Flagged(info, dil) ?? ForeignAudioSearch.Decide(info, null, dil);
        if (karar.Outcome == ForeignAudioOutcome.NotMeasured)
        {
            _taramaCts?.Cancel();
            var cts = new CancellationTokenSource();
            _taramaCts = cts;
            TaramaDurumu(Say("main.subtitles.scan.running"));
            TaramaDugmesi();
            IReadOnlyDictionary<int, int>? paketler = null;
            try { paketler = await CountSubtitlePackets(info, cts.Token); }
            catch (Exception) { }
            var gecerli = ReferenceEquals(_taramaCts, cts);
            cts.Dispose();
            if (!gecerli) return;
            _taramaCts = null;
            TaramaDugmesi();
            karar = ForeignAudioSearch.Decide(info, paketler, dil);
        }

        var sira = karar.Number is int numara ? _burnChoices.IndexOf(numara - 1) : -1;
        if (sira >= 0) CmbBurnSubtitle.SelectedIndex = sira + 1;
        TaramaDurumu(Say("main.subtitles.scan." + karar.Slug, karar.Number, karar.Packets, karar.FullestPackets));
    }

    private void OnBurnChoiceChanged()
    {
        TaramayiSifirla();
        OnOptionChanged();
    }

    private async void OnScanSubtitle(object? sender, RoutedEventArgs e)
    {
        if (SubtitleScanRunning)
        {
            TaramayiSifirla();
            return;
        }
        await ScanSubtitlesAsync();
    }
}
