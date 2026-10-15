using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// DON-P2: Dönüştür'ün altyazı izleri. Kaynağın her altyazı izi bir kutuyla çıktıya girer ya da
/// girmez, dış .srt/.ass dosyaları ek girdi olur; kabın taşıyamadığı iz küçültmenin kuralıyla
/// çevrilir ya da düşer. Metin izi ayrı kısa bir komutla kaynağın yanına dosya olarak kaydedilir.
/// Seçim o videoya aittir: yeni kaynak açılınca sıfırlanır.
/// </summary>
public partial class MainWindow
{
    private readonly List<ExternalSubtitle> _convertSubtitles = new();
    private readonly List<CheckBox> _convertTrackChecks = new();

    /// <summary>Altyazı çıkarma komutu; testler sahtesini verir.</summary>
    internal Func<MediaInfo, int, string, CancellationToken, Task> SubtitleExtractor { get; set; }
        = (info, ordinal, path, ct) => new EncodeRunner().ExtractSubtitleAsync(info, ordinal, path, ct);

    internal IReadOnlyList<ExternalSubtitle> ConvertExternalSubtitles => _convertSubtitles;

    internal IReadOnlyList<CheckBox> ConvertTrackChecks => _convertTrackChecks;

    /// <summary>Kutuların hepsi işaretliyse <c>null</c> (bütün izler), değilse işaretli izlerin sırası.</summary>
    internal IReadOnlyList<int>? ConvertSubtitleChoice()
    {
        if (_convertTrackChecks.All(check => check.IsChecked == true)) return null;
        return Enumerable.Range(0, _convertTrackChecks.Count).Where(i => _convertTrackChecks[i].IsChecked == true).ToList();
    }

    private void ResetConvertSubtitles()
    {
        _convertSubtitles.Clear();
        _convertTrackChecks.Clear();
        ConvertSubtitleTracks.Children.Clear();
        TxtConvertSubtitleResult.Text = "";
        TxtConvertSubtitleResult.IsVisible = false;
        if (_info is { } info)
        {
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            for (var i = 0; i < subtitles.Count; i++) ConvertSubtitleTracks.Children.Add(ConvertTrackRow(subtitles[i], i));
        }
        ConvertSubtitleTracks.IsVisible = _convertTrackChecks.Count > 0;
        ConvertSubtitlePanel.IsVisible = _info is not null;
        RefreshConvertSubtitleList();
    }

