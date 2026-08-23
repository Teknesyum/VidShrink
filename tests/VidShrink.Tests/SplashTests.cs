using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// Panel görüntüsü derleme sırasında üretildiği için önce başlatıcının derlenmiş olması
/// gerekir. Üretilmemişse ölçüm atlanır, yanlış bir yeşil verilmez.
/// </summary>
public sealed class SplashImageFactAttribute : FactAttribute
{
    public SplashImageFactAttribute()
    {
        if (!File.Exists(SplashTests.ImagePath))
            Skip = "obj/splash.png is missing, so the launcher was not built and the splash image was not checked.";
    }
}

/// <summary>Başlatıcı derlenmemişse ikili dosya üzerinde yapılan ölçümler atlanır.</summary>
public sealed class LauncherBinaryFactAttribute : FactAttribute
{
    public LauncherBinaryFactAttribute()
    {
        if (SplashTests.LauncherPath is null)
            Skip = "No built launcher was found under src/VidShrink.Launcher/bin, so the binary was not inspected.";
    }
}

public sealed class SplashTests
{
    private const string TokenChunkKeyword = "vidshrink-tokens";

    private static readonly string Root = FindRoot();

    public static readonly string ImagePath =
        Path.Combine(Root, "src", "VidShrink.Launcher", "obj", "splash.png");

    public static readonly string ThemePath =
        Path.Combine(Root, "src", "VidShrink.App", "Themes", "Theme.axaml");

    public static string? LauncherPath => Directory
        .EnumerateFiles(Path.Combine(Root, "src", "VidShrink.Launcher"), "VidShrink.exe", SearchOption.AllDirectories)
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault();

    /// <summary>
    /// K3: görüntü elle hazırlanmış olamaz. İçine gömülen her belirtecin değeri
    /// Theme.axaml'daki değerin aynısı olmalı; tema değişip görüntü eskirse bu ölçüm
    /// kırmızıya döner.
    /// </summary>
    [SplashImageFact]
    public void SplashImageCarriesTheThemeTokensItWasBuiltFrom()
    {
        var tokens = ReadTokens(File.ReadAllBytes(ImagePath));
        var theme = ReadTheme();

        Assert.NotEmpty(tokens);
        foreach (var (key, value) in tokens)
        {
            // Türetilmiş yerleşim kutuları temada yok; onlar ayrıca ölçülüyor.
            if (key is "panel" or "track" or "title" or "status") continue;
            Assert.True(theme.ContainsKey(key), $"Theme.axaml içinde {key} belirteci yok.");
            Assert.Equal(theme[key], value);
        }
    }

    /// <summary>K3: renkler temanın paletinden; görüntüde uydurulmuş renk olamaz.</summary>
    [SplashImageFact]
    public void EveryColourInTheImageComesFromThePalette()
    {
        var tokens = ReadTokens(File.ReadAllBytes(ImagePath));
        var palette = ReadTheme()
            .Where(pair => pair.Value.StartsWith('#'))
            .Select(pair => pair.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var colours = tokens.Where(pair => pair.Value.StartsWith('#')).ToList();
        Assert.NotEmpty(colours);
        foreach (var (key, value) in colours)
            Assert.True(palette.Contains(value), $"{key} rengi temada yok: {value}");
    }

    /// <summary>
    /// K2/K3: panelin ölçüleri de temadan geliyor. Yükseklik dolgu, satır yüksekliği,
    /// aralık ve çubuk yüksekliğinin toplamı; genişlik ipucu genişliği.
    /// </summary>
    [SplashImageFact]
    public void PanelGeometryIsDerivedFromSpacingTokens()
    {
        var tokens = ReadTokens(File.ReadAllBytes(ImagePath));
        double Number(string key) => double.Parse(tokens[key].Split(',')[0], CultureInfo.InvariantCulture);
        double[] Box(string key) => tokens[key].Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();

        var padding = Number("PanelPadding");
        var line = Number("LineHeightBody");
        var expectedHeight = padding + line + Number("SpaceSm") + line
                             + Number("SpaceLg") + Number("ProgressBarHeight") + padding;

        var panel = Box("panel");
        Assert.Equal(Number("TipMaxWidth"), panel[2]);
        Assert.Equal(expectedHeight, panel[3]);

        // Yazı ve çubuk panel dolgusunun içinde kalmalı.
        foreach (var key in new[] { "title", "status", "track" })
        {
            var box = Box(key);
            Assert.Equal(panel[0] + padding, box[0]);
            Assert.Equal(panel[2] - (padding * 2), box[2]);
        }
    }

    /// <summary>
    /// K2: yazı ve gezen parçanın düştüğü alan panelin donuk iç bölgesinde kalmalı.
    /// GDI alfayı sıfırladığı için o alan sonradan geri donuklaştırılıyor; yuvarlak
    /// köşelere taşarsa köşeler kare görünürdü.
    /// </summary>
    [SplashImageFact]
    public void DrawnContentStaysInsideTheOpaqueInterior()
    {
        var tokens = ReadTokens(File.ReadAllBytes(ImagePath));
        double Number(string key) => double.Parse(tokens[key].Split(',')[0], CultureInfo.InvariantCulture);
        Assert.True(Number("PanelPadding") >= Number("RadiusPanelScalar"));
    }

    /// <summary>
    /// Başlatıcı bir arayüz programı olarak kalmalı: alt sistem 3 olsaydı her açılışta
    /// bir konsol penceresi yanıp sönerdi.
    /// </summary>
    [LauncherBinaryFact]
    public void LauncherStaysAGuiBinary()
    {
        var bytes = File.ReadAllBytes(LauncherPath!);
        var pe = BitConverter.ToInt32(bytes, 0x3C);
        Assert.Equal("PE\0\0", Encoding.ASCII.GetString(bytes, pe, 4));
        // COFF başlığı 20 bayt; alt sistem isteğe bağlı başlığın 68. baytında.
        var subsystem = BitConverter.ToUInt16(bytes, pe + 4 + 20 + 68);
        Assert.Equal(2, subsystem);
    }

    /// <summary>
    /// K1'in kaynaktaki karşılığı: eşik bir sabit ve panel yalnız o sayaç dolduğunda
    /// kuruluyor. Eşiğin düşürülmesi ya da sayacın kaldırılması buradan görülür.
    /// </summary>
    [Fact]
    public void ThresholdIsFourHundredMillisecondsAndGuardsEveryDraw()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Splash.cs"));
        Assert.Contains("Threshold = TimeSpan.FromMilliseconds(400)", source);

        // Pencere yalnız Show içinde kuruluyor, Show da yalnız sayaçtan çağrılıyor.
        Assert.Contains("new Timer(_ => gate.Show(), null, Threshold", source);
        Assert.Single(Regex.Matches(source, @"SplashWindow\.Create\(\)"));

        var program = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Program.cs"));
        Assert.Contains("using (SplashGate.Arm(", program);
    }

