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
    private readonly List<int> _convertBurnChoices = new();

    internal static readonly string[] BurnColorChoices = { "FFFFFF", "FFFF00", "00FFFF", "00FF00" };

    private static readonly string[] BurnColorKeys =
    {
        "main.subtitles.style.color.white", "main.subtitles.style.color.yellow",
        "main.subtitles.style.color.cyan", "main.subtitles.style.color.green"
    };

    /// <summary>DON-P3: Dönüştür'de görüntüye gömülen iz, kaynağın altyazıları içinde 0 tabanlı sıra; seçim yoksa <c>null</c>.</summary>
    internal int? ConvertBurnChoice
    {
        get
        {
            var index = CmbConvertBurn.SelectedIndex - 1;
            return index >= 0 && index < _convertBurnChoices.Count ? _convertBurnChoices[index] : null;
        }
    }

    private bool ConvertBurnsText
    {
        get
        {
            if (ConvertBurnChoice is not int burn || _info is not { } info) return false;
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            return burn < subtitles.Count && StreamMapping.IsTextSubtitle(subtitles[burn].Codec);
        }
    }

    private string? ConvertBurnFont
        => CmbConvertBurnFont.SelectedIndex is var i && i >= 1 && i <= BurnFontChoices.Count ? BurnFontChoices[i - 1] : null;

    private int? ConvertBurnFontSize
        => CmbConvertBurnSize.SelectedIndex is var i && i >= 1 && i <= BurnSizeCandidates.Length ? BurnSizeCandidates[i - 1] : null;

    private string? ConvertBurnColor
        => CmbConvertBurnColor.SelectedIndex is var i && i >= 1 && i <= BurnColorChoices.Length ? BurnColorChoices[i - 1] : null;

    private void InitializeConvertBurnUi()
    {
        var font = CmbConvertBurnFont.SelectedIndex;
        CmbConvertBurnFont.ItemsSource = new[] { Say("main.subtitles.style.default") }.Concat(BurnFontChoices).ToList();
        CmbConvertBurnFont.SelectedIndex = Math.Max(font, 0);
        var size = CmbConvertBurnSize.SelectedIndex;
        CmbConvertBurnSize.ItemsSource = new[] { Say("main.subtitles.style.default") }
            .Concat(BurnSizeCandidates.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();
        CmbConvertBurnSize.SelectedIndex = Math.Max(size, 0);
        var color = CmbConvertBurnColor.SelectedIndex;
        CmbConvertBurnColor.ItemsSource = new[] { Say("main.subtitles.style.default") }.Concat(BurnColorKeys.Select(Say)).ToList();
        CmbConvertBurnColor.SelectedIndex = Math.Max(color, 0);
        RefreshConvertBurnChoices();
    }

    /// <summary>
    /// Küçült'ün yakma listesiyle aynı kural: metin izi yalnız ffmpeg'de <c>subtitles</c> süzgeci varsa
    /// (yoklama okunamadıysa da) girer, görüntü izi (PGS, VOBSUB, DVB) bindirmeyle her zaman.
    /// </summary>
    private void RefreshConvertBurnChoices()
    {
        var chosen = ConvertBurnChoice;
        _convertBurnChoices.Clear();
        var items = new List<string> { Say("main.subtitles.burn.off") };
        if (_info is { } info)
        {
            var textBurn = TextBurnAvailable;
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            for (var i = 0; i < subtitles.Count; i++)
            {
                var text = StreamMapping.IsTextSubtitle(subtitles[i].Codec);
                if (text ? !textBurn : !StreamMapping.IsImageSubtitle(subtitles[i].Codec)) continue;
                _convertBurnChoices.Add(i);
                var label = subtitles[i].Title ?? subtitles[i].Language;
                items.Add(label is null ? $"#{i + 1}" : $"#{i + 1} {label}");
            }
        }
        CmbConvertBurn.ItemsSource = items;
        var at = chosen is int c ? _convertBurnChoices.IndexOf(c) : -1;
        CmbConvertBurn.SelectedIndex = at + 1;
        CmbConvertBurn.IsEnabled = _convertBurnChoices.Count > 0;
        ConvertBurnStylePanel.IsVisible = ConvertBurnsText;
    }

    private void OnConvertBurnChanged()
    {
        ConvertBurnStylePanel.IsVisible = ConvertBurnsText;
        OnConvertChanged();
    }

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
        CmbConvertBurn.SelectedIndex = 0;
        CmbConvertBurnFont.SelectedIndex = 0;
        CmbConvertBurnSize.SelectedIndex = 0;
        CmbConvertBurnColor.SelectedIndex = 0;
        ChkConvertBurnBox.IsChecked = false;
        RefreshConvertBurnChoices();
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
        var text = new SarilanMetin { Text = label, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
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
