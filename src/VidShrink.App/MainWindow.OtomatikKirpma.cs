using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private string? _kirpmaYolu;
    private Task<CropRect?>? _kirpmaIsi;
    private CancellationTokenSource? _kirpmaCts;

    private bool OtomatikKirpmaAcik => ChkFltAutoCrop.IsChecked == true;

    /// <summary>
    /// Kutu açıkken kaynak başına bir kez yoklar ve sonucu önbelleğe alır; yoklama bitince plan
    /// yeniden hesaplanır. Kutu kapalıyken ya da kaynak yokken hiçbir şey koşmaz.
    /// </summary>
    private Task<CropRect?>? KirpmaYoklamasiniBaslat()
    {
        if (!OtomatikKirpmaAcik || _info is not { } info) return null;
        if (_kirpmaIsi is not null && _kirpmaYolu == info.FilePath) return _kirpmaIsi;

        _kirpmaCts?.Cancel();
        var cts = new CancellationTokenSource();
        _kirpmaCts = cts;
        _kirpmaYolu = info.FilePath;
        var is_ = OtomatikKirpma.BulAsync(info, cts.Token);
        _kirpmaIsi = is_;
        _ = KirpmaBitinceAsync(info.FilePath, is_);
        return is_;
    }

    private async Task KirpmaBitinceAsync(string yol, Task<CropRect?> is_)
    {
        try { await is_; }
        catch (OperationCanceledException) { return; }
        if (_kirpmaIsi != is_ || _info?.FilePath != yol || !OtomatikKirpmaAcik) return;
        Recalculate();
        RefreshAdvancedHints();
    }

    /// <summary>Yüklü kaynağın biten yoklamasının dikdörtgeni; yoklama sürüyorsa ya da bant yoksa <c>null</c>.</summary>
    private CropRect? KirpmaSonucu()
        => _info is { } info && _kirpmaYolu == info.FilePath && _kirpmaIsi is { IsCompletedSuccessfully: true } is_
            ? is_.Result
            : null;

    /// <summary>Küçültme başlarken yoklama bitmemişse beklenir; plan onun sonucuyla yeniden kurulur.</summary>
    private async Task KirpmaHazirAsync()
    {
        if (KirpmaYoklamasiniBaslat() is not { IsCompleted: false } is_) return;
        try { await is_; }
        catch (OperationCanceledException) { return; }
        Recalculate();
    }

    private void OnAutoCropChanged()
    {
        KirpmaYoklamasiniBaslat();
        OnOptionChanged();
    }

    private string? KirpmaGerekcesi(EncodePlan plan)
        => OtomatikKirpmaAcik && SuzgecOku(out _).Crop is null && plan.Filters?.Crop is { } kirpma && _info is { } kaynak
            ? Say("main.reason.auto-crop", kirpma.Width, kirpma.Height, kaynak.Width, kaynak.Height)
            : null;
}
