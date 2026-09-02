# Örnekleme — sabit pencere yerine içeriğe bağlı seçim (T103)

Ölçülen commit: `ef9e905` (dal `T103-icerik-ornekleme`, `main` `b9a6544` üzerinden).
ffmpeg 9.0 (gyan.dev), Windows 11, 2026-09-02. **Makine paylaşımlıydı** — aynı anda
yedi ajanın ölçümü koşuyordu. Bu damga yalnız **süre** sayıları için geçerlidir;
sapma sayıları yükten bağımsızdır, aynı kodlamaların yeniden kullanılan
çıktılarından hesaplanır.

## Ne ölçüldü, neye karşı

Bir yoklamanın işi, dosyanın tamamını kodlamadan `bppf`'ini (kare başına piksel
başına bit) kestirmektir. İki ayrı hata var ve bunlar karıştırılmamalı:

- **Alan kayması**: 2 saniyelik bir pencerenin kodlaması, tüm dosyanın kodlamasına
  göre yapısal olarak pahalıdır — pencerenin kendi IDR'si vardır, lookahead ve
  mbtree geçmişi yoktur. Bu kayma zaten `WindowDomainFactor` ile modelleniyor ve
  bu sözleşmenin konusu değil.
- **Örnekleme hatası**: aynı alan içinde, *hangi* pencerelerin seçildiğinden gelen
  hata. T103'ün konusu budur.

İkisini ayırmak için referans, dosyanın tamamının kodlaması değil, **2 sn'lik
pencerelerin yoğun sayımıdır**: her tam saniyeden başlayan 2 sn'lik pencerenin
ayrı ayrı kodlanması ve ortalaması. Böylece aday planların sapması yalnız yerleşim
farkını gösterir.

Ölçüm izolasyonu: kodlamalar `-f null -` ile yapıldı, boyut `vstats` `f_size`
toplamından geldi; kapsayıcı ek yükü ölçünün dışında.

### Korpus

`.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv` (1920x1080 hevc 10-bit
HDR, 60 fps, 1036,166 sn, oyun görüntüsü) tonemap'lenerek 960x540@30 SDR'ye
indirildi, CRF 14 veryfast ile kesildi. Düzenek: `tools/ornekleme/korpus-kur.sh`,
`korpus-kur-2.sh`.

| klip | süre | ne olduğu |
| --- | --- | --- |
| `k1-inisli` | 240 sn | yoğun başlayıp sakinleşen |
| `k2-duz` | 240 sn | tekdüze |
| `k3-tepeli` | 240 sn | ortada tepe |
| `k4-bas` | 240 sn | yük başta |
| `k5-kisa` | 60 sn | kısa dosya |
| `k6-uzun` | 480 sn | uzun dosya |
| `s1-cuval` | 240 sn | %75 kolay + %25 zor, tek geçişte birleştirilmiş |
| `s2-donusumlu` | 240 sn | 20 sn'lik kolay/zor bloklar dönüşümlü |

Korpusun bir sınırı var ve rapora giriyor: bu klipler CRF 14 ile yeniden
kodlanmış 540p kaynaklardır. **`ScanPoints` yanlılığı bu kliplerde güven
bandının dışına düşüyor** (k1 0,185; k5 0,4197 — band 0,5-2,0), oysa gerçek
1080p60 kaynaklarda içeride kalıyor. Yanlılık yolu hakkındaki yargı (K6) bu
yüzden korpustan değil, gerçek kaynaklardan ölçüldü.

## Adaylar

Her aday, ffprobe'un paket boyutlarından çıkarılan **saniye başına kaynak bit
profili** üzerine kurulu. Bu profil çözme gerektirmez; maliyeti aşağıda ayrıca
ölçüldü.

- **bugün** — sabit pencere: `usable*(i+0,5)/n`, `n = süre < 12 ? 2 : 3`.
- **profil-*** — saniyeleri kaynak bitine göre sıralayıp eşit sayılı tabakalara
  böler, her tabakadan bir saniye seçer, tabaka büyüklüğüyle ağırlıklandırır.
  Seçim ölçütü üç varyantta denendi: tabaka **ortalaması**, tabaka **medyanı**,
  ve seçilen saniyeden başlayan **2 sn'lik pencerenin** ortalaması.
