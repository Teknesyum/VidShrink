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

    /// <summary>
    /// Kalın mono tuş ile sans açıklamanın ilk satır taban çizgileri. İkisi de satırın tepesine
    /// yaslanınca yazı tiplerinin çıkış payları farkı kadar (2-3 px) kayıyordu; tabanı yukarıda
    /// kalan blok fark kadar aşağı itilir.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var boyut = base.MeasureOverride(availableSize);
        return TabanlariHizala() ? base.MeasureOverride(availableSize) : boyut;
    }

    private bool TabanlariHizala()
    {
        var degisti = false;
        for (var i = 0; i + 1 < Rows.Children.Count; i += 2)
        {
            if (Rows.Children[i] is not TextBlock tus || Rows.Children[i + 1] is not TextBlock aciklama) continue;
            var fark = Taban(aciklama) - Taban(tus);
            degisti |= Kaydir(tus, Math.Max(0, fark));
            degisti |= Kaydir(aciklama, Math.Max(0, -fark));
        }
        return degisti;
    }

    private static double Taban(TextBlock blok)
        => blok.TextLayout.TextLines.Count > 0 ? blok.TextLayout.TextLines[0].Baseline : 0;

    private static bool Kaydir(TextBlock blok, double ust)
    {
        if (Math.Abs(blok.Margin.Top - ust) < 0.01) return false;
        blok.Margin = new Thickness(0, ust, 0, 0);
        return true;
    }

    private ControlTheme? Find(string key)
        => this.TryFindResource(key, out var value) ? value as ControlTheme : null;
}
