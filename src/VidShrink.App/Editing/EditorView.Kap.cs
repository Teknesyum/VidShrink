using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>
/// Teslim acilirindaki kap ici iz bolumu (DZA-85). Kaynagin izleri acilir ilk acildiginda <see cref="SourceProber"/> ile bir
/// kez okunur; secilen izin dili, adi, varsayilan bayragi, kaydirmasi ve donusu duzenlenir ve kaynak <see cref="RemuxRunner"/>
/// ile yeniden kodlanmadan yeni dosyaya yazilir. Baska dosyanin izleri listeye eklenir, dosyaya katilmayan iz cikar (sesi
/// degistirmek kaynak sesi birakip dis sesi katmaktir), secili iz kendi dosyasina cikarilir ve kap tasiyorsa kapak ilistirilir.
/// Cizelgeye ve teslim planina dokunmaz. Bos dil ve ad kaynaktaki degeri birakir; adi bulunan izde bos ad onu temizler.
/// </summary>
internal partial class EditorView
{
    private sealed class RemuxRow
    {
        internal required RemuxTrack Track { get; init; }
        internal string? Codec { get; init; }
        internal double Duration { get; init; }
        internal bool Include { get; set; } = true;
        internal string Language { get; set; } = string.Empty;
        internal string Title { get; set; } = string.Empty;
        internal bool IsDefault { get; set; }
        internal string Offset { get; set; } = "0";
        internal int Rotation { get; set; }
    }

    private static readonly int?[] RemuxRotations = { null, 0, 90, 180, 270 };

    private RemuxRow[] _remuxRows = Array.Empty<RemuxRow>();
    private string? _remuxFor;
    private string? _remuxCover;
    private double _remuxDuration;
    private bool _remuxSync;

