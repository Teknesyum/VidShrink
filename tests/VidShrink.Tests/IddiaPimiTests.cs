using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Tek cumleyi ayiklayip toplam-pay aritmetigini dogrulayan tarama. T160'in Baglam bolumundeki
/// birinci oncel bu sekli tasiyordu: <c>docs/olcumler/uc-kucuk-borc.md:128</c> (kusur commit'i
/// <c>96e0a05</c>), "24 sayi iddiasi tek tek sayildi: 18'i tuttu, 6'si bayatti." — cumlenin kendi
/// sayilari (18+6=24) tutarli gorunuyordu, tabloya karsi sayilinca yanlisti (17+6+1=24). Turdaki
/// K2'nin cumle-ici aritmetigi bunu YAKALAMADI (denetci turu 1, bagimsiz olculdu). K2' bu yuzden
/// ikinci bir bacak ekliyor: <see cref="IddiaTarama.DurumTablosu"/> ayni <c>## </c> bolumunde bir
/// "Durum" sutunlu tablo bulursa, cumledeki kategori sayilarini o tablonun tallisi ile karsilastirir
/// (bkz. <see cref="Iddia.Tutarli"/>). Tablo yoksa eski aritmetik-yalniz davranisa duser ve
/// <see cref="Iddia.YerGercegiVar"/> false doner — "cozumlenemedi" degil, "yer gercegi yok".
/// </summary>
internal readonly record struct Iddia(
    string Dosya,
    int Satir,
    string Cumle,
    int N,
    int A,
    int B,
    string? ALabel,
    string? BLabel,
    bool YerGercegiVar,
    bool YerGercegiTutarli,
    string? Sinyal,
    IReadOnlyList<string> Notlar)
{
    public bool AritmetikTutarli => A + B == N;

    /// <summary>
    /// Yer gercegi bulunduysa hem cumle-ici aritmetik hem de tabloya karsi kategori esitligi
    /// saglanmali; bulunmadiysa yalniz aritmetik. Boylece "18'i tuttu, 6'si bayatti" ornegindeki
    /// gibi toplami koruyan ama kategoriyi kaydiran cumleler de kirmiziya duser.
    /// </summary>
    public bool Tutarli => YerGercegiVar ? AritmetikTutarli && YerGercegiTutarli : AritmetikTutarli;
}

internal readonly record struct IddiaTaramaSonucu(
    int Incelenen,
    IReadOnlyList<Iddia> Cozumlenen,
    int Atlanan)
{
    public int YerGercegiOlan => Cozumlenen.Count(i => i.YerGercegiVar);
    public int YerGercegiYok => Cozumlenen.Count(i => !i.YerGercegiVar);
}

/// <summary>
/// <c>docs/olcumler/</c> altindaki belgeleri metin olarak okuyup "N ... : A ..., B ..." biciminde
/// toplam-pay bildiren cumleleri ayiklayan tarayici. Tablo satirlari (<c>|</c> ile baslayan) ve kod
/// bloklari (```) atlanir — onlar cumle degil, ham veridir. Kalan duz metin cumlelere bolunur; iki
/// nokta ustuste (<c>:</c>) tasiyan ve iki tarafinda da rakam bulunan her cumle <b>incelenen</b>
/// sayilir. Bunlarin arasinda siki N/A/B kalibina uyanlar <b>cozumlenen</b>, uymayanlar (liste
/// 2'den uzun, sayilar ondalik/binlik grup, iliski kurulamiyor) <b>atlanan</b> sayilir — atlanan
/// sessizce yutulmaz, dokum sayar.
///
/// Cozumlenen her iddia icin ayrica <see cref="DurumTablosu"/> cagrilir: cumlenin bulundugu
/// <c>## </c> bolumu icinde basligi "Durum" olan bir markdown tablosu ariyor, bulursa o sutunun
/// degerlerini tallileyip <c>A</c>/<c>B</c>'nin yanindaki son kelimeyi (orn. "17'si tuttu" -&gt;
/// "tuttu") bu talliyle esler. Bulamazsa <see cref="Iddia.YerGercegiVar"/> false doner.
/// </summary>
internal static class IddiaTarama
{
    private static readonly string DocsRoot = Path.Combine(TipSources.Root, "docs", "olcumler");

