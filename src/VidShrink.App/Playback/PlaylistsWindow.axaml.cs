using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-09: adlı oynatma listeleri penceresi. Üstte listeler, altta seçilenin girdileri; diskte olmayan
/// dosya uyarı biçimiyle ve "eksik" işaretiyle kalır, liste ondan dolayı değişmez. O anki kuyruk adla
/// kaydedilir, liste açılır, adlandırılır, silinir; m3u/m3u8 içe ve dışa aktarılır.
/// </summary>
internal sealed partial class PlaylistsWindow : Window
{
    private readonly NamedPlaylists _store;
    private readonly Func<IReadOnlyList<PlaylistEntry>> _current;
    private readonly Func<string, bool> _open;
    private readonly Action<string> _notify;

    public PlaylistsWindow() : this(new NamedPlaylists(Path.GetTempPath()), () => Array.Empty<PlaylistEntry>(), _ => false, _ => { })
    {
    }

    internal PlaylistsWindow(NamedPlaylists store, Func<IReadOnlyList<PlaylistEntry>> current, Func<string, bool> open, Action<string> notify)
    {
        _store = store;
        _current = current;
        _open = open;
        _notify = notify;
        InitializeComponent();
        Rows.SelectionChanged += (_, _) => OnSelected();
        Rows.DoubleTapped += (_, _) => OpenSelected();
        Rows.KeyDown += OnKey;
        NameBox.KeyDown += OnNameKey;
        Refill(null);
    }

    internal IReadOnlyList<NamedPlaylist> Shown => Rows.Items.OfType<ListBoxItem>().Select(item => (NamedPlaylist)item.Tag!).ToList();

    internal IReadOnlyList<NamedPlaylistEntry> ShownItems => Items.Items.OfType<ListBoxItem>().Select(item => (NamedPlaylistEntry)item.Tag!).ToList();

    internal IReadOnlyList<string> ShownItemTexts => Items.Items.OfType<ListBoxItem>()
        .Select(item => (item.Content as TextBlock)?.Text ?? "").ToList();

    internal TextBox NameField => NameBox;

    internal Func<Task<string?>>? ImportPicker { get; set; }

    internal Func<string, Task<string?>>? ExportPicker { get; set; }

    internal void Select(int index) => Rows.SelectedIndex = index;

    private NamedPlaylist? Selected => (Rows.SelectedItem as ListBoxItem)?.Tag as NamedPlaylist;

    internal static string Describe(NamedPlaylist list)
        => list.Missing > 0
            ? Strings.Get("player.playlists.row-missing", list.Name, list.Count, list.Missing)
            : Strings.Get("player.playlists.row", list.Name, list.Count);

    internal static string DescribeItem(NamedPlaylistEntry entry)
    {
        var name = MediaAddress.IsAddress(entry.Path) ? entry.Path : Path.GetFileName(entry.Path);
        return entry.Missing ? Strings.Get("player.playlists.missing", name) : name;
    }

    internal bool SaveCurrent()
    {
        var entries = _current();
        var name = _store.Save(NameBox.Text, entries);
        _notify(name is null ? Strings.Get("player.list.save-failed") : Strings.Get("player.list.saved", name));
        if (name is null) return false;
        Refill(name);
        return true;
    }

    internal bool OpenSelected()
    {
        if (Selected is not { } list) return false;
        var opened = _open(list.File);
        if (opened) Close();
        return opened;
    }

    internal bool RenameSelected()
    {
        if (Selected is not { } list || _store.Rename(list.Name, NameBox.Text) is not { } name) return false;
        Refill(name);
        return true;
    }

    internal bool DeleteSelected()
    {
        if (Selected is not { } list) return false;
        var index = Rows.SelectedIndex;
        if (!_store.Delete(list.Name)) return false;
        Refill(null);
        if (Rows.ItemCount > 0) Rows.SelectedIndex = Math.Min(index, Rows.ItemCount - 1);
        return true;
    }

