# Yerleşim skoru ölçülen kaliteye karşı

T107. **Ölçülen commit: `9fa1cda`** (`9fa1cdacbf1627cc07c5943a388ad1ae59167a3d`).
Bu belgedeki bütün skor ve kalite sayıları o ağaçtan derlenmiş ikiliyle üretildi.
Değişiklikten önceki ("eski model") sütunları `4a6377d`'nin skor modelidir.
Tur 2'nin ölçümleri (kol ayrımı, dört kırmızının kapanışı, iki yönlü mutasyon)
**`0d13a48`** ağacından; §9.1, §13.1, §14.1 ve §15.1 o ikiliyle üretildi.

> **Tur 2 (`0d13a48`) uyarısı — bu belgedeki "yeni model" sütunu her yerde
> teslim edilen model değildir.** T0 kararıyla yeni `rate` terimi **yalnız yazılım
> kodlayıcısında** geçerli; donanım kolunda eski davranış duruyor (§6.1). Yazılım
> kolunu ölçen bölümlerde (§3, §4, §7, §8, §10) "yeni model" sütunu teslim edilen
> modeldir. **Donanım kolunu ölçen §9'da değildir:** orada teslim edilen model
> "eski skor" sütunudur ve bu ayrıca ölçüldü (§9.1).

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

Ters çiftlerin **tamamı** aşağıda. Sol sütun modelin öne koyduğu yerleşim, sağ sütun
ölçümün öne koyduğu. **Skor farkı** soldakinin skor üstünlüğü, **ölçüm farkı**
sağdakinin ölçülen VMAF üstünlüğü; ikisi de artı olduğu için çift terstir.
Sıralama ölçüm farkının büyüklüğüne göre.

**A-hareketli, 29/78.** 29 çiftin **hepsi** çözünürlük değiştiren çifttir; aynı
çözünürlük içinde yalnız kare hızını değiştiren tek bir ters çift yok.

| model önde diyor | ölçüm önde diyor | skor farkı | ölçüm farkı |
|---|---|---|---|
| `960x540@24` | `1920x1080@60` | +0,19 | +11,850 |
| `960x540@30` | `1920x1080@60` | +1,16 | +8,087 |
| `1280x720@30` | `1920x1080@60` | +0,26 | +7,034 |
| `960x540@24` | `1920x1080@30` | +3,20 | +5,535 |
| `960x540@24` | `1600x900@30` | +1,45 | +4,848 |
| `1280x720@24` | `1920x1080@30` | +2,30 | +4,677 |
| `1600x900@24` | `1920x1080@30` | +0,78 | +4,354 |
| `1280x720@24` | `1600x900@30` | +0,55 | +3,990 |
| `882x496@60` | `1920x1080@60` | +4,23 | +1,805 |
| `960x540@30` | `1920x1080@30` | +4,17 | +1,772 |
| `960x540@24` | `1920x1080@24` | +4,17 | +1,659 |
| `882x496@60` | `1280x720@60` | +0,96 | +1,490 |
| `882x496@60` | `1600x900@60` | +2,48 | +1,411 |
| `960x540@60` | `1920x1080@60` | +4,17 | +1,410 |
| `960x540@24` | `1600x900@24` | +2,42 | +1,181 |
| `960x540@60` | `1280x720@60` | +0,90 | +1,095 |
| `960x540@30` | `1600x900@30` | +2,42 | +1,085 |
| `960x540@30` | `1280x720@30` | +0,90 | +1,053 |
| `960x540@60` | `1600x900@60` | +2,42 | +1,016 |
| `960x540@24` | `1280x720@24` | +0,90 | +0,858 |
| `1280x720@24` | `1920x1080@24` | +3,27 | +0,801 |
| `1280x720@30` | `1920x1080@30` | +3,27 | +0,719 |
| `1600x900@30` | `1920x1080@30` | +1,75 | +0,687 |
| `1600x900@24` | `1920x1080@24` | +1,74 | +0,478 |
| `882x496@60` | `960x540@60` | +0,06 | +0,395 |
| `1600x900@60` | `1920x1080@60` | +1,75 | +0,394 |
| `1280x720@24` | `1600x900@24` | +1,52 | +0,323 |
| `1280x720@60` | `1920x1080@60` | +3,27 | +0,315 |
| `1280x720@30` | `1600x900@30` | +1,52 | +0,032 |