    /// <summary>
    /// Bir rakam dizisini "gercek bir sayac" sayar; hemen ardindan virgul+rakam geliyorsa (Turkce
    /// ondalik, orn. <c>0,58</c>) ya da bosluk+uc rakam geliyorsa (binlik grup, orn.
    /// <c>92 577 316</c>) o rakam dizisi N/A/B adayi degildir — buyuk bir olcum degeridir, toplam-pay
    /// sayaci degil. <c>ALabel</c>/<c>BLabel</c>, sayiyla ayirici (virgul/nokta) arasindaki serbest
    /// metni yakalar — kategori etiketini (orn. "tuttu") buradan cikarmak icin.
    /// </summary>
    private static readonly Regex ToplamPayCumlesi = new(
        @"(?<N>\d+)(?!,\d)(?!\s\d{3}\b)[^\d:]{0,60}:\s*" +
        @"(?<A>\d+)(?!,\d)(?!\s\d{3}\b)(?<ALabel>[^\d,]{0,40}),\s*" +
        @"(?<B>\d+)(?!,\d)(?!\s\d{3}\b)(?<BLabel>[^\d.]{0,40})\.",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Baslik = new(@"^\s*#{1,2}\s", RegexOptions.Compiled);
    private static readonly Regex TabloAyiraci = new(@"^\s*\|?[\s\-:|]+\|?\s*$", RegexOptions.Compiled);

    /// <summary>Iki tarafinda da rakam olan iki-nokta-ustuste cumle: aday nufusu.</summary>
    private static bool AdayCumle(string cumle) =>
        cumle.Contains(':') &&
        cumle.IndexOf(':') is var iki &&
        Regex.IsMatch(cumle[..iki], @"\d") &&
        Regex.IsMatch(cumle[(iki + 1)..], @"\d");

    /// <summary>
    /// Bir belgeyi duz cumle metnine cevirir; tablo satirlari ve kod bloklari cikarilir. Donen
    /// dizi, prose metnindeki her karakterin kaynak dosyadaki satir numarasidir — eslesme
    /// konumundan gercek satira geri donmek icin.
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

    /// <summary>Bir markdown tablo satirini hucrelere ayirir; bas/son bos hucreler (dis "|") atilir.</summary>
    private static List<string> HucreAyikla(string satir)
    {
        var hucreler = satir.Split('|').Select(p => p.Trim()).ToList();
        if (hucreler.Count > 0 && hucreler[0].Length == 0)
            hucreler.RemoveAt(0);
        if (hucreler.Count > 0 && hucreler[^1].Length == 0)
            hucreler.RemoveAt(hucreler.Count - 1);
        return hucreler;
    }

    private static string TemizleHucre(string hucre) => hucre.Replace("**", "").Replace("`", "").Trim();

    /// <summary>
    /// Verilen (1 tabanli) satirin ait oldugu <c>## </c>/<c>#</c> bolumu icinde basligi (case-insensitive)
    /// "Durum" olan ilk markdown tablosunu arar, bulursa o sutunun degerlerini tallileyip doner.
    /// Sinyal: hangi bolumde, hangi satirdaki tabloyu kullandigini soyler — rapora gecmesi icin.
    /// </summary>
    internal static Dictionary<string, int>? DurumTablosu(string[] satirlar, int satirBir, out string sinyal)
    {
        sinyal = "";
        var satirIndex = satirBir - 1;

        var basIndex = 0;
        for (var i = satirIndex; i >= 0; i--)
        {
            if (Baslik.IsMatch(satirlar[i]))
            {
                basIndex = i;
                break;
            }
        }

        var sonIndex = satirlar.Length - 1;
        for (var i = satirIndex; i < satirlar.Length; i++)
        {
            if (i > satirIndex && Baslik.IsMatch(satirlar[i]))
            {
                sonIndex = i - 1;
                break;
            }
        }

        for (var i = basIndex; i <= sonIndex; i++)
        {
            var satir = satirlar[i].Trim();
            if (!satir.StartsWith("|", StringComparison.Ordinal))
                continue;

            var basliklar = HucreAyikla(satir);
            var durumSutunu = basliklar.FindIndex(h => h.Equals("durum", StringComparison.OrdinalIgnoreCase));
            if (durumSutunu < 0)
                continue;

            var j = i + 1;
            if (j <= sonIndex && TabloAyiraci.IsMatch(satirlar[j]))
                j++;

            var tally = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (; j <= sonIndex; j++)
            {
                var satirJ = satirlar[j].Trim();
                if (!satirJ.StartsWith("|", StringComparison.Ordinal))
                    break;

                var hucreler = HucreAyikla(satirJ);
                if (durumSutunu >= hucreler.Count)
                    continue;

                var deger = TemizleHucre(hucreler[durumSutunu]);
                if (deger.Length == 0)
                    continue;

                tally[deger] = tally.GetValueOrDefault(deger) + 1;
            }

            if (tally.Count == 0)
                continue;

            sinyal = $"ayni '## ' bolumu, basligi 'Durum' olan tablo (satir {i + 1})";
            return tally;
        }

        return null;
    }

    /// <summary>Serbest etiket metninden (orn. "'si tuttu") son alfabetik kelimeyi (orn. "tuttu") cikarir.</summary>
    private static string? SonKelime(string metin)
    {
        var eslesmeler = Regex.Matches(metin, @"\p{L}+");
        return eslesmeler.Count == 0 ? null : eslesmeler[^1].Value.ToLowerInvariant();
    }

    /// <summary>
    /// Etiketi (orn. "bayattı") tablo kategorilerinden (orn. "bayat") birine on-ek eslesmesiyle
    /// baglar — Turkce cekim ekleri yuzunden tam esitlik aranmaz. Esleyen bulunamazsa null: bu
    /// kategori icin yer gercegi karsilastirmasi yapilmaz, sessizce atlanir (mismatch degil).
    /// </summary>
    private static string? EslesenKategori(string? etiket, Dictionary<string, int> tablo)
    {
        if (string.IsNullOrEmpty(etiket) || etiket.Length < 3)
            return null;

        foreach (var anahtar in tablo.Keys)
        {
            var k = anahtar.ToLowerInvariant();
            if (k.Length < 3)
                continue;
            if (etiket.StartsWith(k, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(etiket, StringComparison.OrdinalIgnoreCase))
                return anahtar;
        }

        return null;
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
                var aLabel = SonKelime(eslesme.Groups["ALabel"].Value);
                var bLabel = SonKelime(eslesme.Groups["BLabel"].Value);
                var konum = Math.Min(baslangic + eslesme.Index, satirNo.Length - 1);
                var satir = satirNo[Math.Max(0, konum)];

                var tablo = DurumTablosu(satirlar, satir, out var sinyal);
                var yerGercegiVar = tablo is not null;
                var yerGercegiTutarli = true;
                var notlar = new List<string>();

                if (tablo is not null)
                {
                    foreach (var (sayi, etiket) in new[] { (a, aLabel), (b, bLabel) })
                    {
                        var kategori = EslesenKategori(etiket, tablo);
                        if (kategori is null)
                            continue;

                        var beklenen = tablo[kategori];
                        if (beklenen != sayi)
                        {
                            yerGercegiTutarli = false;
                            notlar.Add($"{etiket}={sayi} ama tabloda {kategori}={beklenen}");
                        }
                    }
                }

                cozumlenen.Add(new Iddia(ad, satir, cumle, n, a, b, aLabel, bLabel, yerGercegiVar, yerGercegiTutarli, tablo is null ? null : sinyal, notlar));
            }
        }

        return new IddiaTaramaSonucu(incelenen, cozumlenen, atlanan);
    }
}

