using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>Kartin bir yamasi: adi ve ekrana cizilen sRGB degeri.</summary>
public sealed record CardPatch(string Name, byte R, byte G, byte B);

/// <summary>Yakalanan karede bir yamanin ortalama rengi ve cizilenle farki (CIE76 Delta E).</summary>
public sealed record PatchResult(CardPatch Patch, byte R, byte G, byte B, double DeltaE);

/// <summary>
/// Kaydedici oz sinamasinin renk karti (<c>--recorder-selftest</c>). Kart bilinen sRGB yamalari
/// (beyaz, gri basamaklari, saf renkler, ten ve gok) bir saniye ekranda tutar; ayni bolge HDR'den
/// SDR'ye ceviren yoldan ve 8 bit <c>bgra</c> yoldan birer kare yakalanir, her yamanin ortasi
/// cizilen degerle Delta E olarak karsilastirilir. Sonuc <c>recorder-last.log</c>'a yazilir.
///
/// <para>Esik olcumden gelir: 10 bit PQ nicemlemesi doygun renklerin karanlik kanalinda birkac
/// kod kaydiriyor; sentetik kartta beyaz 80/203/480 nitte en buyuk Delta E 1,21, en buyuk kod
/// farki 4 (<c>docs/olcumler/kaydedici-hdr.md</c>). Griler ve beyaz ±1 kodda kalir.</para>
/// </summary>
public static class RecorderColourCard
{
    /// <summary>Izgaranin sutun sayisi.</summary>
    public const int Columns = 4;

    /// <summary>Izgaranin satir sayisi.</summary>
    public const int Rows = 3;

    /// <summary>Gri yamalarda kanal basina izin verilen en buyuk kod farki.</summary>
    public const int GreyTolerance = 1;

    /// <summary>Renkli yamalarda izin verilen en buyuk Delta E.</summary>
    public const double ColourTolerance = 2.0;

    /// <summary>Kartin yamalari, satir satir soldan saga.</summary>
    public static IReadOnlyList<CardPatch> Patches { get; } = new CardPatch[]
    {
        new("white", 255, 255, 255),
        new("grey-75", 191, 191, 191),
        new("grey-50", 128, 128, 128),
        new("black", 0, 0, 0),
        new("red", 255, 0, 0),
        new("green", 0, 255, 0),
        new("blue", 0, 0, 255),
        new("yellow", 255, 255, 0),
        new("cyan", 0, 255, 255),
        new("magenta", 255, 0, 255),
        new("skin", 194, 150, 130),
        new("sky", 98, 122, 157)
    };

    /// <summary>Yamanin gri olup olmadigi; gri yamalar kod farkiyla olculur.</summary>
    public static bool IsGrey(CardPatch patch) => patch.R == patch.G && patch.G == patch.B;

    /// <summary><paramref name="index"/>. yamanin <paramref name="width"/> x <paramref name="height"/> karttaki dikdortgeni.</summary>
    public static (int X, int Y, int Width, int Height) Cell(int index, int width, int height)
    {
        var col = index % Columns;
        var row = index / Columns;
        var x0 = col * width / Columns;
        var y0 = row * height / Rows;
        return (x0, y0, (col + 1) * width / Columns - x0, (row + 1) * height / Rows - y0);
    }

    /// <summary>
    /// <c>rgb24</c> karede her yamanin ic yarisinin ortalamasi. Kenarlar alinmaz; boylece yakalama
    /// bolgesi birkac piksel kaysa da komsu yama olcuye karismaz.
    /// </summary>
    public static IReadOnlyList<(byte R, byte G, byte B)> Sample(byte[] rgb24, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgb24);
        if (width <= 0 || height <= 0 || rgb24.Length < width * height * 3)
            throw new ArgumentException("Frame is smaller than its size.", nameof(rgb24));

