using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal partial class ShellShortcutsPanel : UserControl, IShortcutCard
{
    private readonly ShortcutTable _table;

    public ShellShortcutsPanel()
    {
        InitializeComponent();
        _table = new ShortcutTable(Rows, TxtNotice, ShortcutMap.Shell, Find);
        Build();
    }

    internal IReadOnlyList<(string Gesture, string Label)> Shown => _table.Shown;

    public ShortcutTable Table => _table;

    public string Heading => Strings.Get("settings.shell-shortcuts.title") + " " + Strings.Get("settings.shell-shortcuts.hint");

    public bool IsOpen
    {
        get => Body.IsVisible;
        set
        {
            Body.IsVisible = value;
            Glyph.Classes.Set("open", value);
        }
    }

    private void OnToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => IsOpen = !IsOpen;

    private void OnReset(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _table.Reset();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        _table.Attach();
        Build();
        Dispatcher.UIThread.Post(() => Root.Classes.Remove("enter"));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        _table.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) Relabel();
        else Dispatcher.UIThread.Post(Relabel);
    }

    private void Relabel()
    {
        _table.Notice = "";
        Build();
    }

    internal void Build() => _table.Build();

    protected override Size MeasureOverride(Size availableSize)
    {
        var boyut = base.MeasureOverride(availableSize);
        return PlayerShortcutsPanel.TabanlariHizala(Rows) ? base.MeasureOverride(availableSize) : boyut;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var boyut = base.ArrangeOverride(finalSize);
        if (PlayerShortcutsPanel.TabanlariHizala(Rows)) Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Render);
        return boyut;
    }

    private ControlTheme? Find(string key)
        => this.TryFindResource(key, out var value) ? value as ControlTheme : null;
}
