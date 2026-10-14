using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// ARC-01 ve ARC-05: medya bilgisi penceresi. Üst bölüm ffprobe'un okuduğu her izi ve bölümü
/// <see cref="MediaReport"/> ile döker; alt bölüm düzenleyicinin sessizlik, siyah kare ve sahne
/// taramasını aynı tarayıcılarla çağırıp <see cref="AuditReport"/> listesini verir. "Kopyala" ve
/// "metin olarak kaydet" ikisini birlikte yazar. Bilgi verilmezse açılışta yoklanır.
/// </summary>
internal sealed partial class MediaInfoWindow : Window
{
    private readonly string _path;
    private MediaInfo? _info;
    private CancellationTokenSource? _scanCts;
    private IReadOnlyList<AuditEntry>? _audit;

    public MediaInfoWindow() : this(string.Empty, null)
    {
    }

    internal MediaInfoWindow(string path, MediaInfo? info)
    {
        _path = path;
        InitializeComponent();
        TxtFile.Text = Path.GetFileName(path);
        BtnScan.Click += (_, _) => _ = ScanAsync();
        BtnScanCancel.Click += (_, _) => _scanCts?.Cancel();
        BtnCopy.Click += (_, _) => _ = CopyAsync();
        BtnSave.Click += (_, _) => _ = SaveAsync();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
        Closed += (_, _) => _scanCts?.Cancel();
        if (info is not null) Show(info);
        else TxtDetails.Text = Strings.Get("mediainfo.loading");
    }

    internal Func<string, CancellationToken, Task<MediaInfo>> Prober { get; set; } = (path, ct) => FfprobeClient.ProbeAsync(path, ct);

    internal Func<string, SilenceCutOptions, double, IProgress<double>, CancellationToken, Task<SilenceScan>> SilenceScanner { get; set; } = DefaultSilenceScan;

    internal Func<string, double, IProgress<double>, CancellationToken, Task<IReadOnlyList<double>>> SceneScanner { get; set; } = DefaultSceneScan;

    internal Func<string, Task<string?>>? SavePicker { get; set; }

    internal MediaInfo? Info => _info;

    internal IReadOnlyList<AuditEntry>? Audit => _audit;

    internal string DetailsText => TxtDetails.Text ?? string.Empty;

    internal string AuditText => TxtAudit.Text ?? string.Empty;

    internal string StatusText => TxtStatus.Text ?? string.Empty;

    internal bool Scanning => _scanCts is not null;

    /// <summary>Bilgi verilmeden açıldıysa dosyayı yoklar; yoklama düşerse nedeni dökümün yerine yazılır.</summary>
    internal async Task<bool> LoadAsync()
    {
        if (_info is not null) return true;
        try
        {
            Show(await Prober(_path, CancellationToken.None).ConfigureAwait(true));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            TxtDetails.Text = Strings.Get("mediainfo.failed", ex.Message);
            return false;
        }
    }

    internal static string Heading(ReportSection section)
        => Strings.Get("mediainfo.section." + section.Key, section.Number.ToString(Strings.Culture));

    internal static string Label(string key) => Strings.Get("mediainfo.field." + key);

    internal static string KindName(AuditKind kind) => Strings.Get(kind switch
    {
        AuditKind.Silence => "mediainfo.audit.silence",
        AuditKind.Black => "mediainfo.audit.black",
        AuditKind.Scene => "mediainfo.audit.scene",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    });

    internal static string DetailsOf(MediaInfo info)
        => MediaReport.Text(MediaReport.Build(info, Strings.Culture), Heading, Label);

