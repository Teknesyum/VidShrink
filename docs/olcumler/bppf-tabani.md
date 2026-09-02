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

Tavanın 1,0'da kalması boşuna değildi: şikâyet işinin kaynağında prob hareket üstelini
**1,163** ölçüyor — eski tavan bu kaynağı 1,0'a kırpıyordu. Kırpılmış değerle 1280x720@30'un
tabanı 0,03514, ölçülen değerle 0,03935; ikisi de o yerleşimi eliyor, ama ikincisi ölçüme
dayanıyor.

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

## 8. Mutasyon kanıtı

Ölçüler tabanı gerçekten sınıyor mu? Üç sabit tek tek bozuldu, her seferinde
yalnız kendi filtresi koşuldu (`PlanCalculatorTests|ComplexityScanTests|ExtremeCompressionTests`,
73 ölçü). Bozulmamış ağaçta 70 geçti, 0 kaldı, 3 atlandı (atlananlar canlı ffmpeg isteyenler).

| # | Bozulan | Nereden nereye | Kırılan ölçü |
|---|---------|----------------|--------------|
| M4 | `CodecModel.FloorBppf` av1 tabanı | 0.020 → 0.025 | `ACheaperFamilyClearsALayoutTheDearerFamiliesReject` |
| M5 | `CodecModel.HardwareFloorFactor` | 1.25 → 1.0 | `TheHardwareFloorRejectsALayoutTheSoftwareFloorAccepts` |
| M6 | `PlanCalculator.LayoutClearsFloor` bppf koşulu | koşul silindi | 5 ölçü birden |

M6'da kırılanlar: yukarıdaki ikisi, `NoTargetEverLandsUnderTheFloorWithoutSayingSo`,
`OneMegabyteTargetCollapsesThePixelRateAndNeverDropsUnderTheFloorSilently`,
`ATargetNoLayoutCanCarryIsReportedInsteadOfPretended`.

Eleme koşulu artık `SearchLayout`'un içinde gömülü değil; `PlanCalculator.LayoutClearsFloor`
olarak dışarı alındı ve ölçüler üretim yolunun kendisini çağırıyor — koşulun kopyasını değil.
M6 bu yüzden beş ölçüyü birden düşürüyor.

### 8.1 Kaldırılan sabit kopyası

`CodecFloorsAreStatedPerCodecAndRaisedForHardware` altı satırda `Assert.Equal(sabit, sabit)`
yazıyordu; taban değiştiğinde sayıyı iki yerde güncellemek gerekiyordu ve davranış hakkında
hiçbir şey söylemiyordu. Yerine `ACheaperFamilyClearsALayoutTheDearerFamiliesReject` geldi:
1280x720@60'ta av1 ile hevc tabanlarının **arasındaki** bir bppf'te av1 geçiyor, hevc eleniyor;
hevc ile h264 arasındaki bir bppf'te hevc geçiyor, h264 eleniyor. Sayı yazılmıyor, sıra ölçülüyor.

`TheFloorFollowsTheContentAndTheFrameRate` içindeki `Assert.Equal(0.035, ...)` de
`CodecModel.FloorBppf("libx264")` çağrısına çevrildi — ölçtüğü şey ölçülmemiş profilin
tabanı oynatmaması, 0.035 sayısının kendisi değil.

## 2. Beş yerleşim, aynı boyut, ölçülen kalite

Şikâyet işi: 1036,17 sn 1920x1080@60 HDR kaynak (bt2020/smpte2084/bt2020nc, hevc
yuv420p10le), 117 MiB hedef. Motorun bu hedefte videoya ayırdığı pay 790k'dır; beş satır
da **aynı `-b:v 790k`** ile kodlandı, tek değişken yerleşim oldu.

Ortak kodlama: `av1_nvenc -preset p5 -rc vbr -multipass fullres -pix_fmt p010le`,
GOP her satırda 2 saniye (60 fps'te 120, 30 fps'te 60), `-threads 4` sabit, ses/altyazı
atıldı, renk etiketleri elle taşındı. Kalite VMAF-NEG (`vmaf_v0.6.1neg`), çıktı 1920x1080'e
bicubic ile geri ölçeklenip kaynakla karşılaştırıldı. İki pencere ölçüldü: raporun tarihsel
penceresi **480–540 sn** ve bağımsız bir ikinci pencere **200–260 sn**.

### 2.1 Sonuç

