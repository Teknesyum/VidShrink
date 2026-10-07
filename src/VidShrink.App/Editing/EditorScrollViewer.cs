using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace VidShrink.App.Editing;

/// <summary>
/// Düzenleyicinin alt bölümünü taşıyan kaydırıcı. Değiştiricisiz PgUp/PgDn sayfayı kaydırır ve
/// tüketilir; değiştiricili olanı kaydırıcı görmez, olay tüketilmeden yukarı çıkar. Hazır
/// kaydırıcı değiştiriciye bakmadan ikisini de tüketiyordu.
/// </summary>
public sealed class EditorScrollViewer : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.PageUp or Key.PageDown && e.KeyModifiers != KeyModifiers.None) return;
        base.OnKeyDown(e);
    }
}
