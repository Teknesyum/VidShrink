using VidShrink.App.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-11: izleme geçmişi penceresi. Satırlar en son izlenen başta; arama kutusu dosya adına ve
/// yola göre süzer. Açma, son dosyalar menüsünün yolundan geçer: dosya yoksa oynatıcı bildirir.
/// </summary>
internal sealed partial class HistoryWindow : Window
{
    private readonly Func<string?, IReadOnlyList<HistoryRow>> _rows;
    private readonly Func<string, bool> _open;

    public HistoryWindow() : this(_ => Array.Empty<HistoryRow>(), _ => false)
    {
    }

    internal HistoryWindow(Func<string?, IReadOnlyList<HistoryRow>> rows, Func<string, bool> open)
    {
        _rows = rows;
        _open = open;
        InitializeComponent();
        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refill();
        };
        SearchBox.KeyDown += OnKey;
        Rows.KeyDown += OnKey;
        Rows.DoubleTapped += (_, _) => OpenSelected();
        Refill();
    }

    internal TextBox Search => SearchBox;

    internal IEnumerable<string> ShownPaths => Rows.Items.OfType<ListBoxItem>().Select(item => (string)item.Tag!);

    /// <summary>Seçili satırı, seçim yoksa ilk satırı açar ve pencereyi kapatır.</summary>
    internal bool OpenSelected()
    {
        var item = Rows.SelectedItem as ListBoxItem ?? Rows.Items.OfType<ListBoxItem>().FirstOrDefault();
        if (item?.Tag is not string path) return false;
        var opened = _open(path);
        Close();
        return opened;
    }

    private void Refill()
    {
        Rows.Items.Clear();
        foreach (var row in _rows(SearchBox.Text))
        {
            var name = new TextBlock { Text = Path.GetFileName(row.Path), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            var detail = new TextBlock
            {
                Text = row.Seen.ToLocalTime().ToString("g", Strings.Culture) + " · " + Path.GetDirectoryName(row.Path),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            };
            if (this.TryFindResource("Hint", out var hint) && hint is Avalonia.Styling.ControlTheme theme) detail.Theme = theme;
            var item = new ListBoxItem { Tag = row.Path, Content = new StackPanel { Children = { name, detail } } };
            ToolTip.SetTip(item, row.Path);
            Rows.Items.Add(item);
        }

        TxtEmpty.IsVisible = Rows.ItemCount == 0;
        BtnOpen.IsEnabled = Rows.ItemCount > 0;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => OpenSelected();
}
