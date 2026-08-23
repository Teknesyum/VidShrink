using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.SplashGen;

/// <summary>
/// Başlatıcı bekleme panelinin arka planını üretir. Girdi <c>Theme.axaml</c>, çıktı bir
/// PNG ve onun içine gömülü belirteç listesi.
///
/// Elle hazırlanmış bir PNG kullanılmıyor: tema değiştiğinde görüntü sessizce eskir ve
/// arayüzle uyumsuzlaşır. Buradaki her renk ve her ölçü temadan okunur, hiçbiri bu
/// dosyada sabit değildir. Kullanılan belirteçler PNG'nin tEXt bölümüne yazılır;
/// başlatıcı çalışma anındaki renklerini de oradan alır, testler de oradan doğrular.
///
/// Kullanım: vidshrink-splashgen &lt;Theme.axaml&gt; &lt;cikti.png&gt;
/// </summary>
internal static class Program
{
    /// <summary>PNG'ye gömülen belirteç listesinin anahtarı.</summary>
    public const string TokenChunkKeyword = "vidshrink-tokens";

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Kullanım: vidshrink-splashgen <Theme.axaml> <cikti.png>");
            return 1;
        }

        var theme = Theme.Read(args[0]);
        var image = Compose(theme, out var tokens);

        var directory = Path.GetDirectoryName(Path.GetFullPath(args[1]));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(args[1], Png.Encode(image, TokenChunkKeyword, tokens));
        return 0;
    }

    /// <summary>
    /// Paneli çizer. Ölçülerin tamamı temanın aralık, yarıçap ve yükseklik
    /// belirteçlerinden türetilir; hiçbir sayı burada uydurulmaz.
    /// </summary>
    private static Raster Compose(Theme theme, out string tokens)
    {
        var padding = theme.Number("PanelPadding");
        var spaceSm = theme.Number("SpaceSm");
        var spaceLg = theme.Number("SpaceLg");
        var line = theme.Number("LineHeightBody");
        var bar = theme.Number("ProgressBarHeight");
        var radius = theme.Number("RadiusPanelScalar");
        var border = theme.Number("BorderThinScalar");
        var glow = theme.GlowRadius("GlowBlue");

        var panelWidth = theme.Number("TipMaxWidth");
        var panelHeight = padding + line + spaceSm + line + spaceLg + bar + padding;

        var width = panelWidth + (glow * 2);
        var height = panelHeight + (glow * 2);
        var raster = new Raster(width, height);

        var panel = new Rect(glow, glow, panelWidth, panelHeight);
        var background = theme.Color("AppBgColor");
        var edge = theme.Color("NeonBlueBorderStrongColor");

        // Dış parıltı: tema BoxShadows belirtecinin rengi ve yarıçapı, panel kenarından
        // dışa doğru sönerek.
        raster.Glow(panel, radius, theme.GlowColor("GlowBlue"), glow);

        // Panel gövdesi ve tek piksellik kenarı.
        raster.RoundedRect(panel, radius, background);
        raster.RoundedRectOutline(panel, radius, edge, border);

        // İlerleme yolu duruk olduğu için görüntüye giriyor; üstünde gezen parça
        // başlatıcıda çiziliyor.
        var track = new Rect(
            panel.X + padding,
            panel.Y + panel.Height - padding - bar,
            panel.Width - (padding * 2),
            bar);
        raster.RoundedRect(track, bar / 2.0, Blend(theme.Color("NeonBlueFillColor"), background));

        var titleTop = panel.Y + padding;
        var statusTop = titleTop + line + spaceSm;

        tokens = string.Join(";", new[]
        {
            $"AppBgColor={theme.Raw("AppBgColor")}",
            $"NeonBlueColor={theme.Raw("NeonBlueColor")}",
            $"NeonBlueBorderStrongColor={theme.Raw("NeonBlueBorderStrongColor")}",
            $"NeonBlueFillColor={theme.Raw("NeonBlueFillColor")}",
            // Gezen parçanın gradyanı: ortada neon mavi, uçlarda neon mor.
            $"NeonPurpleColor={theme.Raw("NeonPurpleColor")}",
            $"TextBodyColor={theme.Raw("TextBodyColor")}",
            $"GlowBlue={theme.Raw("GlowBlue")}",
            $"FontMono={theme.Raw("FontMono")}",
            $"FontSizeMd={theme.Raw("FontSizeMd")}",
            $"FontSizeSm={theme.Raw("FontSizeSm")}",
            $"PanelPadding={theme.Raw("PanelPadding")}",
            $"SpaceSm={theme.Raw("SpaceSm")}",
            $"SpaceLg={theme.Raw("SpaceLg")}",
            $"LineHeightBody={theme.Raw("LineHeightBody")}",
            $"ProgressBarHeight={theme.Raw("ProgressBarHeight")}",
            $"RadiusPanelScalar={theme.Raw("RadiusPanelScalar")}",
            $"BorderThinScalar={theme.Raw("BorderThinScalar")}",
            $"TipMaxWidth={theme.Raw("TipMaxWidth")}",
            $"MotionStaggerMs={theme.Raw("MotionStaggerMs")}",
            // Tarama hızı bu ikisinden türer; başlatıcıda sabit süre yok.
            $"MotionSlow={theme.Raw("MotionSlow")}",
            $"MotionStaggerCount={theme.Raw("MotionStaggerCount")}",
            // Türetilmiş yerleşim: başlatıcı metni ve gezen parçayı bunlara göre koyar.
            $"panel={Box(panel)}",
            $"track={Box(track)}",
            $"title={Box(new Rect(panel.X + padding, titleTop, panel.Width - (padding * 2), line))}",
            $"status={Box(new Rect(panel.X + padding, statusTop, panel.Width - (padding * 2), line))}",
        });

        return raster;
    }

    private static string Box(Rect rect) => string.Format(
        CultureInfo.InvariantCulture, "{0},{1},{2},{3}", rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Yarı saydam bir dolguyu duruk bir zemine karıştırır.</summary>
    private static uint Blend(uint over, uint under)
    {
        var alpha = (over >> 24) / 255.0;
        static double Part(uint value, int shift) => (value >> shift) & 0xFF;
        var r = (Part(over, 16) * alpha) + (Part(under, 16) * (1 - alpha));
        var g = (Part(over, 8) * alpha) + (Part(under, 8) * (1 - alpha));
        var b = (Part(over, 0) * alpha) + (Part(under, 0) * (1 - alpha));
        return 0xFF000000u | ((uint)Math.Round(r) << 16) | ((uint)Math.Round(g) << 8) | (uint)Math.Round(b);
    }
}

