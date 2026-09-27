using Avalonia;
using Avalonia.Controls;

namespace VidShrink.App;

internal static class UstPanel
{
    public static readonly AttachedProperty<object?> IcerikProperty =
        AvaloniaProperty.RegisterAttached<TabControl, object?>("Icerik", typeof(UstPanel));

    public static void SetIcerik(TabControl hedef, object? deger) => hedef.SetValue(IcerikProperty, deger);

    public static object? GetIcerik(TabControl hedef) => hedef.GetValue(IcerikProperty);
}
