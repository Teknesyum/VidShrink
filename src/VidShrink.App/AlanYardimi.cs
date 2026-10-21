using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using VidShrink.App.Localization;

namespace VidShrink.App;

public static class AlanYardimi
{
    public static readonly AttachedProperty<bool> EtiketRozetiProperty =
        AvaloniaProperty.RegisterAttached<Button, bool>("EtiketRozeti", typeof(AlanYardimi));

    internal static readonly AttachedProperty<Button?> RozetProperty =
        AvaloniaProperty.RegisterAttached<Control, Button?>("Rozet", typeof(AlanYardimi));

    static AlanYardimi()
        => Control.LoadedEvent.AddClassHandler<Button>((rozet, _) => Bagla(rozet));

    public static void SetEtiketRozeti(Button rozet, bool value) => rozet.SetValue(EtiketRozetiProperty, value);

    public static bool GetEtiketRozeti(Button rozet) => rozet.GetValue(EtiketRozetiProperty);

    internal static Button? Rozet(Control alan) => alan.GetValue(RozetProperty);

    internal static Control? Alan(Button rozet)
    {
        if (rozet.Parent is not Control etiket || etiket.Parent is not Panel kap) return null;
        foreach (var kardes in kap.Children.Skip(kap.Children.IndexOf(etiket) + 1))
        {
            var aday = new[] { kardes }
                .Concat(kardes.GetLogicalDescendants().OfType<Control>())
                .FirstOrDefault(c => c is InputElement { Focusable: true, IsTabStop: true } && !(c is Button b && GetEtiketRozeti(b)));
            if (aday is not null) return aday;
        }
        return null;
    }

    internal static string Duz(string? ipucu)
        => string.Join(" ", (ipucu ?? string.Empty)
            .Split('\n')
            .Select(satir => satir.Trim().TrimStart('•').Trim())
            .Where(satir => satir.Length > 0));

    internal static bool Ac(Control alan)
    {
        if (Rozet(alan) is not { } rozet || ToolTip.GetTip(rozet) is null) return false;
        ToolTip.SetIsOpen(rozet, true);
        return true;
    }

    private static void Bagla(Button rozet)
    {
        if (!GetEtiketRozeti(rozet) || Alan(rozet) is not { } alan || Rozet(alan) is not null) return;
        alan.SetValue(RozetProperty, rozet);
        if (ToolTip.GetTip(rozet) is TextBlock ipucu)
        {
            AutomationProperties.SetHelpText(alan, Duz(Bullets.GetText(ipucu)));
            ipucu.PropertyChanged += (_, e) =>
            {
                if (e.Property == Bullets.TextProperty) AutomationProperties.SetHelpText(alan, Duz(Bullets.GetText(ipucu)));
            };
        }
        alan.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.F1 || e.KeyModifiers != KeyModifiers.None) return;
            e.Handled = Ac(alan);
        }, RoutingStrategies.Bubble);
        alan.LostFocus += (_, _) => ToolTip.SetIsOpen(rozet, false);
    }
}
