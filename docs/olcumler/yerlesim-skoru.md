# Yerleşim skoru ölçülen kaliteye karşı

T107. **Ölçülen commit: `9fa1cda`** (`9fa1cdacbf1627cc07c5943a388ad1ae59167a3d`).
Bu belgedeki bütün skor ve kalite sayıları o ağaçtan derlenmiş ikiliyle üretildi.
Değişiklikten önceki ("eski model") sütunları `4a6377d`'nin skor modelidir.

## 1. Soru

`PlanCalculator.LayoutScore` bir yerleşime (çözünürlük × kare hızı) puan verir ve
`SearchLayout` en yüksek puanlıyı seçer. Bu puanın ölçülen kaliteyle ilişkisi hiç
sınanmamıştı. Bu belge onu ölçer.

Puanın bugünkü hâli:

```
required = ReferenceBppf · RelativeBitrateNeed(codec) · ScaleFactor(scale) · TemporalFactor(fps)
provided = videoK · 1000 / (w · h · fps)
rate     = AtReference − PerHalving · log2(required / provided)
score    = min(rate, QualityLimit) − ScalePenalty(scale) − FpsPenalty(fps)
```

## 2. Ölçüm düzeneği

Düzenek `tools/yerlesim-skoru/`:

- `olc.sh <kaynak> <etiket> <W> <H> <fps> <kbit> <kodlayıcı> <çıktı>` — bir yerleşimi
  kodlar, VMAF-NEG ölçer, bir TSV satırı basar.
- `kos-izgara.sh` — 4 çözünürlük × 3 kare hızı ızgarası, artı ek satır dosyası.
- `ozet.py` — libvmaf JSON'undan ortalama / p10 / p1 / min ve teslim edilen bit hızı.
- `analiz.py` — K1 tablosu, K2 Spearman ve ters çift listesi, merdiven uydurması.
- `Skor.csproj` (`t107skor`) — kaynağı motorun kendi yolundan yoklar
  (`ComplexityProbe.RunDetailedAsync` → `WithProbeQuality` → `WithoutSampleContainerBias`),
  sonra `PlanCalculator.ScoreLayout` ile her yerleşimin skorunu parçalarına ayırıp basar.

Kodlama: `libsvtav1`, **iki geçişli VBR**. Tek geçiş 800k istekte 573,8 kbps teslim
ediyordu (−%28); eşit boyut varsayımı bununla çökerdi. İki geçişte teslim 787–814 kbps
bandında kaldı, yani ızgara eşit boyutta karşılaştırılmış sayılır. İş parçacığı
`-svtav1-params lp=4:pin=0` ile sabitlendi (makine yedi ajanla paylaşımlı).

Kalite: VMAF-NEG (`vmaf_v0.6.1neg`). Bozulmuş akış kaynağın geometrisine bicubic ile,
kaynağın kare hızına `fps=` ile geri getirilip karşılaştırıldı — yani kare hızı
düşürmenin bedeli de ölçüye giriyor. Ortalama ve p10 raporlanır; **harmonik ortalama
kullanılmadı** (T106 onu inceliyor).

Kaynaklar (`.calisma/T107/kaynak/`, worktree-yerel, denetçi göremez):

| Etiket | Ne | Geometri | Süre | Kare farkı |
|---|---|---|---|---|
| A-hareketli | HDR, hızlı hareket | 1920x1080@60 | 20,42 sn | 23,65 |
| B-durgun | HDR, durgun | 1920x1080@60 | 20,40 sn | 1,67 |
| C-ucuncu | SDR | 1920x1080@30 | 23,40 sn | — |

Kaynaklar isimle değil, kare farkı göstergesiyle seçildi. C hiçbir uydurmaya
girmedi; yalnız K4 doğrulamasında kullanıldı.

Üretilen komutlar (worktree kökünden):

