using System.ComponentModel;
using System.Text.Json;
using Avalonia.Data;

namespace VidShrink.App.Localization;

public static class Etiketler
{
    internal const string Kaynak = "VidShrink.App.Labels.labels.";

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Diller = new(StringComparer.Ordinal);

    public static bool Kapsar(string? dil) => Dil(dil) == dil;

    public static string Get(string key) => Get(key, Strings.Language);

    public static string Get(string key, string? dil)
        => Tablo(Dil(dil)).TryGetValue(key, out var deger) ? deger : Tablo("en")[key];

    public static IReadOnlyDictionary<string, string> Tablo(string dil)
    {
        lock (Diller)
        {
            if (Diller.TryGetValue(dil, out var bilinen)) return bilinen;
            using var akis = typeof(Etiketler).Assembly.GetManifestResourceStream($"{Kaynak}{dil}.json")
                ?? throw new InvalidOperationException(Kaynak + dil);
            var tablo = JsonSerializer.Deserialize(akis, CatalogJson.Default.DictionaryStringString)
                ?? new Dictionary<string, string>();
            Diller[dil] = tablo;
            return tablo;
        }
    }

    private static string Dil(string? dil) => string.Equals(dil, "tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
}

public sealed class EtiketMetni : INotifyPropertyChanged
{
    private static readonly Dictionary<string, EtiketMetni> Bilinen = new(StringComparer.Ordinal);

    private static readonly CompiledBindingPath DegerYolu =
        CompiledBinding.Create<EtiketMetni, string>(metin => metin.Value).Path!;

    private readonly string _key;

    private EtiketMetni(string key)
    {
        _key = key;
        Strings.Changed += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        Binding = new CompiledBinding(DegerYolu) { Source = this, Mode = BindingMode.OneWay };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => Etiketler.Get(_key);

    internal CompiledBinding Binding { get; }

    public static EtiketMetni For(string key)
    {
        lock (Bilinen)
        {
            if (Bilinen.TryGetValue(key, out var bilinen)) return bilinen;
            var yeni = new EtiketMetni(key);
            Bilinen[key] = yeni;
            return yeni;
        }
    }
}

public sealed class EtiketExtension
{
    public EtiketExtension()
    {
    }

    public EtiketExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider provider) => EtiketMetni.For(Key).Binding;
}
