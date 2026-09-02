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

Ölçülen ayrışma, 60 MB'den 600 MB'ye. **Bu tablo `sonuc-parca.json` koşumundan,
yani eşitleme turu eklenmeden önceki koşumdan**; VidShrink'in 60 MB satırı
orada 18,98'di. Eşitleme turlu koşumda o satır 19,66 oluyor (aşağıdaki özet
tablosu) ve ayrışma +39,17'ye iniyor — duyarlılık kararı iki koşumda da aynı,
o yüzden tablo yeniden koşturulmadı:

| yarışmacı | 60 MB harm | 600 MB harm | ayrışma | eşik | ölçülen commit |
|---|---|---|---|---|---|
| handbrake | 28,70 | 67,96 | **+39,26** | 1,00 | `af7a0fe` |
| vidshrink (eşitleme öncesi) | 18,98 | 58,83 | **+39,85** | 1,00 | `af7a0fe` |
| vidshrink (eşitleme sonrası) | 19,66 | 58,83 | **+39,17** | 1,00 | 60 MB `381e8ab`, 600 MB `af7a0fe` |

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

| yapılandırma | tam koşum harm | parça kestirimi harm | sapma | ölçülen commit |
|---|---|---|---|---|
| vidshrink @ 600 MB | 47,78 | 58,83 | **+11,05** | tam `94df05c`, parça `af7a0fe` |

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

