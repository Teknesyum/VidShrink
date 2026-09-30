using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// <c>ddagrab</c>'in bu makinede ve bu istekte acilip acilmadigi. Tek karelik kisa kosum
/// (<see cref="RecorderArguments.BuildDdagrabProbe"/>); sure dolarsa surec oldurulur ve
/// sonuc "acilmadi" sayilir. Yerlesim engeli varsa surec hic baslatilmaz.
/// </summary>
public static class DdagrabProbe
{
    /// <summary>Yoklamanin bekledigi en uzun sure.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static async Task<bool> WorksAsync(RecorderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (RecorderArguments.DdagrabBlocker(request) != DdagrabFallback.None) return false;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try
        {
            var run = await FfmpegRunner.RunAsync(RecorderArguments.BuildDdagrabProbe(request), limit.Token);
            return run.Ok;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