**B-durgun, 57/78.** Model neredeyse tam tersini söylüyor.

| model önde diyor | ölçüm önde diyor | skor farkı | ölçüm farkı |
|---|---|---|---|
| `960x540@24` | `1920x1080@60` | +4,43 | +20,009 |
| `960x540@30` | `1920x1080@60` | +5,29 | +19,494 |
| `960x540@24` | `1920x1080@30` | +7,12 | +17,004 |
| `960x540@60` | `1920x1080@60` | +7,98 | +16,988 |
| `960x540@30` | `1920x1080@30` | +7,98 | +16,489 |
| `960x540@24` | `1920x1080@24` | +7,98 | +16,350 |
| `882x496@60` | `1920x1080@60` | +8,18 | +15,903 |
| `960x540@30` | `1920x1080@24` | +8,84 | +15,835 |
| `960x540@24` | `1600x900@60` | +1,68 | +14,487 |
| `960x540@60` | `1920x1080@30` | +10,67 | +13,983 |
| `960x540@30` | `1600x900@60` | +2,55 | +13,972 |
| `960x540@60` | `1920x1080@24` | +11,53 | +13,329 |
| `882x496@60` | `1920x1080@30` | +10,87 | +12,898 |
| `882x496@60` | `1920x1080@24` | +11,73 | +12,244 |
| `960x540@24` | `1600x900@30` | +4,37 | +11,552 |
| `960x540@60` | `1600x900@60` | +5,23 | +11,466 |
| `960x540@30` | `1600x900@30` | +5,23 | +11,037 |
| `960x540@24` | `1600x900@24` | +5,23 | +10,976 |
| `1280x720@24` | `1920x1080@60` | +1,95 | +10,607 |
| `960x540@30` | `1600x900@24` | +6,10 | +10,461 |
| `882x496@60` | `1600x900@60` | +5,43 | +10,381 |
| `1280x720@30` | `1920x1080@60` | +2,81 | +10,124 |
| `960x540@24` | `1280x720@30` | +1,62 | +9,885 |
| `960x540@60` | `1280x720@60` | +2,48 | +9,603 |
| `960x540@24` | `1280x720@24` | +2,48 | +9,402 |
| `960x540@30` | `1280x720@30` | +2,48 | +9,370 |
| `960x540@30` | `1280x720@24` | +3,35 | +8,887 |
| `960x540@60` | `1600x900@30` | +7,92 | +8,531 |
| `882x496@60` | `1280x720@60` | +2,68 | +8,518 |
| `1600x900@30` | `1920x1080@60` | +0,06 | +8,457 |
| `960x540@60` | `1600x900@24` | +8,78 | +7,955 |
| `1280x720@24` | `1920x1080@30` | +4,63 | +7,602 |
| `882x496@60` | `1600x900@30` | +8,12 | +7,446 |
| `1280x720@60` | `1920x1080@60` | +5,50 | +7,385 |
| `1280x720@30` | `1920x1080@30` | +5,50 | +7,119 |
| `1280x720@24` | `1920x1080@24` | +5,50 | +6,948 |
| `882x496@60` | `1600x900@24` | +8,98 | +6,870 |
| `960x540@60` | `1280x720@30` | +5,17 | +6,864 |
| `1280x720@30` | `1920x1080@24` | +6,36 | +6,465 |
| `960x540@60` | `1280x720@24` | +6,03 | +6,381 |
| `1600x900@24` | `1920x1080@30` | +1,88 | +6,028 |
| `882x496@60` | `1280x720@30` | +5,37 | +5,779 |
| `1600x900@60` | `1920x1080@60` | +2,75 | +5,522 |
| `1600x900@30` | `1920x1080@30` | +2,75 | +5,452 |
| `1600x900@24` | `1920x1080@24` | +2,75 | +5,374 |
| `882x496@60` | `1280x720@24` | +6,24 | +5,296 |
| `1600x900@30` | `1920x1080@24` | +3,61 | +4,798 |
| `1280x720@30` | `1600x900@60` | +0,06 | +4,602 |
| `1280x720@60` | `1920x1080@30` | +8,18 | +4,380 |
| `1280x720@60` | `1920x1080@24` | +9,05 | +3,726 |
| `1600x900@60` | `1920x1080@30` | +5,44 | +2,517 |
| `1280x720@24` | `1600x900@30` | +1,88 | +2,150 |
| `1280x720@60` | `1600x900@60` | +2,75 | +1,863 |
| `1600x900@60` | `1920x1080@24` | +6,30 | +1,863 |
| `1280x720@30` | `1600x900@30` | +2,75 | +1,667 |
| `1280x720@24` | `1600x900@24` | +2,75 | +1,574 |
| `1280x720@30` | `1600x900@24` | +3,61 | +1,091 |

