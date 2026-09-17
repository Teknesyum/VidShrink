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

    /// <summary>
    /// <c>Value</c>'ya giden yol, tek bir kez kuruluyor. <see cref="CompiledBinding.Create{TIn,TOut}"/>
    /// bir lambda ifadesini gezip yolu çıkarır; her bağ için ayrı kurulsa bu iş 493 kez
    /// yapılırdı. Yol bir değer gibi taşınabildiği için kaynak bağ başına değişiyor, yol
    /// değişmiyor.
    /// </summary>
    private static readonly CompiledBindingPath ValuePath =
        CompiledBinding.Create<LocalizedText, string>(text => text.Value).Path!;

    private readonly string _key;

    private LocalizedText(string key)
    {
        _key = key;
        Strings.Changed += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        Binding = new CompiledBinding(ValuePath) { Source = this, Mode = BindingMode.OneWay };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => LanguageCatalog.Display(Strings.Get(_key));

    /// <summary>
    /// Anahtarın bağı. <see cref="BindingBase"/> hedef başına kendi anlatımını kurduğu için
    /// aynı nesne kaç öğeye verilirse verilsin yeter; anahtar başına bir tane tutulur ve
    /// biçimleme aynı nesneyi kaç kez isterse alır.
    /// </summary>
    internal CompiledBinding Binding { get; }

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
///
/// <para>Bağ derlenmiş bağdır, yansımalı değil: <c>new Binding("Value")</c> yol dizgesini
/// çalışma anında ayrıştırıp özelliği yansımayla arıyordu ve biçimlemede 493 yerde
/// kuruluyordu. AOT çözümlemesi bu çağrıyı IL2026/IL3050 ile işaretliyor
/// (<c>docs/olcumler/hipersurus-h.md</c> H4).</para>
/// </summary>
public sealed class TextExtension
{
    public TextExtension()
    {
    }

    public TextExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider provider) => LocalizedText.For(Key).Binding;
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
