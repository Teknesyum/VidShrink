# Kaydedici Mini Şeridinin Düzeni

Dal `agent/kaydedici-mini-duzen`. Sahip "mini pencere düzenimiz kötü" dedi. Bu belgede altı aracın
kayıt şeridine dair araştırma, bugünkü şeridin ölçümü ve yeni düzenin ölçümü var. Ölçümü
`tests/VidShrink.Tests/KaydediciMiniDuzenTests.cs` üretiyor: `OlcumuYaz` sayıları ve PNG'leri yazıyor,
`SeritDuzeniButunDillerdeVeHallerdeTutarli` düzeni 42 dilde ve 5 halde pimliyor.

## Araştırma: Altı Aracın Kayıt Şeridi

Kaynaklar üretici belgeleri ve ikincil yazılar. Sıra, ayraç, boyut ve tutamak ayrıntılarının
çoğu kaynakta yazılı değil; bu ayrıntılar "doğrulanamadı" olarak bırakıldı.

| Araç | Şeritte ne var | Durdur / duraklat | Süre | Kaynak |
|---|---|---|---|---|
| Windows Ekran Alıntısı Aracı | Kayıt çubuğu: duraklat, durdur, süre, sil | Duraklat durdurun hemen yanında, durdur kırmızı | Çubukta | winaero.com (Snipping Tool pause), anoopcnair.com |
| ShareX | Bölgenin altında küçük bir çubuk: durdur ve geçen süre | Durdur tek ve belirgin | Çubukta | getsharex.com, digitalcitizen.life |
| Loom | Kenarda dikey denetim: durdur, duraklat, yeniden başlat, sil | Durdur en başta, kırmızı; duraklat bitişik | Durdurun üstünde | atlassian.com/blog/loom, support.atlassian.com/loom |
| Bandicam | Pencere başlığında kayıt düğmesi ve süre; tepside yeşil/kırmızı/gri durum | Duraklat ve durdur yan yana | Başlıkta | bandicam.com (user interface, hide) |
| Camtasia | Kayıt araç çubuğu: duraklat, durdur, süre; F9 duraklat, F10 durdur | Durdur ve duraklat yan yana | Çubukta | sjsu.edu Camtasia quickstart, techsmith.com |
| OBS | Kayıt şeridi yok; denetim ana penceredeki Controls dock'unda | Ayrı düğmeler | Durum çubuğunda | obsproject.com/kb/quick-start-guide |

Ortak çıkanlar:

1. **Durdur en belirgin denetim.** Ekran Alıntısı Aracı, Loom ve Camtasia'da durdur renkli ve
   öne çıkıyor. ShareX'te şeritte yalnız durdur var.
2. **Duraklat durdurun bitişiğinde.** Ekran Alıntısı Aracı, Loom, Bandicam ve Camtasia'da ikisi
   aynı grupta duruyor.
3. **Süre aynı şeritte.** Bu ShareX, Bandicam ve Camtasia için geçerli.
4. **Denetim kayda girmiyor ya da kadrajın dışında duruyor.** Loom ve Bandicam denetimi
   gizliyor, ShareX bölgenin altına koyuyor. VidShrink bunu zaten yapıyor (`PlaceOutside`,
   `CaptureAffinity`).
5. **Konum kullanıcının elinde.** Loom'da denetim sürükleniyor ve yeri hatırlanıyor.
6. **Kısayol ve tepsi yedek yol.** Camtasia F9/F10 kullanıyor, Bandicam tepsi rengini
   kullanıyor. VidShrink'te F7/F8 ve tepsi durumu var.

## Bugünkü Şeridin Ölçümü

Düzenek pencereyi göstermeden yerleşimi okuyor; şeridin `Bounds` zinciri ölçülüyor.
Diller tr, en ve lv (lv en uzun "bölge seç" metni). Haller boşta, kayıt, kayıt + bölüm işareti,
duraklatıldı ve geri sayım. Toplam 15 ölçüm yapıldı. 1.0 ve 1.5 ölçekte PNG'ler `RenderTargetBitmap` ile çizildi.

