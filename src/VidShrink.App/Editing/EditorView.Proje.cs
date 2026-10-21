using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private IReadOnlyList<EditMarker> _pendingMarkers = Array.Empty<EditMarker>();

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
        MnuBatchExport.Click += (_, _) => _ = BatchExportClickedAsync();
        MnuCutListExport.Click += (_, _) => _ = ExportCutListClickedAsync();
        MnuCutListImport.Click += (_, _) => _ = ImportCutListClickedAsync();
        MnuOtioExport.Click += (_, _) => _ = ExportOtioClickedAsync();
        MnuOtioImport.Click += (_, _) => _ = ImportOtioClickedAsync();
        TimelineMenu.Opening += (_, _) =>
        {
            MnuProjectSave.IsEnabled = _model is not null && _stamp is not null;
            MnuCutListExport.IsEnabled = _model is { Clips.Count: > 0 };
            MnuCutListImport.IsEnabled = _model is not null && !Exporting;
            MnuOtioExport.IsEnabled = _model is { Clips.Count: > 0 };
            MnuOtioImport.IsEnabled = !Exporting;
        };
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
        => _model is { } model && _stamp is { } stamp && ExtraStamps() is { } extras
            ? EditProject.From(stamp, model, SelectedExportMode.ToString(), extras, Timeline.MarkerList, SelectedAudioTrack)
            : null;

    private EditTimeline OpenedTimeline(string path, long sourceDuration, EditProject? given)
    {
        _pendingMarkers = SourceChapterMarkers(path);
        var model = EditTimeline.FromSource(sourceDuration);
        _stamp = SourceStamp.Of(path);
        if (_stamp is not { } stamp) return model;
        if ((given ?? Projects?.Load(stamp.Path)) is not { } project) return model;

        if (!project.Source.Matches(stamp) || !project.FitsIn(sourceDuration))
        {
            ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
            return model;
        }

        if (project.ExtraSources.Count > 0)
        {
            if (!AdoptSources(project.ExtraSources))
            {
                ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
                return model;
            }

            model = new EditTimeline(model.Clips, sourceDuration, extraSources: project.ExtraSources.Select(extra => extra.Duration), frame: model.Frame);
        }

        UseAudioTrack(project.AudioTrack);
        if (!model.Restore(project.Clips, project.Texts, project.Music, project.Frame, project.Layers) && project.Markers.Count == 0) return model;
        _pendingMarkers = project.Markers;
        UseExportMode(project.ExportMode);
        ShowProjectStatus("StatusSuccess", Strings.Get("editor.project.restored"));
        WarnMissingLuts(project);
        return model;
    }

    private void WarnMissingLuts(EditProject project)
    {
        if (project.MissingLuts.Count == 0) return;
        var names = string.Join(", ", project.MissingLuts.Select(p => Path.GetFileName(p.Replace('\\', '/').Split('/')[^1])));
        ShowProjectStatus("StatusWarning", string.Format(Strings.Culture, Strings.Get("editor.clip.lut.missing"), names));
    }

    /// <summary>
    /// Acilista onerilen isaretciler: kaynagin bolum baslari, bolumun adiyla (<see cref="EditChapters.Markers"/>).
    /// Kayitli proje isaretci tasiyorsa onlar kazanir. Bilgi <see cref="KnownInfo"/>'dan gelir; kaynagin baslangic
    /// kaymasi acilista bilinmez, sifir sayilir.
    /// </summary>
    private IReadOnlyList<EditMarker> SourceChapterMarkers(string path)
        => KnownInfo?.Invoke(path) is { Chapters.Count: > 0 } info
            ? EditChapters.Markers(info.Chapters, 0)
            : Array.Empty<EditMarker>();

    /// <summary>Kutudaki adi oynatma basindaki isaretciye yazar; isaretci yoksa ekler. Bos ad adi siler.</summary>
    internal bool CommitMarkerName()
    {
        if (_model is null || !Timeline.NameMarker(Timeline.Playhead, TxtMarkerName.Text)) return false;
        MarkEdited();
        return true;
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

        return await OpenProjectAsync(project).ConfigureAwait(true);
    }

    private async Task<bool> OpenProjectAsync(EditProject project)
    {
        var source = project.Source.Path;
        if (SourceStamp.Of(source) is not { } stamp || !project.Source.Matches(stamp))
        {
            ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
            return false;
        }

        if (CurrentMedia.SamePath(source, _source) && _model is { } model && project.ExtraSources.Count == 0)
        {
            if (!project.FitsIn(model.SourceDuration ?? project.SourceDuration))
            {
                ShowProjectStatus("StatusWarning", Strings.Get("editor.project.stale"));
                return false;
            }

            _stamp = stamp;
            UseAudioTrack(project.AudioTrack);
            if (Apply(m => m.Restore(project.Clips, project.Texts, project.Music, project.Frame, project.Layers), -1, reload: true)) AfterTextEdit(-1);
            Timeline.SetMarkers(project.Markers);
            UseExportMode(project.ExportMode);
            ShowProjectStatus("StatusSuccess", Strings.Get("editor.project.restored"));
            WarnMissingLuts(project);
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

    /// <summary>DZA-09: seçilen proje dosyalarını toplu teslim penceresinde açar; testler açıcıyı değiştirir.</summary>
    internal Action<IReadOnlyList<string>>? BatchExportOpener { get; set; }

    private async Task BatchExportClickedAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.batch.add"),
            AllowMultiple = true,
            FileTypeFilter = new[] { ProjectFileType() }
        }).ConfigureAwait(true);
        var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
        if (paths.Length > 0) (BatchExportOpener ?? (chosen => new EditBatchWindow(chosen).Show()))(paths);
    }

    private static FilePickerFileType ProjectFileType()
        => new(Core.AppIdentity.Name) { Patterns = new[] { "*" + EditProject.Extension } };

    /// <summary>
    /// Cizelgeyi uzantinin bicimindeki kesim listesine yazar (<see cref="CutList"/>): <c>.csv</c>, <c>.edl</c>, YouTube
    /// bolumleri icin <c>.txt</c>, Premiere Pro ve DaVinci Resolve icin Final Cut <c>.xml</c>. Adsiz bolum yerel
    /// "Bolum {0}" adini alir.
    /// </summary>
    internal bool ExportCutListTo(string path)
    {
        if (_model is not { Clips.Count: > 0 } model || CutList.FormatOf(path) is not { } format || !CutList.CanWrite(format)) return false;
        var info = _source is { } source ? KnownInfo?.Invoke(source) : null;
        var context = new CutListContext(SourcePaths, MonitorFps)
        {
            Width = info?.Width ?? 0,
            Height = info?.Height ?? 0,
            HasAudio = info?.HasAudio ?? true,
            ChapterTitle = Strings.Get("mediainfo.section.chapter"),
            Title = _source is { } named ? Path.GetFileNameWithoutExtension(named) : null
        };
        bool ok;
        try
        {
            File.WriteAllText(path, CutList.Write(format, model, context));
            ok = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ok = false;
        }

        ShowProjectStatus(ok ? "StatusSuccess" : "StatusError",
            string.Format(Strings.Culture, Strings.Get(ok ? "editor.cutlist.exported" : "editor.cutlist.failed"), path));
        return ok;
    }

    /// <summary>
    /// CSV, EDL ya da CUE listesindeki araliklari ilk kaynaktan parca olarak cizelgenin yerine koyar; metinler ve muzik
    /// kalir. Tek geri alma adimidir. Okunamayan ya da gecerli araligi kalmayan liste cizelgeye dokunmaz.
    /// </summary>
    internal bool ImportCutListFrom(string path)
    {
        if (_model is not { } model || Exporting) return false;
        IReadOnlyList<CutListEntry>? entries = null;
        if (CutList.FormatOf(path) is { } format && CutList.CanRead(format) && model.SourceDuration is { } duration)
        {
            try
            {
                entries = CutList.Read(format, File.ReadAllText(path), MonitorFps, duration);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                entries = null;
            }
        }

        if (entries is null)
        {
            ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.cutlist.unreadable"), path));
            return false;
        }

        var clips = CutList.ToClips(entries);
        Apply(m => m.Restore(clips, m.Texts), 0, reload: true);
        ShowProjectStatus("StatusSuccess", string.Format(Strings.Culture, Strings.Get("editor.cutlist.imported"), clips.Count));
        return true;
    }

    internal bool ExportOtioTo(string path)
    {
        if (_model is not { Clips.Count: > 0 } model) return false;
        var info = _source is { } source ? KnownInfo?.Invoke(source) : null;
        var context = new CutListContext(SourcePaths, MonitorFps)
        {
            HasAudio = info?.HasAudio ?? true,
            Title = _source is { } named ? Path.GetFileNameWithoutExtension(named) : null
        };
        bool ok;
        try
        {
            File.WriteAllText(path, Otio.Write(model, context, Timeline.MarkerList));
            ok = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ok = false;
        }

        ShowProjectStatus(ok ? "StatusSuccess" : "StatusError",
            string.Format(Strings.Culture, Strings.Get(ok ? "editor.otio.exported" : "editor.otio.failed"), path));
        return ok;
    }

    internal async Task<bool> ImportOtioAsync(string path)
    {
        if (Exporting) return false;
        OtioImport? read;
        try
        {
            read = Otio.Read(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            read = null;
        }

        if (read is null)
        {
            ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.otio.unreadable"), path));
            return false;
        }

        if (read.ToProject(SourceStamp.Of, out var missing) is not { } project)
        {
            ShowProjectStatus("StatusWarning", string.Format(Strings.Culture, Strings.Get("editor.otio.missing"), missing));
            return false;
        }

        if (!await OpenProjectAsync(project).ConfigureAwait(true)) return false;
        ShowProjectStatus(read.Skipped > 0 ? "StatusWarning" : "StatusSuccess", read.Skipped > 0
            ? string.Format(Strings.Culture, Strings.Get("editor.otio.imported-partial"), read.Clips.Count, read.Skipped)
            : string.Format(Strings.Culture, Strings.Get("editor.otio.imported"), read.Clips.Count));
        return true;
    }

    private static FilePickerFileType OtioFileType() => new("OpenTimelineIO") { Patterns = new[] { "*" + Otio.Extension } };

    private async Task ExportOtioClickedAsync()
    {
        if (_model is null || _source is not { } source) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.otio.export"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(source) + Otio.Extension,
            DefaultExtension = Otio.Extension.TrimStart('.'),
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { OtioFileType() }
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is { } path) ExportOtioTo(path);
    }

    private async Task ImportOtioClickedAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.otio.import"),
            AllowMultiple = false,
            FileTypeFilter = new[] { OtioFileType() }
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) await ImportOtioAsync(path).ConfigureAwait(true);
    }

    private async Task ExportCutListClickedAsync()
    {
        if (_model is null || _source is not { } source) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.cutlist.export"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(source) + ".csv",
            DefaultExtension = "csv",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("EDL") { Patterns = new[] { "*.edl" } },
                new FilePickerFileType(Strings.Get("editor.cutlist.youtube")) { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("XML") { Patterns = new[] { "*.xml" } }
            }
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is { } path) ExportCutListTo(path);
    }

    private async Task ImportCutListClickedAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.cutlist.import"),
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(Strings.Get("editor.cutlist.files")) { Patterns = new[] { "*.csv", "*.edl", "*.cue" } } }
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) ImportCutListFrom(path);
    }
}
