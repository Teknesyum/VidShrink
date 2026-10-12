using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace VidShrink.App;

/// <summary>
/// Gömülü Atkinson Hyperlegible Next yalnız Latin yazısını taşıyor; öteki yazılar sistemin
/// yedeğine düşüyor. Windows'ta o yedek Segoe UI (tema zincirinin ikinci adı); Linux'ta
/// fontconfig'in seçtiği aile. Tay yazısı orada Loma'ya düşüyor ve satır 25,4 px istiyordu:
/// Kaydedici'nin Başlat / Otomatik kur / Mini kaydedici düğmelerinde yazı 22 px'lik yere
/// sığmayıp kesiliyordu (yerleşim denetimi, 1024x720'de 33 kusur). Noto Sans Thai Segoe UI'a
/// yakın satır yüksekliğiyle sığıyor; kurulu değilse atlanır, Avalonia yine sistemin yedeğine düşer.
/// <para>
/// Arap ve Kiril için Noto denendi ve eklenmedi: Noto Sans Arabic satırı 33,8 px'e çıkarıp
/// kusuru çoğalttı, Noto Sans Kiril'de DejaVu'dan dar değil (2026-10-12 ölçümü, tests AGENTS.md).
/// </para>
/// </summary>
internal static class YaziTipiYedegi
{
    internal static readonly string[] LinuxAileleri = { "Noto Sans Thai" };

    internal static AppBuilder Uygula(AppBuilder builder)
        => OperatingSystem.IsLinux() ? builder.With(Secenekler()) : builder;

    internal static FontManagerOptions Secenekler() => new()
    {
        FontFallbacks = LinuxAileleri.Select(ad => new FontFallback { FontFamily = new FontFamily(ad) }).ToArray()
    };
}