```
tools/yerlesim-skoru/kos-izgara.sh .calisma/T107/kaynak/A-hareketli.mkv A libsvtav1 800 \
  .calisma/T107 .calisma/T107/ek-satirlar.txt >> .calisma/T107/gunluk/izgara-A.tsv
dotnet run --project tools/yerlesim-skoru/Skor.csproj -c Release -- \
  .calisma/T107/kaynak/A-hareketli.mkv 2.0 libsvtav1 .calisma/T107/yerlesimler.txt --videok 800
python tools/yerlesim-skoru/analiz.py .calisma/T107/gunluk/skor-AB.txt \
  .calisma/T107/yerlesimler.txt 800 .calisma/T107/gunluk/izgara-A.tsv .calisma/T107/gunluk/izgara-B.tsv
python tools/yerlesim-skoru/ayristir.py
```

Ölçüm dosyaları `.calisma/T107/gunluk/` altında ve **worktree-yereldir; denetçi göremez.**
Aşağıdaki her sayı bu komutlardan çıktı.

## 3. K1 — model ile ölçüm yan yana

800 kbit/s, `libsvtav1` iki geçiş. Skor sütunu değişiklikten **önceki** modeldir.

### 3.1 A-hareketli (12 ızgara + 882x496)

| Yerleşim | eski skor | ölçülen ort | ölçülen p10 |
|---|---|---|---|
| 1920x1080@60 | 68,88 | **45,266** | 32,118 |
| 1920x1080@30 | 65,87 | 38,951 | 13,760 |
| 1920x1080@24 | 64,90 | 35,075 | 12,102 |
| 1600x900@60 | 70,63 | 44,872 | 32,031 |
| 1600x900@30 | 67,62 | 38,264 | 13,449 |
| 1600x900@24 | 66,65 | 34,597 | 11,903 |
| 1280x720@60 | 72,15 | 44,951 | **33,169** |
| 1280x720@30 | 69,14 | 38,232 | 13,183 |
| 1280x720@24 | 68,17 | 34,274 | 11,512 |
| 960x540@60 | 73,05 | 43,856 | 31,990 |
| 960x540@30 | 70,04 | 37,179 | 12,760 |
| 960x540@24 | 69,07 | 33,416 | 11,156 |
| 882x496@60 | **73,11** | 43,461 | 31,719 |

Model en yüksek puanı en küçük çerçeveye veriyor; ölçüm en yüksek kaliteyi en büyük
çerçevede buluyor.

### 3.2 B-durgun

| Yerleşim | eski skor | ölçülen ort | ölçülen p10 |
|---|---|---|---|
| 1920x1080@60 | 77,88 | **81,288** | 77,982 |
| 1920x1080@30 | 75,20 | 78,283 | 68,754 |
| 1920x1080@24 | 74,33 | 77,629 | 67,226 |
| 1600x900@60 | 80,63 | 75,766 | 72,934 |
| 1600x900@30 | 77,95 | 72,831 | 64,283 |
| 1600x900@24 | 77,08 | 72,255 | 62,890 |
| 1280x720@60 | 83,38 | 73,903 | 71,583 |
| 1280x720@30 | 80,69 | 71,164 | 63,660 |
| 1280x720@24 | 79,83 | 70,681 | 62,780 |
| 960x540@60 | 85,86 | 64,300 | 62,505 |
| 960x540@30 | 83,18 | 61,794 | 55,510 |
| 960x540@24 | 82,31 | 61,279 | 54,608 |
| 882x496@60 | **86,07** | 65,385 | 63,793 |

Durgun kaynakta model sıralaması ölçülen sıralamanın neredeyse tam tersi.

Teslim edilen bit hızları 786,8–814,3 kbps bandında; karşılaştırma eşit boyutta.

## 4. K2 — sıralama hatası

| Kaynak | Spearman(skor, ort) | Spearman(skor, p10) | ters çift |
|---|---|---|---|
| A-hareketli | +0,478 | +0,505 | 29/78 |
| B-durgun | **−0,687** | −0,374 | **57/78** |
| C-ucuncu | −0,286 | — | 18/28 |

