using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

/// <summary>
/// Ayarlar sayfasindaki katlanir "Gelismis" kumesi. Durum oynaticinin kendisinde durur;
/// panel yalniz okur ve ayni yollari cagirir, boylece sag tik menusuyle tek kaynaktan gider.
/// </summary>
internal partial class PlayerAdvancedPanel : UserControl
{
    private PlayerView? _player;

    public PlayerAdvancedPanel()
    {
        InitializeComponent();
        Build();
    }

    internal PlayerView? Player
    {
        get => _player;
        set
        {
            if (_player is not null) _player.AdvancedChanged -= OnAdvancedChanged;
            _player = value;
            if (_player is not null) _player.AdvancedChanged += OnAdvancedChanged;
            Build();
        }
    }

    internal bool IsOpen => Body.IsVisible;

    internal IReadOnlyList<string> Shown
    {
        get
        {
            var shown = new List<string>();
            foreach (var child in Rows.Children.OfType<TextBlock>()) shown.Add(child.Text ?? "");
            foreach (var child in Switches.Children.OfType<CheckBox>()) shown.Add(child.Content as string ?? "");
            return shown;
        }
    }

    internal string Summary => TxtSummary.Text ?? "";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        Build();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) Build();
        else Dispatcher.UIThread.Post(Build);
    }

    private void OnAdvancedChanged(object? sender, EventArgs e) => Build();

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        Body.IsVisible = !Body.IsVisible;
        Glyph.Text = Body.IsVisible ? "▴" : "▾";
    }

    private void OnReset(object? sender, RoutedEventArgs e) => _player?.ResetAdvanced();

    internal void Build()
    {
        Rows.Children.Clear();
        Rows.RowDefinitions.Clear();
        Switches.Children.Clear();
        if (_player is not { } player)
        {
            TxtSummary.Text = "";
            return;
        }

        var index = 0;
        foreach (var knob in PlayerView.Knobs)
        {
            var which = knob;
            Rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var label = new TextBlock { Text = player.KnobLabel(which), Theme = Find("Body") as ControlTheme, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            Grid.SetRow(label, index);
            Grid.SetColumn(label, 0);

            var less = Step("-", () => player.NudgePicture(which, -1));
            Grid.SetRow(less, index);
            Grid.SetColumn(less, 1);

            var more = Step("+", () => player.NudgePicture(which, 1));
            Grid.SetRow(more, index);
            Grid.SetColumn(more, 2);

            Rows.Children.Add(label);
            Rows.Children.Add(less);
            Rows.Children.Add(more);
            index++;
        }

        Switches.Children.Add(Toggle("player.advanced.deinterlace", player.Advanced.Picture.Deinterlace, player.ToggleDeinterlace));
        Switches.Children.Add(Toggle("player.advanced.normalize", player.Advanced.Sound.Normalize, player.ToggleNormalize));
        Switches.Children.Add(Toggle("player.advanced.boost", player.Advanced.Sound.Boost, player.ToggleBoost));

        var state = player.Advanced.State().Select(part => Strings.Get(PlayerView.StateKey(part))).ToList();
        TxtSummary.Text = state.Count == 0 ? Strings.Get("player.advanced.state-off") : string.Join(", ", state);
    }

    private Button Step(string glyph, Action act)
    {
        var button = new Button { Content = glyph, Theme = Find("GhostButton") as ControlTheme };
        button.Click += (_, _) => act();
        return button;
    }

    private CheckBox Toggle(string key, bool on, Action act)
    {
        var box = new CheckBox { Content = Strings.Get(key), IsChecked = on, Theme = Find("CheckStyle") as ControlTheme };
        box.Click += (_, _) => act();
        return box;
    }

    private object? Find(string key) => this.TryFindResource(key, out var value) ? value : null;
}
