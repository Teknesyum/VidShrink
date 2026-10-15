using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.App.Localization;
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
        else if (notify && RecorderArguments.HdrLost(chosen)) ShowNotice(Say("recorder.capture.hdr-lost"));
        return chosen;
    }

    internal static string FallbackKey(DdagrabFallback reason) => reason switch
    {
        DdagrabFallback.Unavailable or DdagrabFallback.NoOutputs => "recorder.capture.fallback-unavailable",
        DdagrabFallback.OutsideOneOutput or DdagrabFallback.NoWindowRect => "recorder.capture.fallback-layout",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    internal Func<string, long?> FreeSpace { get; set; } =
        folder => DiskSpaceGuard.TryGetFreeBytes(folder, out var free) ? free : null;

    internal Func<DateTime> DiskClock { get; set; } = () => DateTime.UtcNow;

    private string? _diskFolder;
    private DateTime _nextDiskCheck;

    internal string DiskWarningText => DiskRow.IsVisible ? TxtDisk.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// EKA-11: kayit klasorunun diskinde bos alan kaydedici esiginin altindaysa durum satirinda
    /// uyari gosterir, degilse satiri gizler. Okunamayan disk uyari uretmez.
    /// </summary>
    internal bool CheckDisk(string folder)
    {
        var free = FreeSpace(folder);
        var low = free is { } bytes && DiskSpaceGuard.IsLowForRecording(bytes);
        TxtDisk.Text = low
            ? Say("recorder.disk.low", Bicim.Boyut.Mb(Math.Max(0, free!.Value) / Megabayt.Bayt, Strings.Culture))
            : string.Empty;
        DiskRow.IsVisible = low;
        return low;
    }

    /// <summary>Kayit baslarken diski bir kez okur ve sonraki okumayi araliga kurar.</summary>
    internal void BeginDiskWatch(string folder)
    {
        _diskFolder = folder;
        CheckDisk(folder);
        _nextDiskCheck = DiskClock() + DiskSpaceGuard.RecorderCheckInterval;
    }

    /// <summary>Ilerleme her geldiginde cagrilir; disk yalniz aralik dolunca yeniden okunur.</summary>
    internal void CheckDiskWhileRecording()
    {
        if (_diskFolder is null) return;
        var now = DiskClock();
        if (now < _nextDiskCheck) return;
        _nextDiskCheck = now + DiskSpaceGuard.RecorderCheckInterval;
        CheckDisk(_diskFolder);
    }

    internal Func<string, IReadOnlyList<string>> OrphanSource { get; set; } = RecorderSession.FindOrphanCaptures;

    private IReadOnlyList<string> _orphans = Array.Empty<string>();

    internal bool RecoverVisible => RecoverRow.IsVisible;

    internal string RecoverText => RecoverRow.IsVisible ? TxtRecover.Text ?? string.Empty : string.Empty;

    /// <summary>EKA-20: cikti klasorunde teslim edilmemis yakalama arar, varsa kurtarmayi onerir.</summary>
    internal void ScanOrphans(string folder)
    {
        IReadOnlyList<string> found;
        try { found = OrphanSource(folder); }
        catch (IOException) { found = Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { found = Array.Empty<string>(); }
        ShowOrphans(found);
    }

    private void ShowOrphans(IReadOnlyList<string> found)
    {
        _orphans = found;
        TxtRecover.Text = found.Count == 0
            ? string.Empty
            : Say("recorder.recover.found", string.Join(", ", found.Select(Path.GetFileName)));
        RecoverRow.IsVisible = found.Count > 0;
    }

    /// <summary>
    /// Bulunan yakalamalari ayardaki kaba aktarir. Kurtarilan hedefler bildirimde, kurtarilamayan
    /// yakalamalar hata satirinda soylenir ve oneride kalir.
    /// </summary>
    internal async Task<IReadOnlyList<string>> RecoverAsync(Func<IReadOnlyList<string>, Task<bool>> run)
    {
        var recovered = new List<string>();
        var failed = new List<string>();
        foreach (var capture in _orphans)
        {
            var target = RecorderArguments.RecoveryTarget(capture, _settings.Container, File.Exists);
            if (await RecorderSession.RecoverAsync(capture, target, run)) recovered.Add(target);
            else failed.Add(capture);
        }

        if (recovered.Count > 0) ShowNotice(Say("recorder.recover.done", string.Join(", ", recovered)));
        if (failed.Count > 0) ShowError(Say("recorder.recover.failed", string.Join(", ", failed.Select(Path.GetFileName))));
        ShowOrphans(failed);
        return recovered;
    }

    private async void OnRecover(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnRecover.IsEnabled = false;
        try
        {
            await RecoverAsync(async args => (await FfmpegRunner.RunAsync(args)).Ok);
        }
        finally
        {
            BtnRecover.IsEnabled = true;
        }
    }
}
