using System;
using System.IO;

namespace VidShrink.App;

/// <summary>
/// Kayıt ve ekran görüntüsünün varsayılan klasörü. Linux'ta .NET, <c>~/Videos</c> ya da
/// <c>~/Pictures</c> yoksa ve XDG kullanıcı klasörleri tanımlı değilse <c>MyVideos</c> /
/// <c>MyPictures</c> için boş dize döndürüyor; kayıt o zaman çalışma klasörüne düşüyordu —
/// masaüstü başlatıcısından açılan uygulamada bu çoğu zaman <c>/</c>, yazılamaz.
/// Sıra: sistemin klasörü, yoksa ev klasörünün altında aynı ad, o da yoksa çalışma klasörü.
/// </summary>
internal static class KullaniciKlasoru
{
    internal static Func<Environment.SpecialFolder, string> Bul { get; set; } = Environment.GetFolderPath;

    internal static string Video() => Sec(Environment.SpecialFolder.MyVideos, "Videos");

    internal static string Resim() => Sec(Environment.SpecialFolder.MyPictures, "Pictures");

    private static string Sec(Environment.SpecialFolder sistem, string evAlti)
    {
        var klasor = Bul(sistem);
        if (!string.IsNullOrEmpty(klasor)) return Path.Combine(klasor, Core.AppIdentity.FolderName);
        var ev = Bul(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(ev) ? Environment.CurrentDirectory : Path.Combine(ev, evAlti, Core.AppIdentity.FolderName);
    }
}
