namespace VidShrink.Core;

/// <summary>
/// Ölçeklenen boyutun yuvarlanma kuralı. Tek yerde durur: aynı kural üç dosyada ayrı ayrı
/// <c>private static int EvenDown</c> olarak yazılıydı ve çarpan üçünde de 2'ye gömülüydü.
/// </summary>
/// <remarks>
/// Çarpan neden var: kodlayıcılar tek sayılı boyut kabul etmez (4:2:0 kroma yarım piksel
/// olamaz), eski donanım kodlayıcıları ise 16'nın katını ister. HandBrake bunu
/// <c>--modulus</c> ile sunar; bizde de aynı küme seçilebilir.
/// </remarks>
public static class Olcek
{
    /// <summary>Seçilebilir çarpanlar. Küme kapalı: dışarıdan gelen sayı buna göre elenir.</summary>
    public static readonly IReadOnlyList<int> Moduller = [2, 4, 8, 16];

    /// <summary>Çarpan verilmediğinde kullanılan değer; bugünkü davranış.</summary>
    public const int VarsayilanModul = 2;

    /// <summary>Verilen sayı <see cref="Moduller"/> kümesinde mi.</summary>
    public static bool GecerliModul(int modul) => Moduller.Contains(modul);

    /// <summary>
    /// Boyutu çarpanın katına <b>aşağı</b> yuvarlar. Yukarı yuvarlamak hedef boyutu
    /// büyütür; küçültme aracında yukarı yuvarlamak yanlış yön.
    /// </summary>
    /// <remarks>
    /// Sonuç hiçbir zaman sıfır olmaz: çarpandan küçük bir kenar çarpanın kendisine
    /// yükselir, çünkü sıfır genişlikli bir kare ffmpeg'e verilemez.
    /// </remarks>
    public static int Modul(int deger, int modul = VarsayilanModul)
    {
        if (modul < 2) modul = VarsayilanModul;
        if (deger <= modul) return modul;
        return deger - deger % modul;
    }
}
