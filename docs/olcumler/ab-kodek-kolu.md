# A/B'nin bizim tarafımız hangi kodeği koşuyor

A/B tablosunun VidShrink sütunu, ürünün varsayılan `auto` modunu değil
**uyumluluk kolunu** koşturuyordu. Bu belge o yapılandırma hatasını, düzeltmesini
ve düzeltmenin tabloya ne yaptığını yazar.

Ölçüm düzeneğinin kendisi ve tablolar `ab-duzenegi.md`de.

## Yapılandırma hatası — üç yerden okunuyor

`tools/VidShrink.Ab/Competitors.cs` `PlanOptions` kuruyor ve `Codec` alanını
hiç atamıyordu. Alanın varsayılanı `src/VidShrink.Core/PlanCalculator.cs:11`de
`CodecPreference.Compatible`. Uygulamanın kendi varsayılanı ise `Auto`:
`src/VidShrink.App/MainWindow.axaml:398` `CmbCodec` `SelectedIndex="0"`, ve
`MainWindow.axaml.cs:1597-1602` `CodecFromIndex(0)` → `CodecPreference.Auto`.

Koşum günlüğü bunu doğruluyor —
`.calisma/ab/gunluk/parca-1_vidshrink_3.497mb.log:1` içinde `-c:v libx264`.

## `Auto` ne zaman gerçekten kodek değiştiriyor

`CodecPreference.Auto` tek başına bir kodek değil, bir **yönlendirme**:
`PlanCalculator.cs:111-113` isteği `CompressionStrategy.AutoPreference(regime)`e
devrediyor. Rejim yalnız **oran**dan, yani `kaynak MB / hedef MB`'den geliyor
(`CompressionStrategy.cs:45-52`, oran dosya boyutundan hesaplanıyor, video
baskısından değil).

| oran (kaynak/hedef) | rejim | `AutoPreference` | seçilen kodek |
|---|---|---|---|
| < 1,5 | `Light` | `Compatible` | libx264 |
| 1,5 – 6,0 | `Balanced` | `Compatible` | libx264 |
| 6,0 – 30,0 | `Aggressive` | `MaxCompression` | libsvtav1 (yoksa libx265) |
| ≥ 30,0 | `Extreme` | `MaxCompression` | libsvtav1 (yoksa libx265) |

Kodek seçimi `PlanCalculator.cs:762-782`: `MaxCompression`in tercihi
`libsvtav1`, ffmpeg'de yoksa `libx265`. Bu makinenin ffmpeg'inde
`libsvtav1` **var** (`ffmpeg -encoders`), yani `MaxCompression` bu ölçümlerde
libsvtav1 demek.

**Yani eşik oran 6,0.** Oran 6,0'ın altındaki her satırda `Auto` ile
`Compatible` aynı şeydir; kolun düzeltilmesi o satırlarda hiçbir şeyi
değiştirmez.

### Bu ölçümün satırları eşiğin neresinde

Kaynak boyutları normalize edilmiş (yalnız video) parçaların baytı.

| girdi | kaynak MB | hedef MB | oran | rejim | `Auto` ne seçer |
|---|---|---|---|---|---|
| parca-1 | 88,289 | 3,4975 | 25,24 | `Aggressive` | libsvtav1 |
| parca-2 | 109,159 | 3,4975 | 31,21 | `Extreme` | libsvtav1 |
| parca-3 | 93,904 | 3,4984 | 26,84 | `Aggressive` | libsvtav1 |
| parca-1 | 88,289 | 34,9745 | 2,52 | `Balanced` | libx264 |
| parca-2 | 109,159 | 34,9861 | 3,12 | `Balanced` | libx264 |
| parca-3 | 93,904 | 34,9936 | 2,68 | `Balanced` | libx264 |

Kodek yalnız **60 MB hedefinin üç satırında** değişiyor. 600 MB hedefinin üç
satırında `Auto` da `Compatible` veriyor; o satırlar düzeltmeden önce ve sonra
aynı yapılandırmayı koşuyor.

## Kodeği değişmeyen satırlarda açık nereden geliyor

600 MB'ın üç satırında biz libx264, HandBrake x265-slow-multipass koşuyor. Bu
açık **ölçüm hatası değil, ürünün kendi kuralı**: oran 6,0'ın altında kaldığı
için `AutoPreference` uyumluluğu seçiyor ve H.264'te kalıyor.

Kuralın gerekçesi kodda yazılı değil; ürünün kullanıcıya söylediği cümlede
duruyor (`src/VidShrink.App/Locales/tr/main.json:293`,
`AdviceCode.CodecUpgradeRecommended`):

> Bu sıkışıklıkta H.265 aynı boyutta gözle görülür şekilde daha iyi sonuç
> verir; sıkıştırma algoritmasını otomatik veya H.265 yapmayı düşün.

Yani gerekçe "yalnız **sıkışıklıkta** kodek yükselt": düşük oranda uyumluluk
(H.264'ün her yerde açılması) modern kodeğin kazancından daha değerli sayılıyor.
Eşik 6,0'ın **nereden geldiği** kodda da belgede de yazılı değil — `ölçülmedi`.

`CompressionStrategy.cs` bu sözleşmenin `owns`unda değil; kural ölçüldü ve
yazıldı, değiştirilmedi.

İki not, ikisi de bu sözleşmenin dışında, ikisi de dokunulmadı:

- **Eşik sert, histerezis yok.** Oran 5,999 → 6,001 geçişinde kodek ve kare
  hızı izni **aynı anda** değişiyor. Aynı bulgu `docs/inceleme/model-strateji.md:38-42`de
  zaten yazılı (T103, "Rejim eşikleri sınırda sert").
- **Tavsiye metni ile kod ayrışıyor.** Metin "H.265" diyor; `MaxCompression`in
  tercihi `libsvtav1`, yani AV1. libx265 yalnız yedek.
