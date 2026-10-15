using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-15: açık dosyanın yer imi listesi. Satır zaman ve ad gösterir; seçilene gidilir, ad
/// verilir ya da silinir. Değişiklik oynatıcının geçmiş kaydına hemen yazılır.
/// </summary>
internal sealed partial class BookmarksWindow : Window
{
    private readonly Func<IReadOnlyList<Bookmark>> _rows;
    private readonly Action<double> _go;
    private readonly Func<double, string?, bool> _rename;
    private readonly Func<double, bool> _remove;

    public BookmarksWindow() : this(() => Array.Empty<Bookmark>(), _ => { }, (_, _) => false, _ => false)
    {
    }

    internal BookmarksWindow(Func<IReadOnlyList<Bookmark>> rows, Action<double> go, Func<double, string?, bool> rename, Func<double, bool> remove)
    {
        _rows = rows;
        _go = go;
        _rename = rename;
        _remove = remove;
        InitializeComponent();
        Rows.SelectionChanged += (_, _) => OnSelected();
        Rows.DoubleTapped += (_, _) => GoSelected();
        Rows.KeyDown += OnKey;
        NameBox.KeyDown += OnNameKey;
        Refill(null);
    }

    internal IReadOnlyList<Bookmark> Shown => Rows.Items.OfType<ListBoxItem>().Select(item => (Bookmark)item.Tag!).ToList();

    internal TextBox NameField => NameBox;

    internal void Select(int index) => Rows.SelectedIndex = index;

    private Bookmark? Selected => (Rows.SelectedItem as ListBoxItem)?.Tag as Bookmark;

    internal bool GoSelected()
    {
        if (Selected is not { } mark) return false;
        _go(mark.Seconds);
        Close();
        return true;
    }

    internal bool RenameSelected()
    {
        if (Selected is not { } mark || !_rename(mark.Seconds, NameBox.Text)) return false;
        Refill(mark.Seconds);
        return true;
    }

    internal bool DeleteSelected()
    {
        if (Selected is not { } mark) return false;
        var index = Rows.SelectedIndex;
        if (!_remove(mark.Seconds)) return false;
        Refill(null);
        if (Rows.ItemCount > 0) Rows.SelectedIndex = Math.Min(index, Rows.ItemCount - 1);
        return true;
    }

    internal static string Describe(Bookmark mark)
    {
        var at = Saat.Ekran(TimeSpan.FromSeconds(mark.Seconds));
        return mark.Name is null ? at : at + " · " + mark.Name;
    }

    private void Refill(double? keep)
    {
        Rows.Items.Clear();
        foreach (var mark in _rows())
        {
            var item = new ListBoxItem { Tag = mark, Content = new TextBlock { Text = Describe(mark), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis } };
            Rows.Items.Add(item);
            if (keep is { } at && Math.Abs(mark.Seconds - at) < PlaybackHistory.BookmarkMergeSeconds) Rows.SelectedItem = item;
        }

        TxtEmpty.IsVisible = Rows.ItemCount == 0;
        OnSelected();
    }

    private void OnSelected()
    {
        var mark = Selected;
        NameBox.Text = mark?.Name ?? "";
        NameBox.IsEnabled = mark is not null;
        BtnGo.IsEnabled = mark is not null;
        BtnRename.IsEnabled = mark is not null;
        BtnDelete.IsEnabled = mark is not null;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            GoSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnNameKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RenameSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnGo(object? sender, RoutedEventArgs e) => GoSelected();

    private void OnRename(object? sender, RoutedEventArgs e) => RenameSelected();

    private void OnDelete(object? sender, RoutedEventArgs e) => DeleteSelected();
}
