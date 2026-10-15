namespace VidShrink.Core;

/// <summary>
/// SIK-18: teslimden önce çıktının okunan süresiyle beklenen süresi. <see cref="OkunanSaniye"/>
/// <c>null</c> ise dosya çözülemedi.
/// </summary>
public sealed record CiktiKusuru(double BeklenenSaniye, double? OkunanSaniye)
{
    public bool Cozulemedi => OkunanSaniye is null;
}

/// <summary>
/// SIK-18: kodlanan dosya asıl ada taşınmadan önce okunur. Çözülemeyen ya da beklenenden belirgin
/// kısa dosya teslim edilmez; hedefteki eski dosya ezilmez.
/// </summary>
public static class CiktiDogrulama
{
    /// <summary>Beklenen sürenin bu oranı kadar kısalık kap ve konteyner farkı sayılır.</summary>
    public const double OranPayi = 0.05;

    /// <summary>Kısa klipte oran payı yetmez; en az bu kadar saniye tanınır.</summary>
    public const double EnAzPaySaniye = 1.0;

    /// <summary>
    /// Kusur yoksa <c>null</c>. Okunan süre yoksa ya da sıfırsa dosya çözülemedi sayılır; beklenen süre
    /// bilinmiyorsa yalnız çözülebilirlik aranır.
    /// </summary>
    public static CiktiKusuru? Denetle(double beklenenSaniye, double? okunanSaniye)
    {
        if (okunanSaniye is not double okunan || double.IsNaN(okunan) || okunan <= 0)
            return new CiktiKusuru(beklenenSaniye, null);
        if (beklenenSaniye <= 0) return null;
        var pay = Math.Max(EnAzPaySaniye, beklenenSaniye * OranPayi);
        return okunan < beklenenSaniye - pay ? new CiktiKusuru(beklenenSaniye, okunan) : null;
    }
}

/// <summary>SIK-11: kaynağın oluşturma ve değiştirme tarihini çıktıya yazar.</summary>
public static class KaynakTarihi
{
    /// <summary>
    /// Önce oluşturma, sonra değiştirme tarihi yazılır: oluşturma tarihini tutmayan dosya sisteminde
    /// değiştirme tarihi yine kaynağınki kalır. Yazılamazsa <c>false</c>; iş bunun için düşmez.
    /// </summary>
    public static bool Tasi(string kaynak, string hedef)
    {
        try
        {
            File.SetCreationTimeUtc(hedef, File.GetCreationTimeUtc(kaynak));
            File.SetLastWriteTimeUtc(hedef, File.GetLastWriteTimeUtc(kaynak));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
