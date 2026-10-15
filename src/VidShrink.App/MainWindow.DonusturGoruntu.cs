using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private Task<CropRect?>? _donusturKirpmaBekleyen;

    private bool DonusturKirpmaAcik => ChkConvertAutoCrop.IsChecked == true;

    /// <summary>ARC-P4: ffmpeg yapısında <c>minterpolate</c> yoksa ara kare kutusu gizlenir; yoklama gelmeden görünür.</summary>
    internal bool InterpolateAvailable => _encoders is not VidShrink.Ffmpeg.EncoderCapabilities { Loaded: true } loaded
        || loaded.HasFilter(VideoFilterChain.InterpolateFilterName);

    private void RefreshConvertInterpolate()
    {
        var available = InterpolateAvailable;
        ChkConvertInterpolate.IsVisible = available;
        if (!available) ChkConvertInterpolate.IsChecked = false;
    }

    private void InitializeConvertFilterUi()
    {
        foreach (var box in new[] { CmbConvertDeinterlace, CmbConvertDenoise, CmbConvertRotate, CmbConvertScaler, CmbConvertFpsMode })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnConvertChanged);
        Watch(ChkConvertAutoCrop, ToggleButton.IsCheckedProperty, OnConvertChanged);
        Watch(ChkConvertInterpolate, ToggleButton.IsCheckedProperty, OnConvertChanged);
        Watch(ChkConvertLetterbox, ToggleButton.IsCheckedProperty, OnConvertChanged);
    }

    /// <summary>
    /// DON-P10: Dönüştür'ün süzgeç kutuları Küçült'ün <see cref="VideoFilterOptions"/>'ına iner; tarama
    /// çözme Dönüştür'de kapalı başlar. Otomatik kırpma Gelişmiş'in yoklama önbelleğini paylaşır ve
    /// yalnız biten yoklamanın dikdörtgenini yazar.
    /// </summary>
    private VideoFilterOptions DonusturSuzgecleri()
    {
        var gurultu = SelectedTag(CmbConvertDenoise);
        var filters = ConversionPlan.DefaultFilters with
        {
            Deinterlace = SelectedTag(CmbConvertDeinterlace) switch
            {
                "auto" => DeinterlaceMode.Auto,
                "on" => DeinterlaceMode.On,
                _ => DeinterlaceMode.Off
            },
            Denoise = gurultu is null or "off" ? DenoiseFilter.Off : DenoiseFilter.Hqdn3d,
            DenoiseStrength = gurultu switch
            {
                "light" => FilterStrength.Light,
                "strong" => FilterStrength.Strong,
                _ => FilterStrength.Medium
            },
            Transpose = SelectedTag(CmbConvertRotate) switch
            {
                "clock" => TransposeMode.Clockwise,
                "cclock" => TransposeMode.CounterClockwise,
                "180" => TransposeMode.UpsideDown,
                "hflip" => TransposeMode.FlipHorizontal,
                "vflip" => TransposeMode.FlipVertical,
                _ => TransposeMode.None
            },
            Scaler = SelectedTag(CmbConvertScaler) switch
            {
                "bicubic" => ScaleAlgorithm.Bicubic,
                "bilinear" => ScaleAlgorithm.Bilinear,
                "spline" => ScaleAlgorithm.Spline,
                "neighbor" => ScaleAlgorithm.Neighbor,
                _ => ScaleAlgorithm.Lanczos
            }
        };
        return OtomatikKirpma.Uygula(filters, DonusturKirpmaAcik && _sequence is null, KirpmaSonucu());
    }

    private void OnConvertFiltersToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConvertFilterBody.IsVisible = !ConvertFilterBody.IsVisible;
        ConvertFiltersGlyph.Classes.Set("open", ConvertFilterBody.IsVisible);
    }

    private FrameRateMode DonusturKareHiziKipi() => SelectedTag(CmbConvertFpsMode) switch
    {
        "constant" => FrameRateMode.Constant,
        "variable" => FrameRateMode.Peak,
        _ => FrameRateMode.Auto
    };

    /// <summary>
    /// Kutu açıkken yüklü kaynağı bir kez yoklar (Gelişmiş'in önbelleğiyle aynı); yoklama sürüyorsa
    /// bitince Dönüştür yeniden hesaplanır. Görüntü dizisi ve ses kaynağı yoklanmaz.
    /// </summary>
    private void DonusturKirpmaYoklamasi()
    {
        if (!DonusturKirpmaAcik || _sequence is not null || _info is not { } info) return;
        var is_ = _kirpmaIsi is not null && _kirpmaYolu == info.FilePath ? _kirpmaIsi : KirpmaIsiniBaslat(info);
        if (is_.IsCompleted || _donusturKirpmaBekleyen == is_) return;
        _donusturKirpmaBekleyen = is_;
        _ = DonusturKirpmaBitinceAsync(info.FilePath, is_);
    }

    private async Task DonusturKirpmaBitinceAsync(string yol, Task<CropRect?> is_)
    {
        try { await is_; }
        catch (OperationCanceledException) { return; }
        if (_kirpmaIsi != is_ || _info?.FilePath != yol || !DonusturKirpmaAcik) return;
        RefreshConversion();
    }
}