    internal bool ImportFrom(string source)
    {
        var name = _store.Import(source);
        if (name is null)
        {
            _notify(Strings.Get("player.playlists.import-failed"));
            return false;
        }

        Refill(name);
        return true;
    }

    internal bool ExportSelectedTo(string target)
    {
        if (Selected is not { } list) return false;
        var exported = _store.Export(list.Name, target);
        _notify(exported ? Strings.Get("player.list.saved", target) : Strings.Get("player.list.save-failed"));
        return exported;
    }

    private void Refill(string? keep)
    {
        Rows.Items.Clear();
        foreach (var list in _store.List())
        {
            var item = new ListBoxItem { Tag = list, Content = new SarilanMetin { Text = Describe(list), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis } };
            Rows.Items.Add(item);
            if (keep is not null && string.Equals(list.Name, keep, StringComparison.OrdinalIgnoreCase)) Rows.SelectedItem = item;
        }

        TxtEmpty.IsVisible = Rows.ItemCount == 0;
        OnSelected();
    }

    private void OnSelected()
    {
        var list = Selected;
        Items.Items.Clear();
        if (list is not null)
        {
            var warning = this.TryFindResource("StatusWarning", out var found) ? found as ControlTheme : null;
            foreach (var entry in _store.Entries(list.File))
            {
                var text = new SarilanMetin { Text = DescribeItem(entry), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
                if (entry.Missing && warning is not null) text.Theme = warning;
                var item = new ListBoxItem { Tag = entry, Content = text };
                ToolTip.SetTip(item, entry.Path);
                Items.Items.Add(item);
            }
        }

        NameBox.Text = list?.Name ?? NameBox.Text;
        BtnOpen.IsEnabled = list is not null && list.Count > list.Missing;
        BtnRename.IsEnabled = list is not null;
        BtnDelete.IsEnabled = list is not null;
        BtnExport.IsEnabled = list is not null && list.Count > 0;
        BtnSave.IsEnabled = _current().Count > 0;
    }

    private async Task PickImportAsync()
    {
        var picker = ImportPicker ?? DefaultImportPicker;
        if (await picker().ConfigureAwait(true) is { } path) ImportFrom(path);
    }

    private async Task PickExportAsync()
    {
        if (Selected is not { } list) return;
        var picker = ExportPicker ?? DefaultExportPicker;
        if (await picker(list.Name + NamedPlaylists.Extension).ConfigureAwait(true) is { } path) ExportSelectedTo(path);
    }

    private async Task<string?> DefaultImportPicker()
    {
        if (StorageProvider is not { CanOpen: true } storage) return null;
        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.Get("player.playlists.import"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Strings.Get("player.list.files")) { Patterns = new[] { "*.m3u", "*.m3u8" } },
                    FilePickerFileTypes.All
                }
            }).ConfigureAwait(true);
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<string?> DefaultExportPicker(string suggested)
    {
        if (StorageProvider is not { CanSave: true } storage) return null;
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Strings.Get("player.playlists.export"),
                SuggestedFileName = suggested,
                DefaultExtension = PlayerView.PlaylistSaveExtension,
                ShowOverwritePrompt = true,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(Strings.Get("player.list.files")) { Patterns = new[] { "*." + PlayerView.PlaylistSaveExtension } }
                }
            }).ConfigureAwait(true);
            return file?.TryGetLocalPath();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
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
            SaveCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnSave(object? sender, RoutedEventArgs e) => SaveCurrent();

    private void OnRename(object? sender, RoutedEventArgs e) => RenameSelected();

    private void OnDelete(object? sender, RoutedEventArgs e) => DeleteSelected();

    private void OnImport(object? sender, RoutedEventArgs e) => _ = PickImportAsync();

    private void OnExport(object? sender, RoutedEventArgs e) => _ = PickExportAsync();
}
