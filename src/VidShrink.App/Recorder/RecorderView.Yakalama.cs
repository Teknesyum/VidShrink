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

    /// <summary>`zscale` var mi; ffmpeg listesi okunmadiysa var sayilir, kodlayici acilmazsa kayit hatayla doner.</summary>
    internal Func<bool> TonemapCheck { get; set; } =
        () => !EncoderCapabilities.Instance.Loaded || EncoderCapabilities.Instance.HasFilter("zscale");

    /// <summary>HDR korunurken H.264 yerine denenecek HEVC kodlayicisi bu makinede aciliyor mu.</summary>
    internal Func<string, Task<bool>> HevcCheck { get; set; } =
        codec => Task.Run(() => EncoderCapabilities.Instance.WorksAsEncoder(codec));

    internal async Task<RecorderRequest> ChooseCaptureAsync(RecorderRequest request, bool notify = true)
    {
        if (request.Platform != RecorderPlatform.Windows) return request;

        var candidate = request with
        {
            DdaOutputs = DdaOutputSource(),
            CanTonemap = TonemapCheck(),
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
        var outcome = HdrCodecOutcome.Unchanged;
        if (RecorderArguments.HdrCapture(chosen))
        {
            string? hevc = null;
            foreach (var codec in RecorderArguments.HdrHevcOrder(chosen.VideoCodec))
            {
                bool ok;
                try { ok = await HevcCheck(codec); }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    ok = false;
                }
                if (!ok) continue;
                hevc = codec;
                break;
            }
            chosen = RecorderArguments.WithHdrCodec(chosen, codec => codec == hevc, out outcome);
        }

        if (notify && CaptureNotice(chosen, reason, outcome) is { } notice) ShowNotice(notice);
        return chosen;
    }

    /// <summary>
    /// Yakalama seciminin tek bildirim satiri. Satir tek oldugu icin ayni kayitta iki durum
    /// birlesik metinle soylenir: HDR ekranda GDI'ye dusus, dusus metninden once gelir cunku
    /// bozuk renk kare kaybindan agirdir.
    /// </summary>
    internal string? CaptureNotice(RecorderRequest chosen, DdagrabFallback reason, HdrCodecOutcome outcome)
    {
        if (reason != DdagrabFallback.None)
            return RecorderArguments.HdrOnGdigrab(chosen) ? Say("recorder.capture.fallback-hdr") : Say(FallbackKey(reason));
        if (outcome == HdrCodecOutcome.NoHevc) return Say("recorder.capture.hdr-no-hevc");
        if (RecorderArguments.HdrLost(chosen)) return Say("recorder.capture.hdr-lost");
        if (RecorderArguments.HdrTonemapMissing(chosen)) return Say("recorder.capture.hdr-no-tonemap");
        if (RecorderArguments.SdrWhiteAssumed(chosen))
            return Say("recorder.capture.sdr-white-assumed", RecorderArguments.DefaultSdrWhiteNits.ToString("0", Strings.Culture));
        if (outcome == HdrCodecOutcome.SwitchedToHevc) return Say("recorder.capture.hdr-kept-hevc", chosen.VideoCodec);
        if (RecorderArguments.HdrCapture(chosen)) return Say("recorder.capture.hdr-kept");
        return null;
    }

    /// <summary>
    /// Kayit tani gunlugunun klasoru. Ayar dosyasi verilmis gorunumde onun yanindaki
    /// <c>gunluk</c> klasoru, verilmemisse uygulamanin gunluk klasoru; testler buradan yonlendirir.
    /// </summary>
    internal Func<string> DiagnosticFolder { get; set; }

    /// <summary>Son yazilan tani gunlugunun yolu; yazilamadiysa <c>null</c>.</summary>
    internal string? LastDiagnosticPath { get; private set; }

    private string DefaultDiagnosticFolder()
    {
        var folder = string.IsNullOrEmpty(_settingsPath) ? null : Path.GetDirectoryName(_settingsPath);
        return string.IsNullOrEmpty(folder) ? Gunluk.Klasor : Path.Combine(folder, Gunluk.KlasorAdi);
    }

    internal void WriteDiagnostic(RecorderRequest request, IReadOnlyList<string>? args, string? error)
    {
        var version = UpdateCheck.CurrentVersion();
        var text = KayitTanisi.Metin(request, args, version, DateTimeOffset.Now, error);
        var folder = DiagnosticFolder();
        LastDiagnosticPath = KayitTanisi.Yaz(folder, text);
        if (RecorderArguments.SdrWhiteAssumed(request))
            Gunluk.Yaz(folder, Gunluk.Blok(RecorderArguments.SdrWhiteAssumedLogLine,
                version, "-", 0, TimeSpan.Zero, Array.Empty<string>()));
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

    internal bool RecoverVisible => RecoverPanel.IsVisible;

    internal string RecoverText => RecoverPanel.IsVisible ? TxtRecover.Text ?? string.Empty : string.Empty;

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
        RecoverPanel.IsVisible = found.Count > 0;
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