- **pencere-*** — tabakalama saniye üzerinde değil, doğrudan **2 sn'lik pencere**
  üzerinde. Sıralanan büyüklük, hedeflenen büyüklük ve ağırlık aynı şeydir.
- **sahne-*** — `SceneMap`'in kesimlerinden sahneler kurar, sahneleri bit oranına
  göre sıralar, süre ağırlıklı tabakalara böler, her tabakanın en uzun sahnesinin
  ortasından örnekler.
- **-oran** eki: her pencerenin `bppf`'i `tabaka ortalaması / pencere bitleri`
  ile çarpılır (tabaka içi oran düzeltmesi).
- **-genel** eki: klasik oran tahmincisi — `ortalama(bppf/pencere biti) × dosya
  ortalaması`.
- **eşaralık** — tabakalama yok, pencereler süreye eşit aralıklı.

Değerlendirme `tools/ornekleme/cozumle.py` ile yapıldı: kodlamalar diske
önbelleklendiği için her aday, yeniden kodlama olmadan aynı kodlamalar üzerinden
puanlanıyor. Bu yüzden aday karşılaştırması **yükten bağımsız ve tekrarlanabilir**;
aynı girdiyle aynı sayıyı verir.

## Seçilen kural

Aday seçimi **en düşük hücreye** göre yapılmadı. Pencere sayısı koşumda içeriğe
göre belirlendiği için bir ailenin tek bir N'de iyi olması yetmez; **her N'de**
kabul edilebilir olması gerekir. Bu yüzden ölçüt, ailenin N'ler arasındaki **en
kötü klip sapmasıdır**.

Kazanan: **pencere tabanlı tabakalama, oran düzeltmesiz**. Kural üç parçadan
oluşuyor ve üçü de ölçümden geliyor:

1. **Neye bakılır** — 2 sn'lik pencerenin kaynak bitleri. Tabakalama, hedefleme
   ve ağırlık aynı büyüklük üzerinde; saniye üzerinden tabakalayıp pencere
   kodlayan varyantlar geçiş saniyelerinde sapıyor.
2. **Kaç pencere** — `N = clamp(2 + 3·cv, 2, 8)`, `cv` = saniye başına kaynak bit
   profilinin değişim katsayısı. Eğim 3, ölçülen "%5 sapma için gereken en küçük
   N" tablosuna uyan en küçük tam sayı katsayıdır (aşağıda).
3. **Üst sınır** — 8. Ölçülen N eğrisinde en kötü sapmanın en aza indiği nokta;
   ötesinde iyileşme yok. Alt sınır 2. Ayrıca dosyanın kendisi sınırlıyor:
   pencereler örtüşmediği için en çok `floor(süre / 2)` pencere olabilir.

Oran düzeltmesi (`-oran`) saniye tabanlı tabakalamada işe yarıyor, pencere
tabanlısında **zarar veriyor** — tabaka ile pencere aynı büyüklük olduğunda
düzeltme fazlalık, kendi varyansını ekliyor. Klasik oran tahmincisi (`-genel`)
elendi: kaynak biti ile 2 sn'lik yeniden kodlamanın `bppf`'i orantılı değil,
sapma %200'ün üzerine çıkıyor.

## K1 — Bugünkü örneklemenin içerik başına sapması

Referans: aynı kliplerin 2 sn'lik pencerelerinin yoğun sayımı (60 sn'de 30,
240 sn'de 120, 480 sn'de 240 pencere). "düzeltmesiz" sütunu ham sabit pencere
kestirimi, "bugün" sütunu üretimde uygulanan yanlılık düzeltmesinden sonrası.

| klip | süre | cv | düzeltme | düzeltmesiz | bugün |
| --- | --- | --- | --- | --- | --- |
| `k5-kisa` | 60 | 1,95 | yok | −19,51 % | −19,51 % |
| `k1-inisli` | 240 | 2,06 | yok | −42,96 % | −42,96 % |
| `k3-tepeli` | 240 | 0,90 | scan | −34,44 % | −9,97 % |
| `s1-cuval` | 240 | 0,34 | scan | −20,50 % | +12,54 % |
| `s2-donusumlu` | 240 | 0,35 | scan | +2,36 % | −1,92 % |
| `k2-duz` | 240 | 0,27 | scan | −4,03 % | −13,07 % |
| `k4-bas` | 240 | 1,00 | scan | +18,98 % | +20,32 % |
| `k6-uzun` | 480 | 1,08 | scan | −3,18 % | −5,71 % |

