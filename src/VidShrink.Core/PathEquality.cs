namespace VidShrink.Core;

/// <summary>
/// "Bu iki yol aynı dosyayı mı gösteriyor" sorusunun tek gövdesi. Soru üç yerde ayrı
/// yazılmıştı (<c>CurrentMedia.SamePath</c>, <c>MainWindow.OdakTakibi.SamePath</c>,
/// <c>CliApp.PathEquals</c>) ve davranışları ayrışmıştı: biri <see cref="ArgumentException"/>
/// yutuyor, ikisi yutmuyordu — bozuk bir yolla çağrılan CLI kolu çakıyordu. Karşılaştırma
/// artık işletim sistemini de izliyor: Windows ve macOS'ta harf duyarsız, Linux'ta duyarlı
/// (<see cref="WatchFolder.PathComparison"/> dikişiyle aynı kural).
/// </summary>
public static class PathEquality
{
    /// <summary>
    /// Yollardan biri boşsa, ikisi de aynı dosyayı göstermiyor sayılır. Çözülemeyen yol
    /// (geçersiz karakter, aşırı uzunluk) de eşitsizliktir: burada hüküm vermek çağıranın
    /// işi değil, çünkü üç çağrı yeri de "aynıysa atla" kolunda duruyor.
    /// </summary>
    public static bool Same(string? left, string? right, StringComparison? comparison = null)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                comparison ?? WatchFolder.PathComparison);
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PathTooLongException) { return false; }
    }
}