**C-ucuncu, 18/28.**

| model önde diyor | ölçüm önde diyor | skor farkı | ölçüm farkı |
|---|---|---|---|
| `960x540@24` | `1920x1080@30` | +10,19 | +20,291 |
| `1280x720@24` | `1920x1080@30` | +6,76 | +17,854 |
| `960x540@24` | `1600x900@30` | +6,84 | +16,035 |
| `1600x900@24` | `1920x1080@30` | +3,27 | +15,315 |
| `1280x720@24` | `1600x900@30` | +3,41 | +13,598 |
| `960x540@24` | `1280x720@30` | +3,35 | +13,165 |
| `960x540@30` | `1920x1080@30` | +10,27 | +9,552 |
| `960x540@24` | `1920x1080@24` | +10,27 | +8,841 |
| `1280x720@30` | `1920x1080@30` | +6,84 | +7,126 |
| `1280x720@24` | `1920x1080@24` | +6,84 | +6,404 |
| `960x540@30` | `1600x900@30` | +6,92 | +5,296 |
| `960x540@24` | `1600x900@24` | +6,92 | +4,976 |
| `1600x900@30` | `1920x1080@30` | +3,35 | +4,256 |
| `1600x900@24` | `1920x1080@24` | +3,35 | +3,865 |
| `1280x720@30` | `1600x900@30` | +3,49 | +2,870 |
| `1280x720@24` | `1600x900@24` | +3,49 | +2,539 |
| `960x540@24` | `1280x720@24` | +3,44 | +2,437 |
| `960x540@30` | `1280x720@30` | +3,44 | +2,426 |

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
var onSourceGrid = !CodecModel.IsHardware(codec);
var rateRequired = onSourceGrid ? required / Math.Max(complexity.ScaleFactor(scale), 1e-9) : required;
var rateProvided = onSourceGrid ? provided * scale * scale : provided;
var rate = level.AtReference - level.PerHalving * Math.Log2(
    Math.Max(rateRequired, 1e-9) / Math.Max(rateProvided, 1e-9));
