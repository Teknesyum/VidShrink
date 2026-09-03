using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Tek cumleyi ayiklayip toplam-pay aritmetigini dogrulayan tarama. T160'in Baglam
/// bolumundeki birinci oncel bu sekli tasiyordu: <c>docs/olcumler/uc-kucuk-borc.md:128</c>,
/// "24 sayi iddiasi tek tek sayildi: 18'i tuttu, 6'si bayatti." — cumlenin kendi sayilari
/// (18+6=24) tutarli gorunuyordu, tabloya karsi sayilinca yanlisti (17+6+1=24). Bu sinifi
/// bir <b>kategori kaymasi</b> yakalamaz (bkz. <see cref="Iddia.Kategorik"/>); yakaladigi
/// tek sey cumlenin kendi bildirdigi N ile A+B arasindaki aritmetik uyusmazliktir.
/// </summary>
internal readonly record struct Iddia(string Dosya, int Satir, string Cumle, int N, int A, int B)
{
    public bool Tutarli => A + B == N;
}

internal readonly record struct IddiaTaramaSonucu(
    int Incelenen,
    IReadOnlyList<Iddia> Cozumlenen,
    int Atlanan);

/// <summary>
/// <c>docs/olcumler/</c> altindaki belgeleri metin olarak okuyup "N ... : A ..., B ..."
/// biciminde toplam-pay bildiren cumleleri ayiklayan tarayici. Tablo satirlari (<c>|</c> ile
/// baslayan) ve kod bloklari (```) atlanir — onlar cumle degil, ham veridir. Kalan duz
/// metin cumlelere bolunur; iki nokta ustuste (<c>:</c>) tasiyan ve iki tarafinda da rakam
/// bulunan her cumle <b>incelenen</b> sayilir. Bunlarin arasinda siki N/A/B kalibina uyanlar
/// <b>cozumlenen</b>, uymayanlar (liste 2'den uzun, sayilar ondalik/binlik grup, iliski
/// kurulamiyor) <b>atlanan</b> sayilir — atlanan sessizce yutulmaz, dokum sayar.
/// </summary>
internal static class IddiaTarama
{
    private static readonly string DocsRoot = Path.Combine(TipSources.Root, "docs", "olcumler");

