using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal partial class PlayerShortcutsPanel : UserControl
{
    public PlayerShortcutsPanel()
    {
        InitializeComponent();
        Build();
    }

    internal IReadOnlyList<(string Gesture, string Label)> Shown
    {
        get
        {
            var shown = new List<(string, string)>();
            for (var i = 0; i + 1 < Rows.Children.Count; i += 2)
                shown.Add((((TextBlock)Rows.Children[i]).Text ?? "", ((TextBlock)Rows.Children[i + 1]).Text ?? ""));
            return shown;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        Build();
        Dispatcher.UIThread.Post(() => Root.Classes.Remove("enter"));
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

    internal void Build()
    {
        Rows.Children.Clear();
        Rows.RowDefinitions.Clear();
        var index = 0;
        foreach (var row in Keymap.Rows)
        {
            Rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var gesture = new TextBlock { Text = Keymap.Gesture(row.Input), Theme = Find("MonoValue") };
            Grid.SetRow(gesture, index);
            Grid.SetColumn(gesture, 0);

            var label = new TextBlock
            {
                Text = Keymap.Label(row),
                Theme = Find("Body"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            Grid.SetRow(label, index);
            Grid.SetColumn(label, 1);

            Rows.Children.Add(gesture);
            Rows.Children.Add(label);
            index++;
        }
    }

    private ControlTheme? Find(string key)
        => this.TryFindResource(key, out var value) ? value as ControlTheme : null;
}
