# Sahne haritası — eşik, kestirim değeri, maliyet (T96, T101, T105)

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` (1920x1080 hevc 10-bit HDR,
60 fps, 1036,17 sn, oyun görüntüsü). ffmpeg 9.0 (gyan.dev), 2026-09-01/02.
Makine paylaşımlıydı; paralelde başka ajanların ölçümleri koşuyordu. **Süre
sayılarında bu damga var, kalite ve boyut sayılarında yok** — bit oranı ve
kesim sayısı yükten etkilenmez, saniye etkilenir.

T101 T96'nın kurulumunu yeniden üretti: tek geniş tarama (`baseThreshold=0.01`)
0,05'e süzüldüğünde T96'nın haritasının aynısını verdi — 531 aday, 209/82/47/23/6
kesim, 24 sahne, aynı karmaşıklıklar; libx264 bit oranları da %1 içinde. T105 aynı
taramayı üçüncü kez koştu ve 12.686 adayı, 0,20'de 23 kesimi, 24 sahneyi ve
`sahneler.csv`'deki sahne sınırlarını birebir yeniden üretti. Bu sayfadaki her
tur farkı kurulum kaymasına değil, değiştirilen tek şeye aittir.

## Kesim eşiği (T105/K1–K3)

T96 eşiği yalnız **yanlış pozitife** bakarak seçmişti: üretilen kesimlerin
gerçek olup olmadığı denetlendi, üretilmeyen gerçek kesimlere bakılmadı. T101
eksik yarıyı ölçtü ve 0,2'nin çok yüksek olduğunu gösterdi ama düşürmenin
bedelini ölçmedi. T105 iki yarıyı aynı eğride birleştirir.

### Yer gerçeği: üç pencere

| Pencere | Aralık | Süre | İçerik | Gözle doğrulanan gerçek kesim |
|---------|--------|------|--------|-------------------------------|
| P1-karisik | (144,117 – 333,300] | 189,2 sn | oyun + menü + diyalog | **28** |
| P2-durgun | (333,300 – 519,666] | 186,4 sn | menü / eğitim ekranı | **7** |
| P3-hareketli | (600,000 – 789,000] | 189,0 sn | kesintisiz kılıç dövüşü | **0** |

P1 T101'in penceresidir (`tools/sahne-yer-gercegi/gercek-kesimler.txt`,
elle üretildi, yeniden üretilemez). P2 ve P3 T105'te aynı yöntemle işaretlendi:
1 fps kontakt sayfası taraması + eşik altı bir taban skorun üstündeki her aday
için kesim öncesi/sonrası kare çifti. Listeler ve komutlar bu sayfanın sonunda.

**Pencerelerin başlangıcı 144,117'dir, 144,2 değil.** T96/T101 sayfası 144,2
yazıyordu; ölçüm 144,116778'de yapıldı ve yuvarlama pencereye giren ilk gerçek
kesimi (158,966) etkilemiyor, ama sayı yanlıştı, düzeltildi.

P3'ün **sıfır** olması bir ölçüm eksikliği değil sonucun kendisidir: penceredeki
iki belirgin çerçeve değişimi (~754,5 ve ~767,5) sürekli kamera hareketidir,
o aralıktaki en yüksek skor 0,028 — `BaseThreshold = 0.05` tabanının bile
altında. P3'te üretilen her kesim tanım gereği yanlış pozitiftir; pencerenin
işi eşik düşerken yanlış pozitifin ne kadar hızlı çoğaldığını **saymaktır**.

### Eğri

Aynı tek taramanın adayları (`baseThreshold=0.01`, 12.686 aday) üzerinde,
üretimin kendi `CutTimes` sırasıyla (skor eleği → 1 sn asgari aralık → sona
1 sn yakınlık) tarandı. Eşleşme toleransı **0,25 sn**; gerekçesi:
`MinSceneSeconds = 1.0` üretilen kesimlerin en az 1 sn arayla olmasını garanti
ettiği için 0,25 sn'lik pencere iki üretilen kesimi aynı gerçek kesime
bağlayamaz, ama karartmayla başlayan geçişte kesimin karartma başına düşmesine
izin verir (255,551 → gerçek 255,750; Δ = 0,199).

| Eşik | P1 üretilen | P1 yakalanan/28 | P1 yanlış poz. | P2 yakalanan/7 | P3 yanlış poz. | F1 | F2 | Kaynak tamamı kesim |
|------|-------------|-----------------|----------------|----------------|----------------|-----|-----|---------------------|
| 0,050 | 41 | 28 | 13 | 6 | 33 | 0,591 | 0,773 | 209 |
| 0,060 | 40 | 28 | 12 | 6 | 25 | 0,642 | 0,806 | 173 |
| 0,070 | 37 | 28 | 9 | 6 | 21 | 0,687 | 0,833 | 148 |
| 0,080 | 33 | 28 | 5 | 6 | 15 | 0,764 | 0,876 | 116 |
| 0,090 | 33 | 28 | 5 | 5 | 10 | 0,795 | 0,878 | 94 |
| 0,095 | 32 | 28 | 4 | 5 | 9 | 0,815 | 0,887 | 91 |
| 0,100 | 30 | 28 | 2 | 4 | 8 | 0,831 | 0,879 | 82 |
| **0,105** | **28** | **28** | **0** | **4** | **6** | 0,877 | **0,899** | **76** |
| 0,110 | 28 | 28 | 0 | 3 | 3 | 0,899 | 0,891 | 69 |
| 0,115 | 27 | 27 | 0 | 3 | 0 | **0,923** | 0,882 | 62 |
| 0,120 | 26 | 26 | 0 | 3 | 0 | 0,906 | 0,858 | 60 |
| 0,130 | 26 | 26 | 0 | 3 | 0 | 0,906 | 0,858 | 57 |
| 0,150 | 23 | 23 | 0 | 3 | 0 | 0,852 | 0,783 | 47 |
| 0,200 | 10 | 10 | 0 | 3 | 0 | 0,542 | 0,425 | 23 |
| 0,250 | 3 | 3 | 0 | 3 | 0 | 0,293 | 0,205 | 9 |
| 0,300 | 2 | 2 | 0 | 2 | 0 | 0,205 | 0,139 | 6 |

F1 ve F2 üç pencerenin toplamı üzerinden (35 gerçek kesim, P2'nin yanlış
pozitifi her eşikte 0). Ham çıktı: `.calisma/T105/egri-min1.0.csv`; eşik başına
hangi kesimin kaçtığı ve hangi zamanın yanlış pozitif olduğu
`.calisma/T105/egri-ayrinti-min1.0.txt` içinde tek tek yazılı.

**Yanlış pozitifler sayıldı, varsayılmadı.** T101'in "yanlış pozitif sıfır"
sonucu yalnız 0,2 için doğrudur ve eşik düşünce sıfır kalmıyor: 0,05'te
üç pencerede toplam 46 yanlış kesim var.

### En iyi eşik pencereden pencereye değişiyor

Bu, sözleşmenin yazılmasını istediği sonuçtur ve tek bir sayının neden
uzlaşma olduğunu açıklar:

- **P1 (karışık)** 0,105–0,110 bandında kusursuz: 28/28, sıfır yanlış pozitif.
  Kendi başına bırakılsa bu bandı seçerdi.
- **P2 (durgun)** daha alçak ister: 0,08'de 6/7, 0,105'te 4/7. Menü ve eğitim
  ekranları arasındaki geçişler düşük skorlu — ekranın büyük kısmı sabit kalıyor.
- **P3 (hareketli)** daha yüksek ister: yanlış pozitif ancak **0,115**'te sıfıra
  iniyor, 0,08'de 189 sn'de 15 sahte kesim (12,6 sn'de bir) üretiyor.

Yani durgun içerik ile hareketli içeriğin istediği eşikler **ters yönde** ve
aralarında ~0,035'lik bir açı var. Tek sabit eşik ikisini birden memnun edemez;
içerik uyarlamalı eşik bu turda **ölçülmedi**.

### Seçim ölçütü

Maliyet asimetriktir ve ölçüt bunu yansıtmalıdır:

- **Kaçan kesim** iki çekimin bit bütçesini karıştırır. Hata sahnenin tamamına
  yayılır ve sahne ne kadar uzunsa o kadar uzun sürer: 0,2'de haritanın en uzun
  sahnesi 458 sn'ydi.
- **Sahte kesim** yalnız yerel maliyet doğurur. Tek bir çekimi iki parçaya
  böler; iki parçanın karmaşıklığı birbirinin aynı olduğu için ikisi de doğru
  bütçeyi alır. **Bedava değil ama:** T98 anahtar kare aralığını haritadan
  türetiyor, her sahte kesim zorunlu bir anahtar kare demek. Bu yüzden ölçüt
  yanlış pozitifi tamamen görmezden gelemez.

Ölçüt: **üç pencerenin toplamı üzerinde F2 en yüksek olan eşik.** β = 2, yani
duyarlılık kesinliğin iki katı ağırlıkta — asimetrinin sayısal karşılığı.
Eşik seçimi F1'e bakılsaydı 0,115 çıkardı (F1 = 0,923); F2 asimetriyi
hesaba kattığı için bir kademe aşağı iniyor.

### Seçilen değer

**Sabit eşikte seçilen değer 0,105'ti** — F2 = 0,899 ile eğrinin tepesi. Bugün bu
değer üretimde **karar eşiği değil**: T109 eşiği içerikten türetiyor
(`θ(t) = clamp(0,08 + 2,09·p90(±40 sn); 0,05; 0,15)`, `ThresholdRule.Measured`) ve türetilen
haritada tek bir eşik yok: `SceneMap.Threshold` **`NaN`** gelir, kural `SceneMap.Rule`'da
durur. 0,105 sabiti `SceneMap.FixedThreshold` adıyla, bu sayfadaki eğriyi üreten rejimin
kaydı olarak duruyor. Türetilen eşiğin ölçümü `docs/olcumler/dinamik-esik.md`'de.

Kararlılık kontrolleri (hepsi diskte):

- **Tolerans.** 0,25 yerine 0,35 kullanıldığında eğri değişmiyor, tepe yine
  0,105. 0,15 kullanıldığında P1'in karartmayla başlayan kesimi (255,750) her
  eşikte kaçık sayılıyor ve tüm sütun bir birim kayıyor; tepenin yeri yine
  değişmiyor.
- **Plato.** 0,095–0,115 aralığında F2 0,882–0,899 arasında; seçim bıçak sırtı
  değil. ±0,01 kayma F2'yi 0,02'den az değiştirir.
- **Aşırı uyum riski.** 0,105 P1'in son yanlış pozitifinin kaybolduğu noktadır
  ve P1 eşiği belirleyen pencerelerden biridir. Bunu dengeleyen şey P2 ile
  P3'ün ters yönde çekmesi: 0,105 P2'ye göre fazla yüksek, P3'e göre fazla
  alçaktır. Tek pencereye uydurulmuş bir değer bu iki cezayı birden almazdı.

**Bir sahne belirsiz kaldı ve gerçek kesim sayılmadı:** P1'de 220,951/221,134'teki
menü panelinin kararması. Ölçüt "ani çekim değişimi ya da karartmaya geçişin
başlangıcı" olarak tutuldu, bu geçiş ikisine de tam uymuyor. Seçilen eşikte
etkisi yok — bu aday 0,095'in üstünde zaten üretilmiyor. `gercek-kesimler.txt`
**değiştirilmedi.**

## Üç elek (T101/K3, T105/K4)

Bir gerçek kesimin haritaya geçmesi için üç süzgeçten geçmesi gerekir. Sırayla:

**1. `SceneDetector.BaseThreshold = 0.05`** — ffmpeg'in `select='gte(scene,X)'`
argümanına giren taban. Skoru bunun altında kalan kare hiç günlüğe yazılmaz.
Görevi kesim seçmek değil, günlüğü küçültmek: 1036 sn'lik kaynakta ≥0,01 olan
12.686 kare var, 0,05'te 531 kalıyor — 24 kat küçük günlük.

**Ölçüldü, değişmedi.** Hangi aralıkta bağladığı:

| `DefaultThreshold` | Bağlayan elek |
|--------------------|---------------|
| > 0,05 | Taban **hiç bağlamaz.** 0,01 taramasının 0,05'te süzülmesi haritayı birebir yeniden üretir (531 aday, 209/82/47/23/6 kesim, 24 sahne) — taban skorları bozmuyor, yalnız kırpıyor. |
| ≤ 0,05 | Taban **tavana dönüşür**; karar eleğinin altına inen hiçbir aday günlüğe girmediği için eşiği düşürmek etkisiz kalır. |

0,2 → 0,105 değişimiyle bu sabitin payı 0'dan 0'a gitti, ama **payanda inceldi**:
karar eleğiyle taban arasındaki açıklık 0,15 iken 0,055 oldu. Kural yazıldı ve
teste bağlandı: `DefaultThreshold` 0,05'in altına indirilecekse `BaseThreshold`
de indirilmelidir.

**2. Karar eleği** — sabit rejimde `SceneMap.FixedThreshold = 0.105`, bugünkü
üretimde `ThresholdRule.Measured` kuralının o an verdiği `θ(t)`. Sabit rejimde `CutTimes`,
türetilen rejimde `DerivedCutTimes` skoru eşiğin altındaki adayı atar. Aşağıdaki sayılar
**sabit 0,105 rejiminde** ölçüldü. Seçilen değerde düşürdüğü gerçek kesim: P1'de **0**,
P2'de **2** (405,733 ve 444,000), P3'te 0. Üretilen yanlış kesim: P1'de 0,
P2'de 0, P3'te 6.

**3. `SceneMap.DefaultMinSceneSeconds = 1.0`** — eşiği geçmiş iki kesim 1 sn'den
yakınsa ikincisi, ve kaynağın sonuna 1 sn'den yakın kesim, atılır.

**T101'in "payı 0" sonucu yanlıştı.** T101 yalnız P1'e bakmıştı; P2'de bu elek
gerçek bir kesimi düşürüyor. 334,000'deki kesim **her eşikte** kaçıyor, çünkü
333,300'de bir kesim var ve arada yalnız 0,700 sn geçiyor. Kanıt: aynı eğri
`minSceneSeconds = 0.5` ile yeniden koşulduğunda P2 duyarlılığı 0,05–0,08
bandında 6/7'den **7/7**'ye çıkıyor ve seçilen 0,105'te F2 0,899'dan 0,922'ye
yükseliyor (`.calisma/T105/egri-min0.5.csv`).

**Ölçüldü, değişmedi.** Gerekçe: bu sabitin bedeli yalnız kaçan kesimle değil,
müşterileriyle ölçülür — 1 sn'lik taban T98'in anahtar kare aralığına ve
T104'ün ölçüm penceresine alt sınır koyuyor. 0,5 sn'ye inmenin o iki tarafta
ne kadar tuttuğu **ölçülmedi**; fiyatını T98 bilebilir, T105 bilemez. Elde
duran sayı şu: 1,0 → 0,5, 35 gerçek kesimin 1'ini geri kazandırıyor (%2,9
duyarlılık) ve kaynağın tamamındaki kesimi 76'dan 80'e çıkarıyor.

## Sahne sayısı: eski ve yeni (T105/K5)

T98 (anahtar kare aralığı) ve T104 (ölçüm penceresi) haritadan türetiyor;
gördükleri değişti. Kaynağın tamamı, `minSceneSeconds = 1.0`:

| | Eşik 0,20 (eski) | Eşik 0,105 (yeni) |
|---|---|---|
| Sahne sayısı | 24 | **77** |
| Ortalama sahne süresi | 43,17 sn | **13,46 sn** |
| Medyan sahne süresi | 14,03 sn | **5,62 sn** |
| En kısa sahne | 2,08 sn | 1,29 sn |
| **En uzun sahne** | **457,95 sn** | **122,37 sn** |

Ham çıktı: `.calisma/T105/sahne-ozet.csv`, sahne sınırlarının tamamı
`.calisma/T105/sahneler-esik0.200.csv` ve `sahneler-esik0.105.csv`.

Müşteriler için önemli olan satır **en uzun sahne**. 458 sn'lik bir "sahne"
tek bir çekim değil, düzinelerce çekimin ortalamasıdır; ona biçilen bütçe
içindeki en zor çekimin altında kalır. 122 sn hâlâ uzun, ama aynı hatanın
dörtte biri.

Ortalama ve medyan arasındaki açıklık iki eşikte de büyük (43 ↔ 14, 13,5 ↔ 5,6):
dağılım sağa çarpık, birkaç uzun sahne ortalamayı çekiyor. Aralık ya da
pencere seçen taraf ortalamayı değil **medyanı** kullanmalı.

## Sahne başına karmaşıklık

İlk deneme kaynak paket boyutuydu (bit/sn, ffprobe): kestirim **zayıftı,
Spearman 0.119** — menü/eğitim ekranlarında kaynak kodlayıcı bol bit harcarken
yeniden kodlama neredeyse bedava, sıralama çöküyor.

Yerine geçen sinyal: aynı geçişte 640 piksele küçültülmüş görüntü
`libx264 ultrafast crf 23` ile null'a kodlanır, kare başına kodlanmış boyut
`-vstats_file` üzerinden okunur. Sahnenin karmaşıklığı = sahnenin sonda
kodlama bit/sn'sinin **tüm kaynağın** sonda bit/sn ortalamasına oranı
(`meanBps = bits.Sum() / duration`). Payda sahne sayısına bağlı değildir —
eşik değişince sınırları değişmeyen bir sahnenin karmaşıklığı da değişmez.
Tarama ve sonda tek decode paylaşır (`split` filtresi).

## Kestirim değeri ve sınırı (T101/K1, T105/K6)

### 0,976 tam olarak neyi ölçüyor

T96'nın 0,976'sı iki ayrı şeyin üst üste gelmesiyle çıktı:

1. **Sonda ile doğrulama hedefi aynı kodlayıcı ve aynı CRF'te.** Sonda
   `libx264 crf 23`, doğrulama kodlaması da `libx264 crf 23`. Değişen yalnız
   ölçek (640 → 1920) ve ön ayar (`ultrafast` → `veryfast`).
2. Dolayısıyla ölçülen şey **ölçek/ön ayar aktarımı**: "640p ultrafast x264'te
   pahalı olan sahne, 1080p veryfast x264'te de pahalı mı?" Cevabı 0,976.
   "Kodlayıcıdan bağımsız sahne zorluğu" sorusunun cevabı değildir; o soru
   T96'da sorulmamıştı.

Harita beş kodlayıcıya hizmet edecek. T101 aynı 8 sahneyi iki modern kodlayıcıda
daha, üretimin kendi kalite çıpalarıyla (`CodecModel.ReferenceCrf`: hevc 28,
av1 35; `libsvtav1` ön ayar 8) kodladı.

### Kodlayıcı başına Spearman

| Kodlayıcı | Ayar | Sahne | Spearman (harita karmaşıklığı ↔ ölçülen bit/sn) |
|-----------|------|-------|--------------------------------------------------|
| libx264 | veryfast crf 23 | 8 | **0,976** |
| libx265 | veryfast crf 28 | 8 | **0,929** |
| libsvtav1 | preset 8 crf 35 | 8 | **0,929** |

Düşüş yazıldı: **0,976 genellenmiyor, sınırı 0,93.**

Ölçülen bit oranları (bit/sn), karmaşıklığa göre sıralı:

| Sahne | Karmaşıklık | libx264 | libx265 | libsvtav1 |
|-------|-------------|---------|---------|-----------|
| 17 | 1,559 | 12.768.555 | 5.040.955 | 14.597.977 |
| 10 | 1,457 | 10.638.199 | 4.372.048 | 11.992.356 |
| 12 | 1,015 | 8.112.094 | 3.763.716 | 11.787.950 |
| 3 | 0,794 | 3.431.336 | 1.452.442 | 4.349.138 |
| 21 | 0,765 | 4.437.046 | 2.112.287 | 6.068.998 |
| 23 | 0,489 | 3.128.966 | 1.458.204 | 4.394.666 |
| 16 | 0,408 | 1.501.036 | 777.356 | 2.000.730 |
| 14 | 0,129 | 597.537 | 335.157 | 748.071 |

### Kaybın nereden geldiği

Kaybın tamamı **tek bir sahne**: 3 numara (144,117–158,966 · karmaşıklık 0,794),
x264'te 4. sıradayken HEVC ve AV1'de 6. sıraya düşüyor. Harita onu 21'in üstüne
koyuyor, iki modern kodlayıcı da altına koyuyor.

Bunun kodlayıcıya özgü gürültü değil **sistematik** olduğunun kanıtı: ölçülen bit
oranları arasında libx265 ~ libsvtav1 Spearman'ı **1,000**. İki modern kodlayıcı
birbiriyle tam anlaşıyor, ikisi birden haritadan aynı yönde ayrılıyor.

### Elenen açıklama: gizli kesim değil

En yakın hipotez, 3 numaralı sahnenin aslında birden çok çekim olması ve
karmaşıklığının bir ortalama olmasıydı. **Yeni eşik bunu eliyor:** 0,105'te
sahne 3 bölünmüyor, sınırları birebir aynı kalıyor (144,117–158,966), yalnız
indeksi 3'ten 10'a kayıyor. Karmaşıklık paydası kaynağın tamamının ortalaması
olduğu için değeri de değişmiyor: 0,794. Sapma eşikten gelmiyor.

### Sapmanın adı: 640 piksele küçültme — kısmen

T105 sondayı tek değişkenle üç kez koştu. Aynı 8 sahne, aynı kesim noktaları,
aynı `-threads 4`, tek değişen sonda yapılandırması:

| Varyant | Ölçek | Ön ayar | Spearman ↔ libx265/libsvtav1 |
|---------|-------|---------|------------------------------|
| **A** — üretimin sondası | 640p | ultrafast | **0,929** |
| **B** — küçültme kaldırıldı | 1080p | ultrafast | **0,952** |
| **C** — ön ayar iyileştirildi | 640p | veryfast | **0,905** |

Ölçüm geçerli, çünkü A haritanın kendisini birebir yeniden üretiyor: A ile
`sahneler.csv`'deki karmaşıklık arasında Spearman **1,000**. Yani ayrı ayrı
kesilmiş klipleri kodlamak, boru hattındaki `split`'li sondanın yerini
tutuyor; A/B/C farkları düzenek farkı değil.

Yön belli ve tek yönlü değil:

- **Küçültmeyi kaldırmak iyileştiriyor** (0,929 → 0,952). Kayıp sahnesi 3,
  A'da 5. sırada, B'de 4. sıraya çıkıyor; modern kodlayıcılar onu 3. sıraya
  koyuyor. Yani 640p'ye inmek, sahne 3'ün iki basamaklık sapmasının **bir
  basamağını** açıklıyor.
- **Ön ayarı iyileştirmek kötüleştiriyor** (0,929 → 0,905). `veryfast` sonda
  sahne 3'ü doğru yöne taşıyor ama sahne 23'ü ve 17/10 çiftini bozuyor.
  Bu, "sonda ne kadar iyi kodlarsa o kadar iyi kestirir" beklentisini
  **çürütüyor**.

**Adı konan kısım:** 640 piksele küçültme, sahne 3'te kaybolan iki sıra
basamağının birini alıyor. Küçültme yüksek frekanslı ayrıntıyı siliyor;
modern kodlayıcıların 1080p'de pahalı bulduğu şeyi sonda 640p'de görmüyor.

**Adı konmayan kısım:** B bile 1,000'e ulaşmıyor. Kalan bir basamak
`ultrafast` x264 ile HEVC/AV1 arasındaki kodlama araçları farkından geliyor
olabilir — ama C bunu test etti ve **çürüttü**; başka bir açıklama
**bilinmiyor**.

**Ve her üç sayı da n = 8'de ayrılamaz.** 0,905 ↔ 0,929 ↔ 0,952 aralarındaki
fark birer komşu sıra takasıdır. Buradan okunacak şey "küçültmeyi kaldırınca
korelasyon %2,5 artar" değil, "sekiz sahnede sapmanın yarısı küçültmeye
bakıyor, yarısı bakmıyor". Güven aralığı **ölçülmedi**, örneklem
büyütülmedi.

Ham çıktı: `.calisma/T105/sonda-varyant.csv` (bit, bit/sn, kodlama süresi,
iş parçacığı sayısı), Spearman matrisi ve sıra tablosu
`.calisma/T105/spearman.csv` + `spearman-seriler.csv`.

### Ne yapılmalı — ve ne yapılmamalı

T101 sözleşmesi "düşükse haritanın kodlayıcı başına ayarlanması gerektiğini
yaz" diyordu. Ölçüm bunu **desteklemiyor**: kodlayıcı başına ayrı katsayı,
aralarında 1,000 korele olan iki kodlayıcı için iki ayrı düzeltme öğrenmek
olur. Gerekiyorsa gereken tek bir ortak düzeltmedir (sonda → modern kodlayıcı).

T105 buna bir şey ekliyor: düzeltmenin **en ucuz biçimi katsayı değil, sondayı
küçültmemek** olabilir. B varyantı 0,952'yi hiçbir model öğrenmeden veriyor.
Bedeli ölçüldü ve küçük değil: 8 sahnenin toplam kodlama süresi A'da 32,0 sn,
B'de 51,2 sn (`sonda-varyant.csv`, paylaşımlı makine, `-threads 4`) — sonda
kolu **%60 yavaşlıyor**. Ama sonda maliyetin küçük terimi; belirleyici olan
çözme. Tam kaynakta bu takasın ne ettiği **ölçülmedi**.

## Çıkarma maliyeti ve yargı (T96/K4, T101/K6)

### Ölçülen süreler ve neyin ölçülmediği

| Koşum | Taban eşiği | İş parçacığı | Süre | Kaynak |
|-------|-------------|--------------|------|--------|
| `BuildMapAsync` tam kaynak | 0,05 (üretim) | sabitlenmedi | 107,3 sn | T96 |
| Tek geniş tarama | **0,01** | sabitlenmedi | 106,4 sn | T101 |
| Tek geniş tarama | **0,01** | `-threads 4` | **329 sn** | T105 |

**T101'in 106,9 sn'si düzeltildi.** İki hata vardı: sayı `baseThreshold=0.01`
koşumundan geliyordu, üretimin `0.05`'inden değil; ve günlüğün kendi `elapsed`
değeri 106,39'du, 106,9 değil. T101 bu süreyi üretim yapılandırmasının süresi
gibi yazdı — değildi. **Üretim yapılandırmasında süre T101'de ve T105'te
yeniden ölçülmedi.**

T105'in 329 sn'si aynı komutun üç ay sonraki koşumu değil, **yedi ajanın
paralel koştuğu bir makinede iş parçacığı 4'e sabitlenmiş** koşumudur.
106,4 ile karşılaştırılamaz; buraya yazılma sebebi süre sayılarının bu
sayfada makine yüküne ne kadar bağlı olduğunu göstermek.

### Payda yanlıştı

T96 süreyi kaynağın süresine böldü: %10,4. Ama harita videonun süresine değil,
**onun yerine koşacak kodlamaya** ek yüktür. Doğru payda odur. T101'in
8 sahnelik ölçümünden (133,8 sn içerik, paylaşımlı makine):

| Hedef | Ölçülen hız | 1036 sn kaynak için tahmini | Haritanın payı |
|-------|-------------|------------------------------|-----------------|
| libx264 veryfast | 3,04× gerçek zaman | ~341 sn | **%31** |
| libsvtav1 preset 8 | 1,11× | ~935 sn | %11 |
| libx265 veryfast | 0,97× | ~1063 sn | %10 |

Üretimde yazılım kodlayıcılar iki geçişli (`FfmpegArguments.NeedsTwoPasses`
donanım olmayan her kodlayıcıda doğru), bu paydaları büyütür ve haritanın payını
düşürür. Donanım yollarında (`*_nvenc`) tersi geçerli — orada kodlama en hızlı,
haritanın payı en yüksek. Donanım kodlama hızı **ölçülmedi**.

Tablodaki süreler paylaşımlı makinede alındı. Mutlak değerleri değil,
aralarındaki büyüklük sırasını okuyun.

### Maliyet nereye gidiyor — geri çekildi

T101 burada üç sayı yazmıştı: yalnız çözme 91,8 sn, sonda + kare atlatma
131,4 sn, doygun makinede ikisi de 175,5 sn. **Bu üç sayı geri çekildi.**
T101'in denetimi `maliyet.ps1`'in beş günlük dosyasının da 0 bayt olduğunu,
sonucun yalnız ekrana basıldığını buldu; sayıların arkasında disk yok.
T105'te yeniden ölçülmediler.

`maliyet.ps1`'in bu kusuru düzeltilmedi — `tools/` T105'in `owns`'ı dışında.
Düzeltmesi tek satır: betiğin `Start-Process` çağrısındaki
`-RedirectStandardError` hedefi bir kez oluşturulup her koşumda üzerine
yazılıyor; her koşuma ayrı dosya adı verilmeli ve sonuç satırı
`Add-Content -Path $csv` ile diske de yazılmalı (`kodla.ps1`'in yaptığı gibi).

**Yerine duran şey:** çözmenin baskın terim olduğu yönündeki yargı ölçüsüz
değildir, ama dayanağı artık yalnız yapısal: `select='not(mod(n,2))'` filtre
grafiğinde, yani çözmeden **sonra** çalışır — atlanan kare yine de çözülmüştür.
Kare atlatmanın çözme maliyetinin altına inmesi mümkün değildir. Payın
büyüklüğü (%86 mı %60 mı) **bilinmiyor**.

### Yargı (T101/K6)

**1,8 dakika bugünkü haliyle kabul edilmiyor** — ama T96'nın gerekçesiyle
değil. Sorun sürenin uzunluğu değil, *ikinci bir çözme* olması: kullanıcı
kaynağı bir kez harita için, bir kez kodlama için çözüyor.

**Seçilen aday: sondayı asıl kodlamanın ilk geçişiyle birleştirmek.
Kare atlatma reddedildi.** Gerekçe yukarıdaki yapısal argümandır: atlatma
yanlış terime saldırıyor, birleştirme terimi tamamen kaldırıyor.

Bu seçimin bedeli, ve neden bu turda uygulanmadığı:

- Yazılım kodlayıcılarda ilk geçiş zaten var (`FfmpegArguments.NeedsTwoPasses`
  donanım olmayan her kodlayıcıda doğru) ve ilk geçişin istatistikleri
  **hedef kodlayıcının kendi** kare boyutlarıdır — 640p x264 sondasından daha
  iyi bir sinyal, üstelik K1'deki 0,929 sapmasını da kökünden kaldırır.
- Ama plan (hedef bit oranı / CRF) ilk geçişten **önce** kurulur. Birleştirme
  planlamayı iki aşamaya bölmeyi gerektirir: haritasız kaba bir ilk geçiş,
  sonra haritayla ikinci geçiş. Bu `EncodeRunner` (T100) ve `PlanCalculator`
  (T99) işidir.
- Donanım yollarında ilk geçiş yok. Orada birleştirme, haritanın kodlamadan
  önce değil kodlama sırasında kurulması demek — tasarım değişikliği, ayar
  değil. Donanım yolunun ne kadar kaybettiği **ölçülmedi**.

## Sabitleri tutan testler (T105/K8)

Üç sabit de artık **iki yönlü** kıskaçta. Kanıt sabit karşılaştırarak değil,
sabiti oynatıp testin düşüp düşmediğine bakarak alındı:

| Mutasyon | Sonuç |
|----------|-------|
| `DefaultThreshold` 0,105 → 0,100 | öldü (1 test) |
| `DefaultThreshold` 0,105 → 0,111 | öldü (1 test) |
| `DefaultThreshold` 0,105 → 0,2 (eski değer) | öldü (1 test) |
| `DefaultMinSceneSeconds` 1,0 → 0,8 | öldü (1 test) |
| `DefaultMinSceneSeconds` 1,0 → 1,2 | öldü (2 test) |
| `BaseThreshold` 0,05 → 0,02 | öldü (2 test) |
| `BaseThreshold` 0,05 → 0,08 | öldü (2 test) |
| `ProbeWidth` 640 → 320 | öldü (1 test) |
| `ProbeCrf` 23 → 30 | öldü (1 test) |
| Sona yakınlık eleği kaldırıldı | öldü (2 test) |
| Elek sırası: `last` skoru düşen adayda da ilerlesin | öldü (1 test) |

Ham çıktı: `.calisma/T105/mutasyon-tum.txt`.

T101 denetiminin bulduğu borç kapandı: eski taban eşiği pini yalnız
**yukarı** kaçışı yakalıyordu (`0,05 → 0,15` düşürüyordu ama `0,05 → 0,02`
sessizce geçiyordu, sabit ~0,017'ye kadar kayabiliyordu). Yeni pin,
skorları ölçülerek merdiven halinde kurulmuş bir klip kullanıyor —
0,0246 · 0,0357 · 0,0572 · 0,0910 — ve tabanı **(0,036 – 0,057]** aralığına
kilitliyor. `ScanArgs` ayrıca doğrudan test ediliyor (T101'de hiç test
edilmiyordu): üretilen filtre grafiğinden `gte(scene,X)` çekiliyor ve hem
varsayılan hem açıkça verilen değer için karşılaştırılıyor.

ffmpeg gerektiren testler `[FfmpegFact]` ile işaretli; ffmpeg yoksa **atlanır**,
sessizce yeşil dönmez. Süitte `Skip` sabiti ve ffmpeg yokluğunda erken dönen
test yok.

## Yer gerçeği listeleri ve komutlar

Düzenek `tools/sahne-yer-gercegi/` altında ve T105'in `owns`'ı dışında; T105'in
ürettiği iki yeni liste oraya yazılamadı, buraya gömüldü. Taşınmaları
düzeneğin sahibinin işidir.

### P2-durgun — pencere (333,300 – 519,666], 7 kesim

```
334.000
355.566
405.733
444.000
477.933
506.450
519.666
```

### P3-hareketli — pencere (600,000 – 789,000], 0 kesim

Liste boş. Penceredeki 52 adayın (skor ≥ 0,06) tamamı kılıç ve kamera
hareketinden gelen yanlış pozitiftir.

### Tarama (12.686 aday)

```powershell
$src = "C:\...\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$graph = "[0:v]split=2[a][b];[a]select='gte(scene,0.01)',metadata=print[sc];[b]scale=640:-2[enc]"
ffmpeg -hide_banner -loglevel info -nostats -i $src -filter_complex $graph `
  -map "[sc]" -f null - `
  -map "[enc]" -an -threads 4 -c:v libx264 -preset ultrafast -crf 23 `
  -vstats_file vstats-tam.log -f null - 2> scan-tam.log
```

