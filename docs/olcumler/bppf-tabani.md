# Bppf tabanı

Sözleşme: T99. Tarih: 2026-09-02. Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4`
(1920x1080, hevc, yuv420p10le, bt2020/smpte2084/bt2020nc, 60 fps, 1036,17 sn).

Bu dosya `CodecModel.FloorBppf` tabanının nereden geldiğini, eş boyutta yapılan
yerleşim taramasını ve tabanın ölçümden sonra nereye konduğunu kaydeder.

## 1. Tabanın kökeni

Bu bölüm **tarihtir**: aşağıdaki sayılar T99 öncesinin kodudur. İkisi bu turda değişti —
av1 tabanı 0,020 → 0,0095 ve `HardwareFloorFactor` 1,25 → 1,52; yeni değerler ve dayanakları
bölüm 3, 4 ve 5'te. Geri kalan sabitler değişmedi.

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
bozar, ama davranışın bozulduğunu göstermez. Bölüm 8.1'e bakınız.

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

## 3. Taban ölçülen kazanana göre yeniden kondu

K2'nin kazananı bugünkü seçim değil, bu yüzden taban düşürüldü. İki sabit değişti:

| Sabit | Eski | Yeni | Dayanak |
|---|---:|---:|---|
| `CodecModel.FloorBppf` av1 tabanı | 0,020 | **0,0095** | Bölüm 4: yazılım kolunun doygunluk noktası 0,00669 teslim bppf; içerik uyarlaması (0,7028) ile geri çözülünce 0,00952. |
| `CodecModel.HardwareFloorFactor` | 1,25 | **1,52** | Bölüm 5: p10 yıkım noktasının donanım/yazılım oranı (nvenc 0,01387, svt 0,00912). |

`hevc` (0,025) ve `h264` (0,035) tabanları **değişmedi** — bu turda ölçülmediler. K1'deki
"dayanak bulunamadı" damgaları ikisinde de duruyor.

Şikâyet kaynağında (`ReferenceBppf = 0,06244`, `FloorAdaptation = 0,7028`) uygulanan
tabanlar, 60 fps'te:

| Kodlayıcı | Eski taban | Yeni taban |
|---|---:|---:|
| `libx264` | 0,02460 | 0,02460 |
| `libx265` | 0,01757 | 0,01757 |
| `libsvtav1` | 0,01406 | **0,00668** |
| `av1_nvenc` | 0,01757 | **0,01015** |
| `hevc_nvenc` | 0,02196 | **0,02671** |

Kazanan yerleşim 0,01429 bppf'te: eski taban 0,01757 onu **eliyordu**, yeni taban 0,01015
**geçiriyor**. Ölçüm bunu doğruluyor — `LayoutClearsFloor(av1_nvenc, 790k, 1280x720@60)`
artık `True`.

### 3.1 Taban tek sebep değilmiş

Taban düşürüldükten sonra plan yeniden koşuldu ve **hâlâ 882x496 seçiyor**:

```
bench shrink <kaynak> 117 --plan-only --force-codec av1_nvenc
-> scale=882:496  -b:v 801k
```

Yani 1280x720@60 artık **aday**, ama `PlanCalculator.LayoutScore` onu 882x496'ya karşı
kaybettiriyor. Skor = tahmin edilen kalite − `ScalePenalty` − `FpsPenalty`; ölçüm ise
480 penceresinde 1280x720@60'ı 882x496'nın **6,39 puan önünde** buluyor. Skor modelinin
tahmini ile ölçülen sıralama bu kaynakta ters. Sözleşme tabanı sorduğu için skor modeli
bu turda **değiştirilmedi**; ölçüsüz oynatılacak bir sabit değil, ayrı bir işe ait.

Açığın büyüklüğü hakkında bu tur şunu söyleyebilir: tabanı doğru yere koymak şikâyet
işini tek başına çözmüyor. Eleme kalktı, seçim değişmedi.

### 3.2 HandBrake'in çalışma noktası hâlâ dışarıda — ama tabandan değil

Sözleşme HandBrake'in kazanan dosyasının 0,0116 bppf'te koştuğunu söylüyor. Yeni tabanın
(0,01015) üstünde, yani taban artık onu elemiyor. Buna karşılık `LayoutClearsFloor`'un
ikinci koşulu eliyor: `CodecModel.UsableBitrateK("av1_nvenc", 1280, 720, 60)` = **706k**,
0,0116 bppf ise 641k. Bu sınır donanım kodlayıcısının istenen bit hızını takip etmeyi
bıraktığı yer ve **ölçülmüş** (`CodecModel.cs:22-33`, T4). 1280x720@60'ta 706k = 0,01277
bppf, yani bu yerleşimde donanım yolunda bağlayıcı olan **taban değil, o sınır**; yeni taban
0,01015 ile onun altında kalıyor ve atıl.

Kazanan yerleşim 790k'da, sınırın üstünde. HandBrake yazılım kodlayıcı kullandığı için o
sınıra tabi değil; biz donanım kolundayken aynı noktaya inemeyiz. Bu, bu turda kapatılmayan
gerçek bir fark.

## 4. Alt sınır ne koruyor — yıkım ölçüldü

Süpürme: kaynağın **480–540 sn** penceresi (bölüm 2.4'teki çöken sahnenin bulunduğu
pencere, yani en zor 60 saniye), yerleşim 1280x720@60 sabit, kol başına sekiz bit hızı.
Eksende **teslim edilen** bppf var, istenen değil. Kalite VMAF-NEG, 1080p'ye geri
ölçeklenip kaynakla karşılaştırıldı; `-threads 4` sabit.

Yazılım kolu (`libsvtav1 -preset 6 -svtav1-params lp=4`) — tabanın türetildiği kol:

| bppf | ort | p10 | 5'in altındaki kare (n=3600) |
|---:|---:|---:|---:|
| 0,00518 | 66,14 | 11,77 | 133 |
| 0,00669 | 68,87 | 12,23 | 128 |
| 0,00842 | 70,85 | 17,92 | 95 |
| 0,00987 | 72,28 | 25,66 | 63 |
| 0,01231 | 74,04 | 34,41 | 40 |
| 0,01519 | 75,30 | 40,32 | 34 |
| 0,02083 | 77,02 | 48,51 | 23 |
| 0,02809 | 78,18 | 54,24 | 18 |

Yarılanma başına düşen p10 puanı, düşükten yükseğe:

```
0,00518 -> 0,00669   1,24
0,00669 -> 0,00842  17,18
0,00842 -> 0,00987  33,71   <- en dik
0,00987 -> 0,01231  27,48
0,01231 -> 0,01519  19,47
0,01519 -> 0,02083  17,99
0,02083 -> 0,02809  13,27
```

Okunan şey: yıkım 0,0084–0,0099 arasında en hızlı; **0,00669'un altında eğri düzleşiyor**
(bir yarılanma 1,24 puana mal oluyor, üstündeki aralıkta 17,18'e). Sebebi tabloda görünüyor —
o noktada karelerin %3,6'sı zaten sıfırda ve p10 dibe oturmuş. Aşağıda kaybedilecek bir şey
kalmadığı için eğri yatıyor. Taban buraya kondu: **0,00669 / 0,7028 = 0,00952 → 0,0095**.

### 4.1 Bu bir seçim, ve hangi seçim olduğu

Ölçüm iki farklı yere sınır koymaya izin veriyordu: yıkımın **başladığı** yer (~0,0099) ya da
**doygunluğa ulaştığı** yer (~0,0067). Doygunluk seçildi. Gerekçe ölçüm değil, mekanizmanın
ne olduğu: taban sert bir **eleme kapısı**, kalite pazarlığı değil. Kaliteyi tartan yer
`LayoutScore`; kapı yalnız geri dönüşü olmayanı kesmeli. Yıkımın başladığı yere konan bir
kapı tam olarak bu turun şikâyetini üretiyor — bölüm 2 kazananın 0,01429'da olduğunu
gösteriyor, yıkımın başladığı noktanın çok üstünde.

Bu ayrımın kendisi **ölçülmedi**; "kapı doygunlukta durmalı" bir politika kararıdır. Ölçülen
şey doygunluğun nerede olduğudur.

### 4.2 Sınırlar

- Tek kaynak, tek pencere, tek yerleşim, tek kodlayıcı ailesi (AV1). `hevc` ve `h264`
  tabanları bu ölçümün dışında kaldı ve değişmedi.
- Doygunluk noktası ölçülen iki nokta arasında (0,00518 ile 0,00842) sıkışıyor; 0,00669
  bunların arasındaki ölçülmüş noktadır, ara değer değil. Daha sık ızgara sınırı daraltır.
- Donanım kolunda doygunluğun kendisi ölçülemedi: av1_nvenc bu yerleşimde 0,0066 bppf'in
  altına inmiyor (bölüm 5), yani doygunluk bölgesi kodlayıcının erişemediği yerde.

## 5. `HardwareFloorFactor = 1.25` yargılandı

Aynı yerleşim (1280x720@60), aynı kaynak, aynı istenen bit hızları; tek fark kodlayıcı.
Donanım kolu `av1_nvenc -preset p5 -rc vbr -multipass fullres`, yazılım kolu
`libsvtav1 -preset 6 -svtav1-params lp=4`. İki kolda da `-maxrate`/`-bufsize` yok
(libsvtav1 VBR bunları kabul etmiyor; hız denetimi karşılaştırılabilir kalsın diye
donanım kolundan da çıkarıldı) ve iş parçacığı dörde sabitlendi. Kalite yine VMAF-NEG,
çıktı 1080p'ye geri ölçeklenip kaynakla karşılaştırıldı. Eksende **istenen** değil
**teslim edilen** bppf var: av1_nvenc bazı noktalarda istenenin altında kalıyor
(0,0080 istendi, 0,0066 teslim etti), yazılım kolu da isteneni tam tutmuyor.

İki pencere, dört okuma:

| Okuma | Pencere | Oran (donanım / yazılım) |
|---|---|---:|
| Eşit ortalama kalite, 80 / 82 / 84 | 420–480 sn (kolay) | 1,706 · 1,709 · 1,704 |
| Eşit ortalama kalite, 70 / 75 / 78 | 480–540 sn (zor) | 1,637 · 1,314 · 1,122 |
| p10 yıkım noktası (en dik aralığın geometrik ortası) | 480–540 sn | **1,52** |
| Eşit p10, 20 / 30 / 40 / 50 | 480–540 sn | 0,79 · 0,93 · 0,97 · 0,88 |

**Seçilen 1,52.** Sebebi: tabanı da üreten alet bu — p10 yıkım noktası. Bir tabanın
koruduğu olgu ortalama kalite değil, alt yüzdeliklerin çökmesidir; iki kolun bu olguyu
nerede yaşadığının oranı, tabanın ölçeklenmesi gereken orandır. Donanım kolunun en dik
p10 aralığı 0,01219–0,01578 (geometrik ortası 0,01387), yazılım kolununki
0,00842–0,00987 (ortası 0,00912); oran 1,521.

**1,25 dört okumanın hiçbirine oturmuyor.** Üç ortalama tabanlı okuma da onun üstünde
(1,12–1,71), p10 tabanlı okuma altında (0,79–0,97). 1,25 ölçümün boş bıraktığı bir yerde
duruyor.

### 5.1 p10 okuması neden ağır basmadı

Eşit-p10 okuması donanımı yazılımdan **iyi** gösteriyor (oran < 1). Aynı bppf'te sıfır
puanlı kare sayısı: yazılım 133 / 128 / 95 / 63 / 40 / 34 / 23 / 18, donanım
50 / 49 / 27 / 28 / 23 / 18 / 8 / 6 — yazılım kolunda iki-üç katı. T106 tam bu olguyu
soruşturuyor: VMAF-NEG'in SVT-AV1 çıktılarında aynı karelerde sıfır vermesi ölçünün mü
kodlayıcının mı. Artefakt çıkarsa yazılımın gerçek kalitesi ölçülenden **yüksek** demektir,
yani düzeltme oranı **yukarı** iter. Bu yüzden buradaki her sayı bir alt sınırdır ve 1,52
seçimi yüksek değil, düşük taraftadır.

### 5.2 Ölçümün kapsamı ve bir risk

Bu karşılaştırma motorun bit hızı hesabının **dışında**, doğrudan ffmpeg ile kuruldu.
Bunun sebebi `PlanCalculator`'daki iki yol farkı: `DeliveryReserveK` (11k) ve
`HardwareBitrateYield` (0,877) yalnız donanım yolunda uygulanıyor. Kodda bunun bilerek
konduğu yazılı — `PlanCalculator.cs:90-91`: yalnız donanım yolu ölçüldü, işlemci yolu
bugünkü bit hızlarını koruyor. Yani ayrım kasıtlı ve gerekçesi kayıtlı.

Risk şu: motor içinden ölçseydik, donanım kolu 790k yerine 779k ile kodlanacak (%1,4 daha
az bit) ve karşılaştırma tabanın yanında bu farkı da ölçecekti. Dışarıdan ölçmek bunu
temizliyor; buna karşılık yukarıdaki oran **saf kodlayıcı farkıdır**, motorun donanım
yolunda uyguladığı %1,4'lük ayırmayı içermez. Taban çarpanını bu orana eşitlerken
o %1,4 iki kez sayılmıyor: biri bit hızından, öteki tabandan iner ve ikisi aynı yöne bakar.

### 5.3 Ölçülmemiş yan etki

Çarpan yükselince yalnız AV1 değil, **donanım hevc ve h264 tabanları da** yükseliyor —
ve o iki taban ölçülmedi. Şikâyet kaynağında `hevc_nvenc` tabanı 0,02196'dan 0,02671'e
çıkıyor (%21,6 daha sıkı). Yön eski niyetle aynı (donanım yolu daha temkinli), ama bu bir
ölçüye dayanmayan davranış değişikliğidir; ayrı bir ölçüm işine adaydır.

Ayrıca T4 denetiminin yazdığı kusur duruyor: `IsHardware` çarpanı QSV ve AMF'ye de
uyguluyor, oysa ölçülen yalnız NVENC (bu turda da yalnız NVENC ölçüldü).

## 6. Kullanıcıya görünen gerekçe

Taban değiştiği için gerekçe cümlelerindeki sayı da değişti. Metin `AdviceCode`
anahtarlarından geliyor, gömülü dizge yazılmadı; değişen tek şey biçimlenen sayı.

Ölçü: `EveryFloorReasonQuotesTheFloorThatWasActuallyApplied` (`PlanCalculatorTests`).
Sekiz hedef boyutu (3–250 MB) süpürüyor, `TargetBelowCodecFloor` ve `FrameRateCutForFloor`
dallarının ikisini de kapsıyor ve her birinde planın gerekçesinin **o planda gerçekten
uygulanmış** tabanı alıntıladığını doğruluyor. Taban sabiti ölçünün içinde geçmiyor —
değer `result.Profile.FloorBppf(...)` ile üretim yolundan geri okunuyor, bu yüzden ölçü
taban değişince düzenlenmeden yeşil kalıyor; bu turda öyle oldu.

İki tuzak açıkça kapatıldı: süpürme hiçbir taban gerekçesine çarpmazsa ölçü sessizce
geçmesin diye `quoted >= 2` kapısı var (boş doğrulama koruması); ve sayı biçimlenirken
üretim gibi **geçerli kültür** kullanılıyor — `InvariantCulture` ile yazılan bir ölçü
tr-TR'de virgüllü çıktıyı noktalı beklerdi ve yanlış yeşil verirdi.
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

Ölçüler tabanı gerçekten sınıyor mu? Sabitler tek tek bozuldu, her seferinde
yalnız kendi filtresi koşuldu (`PlanCalculatorTests|ComplexityScanTests|ExtremeCompressionTests`,
75 ölçü). Bozulmamış ağaçta 72 geçti, 0 kaldı, 3 atlandı (atlananlar canlı ffmpeg isteyenler).
Taban ve çarpan **iki yönde** de bozuldu — yalnız düşürmek ya da yalnız yükseltmek
tek taraflı bir kapı bırakırdı.

| # | Bozulan | Nereden nereye | Kırılan ölçü |
|---|---------|----------------|--------------|
| M4 | `CodecModel.FloorBppf` av1 tabanı | 0.0095 → 0.020 (eski değer) | `TheFloorAdmitsTheLayoutThatWonTheMeasurementAndStillRejectsTheSaturatedOne` |
| M4b | aynı | 0.0095 → 0.0065 | yukarıdaki + `TheHardwareFloorRejectsALayoutTheSoftwareFloorAccepts` |
| M5 | `CodecModel.HardwareFloorFactor` | 1.52 → 1.0 | `TheHardwareFloorRejectsALayoutTheSoftwareFloorAccepts` |
| M5b | aynı | 1.52 → 2.2 | `TheFloorAdmitsTheLayoutThatWonTheMeasurementAndStillRejectsTheSaturatedOne` |
| M6 | `PlanCalculator.LayoutClearsFloor` bppf koşulu | koşul silindi | 6 ölçü birden |

M6'da kırılanlar: `TheFloorAdmitsTheLayoutThatWonTheMeasurementAndStillRejectsTheSaturatedOne`,
`NoTargetEverLandsUnderTheFloorWithoutSayingSo`, `EveryFloorReasonQuotesTheFloorThatWasActuallyApplied`,
`ACheaperFamilyClearsALayoutTheDearerFamiliesReject`,
`OneMegabyteTargetCollapsesThePixelRateAndNeverDropsUnderTheFloorSilently`,
`ATargetNoLayoutCanCarryIsReportedInsteadOfPretended`.

Taban **değerini** tutan ölçü `TheFloorAdmitsTheLayoutThatWonTheMeasurementAndStillRejectsTheSaturatedOne`:
iki uçtan sıkıştırıyor. Üstten K2'nin kazananı (1280x720@60, 790k, 0,01429 bppf) geçmek
zorunda; alttan doygunluğun altındaki 0,0052 bppf geçmemek zorunda. İkisi de bu turda
ölçülmüş noktalar, sabitin kopyası değil. Kapı av1 tabanını yaklaşık **[0,0074 – 0,0134]**
aralığına hapsediyor; 0,0095 içeride.

Sıra ölçüsü (`ACheaperFamilyClearsALayoutTheDearerFamiliesReject`) tek başına yetmiyordu:
av1 tabanını 0,0095'ten 0,020'ye geri almak aileler arası sırayı bozmadığı için o ölçüyü
kırmıyor. Değeri tutan kapı ayrıca gerekliydi — bu, ölçüyü yazarken değil, M4'ü koşarken
görüldü.

### 8.0 Bir tuzak: geri alınan mutasyon eski ikiliyi koşturuyor

Mutasyon betiği dosyayı `cp` ile yedekleyip sonra `mv` ile geri koyuyordu. Geri konan
dosyanın zaman damgası yedeğin alındığı andı, yani mutasyon koşusunda üretilen DLL'den
**eski**; MSBuild kaynağı güncel sayıp yeniden derlemiyor ve bir sonraki koşu mutasyonlu
ikiliyi ölçüyordu. Bir kez yanlış kırmızı üretti (taban 0,0095 iken ölçü 0,020 hesaplıyordu).
Betiğe geri koyduktan sonra `touch` eklendi. Aynı hatanın bilinen kardeşi: `--no-build`
ile yeşil okuyup yanlış commit'i ölçmek.


### 8.1 Kaldırılan sabit kopyası

`CodecFloorsAreStatedPerCodecAndRaisedForHardware` altı satırda `Assert.Equal(sabit, sabit)`
yazıyordu; taban değiştiğinde sayıyı iki yerde güncellemek gerekiyordu ve davranış hakkında
hiçbir şey söylemiyordu. Yerine `ACheaperFamilyClearsALayoutTheDearerFamiliesReject` geldi:
1280x720@60'ta av1 ile hevc tabanlarının **arasındaki** bir bppf'te av1 geçiyor, hevc eleniyor;
hevc ile h264 arasındaki bir bppf'te hevc geçiyor, h264 eleniyor. Sayı yazılmıyor, sıra ölçülüyor.

`TheFloorFollowsTheContentAndTheFrameRate` içindeki `Assert.Equal(0.035, ...)` de
`CodecModel.FloorBppf("libx264")` çağrısına çevrildi — ölçtüğü şey ölçülmemiş profilin
tabanı oynatmaması, 0.035 sayısının kendisi değil.

## 8.5 Tur 2: CI'da düşen üç ölçü

Tur 1 yerelde yeşildi ama CI kırmızı döndü — run `33575858190`, commit `293ced8`,
`Failed: 3, Passed: 1020, Skipped: 81, Total: 1104`. Ne sözleşmenin `verify` filtresi
(`PlanCalculatorTests|ComplexityScanTests`) ne de denetimin geniş filtresi bu üç
ölçüden birine değiyordu. Üçü de üretim kodunun kusuru değil; üçü de tur 1'in
değiştirdiği iki sabitin görülmemiş sonucu. **Bu turda `src/` altında tek satır
değişmedi**, yalnız üç ölçü düzeltildi.

Hangi sabitin hangi ölçüyü düşürdüğü tek tek ayrıştırıldı: her sabit tek başına eski
değerine döndürülüp ölçü yeniden koşuldu.

| Ölçü | Sebep | Taban geri alınınca | Üstel geri alınınca |
| --- | --- | --- | --- |
| `ChipTests.KatliGerekceBasligiSayiyiSoyler` | taban | 9 gerekçe (eski hâl) | 7 (değişmez) |
| `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched` | hareket üsteli | 12 satır hâlâ değişik | 18/18 eski listeyle birebir |
| `QualityTargetTests.SearchLandsWithinTheMeasuredTolerance` | hareket üsteli | sapma 3,375 (değişmez) | sapma ≤ 1,0, eski geçit tutuyor |

### 8.5.1 Gerekçe sayısı 9'dan 7'ye düştü — kaybolan iki satır

Ölçü 4K/60 örneğinde (`ChipTests.Sample()`, 3840x2160@59,94, 420 MB, 187,5 sn,
hedef 16 MB) katlı gerekçe başlığının `"Why These Choices · 9"` demesini bekliyordu.

Kaybolan iki satır **aynı olgunun iki yüzü**: kare hızı kesintisi.

- `AdviceCode.FrameRateReduced` strateji satırı — "Frame Rate Was Lowered To Free
  Bits For The Frames That Remain."
- `ReasonCode.FrameRateReduced` gerekçe satırı — "Frame Rate Reduced To 39.96 To
  Keep Per-frame Detail"

Taban av1'de 0,020 × 1,25 = 0,025'ten 0,0095 × 1,52 = 0,01444 bppf'e inince 59,94 fps
artık taban altında kalmıyor, plan kare hızını hiç kesmiyor ve iki satır birden
düşüyor. **Bu tam olarak sözleşmenin istediği sonuç**: taban, kaynağın kendi kare
hızındaki yerleşimi elemeyi bıraktı.

Kalan yedi satır eskisiyle birebir aynı; yerleşim 922x518@39,96'dan
**1306x734@59,94**'e, tahmini kalite **68,9'dan 74,4**'e çıktı. `TargetBelowCodecFloor`
notu ne eski ne yeni hâlde vardı — kaybolan o değil. 1080p30 örneğinde
(`ChipTests.Modest()`) sayı altı ve değişmedi.

Ölçü artık sabit sayıyı tek başına tutmuyor: başlıktaki sayının listedeki madde
sayısıyla aynı olduğu da iddia ediliyor, yani başlığın listeyi yanlış sayması da kırar.

### 8.5.2 Altın parmak izinin 12 satırı değişti — sebep hareket üsteli

`SpeedModeTests.QualityModeLeavesTodaysPlansUntouched` 18 satırlık bir liste tutuyor
(üç kodlayıcı tercihi × üç hedef boyut × iki doldurma politikası). On ikisi değişti.

**Değişimin tamamı `ComplexityProfile.DefaultMotionExponent` 0,25 → 0,871'den geliyor.**
Taban değişikliği bu listede tek satır bile oynatmadı: yalnız `CodecModel` sabitleri
eski değerine alınınca liste yine bugünkü hâline çıkıyor, yalnız üstel eski değerine
alınınca liste 18/18 eski beklentiyle birebir oturuyor.

0,25'te kare hızını yarıya indirmek bitlerin %40,5'ini kurtarıyor görünüyordu; T89'un
ölçtüğü 0,871'de kurtardığı **%8,6**. Kare hızı kesmek artık yer açmadığı için arama
kesmiyor. Değişen satırların hepsi bu yönde:

| Satır | Eski | Yeni |
| --- | --- | --- |
| `Compatible\|25\|FillTarget` | 806x454@20 | 806x454@30 |
| `Compatible\|25\|QualityCeiling` | 806x454@20 | 806x454@30 |
| `Compatible\|8\|FillTarget` | 576x324@6 | 576x324@30 |
| `Compatible\|8\|QualityCeiling` | 576x324@6 | 576x324@30 |
| `MaxCompression\|25\|FillTarget` | 806x454@20 | 806x454@30 |
| `MaxCompression\|25\|QualityCeiling` | 806x454@20 | 806x454@30 |
| `MaxCompression\|8\|FillTarget` | 614x346@6 | 576x324@30 |
| `MaxCompression\|8\|QualityCeiling` | crf 32 / 433k / 614x346@6 | 2pass / 470k / 576x324@30 |
| `Auto\|25\|FillTarget` | 806x454@20 | 806x454@30 |
| `Auto\|25\|QualityCeiling` | 806x454@20 | 806x454@30 |
| `Auto\|8\|FillTarget` | 614x346@6 | 576x324@30 |
| `Auto\|8\|QualityCeiling` | crf 32 / 433k / 614x346@6 | 2pass / 470k / 576x324@30 |

Değişmeyen altı satırın hepsi 180 MB hedefinde: o bütçede zaten kare hızı
kesilmiyordu. Liste yenilendi, değişen her satır burada ve ölçünün kendi belgesinde
yazılı.

### 8.5.3 Ters çevirme sapması 0,833'ten 3,375 puana çıktı

`QualityTargetTests.SearchLandsWithinTheMeasuredTolerance` istenen kaliteyi hedef
boyuta çeviriyor ve dönen hedefin ne kadar aştığını ölçüyor. Geçit 1,0 puandaydı
(ölçülen 0,833'ün yukarı yuvarlanmışı). Şimdi ölçülen en kötü sapma **3,375 puan**:
`capture.mkv` Sharing, istenen 55,5 → 23,2606 MB / **58,875**.

Sebep taban değil, yine hareket üsteli. Hedefi 0,1 MB adımlarla tarayınca:

| Hedef | Kazanan yerleşim | Tahmini kalite |
| --- | --- | --- |
| 23,2 MB | 306x172@30 | 55,49 |
| 23,3 MB | 358x202@6 | 58,875 |

İkisinin arasında bir şey seçen hedef yok — merdivenin o basamağı 3,385 puan
yüksekliğinde. Üstel 0,25'ken 30 fps ile 6 fps yerleşimleri birbirinden uzaktı;
0,871'de puanları birbirine yaklaştı ve basamak oluştu. Taban sabitleri tek başına eski
değerine alınınca bu tarama **hiç** değişmiyor.

Geçit ölçülen en kötüye (3,5) çekildi, ama ölçü sabit güncellemesine indirgenmedi:
1,0 puanı aşan her istek için, dönen hedefin bir tarama adımı (%0,5) altındaki planın
**farklı bir yerleşim** olduğu ayrıca iddia ediliyor. Bu taramada 1,0'ı aşan dokuz
istek var ve dokuzu da böyle bir basamak:

```
0,3272 MB 130x230@40  -> 0,3288 MB 150x268@6     (phone.mp4, iki istek)
23,1449 MB 306x172@30 -> 23,2606 MB 358x202@6    (capture.mkv, beş istek)
27,1633 MB 306x172@30 -> 27,2991 MB 358x202@6    (capture.mkv, iki istek)
```

Yani aşım aramanın erken durmasından değil, merdivenin basamağından. "Eksik kalma
sıfır" iddiası olduğu gibi duruyor.

### 8.5.4 Denenip ölçüyle çürütülen daha sıkı iddia

Önce şu iddia yazıldı: "dönen hedefin %0,1 altındaki hedef isteği karşılayamamalı" —
yani arama gerçekten en küçük hedefi buluyor. Ölçü bunu **çürüttü**: tahmini kalite
hedef boyutta yüzde-altı ölçekte tekdüze değil.

`capture.mkv` Sharing, 204,3 MB civarı, hedefi 0,02 MB adımlarla:

| Hedef | Ses | Video | Yerleşim | Kalite |
| --- | --- | --- | --- | --- |
| 204,31 MB | 84k | 383k | 818x460@30 | 79,915 |
| 204,33 MB | 85k | 382k | 818x460@25 | 78,995 |
| 204,43 MB | 85k | 382k | 818x460@25 | 78,995 |
| 204,45 MB | 85k | 382k | 818x460@30 | 79,915 |

Hedef büyürken ses merdiveni 84k'dan 85k'ya çıkıyor, videodan bir kbit çalıyor ve
382k penceresinde 25 fps yerleşimi 30 fps'i geçiyor. Yaklaşık 0,12 MB (%0,06)
genişliğinde bir ada. Bu ada `PickAudio`'nun kademesinden geliyor, tabandan değil;
T99'un kapsamı dışında ve kendi işine bırakıldı. Aynı sebep %0,5 payda iki istek daha
düşürüyor (`sample.mp4`, istenen 62,5). İddia bu yüzden yazılmadı — yanlış olduğu
ölçüldü.

## 9. Notlar

**Sözleşmenin `verify` filtresi bu turun ölçülerinin tamamını kapsamıyor.** Filtre
`PlanCalculatorTests|ComplexityScanTests` (58 ölçü, hepsi yeşil). Tabanla ilgili üç ölçü
`ExtremeCompressionTests` içinde yaşıyor: `ACheaperFamilyClearsALayoutTheDearerFamiliesReject`,
`TheFloorFollowsTheContentAndTheFrameRate`,
`OneMegabyteTargetCollapsesThePixelRateAndNeverDropsUnderTheFloorSilently`. Mutasyon
kanıtı üçünü de kapsayan geniş filtreyle koşuldu (75 ölçü). Tabanı bozan bir değişiklik
yalnız `verify` koşulursa **görülmeyebilir**; denetim geniş filtreyi koşsun.

**Bir ad değişti, davranış değişmedi.** `ComplexityProfile.VmafNegMin` / `VmafNegMax`
(1,0 / 100,0) → `ReferenceVmafNegMin` / `ReferenceVmafNegMax`. Sabitler gerçekten bir
kararda kullanılıyor (`Math.Clamp`, `ComplexityProfile.cs:190` ve `:201`), oysa T104'ün
belgesi aynı adla "hiçbir karar bu alana bakmamalı" diyen başka bir alanı anlatıyordu.
Ad artık ne yaptığını söylüyor; hesap aynı.

**Plan ölçümünde kodlayıcı zorlandı.** `bench shrink --plan-only` bu makinede av1_nvenc
çalışır durumdayken `libx264` seçiyor; şikâyet işinin planını üretmek için
`--force-codec av1_nvenc` verildi. Bölüm 3 ve 3.1'deki plan çıktıları bu bayrakla alındı.
Seçimin neden böyle olduğu bu turda araştırılmadı, `_sorun.log`'a yazıldı.

**Süre sayısı yok.** Makine paylaşımlıydı (yedi ajan, dört ffmpeg birden), bu yüzden bu
belgede kodlama süresi rapor edilmiyor. Kalite ve boyut sayıları etkilenmez: her koşumda
iş parçacığı `-threads 4` ile sabitlendi, böylece kodlayıcı boştaki çekirdeğe göre farklı
bölümleme seçmedi.

Eleme koşulu artık `SearchLayout`'un içinde gömülü değil; `PlanCalculator.LayoutClearsFloor`
olarak dışarı alındı ve ölçüler üretim yolunun kendisini çağırıyor — koşulun kopyasını değil.
M6 bu yüzden beş ölçüyü birden düşürüyor.
