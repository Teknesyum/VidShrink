using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VidShrink.App.Playback;

/// <summary>
/// Kısayol tablosunun tuş metni. Tek satırda kalır; yalnız tavana (<c>ShortcutKeyMaxWidth</c>)
/// sığmazsa boşlukta sarılır. Hep sarılan metin kendi ölçtüğü genişliğe yerleşirken yuvarlamayla
/// ikinci satıra düşebiliyordu (th <c>&lt;ปุ่มเมนู&gt;</c>); geçiş tek yönlü, metin değişince sıfırlanır.
/// </summary>
internal sealed class KeyText : TextBlock
{
    public KeyText() => TextWrapping = TextWrapping.NoWrap;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (TextWrapping != TextWrapping.NoWrap || size.Width <= availableSize.Width) return size;
        TextWrapping = TextWrapping.Wrap;
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) TextWrapping = TextWrapping.NoWrap;
    }
}
