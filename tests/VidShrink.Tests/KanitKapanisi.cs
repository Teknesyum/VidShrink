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
        foreach (var ad in adlar)
        {
            var yol = Path.Combine(klasor, ad);
            if (File.Exists(yol)) File.Delete(yol);
            else if (Directory.Exists(yol)) Directory.Delete(yol, true);
        }
    }

    internal static void Kapat(string klasor, params string[] adlar)
    {
        foreach (var ad in adlar)
        {
            var yol = Path.Combine(klasor, ad);
            if (File.Exists(yol)) File.Delete(yol);
            else if (Directory.Exists(yol)) Directory.Delete(yol, true);
        }

        var dizin = new DirectoryInfo(klasor);
        while (dizin is not null && dizin.Exists && dizin.Name != ".calisma" && dizin.GetFileSystemInfos().Length == 0)
        {
            var ust = dizin.Parent;
            dizin.Delete();
            dizin = ust;
        }
    }
}
