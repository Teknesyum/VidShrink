# Meta Veriyi Sil: Dönüştür Ve Düzenleyici

Tarih: 2026-10-07. ffmpeg 9.0, `-threads 2`, tek süreç, lavfi kaynaklı 2-4 sn'lik klipler
(320x240). Ölçüyü `MetaSilDonusturTests` ve `MetaSilDuzenleyiciTests` canlı kolları üretir;
klipler test bitince silinir.

## Ne Yazılıyor

İki yüz de küçültmenin dizisini aynı fonksiyondan alır (`StreamMapping.MetadataArguments`):
`-map_metadata -1`, ardından taşınan ses izinin dili `-metadata:s:a:0 language=<dil>`.
Kopya yok; küçültmenin `StreamPlan.OutputArguments`'ı da aynı fonksiyonu çağırır.

## Dönüştür

Kaynak: başlıklı mp4/mkv, video izinde başlık, iki ses izi (eng, tur), iki adlı bölüm.

| Alan | Kutu kapalı | Kutu açık |
| --- | --- | --- |
| Kap başlığı (`title`) | taşınır | düşer |
| Video izi başlığı | taşınır | düşer |
| Bölüm adları | taşınır | düşer |
| Bölüm işaretleri (2) | kalır | kalır |
| Ses dili | kalır | kalır (açıkça yazılır) |

Dönüştür ses izini kendi eşlemez, ffmpeg seçer. Dil o izden yazılır
(`ConversionArguments.AutoAudio`): varsayılan işaretli iz, yoksa en çok kanallı, eşitlikte ilk.
Ölçüm: varsayılan işaretli tek kanallı `eng` izi iki kanallı `tur` izinin önüne geçti, çıktıda
`eng` okundu; işaret yokken `tur`.

GIF dalına meta argümanı yazılmaz (komut kutuyla değişmez). Altyazı dili yalnız tek altyazı
izinde yazılır.

## Düzenleyici

Düzenleyici kendi başına etiket ya da bölüm yazmaz; korunacak kasıtlı meta yok. Kaynak:
başlığı "Gizli Baslik", ses dili `tur` olan 4 sn'lik mp4; düzenlemesiz tek parça teslim.

| Kip | Kutu | Başlık | Ses dili |
| --- | --- | --- | --- |
| Akıllı (concat listesi) | kapalı | yok | tur |
| Akıllı | açık | yok | tur |
| Tam (`filter_complex`) | kapalı | Gizli Baslik | und |
| Tam | açık | yok | tur |

Okuma: concat listesinden okuyan kollar (Hızlı, Akıllı) kap başlığını kutu olmadan da
taşımıyor; kutunun gözle görülür etkisi Tam kipte. Tam kipte kutu kapalıyken süzgeç grafından
çıkan sesin dili `und` oluyor, kutu açıkken dil açıkça yazıldığı için `tur` kalıyor. Hızlı kip
canlı ölçülmedi; argümanı Akıllı ile aynı son adımı paylaşır ve argüman koluyla pimli.

Kutu değişince aynı düzenleme yeniden yazılır (kayıt anahtarına `|meta` girer); değişmeyince
ikinci Kaydet yeniden kodlamaz. Parçaları ayrı yazarken her parçanın son adımı diziyi alır.

## Mutasyonlar

24 kol (üç sınıf, kare hızı gerekçesiyle birlikte). Her mutasyon elle geri alındı.

| Mutasyon | Kırmızı |
| --- | --- |
| Dönüştür'ün `MetadataArgs`'ı hep boş | 9 |
| `ReadConversionPlan` kutuyu okumaz | 1 |
| `convertDropMetadata` geri yüklenmez | 1 |
| `convertDropMetadata` ve `editorDropMetadata` yazılmaz | 2 |
| Düzenleyici `WithoutMetadata`'yı atlar | 5 |
| Düzenleyici dili yazmaz | 3 |
| Kayıt anahtarından `|meta` çıkar | 1 |
| Görünüme saklanan seçim verilmez | 1 |
| `DropMetadataChanged` çağrılmaz | 2 |
| Tavanlı kipte not planın hızını söyler | 1 |
| CLI satırı yazılmaz | 1 |
| Pencere gerekçesi eklenmez | 1 |
| Kopyalama korumasını kaldırmak | 1 |

## Ölçülmeyenler

Gerçek pencerede elle deneme yapılmadı. Hızlı kip canlı koşulmadı. mp4 dışındaki kaplarda
düzenleyici teslimi (mkv, mov) canlı ölçülmedi.