Adaylar `scan-tam.log`'dan `pts_time` + bir sonraki satırın
`lavfi.scene_score` değeri eşlenerek çıkarılır:

```
[Parsed_metadata_1 @ ...] frame:0    pts:31583   pts_time:0.350922
[Parsed_metadata_1 @ ...] lavfi.scene_score=0.111772
```

### Kontakt sayfası (pencerede eksik kesim aramak için)

```powershell
ffmpeg -hide_banner -y -ss <bas> -t <sure> -i $src -threads 4 `
  -vf "fps=1,scale=320:-2,drawtext=fontfile=C\\:/Windows/Fonts/consola.ttf:fontcolor=yellow:fontsize=18:x=4:y=4:text='%{eif\:trunc(t)+<basInt>\:d}',tile=8x5" `
  -frames:v 1 sayfa-%02d.png
```

### Kesim öncesi/sonrası kare çifti (bir adayı yargılamak için)

```powershell
ffmpeg -hide_banner -y -ss <t-0.060> -i $src -frames:v 1 -vf "scale=320:-2" once.png
ffmpeg -hide_banner -y -ss <t+0.020> -i $src -frames:v 1 -vf "scale=320:-2" sonra.png
```

`-ss` girdiden önce (hızlı arama), kare çiftinin arası 80 ms — 60 fps'de
kesimin iki yanına düşecek kadar dar, arama hatasını yutacak kadar geniş.

## Ölçülmeyenler

- Farklı **kaynak** (film, konuşan kafa, ekran kaydı) üzerinde eşik. Üç pencere
  de aynı 17 dakikalık oyun görüntüsünden; 0,105 bu kaynakta ölçüldü.
- İçerik uyarlamalı eşik. Durgun ve hareketli pencerelerin istediği eşikler
  ~0,035 açıyla ters yönde; tek sabit yerine sahne başına uyarlamanın kazancı
  ölçülmedi.
- `DefaultMinSceneSeconds` 1,0 → 0,5 değişiminin T98 (anahtar kare aralığı) ve
  T104 (ölçüm penceresi) üzerindeki bedeli.
- Donanım kodlayıcılarda (`h264_nvenc`, `hevc_nvenc`, `av1_nvenc`) aktarım ve hız.
- Kodlayıcı başına Spearman'ın güven aralığı (n = 8).
- Üretim yapılandırmasında (`baseThreshold=0.05`) harita çıkarma süresi;
  T96'nın 107,3 sn'si tek koşum, T101 ve T105 yanlış yapılandırmayı ölçtü.
- Çözme teriminin maliyetteki payının sayısal değeri (T101'in 91,8/131,4/175,5
  sayıları geri çekildi).
