namespace VidShrink.Core;

/// <summary>
/// Hedef boyutun ve kullanıcıya okunan dosya boyutlarının birimi: ondalık MB,
/// 1 MB = 1 000 000 bayt (<c>docs/netlestirme/025-mb-birimi.md</c>). ffmpeg'in
/// <c>-b:v Nk</c> birimi zaten 1000 tabanlı; hedef, kaynak ve sonuç aynı birimde
/// okunmazsa "25 MB hedef → 24,9 MB çıktı" cümlesi iki ayrı birimi kıyaslar.
/// </summary>
public static class Megabayt
{
    public const double Bayt = 1_000_000.0;

    /// <summary>Bir MB'ın kilobit karşılığı: 1 000 000 × 8 / 1000.</summary>
    public const double Kbit = 8000.0;

    public static double Oku(long bayt) => bayt / Bayt;

    /// <summary>Hedefin bayt tavanı, aşağı yuvarlanmış: tavanı aşan tek bayt bile sınırı geçer.</summary>
    public static long Tavan(double mb) => (long)Math.Floor(mb * Bayt);
}
