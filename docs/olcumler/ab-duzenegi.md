# A/B ölçüm düzeneği — eş boyutta HandBrake karşılaştırması

Tarih: 2026-09-02. Araç: `tools/VidShrink.Ab`. Sözleşme: T95.

Bu belgenin konusu önce **alet**, sonra sayı. `docs/olcumler/handbrake-acigi.md`
içindeki GEÇERSİZ tablo, etiketsiz kaynağı bt709 etiketli çıktıyla karşılaştırdığı
için hedef boyut on kat değişirken XPSNR'ı 14,86 / 14,78 / 14,67'de sabit
bırakmıştı. Duyarsız ölçü sayı basmaya devam eder ve yanlışlığını kendi söylemez.
Buradaki düzenek o hatayı tekrar edemesin diye kapılıdır.

## Düzenek

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` — 1920x1080, hevc,
yuv420p10le, bt2020 / smpte2084 / bt2020nc, full range, 60 fps, 1036,17 sn,
1.729.085.563 bayt.

Araç `ab kos` ile koşar; çıktısı hem makine okunur JSON hem insan okunur tablodur.
Ham ffmpeg ve HandBrake günlükleri `.calisma/ab/gunluk/` altında kalır.

### Renk doğruluğu kapısı

Her çıktının `color_space` / `color_transfer` / `color_primaries` / `pix_fmt`
değeri ffprobe ile okunur ve referansınkiyle karşılaştırılır. Karar üç yoldan biridir:

| durum | karar | etiket |
|---|---|---|
| iki taraf da aynı HDR uzayı | doğrudan ölç | `aynı renk uzayında doğrudan karşılaştırma` |
| iki taraf da aynı SDR uzayı | doğrudan ölç | `aynı renk uzayında doğrudan karşılaştırma` |
| referans HDR, çıktı bt709 SDR | referansı aynı dönüşümden geçir, öyle ölç | `SDR uzayında karşılaştırma — HDR kaybı hariç` |
| taraflardan biri etiketsiz | **reddet** | sayı basılmaz |
| iki HDR uzayı ayrı (ör. PQ ↔ HLG) | **reddet** | sayı basılmaz |
| referans SDR, çıktı HDR | **reddet** | sayı basılmaz |

Etiketsizi reddetmek bu kapının asıl işidir: GEÇERSİZ tablo tam olarak etiketsiz
tarafa varsayım uydurulduğu için sabit sayı basmıştı.

Kapı canlı olarak iki uçta denendi. Etiketsiz taraf gerçekten etiketsiz üretildi:
`-c copy` renk etiketini HEVC bitstream VUI'sinden söküp atamıyor, bu yüzden
etiketleri düşürmek için `setparams=color_primaries=unknown:color_trc=unknown:colorspace=unknown`
ile yeniden kodlamak gerekti.

```
> ab denetle parca-1.mkv etiketsiz-2sn.mp4
referans : parca-1.mkv       | bt2020/smpte2084/bt2020nc/yuv420p10le | hdr=True | 60 fps
aday     : etiketsiz-2sn.mp4 | etiketsiz/etiketsiz/etiketsiz/yuv420p | hdr=False | 60 fps
kapı     : Rejected - çıktının renk etiketleri eksik; etiketsiz çıktı etiketli
           referansla karşılaştırılmaz.
