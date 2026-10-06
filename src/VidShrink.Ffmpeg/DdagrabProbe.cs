using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// <c>ddagrab</c>'in bu makinede ve bu istekte acilip acilmadigi. Tek karelik kisa kosum
/// (<see cref="RecorderArguments.BuildDdagrabProbe"/>); sure dolarsa surec oldurulur ve
/// sonuc "acilmadi" sayilir. Yerlesim engeli varsa surec hic baslatilmaz. Masaustu hic
/// kare vermezse yoklama kendiliginden bitmiyor ve bir cekirdegi donduruyor; zamanlayici
/// bu surecle birlikte oldugu icin surec <see cref="ChildJob"/>'a da baglanir.
/// </summary>
public static class DdagrabProbe
{
    /// <summary>Yoklamanin bekledigi en uzun sure.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Dogruyken <see cref="WorksAsync"/> surec acmaz. Test konagi acilista kurar.</summary>
    internal static bool Disabled { get; set; }

    public static Task<bool> WorksAsync(RecorderRequest request, CancellationToken ct = default)
        => Disabled ? Task.FromResult(false) : ProbeAsync(request, ct);

    internal static async Task<bool> ProbeAsync(RecorderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (RecorderArguments.DdagrabBlocker(request) != DdagrabFallback.None) return false;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try
        {
            var run = await FfmpegRunner.RunAsync(RecorderArguments.BuildDdagrabProbe(request), true, limit.Token);
            return run.Ok;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
