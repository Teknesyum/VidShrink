using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// C1-6: B1'in yalnız CLI'da duran yüzeyi arayüze iner — ses yüksekliği (<c>loudnorm</c>),
/// sabit kazanç, dış altyazı dosyaları ve kaynağın metin altyazısını yakma. Dış altyazı ve
/// yakma o videoya aittir: yeni kaynak açılınca sıfırlanır, kuyruk penceresine taşınmaz.
/// </summary>
public partial class MainWindow
{
    internal static readonly int[] GainCandidates = { -12, -9, -6, -3, 0, 3, 6, 9, 12 };

    private readonly List<ExternalSubtitle> _externalSubtitles = new();
    private readonly List<int> _burnChoices = new();

    internal IReadOnlyList<ExternalSubtitle> ExternalSubtitles => _externalSubtitles;

    private void InitializeTrackUi()
    {
        var gain = CmbAudioGain.SelectedIndex;
        CmbAudioGain.ItemsSource = GainCandidates
            .Select(db => db == 0 ? Say("main.audio.gain.none") : db.ToString("+0;-0", CultureInfo.InvariantCulture) + " dB")
            .ToList();
        CmbAudioGain.SelectedIndex = gain >= 0 ? gain : Array.IndexOf(GainCandidates, 0);
        RefreshBurnChoices();
        RefreshSubtitleList();
    }

    /// <summary>Kaynağın metin altyazıları yakma listesine girer; görüntü altyazı (PGS) yakılamaz, listede yok.</summary>
    private void RefreshBurnChoices()
    {
        var chosen = BurnChoice;
        _burnChoices.Clear();
        var items = new List<string> { Say("main.subtitles.burn.off") };
        if (_info is { } info)
        {
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            for (var i = 0; i < subtitles.Count; i++)
            {
                if (!StreamMapping.IsTextSubtitle(subtitles[i].Codec)) continue;
                _burnChoices.Add(i);
                var label = subtitles[i].Title ?? subtitles[i].Language;
                items.Add(label is null ? $"#{i + 1}" : $"#{i + 1} {label}");
            }
        }
        CmbBurnSubtitle.ItemsSource = items;
        var at = chosen is int c ? _burnChoices.IndexOf(c) : -1;
        CmbBurnSubtitle.SelectedIndex = at + 1;
        CmbBurnSubtitle.IsEnabled = _burnChoices.Count > 0;
    }

    /// <summary>Seçilen yakma izi, kaynağın altyazıları içinde 0 tabanlı sıra; seçim yoksa <c>null</c>.</summary>
    internal int? BurnChoice
    {
        get
        {
            var index = CmbBurnSubtitle.SelectedIndex - 1;
            return index >= 0 && index < _burnChoices.Count ? _burnChoices[index] : null;
        }
    }

    private void ApplyTrackOptions(PlanOptions options)
    {
        options.AudioLoudnorm = ChkAudioLoudnorm.IsChecked == true;
        var gain = CmbAudioGain.SelectedIndex;
        options.AudioGainDb = gain >= 0 && gain < GainCandidates.Length && GainCandidates[gain] != 0 ? GainCandidates[gain] : null;
        options.ExternalSubtitles = _externalSubtitles.ToList();
        if (BurnChoice is int burn) options.Filters = options.Filters with { BurnSubtitle = burn };
    }

    /// <summary>Dosya başına bir kez eklenir; desteklenmeyen uzantı ve olmayan dosya sessizce atlanır. Eklenen sayı döner.</summary>
    internal int AddSubtitleFiles(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (ExternalSubtitle.FromFile(path) is not { } subtitle) continue;
            if (_externalSubtitles.Any(s => string.Equals(s.Path, subtitle.Path, StringComparison.OrdinalIgnoreCase))) continue;
            _externalSubtitles.Add(subtitle);
            added++;
        }
        if (added > 0) SubtitlesChanged();
        return added;
    }

    internal void RemoveSubtitle(int index)
    {
        if (index < 0 || index >= _externalSubtitles.Count) return;
        _externalSubtitles.RemoveAt(index);
        SubtitlesChanged();
    }

    /// <summary>Yeni kaynak: önceki videonun dış altyazıları ve yakma seçimi taşınmaz.</summary>
    private void ResetTracksForSource()
    {
        _externalSubtitles.Clear();
        CmbBurnSubtitle.SelectedIndex = 0;
        RefreshBurnChoices();
        RefreshSubtitleList();
    }

    private void SubtitlesChanged()
    {
        RefreshSubtitleList();
        OnOptionChanged();
    }

    internal static bool IsSubtitleFile(string path)
        => ExternalSubtitle.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private void RefreshSubtitleList()
    {
        SubtitleList.Children.Clear();
        for (var i = 0; i < _externalSubtitles.Count; i++) SubtitleList.Children.Add(SubtitleRow(i));
        SubtitleList.IsVisible = _externalSubtitles.Count > 0;
    }

    private Grid SubtitleRow(int index)
    {
        var subtitle = _externalSubtitles[index];
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = TokenOf("SpaceXs") };
        var name = new TextBlock
        {
            Text = subtitle.Language is { } language ? $"{Path.GetFileName(subtitle.Path)} · {language}" : Path.GetFileName(subtitle.Path),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("Hint") is Avalonia.Styling.ControlTheme hint) name.Theme = hint;
        ToolTip.SetTip(name, subtitle.Path);
        row.Children.Add(name);
        var size = TokenOf("IconSizeSm");
        var remove = new Button
        {
            Content = new PathIcon { Data = this.FindResource("IconClose") as Geometry, Width = size, Height = size },
            Padding = this.FindResource("ChipPadding") is Avalonia.Thickness padding ? padding : default,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("GhostButton") is Avalonia.Styling.ControlTheme ghost) remove.Theme = ghost;
        AutomationProperties.SetName(remove, Say("main.subtitles.remove"));
        ToolTip.SetTip(remove, Say("main.subtitles.remove"));
        remove.Click += (_, _) => RemoveSubtitle(index);
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);
        return row;
    }

    private double TokenOf(string key) => this.FindResource(key) is double value ? value : 0;

    private async void OnAddSubtitle(object? sender, RoutedEventArgs e)
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
            AddSubtitleFiles(files.Select(file => file.TryGetLocalPath()).OfType<string>());
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }
}