        var result = new (byte, byte, byte)[Patches.Count];
        for (var i = 0; i < Patches.Count; i++)
        {
            var (x, y, w, h) = Cell(i, width, height);
            long r = 0, g = 0, b = 0, n = 0;
            for (var yy = y + h / 4; yy < y + h - h / 4; yy++)
            for (var xx = x + w / 4; xx < x + w - w / 4; xx++)
            {
                var p = (yy * width + xx) * 3;
                r += rgb24[p];
                g += rgb24[p + 1];
                b += rgb24[p + 2];
                n++;
            }
            result[i] = n == 0 ? ((byte)0, (byte)0, (byte)0) : (Mean(r, n), Mean(g, n), Mean(b, n));
        }
        return result;
    }

    /// <summary>Iki sRGB rengin CIE76 farki (D65, Lab).</summary>
    public static double DeltaE76((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        var (l1, a1, b1) = Lab(a);
        var (l2, a2, b2) = Lab(b);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>Orneklenen renkleri kartin yamalariyla eslestirir.</summary>
    public static IReadOnlyList<PatchResult> Compare(IReadOnlyList<(byte R, byte G, byte B)> sampled)
    {
        ArgumentNullException.ThrowIfNull(sampled);
        if (sampled.Count != Patches.Count) throw new ArgumentException("Sample count must match the card.", nameof(sampled));
        return Patches.Select((p, i) => new PatchResult(p, sampled[i].R, sampled[i].G, sampled[i].B,
            DeltaE76((p.R, p.G, p.B), sampled[i]))).ToArray();
    }

    /// <summary>Yamanin esigi gecip gecmedigi: gri yamada kanal basina kod farki, renkli yamada Delta E.</summary>
    public static bool Passes(PatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsGrey(result.Patch)) return result.DeltaE <= ColourTolerance;
        var p = result.Patch;
        return Math.Abs(result.R - p.R) <= GreyTolerance && Math.Abs(result.G - p.G) <= GreyTolerance
               && Math.Abs(result.B - p.B) <= GreyTolerance;
    }

    /// <summary>Bir yolun satirlari: yama basina olculen renk, Delta E ve hukum.</summary>
    public static string Report(string path, IReadOnlyList<PatchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var inv = CultureInfo.InvariantCulture;
        var b = new StringBuilder();
        b.Append("selftest-path: ").Append(path)
            .Append(string.Format(inv, " max-de={0:0.00} mean-de={1:0.00} ", results.Max(r => r.DeltaE), results.Average(r => r.DeltaE)))
            .AppendLine(results.All(Passes) ? "pass" : "fail");
        foreach (var r in results)
            b.AppendLine(string.Format(inv, "  {0,-8} drawn={1},{2},{3} captured={4},{5},{6} de={7:0.00} {8}",
                r.Patch.Name, r.Patch.R, r.Patch.G, r.Patch.B, r.R, r.G, r.B, r.DeltaE, Passes(r) ? "ok" : "off"));
        return b.ToString();
    }

    /// <summary>Oz sinama raporunun ilk satiri; makine kaydi, dili sabit.</summary>
    public static string Header(DateTimeOffset at)
        => "selftest: recorder colour card " + at.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    /// <summary>Kart tek bir DXGI cikisina dusmediginde yazilan satir.</summary>
    public const string OutsideOutput = "selftest-error: card is not inside one DXGI output";

    /// <summary>Bir yolun yakalamasi sure asiminda kaldiginda yazilan satir.</summary>
    public static string TimedOut(string path) => "selftest-error: " + path + " timed out";

    /// <summary>Bir yolun ffmpeg cagrisi dustugunde yazilan satir.</summary>
    public static string FfmpegFailed(string path, string detail) => "selftest-error: " + path + " ffmpeg failed: " + detail;

    /// <summary>Bir yolda dosya ya da arguman hatasi olustugunda yazilan satir.</summary>
    public static string PathError(string path, string detail) => "selftest-error: " + path + " " + detail;

    private static byte Mean(long sum, long count) => (byte)Math.Clamp((int)Math.Round((double)sum / count), 0, 255);

    private static double Linear(byte v)
    {
        var c = v / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static (double L, double A, double B) Lab((byte R, byte G, byte B) c)
    {
        var r = Linear(c.R);
        var g = Linear(c.G);
        var b = Linear(c.B);
        var x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
        var y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        var z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;
        double F(double t) => t > 216.0 / 24389.0 ? Math.Cbrt(t) : (24389.0 / 27.0 * t + 16.0) / 116.0;
        var fx = F(x);
        var fy = F(y);
        var fz = F(z);
        return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }
}
