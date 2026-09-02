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
referans : parca-1.mkv | bt2020/smpte2084/bt2020nc/yuv420p10le | hdr=True | 1920x1080 | 60 fps
aday     : etiketsiz-2sn.mp4 | etiketsiz/etiketsiz/etiketsiz/yuv420p | hdr=False | 320x180 | 60 fps
kapı     : Rejected — Çıktının renk etiketleri eksik (color_primaries, color_transfer, color_space); etiketsiz çıktı etiketli referansla karşılaştırılmaz.
kare hızı: kare hızı eşit (60 fps)
geometri : en boy oranı uyuyor (1,7778 ~ 1,7778)
sonuç    : SAYI BASILMADI — renk kapısı reddetti.
```

Çıkış kodu 2.

```
> ab denetle parca-1.mkv sdr-2sn.mp4
referans : parca-1.mkv | bt2020/smpte2084/bt2020nc/yuv420p10le | hdr=True | 1920x1080 | 60 fps
aday     : sdr-2sn.mp4 | bt709/bt709/bt709/yuv420p | hdr=False | 320x180 | 60 fps
kapı     : ReferenceTransformed — Çıktı SDR bt709; referans aynı dönüşümden (zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p) geçirilip öyle ölçüldü.
kare hızı: kare hızı eşit (60 fps)
geometri : en boy oranı uyuyor (1,7778 ~ 1,7778)
etiket   : SDR uzayında karşılaştırma — HDR kaybı hariç
sonuç    : harm=1.17 p10=0.00 min=0.00 ort=1.42 XPSNR=7.86 SSIM=0.70
```

Çıkış kodu 0. İki blok da `ab denetle`nin çıktısının birebir kopyasıdır,
düzenlenmemiştir.

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

### Parça kestiriminin sapması

Bir yapılandırma hem parça kestirimiyle hem baştan sona tam koşumla ölçüldü:
VidShrink, 600 MB hedefi, aynı kaynak.

| yapılandırma | tam koşum harm | parça kestirimi harm | sapma |
|---|---|---|---|
| vidshrink @ 600 MB | 47,78 | 58,83 | **+11,05** |

Diğer sütunlar da aynı yöne kayıyor: ortalama 67,33 → 76,17 (+8,84), XPSNR
34,54 → 40,77 (+6,23). Kare minimumu iki tarafta da 0,00.

**Parça kestirimi iyimser.** Seçtiğimiz üç dakika, filmin ortalamasından kolay.
Sayı olarak: 60 saniyelik üç parçadan çıkan tahmin, 17 dakikanın tamamından
11 VMAF-NEG puan yüksek. Bu, iki kodlayıcı arasındaki 9,13 puanlık farktan büyük.

Sonuç, düzeneğin kullanım kuralı olarak: **parça kipi mutlak kalite iddiası için
kullanılamaz.** "VidShrink bu kaynakta 58,8 alıyor" cümlesi parça sayısından
kurulamaz. Bu belgedeki parça tabloları karşılaştırma tablosudur, mutlak kalite
tablosu değil.

Parça kipinin **karşılaştırma** için kullanılabilir olması ayrı bir iddia ve
**ölçülmedi.** Dayanağı şu varsayım: iyimserlik iki yarışmacıya da aynı biçimde
biniyor, yani aradaki fark parçalarda da tam koşumda da aynı kalıyor. Bu varsayım
sınanmadı, çünkü sınamak HandBrake'in de tam koşumunu gerektirirdi ve o
koşturulmadı. Sınanmamış olması hafif bir eksik değil: sapmanın kendisi (11,05)
karşılaştırdığımız farktan (9,13) **büyük**, yani varsayım yıkılırsa sıralama da
gidebilir. Elimizdeki tek yan kanıt, altı parça-hedef çiftinin altısında da
harmonik ortalama, p10 ve ortalamanın aynı kazananı vermesi — bu ölçütten
bağımsızlığı gösterir, örneklemeden bağımsızlığı değil.

Tam koşum tek yapılandırmayla sınırlı kaldı; HandBrake tarafının tam koşumu ve
60 MB hedefinin tam koşumu **ölçülmedi**.

### Kare eşlemesi zaman damgasına takılıyor mu

`start_time` sıfır olmayan bir girdide libvmaf'ın kareleri komşusuyla eşleyip
eşlemediği ayrıca sınandı. Kaynağın ve `parca-1`in `start_time`ı 0,000000;
`parca-2` 0,020000, `parca-3` 0,017000 (ikisi de `-c copy` kesiminden geliyor).
Bütün kodlayıcı çıktıları 0,000000.

Sınama: aynı çift, bir kez ham referansla, bir kez zaman damgası sıfırlanmış
referansla ölçüldü.

| çift | ham referans | sıfırlanmış referans |
|---|---|---|
| parca-3 / handbrake 3,499 MB | 13,72 / 11,79 / 0,00 / 17,46 | 13,72 / 11,79 / 0,00 / 17,46 |
| parca-2 / handbrake 3,499 MB | 93,70 / 93,07 / 72,27 / 93,71 | 93,70 / 93,07 / 72,27 / 93,71 |

Tek basamağına kadar aynı; bu düzenekte kayma eşlemeyi bozmuyor. Yine de araç
artık `start_time`ı sıfır olmayan her girdiyi ölçümden önce
`-fflags +genpts -c copy -avoid_negative_ts make_zero` ile sıfıra çekiyor ve bunu
günlüğe yazıyor. Yukarıdaki tablolardaki sayılar sıfırlamadan önceki koşumdan —
sıfırlamanın sayıyı değiştirmediği ölçülerek gösterildiği için oldukları gibi
duruyorlar.

### Bu turda ölçülmeyen

Süre ve hız. Makine paylaşımlı — ölçüm boyunca aynı makinede başka ajanlar da
kodlama koşuyordu. Bu belgede hiçbir süre ya da hız iddiası yok.

HandBrake'in tam kaynak koşumu ve 60 MB hedefinin tam kaynak koşumu da ölçülmedi;
tam koşum yalnız VidShrink @ 600 MB için yapıldı. Tonemap yolu gerçek bir
yarışmacı çıktısıyla değil, kapı denemesiyle sınandı — `vidshrink-sdr` yarışmacısı
tanımlı ama bu turda koşturulmadı.

## İlk gerçek koşum

Üç parça, iki hedef boyut, iki yarışmacı. Hedef boyutlar tam kaynak için 60 MB ve
600 MB; parça başına 3,497-3,499 MB ve 34,975-34,994 MB'a oranlandı. Bütün satırlar
`aynı renk uzayında doğrudan karşılaştırma` — kaynak da çıktılar da bt2020/PQ kaldı,
tonemap yoluna hiç girilmedi.

### 60 MB hedefinde

İlk koşumda VidShrink üç parçanın üçünde de hedefin altında kalmış ve üç satır da
`eş boyut değil` damgası yemişti (-%3,13 / -%8,61 / -%3,17). O tablodan
karşılaştırma çıkarılamaz. Araca **eşitleme turu** eklendi: taban yarışmacının
gerçek baytına bakıp adayın hedefini oranla düzeltip yeniden kodluyor, en fazla
iki deneme. Aşağıdaki tablo eşitleme turlu koşumdan.

| girdi | yarışmacı | bayt | fark % | eş boyut | harm | p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|
| parca-1 | handbrake | 3.531.037 | 0,00 | evet | 47,71 | 34,32 | 3,02 | 52,16 | 30,93 |
| parca-1 | vidshrink | 3.519.437 | -0,33 | evet | 31,81 | 17,76 | 1,19 | 39,16 | 28,97 |
| parca-2 | handbrake | 3.730.691 | 0,00 | evet | 93,70 | 93,07 | 72,27 | 93,71 | 43,59 |
| parca-2 | vidshrink | 3.592.973 | -3,69 | **eş boyut değil** | 69,03 | 67,04 | 51,30 | 69,14 | 33,22 |
| parca-3 | handbrake | 3.680.998 | 0,00 | evet | 13,72 | 11,79 | 0,00 | 17,46 | 28,21 |
| parca-3 | vidshrink | 3.678.393 | -0,07 | evet | 9,33 | 7,01 | 0,00 | 13,59 | 27,18 |

Üç parçanın ikisi kapının içine girdi (-%0,33 ve -%0,07). `parca-2` giremedi;
nedeni aşağıda ayrı bir bulgu olarak ölçüldü. Toplam bayt 10.790.803 ↔ 10.942.726,
yani **-%1,39** — toplamda kapının içinde, ama satır bazında bir parça dışarıda
kaldığı için özet satırı damgalı kalıyor.

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
| vidshrink | 60 | 10.790.803 | 2/3 parça eş boyut, toplam -%1,39 | **19,59** | 7,01 | 0,00 | 40,62 | 29,79 |
| handbrake | 600 | 108.326.915 | evet | **67,96** | 64,62 | 0,00 | 83,03 | 40,49 |
| vidshrink | 600 | 107.433.177 | evet | **58,83** | 54,59 | 0,00 | 76,17 | 40,77 |

**Geridiyiz.** Eş boyutta ölçülmüş tek hedef 600 MB ve orada HandBrake harmonik
ortalamada **9,13 puan** önde (67,96 ↔ 58,83); altı satırın altısı da ±%2 içinde.

60 MB hedefi **tam eş boyutta ölçülemedi**, o yüzden oradan tek bir açık sayısı
verilmiyor. Eşitleme turundan sonra üç parçanın ikisi kapının içinde (-%0,33 ve
-%0,07), üçüncüsü -%3,69 ile dışarıda; toplam bayt -%1,39. Bu haliyle özet
harmonik ortalamalar 28,70 ↔ 19,59, yani **9,11 puan** — ama içinde eş boyut
olmayan bir satır taşıdığı için bu sayı 600 MB'deki 9,13 ile aynı sağlamlıkta
değil. Yönü de bilinir: dışarıda kalan satırda **VidShrink daha az bayt harcadı**,
dolayısıyla tam eşitlikte açığın bir miktar **daralması** beklenir, büyümesi değil.
Nitekim eşitleme turu öncesi aynı satırların açığı 9,72'ydi ve eşitlemeye
yaklaştıkça 9,11'e indi.

Altı parça-hedef çiftinin beşinde HandBrake kazandı; VidShrink yalnız
`parca-2` @ 600 MB'de önde (95,84'e 95,78 — bu fark gürültü sayılır) ve orada
XPSNR'ı belirgin yüksek (51,25'e 47,11).

Bir gözlem daha, o da sayıdan çıkıyor:

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

### Ayrı bulgu — VidShrink istenen boyutu bitirmiyor

Bu açığın parçası değil, ayrı bir bulgu. Ölçüldü çünkü eş boyutu tutturmayı
zorlaştıran şey buydu.

VidShrink'e istenen boyut ile teslim ettiği boyut arasında sistematik bir açık
var. Eşitleme turu öncesi, ilk denemede teslim edilenler:

| girdi | istenen | teslim | fark |
|---|---|---|---|
| parca-1 | 3,497 MB | 3,261 MB | **-%6,75** |
| parca-2 | 3,497 MB | 3,244 MB | **-%7,23** |
| parca-3 | 3,498 MB | 3,399 MB | **-%2,83** |

Kodlayıcı günlüğü açığın iki katmandan geldiğini gösteriyor.

**Birinci katman — planın kendine ayırdığı pay.** Plan, istenen boyutu doğrudan
hedeflemiyor; altında bir iç hedef kuruyor:

| istenen | iç hedef | pay |
|---|---|---|
| 3,497 MB | 3,358 MB | -%3,97 |
| 3,612 MB | 3,467 MB | -%4,01 |
| 34,975 MB | 34,100 MB | -%2,50 |
| 34,994 MB | 34,119 MB | -%2,50 |

Küçük hedeflerde pay ~%4, büyük hedeflerde ~%2,5.

**İkinci katman — kalibrasyonun kabul bandı.** Kodlayıcı iç hedefin de altına
düşüyor ve kalibrasyon bunu `in band` sayıp yeni tur açmıyor:

| iç hedef | gerçek | fark | kalibrasyon kararı |
|---|---|---|---|
| 3,358 MB | 3,244 MB | -%3,39 | `in band` |
| 3,358 MB | 3,261 MB | -%2,89 | `in band` |
| 3,467 MB | 3,356 MB | -%3,20 | `in band` |
| 34,119 MB | 33,903 MB | -%0,63 | `in band` |
| 34,100 MB | 33,055 MB | -%3,06 | `under band` → yeni tur |
| 3,683 MB | 3,496 MB | -%5,08 | `under band` → yeni tur |

Bandın kuralını çıkaramadım: -%3,39 kabul edilirken -%3,06 reddediliyor, yani
sabit bir yüzde değil. `PlanCalculator` benim `owns`ımda olmadığı için içine
bakmadım; ölçülen davranış bu.

**Üçüncü gözlem — çözünürlük basamağı eşitlemeyi kırıyor.** `parca-2`de eşitleme
turu ±%2'ye giremedi ve nedeni ölçülebilir: hedefi büyütmek plana çözünürlük
atlatıyor.

| istenen | seçilen çözünürlük | teslim | tabana fark |
|---|---|---|---|
| 3,497 MB | 1152x648 | 3.401.123 | -%8,83 |
| 3,836 MB | **1190x670** | 3.868.475 | +%3,69 |
| 3,700 MB | 1152x648 | 3.592.973 | -%3,69 |

Hedefi %3,7 büyütmek (3,700 → 3,836) çözünürlüğü bir basamak yukarı atıyor ve
teslim edilen bayt %7,7 sıçrıyor. Taban (3.730.691 bayt) tam bu sıçramanın
ortasına düşüyor; iki komşu basamak da ±%2'nin dışında kalıyor. Yani bu parçada
eş boyut, hedefi ayarlayarak **erişilebilir değil** — ulaşılabilir bayt değerleri
ayrık.

Bu üç maddenin üçü de `src/VidShrink.Core` tarafında ve bu sözleşmenin dışında.
Buraya ölçüm olarak yazıldı; düzeltmesi ayrı bir sözleşmenin işi.

### Harmonik ortalamanın tabanı — manşetin dayandığı sayı

Bu belgedeki bütün manşet sayıları harmonik ortalamadır ve harmonik ortalama
`src/VidShrink.Ffmpeg/QualityMeter.cs:147`'de şöyle hesaplanıyor:

```csharp
var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));
```

`Math.Max(x, 1.0)` bir kıskaçtır: **taban 1,0 VMAF-NEG.** Bunun neden önemli
olduğu özet tablodan okunuyor — dört satırın **dördünde de** kare minimumu 0,00.
Yani her dört ölçümde de en az bir kare gerçek sıfır aldı. Kıskaç olmasaydı
`1/0` sonsuza gider ve dört harmonik ortalamanın dördü de **0,00** olurdu.
28,70 da, 67,96 da, 58,83 da, 18,98 da kıskacın varlığına borçlu.

Kıskacın kuyruğu ne kadar belirlediğinin göstergesi `parca-3 @ 600 MB /
handbrake` satırı: harmonik **46,66**, p10 ise **64,62**. Harmonik ortalamanın
p10'un altına düşmesi, dağılımın alt ucunun tamamen taban tarafından
tutulduğu anlamına gelir.

**Kaç karenin kıskaca değdiği ölçülemedi.** Sebebi: libvmaf'ın kare kare JSON
günlüğü `QualityMeter.cs:124`'te `%TEMP%`'e yazılıyor ve `157`'deki `finally`
bloğunda siliniyor; `QualityScore` yalnız dört toplu sayı döndürüyor, kare
listesini dışarı vermiyor. Elimizdeki tek şey toplu sayı.

Toplu sayıdan bir **üst sınır** türetilebilir. Kıskaca değen karelerin oranı
`f` ise, o karelerin her biri toplama `1/1 = 1` katkı verir; diğer karelerin
katkısı pozitiftir. `H = N / Σ(1/max(x,1))` olduğundan `Σ/N = 1/H ≥ f`, yani
**`f ≤ 1/H`.** `parca-3 / handbrake @ 60 MB` için `H = 13,72` → **`f ≤ %7,3`.**
Bu bir tavan, ölçüm değil; gerçek oran bundan çok daha küçük olabilir.

Kaldıraç büyük. Aynı satırda karelerin yalnız %1'i gerçek 0 olsaydı ve taban
1,0 yerine 0,1 olsaydı, o kareler toplama 1 yerine 10 katkı verirdi ve harmonik
ortalama **13,72 → 6,14**'e düşerdi. Yani manşet sayı, seçilmiş ama belgelenmemiş
bir sabite bu ölçüde duyarlı.

Bu satır `QualityMeter` bizim `owns`ımızda olmadığı için düzeltilmedi; eksik
listesine yazıldı. Karşılaştırmayı geçersiz kılmıyor — kıskaç iki tarafa da aynı
biçimde uygulanıyor — ama **mutlak sayı olarak okunmamalı.**

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
| harmonik ortalamada `Math.Max(x, 1.0)` kıskacı (satır 147) belgesiz | manşet sayının tamamı taban 1,0'a bağlı; kıskaç olmasa dört özet satırının dördü de 0,00 olurdu |
| libvmaf JSON günlüğü `%TEMP%`e yazılıp `finally` içinde siliniyor (satır 124, 157) | ham ölçüm günlüğü saklanamıyor **ve** kıskaca kaç karenin değdiği sayılamıyor; yalnız `f ≤ 1/H` tavanı türetilebiliyor |
| `ColorFilter` etiketsiz girdiye varsayım uyduruyor (`?? (hdr ? "bt2020" : "bt709")`) | GEÇERSİZ tabloyu üreten hata `QualityMeter` içinde hâlâ duruyor; kapı A/B aracında, ölçerde değil |
| `ColorIncompatibility` yalnız HDR/SDR uyuşmazlığını reddediyor | etiketsiz taraf ölçere kadar gidebiliyor |
| kare hızı hiç bakılmıyor | kare hızları ayrıyken libvmaf sessizce sayı üretiyor; kapı yine A/B aracında |
| en boy oranı hiç bakılmıyor | kırpılmış çıktı sayı basıyor ve puan bit hızından bağımsız sabitleniyor |
| `[t][r]libvmaf` grafiğinde `setpts=PTS-STARTPTS` yok | kare eşlemesi zaman damgasına bağlı; `start_time` sıfır değilse sıfırlama A/B aracının işi oluyor |
| tonemap'li ölçümün pencere (başlangıç/süre) aşırı yüklemesi yok | parça ölçümü ayrı dosya kesmeyi gerektiriyor |
| XPSNR düzlem başına verilmiyor | kroma bozulması ayrı görülemiyor |

## Ölçüler

`AbTests` adıyla, `dotnet test -c Release --filter "AbTests"` — 55 test, 55 geçti,
0 kaldı, 0 atlandı. 42'si bu sözleşmenin (`ColorGateAbTests` 9,
`ChunkAggregateAbTests` 8, `SensitivityAbTests` 6, `GeometryGateAbTests` 5,
`SizeParityAbTests` 5, `AbSettingsAbTests` 4, `HandBrakeArgumentsAbTests` 4,
`DeviationAbTests` 1); kalan 13 `SettingsTabTests` süzgece adından ötürü takılıyor.
Renk kapısı, geometri kapısı, eş boyut toleransı, parça birleştirme (p10 dahil),
duyarlılık eşiği ve HandBrake bit hızı hesabı üretim davranışı üzerinden ölçülür:
sabit karşılaştırma, `Skip` ve sessiz erken dönüş yok.