internal readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>Theme.axaml'ın metin olarak okunması. Avalonia'ya bağımlılık yok.</summary>
internal sealed class Theme
{
    private readonly Dictionary<string, string> _values;

    private Theme(Dictionary<string, string> values) => _values = values;

    public static Theme Read(string path)
    {
        var text = File.ReadAllText(path);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        // <Color x:Key="X">#AARRGGBB</Color> ve aynı biçimdeki sayı/ölçü belirteçleri.
        foreach (Match match in Regex.Matches(
                     text, @"<(?<tag>Color|x:Double|x:Int32|x:String|sys:TimeSpan|CornerRadius|Thickness|FontFamily)\s+x:Key=""(?<key>[^""]+)""\s*>(?<value>[^<]*)</\1>"))
        {
            values[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }

        // <BoxShadows x:Key="GlowBlue">0 0 20 0 #4000F3FF</BoxShadows>
        foreach (Match match in Regex.Matches(text, @"<BoxShadows\s+x:Key=""(?<key>[^""]+)""\s*>(?<value>[^<]*)</BoxShadows>"))
        {
            values[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }

        return new Theme(values);
    }

    public string Raw(string key) => _values.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"Theme.axaml içinde belirteç yok: {key}");

    public double Number(string key) =>
        double.Parse(Raw(key).Split(',')[0], CultureInfo.InvariantCulture);

    /// <summary>#AARRGGBB metnini 0xAARRGGBB sayısına çevirir.</summary>
    public uint Color(string key) => ParseColor(Raw(key));

    public uint GlowColor(string key) => ParseColor(Raw(key).Split(' ')[^1]);

    public double GlowRadius(string key) =>
        double.Parse(Raw(key).Split(' ')[2], CultureInfo.InvariantCulture);

    private static uint ParseColor(string text) =>
        uint.Parse(text.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <summary>Kenar yumuşatmalı, düz alfa taşıyan bir piksel tamponu.</summary>
internal sealed class Raster
{
    private const int Samples = 4;

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public Raster(double width, double height)
    {
        Width = (int)Math.Round(width);
        Height = (int)Math.Round(height);
        Pixels = new byte[Width * Height * 4];
    }

    public void RoundedRect(Rect rect, double radius, uint color) =>
        Fill(rect, radius, color, (_, coverage) => coverage);

    public void RoundedRectOutline(Rect rect, double radius, uint color, double thickness)
    {
        var inner = new Rect(
            rect.X + thickness, rect.Y + thickness,
            rect.Width - (thickness * 2), rect.Height - (thickness * 2));
        var innerRadius = Math.Max(0, radius - thickness);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var coverage = Coverage(x, y, rect, radius) - Coverage(x, y, inner, innerRadius);
                if (coverage > 0) Paint(x, y, color, coverage);
            }
        }
    }

    /// <summary>Panelin dışına doğru sönen parıltı.</summary>
    public void Glow(Rect rect, double radius, uint color, double spread)
    {
        var strength = (color >> 24) / 255.0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var distance = Distance(x + 0.5, y + 0.5, rect, radius);
                if (distance <= 0 || distance >= spread) continue;
                var falloff = 1 - (distance / spread);
                Paint(x, y, color, strength * falloff * falloff);
            }
        }
    }

    private void Fill(Rect rect, double radius, uint color, Func<int, double, double> shape)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var coverage = shape(x, Coverage(x, y, rect, radius));
                if (coverage > 0) Paint(x, y, color, coverage);
            }
        }
    }

    /// <summary>Bir pikselin yuvarlatılmış dikdörtgen içinde kalan oranı.</summary>
    private static double Coverage(int x, int y, Rect rect, double radius)
    {
        var inside = 0;
        for (var sy = 0; sy < Samples; sy++)
        {
            for (var sx = 0; sx < Samples; sx++)
            {
                var px = x + ((sx + 0.5) / Samples);
                var py = y + ((sy + 0.5) / Samples);
                if (Distance(px, py, rect, radius) <= 0) inside++;
            }
        }
        return (double)inside / (Samples * Samples);
    }

    /// <summary>İşaretli uzaklık: içeride negatif, dışarıda pozitif.</summary>
    private static double Distance(double px, double py, Rect rect, double radius)
    {
        var halfWidth = rect.Width / 2;
        var halfHeight = rect.Height / 2;
        radius = Math.Min(radius, Math.Min(halfWidth, halfHeight));

        var dx = Math.Abs(px - (rect.X + halfWidth)) - (halfWidth - radius);
        var dy = Math.Abs(py - (rect.Y + halfHeight)) - (halfHeight - radius);
        var outsideX = Math.Max(dx, 0);
        var outsideY = Math.Max(dy, 0);
        return Math.Sqrt((outsideX * outsideX) + (outsideY * outsideY))
               + Math.Min(Math.Max(dx, dy), 0) - radius;
    }

    /// <summary>Düz alfa ile üst üste bindirme.</summary>
    private void Paint(int x, int y, uint color, double coverage)
    {
        coverage = Math.Clamp(coverage, 0, 1);
        var alpha = ((color >> 24) / 255.0) * coverage;
        if (alpha <= 0) return;

        var offset = ((y * Width) + x) * 4;
        var destinationAlpha = Pixels[offset + 3] / 255.0;
        var outAlpha = alpha + (destinationAlpha * (1 - alpha));
        if (outAlpha <= 0) return;

        for (var channel = 0; channel < 3; channel++)
        {
            var source = (color >> (16 - (channel * 8))) & 0xFF;
            var destination = Pixels[offset + channel];
            var value = ((source * alpha) + (destination * destinationAlpha * (1 - alpha))) / outAlpha;
            Pixels[offset + channel] = (byte)Math.Clamp(Math.Round(value), 0, 255);
        }
        Pixels[offset + 3] = (byte)Math.Clamp(Math.Round(outAlpha * 255), 0, 255);
    }
}

/// <summary>Sıkıştırmasız kütüphane gerektirmeyen küçük bir PNG yazıcısı.</summary>
internal static class Png
{
    public static byte[] Encode(Raster raster, string keyword, string text)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var header = new byte[13];
        WriteInt(header, 0, raster.Width);
        WriteInt(header, 4, raster.Height);
        header[8] = 8;  // bit derinliği
        header[9] = 6;  // renk türü: RGBA
        Chunk(output, "IHDR", header);

        var comment = Encoding.ASCII.GetBytes(keyword + "\0" + text);
        Chunk(output, "tEXt", comment);

        var scanlines = new byte[(raster.Width * 4 + 1) * raster.Height];
        for (var y = 0; y < raster.Height; y++)
        {
            var source = y * raster.Width * 4;
            var destination = (y * (raster.Width * 4 + 1)) + 1;
            scanlines[destination - 1] = 0; // süzgeç yok
            Array.Copy(raster.Pixels, source, scanlines, destination, raster.Width * 4);
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(scanlines);
        }
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);

        var body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        output.Write(body);

        var crc = new byte[4];
        WriteInt(crc, 0, unchecked((int)Crc32(body)));
        output.Write(crc);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xEDB88320u & (uint)(-(crc & 1)));
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