| Yerleşim | bppf | Boyut (MiB) | 480: ort / harm / p10 | 200: ort / harm / p10 |
|---|---|---|---|---|
| **1280x720@60** | 0,01429 | 95,33 | **68,81 / 46,67 / 26,49** | **48,14 / 32,45 / 18,12** |
| 1280x720@30 | 0,02857 | 96,40 | 69,32 / 34,03 / 23,69 | 47,47 / 27,41 / 11,33 |
| 882x496@60 (bugünkü seçim) | 0,03010 | 95,42 | 62,42 / 43,54 / 26,18 | 45,95 / 31,20 / 17,40 |
| 960x540@60 | 0,02540 | 96,13 | 63,58 / 44,14 / 26,10 | 45,85 / 31,76 / 17,96 |
| 960x540@30 | 0,05080 | 97,41 | 63,84 / 32,86 / 24,57 | 44,61 / 26,52 / 11,49 |

Boyutlar %2,2 bandında (95,33–97,41 MiB); karşılaştırma eşit boyutta yapıldı sayılır.
Harmonik ortalama libvmaf'ın havuzlama tanımıyla hesaplandı (`n / Σ 1/(x+1) − 1`);
düz harmonik ortalama, aşağıdaki sıfır puanlı kareler yüzünden her satırda 0 çıkıyor.
**Harmonik sütununa yaslanmayın — T106 soruşturuyor:** VMAF-NEG'in AV1 çıktılarında
sıfır veren kareleri harmoniği aşağı çekiyor. Aşağıdaki karar ortalama ve p10 üzerinden verildi;
harmonik sütun çıkarılsa da kazanan değişmiyor.

**Kazanan 1280x720@60.** Altı ölçüden beşini alıyor. Kaybettiği tek ölçü 480 penceresinin
düz ortalaması (69,32'ye karşı 68,81, fark 0,51); aynı satır aynı pencerede harmonikte 12,64,
p10'da 2,80 geride. Harmonik iki sütun tamamen atıldığında da sıralama aynı: kalan dört
ölçünün üçünde birinci (200 ort, 480 p10, 200 p10), 480 ortalamasında 0,51 farkla ikinci. Bugünkü seçime (882x496@60) karşı üstünlüğü iki pencerede de aynı yönde:
480'de +6,39 ort / +3,13 harm / +0,31 p10, 200'de +2,19 / +1,25 / +0,72.

Sözleşmedeki liste "960x540" ile "540p60"ı ayrı sayıyordu; bunlar tek yerleşimin iki adı.
Beşinci satır olarak 960x540@30 kondu, dördüncü satır 960x540@60'tır.

### 2.2 Hangi kolda ölçüldü

Beş satır da **tek denemede** çıktı; hiçbiri motorun `PlanCalculator.Correct` yeniden
deneme yoluna girmedi, çünkü ffmpeg doğrudan sabit `-b:v` ile çağrıldı. `-multipass fullres`
zaten iki geçişli VBR'dır — yani tablo, T100'ün ölçtüğü "CRF atılıp VBR'a düşülen" kolun
**içinde**, satırlar arasında adil. Ölçülmeyen şey, tabanı düşürünce daha düşük bppf'li bir
yerleşimin ilk denemede hedefi tutturamayıp `Correct`'e düşme olasılığının bedelidir:
**ayrılmadı**. Kazanan yerleşim 0,01429 bppf'te 95,33 MiB verdi, 117 MiB hedefin dolum
bandının altında kalmadı; bu satır için yeniden deneme beklenmiyor, ama bu tek koşumluk bir
gözlem, ölçüm değil.

### 2.3 T95 kapıları (elle)

`ffprobe` beş çıktıda da aynı: `yuv420p10le`, `color_space=bt2020nc`,
`color_transfer=smpte2084`, `color_primaries=bt2020`, akış sayısı 1 (yalnız video).
Kare hızı ve geometri yerleşimin kendisi. Ölçüm bu yüzden renk yolu ya da akış farkı
taşımıyor.

### 2.4 Yolda görülen: çöken kareler her satırda aynı yerde

Her satırda 33–62 kare 5 VMAF-NEG'in altına düşüyor (n=3600). Düşük kareler bütün
satırlarda **aynı indislerde** (480 penceresinde 2471–3231 arası, yani kaynağın 521–534.
saniyeleri). Bu, ölçüm hizasızlığı değil — hizasızlık bütün kareleri düşürürdü. Bu hedef
boyutta o sahne yerleşimden bağımsız olarak çöküyor. T106 aynı olguyu ölçünün kendi
tarafından soruşturuyor — sıfır puanlı karelerin ne kadarı kodlayıcının, ne kadarı ölçünün. p1 sütunu bu yüzden yerleşimleri
ayırmıyor (2,93–5,15), p10 ayırıyor.
