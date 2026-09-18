using System.IO;

namespace VidShrink.Tests;

/// <summary>
/// Kanıt klasörünün kapanışı. Çağrısı <b>son asertten sonra</b> durur: yeşil koşum kendi
/// bıraktığını siler, kırmızı koşum kanıtını korur çünkü düşen asert buraya hiç gelmez.
/// Klasör boşalınca o da gider, altında kaldığı boş üst klasörlerle birlikte — ama
/// <c>.calisma</c>'nın kendisine dokunulmaz.
/// </summary>
internal static class KanitKapanisi
{
    /// <summary>
    /// Bir testin <b>kendi</b> yazdığı adları koşum başında temizler. Klasörü süpürmez:
    /// kardeş ölçünün kırmızı koşumdan kalan kanıtı yerinde durur.
    /// </summary>
    internal static void Onceki(string klasor, params string[] adlar)
    {
        if (!Directory.Exists(klasor)) return;
        foreach (var ad in adlar) Sil(klasor, ad);
    }

    internal static void Kapat(string klasor, params string[] adlar)
    {
        foreach (var ad in adlar) Sil(klasor, ad);

        var dizin = new DirectoryInfo(klasor);
        while (dizin is not null && dizin.Exists && dizin.Name != ".calisma" && dizin.GetFileSystemInfos().Length == 0)
        {
            var ust = dizin.Parent;
            dizin.Delete();
            dizin = ust;
        }
    }

    /// <summary>
    /// Tek adı siler. Ad joker (<c>*</c> ya da <c>?</c>) taşıyorsa eşleşen her girdiyi siler:
    /// eski <c>AltyaziKanit.Kapat</c> gövdesinin yaptığı buydu, ortak gövdeye geri kondu.
    /// </summary>
    private static void Sil(string klasor, string ad)
    {
        if (ad.IndexOfAny(Jokerler) >= 0)
        {
            if (!Directory.Exists(klasor)) return;
            foreach (var eslesen in Directory.GetFileSystemEntries(klasor, ad)) SilYolu(eslesen);
            return;
        }

        SilYolu(Path.Combine(klasor, ad));
    }

    private static void SilYolu(string yol)
    {
        if (File.Exists(yol)) File.Delete(yol);
        else if (Directory.Exists(yol)) Directory.Delete(yol, true);
    }

    private static readonly char[] Jokerler = ['*', '?'];
}
