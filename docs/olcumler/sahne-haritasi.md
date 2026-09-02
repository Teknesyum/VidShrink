# Sahne haritası — eşik, kestirim değeri, maliyet (T96, T101)

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` (1920x1080 hevc 10-bit HDR,
60 fps, 1036,17 sn, oyun görüntüsü). ffmpeg 9.0 (gyan.dev), 2026-09-02.
Makine paylaşımlıydı; paralelde başka ajanların ölçümleri koşuyordu.

T101 T96'nın kurulumunu yeniden üretti: tek geniş tarama (`baseThreshold=0.01`)
0,05'e süzüldüğünde T96'nın haritasının aynısını verdi — 531 aday, 209/82/47/23/6
kesim, 24 sahne, aynı karmaşıklıklar; libx264 bit oranları da %1 içinde. Bu
sayfadaki her T101 farkı kurulum kaymasına değil, değiştirilen tek şeye aittir.

## Sahne kesimi eşiği

Tek geçişte `select='gte(scene,0.05)',metadata=print` ile 531 aday toplandı;
eşikler aynı adaylar üzerinde 1 sn asgari sahne aralığıyla tarandı.

| Eşik | Aday | Kesim sayısı |
|------|------|-------------|
| 0.05 | 531 | 209 |
| 0.10 | 102 | 82 |
| 0.15 | 54 | 47 |
| 0.20 | 26 | 23 |
| 0.30 | 9 | 6 |

ffmpeg çıktısından örnek (kesimler `pts_time` + `lavfi.scene_score` çifti olarak okunur):

```
[Parsed_metadata_1 @ 00000238f5c7b180] frame:0    pts:31583   pts_time:0.350922
[Parsed_metadata_1 @ 00000238f5c7b180] lavfi.scene_score=0.111772
[Parsed_metadata_1 @ 00000238f5c7b180] frame:1    pts:40594   pts_time:0.451044
[Parsed_metadata_1 @ 00000238f5c7b180] lavfi.scene_score=0.056520
```

Gözle doğrulama: 28 aday için kesim öncesi/sonrası kareler çıkarılıp bakıldı.
≥0.20'deki 23 adayın **23'ü gerçek kesim** (diyalog açı değişimi, oyun→menü,
karartma). 0.15–0.20 bandından 5 örneklemin 4'ü gerçek, 1'i sahte
(t=128,2: aynı çekimde kamera kayması, skor 0.156).

**Seçilen eşik 0.20** — gözle bakılan örneklemde yanlış pozitif sıfır, hemen
altındaki bantta sahte kesim başlıyor. `SceneMap.DefaultThreshold = 0.2`.

Bu doğrulama yalnız **yanlış pozitifi** ölçüyordu: üretilen kesimlerin gerçek
olup olmadığına bakıldı, üretilmeyen gerçek kesimlere bakılmadı. Yanlış negatif
T101/K2'de ölçüldü, aşağıda.

## Sahne başına karmaşıklık

İlk deneme kaynak paket boyutuydu (bit/sn, ffprobe): kestirim **zayıftı,
Spearman 0.119** — menü/eğitim ekranlarında kaynak kodlayıcı bol bit harcarken
yeniden kodlama neredeyse bedava, sıralama çöküyor.

Yerine geçen sinyal: aynı geçişte 640 piksele küçültülmüş görüntü
`libx264 ultrafast crf 23` ile null'a kodlanır, kare başına kodlanmış boyut
`-vstats_file` üzerinden okunur. Sahnenin karmaşıklığı = sahnenin sonda
kodlama bit/sn'sinin tüm harita ortalamasına oranı. Tarama ve sonda tek
decode paylaşır (`split` filtresi).

## Üç elek (T101/K3)

Bir gerçek kesimin haritaya geçmesi için üç süzgeçten geçmesi gerekir. Sırayla:

**1. `SceneDetector.BaseThreshold = 0.05`** — ffmpeg'in `select='gte(scene,X)'`
argümanına giren taban. Skoru bunun altında kalan kare hiç günlüğe yazılmaz.
Görevi kesim seçmek değil, günlüğü küçültmek: ham skorların tamamı toplandığında
1036 sn'lik kaynakta ≥0,01 olan 12.686 kare var, 0,05'te 531 kalıyor.

**Bugünkü ayarda hiçbir gerçek kesimi düşüremez.** Üretim `DefaultThreshold = 0.2`
kullanıyor ve 0,2 ≥ 0,05 olduğu için 0,05–0,20 bandında düşen aday zaten ikinci
elekte de düşecekti. K2'de düşürdüğü gerçek kesim: **0**. Bu sabit ancak
`DefaultThreshold` 0,05'in altına indirilirse bir eleğe dönüşür — o gün tavanı
olur, bugün değil.

**2. `SceneMap.DefaultThreshold = 0.2`** — asıl karar eleği. `CutTimes` skoru
bunun altındaki adayı atar. K2'de düşürdüğü gerçek kesim: **18** (skorları
0,112–0,199). Bugün haritanın tek yanlış negatif kaynağı budur.

**3. `SceneMap.DefaultMinSceneSeconds = 1.0`** — eşiği geçmiş iki kesim 1 sn'den
yakınsa ikincisi, ve kaynağın sonuna 1 sn'den yakın kesim, atılır. Kaynağın
tamamında 0,20 eşiğinde 26 aday var, 23 kesim çıkıyor: **3 aday** bu elekte
düşüyor (334,000 @0,438 · 334,084 @0,317 · 522,465 @0,382). Üçü de zaten
kesilmiş bir geçişin ikinci kez yakalanması — yani düşürdüğü **gerçek kesim: 0**.
K2 penceresinde de 0.

## Kaçırılan kesim (T101/K2)

Pencere: kaynağın (144,2 – 333,3] sn arası, 189 sn. Yöntem: 1 fps kontakt
sayfası taraması + ≥0,10 skorlu her aday için kesim öncesi/sonrası kare çifti +
0,03–0,05 bandından kontrol örneklemi. x264 `scenecut` anahtar kareleri yer
gerçeği olarak **denendi ve elendi** — pencerede 35 anahtar kare üretti, ~20'si
kesintisiz oynanışın ortasında, buna karşılık gözle bariz iki kesimi
(158,966 · 170,017) kaçırdı.

| | |
|---|---|
| Gözle doğrulanan gerçek kesim | **28** |
| Haritanın ürettiği kesim | **10** |
| Yanlış pozitif | **0** |
| Kaçırılan (yanlış negatif) | **18** |
| Düşüren elek | `DefaultThreshold = 0.2` — 18'inin **18'i** |
| `BaseThreshold = 0.05` payı | 0 |
| `DefaultMinSceneSeconds = 1.0` payı | 0 |

Kaçırılanların skorları 0,112 ile 0,199 arasında yığılmış. Yani harita
"hassasiyeti yüksek, duyarlılığı düşük" bir kesici: ürettiği kesim doğru
(%100 kesinlik), ama gerçek kesimlerin **%36'sını** üretiyor.

Sonucu somut: haritanın 11 numaralı sahnesi (255,750–327,683 · 72 sn ·
karmaşıklık 0,648) tek bir sahne değil, **~16 çekim**. Oraya biçilen sahne
başına bütçe 16 çekimin ortalamasıdır; içindeki en zor çekim bütçesinin
altında kalır. Eşiği düşürmenin bedeli T96'da ölçülmüştü: 0,15'te yanlış
pozitif başlıyor. İki hatanın hangisinin daha ucuz olduğu **ölçülmedi**.

## Kestirim değeri ve sınırı (T101/K1)

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

Kaybın tamamı **tek bir sahne**: 3 numara (karmaşıklık 0,794), x264'te 4. sıradayken
HEVC ve AV1'de 6. sıraya düşüyor. Harita onu 21'in üstüne koyuyor, iki modern
kodlayıcı da altına koyuyor.

Bunun kodlayıcıya özgü gürültü değil **sistematik** olduğunun kanıtı: ölçülen bit
oranları arasında libx265 ~ libsvtav1 Spearman'ı **1,000**. İki modern kodlayıcı
birbiriyle tam anlaşıyor, ikisi birden haritadan aynı yönde ayrılıyor. Yani
uyuşmazlık "her kodlayıcı başka türlü davranıyor" değil, "640p ultrafast x264
sondası modern kodlayıcıların gördüğü bir şeyi göremiyor".

### Ne yapılmalı — ve ne yapılmamalı

Sözleşme "düşükse haritanın kodlayıcı başına ayarlanması gerektiğini yaz" diyordu.
Ölçüm bunu **desteklemiyor**: kodlayıcı başına ayrı katsayı, aralarında 1,000
korele olan iki kodlayıcı için iki ayrı düzeltme öğrenmek olur. Gerekiyorsa
gereken tek bir ortak düzeltmedir (sonda → modern kodlayıcı), kodlayıcı başına
değil. Bu iş bu turda yapılmadı.

**Sayının kırılganlığı:** n = 8. 0,976 ile 0,929 arasındaki fark tek bir sıra
takasıdır ve bu örneklem büyüklüğünde istatistiksel olarak ayrılamaz. Doğru
okuma "AV1'de %5 kötü" değil, "sekiz sahnede sıralama bir yerde bozuluyor".
Güven aralığı **ölçülmedi**.

Ölçülmeyen kodlayıcılar: `h264_nvenc`, `hevc_nvenc`, `av1_nvenc` — donanım
yollarında korelasyon **ölçülmedi**.

## Çıkarma maliyeti ve yargı (T96/K4, T101/K6)

`SceneDetector.BuildMapAsync` tam kaynakta **107,3 sn** sürdü (T96); T101'in
yeniden koşumu **106,9 sn**. Makine iki koşumda da paylaşımlıydı.

### Payda yanlıştı

T96 bu süreyi kaynağın süresine böldü: %10,4. Ama harita videonun süresine
değil, **onun yerine koşacak kodlamaya** ek yüktür. Doğru payda odur.
T101'in 8 sahnelik ölçümünden (133,8 sn içerik, paylaşımlı makine):

| Hedef | Ölçülen hız | 1036 sn kaynak için tahmini | Haritanın payı |
|-------|-------------|------------------------------|-----------------|
| libx264 veryfast | 3,04× gerçek zaman | ~341 sn | **%31** |
| libsvtav1 preset 8 | 1,11× | ~935 sn | %11 |
| libx265 veryfast | 0,97× | ~1063 sn | %10 |

Üretimde yazılım kodlayıcılar iki geçişli (`FfmpegArguments.NeedsTwoPasses`
donanım olmayan her kodlayıcıda doğru), bu paydaları büyütür ve haritanın payını
düşürür. Donanım yollarında (`*_nvenc`) tersi geçerli — orada kodlama en hızlı,
haritanın payı en yüksek. Donanım kodlama hızı **ölçülmedi**.

Tablodaki süreler paylaşımlı makinede alındı (dört ffmpeg paralel, CPU %100).
Mutlak değerleri değil, aralarındaki büyüklük sırasını okuyun.

### Maliyet nereye gidiyor

Kaynağın tamamı üzerinde ayrı koşumlar (paylaşımlı makine):

| Koşum | Süre |
|-------|------|
| Yalnız çözme (`-i src -f null -`) | 91,8 sn |
| Sonda + kare atlatma (2 karede 1) | 131,4 sn |

Bu ikisi aynı yük penceresinde arka arkaya koştu, karşılaştırılabilir. Daha
yüklü bir pencerede yalnız çözme ve yalnız sonda **ikisi de 175,5 sn** ölçüldü;
makine doyduğunda sondanın çözmenin üstüne eklediği yük ölçüm gürültüsünün
altında kalıyor.

Sonuç: **maliyet çözme-baskın.** Haritanın 107 sn'sinin kabaca ~92 sn'si
1080p60 10-bit HEVC kaynağı çözmek. Tarama ve sonda kodlaması geriye kalan
~%14'ün içinde. Bu oran paylaşımlı makinede çıktı; kesin pay **ölçülmedi**,
ama iki bağımsız koşum aynı yönü gösteriyor.

### Yargı (T101/K6)

**1,8 dakika bugünkü haliyle kabul edilmiyor** — ama T96'nın gerekçesiyle
değil. Sorun sürenin uzunluğu değil, *ikinci bir çözme* olması: kullanıcı
kaynağı bir kez harita için, bir kez kodlama için çözüyor.

**Seçilen aday: sondayı asıl kodlamanın ilk geçişiyle birleştirmek.
Kare atlatma reddedildi.**

Gerekçe — kare atlatma yanlış terime saldırıyor. `select='not(mod(n,2))'`
filtre grafiğinde, yani **çözmeden sonra** çalışır; atlanan kare yine de
çözülmüştür. Çözme maliyetin ~%86'sı olduğuna göre kare atlatmanın
kazanabileceği en fazla şey kalan ~%14'ün yarısı, yani toplamın **~%7'si**.
Ölçüm de bunu söylüyor: kare atlatmalı sonda 131,4 sn, çıplak çözme 91,8 sn —
atlatma taban maliyetin altına inemiyor. 107 sn'yi ~100 sn'ye indirmek için
kestirimin doğruluğunu riske atmak kötü bir takas.

Birleştirme tek terimi kaldırıyor: kodlamanın zaten yapmak zorunda olduğu
çözmeye binerse haritanın kendi çözmesi sıfırlanır — kazanç ~%86, ~%7 değil.

Bu seçimin bedeli, ve neden bu turda uygulanmadığı:

- Yazılım kodlayıcılarda ilk geçiş zaten var (`FfmpegArguments.NeedsTwoPasses`
  donanım olmayan her kodlayıcıda doğru) ve ilk geçişin istatistikleri
  **hedef kodlayıcının kendi** kare boyutlarıdır — 640p x264 sondasından daha
  iyi bir sinyal, üstelik K1'deki 0,929 sapmasını da kökünden kaldırır.
- Ama plan (hedef bit oranı / CRF) ilk geçişten **önce** kurulur. Birleştirme
  planlamayı iki aşamaya bölmeyi gerektirir: haritasız kaba bir ilk geçiş,
  sonra haritayla ikinci geçiş. Bu `EncodeRunner` (T100) ve `PlanCalculator`
  (T99) işidir, T101'in `owns`'ı dışında.
- Donanım yollarında ilk geçiş yok. Orada birleştirme, haritanın kodlamadan
  önce değil kodlama sırasında kurulması demek — tasarım değişikliği, ayar
  değil. Donanım yolunun ne kadar kaybettiği **ölçülmedi**.

## Ölçülmeyenler

- Farklı içerik (film, konuşan kafa, ekran kaydı) üzerinde eşik ve korelasyon.
- Donanım kodlayıcılarda (`h264_nvenc`, `hevc_nvenc`, `av1_nvenc`) aktarım ve hız.
- Kaçırılan 18 kesimin plan hatasına dönüşen bedeli — eşiği düşürmenin
  getirdiği yanlış pozitifle karşılaştırılmadı.
- Kodlayıcı başına Spearman'ın güven aralığı (n = 8).
- Harita→plan bağlaması (T96 kapsam dışı, `PlanCalculator` T99'da).
- Sonda kodlamanın süre kararlılığı (tek koşum, paylaşımlı makine).
