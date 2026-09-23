using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// HB #36: "Siyah bantları kırp" onay kutusunun motoru. Yoklama <see cref="CropProbe"/>'tur;
/// testler <see cref="Tespit"/>'i sahte bir yoklamayla değiştirir, gerçek ffmpeg gerekmez.
/// Ana pencere ve kuyruk aynı <see cref="Uygula"/> kuralını kullanır.
/// </summary>
internal static class OtomatikKirpma
{
    internal static Func<MediaInfo, CancellationToken, Task<CropDetection>> Tespit { get; set; } = CropProbe.RunAsync;

    /// <summary>
    /// Yoklamayı koşar; yoklama başarısızsa (ffmpeg yok, dosya okunmuyor) bant yok sayılır ve
    /// küçültme kırpmasız sürer. İptal yukarı taşınır.
    /// </summary>
    internal static async Task<CropRect?> BulAsync(MediaInfo info, CancellationToken ct)
    {
        try
        {
            return (await Tespit(info, ct)).Rect;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Bulunan dikdörtgen yalnız kutu açıkken ve kullanıcı belirtimde kendi <c>crop=</c>'unu
    /// yazmamışken süzgece girer; CLI'daki <c>--kirp</c> kuralıyla aynı.
    /// </summary>
    internal static VideoFilterOptions Uygula(VideoFilterOptions filters, bool acik, CropRect? bulunan)
        => acik && filters.Crop is null && bulunan is { } rect ? filters.WithCrop(rect) : filters;
}
