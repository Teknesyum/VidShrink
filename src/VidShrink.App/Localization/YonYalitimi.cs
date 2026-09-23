using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.Core;

namespace VidShrink.App.Localization;

internal static class YonYalitimi
{
    private static bool _kuruldu;

    public static void Kur()
    {
        if (_kuruldu) return;
        _kuruldu = true;
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((metin, _) => Uygula(metin));
        Visual.FlowDirectionProperty.Changed.AddClassHandler<TextBlock>((metin, _) => Uygula(metin));
    }

    private static void Uygula(TextBlock metin)
    {
        if (metin.FlowDirection != FlowDirection.RightToLeft) return;
        var yazi = metin.Text;
        if (string.IsNullOrEmpty(yazi)) return;
        var yalitilmis = Bicim.Satir.Yalit(yazi);
        if (yalitilmis != yazi) metin.SetCurrentValue(TextBlock.TextProperty, yalitilmis);
    }
}