    /// <summary>Kopyalanan ve kaydedilen metin: döküm, tarandıysa ardından denetim listesi.</summary>
    internal string ReportText()
    {
        var text = new StringBuilder(DetailsText.TrimEnd());
        if (_audit is not null)
        {
            text.AppendLine().AppendLine().AppendLine(Strings.Get("mediainfo.audit.title"));
            text.Append(_audit.Count == 0 ? Strings.Get("mediainfo.audit.none") + Environment.NewLine : AuditReport.Text(_audit, KindName));
        }
        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    internal async Task<bool> ScanAsync()
    {
        if (_info is not { } info || Scanning) return false;
        using var cts = new CancellationTokenSource();
        _scanCts = cts;
        SetBusy(true);
        TxtStatus.Text = Strings.Get("editor.silence.scanning");
        string? failure = null;
        try
        {
            var options = new SilenceCutOptions { Silence = info.HasAudio, Black = true };
            var first = new Progress<double>(value => ScanProgress.Value = value / 2);
            var scan = await SilenceScanner(_path, options, info.DurationSeconds, first, cts.Token).ConfigureAwait(true);
            var second = new Progress<double>(value => ScanProgress.Value = 0.5 + value / 2);
            var scenes = await SceneScanner(_path, info.DurationSeconds, second, cts.Token).ConfigureAwait(true);
            _audit = AuditReport.Build(scan, scenes);
            TxtAudit.Text = _audit.Count == 0 ? Strings.Get("mediainfo.audit.none") : AuditReport.Text(_audit, KindName).TrimEnd();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or UnauthorizedAccessException)
        {
            failure = ex.Message;
            return false;
        }
        finally
        {
            _scanCts = null;
            SetBusy(false);
            TxtStatus.Text = failure is null ? string.Empty : Strings.Get("editor.silence.failed", failure);
        }
    }

    internal async Task<bool> CopyAsync()
    {
        try
        {
            if (Clipboard is not { } clipboard) return false;
            await clipboard.SetTextAsync(ReportText()).ConfigureAwait(true);
            TxtStatus.Text = Strings.Get("mediainfo.copied");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            TxtStatus.Text = Strings.Get("mediainfo.failed", ex.Message);
            return false;
        }
    }

    internal async Task<bool> SaveAsync()
    {
        if (_info is null) return false;
        try
        {
            var target = await (SavePicker ?? PickAsync)(Path.GetFileNameWithoutExtension(_path) + ".txt").ConfigureAwait(true);
            if (target is null) return false;
            File.WriteAllText(target, ReportText(), new UTF8Encoding(false));
            TxtStatus.Text = Strings.Get("mediainfo.saved", target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TxtStatus.Text = Strings.Get("mediainfo.failed", ex.Message);
            return false;
        }
    }

    private void Show(MediaInfo info)
    {
        _info = info;
        TxtDetails.Text = DetailsOf(info).TrimEnd();
        BtnScan.IsEnabled = true;
        BtnCopy.IsEnabled = true;
        BtnSave.IsEnabled = true;
    }

    private void SetBusy(bool busy)
    {
        ScanProgress.Value = 0;
        ScanProgress.IsVisible = busy;
        BtnScanCancel.IsVisible = busy;
        BtnScan.IsEnabled = !busy && _info is not null;
    }

    private Task<string?> PickAsync(string suggested) => PickTextAsync(this, suggested);

    internal static async Task<string?> PickTextAsync(TopLevel? top, string suggested)
    {
        if (top is null) return null;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggested,
            DefaultExtension = "txt",
            FileTypeChoices = new[] { new FilePickerFileType(Strings.Get("mediainfo.file-type")) { Patterns = new[] { "*.txt" } } }
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    private static Task<SilenceScan> DefaultSilenceScan(string source, SilenceCutOptions options, double duration, IProgress<double> progress, CancellationToken ct)
    {
        if (!ToolLocator.IsAvailable(out var missing)) throw new InvalidOperationException(missing);
        var ffmpeg = ToolLocator.Ffmpeg;
        return Task.Run(() => SilenceCut.ScanAsync(ffmpeg, source, options, duration, progress, ct), ct);
    }

    private static Task<IReadOnlyList<double>> DefaultSceneScan(string source, double duration, IProgress<double> progress, CancellationToken ct)
    {
        if (!ToolLocator.IsAvailable(out var missing)) throw new InvalidOperationException(missing);
        return Task.Run(() => SceneDetector.CutsAsync(source, duration, progress, ct), ct);
    }
}
