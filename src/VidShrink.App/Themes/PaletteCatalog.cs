using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace VidShrink.App.Themes;

/// <summary>
/// Yürürlükteki palet. Renk programın hiçbir yerinde yazılı değil; hepsi
/// <c>Themes/Palette/&lt;ad&gt;.axaml</c> içinde durur. Ölçüler <c>Theme.axaml</c>'de
/// kaldığı için palet değişince yalnız renk değişir, yerleşim yerinde kalır.
///
/// <para>Seçim çalışırken yürürlüğe girer, yeniden başlatma istemez. Bunun iki adımı var
/// ve ikisi de gerekli. Birincisi sözlük: birleşmiş sözlüğün ilk sırasındaki palet
/// dosyası yenisiyle değiştirilir, böylece bundan sonra kurulan her fırça doğru renkle
/// doğar. İkincisi <b>yaşayan fırçalar</b>: programdaki bütün başvurular
/// <c>{StaticResource}</c> olduğu için fırça nesneleri bir kez kurulup denetimlerin
/// içinde saklanır ve sözlüğün değişmesi onlara ulaşmaz. O yüzden kurulmuş her fırça
/// yerinde boyanır — eski paletin rengi yenisininkiyle değiştirilir. Fırça nesnesi aynı
/// kaldığı için onu tutan her denetim kendiliğinden yeniden çizer.</para>
///
/// <para>Eşleme anahtar üzerinden değil <b>değer</b> üzerinden kurulur: iki palet
/// dosyasının aynı anahtarları taşıdığı bilindiği için eski değerden yeni değere bir
/// tablo çıkarılır. Bir eski renk iki ayrı yeni renge düşüyorsa (paletler arasında
/// bölünmüş bir değer) o giriş tabloya hiç alınmaz; yanlış boyamaktansa o fırça eski
/// rengiyle kalır. Ölçü: <c>PaletteApplyTests</c>.</para>
///
/// <para>Liste elle yazılı: derlenmiş XAML kaynakları çalışırken sayılamıyor
/// (<c>AssetLoader.GetAssets</c> yalnız ham kaynakları görüyor, denendi ve boş döndü).
/// Listenin klasörle aynı kalması <c>PaletteTests</c>'e bırakıldı; yeni palet eklenip
/// buraya yazılmazsa ölçü kırmızı yanar.</para>
/// </summary>
public static class PaletteCatalog
{
    private const string Folder = "avares://VidShrink.App/Themes/Palette";

    /// <summary>Açık ve koyu paleti ayıran zemin parlaklığı.</summary>
    private const double LightThreshold = 0.5;

    public const string Default = "Neon";