sonuç    : SAYI BASILMADI - renk kapısı reddetti.              (çıkış kodu 2)
```

```
> ab denetle parca-1.mkv sdr-2sn.mp4
referans : parca-1.mkv | bt2020/smpte2084/bt2020nc/yuv420p10le | hdr=True | 60 fps
aday     : sdr-2sn.mp4 | bt709/bt709/bt709/yuv420p             | hdr=False | 60 fps
kapı     : ReferenceTransformed - referans aynı dönüşümden geçirilip öyle ölçüldü
etiket   : SDR uzayında karşılaştırma - HDR kaybı hariç
sonuç    : harm=1.17 p10=0.00 min=0.00 ort=1.42 XPSNR=7.86 SSIM=0.70  (çıkış kodu 0)
```

İkinci koşumdaki puanların düşüklüğü adayın 320x180 crf 30 ile üretilmiş bir kapı
denemesi olmasındandır; buradaki bulgu sayı değil, kapının hangi yolu seçtiği ve
etiketi basmasıdır.

Renk kapısının yanında bir **kare hızı kapısı** vardır. Kare hızları ayrıysa
libvmaf kareleri yanlış eşler ve sessizce bir sayı üretir; araç bu durumda da
sayı basmaz. Bu yüzden her iki yarışmacı da kaynağın kare hızına sabitlenir
(HandBrake `-r 60 --cfr`, VidShrink `AllowFpsDrop = false`).

### Eş boyut

Yarışmacılar aynı hedef boyutu alır. Karşılaştırma tabanı listedeki ilk
yarışmacının gerçek bayt sayısıdır; diğerleri ona göre ±%2 içinde sayılır.
Dışarı taşan satıra `eş boyut değil` damgası basılır ve gerçek bayt sayıları
tabloda durur. Sessiz karşılaştırma yok.

### Ses

HandBrake tarafı `-a none` ile koşuyor. Bütçenin iki tarafta da tümüyle videoya
gitmesi için düzenek **video-only** girdiyle çalışır: sesli her girdiden
`-map 0:v:0 -c copy` ile sessiz bir kopya türetilir, kodlama onun üzerinde yapılır.
Girdi bu adımdan sonra hâlâ ses taşıyorsa araç kodlamaya başlamadan durur.

### Parçalar

17 dakikayı her yapılandırma için baştan sona kodlamak saatler sürdüğünden araç
bir `--parca` kipi taşır. Parçalar `-c copy` ile kesilir, dolayısıyla kesim
istenen saniyeye değil ondan önceki en yakın anahtar kareye oturur. Üç parçanın
kaynaktaki gerçek başlangıcı kare karma (`framehash`) eşlemesiyle doğrulandı:

| parça | istenen | doğrulanan başlangıç |
|---|---|---|
| `parca-1.mkv` | 00:02:00 | 00:01:59,6 |
| `parca-2.mkv` | 00:07:30 | 00:07:29,6 |
| `parca-3.mkv` | 00:13:00 | 00:12:59,6 |

Üçü de hedeflenen saniyeden 0,4 sn önceki anahtar kareye oturuyor; `-c copy`
kesiminin beklenen davranışı budur. `parca-2` ve `parca-3` bu sözleşme
başlamadan önce paralel çalışan başka bir ajan tarafından kesilmişti; sözleşme
"varsa yeniden kesme" dediği için yeniden kesilmediler, yalnız başlangıçları
doğrulandı. İkisi ses taşıdığı için ölçüme video-only türevleriyle girdiler.

Parça kipinde hedef boyut parçanın süresiyle oranlanır: tam kaynak için istenen
N MB, 60,4 saniyelik bir parçada N × 60,4 / 1036,17 MB olur.

### Ölçü

VMAF-NEG dörtlüsü (ortalama, harmonik ortalama, p10, kare minimumu) ile XPSNR ve
SSIM `src/VidShrink.Ffmpeg/QualityMeter.cs`ten gelir; A/B aracı bu hesabı
kopyalamaz, kütüphane olarak çağırır.

Parçalar birleştirilirken:

- **harmonik ortalama** kare sayısıyla ağırlıklı harmonik ortalamadır. Parça
  başına kare sayıları eşit olduğunda bu, bütün karelerin harmonik ortalamasına
  matematiksel olarak eşittir.
- **kare minimumu** parça minimumlarının en küçüğüdür — bu da kesin.
- **p10** parçalar arasında kesin olarak birleştirilemez, çünkü `QualityMeter`
  kare başına puanları dışarı vermiyor. Bu yüzden özet satırındaki sütun
  **en kötü parçanın p10'u**dur ve tabloda öyle adlandırılmıştır. Parça başına
  p10'lar ayrı satırlarda durur.

Rapora tek bir ortalama sayı başlık yapılmaz: harmonik ortalama, p10 ve kare
minimumu birlikte durur. Kullanıcı en kötü sahnede rahatsız olur, ortalamada değil.

### Duyarlılık

Aynı kodlayıcı ve aynı kaynak iki farklı hedef boyutta koşturulur. Büyük hedefin
puanı küçük hedefinkinden en az 1,00 VMAF-NEG puanı yüksek çıkmazsa araç o satırı
`AYRIŞMIYOR` diye işaretler; düzenek duyarsız sayılır.

Ölçülen ayrışma, 60 MB'den 600 MB'ye:

| yarışmacı | 60 MB harm | 600 MB harm | ayrışma | eşik |
|---|---|---|---|---|
| handbrake | 28,70 | 67,96 | **+39,26** | 1,00 |
| vidshrink | 18,98 | 58,83 | **+39,85** | 1,00 |

Eşiğin kırk katı. GEÇERSİZ tablodaki 14,86 → 14,67 (yani -0,19) ile arasındaki fark
düzeneğin varlık sebebi.

## Düzeneğin reddettikleri

Bir ölçüm düzeneğinin neyi **reddettiği**, ne ölçtüğü kadar önemlidir. Bu araç
şu durumlarda sayı basmaz, hata verir:

| reddediş | gerekçe |
|---|---|
| taraflardan biri etiketsiz | etiketsize varsayım uydurmak GEÇERSİZ tabloyu üreten hatanın ta kendisi |
| iki HDR uzayı ayrı | PQ ile HLG doğrudan puanlanamaz |
| referans SDR, çıktı HDR | referans yükseltilemez |
| kare hızları ayrı | libvmaf kareleri yanlış eşler, sessizce sayı üretir |
| en boy oranları ayrı | kırpma ya da dolgu var; puan bit hızından bağımsız sabitlenir |
| girdi ses taşıyor | HandBrake `-a none` koşarken bütçe iki tarafta eşit bölünmez |

Son satır bu sözleşme sırasında gerçekten yakalandı ve bir koşumu iptal ettirdi.
`parca-2` ve `parca-3` — bu sözleşmeden önce başka bir ajan tarafından kesilmiş
oldukları için — ses akışı taşıyordu; `parca-1` taşımıyordu. VidShrink tarafı iki
parçada bütçenin bir kısmını sese ayırıp üçüncüsünde ayırmayacak, HandBrake tarafı
ise hiçbirinde ayırmayacaktı. Ortaya çıkacak sayı hem HandBrake'e karşı haksız hem
de parçalar arasında kendi içinde tutarsız olurdu. Sayılar üretildikten sonra bunu
fark etmek zordur: tablo gayet makul görünür.

Düzenek artık sesli her girdiden `-map 0:v:0 -c copy` ile video-only bir kopya
türetiyor; türetmeden sonra girdi hâlâ ses taşıyorsa kodlamaya başlamadan duruyor.

En boy oranı satırı da bu sözleşme sırasında yakalandı, üstelik GEÇERSİZ tablonun
tıpatıp aynı imzasıyla. İlk parça koşumunda `parca-2` üzerinde HandBrake'in puanı
hedef boyut on kat büyürken kımıldamadı: 60 MB'de harmonik 20,27, 600 MB'de 20,30.
İlk okuyuşta bu "ölçü duyarsız" demektir. Değildi: preset'in otomatik kırpması o
parçada 8 satır kırpıp 1920x1072 üretiyordu, öbür iki parçada kırpmıyordu. Kırpılmış
kareyi 1920x1080 referansla eşleştirince puan bit hızından bağımsız bir tabana
oturuyor. Aynı koşumda VidShrink 1152x648'e inip 68,60'tan 95,84'e çıkıyordu —
yani ölçü duyarlıydı, hizalama bozuktu.

İki değişiklik yapıldı: HandBrake `--crop 0:0:0:0 --non-anamorphic` ile koşuyor, ve
en boy oranı %0,5'ten fazla ayrılan çift artık sayı basmıyor. Çözünürlük düşüşü
serbest kalıyor — 1152x648 ile 1920x1080 aynı oran, libvmaf ölçekleyip karşılaştırır;
yasak olan oranın değişmesi. Bu belgedeki bütün sayılar düzeltmeden sonraki koşumdan.

### Bu turda ölçülmeyen

Süre ve hız. Makine paylaşımlı — ölçüm boyunca aynı makinede başka ajanlar da
kodlama koşuyordu. Bu belgede hiçbir süre ya da hız iddiası yok.

## İlk gerçek koşum

Üç parça, iki hedef boyut, iki yarışmacı. Hedef boyutlar tam kaynak için 60 MB ve
600 MB; parça başına 3,497-3,499 MB ve 34,975-34,994 MB'a oranlandı. Bütün satırlar
`aynı renk uzayında doğrudan karşılaştırma` — kaynak da çıktılar da bt2020/PQ kaldı,
tonemap yoluna hiç girilmedi.

### 60 MB hedefinde

| girdi | yarışmacı | bayt | fark % | eş boyut | harm | p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|
| parca-1 | handbrake | 3.531.037 | 0,00 | evet | 47,71 | 34,32 | 3,02 | 52,16 | 30,93 |
| parca-1 | vidshrink | 3.420.687 | -3,13 | **eş boyut değil** | 31,50 | 16,93 | 1,25 | 38,77 | 28,91 |
| parca-2 | handbrake | 3.730.691 | 0,00 | evet | 93,70 | 93,07 | 72,27 | 93,71 | 43,59 |
| parca-2 | vidshrink | 3.409.438 | -8,61 | **eş boyut değil** | 68,60 | 66,52 | 49,48 | 68,71 | 33,07 |
| parca-3 | handbrake | 3.680.998 | 0,00 | evet | 13,72 | 11,79 | 0,00 | 17,46 | 28,21 |
| parca-3 | vidshrink | 3.564.225 | -3,17 | **eş boyut değil** | 8,95 | 6,64 | 0,00 | 13,02 | 27,06 |

### 600 MB hedefinde

| girdi | yarışmacı | bayt | fark % | eş boyut | harm | p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|
| parca-1 | handbrake | 35.288.140 | 0,00 | evet | 81,48 | 79,04 | 4,12 | 83,43 | 38,00 |
| parca-1 | vidshrink | 35.746.203 | +1,30 | evet | 70,82 | 67,21 | 3,49 | 72,56 | 36,07 |
| parca-2 | handbrake | 36.766.517 | 0,00 | evet | 95,78 | 95,32 | 71,96 | 95,78 | 47,11 |
| parca-2 | vidshrink | 36.137.359 | -1,71 | evet | 95,84 | 95,42 | 74,89 | 95,84 | 51,25 |
| parca-3 | handbrake | 36.272.258 | 0,00 | evet | 46,66 | 64,62 | 0,00 | 69,87 | 36,35 |
| parca-3 | vidshrink | 35.549.615 | -1,99 | evet | 37,83 | 54,59 | 0,00 | 60,10 | 35,00 |

### Özet

| yarışmacı | hedef MB | toplam bayt | eş boyut | harm | en kötü p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|
| handbrake | 60 | 10.942.726 | evet | **28,70** | 11,79 | 0,00 | 54,44 | 34,25 |
| vidshrink | 60 | 10.394.350 | **eş boyut değil** | **18,98** | 6,64 | 0,00 | 40,16 | 29,68 |
| handbrake | 600 | 108.326.915 | evet | **67,96** | 64,62 | 0,00 | 83,03 | 40,49 |
| vidshrink | 600 | 107.433.177 | evet | **58,83** | 54,59 | 0,00 | 76,17 | 40,77 |

**Geridiyiz.** Eş boyutta HandBrake her iki hedefte de önde: 60 MB'de harmonik
ortalamada 9,72 puan, 600 MB'de 9,13 puan. Altı parça-hedef çiftinin beşinde
HandBrake kazandı; VidShrink yalnız `parca-2` @ 600 MB'de önde (95,84'e 95,78 —
bu fark gürültü sayılır) ve orada XPSNR'ı belirgin yüksek (51,25'e 47,11).

İki gözlem, ikisi de sayıdan çıkıyor:

- **60 MB hedefinde VidShrink bütçeyi bitirmiyor.** Üç parçanın üçünde de hedefin
  altında kalıyor (-%3,13, -%8,61, -%3,17) ve üç satır da `eş boyut değil` damgalı.
  Yani bu üç satırda VidShrink hem daha az bit harcayıp hem daha düşük puan aldı;
  kullanılmayan bütçe doğrudan kayıp. 600 MB hedefinde bu kaybolmuyor ama küçülüyor
  (+%1,30 / -%1,71 / -%1,99, üçü de ±%2 içinde).
- **Kare minimumu tek başına ayırt etmiyor.** `parca-3`'te iki taraf da her hedefte
  0,00 kare minimumu veriyor; oradaki farkı ancak harmonik ortalama ve p10 gösteriyor.
  Tersi de var: `parca-1` @ 600 MB'de harmonik 81,48'ken kare minimumu 4,12. Tek
  sayıya indirgenmiş bir başlık bu tabloyu anlatamaz.

HandBrake'in tam komut satırı (parça 1, 60 MB hedefi; bit hızı dışında bütün
satırlarda aynı):

```
HandBrakeCLI -i parca-1.mkv -o parca-1_handbrake_3.497mb.mkv \
  -Z "H.265 MKV 1080p30" -e x265 -b 483 --multi-pass --turbo \
  --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