/// <summary>
/// Sinifi yakalayan pim: <c>docs/olcumler/</c> altindaki her belgede sayisal bir ozet cumlesi
/// altindaki dokumle celisiyorsa bu olcu kirilir. K2' (T160 tur 2, denetci KRITIK sonrasi):
/// cumle-ici aritmetigin ustune, ayni <c>## </c> bolumundeki "Durum" sutunlu tabloya karsi
/// kategori sayimi eklendi. Boylece "toplam dogru ama kategori kaymis" bicimi de (bkz. T160
/// Baglam #1: "18'i tuttu, 6'si bayatti" — 18+6=24 kendi icinde tutarli ama tabloda tuttu=17)
/// yakalanir; yer gercegi tablosu bulunamayan cumlelerde eski aritmetik-yalniz davranisa
/// dusulur ve bu ayri sayilir (<see cref="IddiaTaramaSonucu.YerGercegiYok"/>).
/// </summary>
public sealed class IddiaPimiTests
{
    private readonly ITestOutputHelper _cikti;

    public IddiaPimiTests(ITestOutputHelper cikti) => _cikti = cikti;

    [Fact]
    public void ToplamPayCelisenCumleYoktur()
    {
        var sonuc = IddiaTarama.Tara();

        _cikti.WriteLine(
            $"incelenen={sonuc.Incelenen} cozumlenen={sonuc.Cozumlenen.Count} " +
            $"yerGercegiOlan={sonuc.YerGercegiOlan} yerGercegiYok={sonuc.YerGercegiYok} atlanan={sonuc.Atlanan}");

        foreach (var iddia in sonuc.Cozumlenen)
        {
            var kaynak = iddia.YerGercegiVar ? $"yer gercegi: {iddia.Sinyal}" : "yer gercegi yok, yalniz aritmetik";
            var notlar = iddia.Notlar.Count == 0 ? "" : $" [{string.Join("; ", iddia.Notlar)}]";
            _cikti.WriteLine(
                $"{(iddia.Tutarli ? "tutarli" : "CELISKILI")} {iddia.Dosya}:{iddia.Satir} " +
                $"N={iddia.N} A={iddia.A}({iddia.ALabel}) B={iddia.B}({iddia.BLabel}) :: {kaynak}{notlar} :: {iddia.Cumle}");
        }

        if (sonuc.Incelenen > 0 && sonuc.Atlanan > sonuc.Incelenen / 2)
            _cikti.WriteLine(
                $"UYARI: atlama orani yuksek ({sonuc.Atlanan}/{sonuc.Incelenen}) — olcunun degeri dusuk, gizlenmiyor.");

        var celiskili = sonuc.Cozumlenen.Where(i => !i.Tutarli).ToList();
        Assert.Empty(celiskili);
    }
}