**Asıl sayı sapmanın büyüklüğü değil, içerikten içeriğe ne kadar değiştiğidir:**
−42,96 % ile +20,32 % arasında, **63,28 puanlık yayılım**. Ortalama mutlak sapma
%15,75. İşaret bile sabit değil — aynı sabit kural bir klipte hafife alıyor,
diğerinde abartıyor, bu yüzden tek bir sabit katsayıyla kapatılamaz.

`k5-kisa` ve `k1-inisli`'de yanlılık düzeltmesi hiç uygulanmadı: `ScanPoints`
yanlılığı güven bandının (0,5-2,0) dışına düştü ve paket yanlılığı da öyle.
Bu iki klip aynı zamanda en yüksek cv'li ikili.

## K2 — İki aday ile bugünkü yan yana

Ölçüt, ailenin N'ler arasındaki **en kötü klip sapması** (parantez içinde korpus
ortalaması). Tabloda her ailenin tüm N'lerdeki en kötüsü ve en iyisi:

| aile | N'ler arası en kötü | en iyi N'de |
| --- | --- | --- |
| **pencere** (profil tabanlı, seçilen) | **0,144** | **0,022** |
| pencere-oran | 0,195 | 0,038 |
| profil-window | 0,227 | 0,029 |
| profil-median | 0,261 | 0,074 |
| profil-mean-oran | 0,291 | 0,070 |
| **sahne** (`SceneMap` tabanlı) | **0,395** | **0,114** |
| profil-mean | 0,542 | 0,071 |
| eşaralık | 0,631 | 0,156 |
| sahne-oran | 0,850 | 0,230 |
| profil-median-oran | 1,133 | 0,034 |
| *-genel (klasik oran tahmincisi) | 2,845 - 3,721 | 0,530 - 2,709 |

**Bugünkü sabit pencerenin en kötü klip sapması 0,4296.** Profil tabanlı aday
onu üçte bire (0,144), seçilen N'de otuzda bire (0,022) indiriyor; sahne tabanlı
aday da bugünkünden iyi (0,395) ama profil tabanlının gerisinde.

Sahne tabanlısı neden geride: sahne sınırları içerik karmaşıklığını değil
kesmeyi izliyor. Uzun tek bir sahne içinde yük çok değişebiliyor, sahnenin
ortasından alınan tek pencere o değişimi görmüyor. `sahne-oran` daha da kötü —
sahne ortalaması ile pencere biti arasındaki oran uzun sahnelerde 1'den çok
uzaklaşıyor ve düzeltme kendi hatasını büyütüyor.

## K3 — Pencere sayısı neye bağlı

Seçilen ailenin N eğrisi (korpusun en kötü klip sapması):

| N | 2 | 3 | 4 | 5 | 6 | 8 | 10 | 12 | 16 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| en kötü | 0,090 | 0,110 | 0,144 | 0,079 | 0,044 | **0,022** | 0,045 | 0,048 | 0,043 |
| ortalama | 0,033 | 0,052 | 0,058 | 0,033 | 0,024 | **0,012** | 0,025 | 0,022 | 0,020 |

En kötü sapma N=8'de en aza iniyor; ötesinde iyileşmiyor. **Üst sınır 8 buradan
geliyor** — konulan bir tavan değil, ölçülen bir diz.

