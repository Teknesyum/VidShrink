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
/// Teslim acilirindaki kap ici iz bolumu (DZA-85 ilk yarisi). Kaynagin izleri acilir ilk acildiginda
/// <see cref="SourceProber"/> ile bir kez okunur; secilen izin dili, adi, varsayilan bayragi, kaydirmasi ve donusu
/// duzenlenir ve kaynak <see cref="RemuxRunner"/> ile yeniden kodlanmadan yeni dosyaya yazilir. Cizelgeye ve teslim
/// planina dokunmaz. Bos dil ve ad kaynaktaki degeri birakir; adi bulunan izde bos ad onu temizler.
/// </summary>
internal partial class EditorView
{
    private sealed class RemuxRow
    {
        internal required RemuxTrack Track { get; init; }
        internal string Language { get; set; } = string.Empty;
        internal string Title { get; set; } = string.Empty;
        internal bool IsDefault { get; set; }
        internal string Offset { get; set; } = "0";
        internal int Rotation { get; set; }
    }

    private static readonly int?[] RemuxRotations = { null, 0, 90, 180, 270 };

    private RemuxRow[] _remuxRows = Array.Empty<RemuxRow>();
    private string? _remuxFor;
    private double _remuxDuration;
    private bool _remuxSync;

    private void InitRemux()
    {
        BtnExportOptions.Flyout!.Opened += (_, _) => _ = LoadRemuxAsync();
        BtnRemux.Click += (_, _) => _ = RemuxClickedAsync();
        CmbRemuxTrack.SelectionChanged += (_, _) => ShowRemuxRow();
        TxtRemuxLanguage.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Language = TxtRemuxLanguage.Text ?? string.Empty); };
        TxtRemuxTrackTitle.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Title = TxtRemuxTrackTitle.Text ?? string.Empty); };
        TxtRemuxOffset.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) EditRemuxRow(row => row.Offset = TxtRemuxOffset.Text ?? string.Empty); };
        ChkRemuxDefault.IsCheckedChanged += (_, _) => EditRemuxRow(row => row.IsDefault = ChkRemuxDefault.IsChecked == true);
        CmbRemuxRotation.SelectionChanged += (_, _) => EditRemuxRow(row => row.Rotation = Math.Max(0, CmbRemuxRotation.SelectedIndex));
        ShowRemuxRow();
    }

    /// <summary>Kaynagin izlerini okur; ayni kaynak icin ikinci kez okumaz. Okunamazsa bolum kapali kalir.</summary>
    internal async Task LoadRemuxAsync()
    {
        if (_source is not { } source || PathEquality.Same(source, _remuxFor)) return;
        MediaInfo info;
        try
        {
            var prober = SourceProber ?? FfprobeClient.ProbeAsync;
            info = await prober(source, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            return;
        }

        if (!PathEquality.Same(source, _source)) return;
        _remuxFor = source;
        _remuxDuration = info.DurationSeconds;
        _remuxRows = Remux.TracksOf(info).Select(track => new RemuxRow
        {
            Track = track,
            Language = track.Language ?? string.Empty,
            Title = track.Title ?? string.Empty,
            IsDefault = track.IsDefault
        }).ToArray();
        _remuxSync = true;
        CmbRemuxTrack.ItemsSource = _remuxRows.Select(RemuxItem).ToArray();
        CmbRemuxTrack.SelectedIndex = _remuxRows.Length > 0 ? 0 : -1;
        _remuxSync = false;
        ShowRemuxRow();
    }

    /// <summary>
    /// Kutulardaki degerlerden istek kurar. Gecersiz dil ya da kaydirma, ya da kaynagin ustune yazma durum satirina hata
    /// yazar ve <c>null</c> doner.
    /// </summary>
    internal RemuxRequest? RemuxRequestFor(string output)
    {
        if (_source is not { } source || _remuxRows.Length == 0) return null;
        if (PathEquality.Same(source, output)) return RemuxError(Strings.Get("editor.remux.same-file"));
        var tracks = new List<RemuxTrack>();
        foreach (var row in _remuxRows)
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

        var title = TxtRemuxFileTitle.Text?.Trim();
        return new RemuxRequest(source, output, tracks, string.IsNullOrEmpty(title) ? null : title);
    }

    internal async Task<bool> RemuxToAsync(string output)
    {
        if (Exporting || RemuxRequestFor(output) is not { } request) return false;
        using var cts = new CancellationTokenSource();
        _exportCts = cts;
        ShowExportBar(running: true);
        SetStatus("Hint", string.Empty);
        ExportProgress.Value = 0;
        var progress = new Progress<EncodeProgress>(p => ExportProgress.Value = p.Fraction);
        try
        {
            await RemuxRunner.RunAsync(request, _remuxDuration, progress, cts.Token).ConfigureAwait(true);
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

    private RemuxRequest? RemuxError(string message)
    {
        ExportBar.IsVisible = true;
        SetStatus("StatusError", message);
        return null;
    }

    private void EditRemuxRow(Action<RemuxRow> edit)
    {
        if (_remuxSync || CmbRemuxTrack.SelectedIndex is var index && (index < 0 || index >= _remuxRows.Length)) return;
        edit(_remuxRows[index]);
    }

    private void ShowRemuxRow()
    {
        if (_remuxSync) return;
        var index = CmbRemuxTrack.SelectedIndex;
        var row = index >= 0 && index < _remuxRows.Length ? _remuxRows[index] : null;
        _remuxSync = true;
        TxtRemuxLanguage.Text = row?.Language ?? string.Empty;
        TxtRemuxTrackTitle.Text = row?.Title ?? string.Empty;
        TxtRemuxOffset.Text = row?.Offset ?? string.Empty;
        ChkRemuxDefault.IsChecked = row?.IsDefault == true;
        CmbRemuxRotation.SelectedIndex = row?.Track.Kind == StreamKind.Video ? row.Rotation : 0;
        _remuxSync = false;
        var on = row is not null;
        TxtRemuxLanguage.IsEnabled = on;
        TxtRemuxTrackTitle.IsEnabled = on;
        TxtRemuxOffset.IsEnabled = on;
        ChkRemuxDefault.IsEnabled = on;
        CmbRemuxRotation.IsEnabled = row?.Track.Kind == StreamKind.Video;
        BtnRemux.IsEnabled = on;
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
        return row.Track.Language is { Length: > 0 } language ? text + " (" + language + ")" : text;
    }

    private async Task RemuxClickedAsync()
    {
        if (Exporting || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || _source is not { } source) return;
        var extension = Path.GetExtension(source);
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.Get("editor.remux.save-title"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(source) + "-remux" + extension,
            DefaultExtension = extension.TrimStart('.'),
            ShowOverwritePrompt = true
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is { } output) await RemuxToAsync(output).ConfigureAwait(true);
    }
}
