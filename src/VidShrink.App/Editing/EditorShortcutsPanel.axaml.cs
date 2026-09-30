using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;

namespace VidShrink.App.Editing;

internal partial class EditorShortcutsPanel : UserControl
{
    private readonly ShortcutTable _table;

    public EditorShortcutsPanel()
    {
        InitializeComponent();
        _table = new ShortcutTable(Rows, TxtNotice, ShortcutMap.Editor, Find);
        Build();
    }

    internal IReadOnlyList<(string Gesture, string Label)> Shown => _table.Shown;

    internal ShortcutTable Table => _table;

    /// <summary>
    /// Liste açık mı. Tek başına kurulan panel açık gelir; Ayarlar sayfası kapalı kurar ki
    /// tuş listesi sayfayı kaydırmaya zorlamasın (oynatıcı listesi tek başına 1817 px).
    /// </summary>
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
        var size = base.MeasureOverride(availableSize);
        return PlayerShortcutsPanel.TabanlariHizala(Rows) ? base.MeasureOverride(availableSize) : size;
    }

    private ControlTheme? Find(string key) => EditorTokens.Theme(this, key);
}
