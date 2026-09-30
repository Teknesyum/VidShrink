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
    private readonly ShortcutTable _table;

    public PlayerShortcutsPanel()
    {
        InitializeComponent();
        _table = new ShortcutTable(Rows, TxtNotice, ShortcutMap.Player, Find);
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

    /// <summary>
    /// Kalın mono tuş ile sans açıklamanın ilk satır taban çizgileri. İkisi de satırın tepesine
    /// yaslanınca yazı tiplerinin çıkış payları farkı kadar (2-3 px) kayıyordu; tabanı yukarıda
    /// kalan blok fark kadar aşağı itilir.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var boyut = base.MeasureOverride(availableSize);
        return TabanlariHizala(Rows) ? base.MeasureOverride(availableSize) : boyut;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var boyut = base.ArrangeOverride(finalSize);
        if (TabanlariHizala(Rows)) Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Render);
        return boyut;
    }

    internal static bool TabanlariHizala(Grid rows)
    {
        var degisti = false;
        for (var i = 0; i + 1 < rows.Children.Count; i += 2)
        {
            if (rows.Children[i + 1] is not TextBlock aciklama) continue;
            double tusTabani;
            Control tus;
            switch (rows.Children[i])
            {
                case TextBlock blok:
                    tus = blok;
                    tusTabani = Taban(blok);
                    break;
                case ShortcutKeyButton { Content: TextBlock ic } kutu:
                    tus = kutu;
                    tusTabani = IcUst(kutu, ic) + Taban(ic);
                    break;
                default:
                    continue;
            }

            var fark = Taban(aciklama) - tusTabani;
            degisti |= Kaydir(tus, Math.Max(0, fark));
            degisti |= Kaydir(aciklama, Math.Max(0, -fark));
        }
        return degisti;
    }

    private static double IcUst(Button kutu, TextBlock ic)
    {
        if (ic.TranslatePoint(default, kutu) is { } yer && ic.Bounds.Height > 0) return yer.Y;
        var kenar = kutu.BorderThickness;
        var dolgu = kutu.Padding;
        var bos = kutu.DesiredSize.Height - kutu.Margin.Top - kutu.Margin.Bottom - kenar.Top - kenar.Bottom - dolgu.Top - dolgu.Bottom - ic.DesiredSize.Height;
        return kenar.Top + dolgu.Top + Math.Max(0, bos / 2);
    }

    private static double Taban(TextBlock blok)
        => blok.TextLayout.TextLines.Count > 0 ? blok.TextLayout.TextLines[0].Baseline : 0;

    private static bool Kaydir(Control blok, double ust)
    {
        if (Math.Abs(blok.Margin.Top - ust) < 0.01) return false;
        blok.UseLayoutRounding = false;
        if (blok is ContentControl { Content: Control ic }) ic.UseLayoutRounding = false;
        blok.Margin = new Thickness(0, ust, 0, 0);
        return true;
    }

    private ControlTheme? Find(string key)
        => this.TryFindResource(key, out var value) ? value as ControlTheme : null;
}