| Ölçü | Sonuç |
|---|---|
| Taşan düğme (15 ölçümde) | 60 satır: Oynat/Duraklat 15, Seçenekler 15, Büyüt 15, Durdur 12, Bölge 3 |
| Neden | 40'lık düğme 38'lik iç alanda (iç kutu 17,9 → 349,38) |
| Kesik metin | 0 |
| Şerit genişliği, tr | boşta 383, kayıt 326, kayıt + bölüm 477, duraklatıldı 314, geri sayım 314 |
| Oynat/Duraklat x, tr | 318 / 221 / 372 / 209 / 209: Başlat'a basınca düğme 109 px kayıyor |
| Durdur x, tr | 269 / 420 / 257 / 257 |
| Sıra | sayaç, Büyüt, Bölge, Seçenekler, Oynat/Duraklat, Durdur |
| Boşluk | çoğu 8, arada düzensiz bir 16 |
| 1.5 ölçekte kesirli kenar | 70 satır |
| Yeni pine karşı eski XAML, 42 dil × 5 hal | 1491 kusur |

Eski koşumdaki 57 "simge görünmüyor" satırı hata ayıklama eşiğinin yapıntısıydı ve sayıma
girmedi; durdur simgesinin görünür olduğu PNG'den doğrulandı.

## Yeni Düzen

Sıra soldan sağa üç grup:

1. `[Oynat/Duraklat] [Durdur]`. Boşta durdurun yuvasında `[Bölge seç]` duruyor.
2. `[● sayaç] [bölüm işareti]`. Sayaç sürükleme tutamağı olarak kaldı.
3. `[Seçenekler] [Büyüt]`.

Grup içi boşluk `RecorderStripGap` (8), gruplar arası `RecorderMiniGroupGap` (`SpaceLg`, 16).

Gerekçe:

- Durdur ve duraklat araştırmadaki gibi bitişik ve şeridin başında. Pencere sağa doğru
  büyüdüğü için baştaki düğme hiçbir halde kaymıyor.
- Kayıt noktası artık hal değişince yer açıp kapatmıyor (`Opacity`). Böylece genişlik
  kayıtta da duraklatmada da aynı kalıyor.
- Yeni belirteç `RecorderMiniPadding` = `SpaceLg - BorderThin, SpaceSm - BorderThin` = 15,7.
  `RecorderMiniHeight` (56) hesabı panel kenarlığını unutmuştu: 56 - 2×8 - 2×1 = 38'lik iç
  alana 40'lık düğme konuyordu. Yeni payla iç alan tam 40. Ayrıca her düğme kenarı çift
  sayıya düşüyor (1 + 15 = 16).

## Yeni Düzenin Ölçümü

| Ölçü | Sonuç |
|---|---|
| Taşma, kesik metin, düğme yüksekliği farkı (42 dil × 5 hal) | 0 kusur |
| Şerit genişliği, tr | boşta 401, kayıt + bölüm 483, öteki üç hal 332 |
| Şerit genişliği, en / lv | boşta 431 / 477, kayıt + bölüm 473 / 476, öteki haller 332 |
| Oynat/Duraklat x | her halde 16 |
| Durdur x | her kayıt halinde 64 |
| Boşluk | grup içi 8, gruplar arası 16 |
| 1.5 ölçekte kesirli kenar | 21 satır, yalnız değişken genişlikli metinden sonra |

Mutasyonlar, hepsi kırmızı: eski XAML 1491 kusur; pay `RecorderStripPadding`'e dönünce 1462;
paket 161'de `GatePointerBoxes` gövdesi boş, olay bağlantısı yok ya da `WantsPointer` kapısız
bırakılınca her biri 2 test düşüyor.

## Ölçülmeyenler

- Gerçek 1.5 ölçekli Windows penceresi ölçülmedi. Win32 arka ucunda ölçek elle seçilemiyor.
  1.5 ölçek `RenderTargetBitmap` çizimiyle ve kenarların ×1.5'te tam sayıya düşüp düşmediğiyle
  dolaylı ölçüldü.
- macOS ve Linux'ta şerit ölçülmedi.
- Seçenek açılırının (flyout) yeni yerdeki konumu ölçülmedi. Düğme şeridin sağ grubunda
  kaldı, açılır yukarı açılıyor.
