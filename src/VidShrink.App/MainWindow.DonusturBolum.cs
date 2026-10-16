using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// later.md #44: Dönüştür'ün bölüm açılırı. Kaynak yüklenince satırlar kaynağın bölümleriyle dolar; satırlara
/// dokunulmadıkça plan <see cref="ConversionPlan.Chapters"/>'ı boş bırakır ve ffmpeg bölümleri kendisi taşır.
/// İlk düzenlemeden sonra plan listeyi taşır; hepsi silinirse çıktı bölümsüz olur. Açılır yalnız bölüm taşıyan
/// kaplarda (mp4, mkv, mov) görünür.
/// </summary>
public partial class MainWindow
{
    private bool _convertChaptersEdited;

    private sealed record ChapterRow(Grid Row, TextBox Time, TextBox Name, Button Remove);

    private readonly List<ChapterRow> _convertChapterRows = new();

    internal IReadOnlyList<(TextBox Time, TextBox Name, Button Remove)> ConvertChapterFields
        => _convertChapterRows.Select(r => (r.Time, r.Name, r.Remove)).ToArray();

    internal bool ConvertChaptersEdited => _convertChaptersEdited;

    /// <summary>Kaynak değişti: satırlar kaynağın bölümleriyle yeniden kurulur, düzenleme izi silinir.</summary>
    private void ResetConvertChapters(MediaInfo info)
    {
        var syncing = _syncing;
        _syncing = true;
        _convertChapterRows.Clear();
        ConvertChapterList.Children.Clear();
        foreach (var chapter in info.Chapters)
            AddConvertChapterRow(TimeSpan.FromSeconds(Math.Max(0, chapter.StartSeconds)), chapter.Title ?? "");
        _syncing = syncing;
        _convertChaptersEdited = false;
        RefreshConvertChapterSummary();
    }

    internal void AddConvertChapter()
    {
        var times = _convertChapterRows.Select(r => ParseTime(r.Time.Text)).OfType<TimeSpan>().Where(t => t != TimeSpan.MinValue).ToArray();
        var at = times.Length == 0 ? TimeSpan.Zero : times.Max() + TimeSpan.FromSeconds(RecorderChapters.MinGapSeconds);
        AddConvertChapterRow(at, Say("main.convert.chapters.default", _convertChapterRows.Count + 1));
        ConvertChaptersChanged();
    }

    internal void RemoveConvertChapter(int index)
    {
        var row = _convertChapterRows[index];
        _convertChapterRows.RemoveAt(index);
        ConvertChapterList.Children.Remove(row.Row);
        ConvertChaptersChanged();
    }

    private void AddConvertChapterRow(TimeSpan start, string title)
    {
        var time = new TextBox
        {
            Text = Saat.Ekran(start, TimeSpan.FromHours(1)),
            Width = (double)this.FindResource("NumberBoxWidth")!
        };
        Avalonia.Automation.AutomationProperties.SetName(time, Say("main.convert.chapters.time"));
        var name = new TextBox { Text = title };
        Avalonia.Automation.AutomationProperties.SetName(name, Say("main.convert.chapters.name"));
        var remove = new Button
        {
            Content = Say("main.convert.chapters.remove"),
            Theme = (Avalonia.Styling.ControlTheme)this.FindResource("GhostButton")!
        };
        Avalonia.Automation.AutomationProperties.SetName(remove, Say("main.convert.chapters.remove"));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = (double)this.FindResource("SpaceSm")! };
        Grid.SetColumn(name, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(time);
        row.Children.Add(name);
        row.Children.Add(remove);
        var entry = new ChapterRow(row, time, name, remove);
        _convertChapterRows.Add(entry);
        ConvertChapterList.Children.Add(row);
        Watch(time, TextBox.TextProperty, ConvertChaptersChanged);
        Watch(name, TextBox.TextProperty, ConvertChaptersChanged);
        remove.Click += (_, _) => RemoveConvertChapter(_convertChapterRows.IndexOf(entry));
    }

    private void ConvertChaptersChanged()
    {
        if (_syncing) return;
        _convertChaptersEdited = true;
        RefreshConvertChapterSummary();
        OnConvertChanged();
    }

    private void RefreshConvertChapterSummary()
    {
        var count = _convertChapterRows.Count;
        BtnConvertChapters.Content = count == 0 ? Say("main.convert.chapters") : Say("main.convert.chapters.count", count);
        TxtConvertChaptersEmpty.IsVisible = count == 0;
    }

    /// <summary>Planın bölüm listesi; dokunulmadıysa <c>null</c>, zamanı okunamayan satır listeye girmez.</summary>
    private IReadOnlyList<ChapterEntry>? ReadConvertChapters()
        => !_convertChaptersEdited
            ? null
            : _convertChapterRows
                .Select(r => (Time: ParseTime(r.Time.Text), Name: r.Name.Text ?? ""))
                .Where(r => r.Time is { } t && t != TimeSpan.MinValue)
                .Select(r => new ChapterEntry(r.Time!.Value, r.Name))
                .ToArray();

    private string? ConvertChapterError()
        => _convertChaptersEdited && _convertChapterRows.Any(r => ParseTime(r.Time.Text) is not { } t || t == TimeSpan.MinValue)
            ? Say("main.convert.chapters.bad-time")
            : null;

    private void OnConvertChapterAdd(object? sender, RoutedEventArgs e) => AddConvertChapter();
}
