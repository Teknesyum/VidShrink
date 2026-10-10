using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.App.Share;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private CancellationTokenSource? _exportCts;
    private IReadOnlyList<ExportPlan>? _pendingPlan;
    private bool _singleOnly;
    private string? _pendingKey;
    private string? _exported;
    private string? _savedKey;
    private ShareSession? _shareSession;

    internal Func<string, Task>? OpenInPlayer { get; set; }

    internal Action<string> RevealFolder { get; set; } = Platform.Reveal;

    internal Func<string, bool> OutputExists { get; set; } = path => File.Exists(path) || Directory.Exists(path);

    internal Func<ShareFlow>? CreateShareFlow { get; set; }

    internal ExportPlan? LastPlan { get; private set; }

    internal IReadOnlyList<ExportPlan> LastPlans { get; private set; } = Array.Empty<ExportPlan>();

    internal IReadOnlyList<string> ExportedPaths { get; private set; } = Array.Empty<string>();

    internal int Exports { get; private set; }

    internal bool Exporting => _exportCts is not null;

    internal bool Sharing => _shareSession?.Running ?? false;

    internal string? ExportedPath => _exported;

    internal string ExportStatusText => TxtExportStatus.Text ?? string.Empty;

    internal string ExportNoteText => TxtExportNote.IsVisible ? TxtExportNote.Text ?? string.Empty : string.Empty;

    internal string ShareStatusText => TxtShareStatus.IsVisible ? TxtShareStatus.Text ?? string.Empty : string.Empty;

    internal string ShareLinkText => ShareLinkRow.IsVisible ? TxtShareLink.Text ?? string.Empty : string.Empty;

    internal string? DefaultOutput() => _source is { } source ? EditOutputName.For(source, OutputExists) : null;

    internal string? SavedPath
        => _exported is { } path && _model is { } model && _savedKey == SaveKey(model) && File.Exists(path) ? path : null;

    /// <summary>
    /// Teslim her parcayi ayri dosyaya yazar mi. Tek parcada secenek dugmesi pasiftir, kutu isaretli kalsa da etkisizdir;
    /// paylasim tek dosya ister, o yolda da kapalidir.
    /// </summary>
    internal bool ExportSeparately
    {
        get => !_singleOnly && ChkExportSeparate.IsChecked == true && _model is { Clips.Count: > 1 };
        set => ChkExportSeparate.IsChecked = value;
    }

    internal bool ExportSeparatelyEnabled => BtnExportOptions.IsEnabled && ChkExportSeparate.IsEnabled;

    /// <summary>
    /// Teslim dosyasindan kap etiketleri silinir mi; paylasim teslimi de ayni kutuyu okur. Secim ana pencerenin
    /// ayarinda saklanir, degisimi <see cref="DropMetadataChanged"/> bildirir.
    /// </summary>
    internal bool DropMetadata
    {
        get => ChkExportDropMetadata.IsChecked == true;
        set => ChkExportDropMetadata.IsChecked = value;
    }

    internal Action<bool>? DropMetadataChanged { get; set; }

    internal ExportMode SelectedExportMode
    {
        get => CmbExportMode.SelectedIndex switch { 0 => ExportMode.Fast, 2 => ExportMode.Full, _ => ExportMode.Smart };
        set => CmbExportMode.SelectedIndex = value switch { ExportMode.Fast => 0, ExportMode.Full => 2, _ => 1 };
    }

    internal static long ExportMemoryBudget() => Math.Max(1, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2);

    private void InitExport()
    {
        BtnSave.Click += (_, _) => _ = SaveAsync();
        BtnExport.Click += (_, _) => _ = ExportClickedAsync();
        BtnShare.Click += (_, _) => _ = ShareAsync();
        BtnShareCancel.Click += (_, _) => _shareSession?.Cancel();
        BtnShareCopy.Click += (_, _) => _ = CopyShareLinkAsync();
        BtnExportCancel.Click += (_, _) => _exportCts?.Cancel();
        BtnExportReveal.Click += (_, _) => { if (_exported is { } path) RevealFolder(path); };
        BtnExportPlay.Click += (_, _) => { if (_exported is { } path && OpenInPlayer is { } open) _ = open(path); };
        CmbExportMode.SelectionChanged += (_, _) => _pendingPlan = null;
        ChkExportSeparate.IsCheckedChanged += (_, _) => _pendingPlan = null;
        ChkExportDropMetadata.IsCheckedChanged += (_, _) =>
        {
            _pendingPlan = null;
            DropMetadataChanged?.Invoke(DropMetadata);
        };
    }

    private void RefreshExport() => EnableExport(!Exporting);

    private void EnableExport(bool idle)
    {
        var ready = idle && _model is { Clips.Count: > 0 } && _source is not null;
        BtnSave.IsEnabled = ready;
        BtnExport.IsEnabled = ready;
        BtnShare.IsEnabled = ready && !Sharing;
        BtnExportOptions.IsEnabled = ready;
        ChkExportSeparate.IsEnabled = _model is { Clips.Count: > 1 };
        var neden = _model is not { Clips.Count: > 0 } || _source is null ? Strings.Get("main.action.shrink.disabled-tip") : null;
        ToolTip.SetTip(BtnSave, neden ?? Strings.Get("editor.save"));
        ToolTip.SetTip(BtnExport, neden ?? Tip("editor.save-as", EditorCommand.Export));
        ToolTip.SetTip(BtnShare, neden ?? Strings.Get("main.action.share"));
    }

    private void ForgetSaved()
    {
        _exported = null;
        _savedKey = null;
        _pendingPlan = null;
    }

    private string SaveKey(EditTimeline model) => Fingerprint(model) + "|" + SelectedExportMode + (ExportSeparately ? "|parts" : string.Empty) + (DropMetadata ? "|meta" : string.Empty);

    internal async Task<bool> SaveAsync()
    {
        if (_model is not { Clips.Count: > 0 } model || _source is null || Exporting) return false;
        if (SavedPath is { } saved)
        {
            Finish(true, ExportSeparately ? Done(ExportedPaths) : Done(saved));
            return true;
        }

        if (_pendingPlan is { } pending && _pendingKey == SaveKey(model))
        {
            _pendingPlan = null;
            return await RunExportAsync(pending, _pendingKey).ConfigureAwait(true);
        }

        var output = ExportSeparately ? EditOutputName.For(_source, _ => false) : DefaultOutput();
        return output is not null && await ExportToAsync(output).ConfigureAwait(true);
    }

    private async Task ExportClickedAsync()
    {
        if (_model is not { Clips.Count: > 0 } model || _source is null || Exporting) return;
        if (_pendingPlan is { } pending && _pendingKey == SaveKey(model))
        {
            _pendingPlan = null;
            await RunExportAsync(pending, _pendingKey).ConfigureAwait(true);
            return;
        }

        var output = await PickOutputAsync().ConfigureAwait(true);
        if (output is not null) await ExportToAsync(output).ConfigureAwait(true);
    }

    internal async Task<bool> ExportToAsync(string output, bool confirmReverse = false)
    {
        if (_model is not { Clips.Count: > 0 } model || _source is null || Exporting) return false;
        var sources = SourcePaths;
        var snapshot = new EditTimeline(model.Clips.ToArray(), texts: model.Texts.ToArray());
        var key = SaveKey(snapshot);
        _pendingPlan = null;
        _exported = null;
        _savedKey = null;
        ShowExportBar(running: true);
        TxtExportStatus.Text = string.Empty;

        IReadOnlyList<ExportPlan> plans;
        try
        {
            plans = ExportSeparately
                ? await EditExportRunner.PrepareSegmentsAsync(sources, snapshot, SelectedExportMode, output, ExportMemoryBudget(), OutputExists, DropMetadata).ConfigureAwait(true)
                : new[] { await EditExportRunner.PrepareAsync(sources, snapshot, SelectedExportMode, output, ExportMemoryBudget(), DropMetadata).ConfigureAwait(true) };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            Finish(false, string.Format(Strings.Culture, Strings.Get("editor.export.failed"), ex.Message));
            return false;
        }

        LastPlan = plans[0];
        LastPlans = plans;
        TxtExportNote.Text = Notes(plans);
        TxtExportNote.IsVisible = TxtExportNote.Text.Length > 0;

        var over = plans.SelectMany(p => p.ReverseOverLimit).Distinct().OrderBy(i => i).ToArray();
        if (over.Length > 0 && !confirmReverse)
        {
            _pendingPlan = plans;
            _pendingKey = key;
            ShowExportBar(running: false);
            SetStatus("StatusWarning", string.Format(Strings.Culture, Strings.Get("editor.export.reverse-limit"),
                ClipNumbers(over), Saat.Ekran(TimeSpan.FromSeconds(Math.Min(plans[0].ReverseLimitSeconds, TimeSpan.MaxValue.TotalSeconds / 2)))));
            return false;
        }

        return await RunExportAsync(plans, key).ConfigureAwait(true);
    }

    private async Task<bool> RunExportAsync(IReadOnlyList<ExportPlan> plans, string? key)
    {
        using var cts = new CancellationTokenSource();
        _exportCts = cts;
        ShowExportBar(running: true);
        SetStatus("Hint", string.Empty);
        ExportProgress.Value = 0;
        var progress = new Progress<EncodeProgress>(p => ExportProgress.Value = p.Fraction);
        try
        {
            if (plans.Count == 1) await EditExportRunner.RunAsync(plans[0], progress, cts.Token).ConfigureAwait(true);
            else await EditExportRunner.RunSegmentsAsync(plans, progress, cts.Token).ConfigureAwait(true);
            ExportedPaths = plans.Select(p => p.OutputPath).ToArray();
            _exported = plans[0].OutputPath;
            _savedKey = key;
            Exports++;
            Finish(true, plans.Count > 1 ? Done(ExportedPaths) : Done(plans[0].OutputPath));
            return true;
        }
        catch (OperationCanceledException)
        {
            Finish(false, Strings.Get("main.run.cancelled"));
            return false;
        }
        catch (EditSegmentException ex)
        {
            Finish(false, string.Format(Strings.Culture, Strings.Get("editor.export.segment-failed"), ex.Index + 1, ex.Message));
            return false;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Finish(false, string.Format(Strings.Culture, Strings.Get("editor.export.failed"), ex.Message));
            return false;
        }
        finally
        {
            _exportCts = null;
            RefreshExport();
        }
    }

    internal async Task<bool> ShareAsync()
    {
        if (Exporting || Sharing) return false;
        string? path;
        _singleOnly = true;
        try
        {
            path = SavedPath;
            if (path is null && await SaveAsync().ConfigureAwait(true)) path = SavedPath;
        }
        finally
        {
            _singleOnly = false;
        }

        if (path is null) return false;

        var session = _shareSession ??= new ShareSession(() => CreateShareFlow);
        ExportBar.IsVisible = true;
        if (session.Target() is not { } target)
        {
            ShowShareStatus(ShareSession.MissingTargets());
            return false;
        }

        BtnShare.IsEnabled = false;
        BtnShareCancel.IsVisible = true;
        ShareLinkRow.IsVisible = false;
        TxtShareLink.Text = string.Empty;
        ShareProgress.Value = 0;
        ShareProgress.IsVisible = true;
        ShowShareStatus(ShareSession.Uploading());

        var progress = new Progress<CoreShare.UploadProgress>(step => ShareProgress.Value = step.Fraction);
        var result = await session.ShareAsync(target, path, progress).ConfigureAwait(true);

        BtnShareCancel.IsVisible = false;
        ShareProgress.IsVisible = false;
        RefreshExport();
        if (result.Ok && result.Link is { } link)
        {
            TxtShareLink.Text = link.Url;
            ShareLinkRow.IsVisible = true;
        }

        ShowShareStatus(ShareSession.Status(result));
        return result.Ok;
    }

    private void ShowShareStatus(string text)
    {
        TxtShareStatus.Text = text;
        TxtShareStatus.IsVisible = text.Length > 0;
    }

    private async Task CopyShareLinkAsync()
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            await clipboard.SetTextAsync(TxtShareLink.Text ?? string.Empty).ConfigureAwait(true);
        }
        catch (Exception)
        {
        }
    }

    private void Finish(bool ok, string message)
    {
        _exportCts = null;
        ShowExportBar(running: false);
        SetStatus(ok ? "StatusSuccess" : "StatusError", message);
        BtnExportReveal.IsVisible = ok;
        BtnExportPlay.IsVisible = ok && OpenInPlayer is not null;
    }

    private void ShowExportBar(bool running)
    {
        ExportBar.IsVisible = true;
        ExportProgress.IsVisible = running;
        BtnExportCancel.IsVisible = running;
        BtnExportReveal.IsVisible = false;
        BtnExportPlay.IsVisible = false;
        CmbExportMode.IsEnabled = !running;
        EnableExport(!running);
    }

    private void SetStatus(string theme, string text)
    {
        if (this.TryFindResource(theme, out var found) && found is ControlTheme controlTheme) TxtExportStatus.Theme = controlTheme;
        TxtExportStatus.Text = text;
        _projectNotice = false;
    }

    private static string Done(string path) => string.Format(Strings.Culture, Strings.Get("editor.export.done"), path);

    private static string Done(IReadOnlyList<string> paths)
        => string.Format(Strings.Culture, Strings.Get("editor.export.segments-done"), paths.Count,
            Path.GetDirectoryName(paths[0]) ?? paths[0]);

    private static string Notes(IReadOnlyList<ExportPlan> plans)
    {
        var plan = plans[0];
        var motion = plans.SelectMany(p => p.MotionClips).Distinct().OrderBy(i => i).ToArray();
        var lines = new List<string>();
        if (plan.MergeForcedFull) lines.Add(Strings.Get("editor.export.merge-full"));
        else if (plan.TextForcedFull) lines.Add(Strings.Get("editor.export.text-full"));
        else if (plan.EffectsForcedFull) lines.Add(Strings.Get("editor.export.effects-full"));
        else if (plan.AudioForcedFull) lines.Add(Strings.Get("editor.export.audio-full"));
        else if (plan.FellBackToFull) lines.Add(Strings.Get("editor.export.fallback"));
        else if (plans.Any(p => p.Effective != ExportMode.Full) && motion.Length > 0)
            lines.Add(string.Format(Strings.Culture, Strings.Get("editor.export.reencoded"), ClipNumbers(motion)));

        return string.Join(Environment.NewLine, lines);
    }

    private static string ClipNumbers(IEnumerable<int> indexes) => string.Join(", ", indexes.Select(i => (i + 1).ToString(Strings.Culture)));

    private static string Fingerprint(EditTimeline model)
        => string.Join(";", model.Clips.Select(c => $"{c.Source}:{c.SourceStart}-{c.SourceEnd}-{c.Speed}-{c.Reversed}-{c.Effects}"))
           + "|" + string.Join(";", model.Texts.Select(t => $"{t.Start}-{t.End}-{t.Size}-{t.Color}-{t.Bold}-{t.Italic}-{t.FontName}-{t.FadeIn}-{t.FadeOut}-"
               + string.Join(",", t.Keyframes.Select(k => $"{k.Offset}:{k.X}:{k.Y}")) + "-" + t.Text.Length + ":" + t.Text));

    private async Task<string?> PickOutputAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || _source is not { } source) return null;
        var suggested = Path.GetFileName(DefaultOutput() ?? source);
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.export.save-title"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggested) + ".mp4",
            DefaultExtension = "mp4",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType("MP4") { Patterns = new[] { "*.mp4" } }, new FilePickerFileType("MKV") { Patterns = new[] { "*.mkv" } } }
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }
}