Durgun kaynakta korelasyon **negatif**: model ne kadar yüksek puan verirse ölçülen
kalite o kadar düşük.

Ters çiftlerin tam listesi `python tools/yerlesim-skoru/analiz.py ...` çıktısındadır.
A'nın 29 ters çiftinin **hepsi** çözünürlük düşüren çiftlerdir; aynı çözünürlükte
kare hızını değiştiren hiçbir çift ters değildir. En büyük dört tanesi:

- `1920x1080@60` vs `960x540@60`: skor −4,17, ölçülen +1,410
- `1920x1080@60` vs `882x496@60`: skor −4,23, ölçülen +1,805
- `1920x1080@30` vs `960x540@24`: skor −3,20, ölçülen +5,535
- `1920x1080@60` vs `960x540@24`: skor −0,19, ölçülen +11,850

B'de aynı yapı daha sert: 78 çiftin 57'si ters, `1920x1080@60` vs `960x540@60`
skorda −7,98, ölçümde +16,988.

## 5. K3 — hangi yarı yanılıyor

Skor iki parçadır: `rate` (bit bütçesinin ihtiyaca oranından gelen kalite tahmini) ve
`ScalePenalty + FpsPenalty` (yerleşimin kendi bedeli). Ayrıştırma
`python tools/yerlesim-skoru/ayristir.py` ile yapıldı.

**Yanılan `rate`.** Sabit `videoK`'da çözünürlük yarıya inince `rate` şu kadar yükseliyor:

```
rate artışı = PerHalving · (DetailExponent + 2) · log2(1/scale)
```

`provided` bppf `1/scale²` ile büyüyor, `required` ise yalnız `scale^DetailExponent`
ile küçülüyor. İkisi aynı yönde çalışıyor ve fark ölçeğin bir fonksiyonu değil,
**içeriğin** fonksiyonu — `PerHalving` ve `DetailExponent` kaynaktan geliyor.

0,5 ölçeğe inerken bu kredi:

| Kaynak | ölçülen PerHalving | R² | rate kredisi (0,5) | modelin ScalePenalty(0,5) |
|---|---|---|---|---|
| A-hareketli | 13,018 | 0,9983 | +11,17 | 7,00 |
| B-durgun | 4,733 | 0,9601 | +14,98 | 7,00 |
| C-ucuncu | ~1,60 | — | +17,27 | 7,00 |

`PerHalving` ölçüsü, 1920x1080 yerel ızgarada 400/800/1600/3200 kbit/s merdiveninden
doğrusal uyumla çıktı; o noktalarda iki cezanın ikisi de tam olarak sıfırdır, yani
merdiven yalnız `rate`'i ölçüyor.

Üç kaynağın ölçülen `PerHalving` değeri 1,60 ile 13,018 arasında; model üçünde de
**ölçülmemiş** varsayılan olan 6'yı kullanıyor (`ComplexityProfile.SlopeMeasured` üçünde
de `False`). Bu tek başına ayrı bir kusur ve bu sözleşmenin kapsamında değil.

**Ceza yarısı da ölçüyle uyuşmuyor, ama düzeltilebilir değil.** Ölçekten gelen gerçek
bedel (aynı bit hızında yerelden düşünce kaybedilen VMAF):

| ölçek | A | B | C |
|---|---|---|---|
| 0,8333 | 0,394 | 5,522 | 4,256 |
| 0,6667 | 0,315 | 7,385 | 7,126 |
| 0,5000 | 1,410 | 16,988 | 9,552 |
| 0,4593 | 1,805 | 15,903 | — |

Yarım ölçekte bedel 1,410 ile 16,988 arasında, **12 kat** fark. Ölçeğin tek başına
fonksiyonu olan bir ceza tablosu bu aralığı taşıyamaz.

Kare hızı tarafında da aynı: yarılama başına gereken ceza A'da 6,807 ve 8,201,
B'de 3,819 ve 3,582, C'de **38,816**. C 30 fps kaynak; 24 fps'e inmek 5:4 tekleme
üretiyor ve p10 87,4'ten 29,757'ye çöküyor. Model yarılama başına 3,50 veriyor.