Klip başına, %5 sapmanın altında kalmak için gereken en küçük N (o N'den sonraki
tüm N'lerde de altında kalmak şartıyla):

| klip | cv | gereken N | kuralın verdiği N |
| --- | --- | --- | --- |
| `k2-duz` | 0,27 | 3 | 3 |
| `s1-cuval` | 0,34 | 3 | 4 |
| `s2-donusumlu` | 0,35 | 2 | 4 |
| `k3-tepeli` | 0,90 | 5 | 5 |
| `k4-bas` | 1,00 | 4 | 5 |
| `k6-uzun` | 1,08 | 2 | 6 |
| `k5-kisa` | 1,95 | 6 | 8 |
| `k1-inisli` | 2,06 | 6 | 8 |

`N = ceil(2 + 3·cv)` hiçbir klipte gerekenin altına düşmüyor; bağlayıcı nokta
`k3-tepeli` (cv 0,90, gereken 5, kural 5). Eğim 3, bu şartı sağlayan en küçük
tam sayıdır — daha küçük bir eğim `k3-tepeli`yi açıkta bırakıyor.

Hedef %5'in kendisi de ölçüldü, seçilmedi: %3'e inildiğinde gereken N'ler
tutarsızlaşıyor (`s2-donusumlu` cv 0,35 ile N=16 istiyor, `k1-inisli` hiçbir
N'de tutturamıyor). %3, bu yöntemin bu korpustaki çözünürlüğünün altında.

**Bağımlılık:** pencere sayısı süreye değil, saniye başına kaynak bit
profilinin değişim katsayısına bağlı. Süre yalnız üst sınırı kısıtlıyor
(pencereler örtüşemeyeceği için `floor(süre/2)`).

Seçilen kuralla, N=8'de klip başına sapma: `k5-kisa` −2,15 %, `k1-inisli`
+2,06 %, `k3-tepeli` −1,45 %, `s1-cuval` +0,74 %, `s2-donusumlu` +1,41 %,
`k2-duz` −1,03 %, `k4-bas` −0,08 %, `k6-uzun` −0,40 %. **Yayılım 63,28
puandan 4,21 puana iniyor.**

`tools/ornekleme/cozumle.py` her koşumda Python'daki aday planı ile C#'ın
`ComplexityProbe.PlanWindows` çıktısını karşılaştırıyor; bu sayfadaki sayılar
üretimin gerçekten kurduğu planlara ait.

## K4 — Maliyet

`tools/ornekleme` `maliyet` komutu, üç tekrarın **medyanı**, `-threads 4` sabit.
**Makine paylaşımlıydı; süre sayıları bu damgayı taşır.** Yükten bağımsız vekil
olarak kodlama çağrısı sayısı ve kodlanan saniye de veriliyor.

| klip | süre | paket okuma | sahne taraması | `ScanPoints` | bugünkü pencereler | yeni pencereler |
| --- | --- | --- | --- | --- | --- | --- |
| `k5-kisa` | 60 sn | 46 ms | 537 ms | 907 ms | 564 ms (3) | 1504 ms (8) |
| `k3-tepeli` | 240 sn | 95 ms | 2583 ms | 1662 ms | 969 ms (3) | 1843 ms (5) |
| `k6-uzun` | 480 sn | 163 ms | 4828 ms | 1583 ms | 1805 ms (3) | 3655 ms (6) |

Yükten bağımsız vekil:

| | bugün | yeni |
| --- | --- | --- |
| kodlama çağrısı | 43 (3 pencere + 40 `ScanPoints`) | 5 - 8 |
| kodlanan saniye | 46 sn (3×2 + 40×1) | 10 - 16 sn |

Toplam (K6'nın kararıyla `ScanPoints` düşünce, yerine paket okuması gelince):

| klip | bugün | yeni | fark |
| --- | --- | --- | --- |
| `k5-kisa` | 1471 ms | 1550 ms | +5 % |
| `k3-tepeli` | 2631 ms | 1938 ms | −26 % |
| `k6-uzun` | 3388 ms | 3818 ms | +13 % |

Kodlanan saniye %65-78 azalıyor ama duvar saati aynı oranda düşmüyor: `ScanPoints`
pencereleri 1 sn ve 480x270, örnekleme pencereleri 2 sn ve tam çözünürlük. Kısa
ve uzun dosyada yeni yol biraz pahalı, orta boyda ucuz.

Paket okuması bu maliyetin en küçük parçası ve çözme gerektirmiyor. En kötü hâli
ayrıca ölçüldü: 1036,166 sn / 1,70 GB 1080p60 HDR kaynakta ffprobe paket geçişi
**2,96 / 3,08 / 3,20 sn** (62.170 paket).

**Harita ile yoklama tek geçişte birleşsin mi:** hayır, gerekmiyor. T96'nın sahne
haritası koşum süresinin %10,4'ünü alıyor ve burada da en pahalı tek parça
(4,8 sn'ye kadar). Ama seçilen örnekleme profil tabanlı — sahne haritasını hiç
kullanmıyor. Birleştirilecek iki geçiş yok. T101'in K6 yargısı (haritanın
kodlamanın ilk geçişiyle birleştirilmesi, maliyetin çözmede olması) yerinde
duruyor ve burada tekrarlanmadı; T103 haritayı örnekleme yolundan çıkardığı için
o yargının kapsamı değişmiyor.

## K6 — `ScanPoints` düzeltmesi hâlâ gerekli mi

Bugünkü yol, sabit pencerelerin kestirimini `ScanPoints` yanlılığıyla bölerek
düzeltiyor (`ComplexityProfile.WindowBiasMin/Max` = 0,5-2,0 güven bandı, band
dışında paket aralığı yedeği). Yeni planda aynı düzeltme uygulanırsa ne oluyor:

| klip | cv | N | `ScanBias` | düzeltmesiz | scan uygulanırsa |
| --- | --- | --- | --- | --- | --- |
| `k5-kisa` | 1,95 | 8 | 0,4197 | −2,15 % | bandda değil |
| `k1-inisli` | 2,06 | 8 | 0,1851 | +2,06 % | bandda değil |
| `k3-tepeli` | 0,90 | 5 | 0,7282 | +1,98 % | +40,05 % |
| `s1-cuval` | 0,34 | 4 | 0,7064 | +4,34 % | +47,70 % |
| `s2-donusumlu` | 0,35 | 4 | 1,0436 | +3,45 % | −0,88 % |
| `k2-duz` | 0,27 | 3 | 1,1040 | +0,56 % | −8,91 % |
| `k4-bas` | 1,00 | 5 | 0,9889 | −1,02 % | +0,09 % |
| `k6-uzun` | 1,08 | 6 | 1,0269 | +1,24 % | −1,41 % |
| **ortalama mutlak** | | | | **2,10 %** | **12,91 %** |

**Yargı: yeni örnekleme yolunda `ScanPoints` düzeltmesi gerekli değil, zararlı.**
Düzeltmenin kapattığı şey sabit pencerelerin *örnekleme* yanlılığıydı; örnekleme
tabakalı ve ağırlıklı olunca o yanlılık kaynağında yok oluyor, düzeltme üstüne
binince kendi hatasını ekliyor. Ortalama mutlak sapmayı 6 katına çıkarıyor.

**Bu yargının sınırı:** yukarıdaki tablo 540p CRF-14 korpus kliplerine ait ve bu
kliplerde `ScanBias` sekizde ikisinde güven bandının dışına düşüyor — yani korpus
yanlılık yolu için temsilci değil. Gerçek 1080p60 HDR kaynaklarda yanlılık
bandın içinde kalıyor (ölçüldü: `parca-1` 1,2230 · `parca-3` 0,9957 ·
17 dk kaynak 0,7362; `parca-2` 0,3863 ile band dışı, paket yedeği 1,0057
devreye giriyor).

## K7 — Geriye dönük kırılma

`src/VidShrink.Core/ComplexityProfile.cs` bu dalda **hiç değişmedi**
(`git diff main...HEAD -- src/VidShrink.Core/ComplexityProfile.cs` boş).
`ComplexityProfile.FromMeasurements` imzası, alan sırası ve `WindowBias`
güven bandı aynı. Yoklama tarafında eklenenlerin hepsi yeni üye:
`SampleWindow`, `SamplingPlan`, `PlanWindows`, `PlanWindowCount`,
`Heterogeneity`, `WindowBits`, `WeightedBppf`, `PlanBias`, `SecondBitProfile`.
Var olan `Windows`, `ScanPoints`, `ScanBiasAsync`, `PacketBiasAsync`,
`RunDetailedAsync` imzaları korundu; `ComplexityScanTests` (42 test) dokunulmadan
yeşil.

### T99'un iki yolu — ölçüldü

`ComplexityProfile.cs:257-260` iki yol açıyor:

```
var motionMeasured = fullScaleBppf > 0 && halfFpsBppf > 0;
var motion = motionMeasured
    ? Math.Clamp(Math.Log2(halfFpsBppf / fullScaleBppf), MotionExponentMin, MotionExponentMax)
    : DefaultMotionExponent;
```

Ölçülen yolda `:259`'daki kırpma bağlar, ölçülemeyende `:260`'taki varsayılan
devreye girer. Örnekleme değişikliği bu ayrımı **kaydırmıyor**: hareket örneği
(`MotionSampleAsync`) planın orta penceresinden alınıyor ve plan her zaman en az
iki pencere içerdiği için orta pencere her zaman var. `fullScaleBppf` ve
`halfFpsBppf`'in ikisi de pozitif çıktığı sürece yol değişmiyor; ikisini de
üreten kodlama sayısı 3'ten 2-8'e çıkıyor, sıfıra düşmüyor. Korpustaki sekiz
klibin ve üç gerçek kaynağın hepsinde `Measured` yolu seçildi — **ölçülemeyen
yola düşen içerik gözlenmedi**.

## Gerçek kaynaklar — yöntemin dayandığı varsayım burada kırılıyor

Yukarıdaki K1-K4 tabloları `.calisma/t103/korpus` altındaki sekiz **sentetik 540p**
klibe ait. Aynı sürüm üç **gerçek 1080p60 HDR** kaynakta (`parca-1..3`, 60 sn'lik
kesitler) yeniden ölçüldü. Sonuç korpustakinin tersi:

| klip | cv | bugün (sabit pencere) | üretim planı (N koşumda seçildi) |
|---|---|---|---|
| parca-1 | 0.16 | −14.86 % | **+81.00 %** (N=3) |
| parca-2 | 0.24 | −20.12 % | **+21.40 %** (N=3) |
| parca-3 | 0.12 | +5.66 % | +1.30 % (N=3) |

Üçte ikisinde yeni plan bugünkünden **belirgin biçimde kötü**. Ortalama mutlak
sapma bugün 13.55 %, üretim planında 34.57 %.

### Neden — ölçülen kök neden

Plan, pencereleri **ffprobe paket biti** profiline göre tabakalıyor. Bunun
işe yaraması için paket bitinin kodlama maliyetini (CRF-23 bppf) öngörmesi
gerekiyor. Bu varsayım ölçüldü: her klipte tüm tamsayı başlangıçlı 2 sn
pencerelerin paket biti ile önbellekteki gerçek kodlanmış bppf'i arasındaki
Pearson korelasyonu:

| korpus (sentetik 540p) | r | | gerçek 1080p60 HDR | r |
|---|---|---|---|---|
| k5-kisa | +0.972 | | parca-1 | **+0.546** |
| k1-inisli | +0.990 | | parca-2 | **+0.455** |
| k3-tepeli | +0.987 | | parca-3 | +0.927 |
| s1-cuval | +0.971 | | | |
| s2-donusumlu | +0.967 | | | |
| k2-duz | +0.976 | | | |
| k4-bas | +0.992 | | | |
| k6-uzun | +0.985 | | | |

Korpusta r = 0.967-0.992; gerçek kaynaklarda 0.455-0.927. Korpus klipleri
x264 ile üretildiği için paket profili kodlama maliyetiyle neredeyse birebir
gidiyor — yani korpus, yöntemin dayandığı varsayımı **sınamıyor, sağlıyor**.

Mekanizma parca-1'de doğrudan görünüyor: üretim planı `[6, 10, 13]`
pencerelerini seçiyor ve **paket alanında neredeyse yansız** —
`PlanBias = 0.9839`. Ama kodlama alanında üç pencerenin üçü de dosya
ortalamasının üstünde (0.1178 / 0.0956 / 0.0734, sayım 0.0528). Yanlılık
paket alanında yok, kodlama alanında +81 %. Korelasyonun düştüğü iki klip
(r = 0.546, 0.455) planın battığı iki klip; korelasyonun durduğu klip
(r = 0.927) planın kazandığı klip.

Bu, korpusun "temsilci değil" olmasından daha ağır bir sorun: korpus
yöntemin **öncülünü** doğru kabul ettiriyor.

### Aile sıralaması korpuslar arasında durmuyor

Gerçek kaynaklarda ailelerin N'ler arası en kötü sapması, korpustaki
sıralamayı korumuyor. Seçilen `pencere` ailesi korpusta en dayanıklıydı
(0.144); gerçek kaynaklarda en kötüler arasında (0.810). Korpusta ölçümle
elenen `esaralik-genel` ailesi (2.845) gerçek kaynaklarda en iyisi (0.156).
Yani K2'nin aday seçimi korpusa aşırı uydurulmuş.

Üç klip "en kötü klip" ölçüsü için az; bu sıralamayı ters yönde de
kanıt saymıyorum. Kanıt saydığım tek şey şu: **korpusta yapılan seçim
gerçek kaynaklara taşınmıyor.**