    private void InitRemux()
    {
        BtnExportOptions.Flyout!.Opened += (_, _) => _ = LoadRemuxAsync();
        BtnRemux.Click += (_, _) => _ = RemuxClickedAsync();
        BtnRemuxAdd.Click += (_, _) => _ = RemuxAddClickedAsync();
        BtnRemuxExtract.Click += (_, _) => _ = RemuxExtractClickedAsync();
        BtnRemuxCover.Click += (_, _) => _ = RemuxCoverClickedAsync();
        CmbRemuxTrack.SelectionChanged += (_, _) => ShowRemuxRow();
        TxtRemuxLanguage.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Language = TxtRemuxLanguage.Text ?? string.Empty); };
        TxtRemuxTrackTitle.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Title = TxtRemuxTrackTitle.Text ?? string.Empty); };
        TxtRemuxOffset.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Offset = TxtRemuxOffset.Text ?? string.Empty); };
        ChkRemuxInclude.IsCheckedChanged += (_, _) => EditRemuxRow(row => row.Include = ChkRemuxInclude.IsChecked == true);
        ChkRemuxDefault.IsCheckedChanged += (_, _) => EditRemuxRow(row => row.IsDefault = ChkRemuxDefault.IsChecked == true);
        CmbRemuxRotation.SelectionChanged += (_, _) => EditRemuxRow(row => row.Rotation = Math.Max(0, CmbRemuxRotation.SelectedIndex));
        ShowRemuxRow();
    }

    /// <summary>Kaynagin izlerini okur; ayni kaynak icin ikinci kez okumaz. Okunamazsa bolum kapali kalir.</summary>
    internal async Task LoadRemuxAsync()
    {
        if (_source is not { } source || PathEquality.Same(source, _remuxFor)) return;
        if (await ProbeRemuxAsync(source).ConfigureAwait(true) is not { } info || !PathEquality.Same(source, _source)) return;
        _remuxFor = source;
        _remuxCover = null;
        _remuxDuration = info.DurationSeconds;
        ListRemuxRows(RemuxRowsOf(info, null).ToArray(), 0);
    }

    /// <summary>
    /// Baska dosyanin izlerini listenin sonuna ekler ve ilkini secer. Kaynak ya da zaten eklenmis dosya, ve okunamayan dosya
    /// listeyi degistirmez.
    /// </summary>
    internal async Task AddRemuxSourceAsync(string file)
    {
        if (_remuxRows.Length == 0 || PathEquality.Same(file, _source) || _remuxRows.Any(row => PathEquality.Same(row.Track.Source, file))) return;
        var forSource = _remuxFor;
        if (await ProbeRemuxAsync(file).ConfigureAwait(true) is not { } info || !PathEquality.Same(forSource, _remuxFor)) return;
        if (_remuxRows.Any(row => PathEquality.Same(row.Track.Source, file))) return;
        var added = RemuxRowsOf(info, file).ToArray();
        if (added.Length == 0) return;
        ListRemuxRows(_remuxRows.Concat(added).ToArray(), _remuxRows.Length);
    }

    /// <summary>Kapak resmini secer ya da <c>null</c> ile kaldirir; secim durum satirinda adiyla gorunur.</summary>
    internal void SetRemuxCover(string? file)
    {
        _remuxCover = file;
        SetStatus("Hint", file is null ? string.Empty : string.Format(Strings.Culture, Strings.Get("editor.remux.cover.set"), Path.GetFileName(file)));
    }

    /// <summary>Secili izin ayri dosyasi icin onerilen ad: izin dosyasinin adi, iz sirasi ve kodegin uzantisi.</summary>
    internal string? RemuxExtractName()
        => SelectedRemuxRow() is { } row && (row.Track.Source ?? _source) is { } file
            ? Path.GetFileNameWithoutExtension(file) + "-" + row.Track.Index.ToString(CultureInfo.InvariantCulture) + Remux.ExtractExtension(row.Codec)
            : null;

    /// <summary>
    /// Kutulardaki degerlerden istek kurar. Gecersiz dil ya da kaydirma, katilan izin kalmamasi, kapagi tasimayan kap ya da
    /// girdilerden birinin ustune yazma durum satirina hata yazar ve <c>null</c> doner.
    /// </summary>
    internal RemuxRequest? RemuxRequestFor(string output)
    {
        if (_source is not { } source || _remuxRows.Length == 0) return null;
        if (PathEquality.Same(source, output) || _remuxRows.Any(row => PathEquality.Same(row.Track.Source, output)))
            return RemuxError(Strings.Get("editor.remux.same-file"));
        var tracks = new List<RemuxTrack>();
        foreach (var row in _remuxRows.Where(row => row.Include))
        {
            var language = row.Language.Trim();
            var valid = long.TryParse(row.Offset.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset)
                        && (language.Length == 0 || Remux.IsLanguage(language.ToLowerInvariant()));
            if (!valid) return RemuxError(string.Format(Strings.Culture, Strings.Get("editor.remux.invalid"), row.Track.Index));
            tracks.Add(row.Track with
            {
                Language = language.Length == 0 ? row.Track.Language : language.ToLowerInvariant(),
                Title = row.Title.Length == 0 && row.Track.Title is null ? null : row.Title,
                IsDefault = row.IsDefault,
                OffsetMs = offset,
                Rotation = row.Track.Kind == StreamKind.Video ? RemuxRotations[row.Rotation] : null
            });
        }

        if (tracks.Count == 0) return RemuxError(Strings.Get("editor.remux.empty"));
        if (_remuxCover is not null && !Remux.CarriesCover(output)) return RemuxError(Strings.Get("editor.remux.cover.unsupported"));
        var title = TxtRemuxFileTitle.Text?.Trim();
        return new RemuxRequest(source, output, tracks, string.IsNullOrEmpty(title) ? null : title, _remuxCover);
    }

    internal async Task<bool> RemuxToAsync(string output)
    {
        if (Exporting || RemuxRequestFor(output) is not { } request) return false;
        return await RunRemuxAsync(output, (progress, ct) => RemuxRunner.RunAsync(request, _remuxDuration, progress, ct)).ConfigureAwait(true);
    }

    /// <summary>Secili izi yeniden kodlamadan <paramref name="output"/>'a yazar; izin kendi dosyasinin ustune yazmak reddedilir.</summary>
    internal async Task<bool> ExtractRemuxToAsync(string output)
    {
        if (Exporting || _source is not { } source || SelectedRemuxRow() is not { } row) return false;
        if (PathEquality.Same(row.Track.Source ?? source, output))
        {
            RemuxError(Strings.Get("editor.remux.same-file"));
            return false;
        }

        return await RunRemuxAsync(output, (progress, ct) => RemuxRunner.ExtractAsync(source, row.Track, output, row.Duration, progress, ct)).ConfigureAwait(true);
    }

    private async Task<bool> RunRemuxAsync(string output, Func<IProgress<EncodeProgress>, CancellationToken, Task> run)
    {
        using var cts = new CancellationTokenSource();
        _exportCts = cts;
        ShowExportBar(running: true);
        SetStatus("Hint", string.Empty);
        ExportProgress.Value = 0;
        var progress = new Progress<EncodeProgress>(p => ExportProgress.Value = p.Fraction);
        try
        {
            await run(progress, cts.Token).ConfigureAwait(true);
            _exported = output;
            Finish(true, Done(output));
            return true;
        }
        catch (OperationCanceledException)
        {
            Finish(false, Strings.Get("main.run.cancelled"));
            return false;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
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

    private async Task<MediaInfo?> ProbeRemuxAsync(string file)
    {
        try
        {
            var prober = SourceProber ?? FfprobeClient.ProbeAsync;
            return await prober(file, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static IEnumerable<RemuxRow> RemuxRowsOf(MediaInfo info, string? file)
        => Remux.TracksOf(info).Select(track => new RemuxRow
        {
            Track = track with { Source = file },
            Codec = info.Streams.FirstOrDefault(stream => stream.Index == track.Index)?.Codec,
            Duration = info.DurationSeconds,
            Language = track.Language ?? string.Empty,
            Title = track.Title ?? string.Empty,
            IsDefault = track.IsDefault
        });

    private void ListRemuxRows(RemuxRow[] rows, int select)
    {
        _remuxRows = rows;
        _remuxSync = true;
        CmbRemuxTrack.ItemsSource = _remuxRows.Select(RemuxItem).ToArray();
        CmbRemuxTrack.SelectedIndex = _remuxRows.Length > select ? select : -1;
        _remuxSync = false;
        ShowRemuxRow();
    }

    private RemuxRequest? RemuxError(string message)
    {
        ExportBar.IsVisible = true;
        SetStatus("StatusError", message);
        return null;
    }

    private RemuxRow? SelectedRemuxRow()
        => CmbRemuxTrack.SelectedIndex is var index && index >= 0 && index < _remuxRows.Length ? _remuxRows[index] : null;

    private void EditRemuxRow(Action<RemuxRow> edit)
    {
        if (_remuxSync || SelectedRemuxRow() is not { } row) return;
        edit(row);
    }

    private void ShowRemuxRow()
    {
        if (_remuxSync) return;
        var row = SelectedRemuxRow();
        _remuxSync = true;
        TxtRemuxLanguage.Text = row?.Language ?? string.Empty;
        TxtRemuxTrackTitle.Text = row?.Title ?? string.Empty;
        TxtRemuxOffset.Text = row?.Offset ?? string.Empty;
        ChkRemuxInclude.IsChecked = row?.Include == true;
        ChkRemuxDefault.IsChecked = row?.IsDefault == true;
        CmbRemuxRotation.SelectedIndex = row?.Track.Kind == StreamKind.Video ? row.Rotation : 0;
        _remuxSync = false;
        var on = row is not null;
        TxtRemuxLanguage.IsEnabled = on;
        TxtRemuxTrackTitle.IsEnabled = on;
        TxtRemuxOffset.IsEnabled = on;
        ChkRemuxInclude.IsEnabled = on;
        ChkRemuxDefault.IsEnabled = on;
        CmbRemuxRotation.IsEnabled = row?.Track.Kind == StreamKind.Video;
        BtnRemux.IsEnabled = on;
        BtnRemuxExtract.IsEnabled = on;
        BtnRemuxAdd.IsEnabled = _remuxRows.Length > 0;
        BtnRemuxCover.IsEnabled = _remuxRows.Length > 0;
    }

    private static string RemuxItem(RemuxRow row)
    {
        var kind = Strings.Get(row.Track.Kind switch
        {
            StreamKind.Video => "editor.remux.kind.video",
            StreamKind.Audio => "editor.remux.kind.audio",
            StreamKind.Subtitle => "editor.remux.kind.subtitle",
            _ => "editor.remux.kind.attachment"
        });
        var text = row.Track.Index.ToString(Strings.Culture) + ": " + kind;
        if (row.Track.Source is { } file) text = Path.GetFileName(file) + " " + text;
        return row.Track.Language is { Length: > 0 } language ? text + " (" + language + ")" : text;
    }

    private async Task RemuxClickedAsync()
    {
        if (Exporting || _source is not { } source) return;
        var extension = Path.GetExtension(source);
        if (await SaveRemuxPathAsync("editor.remux.save-title", Path.GetFileNameWithoutExtension(source) + "-remux" + extension).ConfigureAwait(true) is { } output)
            await RemuxToAsync(output).ConfigureAwait(true);
    }

    private async Task RemuxExtractClickedAsync()
    {
        if (Exporting || RemuxExtractName() is not { } name) return;
        if (await SaveRemuxPathAsync("editor.remux.extract-title", name).ConfigureAwait(true) is { } output)
            await ExtractRemuxToAsync(output).ConfigureAwait(true);
    }

    private async Task<string?> SaveRemuxPathAsync(string titleKey, string suggested)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get(titleKey),
            SuggestedFileName = suggested,
            DefaultExtension = Path.GetExtension(suggested).TrimStart('.'),
            ShowOverwritePrompt = true
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    private async Task RemuxAddClickedAsync()
    {
        if (Exporting || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Strings.Get("editor.remux.add-title") }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } file) await AddRemuxSourceAsync(file).ConfigureAwait(true);
    }

    private async Task RemuxCoverClickedAsync()
    {
        if (Exporting || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.remux.cover-title"),
            FileTypeFilter = new[] { new FilePickerFileType("PNG/JPEG") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } }
        }).ConfigureAwait(true);
        SetRemuxCover(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
