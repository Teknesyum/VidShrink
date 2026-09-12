using System.Globalization;
using System.Text;
using System.Text.Json;

// Palet üreteci. Kaynak tek dosya: Themes/Palette/seeds.json — her temanın on bir
// çekirdek rengi. Buradaki kural o on bir renkten otuz iki anahtarlı palet dosyasını
// yazar. Elle tema düzenleyen kişi seeds.json'daki bir hex'i değiştirip bu aracı
// çalıştırır; program baştan sona boyanır.

var folder = args.Length > 0
    ? args[0]
    : Path.Combine("src", "VidShrink.App", "Themes", "Palette");

var seedFile = Path.Combine(folder, "seeds.json");
if (!File.Exists(seedFile))
{
    Console.Error.WriteLine($"Çekirdek dosyası yok: {seedFile}");
    return 1;
}

var seeds = JsonSerializer.Deserialize<List<Seed>>(
    File.ReadAllText(seedFile),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Seed>();

foreach (var stale in Directory.GetFiles(folder, "*.axaml")) File.Delete(stale);

foreach (var seed in seeds)
{
    var path = Path.Combine(folder, seed.Name + ".axaml");
    File.WriteAllText(path, Build(seed), new UTF8Encoding(false));
    Console.WriteLine($"{seed.Name,-14} {seed.Accent1} {seed.Accent2} {seed.Accent3}");
}

Console.WriteLine($"{seeds.Count} palet yazıldı: {folder}");
Console.WriteLine("Ad listesi PaletteCatalog.Names içinde; ölçü PaletteTests ikisini karşılaştırır.");
return 0;

static string Build(Seed seed)
{
    var onAccent = Luminance(seed.Accent1) > 0.45 ? "#FF000000" : "#FFFFFFFF";
    var light = Luminance(seed.Bg) > 0.5;

    return $"""
        <ResourceDictionary xmlns="https://github.com/avaloniaui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

          <!-- {seed.Note} -->
          <!-- Elle yazılmadı: Themes/Palette/seeds.json içindeki on bir çekirdek renkten
               üretildi. Değiştirmek için çekirdeği düzenle ve VidShrink.PaletteGen
               aracını çalıştır; komut docs/tema.md içinde. -->

          <Color x:Key="NeonBlueColor">{Solid(seed.Accent1)}</Color>
          <Color x:Key="NeonPinkColor">{Solid(seed.Accent2)}</Color>
          <Color x:Key="NeonPurpleColor">{Solid(seed.Accent3)}</Color>
          <Color x:Key="NeonSuccessColor">{Solid(seed.Success)}</Color>
          <Color x:Key="SurfaceToneColor">{Solid(seed.Surface)}</Color>
          <Color x:Key="AppBgColor">{Solid(seed.Bg)}</Color>
          <Color x:Key="TextBodyColor">{Solid(seed.TextBody)}</Color>
          <Color x:Key="TextDisabledColor">{Solid(seed.TextDim)}</Color>
          <Color x:Key="OnNeonColor">{onAccent}</Color>
          <Color x:Key="PinkTextColor">{Solid(Mix(seed.Accent2, "#FFFFFF", 0.33))}</Color>

          <Color x:Key="NeonBlueFillColor">{Alpha(seed.Accent1, 0x1A)}</Color>
          <Color x:Key="NeonBlueHoverColor">{Alpha(seed.Accent1, 0x33)}</Color>
          <Color x:Key="NeonBlueActiveColor">{Alpha(seed.Accent1, 0x4D)}</Color>
          <Color x:Key="NeonBlueBorderColor">{Alpha(seed.Accent1, 0x4D)}</Color>
          <Color x:Key="NeonBlueBorderStrongColor">{Alpha(seed.Accent1, 0x80)}</Color>
          <Color x:Key="NeonPinkFillColor">{Alpha(seed.Accent2, 0x1A)}</Color>
          <Color x:Key="NeonPurpleBorderColor">{Alpha(seed.Accent3, 0x80)}</Color>

          <Color x:Key="NeonEmberColor">{Solid(seed.Ember)}</Color>
          <Color x:Key="EmberFlameColor">{Solid(seed.Flame)}</Color>
          <Color x:Key="EmberBlazeColor">{Solid(seed.Blaze)}</Color>
          <Color x:Key="EmberDeepColor">{Solid(Dim(Mix(seed.Bg, seed.Ember, 0.02), light))}</Color>
          <Color x:Key="EmberMidColor">{Solid(Dim(Mix(seed.Bg, seed.Ember, 0.035), light))}</Color>
          <Color x:Key="EmberEdgeColor">{Solid(Dim(Mix(seed.Bg, seed.Ember, 0.05), light))}</Color>
          <Color x:Key="EmberBarDeepColor">{Solid(Dim(Mix(seed.Surface, seed.Ember, 0.03), light))}</Color>
          <Color x:Key="EmberBarMidColor">{Solid(Dim(Mix(seed.Surface, seed.Ember, 0.05), light))}</Color>
          <Color x:Key="EmberBarEdgeColor">{Solid(Dim(Mix(seed.Surface, seed.Ember, 0.07), light))}</Color>

          <Color x:Key="PlaybackScrimColor">{Alpha(seed.Bg, 0xCC)}</Color>
          <Color x:Key="PlaybackScrimEdgeColor">{Alpha(seed.Bg, 0x00)}</Color>

          <BoxShadows x:Key="GlowBlue">0 0 20 0 {Alpha(seed.Accent1, 0x40)}</BoxShadows>
          <BoxShadows x:Key="GlowPink">0 0 20 0 {Alpha(seed.Accent2, 0x40)}</BoxShadows>
          <BoxShadows x:Key="GlowPurple">0 0 20 0 {Alpha(seed.Accent3, 0x40)}</BoxShadows>
          <BoxShadows x:Key="GlowNone">0 0 0 0 #00000000</BoxShadows>

        </ResourceDictionary>

        """;
}

static (int R, int G, int B) Parse(string hex)
{
    var body = hex.TrimStart('#');
    if (body.Length == 8) body = body[2..];
    return (
        int.Parse(body[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(body.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(body.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}

static string Solid(string hex) => Alpha(hex, 0xFF);

static string Alpha(string hex, int alpha)
{
    var (r, g, b) = Parse(hex);
    return $"#{alpha:X2}{r:X2}{g:X2}{b:X2}";
}

// Ateş şeridi zeminin üstünde ısınır ama gövde yazısından uzaklaşır: karışım tek
// başına zemini yazıya yaklaştırıyor ve başlık çubuğundaki kontrastı düşürüyordu.
// Ölçü: ThemeBackdropTests.WarmingTheTitleBarDoesNotCostBodyTextContrast.
//
// Yön paletin zeminine bağlı. Koyu palette yazı açık olduğu için şerit koyulaşır
// (0,78 ile ölçeklenir); açık palette yazı koyu olduğu için aynı büyüklükte ters
// yöne, beyaza doğru 0,22 kadar gider. İki kolda da uzaklaşılan şey gövde yazısı.
static string Dim(string hex, bool light)
{
    if (light) return Mix(hex, "#FFFFFF", 1 - 0.78);

    var (r, g, b) = Parse(hex);
    return $"#{(int)Math.Round(r * 0.78):X2}{(int)Math.Round(g * 0.78):X2}{(int)Math.Round(b * 0.78):X2}";
}

static string Mix(string first, string second, double weight)
{
    var (r1, g1, b1) = Parse(first);
    var (r2, g2, b2) = Parse(second);
    int Blend(int a, int b) => (int)Math.Round(a + ((b - a) * weight));
    return $"#{Blend(r1, r2):X2}{Blend(g1, g2):X2}{Blend(b1, b2):X2}";
}

static double Luminance(string hex)
{
    var (r, g, b) = Parse(hex);
    double Channel(int value)
    {
        var part = value / 255.0;
        return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
    }
    return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
}

internal sealed record Seed(
    string Name, string Bg, string Surface, string Accent1, string Accent2, string Accent3,
    string Success, string Ember, string Flame, string Blaze, string TextBody, string TextDim,
    string Note);