    /// <summary>
    /// K1: gezen parçanın turu tema belirteçlerinden türüyor ve T22'deki 1440 ms'lik
    /// turdan belirgin biçimde yavaş. Sabit bir süreye dönülürse buradan görülür.
    /// </summary>
    [SplashImageFact]
    public void SweepPeriodComesFromMotionTokensAndIsSlowerThanBefore()
    {
        var tokens = ReadTokens(File.ReadAllBytes(ImagePath));
        var source = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Splash.cs"));

        Assert.Contains("_art.Duration(\"MotionSlow\")", source);
        Assert.Contains("_art.Number(\"MotionStaggerCount\")", source);

        var period = TimeSpan.Parse(tokens["MotionSlow"], CultureInfo.InvariantCulture).TotalMilliseconds
                     * double.Parse(tokens["MotionStaggerCount"], CultureInfo.InvariantCulture) * 2;
        var before = double.Parse(tokens["MotionStaggerMs"], CultureInfo.InvariantCulture) * 36;
        Assert.True(period >= before * 2, $"Tur {period} ms; {before} ms'den belirgin biçimde yavaş değil.");
    }

    /// <summary>
    /// K2/K3: parçanın iki gradyan ucu ve iki metin rengi de belirteçten okunuyor;
    /// başlatıcıda elle yazılmış renk yok.
    /// </summary>
    [Fact]
    public void PanelColoursAreReadFromTokensOnly()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Splash.cs"));
        foreach (var key in new[] { "NeonBlueColor", "NeonPurpleColor", "TextBodyColor" })
            Assert.Contains($"ColorRef(\"{key}\")", source);

        // Renk sabitlenmiş olamaz: 0x00BBGGRR biçiminde elle yazılmış bir değer yok.
        Assert.Empty(Regex.Matches(source, @"CreateSolidBrush\(\s*0x"));
    }

    private static Dictionary<string, string> ReadTokens(byte[] png)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var offset = 8;
        while (offset + 8 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "tEXt")
            {
                var text = Encoding.ASCII.GetString(png, offset + 8, length);
                var split = text.IndexOf('\0');
                if (split > 0 && text[..split] == TokenChunkKeyword)
                {
                    foreach (var pair in text[(split + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var equals = pair.IndexOf('=');
                        if (equals > 0) tokens[pair[..equals]] = pair[(equals + 1)..];
                    }
                }
            }
            if (type == "IEND") break;
            offset += length + 12;
        }
        return tokens;
    }

    private static Dictionary<string, string> ReadTheme()
    {
        var text = File.ReadAllText(ThemePath);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
                     text, @"<(?<tag>Color|x:Double|x:Int32|x:String|sys:TimeSpan|CornerRadius|Thickness|FontFamily|BoxShadows)\s+x:Key=""(?<key>[^""]+)""\s*>(?<value>[^<]*)</\1>"))
        {
            values[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }
        return values;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VidShrink.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