## 6. Yapılan değişiklik

`PlanCalculator.Decompose` içinde `rate`, teslim edilen ızgarada değil **kaynak
ızgarasında** hesaplanıyor:

```csharp
var requiredAtSourceGrid = required / Math.Max(complexity.ScaleFactor(scale), 1e-9);
var providedAtSourceGrid = provided * scale * scale;
var rate = level.AtReference - level.PerHalving * Math.Log2(
    Math.Max(requiredAtSourceGrid, 1e-9) / Math.Max(providedAtSourceGrid, 1e-9));
```

Her iki taraf da ölçeğe göre aynı şekilde normalize edildiğinden `rate` ölçekten
bağımsız hale geliyor: A kaynağında 60 fps'lik beş yerleşimin dördünde 68,880,
beşincisinde (882x496) 68,878 — fark aday ölçeğinin 0,02'lik adıma yuvarlanmasından,
`rate`'in ölçeğe bağlılığından değil. Çözünürlüğü ayıran tek şey artık `ScalePenalty`.

**Hiçbir sabit değiştirilmedi.** Ne `ScalePenaltyScale`, ne `FpsPenaltyPerHalving`,
ne `PenaltyWeights`. Gerekçe: uydurulmuş cezalar sınanmamış kaynakta daha iyi değil
(§7'deki varyant taraması). Telafi sabiti de eklenmedi.

## 7. K4 — doğrulama ve varyant taraması

C-ucuncu kaynağı model seçilirken hiç kullanılmadı; sekiz yerleşimi (1920x1080,
1600x900, 1280x720, 960x540 × 30/24 fps) sınanmamış ızgaradır.

Dört varyantı aynı ölçüm kümesinde denedim. Hepsi yukarıdaki ölçek-bağımsız `rate`
üstünde, farkları yalnız cezalarda:

| Varyant | ScalePenaltyScale / FpsPenaltyPerHalving | A | B | **C (sınanmamış)** |
|---|---|---|---|---|
| cezalar dokunulmadı | 7,00 / 3,50 | +0,643 (19/78) | +0,967 (4/78) | **+0,667 (6/28)** |
| yalnız fps A+B'ye uyduruldu | 7,00 / 5,6809 | +0,797 (13/78) | +0,879 (10/78) | **+0,667 (6/28)** |
| yalnız ölçek uyduruldu | 8,2477 / 3,50 | +0,599 (20/78) | +0,978 (3/78) | **+0,667 (6/28)** |
| ikisi de uyduruldu | 8,2477 / 5,6809 | +0,780 (14/78) | +0,901 (9/78) | **+0,667 (6/28)** |

Sınanmamış kaynakta dört varyant **aynı** sonucu veriyor. Ölçüm hiçbir sabiti
oynatmak için gerekçe üretmiyor; bu yüzden hiçbiri oynatılmadı.

Beşinci bir seçenek — eski `rate`'i bırakıp cezaları büyütmek — `ScalePenaltyScale`'i
7,00'den **21,19**'a çıkarmayı gerektiriyor ve sınanmamış kaynakta daha kötü:
A +0,681 (17/78), B +0,786 (13/78), C **+0,595 (8/28)**.

Değişiklikten önce ve sonra:

| Kaynak | eski Spearman | yeni Spearman | eski ters çift | yeni ters çift |
|---|---|---|---|---|
| A-hareketli | +0,478 | +0,643 | 29/78 | 19/78 |
| B-durgun | −0,687 | **+0,967** | 57/78 | 4/78 |
| C-ucuncu (sınanmamış) | −0,286 | **+0,667** | 18/28 | 6/28 |

Seçilen plan da değişiyor: A `882x496` → `1728x972`, B `1228x690` → `1920x1080`,
C `1152x648` → `1920x1080`. B ve C'de ölçülen en iyi yerleşim yerel çözünürlüktür,
yani ikisi de artık doğru.

## 8. K5 — 6,39 puanlık örnek

`bppf-tabani.md` §8.5'te modelin `882x496@60`'ı `1280x720@60`'ın 6,39 puan önüne
koyduğu, ölçümün ise tersini söylediği kaydedilmişti.

| Yerleşim | eski skor | yeni skor | ölçülen |
|---|---|---|---|
| 1280x720@60 | 72,15 | **65,615** | **44,951** |
| 882x496@60 | 73,11 | 60,501 | 43,461 |

Yeni modelde 720p önde ve ölçümle aynı yönde. Örnek kapandı.

Bir uyarı: A kaynağında yerel `1920x1080@60` 800 kbit/s'te `tabanGecer=False`, yani
taban ölçüm en iyisini hâlâ eliyor. Taban T99'un konusuydu ve bu sözleşmede
değiştirilmedi.

## 9. K6 — donanım kolu

`av1_nvenc`, A-hareketli, 800 kbit/s. Bu kolda değişiklik **işe yaramıyor** ve bunu
gizlemenin anlamı yok.

| Yerleşim | teslim kbps | eski skor | yeni skor | ölçülen |
|---|---|---|---|---|
| 1920x1080@60 | 729,0 | 68,127 | 68,127 | 31,842 |
| 1600x900@60 | 872,7 | 69,873 | 66,935 | 37,097 |
| 1280x720@60 | 854,0 | 71,394 | 64,862 | 38,730 |
| 960x540@60 | 869,4 | 72,294 | 61,127 | **40,036** |
| 882x496@60 | 858,9 | **72,355** | 59,747 | 38,881 |

Ters çift: eski model **1/10**, yeni model **10/10**. Donanım kolunda küçültme
gerçekten kazanıyor ve eski modelin ölçek kredisi tam da bunu söylüyordu.

Sebep okunabilir: `av1_nvenc` 1920x1080@60'ta 800 kbit/s isteğine karşı 729,0 kbps
teslim ediyor — diğer dört yerleşim 854–873 bandında. Donanım kodlayıcı yerel
çözünürlükte istenen hızı tutturamıyor, kalite de oradan düşüyor. Bu bir kodlayıcı
davranışı; ölçek-kalite ilişkisi değil.

1920x1080@60 satırı eşit boyutta değil (%15 düşük), o yüzden karşılaştırmada
sayılmamalıdır. Kalan dört yerleşimde bile yeni model **6/6** ters.

Model tarafında `kullanilirK` sütunu 1590k (1080p60) ve 1104k (900p60) diyor, yani
taban bu iki yerleşimi 800 kbit/s'te zaten eliyor (`tabanGecer=False`). Ölçüm ikisini
de teslim edebildi; tahmin ile gerçek arasındaki bu fark ayrı bir kusur, kapsam dışı.

## 10. Aşırı rejim (350 kbit/s)

A-hareketli, `libsvtav1`, 12 yerleşim. İki satır dışlandı: `1920x1080@30` 338,6 kbps
ve `1600x900@60` 298,0 kbps teslim etti (istek 350k, −%15), eşit boyut değiller.
Kalan n=10:

| Yerleşim | ölçülen | eski skor | yeni skor |
|---|---|---|---|
| 1280x720@60 | **29,621** | 66,16 | 59,62 |
| 960x540@60 | 29,430 | 68,39 | 57,23 |
| 1920x1080@60 | 28,504 | 61,73 | 61,73 |
| 1280x720@30 | 27,915 | 64,90 | 58,37 |
| 1600x900@30 | 27,445 | 62,64 | 59,70 |
| 960x540@30 | 27,223 | 67,13 | 55,97 |
| 1280x720@24 | 26,057 | 64,49 | 57,96 |
| 1920x1080@24 | 25,871 | 60,06 | 60,06 |
| 1600x900@24 | 25,770 | 62,23 | 59,29 |
| 960x540@24 | 25,498 | 66,73 | 55,56 |

Spearman: eski +0,261 (18/45), yeni +0,285 (18/45). İkisi de bu ızgarayı
sıralayamıyor; yeni model burada gerileme değil, ama iyileşme de değil.

Tek tek bakınca yeni modelin işareti yanlış: ölçüm `1280x720@60`'ı `1920x1080@60`'ın
1,12 puan önüne koyuyor, yeni model 1080p'yi 2,10 puan öne koyuyor.

### Ölçek bedelinin bit hızına bağlılığı

A kaynağında 1920x1080@60 ile 960x540@60'ı eşit toplam bit hızında karşılaştırınca:

| bit hızı | kazanan | fark |
|---|---|---|
| 400k | **960x540** | 3,04 |
| 800k | 1920x1080 | 1,41 |
| 1600k | 1920x1080 | 2,48 |
| 3200k | 1920x1080 | 3,70 |

Geçiş noktası 400k ile 800k arasında. B-durgunda geçiş yok, dört basamakta da yerel
kazanıyor (13,8 / 17,0 / 19,0 / 20,3).

Yalnız ölçeğin fonksiyonu olan bir ceza bunu ifade edemez. Aşırı rejimdeki ve donanım
kolundaki yanlış işaretin kaynağı budur: ikisinde de etkin bit hızı geçiş noktasının
altında kalıyor. Kapatılmadı, §12'de açık kusur olarak duruyor.

## 11. K7 — sabitlerin durumu

Her sayının yanında ne yapıldığı yazıyor. "Ölçüldü, değişmedi" demek: ölçüm bu sayıyı
oynatmak için bir gerekçe **üretmedi** — §7'deki uydurma denemeleri sınanmamış
kaynakta daha iyi çıkmadı.

| Sabit | yer | değer | durum |
|---|---|---|---|
| `ScalePenaltyScale` | `CodecModel.cs:12` | 10,0 | ölçüldü, değişmedi |
| `ScalePenaltyExponent` | `CodecModel.cs:13` | 1,1 | ölçülmedi |
| `FpsPenaltyPerHalving` | `CodecModel.cs:14` | 5,0 | ölçüldü, değişmedi |
| `PenaltyWeights(Aggressive).Scale` | `CompressionStrategy.cs:80` | 0,70 | ölçüldü, değişmedi |
| `PenaltyWeights(Aggressive).Fps` | `CompressionStrategy.cs:80` | 0,70 | ölçüldü, değişmedi |
| `PenaltyWeights(Aggressive).LowFpsSurcharge` | `CompressionStrategy.cs:80` | `true` | ölçülmedi |
| `PenaltyWeights(Extreme).Scale` | `CompressionStrategy.cs:81` | 0,45 | ölçülmedi |
| `PenaltyWeights(Extreme).Fps` | `CompressionStrategy.cs:81` | 0,35 | ölçülmedi |
| `PenaltyWeights(Extreme).LowFpsSurcharge` | `CompressionStrategy.cs:81` | `false` | ölçülmedi |
| `PenaltyWeights` varsayılan (Light/Balanced) | `CompressionStrategy.cs:82` | 1,0 / 1,0 / `true` | ölçülmedi |
| `LowFpsSurcharge` | `PlanCalculator.cs:66` | 12,0 | ölçülmedi |
| `LowFpsThreshold` | `PlanCalculator.cs:67` | 20,0 | ölçülmedi |
| `CalibratedShapeHysteresis` | — | 0,25 | ölçülmedi |

Aggressive ağırlıkları ölçekle çarpılıp kullanılıyor: etkin ölçek cezası katsayısı
10,0 × 0,70 = **7,00**, etkin fps cezası yarılama başına 5,0 × 0,70 = **3,50**.
§7'deki varyantlar bu iki etkin sayıyı 8,2477 ve 5,6809'a taşımayı denedi.

Aşırı rejim §10'da **yalnız skor karşılaştırması için** ölçüldü; `Extreme`
ağırlıklarının kendisi için ayrı bir uyum çalışması yapılmadı, o yüzden dört satırı
da "ölçülmedi".

**Bu sözleşmede hiçbir sabit değiştirilmedi.** Değişen tek şey `rate` teriminin
biçimidir. Telafi sabiti yok.

## 12. Açık kusurlar

1. **Ölçek bedeli bit hızına bağlı** (§10). Geçiş A kaynağında 400k–800k arasında.
   Bunun sonucu olarak yeni model aşırı rejimde ve donanım kolunda yanlış tarafta.
   Ölçeğin tek fonksiyonu olan bir ceza terimi bunu ifade edemez; çözüm ceza terimini
   bit hızına da bağlamaktır ve bu ayrı bir sözleşmedir.
2. **Ölçek bedeli içeriğe bağlı** (§5). Yarım ölçekte 1,410 (A) ile 16,988 (B)
   arasında, 12 kat. Sabit tablo bunu da taşıyamaz.
3. **`PerHalving` üç kaynakta da ölçülmedi.** `SlopeMeasured=False`, model 6
   varsayılanını kullanıyor; ölçülen değerler 1,60 / 4,733 / 13,018.
4. **Taban A kaynağında ölçüm en iyisini eliyor** (§8). 800 kbit/s'te yerel
   `1920x1080@60` için `tabanGecer=False`.
5. **`av1_nvenc` yerel çözünürlükte istenen bit hızını tutturamıyor** (§9):
   800k isteğine 729,0 kbps. Modelin `kullanilirK` tahmini de ölçümle uyuşmuyor
   (1080p60 için 1590k diyor, ölçüm 729,0 kbps teslim etti).
6. **`hevc_nvenc` taban borcu kapanmadı.** A'da 1920x1080@60, sekiz nokta,
   0,005016–0,037958 bppf: 28,186 / 29,259 / 42,601 / 52,145 / 59,157 / 63,439 /
   66,828 / 72,122 (p10 16,874 → 64,328). Ne 0,02196'da ne 0,02671'de diz var;
   iki aday taban da kalitenin çöktüğü yerin çok üstünde. NVENC 1080p60'ta
   ~624 kbps'in altına da inemiyor (500k isteğine 624,1 kbps).

## 13. K8 — mutasyon kanıtı

Üç koşumun her birinden **önce** `dotnet build VidShrink.sln -c Release --no-incremental`
çalıştırıldı; artımlı derleme mutasyonu taşımıyor.

| Mutasyon | sonuç |
|---|---|
| `rate` terimi geri alındı (`requiredAtSourceGrid = required; providedAtSourceGrid = provided;`) | **5 başarısız** / 30 başarılı / 3 atlandı |
| Aşırı düzeltme (`provided * scale * scale * scale * scale`) | **3 başarısız** / 32 başarılı / 3 atlandı |
| Geri alındı (teslim edilen hal) | **0 başarısız** / 35 başarılı / 3 atlandı |

Geri alma mutasyonunda düşen beş durum, bu sözleşmede eklenen beş durumun tamıdır:

```
PlanCalculatorTests.TheRateHalfOfTheScoreDoesNotMoveWhenOnlyTheResolutionChanges(0.08, 0.05)
PlanCalculatorTests.TheRateHalfOfTheScoreDoesNotMoveWhenOnlyTheResolutionChanges(0.08, 0.11)
PlanCalculatorTests.DroppingResolutionAtAFixedBitrateAlwaysCostsScore(0.08, 0.05)
PlanCalculatorTests.DroppingResolutionAtAFixedBitrateAlwaysCostsScore(0.08, 0.11)
PlanCalculatorTests.TheOnlyThingThatSeparatesTwoResolutionsIsTheScalePenalty
```

İki yön de kırmızı olduğu için testler tek bir sabiti değil, terimin **biçimini**
tutuyor. Testlerde elle yazılmış beklenen skor yok; üç test de aynı profilden çıkan
iki yerleşimi birbirine karşılaştırıyor. `DetailExponent` sıfır olsaydı ilk test
boş yere yeşil olurdu, o yüzden `Assert.NotEqual(0.0, complexity.DetailExponent, 3)`
ile önce onun sıfır olmadığı doğrulanıyor; iki `InlineData` satırı işareti de
ters çeviriyor (0,08/0,05 ve 0,08/0,11).

## 14. Atlanan testler

`tools/ci-gibi-kos.sh` `ffmpeg`'i PATH'ten çıkarır; `[FfmpegFact]` ile işaretli
testler orada **atlanır** ve yeşil görünmeleri hiçbir şey söylemez.

`bash tools/ci-gibi-kos.sh` (tam süit, ffmpeg PATH'te değil):

```
Başarısız: 3, Başarılı: 1032, Atlanan: 67, Toplam: 1102, Süre: 7 m 6 s
```

**Kabul kriteri ölçüleri o listede değil.** K8'in beş durumu
(`TheRateHalfOfTheScoreDoesNotMoveWhenOnlyTheResolutionChanges` ×2,
`DroppingResolutionAtAFixedBitrateAlwaysCostsScore` ×2,
`TheOnlyThingThatSeparatesTwoResolutionsIsTheScalePenalty`) ffmpeg'siz koşumda
atlanmadı, yeşil geçti. Üçü de `ComplexityProfile.FromProbe` ile kurulan profil
üzerinden çalışıyor, kodlayıcıya hiç gitmiyor.

Atlanan 67 testin tamamı canlı ffmpeg isteyen ölçüler:
`QualityMeterTests` (13), `PerformanceCheckTests` (6), `SegmentEncoderTests` (6),
`FpsDropTests` (5), `ComplexityProbeTests` (6), `EncodeRunnerTests` (4),
`SceneMapTests` (4), `CalibrationProbeTests` (3), `ExtremeCompressionTests` (3),
`UpdaterTests` (3), `VmafPoolingTests` (3), `HardwareVerdictTests` (2),
`HardwareRateControlTests` (2), `PlaybackFrameSourceTests` (2),
`FfmpegArgumentsTests` (2), `FillBandTests` (1), `HardwareFlagTests` (1).

## 15. `owns` dışında kalan üç kırmızı

Değişiklik üç ölçüyü düşürüyor ve üçü de bu sözleşmenin `owns` listesinde **değil**.
Dosyalara dokunulmadı.

| Ölçü | dosya | ne diyor |
|---|---|---|
| `FillBandTests.FillTargetReachesTheBandWhenTheCeilingWouldLeaveItUnfilled` | `tests/VidShrink.Tests/FillBandTests.cs:97` | "This fixture is expected to hit the transparency ceiling below the band (118,3 MB < 116,6 MB); adjust the fixture if the model changes." |
| `QualityTargetUiTests.SinirDurumuEkrandaYazili` | `tests/VidShrink.Tests/QualityTargetUiTests.cs:167` | "Taban sinirinda satir gorunmuyor." Düzenek artık tabana dayanan bir plan üretmiyor. |
| `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched` | `tests/VidShrink.Tests/SpeedModeTests.cs:346` | Elle yazılmış plan dizisi anlık görüntüsü; skor değişince plan da değişiyor. |

Üçünün de eski modelde yeşil olduğunu ölçtüm: `rate` terimi geri alınmış halde
`dotnet test -c Release --no-build --filter "FillBandTests|QualityTargetUiTests|SpeedModeTests"`
→ **0 başarısız / 52 başarılı / 1 atlandı**. Yeni modelde aynı filtre → 3 başarısız.
Yani üçü de bu değişikliğin sonucudur, önceden kırmızı değillerdi.

Üçü de beklenen değeri eski modele çivilenmiş düzeneklerdir; hiçbiri yeni modelin
bozuk olduğunu söylemiyor. Ama düzeltmek `owns` dışına yazmayı gerektiriyor,
o yüzden sözleşme `blocked` teslim ediliyor.