    /// <summary>
    /// Sıra <c>seeds.json</c> ile aynı: varsayılan başta, sonra koyu paletler tanınırlık
    /// sırasında, en sonda açık zeminli olanlar.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "Neon", "Dracula", "Nord", "Gruvbox",
        "TokyoNight", "Catppuccin", "OneDark", "Monokai",
        "Solarized", "Everforest", "RosePine", "Ayu",
        "NightOwl", "Synthwave", "Cobalt", "MaterialOcean",
        "Github", "Kanagawa", "Horizon", "Moonlight",
        "SolarizedLight", "GithubLight", "CatppuccinLatte",
        "RosePineDawn", "AyuLight", "GruvboxLight"
    };

    /// <summary>Yürürlükte olan paletin adı.</summary>
    public static string Current { get; private set; } = Default;

    /// <summary>
    /// Listede görünen ad: dosya adı tek kelime, burada büyük harften bölünür —
    /// <c>TokyoNight</c> ekranda <c>Tokyo Night</c> olur.
    /// </summary>
    public static string Label(string name)
        => string.Concat(name.Select((letter, at) =>
            at > 0 && char.IsUpper(letter) && !char.IsUpper(name[at - 1]) ? " " + letter : letter.ToString()));

    /// <summary>
    /// Paletin zemini açık mı? Elle tutulan bir liste yok; karar zemin renginin
    /// parlaklığından çıkar, yani yeni bir palet eklendiğinde kendiliğinden doğru olur.
    /// </summary>
    public static bool IsLight(string name)
        => Background(Load(Resolve(name))) is { } colour && Luminance(colour) > LightThreshold;

    /// <summary>
    /// Adı verilen paleti yürürlüğe koyar ve gerçekten uygulanan adı döndürür. Tanınmayan
    /// ad varsayılana düşer: elle düzenlenmiş bir ayar dosyası programı açılışta durdurmaz.
    /// </summary>
    public static string Use(string? name)
    {
        var wanted = Resolve(name);

        if (Application.Current is not { } app)
        {
            Current = wanted;
            return wanted;
        }

        var incoming = Load(wanted);
        var recolour = Recolour(Load(Current), incoming);

        if (app.Resources.MergedDictionaries is { Count: > 0 } merged)
            merged[0] = new ResourceInclude((Uri?)null) { Source = Address(wanted) };

        Repaint(app, recolour);

        app.RequestedThemeVariant = Background(incoming) is { } colour && Luminance(colour) > LightThreshold
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        Current = wanted;
        return wanted;
    }

    private static string Resolve(string? name)
        => Names.FirstOrDefault(palette => string.Equals(palette, name, StringComparison.OrdinalIgnoreCase))
           ?? Default;

    private static Uri Address(string name) => new($"{Folder}/{name}.axaml");

    private static IResourceDictionary Load(string name)
        => new ResourceInclude((Uri?)null) { Source = Address(name) }.Loaded;

    private static Color? Background(IResourceDictionary palette)
        => palette.TryGetValue("AppBgColor", out var value) && value is Color colour ? colour : null;

    /// <summary>Eski paletin renginden yeni paletinkine tablo; bölünmüş değerler dışarıda kalır.</summary>
    private static IReadOnlyDictionary<Color, Color> Recolour(IResourceDictionary from, IResourceDictionary to)
    {
        var map = new Dictionary<Color, Color>();
        var split = new HashSet<Color>();

        foreach (var key in from.Keys)
        {
            if (!from.TryGetValue(key, out var was) || was is not Color old) continue;
            if (!to.TryGetValue(key, out var now) || now is not Color fresh) continue;

            if (map.TryGetValue(old, out var already) && already != fresh) split.Add(old);
            else map[old] = fresh;
        }

        foreach (var ambiguous in split) map.Remove(ambiguous);
        return map;
    }

    /// <summary>
    /// Kurulmuş her fırçayı yerinde boyar. Anahtarlar sözlüklerden toplanır, nesneler
    /// <see cref="Application.TryGetResource(object, ThemeVariant, out object)"/> ile
    /// alınır: sözlükler XAML'ı geç kuruyor, doğrudan değer okumak kurulmamış bir kalem
    /// döndürebilir.
    /// </summary>
    private static void Repaint(Application app, IReadOnlyDictionary<Color, Color> recolour)
    {
        if (recolour.Count == 0) return;

        var painted = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var key in Keys(app))
        {
            if (app.TryGetResource(key, null, out var value)) Paint(value, recolour, painted);
        }
    }

    /// <summary>Uygulamanın kaynak sözlüklerinde ve biçem kapsamında bildirilen bütün anahtarlar.</summary>
    private static IReadOnlyCollection<object> Keys(Application app)
    {
        var keys = new HashSet<object>();

        void FromDictionary(IResourceDictionary? dictionary)
        {
            if (dictionary is null) return;
            foreach (var key in dictionary.Keys) keys.Add(key);
            foreach (var merged in dictionary.MergedDictionaries) FromProvider(merged);
        }

        void FromProvider(IResourceProvider? provider)
        {
            switch (provider)
            {
                case ResourceInclude include: FromDictionary(include.Loaded); break;
                case IResourceDictionary dictionary: FromDictionary(dictionary); break;
            }
        }

        void FromStyle(IStyle? style)
        {
            switch (style)
            {
                case StyleInclude include: FromStyle(include.Loaded); break;
                case Styles styles:
                    FromDictionary(styles.Resources);
                    foreach (var child in styles) FromStyle(child);
                    break;
                case ControlTheme theme:
                    FromDictionary(theme.Resources);
                    foreach (var child in theme.Children) FromStyle(child);
                    break;
                case Style plain:
                    FromDictionary(plain.Resources);
                    foreach (var child in plain.Children) FromStyle(child);
                    break;
            }
        }

        FromDictionary(app.Resources);
        FromStyle(app.Styles);
        return keys;
    }

    private static void Paint(object? value, IReadOnlyDictionary<Color, Color> recolour, HashSet<object> painted)
    {
        if (value is null || !painted.Add(value)) return;

        switch (value)
        {
            case SolidColorBrush solid:
                if (recolour.TryGetValue(solid.Color, out var fresh)) solid.Color = fresh;
                break;

            case GradientBrush gradient:
                foreach (var stop in gradient.GradientStops)
                    if (recolour.TryGetValue(stop.Color, out var moved)) stop.Color = moved;
                break;

            case DrawingBrush drawing:
                Paint(drawing.Drawing, recolour, painted);
                break;

            case DrawingGroup group:
                foreach (var child in group.Children) Paint(child, recolour, painted);
                break;

            case GeometryDrawing geometry:
                Paint(geometry.Brush, recolour, painted);
                Paint(geometry.Pen?.Brush, recolour, painted);
                break;

            case ControlTheme theme:
                foreach (var setter in theme.Setters)
                    if (setter is Setter typed) Paint(typed.Value, recolour, painted);
                foreach (var child in theme.Children) Paint(child, recolour, painted);
                break;

            case Style style:
                foreach (var setter in style.Setters)
                    if (setter is Setter typed) Paint(typed.Value, recolour, painted);
                foreach (var child in style.Children) Paint(child, recolour, painted);
                break;
        }
    }

    /// <summary>sRGB bağıl parlaklık; eşik <see cref="LightThreshold"/>.</summary>
    private static double Luminance(Color colour)
    {
        static double Channel(byte value)
        {
            var part = value / 255.0;
            return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));
    }
}
