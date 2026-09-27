using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private CancellationTokenSource? _exportCts;
    private ExportPlan? _pendingPlan;
    private string? _pendingKey;
    private string? _exported;

    internal Func<string, Task>? OpenInPlayer { get; set; }

    internal Action<string> RevealFolder { get; set; } = Platform.Reveal;

    internal bool Exporting => _exportCts is not null;

    internal string? ExportedPath => _exported;

    internal string ExportStatusText => TxtExportStatus.Text ?? string.Empty;

    internal string ExportNoteText => TxtExportNote.IsVisible ? TxtExportNote.Text ?? string.Empty : string.Empty;

    internal ExportMode SelectedExportMode
    {
        get => CmbExportMode.SelectedIndex switch { 0 => ExportMode.Fast, 2 => ExportMode.Full, _ => ExportMode.Smart };
        set => CmbExportMode.SelectedIndex = value switch { ExportMode.Fast => 0, ExportMode.Full => 2, _ => 1 };
    }

    internal static long ExportMemoryBudget() => Math.Max(1, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2);

    private void InitExport()
    {
        BtnExport.Click += (_, _) => _ = ExportClickedAsync();
        BtnExportCancel.Click += (_, _) => _exportCts?.Cancel();
        BtnExportReveal.Click += (_, _) => { if (_exported is { } path) RevealFolder(path); };
        BtnExportPlay.Click += (_, _) => { if (_exported is { } path && OpenInPlayer is { } open) _ = open(path); };
        CmbExportMode.SelectionChanged += (_, _) => _pendingPlan = null;
    }

    private void RefreshExport()
        => BtnExport.IsEnabled = _model is not null && _source is not null && !Exporting;

    private async Task ExportClickedAsync()
    {
        if (_model is not { } model || _source is null || Exporting) return;
        if (_pendingPlan is { } pending && _pendingKey == Fingerprint(model))
        {
            _pendingPlan = null;
            await RunExportAsync(pending).ConfigureAwait(true);
            return;
        }

        var output = await PickOutputAsync().ConfigureAwait(true);
        if (output is not null) await ExportToAsync(output).ConfigureAwait(true);
    }

    internal async Task<bool> ExportToAsync(string output, bool confirmReverse = false)
    {
        if (_model is not { } model || _source is not { } source || Exporting) return false;
        var snapshot = new EditTimeline(model.Clips.ToArray());
        _pendingPlan = null;
        _exported = null;
        ShowExportBar(running: true);
        TxtExportStatus.Text = string.Empty;

        ExportPlan plan;
        try
        {
            plan = await EditExportRunner.PrepareAsync(source, snapshot, SelectedExportMode, output, ExportMemoryBudget()).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            Finish(false, string.Format(Strings.Culture, Strings.Get("editor.export.failed"), ex.Message));
            return false;
        }

        TxtExportNote.Text = Notes(plan);
        TxtExportNote.IsVisible = TxtExportNote.Text.Length > 0;

        if (plan.ReverseOverLimit.Count > 0 && !confirmReverse)
        {
            _pendingPlan = plan;
            _pendingKey = Fingerprint(snapshot);
            ShowExportBar(running: false);
            SetStatus("StatusWarning", string.Format(Strings.Culture, Strings.Get("editor.export.reverse-limit"),
                ClipNumbers(plan.ReverseOverLimit), Saat.Ekran(TimeSpan.FromSeconds(Math.Min(plan.ReverseLimitSeconds, TimeSpan.MaxValue.TotalSeconds / 2)))));
            return false;
        }

        return await RunExportAsync(plan).ConfigureAwait(true);
    }

    private async Task<bool> RunExportAsync(ExportPlan plan)
    {
        using var cts = new CancellationTokenSource();
        _exportCts = cts;
        ShowExportBar(running: true);
        SetStatus("Hint", string.Empty);
        ExportProgress.Value = 0;
        var progress = new Progress<EncodeProgress>(p => ExportProgress.Value = p.Fraction);
        try
        {
            await EditExportRunner.RunAsync(plan, progress, cts.Token).ConfigureAwait(true);
            _exported = plan.OutputPath;
            Finish(true, string.Format(Strings.Culture, Strings.Get("editor.export.done"), Path.GetFileName(plan.OutputPath)));
            return true;
        }
        catch (OperationCanceledException)
        {
            Finish(false, Strings.Get("main.run.cancelled"));
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
        BtnExport.IsEnabled = !running && _model is not null && _source is not null;
    }

    private void SetStatus(string theme, string text)
    {
        if (this.TryFindResource(theme, out var found) && found is ControlTheme controlTheme) TxtExportStatus.Theme = controlTheme;
        TxtExportStatus.Text = text;
    }

    private static string Notes(ExportPlan plan)
    {
        var lines = new List<string>();
        if (plan.FellBackToFull) lines.Add(Strings.Get("editor.export.fallback"));
        else if (plan.Effective != ExportMode.Full && plan.MotionClips.Count > 0)
            lines.Add(string.Format(Strings.Culture, Strings.Get("editor.export.reencoded"), ClipNumbers(plan.MotionClips)));

        return string.Join(Environment.NewLine, lines);
    }

    private static string ClipNumbers(IEnumerable<int> indexes) => string.Join(", ", indexes.Select(i => (i + 1).ToString(Strings.Culture)));

    private static string Fingerprint(EditTimeline model)
        => string.Join(";", model.Clips.Select(c => $"{c.SourceStart}-{c.SourceEnd}-{c.Speed}-{c.Reversed}"));

    private async Task<string?> PickOutputAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || _source is not { } source) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.export.save-title"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(source) + "-edit.mp4",
            DefaultExtension = "mp4",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType("MP4") { Patterns = new[] { "*.mp4" } }, new FilePickerFileType("MKV") { Patterns = new[] { "*.mkv" } } }
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }
}
