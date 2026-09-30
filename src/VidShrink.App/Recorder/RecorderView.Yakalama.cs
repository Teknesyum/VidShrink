using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Windows'ta yakalama yolunun secimi. Once <c>ddagrab</c> (Desktop Duplication) denenir:
/// cikislar DXGI'dan okunur, pencere hedefi istemci alanina cevrilir, ffmpeg tek karelik
/// yoklamayla kaynagi acar. Yerlesim engeli ya da gecmeyen yoklama <c>gdigrab</c>'a dusurur
/// ve dusus ekranda bir satir olarak soylenir. Yoklama gecince otomatik kipin 30 kare tavani
/// kalkar (<see cref="Machine"/>).
/// </summary>
internal partial class RecorderView
{
    internal Func<IReadOnlyList<DdaOutput>> DdaOutputSource { get; set; } = DdaOutputs.Enumerate;

    internal Func<RecorderRequest, Task<bool>> DdagrabCheck { get; set; } = request => DdagrabProbe.WorksAsync(request);

    /// <summary>Son yoklamanin sonucu; yoklama kosmadiysa <c>null</c>.</summary>
    internal bool? DdagrabWorks { get; set; }

    internal async Task<RecorderRequest> ChooseCaptureAsync(RecorderRequest request, bool notify = true)
    {
        if (request.Platform != RecorderPlatform.Windows) return request;

        var candidate = request with
        {
            DdaOutputs = DdaOutputSource(),
            WindowRegion = request.Target == RecorderTargetKind.Window && !string.IsNullOrWhiteSpace(request.WindowTitle)
                           && WindowRect(request.WindowTitle) is { } window
                ? new RecorderRegion(window.X, window.Y, window.Width, window.Height)
                : request.WindowRegion
        };

        var works = false;
        if (RecorderArguments.DdagrabBlocker(candidate) == DdagrabFallback.None)
        {
            try { works = await DdagrabCheck(candidate with { Capture = RecorderCapture.Ddagrab }); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                works = false;
            }

            if (DdagrabWorks != works) _guess = null;
            DdagrabWorks = works;
        }

        var chosen = RecorderArguments.ChooseCapture(candidate, works, out var reason);
        if (notify && reason != DdagrabFallback.None) ShowNotice(Say(FallbackKey(reason)));
        return chosen;
    }

    internal static string FallbackKey(DdagrabFallback reason) => reason switch
    {
        DdagrabFallback.Unavailable or DdagrabFallback.NoOutputs => "recorder.capture.fallback-unavailable",
        DdagrabFallback.OutsideOneOutput or DdagrabFallback.NoWindowRect => "recorder.capture.fallback-layout",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}