    private Grid ConvertTrackRow(SourceStream track, int ordinal)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = TokenOf("SpaceXs") };
        var name = track.Title ?? track.Language;
        var label = (name is null ? $"#{ordinal + 1}" : $"#{ordinal + 1} {name}") + " · " + track.Codec;
        var text = new TextBlock { Text = label, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        if (this.FindResource("Body") is Avalonia.Styling.ControlTheme body) text.Theme = body;
        var check = new CheckBox { Content = text, IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        if (this.FindResource("CheckStyle") is Avalonia.Styling.ControlTheme checkStyle) check.Theme = checkStyle;
        AutomationProperties.SetName(check, label);
        check.IsCheckedChanged += (_, _) => RefreshConversion();
        _convertTrackChecks.Add(check);
        row.Children.Add(check);
        if (SubtitleExtraction.Extension(track.Codec) is not null)
        {
            var save = new Button
            {
                Content = Say("main.convert.subtitles.extract"),
                Padding = this.FindResource("ButtonPaddingSm") is Avalonia.Thickness padding ? padding : default,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (this.FindResource("GhostButton") is Avalonia.Styling.ControlTheme ghost) save.Theme = ghost;
            AutomationProperties.SetName(save, $"{Say("main.convert.subtitles.extract")} {label}");
            save.Click += async (_, _) => await ExtractConvertSubtitleAsync(ordinal, save);
            Grid.SetColumn(save, 1);
            row.Children.Add(save);
        }
        return row;
    }

    /// <summary>Kabın altyazı taşıyıp taşımadığına göre kutuları ve ekleme düğmesini açar, ipucunu yazar.</summary>
    private void RefreshConvertSubtitleState(ConversionPlan plan)
    {
        var carried = ConversionArguments.SubtitleContainer(plan) is not null;
        foreach (var check in _convertTrackChecks) check.IsEnabled = carried;
        BtnConvertAddSubtitle.IsEnabled = carried;
        var hint = !carried ? Say("main.convert.subtitles.not-carried")
            : _convertTrackChecks.Count == 0 ? Say("main.convert.subtitles.none")
            : "";
        TxtConvertSubtitleHint.Text = hint;
        TxtConvertSubtitleHint.IsVisible = hint.Length > 0;
    }

    /// <summary>Kabın çevirdiği ya da düşürdüğü altyazılar için gerekçe satırları; Dönüştür'de olmayan kutuyu önermez.</summary>
    private IEnumerable<string> ConvertSubtitleNotes(ConversionPlan plan)
    {
        if (_info is null) return Array.Empty<string>();
        var notes = new List<StreamNote>();
        ConversionArguments.Subtitles(_info, plan, notes);
        return notes.Distinct().Select(note => Say(ConvertStreamNoteKey(note)));
    }

    internal static string ConvertStreamNoteKey(StreamNote note)
        => note is StreamNote.ImageSubtitleDropped or StreamNote.ImageSubtitleDroppedByContainer
            ? "main.convert.subtitles.image-dropped"
            : StreamNoteKey(note);

    /// <summary>Dosya başına bir kez eklenir; desteklenmeyen uzantı ve olmayan dosya atlanır. Eklenen sayı döner.</summary>
    internal int AddConvertSubtitleFiles(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (ExternalSubtitle.FromFile(path) is not { } subtitle) continue;
            if (_convertSubtitles.Any(s => string.Equals(s.Path, subtitle.Path, StringComparison.OrdinalIgnoreCase))) continue;
            _convertSubtitles.Add(subtitle);
            added++;
        }
        if (added > 0) ConvertSubtitlesChanged();
        return added;
    }

    internal void RemoveConvertSubtitle(int index)
    {
        if (index < 0 || index >= _convertSubtitles.Count) return;
        _convertSubtitles.RemoveAt(index);
        ConvertSubtitlesChanged();
    }

    private void ConvertSubtitlesChanged()
    {
        RefreshConvertSubtitleList();
        RefreshConversion();
    }

    private void RefreshConvertSubtitleList()
    {
        ConvertSubtitleList.Children.Clear();
        for (var i = 0; i < _convertSubtitles.Count; i++)
        {
            var index = i;
            ConvertSubtitleList.Children.Add(SubtitleRow(_convertSubtitles[i], () => RemoveConvertSubtitle(index)));
        }
        ConvertSubtitleList.IsVisible = _convertSubtitles.Count > 0;
    }

    private async void OnConvertAddSubtitle(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.subtitles.file-type")) { Patterns = ExternalSubtitle.Extensions.Select(x => "*" + x).ToArray() },
                    FilePickerFileTypes.All
                }
            });
            AddConvertSubtitleFiles(files.Select(file => file.TryGetLocalPath()).OfType<string>());
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }

    /// <summary>Kaynağın yanına <c>&lt;ad&gt;.&lt;dil|#n&gt;&lt;uzantı&gt;</c>; ad doluysa <c>-2</c>, <c>-3</c>…</summary>
    internal static string ConvertSubtitleTarget(MediaInfo info, int ordinal)
    {
        var track = SubtitleExtraction.Track(info, ordinal) ?? throw new ArgumentOutOfRangeException(nameof(ordinal));
        var extension = SubtitleExtraction.Extension(track.Codec) ?? throw new ArgumentException(track.Codec, nameof(ordinal));
        var folder = Path.GetDirectoryName(Path.GetFullPath(info.FilePath)) ?? ".";
        var tag = string.IsNullOrWhiteSpace(track.Language) ? (ordinal + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : track.Language;
        var stem = Path.GetFileNameWithoutExtension(info.FilePath) + "." + tag;
        var path = Path.Combine(folder, stem + extension);
        for (var n = 2; File.Exists(path); n++) path = Path.Combine(folder, $"{stem}-{n}{extension}");
        return path;
    }

    internal async Task ExtractConvertSubtitleAsync(int ordinal, Button? button = null)
    {
        if (_info is not { } info) return;
        if (button is not null) button.IsEnabled = false;
        try
        {
            var target = ConvertSubtitleTarget(info, ordinal);
            await SubtitleExtractor(info, ordinal, target, CancellationToken.None);
            TxtConvertSubtitleResult.Text = string.Format(Strings.Culture, Say("main.convert.subtitles.extracted"), Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            TxtConvertSubtitleResult.Text = string.Format(Strings.Culture, Say("main.convert.subtitles.extract-failed"), ex.Message);
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
        TxtConvertSubtitleResult.IsVisible = true;
    }
}
