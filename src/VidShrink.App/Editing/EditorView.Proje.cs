using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

/// <summary>
/// Proje kaydi: her duzenleme adimindan sonra gecikmeli otomatik kayit, ayni kaynak yeniden
/// acilinca geri yukleme ve elle "projeyi kaydet / proje ac". Geri yukleme tek geri alma
/// adimidir; Geri Al kaynagin dokunulmamis haline doner. Geri al yigini diske yazilmaz.
/// </summary>
internal partial class EditorView
{
    private readonly DispatcherTimer _autosave = new() { Interval = EditProjectStore.AutosaveDelay };
    private SourceStamp? _stamp;
    private bool _unsaved;
    private Window? _projectWindow;
    private bool _projectNotice;

    internal EditProjectStore? Projects { get; set; } = EditProjectStore.Default;

    internal TimeSpan AutosaveDelay
    {
        get => _autosave.Interval;
        set => _autosave.Interval = value;
    }

    internal int Autosaves { get; private set; }

    internal bool AutosavePending => _unsaved;

    private void InitProject()
    {
        _autosave.Tick += (_, _) => FlushProject();
        CmbExportMode.SelectionChanged += (_, _) => MarkEdited();
        MnuProjectSave.Click += (_, _) => _ = SaveProjectClickedAsync();
        MnuProjectOpen.Click += (_, _) => _ = OpenProjectClickedAsync();
        TimelineMenu.Opening += (_, _) => MnuProjectSave.IsEnabled = _model is not null && _stamp is not null;
    }

    private void AttachProject()
    {
        DetachProject();
        _projectWindow = TopLevel.GetTopLevel(this) as Window;
        if (_projectWindow is { } window) window.Closing += OnProjectWindowClosing;
    }

    private void DetachProject()
    {
        if (_projectWindow is { } window) window.Closing -= OnProjectWindowClosing;
        _projectWindow = null;
    }

    private void OnProjectWindowClosing(object? sender, WindowClosingEventArgs e) => FlushProject();

    private void MarkEdited()
    {
        if (Projects is null || _stamp is null || _model is null) return;
        _unsaved = true;
        _autosave.Stop();
        _autosave.Start();
    }

    internal bool FlushProject()
    {
        _autosave.Stop();
        if (!_unsaved) return false;
        if (Projects is not { } store || CurrentProject() is not { } project)
        {
            _unsaved = false;
            return false;
        }

        if (!store.Save(project)) return false;
        _unsaved = false;
        Autosaves++;
        return true;
    }

    private void ForgetProject()
    {
        FlushProject();
        _stamp = null;
        if (!_projectNotice || Exporting) return;
        SetStatus("Hint", string.Empty);
        ExportBar.IsVisible = false;
    }

    private EditProject? CurrentProject()
        => _model is { } model && _stamp is { } stamp ? EditProject.From(stamp, model, SelectedExportMode.ToString()) : null;

    private EditTimeline OpenedTimeline(string path, long sourceDuration, EditProject? given)
    {
        var model = EditTimeline.FromSource(sourceDuration);
        _stamp = SourceStamp.Of(path);
        if (_stamp is not { } stamp) return model;
        if ((given ?? Projects?.Load(stamp.Path)) is not { } project) return model;

        if (!project.Source.Matches(stamp) || !project.FitsIn(sourceDuration))
        {
            ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
            return model;
        }

        if (!model.Restore(project.Clips, project.Texts)) return model;
        UseExportMode(project.ExportMode);
        ShowProjectStatus("StatusSuccess", Strings.Get("editor.project.restored"));
        return model;
    }

    private void UseExportMode(string? name)
    {
        if (Enum.TryParse<ExportMode>(name, ignoreCase: false, out var mode) && Enum.IsDefined(mode)) SelectedExportMode = mode;
    }

    private void ShowProjectStatus(string theme, string text)
    {
        if (Exporting) return;
        ShowExportBar(running: false);
        SetStatus(theme, text);
        _projectNotice = true;
    }

    internal bool SaveProjectTo(string path)
    {
        if (CurrentProject() is not { } project) return false;
        var ok = project.Write(path);
        ShowProjectStatus(ok ? "StatusSuccess" : "StatusError",
            string.Format(Strings.Culture, Strings.Get(ok ? "editor.project.saved" : "editor.project.failed"), path));
        return ok;
    }

    internal async Task<bool> OpenProjectAsync(string path)
    {
        if (Exporting) return false;
        if (EditProject.Read(path) is not { } project)
        {
            ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.project.unreadable"), path));
            return false;
        }

        var source = project.Source.Path;
        if (SourceStamp.Of(source) is not { } stamp || !project.Source.Matches(stamp))
        {
            ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
            return false;
        }

        if (CurrentMedia.SamePath(source, _source) && _model is { } model)
        {
            if (!project.FitsIn(model.SourceDuration ?? project.SourceDuration))
            {
                ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
                return false;
            }

            _stamp = stamp;
            if (Apply(m => m.Restore(project.Clips, project.Texts), -1, reload: true)) AfterTextEdit(-1);
            UseExportMode(project.ExportMode);
            ShowProjectStatus("StatusSuccess", Strings.Get("editor.project.restored"));
            return true;
        }

        await OpenSourceAsync(source, 0, project).ConfigureAwait(true);
        return CurrentMedia.SamePath(source, _source) && _model is not null;
    }

    private async Task SaveProjectClickedAsync()
    {
        if (CurrentProject() is null || _source is not { } source) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.project.save"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(source) + EditProject.Extension,
            DefaultExtension = EditProject.Extension.TrimStart('.'),
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { ProjectFileType() }
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is { } path) SaveProjectTo(path);
    }

    private async Task OpenProjectClickedAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.project.open"),
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjectFileType() }
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) await OpenProjectAsync(path).ConfigureAwait(true);
    }

    private static FilePickerFileType ProjectFileType()
        => new("VidShrink") { Patterns = new[] { "*" + EditProject.Extension } };
}