    /// <summary>
    /// Bir rakam dizisini "gercek bir sayac" sayar; hemen ardindan virgul+rakam geliyorsa
    /// (Turkce ondalik, orn. <c>0,58</c>) ya da bosluk+uc rakam geliyorsa (binlik grup, orn.
    /// <c>92 577 316</c>) o rakam dizisi N/A/B adayi degildir — buyuk bir olcum degeridir,
    /// toplam-pay sayaci degil.
    /// </summary>
    private static readonly Regex ToplamPayCumlesi = new(
        @"(?<N>\d+)(?!,\d)(?!\s\d{3}\b)[^\d:]{0,60}:\s*" +
        @"(?<A>\d+)(?!,\d)(?!\s\d{3}\b)[^\d,]{0,40},\s*" +
        @"(?<B>\d+)(?!,\d)(?!\s\d{3}\b)[^\d.]{0,40}\.",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Iki tarafinda da rakam olan iki-nokta-ustuste cumle: aday nufusu.</summary>
    private static bool AdayCumle(string cumle) =>
        cumle.Contains(':') &&
        cumle.IndexOf(':') is var iki &&
        Regex.IsMatch(cumle[..iki], @"\d") &&
        Regex.IsMatch(cumle[(iki + 1)..], @"\d");

    /// <summary>
    /// Bir belgeyi duz cumle metnine cevirir; tablo satirlari ve kod bloklari cikarilir.
    /// Donen dizi, prose metnindeki her karakterin kaynak dosyadaki satir numarasidir —
    /// eslesme konumundan gercek satira geri donmek icin.
    /// </summary>
    private static (string Metin, int[] SatirNo) DuzMetne(string[] satirlar)
    {
        var metin = new StringBuilder();
        var satirNo = new List<int>();
        var kodBlogunda = false;

        for (var i = 0; i < satirlar.Length; i++)
        {
            var satir = satirlar[i];
            var kirpik = satir.TrimStart();

            if (kirpik.StartsWith("```", StringComparison.Ordinal))
            {
                kodBlogunda = !kodBlogunda;
                continue;
            }

            if (kodBlogunda || kirpik.StartsWith("|", StringComparison.Ordinal))
                continue;

            if (kirpik.Length == 0)
            {
                metin.Append('\n');
                satirNo.Add(i + 1);
                continue;
            }

            foreach (var ch in satir)
            {
                metin.Append(ch);
                satirNo.Add(i + 1);
            }

            metin.Append(' ');
            satirNo.Add(i + 1);
        }

        return (metin.ToString(), satirNo.ToArray());
    }

    /// <summary>Duz metni cumlelere boler. Nokta + bosluk/satir sonu sinir sayilir.</summary>
    private static IEnumerable<(string Cumle, int Baslangic)> Cumleler(string metin)
    {
        var baslangic = 0;
        for (var i = 0; i < metin.Length; i++)
        {
            if (metin[i] != '.')
                continue;

            var sonrasiBosMu = i + 1 >= metin.Length || char.IsWhiteSpace(metin[i + 1]);
            if (!sonrasiBosMu)
                continue;

            var cumle = metin[baslangic..(i + 1)].Trim();
            if (cumle.Length > 0)
                yield return (cumle, baslangic);

            baslangic = i + 1;
        }
    }

    internal static IddiaTaramaSonucu Tara()
    {
        var incelenen = 0;
        var cozumlenen = new List<Iddia>();
        var atlanan = 0;

        foreach (var dosya in Directory.GetFiles(DocsRoot, "*.md", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var satirlar = File.ReadAllLines(dosya);
            var (metin, satirNo) = DuzMetne(satirlar);
            var ad = Path.GetFileName(dosya);

            foreach (var (cumle, baslangic) in Cumleler(metin))
            {
                if (!AdayCumle(cumle))
                    continue;

                incelenen++;

                var eslesme = ToplamPayCumlesi.Match(cumle);
                if (!eslesme.Success)
                {
                    atlanan++;
                    continue;
                }

                var n = int.Parse(eslesme.Groups["N"].Value);
                var a = int.Parse(eslesme.Groups["A"].Value);
                var b = int.Parse(eslesme.Groups["B"].Value);
                var konum = Math.Min(baslangic + eslesme.Index, satirNo.Length - 1);
                var satir = satirNo[Math.Max(0, konum)];

                cozumlenen.Add(new Iddia(ad, satir, cumle, n, a, b));
            }
        }

        return new IddiaTaramaSonucu(incelenen, cozumlenen, atlanan);
    }
}

/// <summary>
/// Sinifi yakalayan pim: <c>docs/olcumler/</c> altindaki her belgede sayisal bir ozet
/// cumlesi altindaki dokumle celisiyorsa bu olcu kirilir. Kapsam yalniz kendi cumlesinin
/// aritmetigini dogrulayan siki bir kalip — tabloya karsi kategori sayimi yapmaz, bu yuzden
/// "toplam dogru ama kategori kaymis" seklindeki hatalari (bkz. T160 Baglam #1, 18+6=24
/// kendi icinde tutarli oldugu icin) yakalamaz. Bu sinirlama Cikti belgesinde acikca yazili;
/// gizlenmedi.
/// </summary>
public sealed class IddiaPimiTests
{
    private readonly ITestOutputHelper _cikti;

    public IddiaPimiTests(ITestOutputHelper cikti) => _cikti = cikti;

    [Fact]
    public void ToplamPayCelisenCumleYoktur()
    {
        var sonuc = IddiaTarama.Tara();

        _cikti.WriteLine($"incelenen={sonuc.Incelenen} cozumlenen={sonuc.Cozumlenen.Count} atlanan={sonuc.Atlanan}");

        foreach (var iddia in sonuc.Cozumlenen)
            _cikti.WriteLine(
                $"{(iddia.Tutarli ? "tutarli" : "CELISKILI")} {iddia.Dosya}:{iddia.Satir} " +
                $"N={iddia.N} A={iddia.A} B={iddia.B} :: {iddia.Cumle}");

        if (sonuc.Incelenen > 0 && sonuc.Atlanan > sonuc.Incelenen / 2)
            _cikti.WriteLine(
                $"UYARI: atlama orani yuksek ({sonuc.Atlanan}/{sonuc.Incelenen}) — olcunun degeri dusuk, gizlenmiyor.");

        var celiskili = sonuc.Cozumlenen.Where(i => !i.Tutarli).ToList();
        Assert.Empty(celiskili);
    }
}
