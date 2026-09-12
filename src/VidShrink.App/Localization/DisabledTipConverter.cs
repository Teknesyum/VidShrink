using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VidShrink.App.Localization;

/// <summary>
/// Kapalı bir denetimin ipucu: <c>IsEnabled</c> true iken gizli (null), false iken
/// <paramref name="parameter"/> ile verilen anahtarın o anki dildeki metni. Tek bağlı
/// (IsEnabled) olduğu için dil değişimi yalnızca bir sonraki aç/kapa geçişinde görünür.
/// </summary>
public sealed class DisabledTipConverter : IValueConverter
{
    public static readonly DisabledTipConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true || parameter is not string key
            ? null
            : LanguageCatalog.Display(Strings.Get(key));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