```

Her iki taraf da ölçeğe göre aynı şekilde normalize edildiğinden `rate` ölçekten
bağımsız hale geliyor: A kaynağında 60 fps'lik beş yerleşimin dördünde 68,880,
beşincisinde (882x496) 68,878 — fark aday ölçeğinin 0,02'lik adıma yuvarlanmasından,
`rate`'in ölçeğe bağlılığından değil. Çözünürlüğü ayıran tek şey artık `ScalePenalty`.

**Hiçbir sabit değiştirilmedi.** Ne `ScalePenaltyScale`, ne `FpsPenaltyPerHalving`,
ne `PenaltyWeights`. Gerekçe: uydurulmuş cezalar sınanmamış kaynakta daha iyi değil
(§7'deki varyant taraması). Telafi sabiti de eklenmedi.

### 6.1 Tur 2 — kol ayrımı

Tur 1 ölçümü iki yönü birden söylüyordu ve ikisi de gerçek: yazılım kolunda asıl
şikâyet kapanıyor (§4), donanım kolunda aynı büyüklükte gerileme üretiliyor (§9).
T0 kararı: **yeni terim yalnız yazılım kodlayıcısında geçerli olsun.**

Kolu ayıran koşul kodda zaten vardı — `CodecModel.IsHardware(codec)`
(`src/VidShrink.Core/CodecModel.cs:134`, `Vendor(codec) != EncoderVendor.Software`).
Yeni bayrak eklenmedi, telafi sabiti eklenmedi, hiçbir sabit değişmedi.

Gerekçe **ölçülmüş iki farklı rejimdir, tercih değil**: `av1_nvenc` 1920x1080@60'ta
800 kbit/s isteğine 729,0 kbps teslim ediyor ve 500k isteğine 624,1 kbps — yani
yerel çözünürlükte istenen hızın altına inemiyor. O kolda küçültmek gerçekten
kazanıyor (540p 40,036 > 1080p 31,842 VMAF-NEG) ve eski model bunu **kazara**
tutturuyordu: 1080p'yi aşırı cezalandırdığı için doğru sıraya düşüyordu. Sebep
kodlayıcının bit hızı tabanı; ölçek-kalite ilişkisi değil.

Ayrımın iki yönlü mutasyon kanıtı §13.1'de.

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

Ters çift: eski model **1/10** (yalnız `960x540`–`882x496`), yeni model **9/10**.
Donanım kolunda küçültme gerçekten kazanıyor ve eski modelin ölçek kredisi
tam da bunu söylüyordu.

Sebep okunabilir: `av1_nvenc` 1920x1080@60'ta 800 kbit/s isteğine karşı 729,0 kbps
teslim ediyor — diğer dört yerleşim 854–873 bandında. Donanım kodlayıcı yerel
çözünürlükte istenen hızı tutturamıyor, kalite de oradan düşüyor. Bu bir kodlayıcı
davranışı; ölçek-kalite ilişkisi değil.

1920x1080@60 satırı eşit boyutta değil (%15 düşük), o yüzden karşılaştırmada
sayılmamalıdır. Kalan dört yerleşimde eski model **1/6**, yeni model **5/6** ters —
1080p satırını atmak sonucu değiştirmiyor.

Model tarafında `kullanilirK` sütunu 1590k (1080p60) ve 1104k (900p60) diyor, yani
taban bu iki yerleşimi 800 kbit/s'te zaten eliyor (`tabanGecer=False`). Ölçüm ikisini
de teslim edebildi; tahmin ile gerçek arasındaki bu fark ayrı bir kusur, kapsam dışı.

### 9.1 Tur 2 — bu kolda teslim edilen model "eski skor" sütunudur

Yukarıdaki "yeni skor" sütunu **reddedilen** varyanttır. Teslim edilen model
donanım kolunda eski terimi kullanıyor (§6.1) ve bunu iddia etmekle bırakmadım,
aynı ızgarayı teslim edilen ikiliyle yeniden koşturdum:

```
dotnet run --project tools/yerlesim-skoru/Skor.csproj -c Release --no-build --   .calisma/T107/kaynak/A-hareketli.mkv 2.0 av1_nvenc   .calisma/T107/yerlesimler-hw.txt --videok 800
```

Sonuç 68,127 / 69,873 / 71,394 / 72,294 / 72,355 — "eski skor" sütununun beş
sayısıyla **birebir aynı**. Yani teslim edilen modelin bu koldaki ters çift oranı
**1/10** (eşit boyut olmayan 1080p satırı atılınca **1/6**), 9/10 değil.

## 10. Aşırı rejim (350 kbit/s)

Kodlayıcı `libsvtav1`, yani bu bölüm **yazılım kolunu** ölçer; "yeni skor" sütunu
teslim edilen modeldir (§6.1).

A-hareketli, `libsvtav1`, 12 yerleşim. İki satır dışlandı: `1920x1080@30` 338,6 kbps
ve `1600x900@60` 298,0 kbps teslim etti (istek 350k, −%15), eşit boyut değiller.
Kalan n=10. Ölçülen sütun `.calisma/T107/gunluk/asiri-A.tsv`, skor sütunları

```
dotnet run --project tools/yerlesim-skoru/Skor.csproj -c Release --no-build --   .calisma/T107/kaynak/A-hareketli.mkv 0.4 libsvtav1   .calisma/T107/yerlesimler-asiri.txt --videok 350
```

teslim edilen ikilide ve `rate` terimi geri alınmış ikilide (her ikisinden önce
`--no-incremental` derleme) ayrı ayrı koşuldu:

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
   Bunun sonucu olarak yeni model yazılım kolunda da aşırı rejimde yanlış tarafta:
   350 kbit/s'te ölçüm `1280x720@60`'ı `1920x1080@60`'ın 1,12 puan önüne koyuyor,
   yeni model 1080p'yi 2,10 puan öne koyuyor. Ölçeğin tek fonksiyonu olan bir ceza
   terimi bunu ifade edemez; çözüm ceza terimini bit hızına da bağlamaktır.
   **Bu sözleşmede kapanmadı, ayrı sözleşme açılacak** (T0 kararı, madde 5).
   Donanım kolundaki yanlış taraf tur 2'de düzeltilmedi, **atlatıldı**: o kol eski
   terimde bırakıldı (§6.1). Eski terim orada kazara doğru; bit hızı bağlılığı
   kapandığında bu ayrımın hâlâ gerekli olup olmadığı yeniden ölçülmelidir.
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

### 13.1 Tur 2 — kol ayrımının iki yönlü mutasyonu

Ayrımı tutan ölçü `PlanCalculatorTests.TheHardwareArmKeepsTheScaleCreditTheSoftwareArmGaveUp`
(iki `InlineData`): aynı profille dört basamaklı bir merdiven iki kodlayıcıda
kuruluyor, `av1_nvenc`'te skorun küçüldükçe **artması**, `libsvtav1`'de **azalması**
bekleniyor. Elle yazılmış skor yok; iki kol birbirine karşılaştırılıyor.

Her koşumdan önce `dotnet build VidShrink.sln -c Release --no-incremental`.
Filtre `PlanCalculatorTests|ExtremeCompressionTests|FillBandTests|SpeedModeTests`
(85 ölçü).

| Mutasyon | sonuç | düşen ölçüler |
|---|---|---|
| Koşul kaldırıldı: `onSourceGrid = true` (iki kol da yeni) | **2 başarısız** / 79 başarılı / 4 atlandı | `TheHardwareArmKeepsTheScaleCreditTheSoftwareArmGaveUp` ×2 |
| Koşul ters çevrildi: `onSourceGrid = false` (iki kol da eski) | **8 başarısız** / 73 başarılı / 4 atlandı | yazılım kolunun beş durumu (§13) + `TheHardwareArmKeepsTheScaleCreditTheSoftwareArmGaveUp` ×2 + `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched` |
| Teslim edilen hal (`onSourceGrid = !CodecModel.IsHardware(codec)`) | **0 başarısız** / 81 başarılı / 4 atlandı | — |

Kol ölçüsü iki yönde de kırmızı, çünkü tek başına iki kolu birden iddia ediyor;
kaldırma yönünde **yalnız** o düşüyor, ters çevirme yönünde yazılım kolunun beş
ölçüsü de düşüyor. Yani koşulun hem varlığı hem yönü pimli.

## 14. Atlanan testler

`tools/ci-gibi-kos.sh` `ffmpeg`'i PATH'ten çıkarır; `[FfmpegFact]` ile işaretli
testler orada **atlanır** ve yeşil görünmeleri hiçbir şey söylemez.

**Yereldeki koşum geçerli değil, sayılar CI'dan.** `bash tools/ci-gibi-kos.sh`
koyduğum 540 sn'lik sınıra takıldı; günlüğün sonunda
`Etkin test çalıştırması iptal edildi. Nedeni: Test ana işlemi kilitlendi` yazıyor.
Bastığı `3 başarısız / 1032 başarılı / 67 atlandı / 1102 toplam` özeti **kısmi bir
koşumun** özeti; CI'ın 1183'ünü tutmuyor ve
`QualityTargetTests.SearchCostIsBoundedAndCounted` o koşumda hiç çalışmamış
(o ölçü sonradan tek başına, iki modelde ayrı ayrı koşuldu — §15).
Aşağıdaki sayılar CI koşumu **33592420948**'dendir (headSha `ab70c85`):

```
Failed: 4, Passed: 1074, Skipped: 105, Total: 1183, Duration: 14 m 9 s
```

Aynı sayılar yalnız `docs/` değiştiren `2a42bf0` başında koşum **33595322110**'da
birebir tekrarlandı (`Failed 4 / Passed 1074 / Skipped 105 / Total 1183`, 12 dk 38 sn);
süre farkı koşucunun, sayılar değil.

**Kabul kriteri ölçüleri atlanan listesinde değil.** K8'in beş durumu
(`TheRateHalfOfTheScoreDoesNotMoveWhenOnlyTheResolutionChanges` ×2,
`DroppingResolutionAtAFixedBitrateAlwaysCostsScore` ×2,
`TheOnlyThingThatSeparatesTwoResolutionsIsTheScalePenalty`) CI'ın 105 atlananının
hiçbiri değil; üçü de `ComplexityProfile.FromProbe` ile kurulan profil üzerinden
çalışıyor, kodlayıcıya hiç gitmiyor.

### 14.1 Tur 2 — yerel ci-gibi koşumu tamamlandı

Tur 1'de bu koşum 540 sn sınırına takılmıştı; tur 2'de sınır kaldırılıp tam koşuldu
(`bash tools/ci-gibi-kos.sh`, günlük `.calisma/T107/ci-gibi-tur2.log`):

```
Başarısız: 0, Başarılı: 1080, Atlanan: 105, Toplam: 1185, Süre: 15 m 6 s
```

CI aynı sayıları verdi — koşum **33601165873**, headSha `0d13a48`, **success**:

```
Passed!  - Failed: 0, Passed: 1080, Skipped: 105, Total: 1185, Duration: 16 m 57 s
```

Yani tur 1'in dört kırmızısı CI'da da sıfıra indi; açıklanacak artık kalmadı.

(Süre makine paylaşımlıydı; sayılar değil.) Tur 1'in dört kırmızısı bu koşumda yok.
Toplam 1183'ten 1185'e çıktı: tur 2'de eklenen iki ölçü
`TheHardwareArmKeepsTheScaleCreditTheSoftwareArmGaveUp`'ın iki `InlineData` durumu.

**Kabul kriteri ölçülerinin hiçbiri atlananlar arasında değil.** Günlükten çıkarılan
atlanan listesinde `PlanCalculatorTests`ten tek bir ad yok; `owns`a tur 2'de eklenen
dört dosyadan atlanan yalnız `FillBandTests.LiveFillTargetRunStaysInsideTheBand` ve
`QualityTargetTests.MonotonicityOnRealSourcesIsMeasuredNotAssumed` — ikisi de
`[FfmpegFact]`, bu turda düzeltilen ölçüler değil. Düzeltilen dördü
(`FillTargetReachesTheBandWhenTheCeilingWouldLeaveItUnfilled`,
`SearchCostIsBoundedAndCounted`, `SinirDurumuEkrandaYazili`,
`QualityModeLeavesTodaysPlansUntouched`) ffmpeg'siz ortamda gerçekten koştu.

CI'da atlanan 105 ölçünün sınıf dağılımı:

```
FrameGrabberTests 22   QualityMeterTests 13   PanelHostTests 11
ComplexityProbeTests 7  SegmentEncoderTests 6  PerformanceCheckTests 6
FpsDropTests 5          SceneMapTests 4        PreviewSyncTests 4
EncodeRunnerTests 4     VmafPoolingTests 3     UpdaterTests 3
ExtremeCompressionTests 3  CalibrationProbeTests 3
PlaybackFrameSourceTests 2  HardwareVerdictTests 2
HardwareRateControlTests 2  FfmpegArgumentsTests 2
QualityTargetTests 1    HardwareFlagTests 1    FillBandTests 1
```

## 15. Dört kırmızı

**Tur 1'de** değişiklik dört ölçüyü düşürüyordu ve dördü de o turun `owns`
listesinde değildi; dosyalara dokunulmadı ve CI bu yüzden kırmızıydı
(koşum 33592420948). **Tur 2'de** T0 dördünü `owns`a ekledi ve dördü de kapatıldı
(§15.1). Aşağıdaki tablo tur 1'in tespitidir.

| Ölçü | dosya | ne diyor |
|---|---|---|
| `FillBandTests.FillTargetReachesTheBandWhenTheCeilingWouldLeaveItUnfilled` | `tests/VidShrink.Tests/FillBandTests.cs:97` | "This fixture is expected to hit the transparency ceiling below the band (118,3 MB < 116,6 MB); adjust the fixture if the model changes." |
| `QualityTargetUiTests.SinirDurumuEkrandaYazili` | `tests/VidShrink.Tests/QualityTargetUiTests.cs:167` | "Taban sinirinda satir gorunmuyor." Düzenek artık tabana dayanan bir plan üretmiyor. |
| `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched` | `tests/VidShrink.Tests/SpeedModeTests.cs:346` | Elle yazılmış plan dizisi anlık görüntüsü; skor değişince plan da değişiyor. |
| `QualityTargetTests.SearchCostIsBoundedAndCounted` | `tests/VidShrink.Tests/QualityTargetTests.cs:313` | `KeyNotFoundException: 'BelowFloor'` — sayaç sözlüğünde artık taban altına düşen plan yok. |

İlk üçünün eski modelde yeşil olduğunu ölçtüm: `rate` terimi geri alınmış halde
`dotnet test -c Release --no-build --filter "FillBandTests|QualityTargetUiTests|SpeedModeTests"`
→ **0 başarısız / 52 başarılı / 1 atlandı**; yeni modelde aynı filtre 3 başarısız.
Dördüncüsünü (`SearchCostIsBoundedAndCounted`) ayrıca ölçtüm: eski modelde
`--filter "FullyQualifiedName~QualityTargetTests.SearchCostIsBoundedAndCounted"`
→ **0 başarısız / 1 başarılı**, 1 dk 23 sn; yeni modelde aynı filtre
**1 başarısız**, 2 dk 51 sn. Hata `BelowFloor` anahtarının bulunamaması, yani o da
eski davranışa çivilenmiş. (Süreler makine paylaşımlıydı; sıralama değil, sadece
kırmızı/yeşil bilgisi ölçüdür.)

Dördü de beklenen değeri eski modele çivilenmiş düzeneklerdir; hiçbiri yeni modelin
bozuk olduğunu söylemiyor.

### 15.1 Tur 2 — dördünün kapanışı

Kural: **beklenen sayıyı yenisiyle değiştirmek yetmez.** Her birinin yanına o
sayının nereden geldiği yazıldı; hiçbiri elle uydurulmadı.

**1. `FillBandTests.FillTargetReachesTheBandWhenTheCeilingWouldLeaveItUnfilled`.**
Düzeneğin *önkoşulu* yok olmuştu, beklentisi değil: profil ölçülmemiş
(`Measured=false`) kurulduğu için yeni yazılım modelinde "tavan bandın altında"
rejimi hiç oluşmuyordu. 7 kaynak bit hızı × 6 hedef taraması yapıldı
(`.calisma/T107/fillband-tarama.tsv`, `fillband-tarama2.tsv`); rejime giren her
satır önemsiz bir `PassThrough` çıktı. Düzenek **ölçülmüş** şikâyet profiline
yeniden temellendirildi (`ReferenceBppf = 0,06244`, kaynağı
`docs/olcumler/bppf-tabani.md:125`; `MotionExponent = 1,163`). O profille tavan
80,8 MB, bandın altı 116,6 MB, dolgu 118,3 MB'a ulaşıyor ve karar
`FillTwoPassBandTooNarrowForCrf`. Yeni sayılar taramadan, testin docstring'inde
kayıtlı.

**2. `QualityTargetTests.SearchCostIsBoundedAndCounted` (`:313`).**
Bu, ötekilerin aksine bir anlık görüntü değil: hata `KeyNotFoundException 'BelowFloor'`,
yani süpürme artık **hiç** taban-altı plan üretmiyor ve sözlükte anahtar yok.
Sebep, süpürmenin elle yazılmış 20,0 alt sınırı: taban planının kalitesi ölçüldü
(`.calisma/T107/taban-kalitesi.tsv`) — sample.mp4 8,08; phone.mp4 −3,511;
capture.mkv 13,358 (Sharing) ve 12,431 (Archive). Dördü de 20'nin altında, yani
süpürme taban rejimine hiç girmiyordu. Alt sınır artık kaynağa/niyete göre
**türetiliyor**: `QualityAt(info, options, PlanCalculator.QualityFloorTargetMb(info))`
bir puan altından başlanıyor. Ayrıca sözlük araması yapılmadan önce
`Assert.True(worstByBound.ContainsKey(QualityTargetBound.BelowFloor), ...)` eklendi;
rejim ölçülmediyse test artık `KeyNotFoundException` değil, sebebi yazan bir
kırmızı veriyor. Arama maliyeti yeniden ölçüldü: en kötü durum **1315 çağrı**
(sample.mp4, Sharing, istek 94) — tur 1 ile aynı, sınır 1400 değişmedi.

**3. `QualityTargetUiTests.SinirDurumuEkrandaYazili`.**
Düzeneğin 4K/60 `Sample()`'ı yeni modelde `BelowFloor`a artık ulaşamıyor: varsayılan
`PlanOptions` ile taban planının kalitesi −3,989, arayüz kalite 1'de bile boş uyarı
gösteriyor. Yeni düzenek `FloorBoundSample()` **ölçülerek** seçildi (2560x1440@30,
h264 5,1 Mbps, 3600 sn, 2200 MB): arayüzde taban 20,7 MB → 40,5 puan, tavan
2090 MB → 94,1 puan. Docstring'e arayüzden okunan bu sayılar yazıldı. **Dikkat:**
aynı kaynağın varsayılan `PlanOptions` ile taban kalitesi 13,358'dir; pencere kendi
seçeneklerini kuruyor, iki sayı farklı yollardan gelir ve birbirine karıştırılmamalı.

**4. `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched`.**
Gerçek bir anlık görüntü; 18 altın satırın tamamı düzenekten yeniden üretildi
(`.calisma/T107/hizkipi-liste.txt`), elle düzenlenmedi. Farkın yönü: **18 satırın
18'inde çözünürlük yükseldi**; 180 MB `QualityCeiling` satırlarının üçü crf→2pass
geçti; kodek ve kare hızı 18 satırın hiçbirinde değişmedi. Docstring'e listenin
hangi komutla üretildiği ve bu üç gözlem yazıldı.

Yeniden ölçüm sonrası yerel:
`--filter "QualityTargetUiTests|FillBandTests|SpeedModeTests|PlanCalculatorTests|ExtremeCompressionTests"`
→ **0 başarısız / 89 başarılı / 4 atlandı**; `--filter "QualityTargetTests"`
→ **0 başarısız / 11 başarılı** (4 dk 39 sn; süre makine paylaşımlıydı).

### 15.2 Tur 2'de çıkan, kapsam dışı gözlem

Yukarıdaki 1. maddenin taraması bir yan bulgu verdi: **ölçülmemiş** profille
1920x1080@30 düzenek ailesinde yeni yazılım modelinde tavan-bandın-altında rejimi
tamamen kayboluyor — taranan 42 kombinasyonun hiçbirinde önemsiz olmayan bir örnek
yok. Bu tek başına bir kusur olabilir de olmayabilir de; **ölçülmedi**, yalnız
gözlendi. Bu sözleşmenin kapsamı dışında.
