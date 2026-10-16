using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace VidShrink.App.Playback;

/// <summary>
/// Kısayol tablosunun tuş metni. Tavana (<c>ShortcutKeyMaxWidth</c>) sığdıkça tek satır; sığmazsa
/// yalnız boşlukta sarılır, tavan en uzun boşluksuz parçadan küçük olamaz. Tavansız tuş sütunu
/// açıklamayı ~120 px'e sıkıştırıp uk "повноекранног|о", el "Αντικατοπτρισμ|ός" bölüyordu; düz
/// tavan da boşluksuz tuşu ("&lt;Ctrl+Shift+Rotellin|a&gt;") bölüyordu. Hep sarılan metin kendi
/// genişliğine yerleşirken yuvarlamayla ikinci satıra düşebildiği için (th) geçiş tek yönlü,
/// metin değişince sıfırlanır.
/// </summary>
internal sealed class KeyText : TextBlock
{
    public KeyText() => TextWrapping = TextWrapping.NoWrap;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>Tavan; kaynak bulunamazsa sınırsız (metin hep tek satır).</summary>
    internal double Cap => this.TryFindResource("ShortcutKeyMaxWidth", out var value) && value is double width ? width : double.PositiveInfinity;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (TextWrapping == TextWrapping.NoWrap)
        {
            var size = base.MeasureOverride(availableSize);
            if (size.Width <= Math.Min(availableSize.Width, Cap)) return size;
            TextWrapping = TextWrapping.Wrap;
        }

        var limit = Math.Min(availableSize.Width, Math.Max(Cap, LongestSegment()));
        return base.MeasureOverride(availableSize.WithWidth(limit));
    }

    private double LongestSegment()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        return (Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                using var layout = new TextLayout(part, typeface, FontSize, Foreground);
                return Math.Ceiling(layout.WidthIncludingTrailingWhitespace);
            })
            .DefaultIfEmpty(0)
            .Max();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) TextWrapping = TextWrapping.NoWrap;
    }
}
