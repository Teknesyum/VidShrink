using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private readonly Dictionary<Control, bool> _aramaOncesiGorunur = new();
    private readonly Dictionary<IShortcutCard, bool> _aramaOncesiAcik = new();

    private void TrackShellShortcuts()
    {
        AddHandler(KeyDownEvent, OnKabukTusu, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnKabukBasisi, RoutingStrategies.Bubble);
        Tabs.SelectionChanged += (_, _) => TxtSettingsSearch.IsVisible = ReferenceEquals(Tabs.SelectedItem, TabSettings);
        TxtSettingsSearch.IsVisible = ReferenceEquals(Tabs.SelectedItem, TabSettings);
        TxtSettingsSearch.TextChanged += (_, _) => AyarlariSuz(TxtSettingsSearch.Text);
        Strings.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => AyarlariSuz(TxtSettingsSearch.Text));
    }

    private void OnKabukTusu(object? sender, KeyEventArgs e)
    {
        if (e.Handled || ShortcutKeyButton.IsListening) return;
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control && TxtSettingsSearch.IsVisible)
        {
            TxtSettingsSearch.Focus();
            TxtSettingsSearch.SelectAll();
            e.Handled = true;
            return;
        }

        if (RunShell(ShellKeymap.For(e.Key, e.KeyModifiers))) e.Handled = true;
    }

    private void OnKabukBasisi(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || ShortcutKeyButton.IsListening) return;
        if (EditorView.PressedButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind) is not { } button) return;
        if (RunShell(ShellKeymap.ForPress(button))) e.Handled = true;
    }

    internal bool RunShell(ShellCommand command)
    {
        switch (command)
        {
            case ShellCommand.TabPlayer: return SelectTab(TabPlayer);
            case ShellCommand.TabEditor: return SelectTab(TabEditor);
            case ShellCommand.TabShrink: return SelectTab(TabShrink);
            case ShellCommand.TabConvert: return SelectTab(TabConvert);
            case ShellCommand.TabRecorder: return SelectTab(TabRecorder);
            case ShellCommand.TabSettings: return SelectTab(TabSettings);
            case ShellCommand.OpenFile:
                _ = OpenFromShortcutAsync();
                return true;
            case ShellCommand.NextRegion: return MoveRegion(1);
            case ShellCommand.PreviousRegion: return MoveRegion(-1);
            case ShellCommand.Help:
                DisariAc(HelpAddress(Strings.Language, TabRecorder.IsSelected));
                return true;
            default: return false;
        }
    }

    private bool SelectTab(TabItem tab)
    {
        if (!tab.IsVisible) return false;
        Tabs.SelectedIndex = Tabs.Items.IndexOf(tab);
        return true;
    }

    internal async Task OpenFromShortcutAsync()
    {
        if (TabPlayer.IsSelected || TabEditor.IsSelected)
        {
            var editor = TabEditor.IsSelected;
            try
            {
                var paths = await (SourcePicker ?? PickSourcesAsync)(false);
                if (paths.Count == 0) return;
                if (editor) await OpenInEditorAsync(paths[0]);
                else await OpenInPlayerAsync(paths[0]);
            }
            catch (Exception ex)
            {
                ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
            }

            return;
        }

        if (!TabShrink.IsSelected && !TabConvert.IsSelected) Tabs.SelectedIndex = ShrinkTabIndex;
        await BrowseAsync(true);
    }

    internal static string HelpAddress(string? language, bool recorder)
    {
        var turkish = string.Equals(language, "tr", StringComparison.OrdinalIgnoreCase);
        var address = Destek.DepoAdresi + "/blob/main/docs/" + (turkish ? "kullanim.tr.md" : "kullanim.md");
        if (!recorder) return address;
        return address + (turkish ? "#kaydedici-sekmesi" : "#the-recorder-tab");
    }

    private void OnOpenUserGuide(object? sender, RoutedEventArgs e) => DisariAc(HelpAddress(Strings.Language, false));

    private IReadOnlyList<Control?> Regions()
        => new Control?[] { Tabs.SelectedItem as TabItem, (Tabs.SelectedItem as TabItem)?.Content as Control, TitleBarRight };

    internal bool MoveRegion(int step)
    {
        var regions = Regions();
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var current = -1;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i] is { } root && focused is not null && (ReferenceEquals(root, focused) || root.IsVisualAncestorOf(focused)))
            {
                current = i;
                break;
            }
        }

        if (current < 0 && step < 0) current = 0;
        for (var n = 1; n <= regions.Count; n++)
        {
            var index = ((current + step * n) % regions.Count + regions.Count) % regions.Count;
            if (FirstFocusable(regions[index]) is { } target && target.Focus(NavigationMethod.Tab)) return true;
        }

        return false;
    }

    private static InputElement? FirstFocusable(Control? root)
    {
        if (root is null || !root.IsEffectivelyVisible) return null;
        return root.GetSelfAndVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled && KeyboardNavigation.GetIsTabStop(element));
    }

    internal void AyarlariSuz(string? text)
    {
        var query = (text ?? "").Trim();
        var cards = SettingsCards.Children.OfType<Control>().ToList();
        if (query.Length == 0)
        {
            if (_aramaOncesiGorunur.Count == 0) return;
            foreach (var card in cards)
            {
                if (card is IShortcutCard shortcuts)
                {
                    shortcuts.Table.Filter = "";
                    if (_aramaOncesiAcik.TryGetValue(shortcuts, out var open)) shortcuts.IsOpen = open;
                }

                if (_aramaOncesiGorunur.TryGetValue(card, out var visible)) card.IsVisible = visible;
            }

            _aramaOncesiGorunur.Clear();
            _aramaOncesiAcik.Clear();
            TxtSettingsSearchEmpty.IsVisible = false;
            return;
        }

        if (_aramaOncesiGorunur.Count == 0)
        {
            foreach (var card in cards)
            {
                _aramaOncesiGorunur[card] = card.IsVisible;
                if (card is IShortcutCard shortcuts) _aramaOncesiAcik[shortcuts] = shortcuts.IsOpen;
            }
        }

        var shown = 0;
        foreach (var card in cards)
        {
            var allowed = _aramaOncesiGorunur.TryGetValue(card, out var before) ? before : card.IsVisible;
            bool fits;
            if (card is IShortcutCard shortcuts)
            {
                var heading = SettingsSearch.Matches(shortcuts.Heading, query);
                shortcuts.Table.Filter = heading ? "" : query;
                fits = heading || shortcuts.Table.Matches(query) > 0;
                if (fits) shortcuts.IsOpen = true;
            }
            else fits = SettingsSearch.CardMatches(card, query);

            card.IsVisible = allowed && fits;
            if (card.IsVisible) shown++;
        }

        TxtSettingsSearchEmpty.Text = Say("settings.search.empty", query);
        TxtSettingsSearchEmpty.IsVisible = shown == 0;
    }
}
