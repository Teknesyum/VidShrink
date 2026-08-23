---
name: vidshrink-metin-olcumu
description: VidShrink'te metin genişliği ölçerken ham metni ölçme — ekranda görünen metin Title() geçidinden çıkmış hâli ve daha geniş
metadata:
  type: project
---

VidShrink'te "bu satır sığıyor mu" sorusunu ölçerken iki şey ham hâlde değil:

1. **Yazı tipi.** `Atkinson Hyperlegible Next` `VidShrink.App` içine gömülü, sistemde
   kurulu değil. Sistem yazı tipiyle ölçüm yanlış çıkar.
2. **Metnin kendisi.** Görünen her metin çalışma anında `LanguageCatalog.Title()`
   geçidinden geçiyor. Büyük harf küçük harften geniştir, sarma noktaları kayar.

**Why:** T27'de ilk ölçüm ham metin üzerindeydi ve 22 taşan satır buldu; geçitten geçmiş
metinle ölçünce 25 çıktı ve düzelttiğim satırların bir kısmı hâlâ taşıyordu. Hatayı
ölçüm değil, ekran görüntüsü yakaladı — balondaki metin baştan sona büyük harfliydi.

**How to apply:**

- Ölçümü Avalonia'nın kendi `TextLayout` sınıfıyla yap; sarmayı ekranda yapan kod o.
  Süreçte bir kez `AppBuilder.Configure<Application>().UseSkia().UseWin32().SetupWithoutStarting()`
  çağır — yalnız `SkiaPlatform.Initialize()` yetmez, `IAssetLoader` kayıtlı olmadığı için
  `avares://` yazı tipi çözülmez.
- `Title()` geçidini yeniden yazma, **çağır**. Test projesi `VidShrink.App`'e başvuruyor
  ve `LanguageCatalog.cs` içinde `InternalsVisibleTo("VidShrink.Tests")` var.
- Balon tavanı `TooltipMaxWidth` (460) değil, metne kalan yer:
  460 − 2×`TooltipPadding` yatay (16) − 2×`BorderThin` (1) = **426 px**. Tavanı
  `Theme.axaml` belirteçlerinden oku, sabit yazma.
- Ölçüm `tests/VidShrink.Tests/TipLineMetrics.cs`; metin kaynaklarını
  `TipSources.cs` okuyor (üç kaynak — bkz. [[vidshrink-ipucu-metin-kaynaklari]]).
- **Taşmayı hata sayma.** Uzun bir madde meşru olarak iki satır sürebilir. Hata olan,
  **alt görsel satırda tek kelime kalması**: o kelime yüzünden balon bir satır uzuyor ve
  metni bir tık kısaltmak satırı tümüyle kaldırıyor.
- Ekran görüntüsü almadan önce **yeniden derle**. `dotnet test` sırasında kaynak dosyalar
  geçici olarak eski hâline döndürüldüyse exe eski metinle kalır ve yakalama sessizce
  eski durumu belgeler.

İlgili: [[vidshrink-metin-geciti]], [[vidshrink-arayuz-dogrulama]]
