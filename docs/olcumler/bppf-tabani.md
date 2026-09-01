# Bppf tabanı

Sözleşme: T99. Tarih: 2026-09-02. Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4`
(1920x1080, hevc, yuv420p10le, bt2020/smpte2084/bt2020nc, 60 fps, 1036,17 sn).

Bu dosya `CodecModel.FloorBppf` tabanının nereden geldiğini, eş boyutta yapılan
yerleşim taramasını ve tabanın ölçümden sonra nereye konduğunu kaydeder.

## 1. Tabanın kökeni

Aranan sabitler: `CodecModel.FloorBppf` (av1 0,020 · hevc 0,025 · diğer 0,035),
`CodecModel.HardwareFloorFactor` (1,25) ve tabanı içeriğe uyarlayan
`ComplexityProfile.FloorComplexityAnchor` / `FloorAdaptExponent` / `FloorAdaptMin,Max`.

Beşi de aynı commit'te girdi: `6fac0be` — 2026-08-25, "Measure what to keep when the
target is brutally small", T4 sözleşmesi. Commit mesajı sabitlerin ne yaptığını
anlatıyor, sayıların nereden geldiğini anlatmıyor. Sayılar kodda ilk kez burada
görünüyor ama metinde daha eskiler: `d2f1eb9` (2026-08-19) T4 sözleşmesini eklerken
onları **"başlangıç değerleri"** diye reçete ediyor.

| Sabit | Değer | Dayanak | Nerede |
|---|---:|---|---|
| `FloorBppf` diğer (h264) | 0,035 | **Dayanak bulunamadı — kabul.** T4 yapıcısının kendi beyanı: eğride diz yok, 0,035 ölçümle çelişmiyor ama ondan türetilmiş de değil, pürüzsüz bir eğri üzerinde seçilmiş politika çizgisi. | `contracts/done/T4.md:44-45`, `:204-207` |
| `FloorBppf` hevc | 0,025 | **Dayanak bulunamadı.** Hiç koşulmadı. | `contracts/done/T4.md:213` |
| `FloorBppf` av1 | 0,020 | **Dayanak bulunamadı.** Hiç koşulmadı. | `contracts/done/T4.md:213` |
| `HardwareFloorFactor` | 1,25 | **Dayanak bulunamadı.** Tek donanım koşusu yok; sözleşme yalnız NVENC derken kod QSV/AMF'ye de uyguluyor, denetim bunu kusur olarak yazmış. | `contracts/done/T4.md:45`, `:213`, `:298-299`; `docs/motor-dogrulama-raporu.md:228-229` |
| `FloorComplexityAnchor` | 0,1264 | **Ölçüm.** Tek klipte (gothic, 830,2 MB / 52,6 sn / 1920x1080@48) probun okuduğu bias düzeltilmiş referans bppf. Bağımsız olarak T7 ve T2c'de de aynı sayı. Sınırı: tek klip, ve hız moduna göre kayıyor. | `contracts/done/T4.md:172-173`; `T7.md:199-200`; `T2c.md:164` |
| `FloorAdaptExponent` | 0,5 | **Dayanak bulunamadı — seçim.** Yapıcı beyanı: karekök iki farklı içerikle ölçülmüş bir üs değil. | `contracts/done/T4.md:141-143`, `:208-209` |
| `FloorAdaptMin` / `Max` | 0,6 / 1,6 | **Dayanak bulunamadı.** Kodda yorum, sözleşmede gerekçe, `docs/` altında kayıt yok. | — |

T4'te gerçek bir ölçüm var ama tabanı seçmiyor: `libx264` ile 640x360@24'te bppf
taraması (0,010 → VMAF-NEG 21,4 · 0,035 → 47,3 · 0,090 → 74,7). Bu tablo 0,035
noktasındaki kaliteyi gösteriyor, 0,035'in neden sınır olduğunu değil. Ölçüm
`ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture`
ile üretiliyor ve hâlâ koşuyor.

`docs/olcumler/` altında bu sabitlere ait başka hiçbir dosya yok; bu dosya ilki.

Ayrıca döngüsellik: `ExtremeCompressionTests.CodecFloorsAreStatedPerCodecAndRaisedForHardware`
sabitleri birebir kopyalıyordu (`Assert.Equal(0.035, ...)`, `Assert.Equal(0.035 * 1.25, ...)`).
Bu bir doğrulama değil, sabitin ikinci kopyasıdır: sabiti bozan bir mutasyon testi de
bozar, ama davranışın bozulduğunu göstermez. Bölüm 6'ya bakınız.

## 7. Ölçülen dağılımın dışındaki üç sabit

T89 üç kaynakta 13 pencerede hareket üstelini ölçtü: en küçük **0,597** · medyan
**0,871** · ortalama **0,859** · en büyük **1,319**
(`docs/olcumler/olculen-kaliteyle-plan.md:266-276`). T89 üçünü de değiştiremedi,
çünkü `tests/VidShrink.Tests/ExtremeCompressionTests.cs` `owns` dışındaydı
(`olculen-kaliteyle-plan.md:298-302`, `:354-357`). Bu turda o dosya bende.

| Sabit | Eski | Yeni | Dayanak |
|---|---:|---:|---|
| `ComplexityProfile.DefaultMotionExponent` | 0,25 | **0,871** | Ölçülen 13 pencerenin **medyanı**. Eski değer ölçülen her noktanın altındaydı; ölçüm yokken kullanılan geriye dönüş sabiti artık dağılımın ortasında. `CodecModel.FpsBitrateExponent`'ten türetilmesi kaldırıldı — o sabitin başka kullanıcısı yoktu. |
| `PlanCalculator.MotionCutIsExpensiveAbove` | 0,5 | **log2(1,8) ≈ 0,848** | T89'un "ucuz" eşiği için kullandığı birim: tasarruf oranı. Kare hızını yarıya indirmek bitlerin **%10'undan azını** kurtarıyorsa pahalıdır. 0,848 ölçülen ortalamanın (0,859) ve medyanın (0,871) hemen altında, yani eşik dağılımın içinden geçiyor. Eski 0,5 ölçülen her noktanın altındaydı: ayrım yapmıyor, yalnız "aksi halde" dalıydı. |
| `ComplexityProfile.MotionExponentMax` | 1,0 | **1,4** | Ölçülen en büyük iki pencere (1,296 ve 1,319) eski tavana kırpılıyordu; yeni tavan ikisini de geçiriyor. **Tavanın kendisi ölçülmedi** — üstelin fiziksel olarak nerede durduğunu gösteren bir ölçüm yok, bu yüzden 1,4 keyfidir. Tek dayanağı ölçülen en büyüğü kırpmaması. |

`MotionExponentMin = 0,0` değişmedi: ölçülen en küçük 0,597 ve kelepçe hiçbir
ölçülen noktayı kesmiyor.

### 7.1 Değişikliğin görülen etkisi

Geriye dönüş üsteli 0,25 → 0,871 olunca kare hızını düşürmek artık ucuz görünmüyor
ve plan aynı hedefte kare yerine çözünürlük veriyor. 830 MB / 52,6 sn / 1920x1080@48
kaynağında 1 MB hedefi: eskiden kare hızı 15'in altına düşüyordu, şimdi 384x216@25
(kaynak piksel hızının %2,1'i). Ölçüm kare hızı düşürmenin ucuz olmadığını söylüyor;
plan artık onu takip ediyor.

### 7.2 Testler sabiti kopyalamayı bıraktı

`MotionExponentComesFromTheHalfFrameRateSample` içindeki `Assert.Equal(0.25, ...)`
kaldırıldı — sabitin ikinci kopyasıydı. Yerine üç davranış ölçüsü:

- `TheUnmeasuredFallbackPricesAFrameRateCutInsideTheMeasuredRange` — ölçüm yokken
  yarı kare hızının fiyatı ölçülen en küçük ve en büyük üstelin arasında kalmalı.
  Sabiti 0,25'e geri almak kırar.
- `TheMotionCeilingDoesNotClipTheDearestMeasuredWindow` — 1,319 üsteli üreten bir
  hareket örneği kelepçeden geçmeli. Tavanı 1,0'a geri almak kırar.
- `TheAdviceBandSplitsTheMeasuredMotionDistribution` — 0,597 ucuz, 0,75 hiçbiri,
  0,871 pahalı. Eşiği 0,5'e geri almak orta noktayı pahalı yapar ve kırar.

### 7.3 Yolda görülen, kapsam dışı

`OneMegabyteTargetCollapsesThePixelRateAndNeverDropsUnderTheFloorSilently`
1 MB hedefinde planı 0,0617 bppf'te, tabanın (0,0618) **0,2 kbit/sn altında**
buluyor ve motor `TargetBelowCodecFloor` demiyor. Fark tamsayı bit hızı
yuvarlamasının bir adımından küçük; nedeni bu turda doğrulanmadı. Ölçü bir kbit'lik
payı açıkça adlandırıyor, sessizce yutmuyor. Aramanın gördüğü bit hızı ile plana
yazılan tamsayı bit hızı arasındaki bu tutarsızlık ayrı bir işe aittir.