```

600 MB hedefinde tek fark `-b 4833`. Bit hızı hedeften türetiliyor:
`hedef_bayt x 8 x (1 - 0,005) / süre / 1000`, kapsayıcı payı %0,5.

VidShrink tarafı ürünün kendi borusunu koşuyor — `ComplexityProbe` →
`PlanCalculator` → iki tur `CalibrationProbe` → `EncodeRunner` — ve kendi kararını
kendi veriyor. 600 MB hedefinde seçtiği: `1190x670@60, libx264/2pass, 4712k,
pix=p010le, hdr=Preserve`. Yani çözünürlüğü düşürüp bit hızını kurtarma yolunu
seçiyor; HandBrake 1920x1080'de kalıyor. Bu turda o tercih kazandırmadı.

### Ölçünün kendi kararsızlığı

Aynı yapılandırma iki kez koştu (geometri düzeltmesinden önce ve sonra, `parca-1`
kırpmadan etkilenmediği için iki koşumda da aynı girdiyle). HandBrake bit bit aynı
çıktıyı verdi (3.531.037 bayt, harmonik 47,71 — sıfır fark). VidShrink'in ölçüm
temelli kalibrasyonu koşumdan koşuma az da olsa oynuyor: 3.417.959 → 3.420.687 bayt,
harmonik 31,40 → 31,50, p10 17,35 → 16,93. Yani bu düzenekte 0,1-0,5 VMAF-NEG'lik
farklar gürültüdür; yukarıdaki 9 puanlık açık değildir.

## `QualityMeter` eksikleri

A/B aracı `QualityMeter`ı kütüphane olarak çağırıyor, kopyalamıyor. Çağırırken
karşılaşılan eksikler — hepsi T97'nin girdisi:

| eksik | sonucu |
|---|---|
| kare başına puanlar dışarı verilmiyor (`Percentile` özel) | parçalar arası p10 kesin birleştirilemiyor; özet satırı "en kötü parça" olmak zorunda kaldı |
| kare sayısı döndürülmüyor | parça ağırlıkları süreden türetiliyor, kareden değil |
| libvmaf JSON günlüğü `%TEMP%`e yazılıp `finally` içinde siliniyor | ham ölçüm günlüğü `.calisma/ab/` altında saklanamıyor |
| `ColorFilter` etiketsiz girdiye varsayım uyduruyor (`?? (hdr ? "bt2020" : "bt709")`) | GEÇERSİZ tabloyu üreten hata `QualityMeter` içinde hâlâ duruyor; kapı A/B aracında, ölçerde değil |
| `ColorIncompatibility` yalnız HDR/SDR uyuşmazlığını reddediyor | etiketsiz taraf ölçere kadar gidebiliyor |
| kare hızı hiç bakılmıyor | kare hızları ayrıyken libvmaf sessizce sayı üretiyor; kapı yine A/B aracında |
| tonemap'li ölçümün pencere (başlangıç/süre) aşırı yüklemesi yok | parça ölçümü ayrı dosya kesmeyi gerektiriyor |
| XPSNR düzlem başına verilmiyor | kroma bozulması ayrı görülemiyor |

## Ölçüler

`AbTests` adıyla, `dotnet test -c Release --filter "AbTests"` — 49 test, 49 geçti,
0 kaldı, 0 atlandı. 36'sı bu sözleşmenin (`ColorGateAbTests` 9,
`ChunkAggregateAbTests` 8, `SensitivityAbTests` 6, `SizeParityAbTests` 5,
`AbSettingsAbTests` 4, `HandBrakeArgumentsAbTests` 3, `DeviationAbTests` 1);
kalan 13 `SettingsTabTests` süzgece adından ötürü takılıyor. Renk kapısı, eş
boyut toleransı, parça birleştirme (p10 dahil), duyarlılık eşiği ve HandBrake
bit hızı hesabı üretim davranışı üzerinden ölçülür: sabit karşılaştırma, `Skip`
ve sessiz erken dönüş yok.
