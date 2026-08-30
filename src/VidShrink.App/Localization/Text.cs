using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace VidShrink.App.Localization;

/// <summary>
/// Bir anahtarın ekrandaki hâli. Anahtar başına tek örnek yaşar ve
/// <see cref="Strings.Changed"/>'e bir kez abone olur; her pencere kendi kopyasını
/// kurmadığı için ölçüm yüzlerce pencere açsa da abone sayısı anahtar sayısını geçmez.
/// </summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    private static readonly Dictionary<string, LocalizedText> Known = new(StringComparer.Ordinal);

    private readonly string _key;

    private LocalizedText(string key)
    {
        _key = key;
        Strings.Changed += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => LanguageCatalog.Display(Strings.Get(_key));

    public static LocalizedText For(string key)
    {
        lock (Known)
        {
            if (Known.TryGetValue(key, out var known)) return known;
            var made = new LocalizedText(key);
            Known[key] = made;
            return made;
        }
    }
}

/// <summary>
/// Biçimlemedeki <c>{loc:Text anahtar}</c>. Bir bağ döndürür, dolayısıyla dil değişince
/// metin kendiliğinden yenilenir ve kimsenin görsel ağacı gezmesi gerekmez.
/// </summary>
public sealed class TextExtension
{
    public TextExtension()
    {
    }

    public TextExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public Binding ProvideValue(IServiceProvider provider)
        => new(nameof(LocalizedText.Value))
        {
            Source = LocalizedText.For(Key),
            Mode = BindingMode.OneWay
        };
}

/// <summary>
/// Madde işaretli gövde. <see cref="TextBlock.Text"/>'e bağ vermek koşuları silerdi:
/// yuvarlak işaret ayrı bir <c>Run</c> ve kendi rengini taşıyor. Bağ bu yüzden metni
/// buraya yazar, boyayıcı da koşuları yeniden kurar.
/// </summary>
public static class Bullets
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(Bullets));

    static Bullets()
        => TextProperty.Changed.AddClassHandler<TextBlock, string?>((block, args) =>
            MainWindow.PaintBullets(block, args.NewValue.GetValueOrDefault() ?? string.Empty));

    public static void SetText(TextBlock block, string? value) => block.SetValue(TextProperty, value);

    public static string? GetText(TextBlock block) => block.GetValue(TextProperty);
}