Aşağıdaki sayıların geldiği koşum kayıtları (hepsi `.calisma/` altında, git'e
girmiyor) ve her koşumun **ölçtüğü commit**. Ölçtüğü commit = koşumun ikilisini
üreten ağaç; bunu koşum saatinden önceki son **kod taşıyan** commit belirliyor
(araya giren commit'ler yalnız `docs/` değiştirdi, ölçüme dokunmuyor).

| tablo | koşum kaydı | koşum saati | ölçülen commit |
|---|---|---|---|
| 60 MB (ikiye bölmeli eşitleyici) | `.calisma/ab/sonuc-parca-60-ikiyebolme.json` | 09-02 06:38 | `381e8ab` (tur 3) |
| 60 MB (yalnız oranlı eşitleyici, çürütülen tablo) | `.calisma/ab/sonuc-parca-60-esboyut.json` | 09-02 05:06 | `a1994b3` (tur 2) |
| 600 MB | `.calisma/ab/sonuc-parca.json` | 09-02 03:34 | `af7a0fe` (tur 1) |
| duyarlılık (eşitleme öncesi) | `.calisma/ab/sonuc-parca.json` | 09-02 03:34 | `af7a0fe` (tur 1) |
| tam kaynak koşumu (örnekleme sapması) | `.calisma/ab/sonuc-tam.json` | 09-02 04:27 | `94df05c` (tur 1) |

600 MB tablosu tur 1'in commit'inden geliyor ve **yeniden ölçülmedi**: eşitleyici
o tabloya dokunmuyor, çünkü altı satırın altısı zaten kapının içindeydi. 60 MB
tablosu ise dalın son kod commit'i `381e8ab` ile ölçüldü. Yani bu belgede iki farklı commit
ölçülmüş durumda. İki commit'in sayısı tek tabloda yan yana geldiği tek yer
**Özet** tablosu; orada satır başına `ölçülen commit` sütunu var.

### 60 MB hedefinde

İlk koşumda VidShrink üç parçanın üçünde de hedefin altında kalmış ve üç satır da
`eş boyut değil` damgası yemişti (-%3,13 / -%8,61 / -%3,17). O tablodan
karşılaştırma çıkarılamaz. Araca **eşitleme turu** eklendi
(`tools/VidShrink.Ab/TargetSearch.cs`): taban yarışmacının gerçek baytına bakıp
adayın hedefini düzeltip yeniden kodluyor, varsayılan dört deneme. Arayış iki
kipli — bandın bir yanında kaldığı sürece **oranla** düzeltiyor
(`hedef x taban/teslim`), bir kez üstte ve bir kez altta teslim gördükten sonra
o aralığı **ikiye bölüyor**. İkinci kip zorunlu: teslim edilen bayt hedefin
basamaklı bir fonksiyonu, ve yalnız oranla düzeltme basamak fonksiyonunda
yakınsamıyor, iki basamak arasında salınıyor (ölçüldü, aşağıda). Aşağıdaki
tablo ikiye bölmeli eşitleyicinin koşumundan.

**Ölçülen commit: `381e8ab` — dalın son kod commit'i.**

| girdi | yarışmacı | yerleşim | bayt | fark % | eş boyut | harm | p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|---|
| parca-1 | handbrake | 1920x1080 @483k | 3.531.037 | 0,00 | evet | 47,71 | 34,32 | 3,02 | 52,16 | 30,93 |
| parca-1 | vidshrink | 768x432 @479k | 3.526.564 | -0,13 | evet | 31,94 | 18,11 | 1,46 | 39,14 | 28,97 |
| parca-2 | handbrake | 1920x1080 @483k | 3.730.691 | 0,00 | evet | 93,70 | 93,07 | 72,27 | 93,71 | 43,59 |
| parca-2 | vidshrink | 1190x670 @522k | 3.769.379 | +1,04 | evet | 69,65 | 67,65 | 52,23 | 69,76 | 33,32 |
| parca-3 | handbrake | 1920x1080 @483k | 3.680.998 | 0,00 | evet | 13,72 | 11,79 | 0,00 | 17,46 | 28,21 |
| parca-3 | vidshrink | 652x366 @479k | 3.677.261 | -0,10 | evet | 9,35 | 6,99 | 0,00 | 13,53 | 27,17 |

**Üç parçanın üçü de kapının içinde** (-%0,13, +%1,04, -%0,10). Toplam bayt
10.973.204 ↔ 10.942.726, yani **+%0,28**. Bu tabloda damga yok.

`parca-2`nin kapıya girmesi ikiye bölmeyle oldu; tur 2'de yalnız oranlı düzeltme
vardı ve o parça -%3,69'da takılmıştı. Ölçülen yoklama dizisi:

| deneme | kip | hedef | seçilen yerleşim | teslim | tabana fark |
|---|---|---|---|---|---|
| 0 | — | 3,4975 MB | 1152x648 | 3.402.466 | -%8,80 |
| 1 | oranlı | 3,8348 MB | 1190x670 | 3.863.888 | +%3,57 |
| 2 | ikiye bölme | 3,6661 MB | 1152x648 | 3.572.151 | -%4,25 |
| 3 | ikiye bölme | **3,7505 MB** | **1190x670** | **3.769.379** | **+%1,04** |

Oranlı düzeltme tek başına 3,8348 ile 3,6661 arasında salınıyordu; aradaki
aralığı ancak ikiye bölme yokladı ve üçüncü denemede kapının içine girdi.

### 600 MB hedefinde

**Ölçülen commit: `af7a0fe` (tur 1).** Bu tablo dalın ucuyla ölçülmedi; aradaki
kod değişikliği yalnız eşitleyicidir ve bu tablonun altı satırının altısı zaten
kapının içindeydi, yani eşitleyici hiç devreye girmiyor. Yine de yazılı olan
budur: **bu altı satır `381e8ab` ile yeniden ölçülmedi.**

| girdi | yarışmacı | yerleşim | bayt | fark % | eş boyut | harm | p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|---|
| parca-1 | handbrake | 1920x1080 @4833k | 35.288.140 | 0,00 | evet | 81,48 | 79,04 | 4,12 | 83,43 | 38,00 |
| parca-1 | vidshrink | 1382x778 @4861k | 35.746.203 | +1,30 | evet | 70,82 | 67,21 | 3,49 | 72,56 | 36,07 |
| parca-2 | handbrake | 1920x1080 @4833k | 36.766.517 | 0,00 | evet | 95,78 | 95,32 | 71,96 | 95,78 | 47,11 |
| parca-2 | vidshrink | **1920x1080** @4712k | 36.137.359 | -1,71 | evet | 95,84 | 95,42 | 74,89 | 95,84 | 51,25 |
| parca-3 | handbrake | 1920x1080 @4833k | 36.272.258 | 0,00 | evet | 46,66 | 64,62 | 0,00 | 69,87 | 36,35 |
| parca-3 | vidshrink | 1190x670 @4712k | 35.549.615 | **-1,99** | evet (kıl payı) | 37,83 | 54,59 | 0,00 | 60,10 | 35,00 |

`parca-3 / vidshrink` satırı kapıya **0,008 puan** uzakta: ham fark **-%1,9923**,
eşik -%2. Aşağıdaki "ölçünün kararsızlığı" bölümü kalibrasyonun koşumdan koşuma
2.728 bayt oynadığını yazıyor; bu satırda **2.900 baytlık** bir oynama hem
damgayı hem manşetteki "on ikinin on ikisi eş boyut" cümlesini birlikte devirirdi. Satır
bu haliyle **koşuma bağlı**, kararlı değil — manşetteki 600 MB kaydı bu koşulla
okunmalı (aşağıda "kıl payı satır" başlığında pimlendi).

### Özet

| yarışmacı | hedef MB | ölçülen commit | toplam bayt | eş boyut | harm | en kötü p10 | kare min | ort | XPSNR |
|---|---|---|---|---|---|---|---|---|---|
| handbrake | 60 | `381e8ab` | 10.942.726 | evet | **28,70** | 11,79 | 0,00 | 54,44 | 34,25 |
| vidshrink | 60 | `381e8ab` | 10.973.204 | evet (+%0,28) | **19,66** | 6,99 | 0,00 | 40,81 | 29,82 |
| handbrake | 600 | `af7a0fe` | 108.326.915 | evet | **67,96** | 64,62 | 0,00 | 83,03 | 40,49 |
| vidshrink | 600 | `af7a0fe` | 107.433.177 | evet | **58,83** | 54,59 | 0,00 | 76,17 | 40,77 |

**Geridiyiz.** İki hedefte de eş boyutta ölçüldü ve ikisinde de HandBrake önde:
600 MB'de harmonik ortalamada **9,13 puan** (67,96 ↔ 58,83), 60 MB'de
**9,04 puan** (28,70 ↔ 19,66). On iki satırın on ikisi ±%2 kapısının içinde.

Bir koşula bağlı: 600 MB'deki `parca-3 / vidshrink` satırı kapıya **0,008 puan**
uzakta (-%1,9923) ve o satır tek koşuma dayanıyor. Aşağıdaki kararsızlık bölümü
kalibrasyonun koşumdan koşuma 2.728 bayt oynadığını ölçüyor; o satırda 2.900
baytlık bir oynama damgayı devirir. Yani "on ikinin on ikisi" cümlesi bir satırı
kıl payına borçlu ve o satırın kararlılığı **ölçülmedi.**

60 MB'nin eş boyuta girmesi bu turda oldu. Tur 2'de üç parçanın biri -%3,69 ile
dışarıda kalmıştı ve bunun sebebi ürüne değil **alete** yazıldı: eşitleyici yalnız
oranlı düzeltme yapıyordu ve teslim edilen bayt hedefin basamaklı bir fonksiyonu
olduğu için iki basamak arasında salınıyordu. İkiye bölme eklendi, o parça
üçüncü denemede +%1,04 ile kapının içine girdi. Açığın seyri, aynı satırlar
eşitliğe yaklaştıkça: **9,72 → 9,11 → 9,04.** Yön beklendiği gibi daralma
çıktı, çünkü dışarıda kalan satırda VidShrink daha az bayt harcıyordu.

Altı parça-hedef çiftinin beşinde HandBrake kazandı. Kazandığımız tek çift
`parca-2` @ 600 MB (95,84'e 95,78 — bu fark gürültü sayılır; XPSNR ise belirgin
yüksek, 51,25'e 47,11) ve bu, **çözünürlüğü düşürmediğimiz tek çift**: orada
VidShrink 1920x1080'de kaldı. Cümle "kazandık" diye kurulamaz — kurulacaksa
**çözünürlük düşürmediğimiz yerde kazandık** diye kurulur. Çözünürlük düşüren
beş çiftin beşinde kaybettik. Yönün ötesinde bir şey iddia edilmiyor; büyüklüğün
düşürme oranıyla gitmediği aşağıda ayrı başlıkta ölçüldü.

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
kendi veriyor. HandBrake her satırda 1920x1080'de kalıyor; VidShrink **her parçada
ayrı bir yerleşim seçiyor**, aynı hedefte bile:

| girdi | 60 MB'de seçilen (`381e8ab`) | 600 MB'de seçilen (`af7a0fe`) |
|---|---|---|
| parca-1 | 768x432 @479k | 1382x778 @4861k |
| parca-2 | 1190x670 @522k | **1920x1080** @4712k |
| parca-3 | 652x366 @479k | 1190x670 @4712k |

Yani "çözünürlüğü düşürüp bit hızını kurtarıyor" tek bir karar değil, üç ayrı
karar. Kodlayıcı, önceden bilinen bir kalıp değil, parçanın karmaşıklığına göre
oynayan bir seçim yapıyor.

### Ayrı bulgu — kazandığımız tek satır, düşürmediğimiz tek satır

Altı çiftin içinde VidShrink'in önde bitirdiği tek satır `parca-2 @ 600 MB`
(95,84 ↔ 95,78 harmonik; XPSNR 51,25 ↔ 47,11, bu ikincisi gürültü sayılamayacak
kadar büyük). Bu satır aynı zamanda **1920x1080'de kaldığımız tek satır.**

Sıralama piksel oranına göre (1920x1080 = 2.073.600 piksel taban):

| girdi @ hedef | yerleşim | piksel oranı | harm (vs ↔ hb) | açık | kazanan | ölçülen commit |
|---|---|---|---|---|---|---|
| parca-2 @ 600 | **1920x1080** | %100,0 | 95,84 ↔ 95,78 | **-0,06** | **vidshrink** | `af7a0fe` |
| parca-1 @ 600 | 1382x778 | %51,8 | 70,82 ↔ 81,48 | 10,66 | handbrake | `af7a0fe` |
| parca-2 @ 60 | 1190x670 | %38,4 | 69,65 ↔ 93,70 | 24,05 | handbrake | `381e8ab` |
| parca-3 @ 600 | 1190x670 | %38,4 | 37,83 ↔ 46,66 | 8,83 | handbrake | `af7a0fe` |
| parca-1 @ 60 | 768x432 | %16,0 | 31,94 ↔ 47,71 | 15,77 | handbrake | `381e8ab` |
| parca-3 @ 60 | 652x366 | %11,5 | 9,35 ↔ 13,72 | 4,37 | handbrake | `381e8ab` |

Tablodan **yalnız yön** okunuyor: düşürmediğimiz tek satırda kazanıyoruz,
düşürdüğümüz beş satırda kaybediyoruz. **Büyüklük okunmuyor** — açık piksel
oranıyla tekdüze gitmiyor: aynı %38,4 oranında bir satırda 24,05, diğerinde
8,83; en çok düşürülen satırda (%11,5) açık en küçük (4,37). Yani "ne kadar
çok düşürürsek o kadar çok kaybederiz" bu veriden **çıkmıyor**, yazmıyorum.

Altı nokta zaten sebep ayırt etmeye yetmez: düşürülen parçalar aynı zamanda
başka bakımlardan da farklı parçalar olabilir ve düşürme kararının kendisi
parçanın karmaşıklığından türetiliyor — yani neden ile sonuç aynı kaynaktan
besleniyor. Bu bir ilinti gözlemi, ölçülmüş bir sebep değil.

T107'nin öncülü (yerleşim skoru ölçülen kaliteyi tahmin etmiyor) ile aynı yöne
bakıyor; **T107'ye devredilecek**, burada sonuca bağlanmıyor. Bu düzeneğin
ölçtüğü şey açık, yerleşim kararının doğruluğu değil.

### Ayrı bulgu — VidShrink istenen boyutu bitirmiyor

Bu açığın parçası değil, ayrı bir bulgu. Ölçüldü çünkü eş boyutu tutturmayı
zorlaştıran şey buydu.

VidShrink'e istenen boyut ile teslim ettiği boyut arasında sistematik bir açık
var. Eşitleme arayışının **sıfırıncı** yoklaması, yani ürünün hedefe kendi
başına verdiği cevap:

| girdi | istenen | teslim | fark |
|---|---|---|---|
| parca-1 | 3,4975 MB | 3,2594 MB (3.417.703 bayt) | **-%6,81** |
| parca-2 | 3,4986 MB | 3,2449 MB (3.402.466 bayt) | **-%7,25** |
| parca-3 | 3,4994 MB | 3,3993 MB (3.564.378 bayt) | **-%2,86** |

(Tur 2'nin koşumunda aynı üç sayı -%6,75 / -%7,23 / -%2,83 çıkmıştı; aradaki
fark kalibrasyonun koşum gürültüsü, aşağıdaki kararsızlık bölümüne bakın.)

Kodlayıcı günlüğü açığın iki katmandan geldiğini gösteriyor. Aşağıdaki iki tablo
tur 2'nin kodlayıcı günlüklerinden okundu (`.calisma/ab/gunluk/`); ikiye bölme
turu bu iki katmanı değiştirmiyor, yalnız aletin onlara rağmen kapıya girmesini
sağlıyor.

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

**Üçüncü gözlem — çözünürlük basamağı hedef-bayt eğrisini kesintili yapıyor.**
Hedefi büyütmek bir noktada plana çözünürlük atlatıyor ve teslim edilen bayt
sıçrıyor. `parca-2` @ 60 MB'de ölçülen dört nokta:

| hedef | seçilen çözünürlük | teslim | tabana fark |
|---|---|---|---|
| 3,4975 MB | 1152x648 | 3.402.466 | -%8,80 |
| 3,6661 MB | 1152x648 | 3.572.151 | -%4,25 |
| **3,7505 MB** | **1190x670** | **3.769.379** | **+%1,04** |
| 3,8348 MB | 1190x670 | 3.863.888 | +%3,57 |

Basamak 3,6661 ile 3,7505 arasında. Sıçrama gerçek — %2,3'lük bir hedef artışı
teslimi %5,5 büyütüyor — ama **eş boyutu engellemiyor:** üst basamağın alt ucu
(3,7505) tam kapının içine düşüyor.

> **Tur 2'de burada yanlış bir cümle vardı.** "Bu parçada eş boyut erişilebilir
> değil — ulaşılabilir bayt değerleri ayrık" yazılmıştı. Yanlıştı, iki
> bakımdan. Bir: bir basamak tek bayt değeri üretmiyor, kendi içinde sürekli
> bir aralık üretiyor (1152x648 basamağında 3.402.466'dan 3.572.151'e). İki:
> o cümlenin dayandığı üç nokta (3,497 / 3,700 / 3,836) aradaki aralığı hiç
> yoklamamıştı; **yoklanmamış aralığa dayanan bir imkânsızlık iddiasıydı.**
> Aralık yoklandı, eş boyut bulundu. Sebep üründe değil, aletin
> yakınsamamasındaydı — ve alet düzeltildi.

Bu maddelerden ilk ikisi (plan payı ve kabul bandı) `src/VidShrink.Core`
tarafında ve bu sözleşmenin dışında; buraya ölçüm olarak yazıldı, düzeltmesi
ayrı bir sözleşmenin işi. Üçüncüsü A/B aracının kendi sorunuydu ve kapandı.

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
28,70 da, 67,96 da, 58,83 da, 19,66 da kıskacın varlığına borçlu.

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

### Bu belgedeki sayılar sunum için yuvarlanmıştır

Tablolardaki puanlar iki ondalığa yuvarlı, farklar ise **yuvarlanmış sayılardan**
çıkarılıyor. Ham değerler biraz başka:

| yazılı | ham çıkarma | ham sonuç |
|---|---|---|
| 9,13 (600 MB açığı) | 67,9559 - 58,8311 | **9,1248** |
| 9,04 (60 MB açığı, eş boyut) | 28,6996 - 19,6593 | **9,0403** |
| 9,72 (60 MB, eşitleme öncesi) | 28,6986 - 18,9843 | **9,7143** |

Yuvarlama sunum tercihi; ama belge boyunca "9,13", "9,04" ve "9,72" ham
sayıymış gibi kullanılıyor, öyle okunmamalı. Fark bu ölçekte sonucu
değiştirmiyor (üçüncü ondalıkta), yine de karar bu sayıların binde birine
dayandırılmamalı.

### Ölçünün kendi kararsızlığı

Aynı yapılandırma iki kez koştu (geometri düzeltmesinden önce ve sonra, `parca-1`
kırpmadan etkilenmediği için iki koşumda da aynı girdiyle). HandBrake bit bit aynı
çıktıyı verdi (3.531.037 bayt, harmonik 47,71 — sıfır fark). VidShrink'in ölçüm
temelli kalibrasyonu koşumdan koşuma az da olsa oynuyor: 3.417.959 → 3.420.687 bayt,
harmonik 31,40 → 31,50, p10 17,35 → 16,93. Yani bu düzenekte 0,1-0,5 VMAF-NEG'lik
farklar gürültüdür; yukarıdaki 9 puanlık açık değildir.

#### Kıl payı satır — eş boyut damgası her koşumda aynı çıkmayabilir

Puan gürültüsü açığı devirmiyor, ama **bayt gürültüsü damgayı devirebilir.**
`parca-3 / vidshrink @ 600 MB` satırı -%1,9923 ile kapıya **0,008 puan** uzakta:
kapıya 35.549.615 yerine 35.546.715 bayt gelseydi (aradaki fark **2.900 bayt**)
satır `eş boyut değil` damgası yerdi. Yukarıda ölçülen kalibrasyon oynaması
**2.728 bayt** — aynı büyüklük sınıfında.

Bu satır iki kez koşturulmadı, yani kararlılığı **ölçülmedi**. O yüzden
manşetteki "on iki satırın on ikisi ±%2 içinde" cümlesi koşulsuz değil: bir satırı
tek koşuma dayanıyor ve o satırın payı bin baytlarla ölçülüyor. Manşette bu
koşul açıkça yazılı. Kapatmanın yolu ucuz değil: `parca-3 @ 600 MB` çiftini
birkaç kez koşturup bayt dağılımını çıkarmak gerekir; bu turda koşturulmadı.

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

`AbTests` adıyla, `dotnet test -c Release --filter "AbTests"` — 69 test, 69 geçti,
0 kaldı, 0 atlandı. 56'sı bu sözleşmenin (`TargetSearchAbTests` 11,
`ColorGateAbTests` 9, `SizeParityAbTests` 8, `ChunkAggregateAbTests` 8,
`SensitivityAbTests` 6, `GeometryGateAbTests` 5, `AbSettingsAbTests` 4,
`HandBrakeArgumentsAbTests` 4, `DeviationAbTests` 1); kalan 13 `SettingsTabTests`
süzgece adından ötürü takılıyor. Elli altısının elli altısı düz `[Fact]`.

Bunun ffmpeg'siz CI'da ne anlama geldiği tahminle değil **koşum günlüğüyle**
kapatıldı: CI koşumu `33590657090`'ın atladığı 72 ölçünün sıfırı `AbTests`
(aşağıda "Tam süit"). Yani bu sözleşmenin hiçbir kabul kriteri atlananların
içinde değil. Eskiden bu cümle `ci-gibi-kos.sh`'e dayanıyordu; o dayanak
**geçersiz** — betik ffmpeg'i PATH'ten siliyor ve T115'ten sonra CI'ı temsil
etmiyor (T118).

Renk kapısı, geometri kapısı, eş boyut toleransı, hedef arayışı, parça
birleştirme (p10 dahil), duyarlılık eşiği ve HandBrake bit hızı hesabı üretim
davranışı üzerinden ölçülür: sabit karşılaştırma, `Skip` ve sessiz erken dönüş yok.

Mutasyonla sınandı (her koşumdan önce `dotnet build ... --no-incremental`):

| mutasyon | sonuç |
|---|---|
| `SizeParityCheck.DefaultTolerancePercent` 2.0 → 20.0 | 6 kırmızı |
| `SizeParityCheck.DefaultTolerancePercent` 2.0 → 0.1 | 3 kırmızı |
| `TargetSearch` kıskaç dalı devre dışı (hep oranlı) | 6 kırmızı |
| ikiye bölme orta noktası `span/2` → `span/4` | 2 kırmızı |

Dördü de gerçek kusur: ilk ikisi teslim edilen tolerans **değerini** pimliyor
(tur 2'de sınır mantığı pimliydi, değer değildi); son ikisi ikiye bölmenin
varlığını ve orta noktasını pimliyor.

### Tam süit

| koşum | headSha | sonuç | toplam | geçti | kaldı | atlandı |
|---|---|---|---|---|---|---|
| CI `33590657090` (`ci`, `T95-ab-duzenegi`) | `381e8ab` | success | 1139 | 1067 | 0 | **72** |

Koşum süzgeçsiz: `.github/workflows/ci.yml:31` → `tools/kosum-kapisi/kosum-kapisi.ps1`
→ `dotnet test -c Release --no-restore`, `--filter` yok. Yani bütün çözüm koştu.

**Bu koşum `AbTests` ölçülerini kapsıyor.** Atlanan 72 satırın **sıfırı** `AbTests`
(koşum günlüğündeki `Skipped VidShrink…` satırları sayıldı: 72 satır, `AbTests`
eşleşmesi 0). Bu sözleşmenin 56 ölçüsünün 56'sı düz `[Fact]` ve süzgeç
yok — dolayısıyla ikisi bir arada: ölçüler toplandı ve atlananların içinde
değiller, yani geçenlerin içindeler. Yeşil bu sözleşme hakkında **konuşuyor.**

**Ama yeşilin kapsamı dar, ve bunu yazmak gerekiyor.** Bu koşum **ffmpeg'siz
CI**'da koştu: `main`in bugünkü CI'ında ffmpeg kurulu değil, atlanan 72 ölçünün
tamamı bu yüzden atlanıyor (`Live…`, `PerformanceCheck…`, `ExtremeCompression…`,
`CalibrationProbe…`, `FillBand…`, `Updater…`). Yani "CI yeşil" cümlesi bu belge
için şunu söylüyor: **ffmpeg gerektirmeyen ölçüler geçti.** ffmpeg gerektiren
ölçüler hakkında hiçbir şey söylemiyor — onlar koşmadı. Bu sözleşmenin kabul
kriterleri o 72'nin dışında olduğu için kapsam yeterli; ama cümle "süit geçti"
diye genişletilemez.

**Yerel `tools/ci-gibi-kos.sh` koşumu artık CI'ı temsil etmiyor.**
`381e8ab` üzerindeki yerel koşum 0 çıkış koduyla bitti (dökümü yakalanamadı —
**ölçülmedi**, çıktı boruda kaldı), ama bu çıkış kodu "tam süit geçti" diye
okunamaz: betik ffmpeg'i PATH'ten siliyor, oysa T115 ffmpeg'i CI'a kurdu.
Betik T118'de düzeltilecek. **Bu belgede o betiğe dayanan her cümle bu damgayı
taşır.**

`headSha` ile dalın ucu **aynı değil**, ve bu bilerek böyle: izlenen CI koşumunun
`headSha`'sı `381e8ab`; dalın ucu ondan sonraki, yalnız bu belgeyi değiştiren
commit'ler. Dalın **son kod commit'i `381e8ab`** — `git log --oneline 381e8ab..HEAD
-- src tests tools` boş dönmelidir. Dönmüyorsa CI ölçülen kodu koşturmamıştır ve
yukarıdaki damgalar CI için geçersizdir.
