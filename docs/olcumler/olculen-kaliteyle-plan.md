# T89 — plan ölçülen kaliteyi kullandığında

Dal: `T89-olculen-kaliteyle-plan`, worktree `.claude/worktrees/agent-a57b767c70808d865`.
Ölçüm makinesi: Windows 11, RTX (NVENC mevcut), ffmpeg n7.x, libvmaf `vmaf_v0.6.1neg`.

Rapordaki her sayı `tools/VidShrink.Bench` çıktısındandır. Ölçülmeyen yere
**ölçülmedi** yazılmıştır; hiçbir sayı tahmin edilmemiştir.

Bu turda eklenen sonda alt komutları:

```
bench container-unit <kaynak,...>   # Matroska ek yükü ve hareket üsteli dağılımı
bench search-cost [--runs 5]        # kalite aramasının yineleme ve duvar saati maliyeti
bench peak-curve <kaynak> ...       # tepe çarpanı süpürmesi (K5)
bench shrink <kaynak> <MB> --measured-quality   # yeni plan kolu
```

---

## 1. Kazanç ölçümü (K4) ve süre (K6)

Üç kaynak; her biri elde bulunan uzun kayıtlardan kesilmiş bir parça. Parçalar
`.calisma/` altında tutuldu ve iş bitince silindi — aşağıdaki özellikler onları
tanımlar:

| kısa ad | kaynak | çözünürlük | kodek | süre |
| --- | --- | --- | --- | ---: |
| klip | 1080p60 SDR | 1920x1080@60 | hevc | 24,8 MB |
| oyun | 1080p48 oyun kaydı | 1920x1080@48 | av1 | 78,3 MB |
| hdr | 1080p60 HDR (smpte2084 / bt2020) | 1920x1080@60 | hevc | 77,7 MB |

**Eski plan** = `bench shrink` (kalite ölçümü yok, sabitler kullanılır).
**Yeni plan** = aynı komut `--measured-quality` ile (T88'in ölçtüğü VMAF-NEG
noktaları planlayıcıya girer).

> **KİLİTSİZ ÖLÇER — GEÇERSİZ (T116, 2026-09-02).** Aşağıdaki iki tablonun
> **bütün** sütunları kare kilidinden önceki ölçerden geçti: hem `--measured-quality`
> kolunu besleyen `QualityMeter` (kilit `822dd3a`, 04:44) hem de bu tablonun
> VMAF sütunlarını üreten `bench`in kendi grafiği (kilit `0e2b071`, 03:09) o gün
> kilitsizdi. Kilitsiz ölçer kareleri zaman damgasıyla eşliyordu; T110 aynı çift
> üzerinde 12,13 puanlık hata ölçtü (`algi-olcusu.md` §9).
>
> **Tablolar silinmedi, geçersiz sayıldı.** Aynı kaynaklarla yeniden ölçmek de
> mümkün değil: üç kaynak da o turda silindi (yukarıdaki satır). Yeniden ölçüm
> **ikame kaynaklarla** yapıldı ve **§11'de ayrı tablolarda** duruyor.
> **Bu iki tablo ile §11'in tabloları aynı kaynakları ölçmüyor, satırları
> karşılaştırılamaz.**

| kaynak | hedef | kol | yerleşim | kip | teslim MB | mean | harm | p10 | kodlama sn | deneme |
| --- | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| klip | 8 MB | eski | 1266x712@60 | 2pass 1188k | 7,824 | 85,79 | 85,62 | 81,40 | 10,6 | 1 |
| klip | 8 MB | yeni | 1266x712@60 | 2pass 1188k | 7,815 | 85,81 | 85,64 | 81,37 | 10,7 | 1 |
| klip | 20 MB | eski | 1920x1080@60 | 2pass 3214k | 19,743 | 95,29 | 95,27 | 93,54 | 19,5 | 1 |
| klip | 20 MB | yeni | 1920x1080@60 | crf 20 | 19,465 | 95,26 | 95,24 | 93,47 | 57,2 | 3 |
| oyun | 40 MB | eski | 1266x712@48 | 2pass 5297k | 38,775 | 73,94 | 73,61 | 68,83 | 24,5 | 1 |
| oyun | 40 MB | yeni | 1266x712@48 | crf 26 | 38,539 | 73,78 | 73,46 | 68,71 | 43,5 | 2 |
| hdr | 40 MB | eski | 1650x928@60 | 2pass 5296k | 38,501 | 88,11 | 86,81 | 83,64 | 30,7 | 1 |
| hdr | 40 MB | yeni | 1842x1036@60 | crf 22 | 38,404 | 89,06 | 87,52 | 84,19 | 57,3 | 2 |

Fark (yeni − eski):

| kaynak | hedef | Δmean | Δharm | Δp10 | Δteslim MB | Δkodlama sn |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| klip | 8 MB | +0,02 | +0,02 | −0,03 | −0,009 | +0,1 |
| klip | 20 MB | −0,03 | −0,03 | −0,07 | −0,278 | +37,7 (%193) |
| oyun | 40 MB | −0,16 | −0,15 | −0,12 | −0,236 | +19,0 (%78) |
| hdr | 40 MB | **+0,95** | **+0,71** | **+0,55** | −0,097 | +26,6 (%87) |

Tekrarlanabilirlik: `klip 20 MB` yeni kolu iki kez koşuldu, mean 95,2605 ve 95,26;
eski kolu iki kez koşuldu, mean 95,2886 ve 95,30. Yani ±0,02'nin altındaki fark
gürültüdür. Buna göre klip 8 MB'deki +0,02 gürültü, klip 20 MB'deki −0,03 gürültü
sınırında, oyun'daki −0,16 ve hdr'deki +0,95 gerçektir.

**Sonuç:** kazanç yalnız HDR kaynağında, +0,95 mean / +0,71 harmonik / +0,55 p10.
İki SDR kaynağında kazanç yok (biri gürültü, biri −0,16 kayıp). Boyut her koşumda
istenenin altında kaldı; taşma hiçbir yapılandırmada olmadı. Toplam süre yeni planda
her kaynakta belirgin biçimde arttı — bunun nedeni 2. bölümde ölçüldü.

Kazancın kaynağı **yerleşim seçimi**dir (K1), durdurma kısıtı değil: hdr'de ölçülen
kalite planı 1650x928 yerine 1842x1036'ya taşıdı; iki SDR kaynağında yerleşim
değişmedi ve kalite de değişmedi.

### Sonda maliyeti

Kalite ölçümlü sonda ek zaman ister; `ProbeSeconds` sütunu:

| kaynak | eski sonda sn | yeni sonda sn |
| --- | ---: | ---: |
| klip | 6,4 | 11,3 |
| oyun | 8,7 | 13,0 |
| hdr | 6,1 | 12,0 |

---

## 2. Durdurma kısıtı planda çalışıyor, koşucu onu geri alıyor

K2'nin istediği davranış planda gerçekleşti: ölçülen kalite tabanına ulaşan plan
hedefi doldurmadı. `bench shrink --measured-quality` deneme izleri:

| kaynak | hedef | 1. deneme kipi | 1. denemede çıkan | banda göre |
| --- | ---: | --- | ---: | --- |
| klip | 20 MB | crf 20 | 17,697 MB | band altı |
| oyun | 40 MB | crf 26 | 37,540 MB | band altı |
| hdr | 40 MB | crf 22 | 24,483 MB | band altı |

hdr'de durdurma kısıtı 40 MB'lik bütçenin **15,5 MB**'ını harcamadan bıraktı —
K2'nin tam olarak istediği şey.

Sonra `EncodeRunner` devreye giriyor: çıkan dosya band alt sınırının (hedefin %95'i)
altında kaldığı için **yeniden kodluyor ve bütçeyi dolduruyor**:

| kaynak | 2. deneme | 3. deneme | teslim |
| --- | --- | --- | ---: |
| klip 20 MB | 2pass 2787k → 17,248 MB (band altı) | 2pass 3167k → 19,445 MB | 19,445 MB |
| oyun 40 MB | 2pass 5264k → 38,539 MB | — | 38,539 MB |
| hdr 40 MB | 2pass 5296k → 38,404 MB | — | 38,404 MB |

Klip 20 MB'de son bit hızı 3167k, eski planın 3214k'sının %1,5 altında; kalite de
eski planla aynı. Yani **durdurma kısıtının teslim edilen dosyaya etkisi sıfıra
yakın**: planlayıcı duruyor, koşucu doldurmayı geri getiriyor. K6'daki bütün süre
artışı da bu yeniden denemelerden geliyor (deneme sayısı 1 → 2/3).

**Bu düzeltilmedi.** Band alt sınırı ve band-altı yeniden deneme kararı
`src/VidShrink.Ffmpeg/EncodeRunner.cs` içinde ve bu sözleşmenin `owns` kümesinde
değil. Kalite durdurma kısıtının teslim edilen dosyada görünmesi için koşucunun
"planlayıcı bilerek durdu" durumunu band-altı bir kaza sayması bırakılmalı —
`EncodePlan` üzerinde taşınacak bir bayrak ve `EncodeRunner`'ın band-altı
yeniden deneme kapısında bir istisna gerekiyor. İkisi de `owns` dışında.

### Tabanın nereden geldiği

Durdurma tabanı uydurulmamıştır. Sonda örneği libx264 CRF 23'te
(`ComplexityProfile.ProbeCrf`), preset medium, tam ölçekte kesilir; VMAF-NEG orada
ölçülür. Dolayısıyla ölçülen değer doğrudan o klibin `QualityAtReference`'ıdır ve
ölçülen durdurma CRF'i, ölçümün alındığı çalışma noktasının ta kendisidir. Ölçüm
yoksa `CodecModel.PriorQualityAtReference` / `PriorQualityPerHalving` sabitleri
geriye dönüş olarak kalır — adları bunu söyler.

Ölçüm iki ayrı noktada alındıysa eğim (`QualityPerHalving`) de ölçümden uydurulur,
sabitten değil; iki nokta arasındaki açıklık `QualitySlopeMinSpreadHalvings`'in
altındaysa sabit eğime dönülür.

---

## 3. Yineleme bütçesi (K3)

`PlanCalculator.TargetMbForQuality` üç parçadan oluşur: 2 değerlendirmelik köşeleme,
`QualityScanStep = 1.005` adımlı geometrik tarama, en çok
`QualityBisectionMaxSteps = 4` ikiye bölme. Tarama adım sayısı üstten bağlanır:

```
scanBudget = QualitySearchMaxEvaluations - QualityBisectionMaxSteps - QualityBracketEvaluations
           = 1400 - 4 - 2 = 1394
steps = Clamp(ceil(log(span) / log(1.005)), 1, scanBudget)
```

Sınırsız arama yok: `Clamp`'in üst ucu bütün girdilerde bağlayıcıdır.

`bench search-cost` ile ölçülen duvar saati (5 koşumun en iyisi, ölçüm makinesi):

| durum | kaynak | değerlendirme | bütçe | en iyi ms | ms/değerlendirme |
| --- | --- | ---: | ---: | ---: | ---: |
| en kötü | 4K60, 8 saat, 1,2 Gb/s | 1399 | 1400 | 124 | 0,089 |
| tipik | 1080p60, 2 dakika | 1316 | 1400 | 84 | 0,064 |

Karşılaştırma tabanı T88'in kalite sondası maliyeti: pencere başına **1701–7471 ms**.
Aramanın en kötü hali (124 ms) tek bir sonda penceresinin en ucuz halinin %7'sinden
azdır ve bu turda ölçülen gerçek sonda sürelerinin (11,3–13,0 s) **%1'inin altındadır**.
Arama kullanıcıyı bekletmiyor; bekleten şey sondanın kendisidir.

---

## 4. Tepe eğrisi (K5) — bu turda değiştirilmedi

`FfmpegArguments` tepe eğrisi bu turda **değiştirilmemiştir**. Aşağıdaki süpürme
ölçümdür, öneri onun üstüne kuruludur.

### 4.1 Boyut garantisi

K5'in kırılamaz koşulu bu turun sekiz `bench shrink` koşumunun hepsinde tutmuştur:
teslim edilen boyut istenen boyutu hiçbir koşumda aşmadı (doluluk %96,0–%98,7,
`tasma=yok`). Ara denemeler de aşmadı; yeniden denemeler band **altı** kaldıkları
için tetiklendi, band üstü olduğu için değil.

### 4.2 Süpürme

`bench peak-curve klip.mp4 --codec hevc_nvenc --ratios 3,5,8,12 --peaks 1.02,1.1,1.25,1.5`

Kaynak: `klip` (1920x1080@60), kodek `hevc_nvenc`, taban bit hızı 795k. "oran" =
`b:v / taban`, yani `PeakRateFactor`'ın okuduğu taban oranı. "üretim tepesi" =
bugünkü eğrinin o oranda ürettiği çarpan.

| oran | tepe | üretim tepesi | b:v k | maxrate k | bufsize k | boyut MB | mean | harm | p10 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 1,02 | **1,020** | 2385 | 2432 | 2480 | 11,293 | 94,66 | 94,58 | 90,86 |
| 3 | 1,10 | 1,020 | 2385 | 2623 | 2862 | 11,718 | 95,01 | 94,95 | 91,85 |
| 3 | 1,25 | 1,020 | 2385 | 2981 | 3577 | 13,113 | 95,39 | 95,35 | 92,69 |
| 3 | 1,50 | 1,020 | 2385 | 3577 | 4770 | 13,729 | 95,59 | 95,55 | 93,28 |
| 5 | 1,02 | **1,020** | 3975 | 4054 | 4134 | 17,477 | 96,17 | 96,15 | 94,33 |
| 5 | 1,10 | 1,020 | 3975 | 4372 | 4770 | 18,519 | 96,25 | 96,23 | 94,54 |
| 5 | 1,25 | 1,020 | 3975 | 4968 | 5962 | 20,273 | 96,55 | 96,54 | 95,21 |
| 5 | 1,50 | 1,020 | 3975 | 5962 | 7950 | 21,908 | 96,66 | 96,65 | 95,48 |
| 8 | 1,02 | 1,050 | 6360 | 6487 | 6614 | 25,693 | 96,87 | 96,86 | 95,74 |
| 8 | 1,10 | **1,050** | 6360 | 6996 | 7632 | 27,473 | 97,00 | 96,99 | 95,96 |
| 8 | 1,25 | 1,050 | 6360 | 7950 | 9540 | 30,299 | 97,12 | 97,11 | 96,08 |
| 8 | 1,50 | 1,050 | 6360 | 9540 | 12720 | 33,387 | 97,21 | 97,20 | 96,27 |
| 12 | 1,02 | 1,100 | 9540 | 9730 | 9921 | 36,129 | 97,33 | 97,32 | 96,47 |
| 12 | 1,10 | **1,100** | 9540 | 10494 | 11448 | 38,302 | 97,37 | 97,36 | 96,47 |
| 12 | 1,25 | 1,100 | 9540 | 11925 | 14310 | 42,670 | 97,46 | 97,45 | 96,56 |
| 12 | 1,50 | 1,100 | 9540 | 14310 | 19080 | 47,813 | 97,53 | 97,52 | 96,62 |

Bu tablo tek başına bir şey kanıtlamaz: tepe genişledikçe **teslim edilen boyut da
büyüyor**. Aynı `b:v` ile 1,02'den 1,50'ye çıkmak dosyayı oranlara göre %21,6 / %25,3
/ %30,0 / %32,3 büyütüyor. Kalitedeki artışın bir kısmı sadece harcanan bayttan
geliyor.

### 4.3 Eşit boyutta karşılaştırma

Asıl soru: **aynı teslim boyutunda** geniş tepe daha mı iyi? Taban oranı 3
civarında — bugünkü eğrinin en dar olduğu yer — `b:v` düşürülerek boyut
~11,3 MB'ye sabitlendi:

| tepe | b:v k | teslim MB | mean | harm | p10 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| **1,02 (bugünkü)** | 2385 | 11,293 | 94,66 | 94,58 | 90,86 |
| 1,10 | 2261 | 11,355 | **94,91** | 94,85 | **91,60** |
| 1,25 | 1992 | 11,379 | **94,94** | 94,87 | **91,64** |
| 1,50 | 1908 | 11,272 | 94,88 | 94,82 | **91,81** |

Boyut yayılımı %0,9. Aynı süpürmenin bit hızı ekseninde ölçülen eğimi (oran 3 → 5,
tepe 1,02: +1,51 mean / +6,184 MB = 0,244 mean/MB) bu 0,086 MB'lik farkı 0,021 mean
eder — yani boyut farkı, gözlenen etkinin onda biri kadar bile değil.

**Bulgu:** T88'in tepe bulgusu doğrulandı. Düşük taban oranında dar tepe kalite
bırakıyor. Eşit boyutta 1,02 → 1,10 geçişi **+0,25 mean, +0,27 harmonik,
+0,74 p10** getiriyor. Daha da açmak (1,25) mean'e yalnız +0,03 daha ekliyor;
1,50 mean'i geri düşürüyor ama p10'u en yükseğe çıkarıyor (+0,95).

### 4.4 Önerilen eğri değişikliği

Değişiklik **yapılmadı** (bu tur kapsam dışı). Önerilen hali:

```csharp
public const double TightPeakFactor = 1.10;   // bugün 1.02
```

Yani diz düzleşir: `TightPeakFactor` ile `HardwarePeakCeiling` eşitlenir ve
`PeakOpensAtFloorRatio` / `PeakWidestAtFloorRatio` rampası gereksizleşir. Ölçülen
gerekçe 4.3'teki satır: eşit boyutta +0,25 mean / +0,74 p10.

**Boyut garantisi için zorunlu eş değişiklik.** Tepeyi 1,10'a açmak aynı `b:v`'de
teslim boyutunu büyütür; ölçülen şişme oranlara göre %3,8 / %6,0 / %6,9 / %6,0
(ortalama **%5,7**). Bu telafi edilmezse ilk deneme hedefi aşar ya da band üstüne
çıkar. Ölçülen telafi: taban oranı 3'te `b:v` 2385 → 2261 (**−%5,2**) boyutu
11,293 MB'ye geri getiriyor. Yani `TightPeakFactor` değişikliği, hedef bit hızını
~%5,5 kısan bir düzeltmeyle **birlikte** yapılmalıdır; tek başına yapılırsa boyut
garantisi zorlanır.

### 4.5 Ölçülmeyenler

- Öneri tek kaynakta (`klip`, 1080p60 SDR) ölçüldü. HDR ve oyun kaynağında
  tepe süpürmesi **ölçülmedi**.
- Yalnız `hevc_nvenc` ölçüldü. `h264_nvenc`, `*_qsv`, `*_amf` **ölçülmedi**.
- `PeakRateFactor` yalnız donanım kodekleri için dar; yazılım kodekleri zaten
  `WidePeakFactor = 1.5` alıyor. Yazılım tarafında değişiklik önerilmiyor ve
  **ölçülmedi**.
- Önerilen `b:v` kısıntısıyla birlikte uçtan uca `bench shrink` koşumu
  **ölçülmedi**; 4.4'teki telafi yalnız tek noktada doğrulandı.

---

## 5. `DefaultMotionExponent` eşiği kararı (K9)

Karar ertelenmemiştir. Üç seçenekten **"ölçülen dağılımdan yeniden belirle"**
seçilmiş, ayrıca birimin kirlenmesi de giderilmiştir (bölüm 6).

### 5.1 Ölçülen dağılım

`bench container-unit` ile üç kaynakta (biri HDR) 13 pencerede ölçülen hareket üsteli
— yani kare hızını yarıya indirmenin kare başına bit maliyetini kaç kat artırdığının
log2'si:

| ölçü | değer |
| --- | ---: |
| en küçük | 0,597 |
| medyan | 0,871 |
| ortalama | 0,859 |
| en büyük | 1,319 |
| pencere sayısı | 13 (3 kaynak) |

Eski `CodecModel.DefaultMotionExponent = 0,25` **ölçülen her noktanın altındadır**.
`MotionCutIsExpensiveAbove = 0,5` de ölçülen her noktanın altındadır. Yani eski
öğüt bandı dejenereydi: hiçbir gerçek klip "kare hızını düşürmek ucuz" tarafına
düşemiyordu, çünkü eşik ölçülen aralığın tamamen dışındaydı.

### 5.2 Karar

Eşik artık bir üstel sabiti değil, **tasarruf oranı** olarak yazılır — birim
değiştirildi, çünkü "0,25 üstel" hiçbir kullanıcı sorusunun cevabı değil, "yarıya
indirmek bitlerin %20'sini kurtarır mı" ise cevabıdır:

```csharp
private const double MotionCutCheapSavingShare = 0.20;
private static readonly double MotionCutIsCheapBelow = Math.Log2(2 * (1 - MotionCutCheapSavingShare));
```

`MotionCutIsCheapBelow = log2(1,6) ≈ 0,678`. Ölçülen 13 noktanın 4'ü bu eşiğin
altında, 9'u üstünde — yani öğüt bandı artık ölçülen dağılımın içinden geçiyor,
kenarından değil.

Eşik `ComplexityProfile.DefaultMotionExponent`'ten **ayrıldı**. `DefaultMotionExponent`
ve `MotionCutIsExpensiveAbove` bu turda değiştirilmedi: ikisi de
`tests/VidShrink.Tests/ExtremeCompressionTests.cs` tarafından sabitlenmiş ve o dosya
bu sözleşmenin `owns` kümesinde değil. `DefaultMotionExponent` artık yalnız
ölçüm yokken kullanılan geriye dönüş sabitidir; öğüt kararı ondan okumaz.

---

## 6. Kalan sistematik sapma (K10)

### 6.1 Ölçülen Matroska ek yükü

Her sonda penceresi kendi `.mkv` dosyasıdır, dolayısıyla dosya başına sabit bir ek
yük vardır. `bench container-unit` ile 5 kaynakta 24 noktada ölçülen model:

```
overhead(F) = 764,3 B + 6,545 B · F      (R² = 0,926)
```

Kaynak başına uyum aralığı: sabit terim 723–800 B, kare terimi 5,94–7,00 B/kare.
T88'in yayımladığı iki nokta (60 karede 1082 B, 120 karede 1441 B) bu süpürmede
birebir tekrar üretildi.

Sapmanın yönü: hareket örneği yarı kare hızında kesildiği için (F/2 kare) sabit terim
kare başına **iki kat** düşer. Bu, hareket üstelini yukarı doğru şişirir.

### 6.2 Giderilen kısım

`ComplexityProfile.WithoutSampleContainerBias(width, height)` eklendi ve
`PlanCalculator.BuildDetailed` girişinde çağrılıyor. Hem `ReferenceBppf`'ten hem de
hareket üstelinden ek yük çıkarılıyor; işlem `SampleContainerBiasRemoved` bayrağıyla
bir kez uygulanıyor.

Ölçülen etki:

| içerik | arındırma öncesi üstel | sonrası | ham temel gerçek |
| --- | ---: | ---: | ---: |
| gri (düşük karmaşıklık) | 0,2195 | 0,1512 | 0,1551 |
| gerçek içerik (13 pencere) | — | — | kalıntının medyanı 1,9e-4, en kötüsü 9,4e-3 |

Gri kaynakta kirlenme 0,0644 üstel birimi, düzeltmenin kaldırdığı 0,0683 — yani
kirlenmenin tamamını kaldırıyor, %6 aşarak. Gerçek içerikte etkisi kalıntı
medyanı 1,9e-4 ile ölçülebilir sınırın hemen üstünde. Düzeltme gerçek içerikte zarar
vermiyor, uç durumda kurtarıyor.

### 6.3 Giderilmeyen kalan sapmalar

Bunlar bu turda **giderilmedi**; nedenleriyle birlikte:

1. **`ReferenceBppf` taban kelepçesi.** `ComplexityProfile.FromProbe` sonucu
   `Math.Clamp(corrected, 0.002, 2.0)` ile kelepçeleniyor. 1080p'de bu, kare başına
   518 B'nin altındaki içerikte arındırmayı **etkisiz** kılar: düzeltilmiş değer
   kelepçenin altına düşer ve kelepçe onu geri yukarı çeker. Kelepçenin kendisi
   `FromProbe` sözleşmesinin parçası ve başka ölçüler ona bağlı; bu turda
   dokunulmadı.

2. **`MotionExponentMax = 1,0` kırpması.** Ölçülen 13 noktadan **ikisi** (1,296 ve
   1,319) bu tavanın üstünde ve tavana kırpılıyor. Yani gerçek hareket maliyeti
   en yüksek iki pencerede olduğundan düşük görünüyor. Tavanı yükseltmek
   `ExtremeCompressionTests.cs`'in beklentilerini değiştirir; o dosya `owns`
   dışında.

3. **Ayrıntı üsteli (`detail`) arındırılmadı.** `WithoutSampleContainerBias` yalnız
   tam ve hareket örneklerini düzeltiyor. Ayrıntı örneği aynı kaptan geçtiği için
   aynı sapmayı taşıyor; büyüklüğü **ölçülmedi**.

4. **T88'in `ReferenceBppf` şişmesi.** T88 düşük karmaşıklıklı içerikte
   `ReferenceBppf`'in %21–27 şiştiğini yazmıştı. 6.2'deki arındırma bunun
   konteynerden gelen kısmını kaldırıyor (gri kaynakta 0,2195 → 0,1512, ham gerçek
   0,1551). Kalan fark %2,5; bunun kaynağı **ölçülmedi**.

---

## 7. GUI'de uyuyan yol

Ölçülen kalite yolu uygulamada **çalışmıyor**: `src/VidShrink.App/MainWindow.axaml.cs`
(≈1511. satır) `ComplexityProbe.RunAsync` çağırıyor ve ölçülen kaliteyi atıyor.
Bu turda yalnız `bench shrink --measured-quality` yolundan ölçüldü. `MainWindow.axaml.cs`
bu sözleşmenin `owns` kümesinde değil; yol GUI'ye bağlanana kadar bu turun kazancı
kullanıcıya ulaşmaz.

---

## 8. Mutasyon tablosu

Her yeni davranış için üretim kodu bozuldu, ilgili ölçünün kırmızıya döndüğü
koşuldu, sonra geri alındı. Taban: **54 ölçü, tamamı yeşil.**

Koşum: `dotnet test -c Release --filter "PlanCalculatorTests|ComplexityScanTests"`

| # | Kriter | Bozulan üretim davranışı | Kırmızıya dönen ölçü(ler) | Sonuç |
| --- | --- | --- | --- | ---: |
| M1 | K1 | `LayoutScore` ölçülen `complexity.Level` yerine prior'a sabitlendi | `TheStopSitsAtTheOperatingPointTheMeasurementWasTakenAt`, `MeasuredQualityPointsMoveThePredictionOneForOne`, `MeasuredQualityStopLeavesTheRestOfTheBudgetUnspent` | Başarısız 3 / 54 |
| M2 | K1 | İki noktadan eğim uydurma kapatıldı (`if (false && spread >= …)`) | `TwoSeparatedQualityPointsMeasureTheSlopeInsteadOfAssumingIt` | Başarısız 1 / 54 |
| M3 | K2 | Doldurma dalındaki `!qualityStopBinding` kapısı kaldırıldı | `TheStopSitsAtTheOperatingPointTheMeasurementWasTakenAt`, `MeasuredQualityStopLeavesTheRestOfTheBudgetUnspent` | Başarısız 2 / 54 |
| M4 | K2 | Tavan CRF ölçülen durdurma noktasına yükseltilmiyor | `TheStopSitsAtTheOperatingPointTheMeasurementWasTakenAt` | Başarısız 1 / 54 |
| M5 | K3 | `scanBudget` 1394 yerine `QualitySearchMaxEvaluations * 1000` yapıldı | `QualitySearchStaysInsideItsEvaluationBudget` | Başarısız 1 / 54 |
| M6 | K10 | `WithoutSampleContainerBias` çağrısı `BuildDetailed`'dan çıkarıldı | `ThePlanReadsTheProfileWithTheContainerCostAlreadyTakenOut` | Başarısız 1 / 54 |
| M7 | K10 | `SampleContainerBytesPerFrame` 6,545 → 0 | `ThePlanReadsTheProfile…`, `ContainerOverheadIsTakenOutOfTheMeasuredUnit`, `TheContainerOverheadModelMatchesTheMeasuredMatroskaCost` (9 satır) | Başarısız 11 / 54 |
| M8 | K10 | `SampleContainerFixedBytes` 764,3 → 0 | aynı üçlü, `…MatchesTheMeasuredMatroskaCost` 10 satır | Başarısız 12 / 54 |
| M9 | K9 | Ucuz eşiği ölçülen `MotionCutIsCheapBelow` yerine eski `DefaultMotionExponent`'e döndürüldü | `MotionCutIsCalledCheapOnlyWhenHalvingTheFrameRateReallySavesBits` | Başarısız 1 / 54 |
| M10 | K13 | Sonda komutu yine `FfmpegArguments.Build` ile yazdırılıyor | `TheBenchPrintsTheCommandThroughTheWarmingPath` | Başarısız 1 / 54 |
| M11 | K13 | `EncodeRunner`'daki `FfmpegArguments.WarmPsychovisual` çağrısı silindi | `ThePrintedCommandIsTheCommandThatWouldRun` | Başarısız 1 / 54 |

Hiçbir mutasyon hayatta kalmadı.

### Sabit karşılaştıran ölçü yok

Bu turda eklenen hiçbir ölçü "üretim sabitini üretim sabitiyle karşılaştır" biçiminde
değildir. `TheContainerOverheadModelMatchesTheMeasuredMatroskaCost` on ayrı **ölçülmüş**
bayt değerini `[InlineData]` olarak taşır ve modeli onlara karşı sınar (sapma ≤ %12);
M7 ve M8 bunu kanıtlar — sabiti sıfırlamak dokuz/on satırı kırıyor.

### ffmpeg'siz koşucuda sessizce yeşile dönmeme (K12)

Bu turda eklenen ölçülerin tamamı ya ffmpeg'siz çekirdeğe sahiptir (saf hesap:
`TheContainerOverheadModelMatchesTheMeasuredMatroskaCost`,
`ContainerOverheadIsTakenOutOfTheMeasuredUnit`,
`TheWindowTheProfileCorrectsForIsTheWindowTheProbeCuts`,
`ThePrintedCommandIsTheCommandThatWouldRun`, bütün `PlanCalculatorTests` eklemeleri)
ya da kaynak dosyası bulunamazsa **görünür biçimde atlar**:
`TheBenchPrintsTheCommandThroughTheWarmingPath` `[BenchSourceFact]` ile işaretlidir;
`tools/VidShrink.Bench/Program.cs` bulunamayan bir koşumda test yeşile dönmez,
gerekçesiyle birlikte **atlanmış** görünür. xunit 2.9.2'de `Assert.Skip*` yok;
projedeki yerleşik biçim `FactAttribute` türetip `Skip` alanını doldurmaktır
(`FfmpegFactAttribute`, `TonemapFactAttribute` gibi).

`if (!ToolLocator.IsAvailable(out _)) return;` biçiminde sessiz çıkış eklenmemiştir.

**T88'in iki ölçüsü bu şekle çevrilmedi.** `WindowAndMotionSamplesCountTheSameByteUnit`
ve `ProbeEntryPointUsedByTheAppDoesNotMeasureQuality`
`tests/VidShrink.Tests/ComplexityProbeTests.cs` içindedir ve o dosya bu sözleşmenin
`owns` kümesinde değildir.

---

## 9. Yeniden üretme (K11)

Bu rapordaki her tablo `tools/VidShrink.Bench` içinden çıkar; hiçbiri elle tutulan
bir betikten gelmez.

| bölüm | komut |
| --- | --- |
| 1 · kazanç ve süre | `bench shrink <kaynak> <MB> --out <klasör> --results <json>` ve aynısı `--measured-quality` ile |
| 2 · deneme izleri | aynı komut; her deneme satırı `deneme N: <dal>, <kip>, <bit hızı>, hedeflenen, çıkan` olarak basılır |
| 3 · yineleme bütçesi | `bench search-cost --runs 5` |
| 4 · tepe eğrisi | `bench peak-curve <kaynak> --codec hevc_nvenc --ratios 3,5,8,12 --peaks 1.02,1.1,1.25,1.5` |
| 5 · hareket dağılımı | `bench container-unit <kaynak,...>` (`MotionDistribution` bölümü) |
| 6 · konteyner ek yükü | `bench container-unit <kaynak,...>` (`Overhead` ve `Fit` bölümleri) |

## 10. Ölçü hijyeni (K7)

- Bu turda hiçbir mevcut iddia gevşetilmedi, hiçbir ölçü `Skip`'e taşınmadı.
- Taban koşum: `dotnet test -c Release --filter "PlanCalculatorTests|ComplexityScanTests"`
  → **54 / 54 yeşil**.
- T92 ile birleşme sonrası: `--filter "PlanCalculatorTests|ComplexityScanTests|FfmpegArgumentsTests"`
  → **91 / 91 yeşil** (T92'nin 37 ölçüsü dahil, kırılan yok).
- `dotnet build VidShrink.sln -c Release` → 0 uyarı, 0 hata.
- Tam süit bu turda **koşturulmadı**: paralel çalışan başka ajanlar varken eşzamanlı
  tam koşum ölçüyü kararsız yapıyor (bkz. `docs/olcumler/suit-esszamanli-kosum.md`).

---

## 11. Tablo kilitli ölçerle yeniden ölçüldü (T116)

Ölçülen ağaç: dal `T116-cipa-yeniden`. Ortam: Windows 11, ffmpeg 9.0, libvmaf
`vmaf_v0.6.1neg`. Düzenek ve her sayının komutu `tools/cipa-yeniden/`
(`README.md` ve `duzenek/`) altında; ölçüm çıktıları `.calisma/` altında ve
git'e girmiyor.

### 11.1 Kaynaklar ikamedir — hangi satır neyin yerine geçti

T89'un üç kaynağı o turda silindi (§1). **Aynı kaynaklarla yeniden ölçmek
mümkün değil.** Bunun yerine elde duran kaynaklarla yeni bir ızgara kuruldu ve
aynı ızgara **hem kilitli hem kilitsiz** ikiliyle koşuldu — böylece kilidin
bedeli eski tabloya hiç ihtiyaç duymadan görünüyor.

| T89 satırı | T116'da ne koşuldu | durum |
|---|---|---|
| `klip` (1080p60 SDR hevc, 24,8 MB) | `sdr-1.mkv` — **ikame**: `parca-1.mkv` hable ile SDR'e indirilip libx264 preset fast iki geçiş 3400k ile 23,1 MiB'a kodlandı | ikame ölçüldü |
| `oyun` (1080p48 av1 oyun kaydı, 78,3 MB) | — | **ölçülmedi**: elde 48 fps av1 oyun kaydı yok, sentetik taklit oyun kaydının hareket istatistiğini vermez |
| `hdr` (1080p60 HDR hevc, 77,7 MB) | `parca-1.mkv` ve `parca-2.mkv` — ikisi de 1080p60 HDR, T89'un kaynağının kendisi değil | ikame ölçüldü |

İkame kaynağın üretim tarifi `tools/cipa-yeniden/duzenek/ikame-kaynak.sh`.
Kodlama parametreleri x264'ün iki geçiş günlüğünden geri okundu
(`rc=abr bitrate=3400`, `rc_lookahead=30`, `b_adapt=1` → preset fast,
`threads=4`). Ton eşleme operatörü kapta saklanmadığı için **görüntüden
ölçüldü**: ilk kare dört operatörle yeniden üretilip elde duran dosyanın ilk
karesiyle PSNR'landı — hable **39,03 dB**, reinhard 21,28, mobius 20,08,
clip 19,33 (`duzenek/tonemap-dogrulama.sh`). `desat` değeri ve dosyanın bayt
bayt yeniden üretilebilirliği **ölçülmedi**.

### 11.2 İki ikili, iki kilit yeri

Kare kilidi iki ayrı yerde duruyor ve ikisi de bu ızgarayı etkiliyor:

- **`QualityMeter`** (`src/VidShrink.Ffmpeg`) — `--measured-quality` kolunun
  çıpasını üretir, yani **planı** besler. Kilit `822dd3a`, 09-02 04:44.
- **`bench`in kendi grafiği** (`tools/VidShrink.Bench/Program.cs`) — raporun
  VMAF/XPSNR sütunlarını üretir, yani **tabloyu** besler. Kilit `0e2b071`, 03:09.

Aynı ağaçtan iki ikili yayımlandı. `bench-kilitli` ikisini de kilitli taşıyor;
`bench-kilitsiz` **ikisini de kilitsiz** taşıyor — yani T89'un rejimini birebir
yeniden üretiyor. Bu kaynaktan değil **yayımlanmış DLL'den** doğrulandı
(`duzenek/ikili-kilit-denetimi.sh`): `bench-kilitsiz/VidShrink.Bench.dll`
`[1:v]null[r]` ve `flags=lanczos[t];` dizgilerini taşıyor, `bench-kilitli`
taşımıyor.

Izgara bu yüzden 2×2: iki kol (`eski` = sabitler, `yeni` = `--measured-quality`)
× iki ölçer. `eski` kolu `QualityMeter`'a hiç uğramıyor
(`Program.cs:657`, meter `null`), dolayısıyla `eski-kilitli` ile
`eski-kilitsiz` arasındaki fark **yalnız raporun ölçerinden** geliyor.

### 11.3 Karar neye bağlıydı — kol zaten sevk edildi

K3'ün sorusu "kol açılsın mı" değil: **kol kapalı değil, açık ve varsayılan.**
Zincir şöyle:

1. §1'in tablosu (kilitsiz ölçer) "kazanç yalnız HDR'de, +0,95 mean" dedi ve
   "kazancın kaynağı yerleşim seçimidir, durdurma kısıtı değil" diye ekledi.
2. **T100 bu cümleyi gerekçe olarak aldı.** Sözleşmesi birebir şöyle yazıyor:
   *"T89'un tek gerçek kalite kazancı (HDR'de +0,95 mean) durdurma kısıtından
   değil yerleşim seçiminden geliyordu."* Aynı sözleşmenin K5'i
   `MainWindow.axaml.cs`'in ölçülen kaliteyi atmasını kusur sayıp bağlanmasını
   istedi.
3. T100 bağladı (`f2bd2a6`, 09-02 01:20). Bugün `MainWindow.axaml.cs:1445`
   her dosya yüklemesinde `MeasureComplexityAsync` çağırıyor, o da
   `ProbeWithMeasuredQualityAsync(info, speed, **null**, ct)` ile
   `RunDetailedAsync(measureQuality: true, ...)`'a giriyor.
   `ComplexityProbe.cs:53` `null` metreyi `QualityMeasurement.Instance`'a
   çeviriyor. Yani **arayüzden geçen her kullanıcı `--measured-quality`
   yolundan geçiyor.**

Sonuç: `bench`in `--measured-quality` bayrağı bir *deneme kolu* değil,
**uygulamanın hâlihazırdaki davranışını `bench`te açan anahtar**. `bench`in
bayraksız hâli (`eski`) uygulamanın hiç koşmadığı yoldur. Karar bu yüzden
"açalım mı" değil, **"kilitli ölçüde de açık kalmalı mı"**.

### 11.4 Izgara sırasında çıkan iki olay (ölçüm borcu, düzeltilmedi)

**a) Ölçü sessizce boş döndü ve koşum başarı raporladı.**

`sdr8-yeni-kilitli` koşumu `rc=0` ile bitti, XPSNR yazdı, VMAF alanlarının
dördünü birden `null` bıraktı ve **hiçbir yerde hata satırı üretmedi**:

| alan | değer |
|---|---|
| `VmafNegMean` / `Harmonic` / `P10` / `Min` | `null` |
| `VmafNegFloorFrames` | 0 |
| `Xpsnr` | 32,5278 (var) |
| `MeasureSeconds` | 287,6935 |
| günlük satırı | `VMAF-NEG mean=- harm=- p10=- min=-` |
| süreç çıkış kodu | 0 |

Kanıt `.calisma/T116/gunluk/sessiz-bos/` altında saklandı (json + log).

Sebep okundu: `tools/VidShrink.Bench/Program.cs:807` `VmafNegAsync`, libvmaf
çağrısından sonra

```csharp
await RunLavfiAsync(referencePath, testPath, width, height,
    $"libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path={escaped}");
if (!File.Exists(logPath)) return VmafPool.Empty;
```

yapıyor. `RunLavfiAsync`'in döndürdüğü stderr bu çağrı yerinde **atılıyor** ve
çıkış kodu **hiç bakılmıyor**. Filtre herhangi bir sebeple kırıldığında sonuç
"hata" değil "ölçü yok" olarak görünüyor. Bu, koordinatörün `QualityMeter.cs:224`
için kaydettiği borcun **ikinci örneğidir**; aynı kusur iki ayrı yerde duruyor.
`Program.cs` bu sözleşmenin `owns`'unda değil — **bildirildi, düzeltilmedi.**

Koşum aynı parametrelerle tekrarlandı ve ölçü geldi: mean 64,0037, harm 56,8565,
p10 34,4760, `MeasureSeconds` 110,8945. Yani arıza geçiciydi; tablodaki
`sdr8-yeni-kilitli` satırı bu ikinci koşumdan gelir. **Arızanın kök sebebi
ölçülmedi** — ffmpeg'in ne dediği kaydedilmediği için geriye dönük okunamıyor.

**b) Aynı plan iki koşumda birebir aynı dosyayı üretmedi.**

İki koşumun planı, çözünürlüğü, kipi ve bit hızı özdeşti (1190×670@60,
2pass 1061k, deneme=1) ama çıktı farklı:

| koşum | boyut (MB) | XPSNR |
|---|---:|---:|
| ilk (ölçüsü boş dönen) | 7,536956 | 32,527750 |
| ikinci (tabloya giren) | 7,528613 | 32,526333 |

Fark 0,0083 MB. Bu, tablo satırlarının **bayt bayt yeniden üretilebilir
olmadığını** gösteriyor; §11.1'de "yeniden üretilebilirlik ölçülmedi" diye
yazılan maddenin somut karşılığıdır. Sebebi ölçülmedi: `bench`in kendi kodlama
çağrısında iş parçacığı sayısı sabitlenmiyor, ama bunun tek sebep olduğu
gösterilmedi.

### 11.5 Kilitli ve kilitsiz ölçerle A/B (K2)

Izgaradaki her kaynak **ikamedir** — §11.1. `oyun` satırı yok: o kaynak
**ölçülmedi**, elde 48 fps av1 oyun kaydı bulunmadığı için.

Aşağıdaki tablolar §1'in tablosuyla **aynı kaynakları ölçmüyor; satırları
karşılaştırılamaz.** §1'in tablosu "kilitsiz (geçersiz), kaynak silinmiş,
yeniden üretilemez" damgasıyla yerinde duruyor, silinmedi.

Süre sayıları için **makine paylaşımlıydı** (dokuz ajan). Kalite ve boyut
sayılarına bu damga basılmadı.

#### Satırlar — ölçer **kilitli**, kaynaklar ikame

| kaynak (ikame) | hedef | kol | yerleşim | kip | teslim MB | mean | harm | p10 | kodlama sn | deneme |
| --- | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| klip ikamesi | 8 MB | eski | 1190@60 | 2pass 1061k | 7.541 | 64.01 | 56.95 | 34.78 | 52.6 | 1 |
| klip ikamesi | 8 MB | yeni | 1190@60 | 2pass 1061k | 7.529 | 64.00 | 56.86 | 34.48 | 17.3 | 1 |
| klip ikamesi | 20 MB | eski | 1920@60 | 2pass 2695k | 19.304 | 79.59 | 75.00 | 51.92 | 81.0 | 1 |
| klip ikamesi | 20 MB | yeni | 1920@60 | 2pass 2695k | 19.304 | 79.62 | 75.05 | 51.29 | 71.1 | 1 |
| hdr ikamesi (parca-1) | 40 MB | eski | 1458@60 | 2pass 5389k | 38.990 | 81.25 | 80.92 | 74.90 | 245.2 | 2 |
| hdr ikamesi (parca-1) | 40 MB | yeni | 1612@60 | crf 21 | 37.396 | 82.85 | 82.59 | 78.22 | 71.6 | 1 |
| hdr ikamesi (parca-2) | 40 MB | eski | 1920@60 | 2pass 5258k | 39.454 | 95.95 | 95.94 | 95.54 | 111.5 | 1 |
| hdr ikamesi (parca-2) | 40 MB | yeni | 1920@60 | crf 23 | 39.114 | 95.94 | 95.94 | 95.52 | 101.3 | 3 |

#### Satırlar — ölçer **kilitsiz**, kaynaklar ikame

| kaynak (ikame) | hedef | kol | yerleşim | kip | teslim MB | mean | harm | p10 | kodlama sn | deneme |
| --- | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| klip ikamesi | 8 MB | eski | 1190@60 | 2pass 1061k | 7.539 | 60.33 | 27.47 | 23.00 | 16.6 | 1 |
| klip ikamesi | 8 MB | yeni | 1190@60 | 2pass 1061k | 7.540 | 60.32 | 27.55 | 23.71 | 16.3 | 1 |
| klip ikamesi | 20 MB | eski | 1920@60 | 2pass 2695k | 19.325 | 74.06 | 32.49 | 38.70 | 34.8 | 1 |
| klip ikamesi | 20 MB | yeni | 1920@60 | 2pass 2695k | 19.328 | 74.05 | 32.43 | 38.91 | 38.5 | 1 |

#### Kol farkı (yeni − eski), aynı ölçer, kaynaklar ikame

| kaynak (ikame) | hedef | ölçer | Δmean | Δharm | Δp10 | Δteslim MB | Δkodlama sn |
| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| klip ikamesi | 8 MB | kilitli | -0.01 | -0.09 | -0.30 | -0.012 | -35.2 |
| klip ikamesi | 8 MB | kilitsiz | -0.00 | +0.08 | +0.71 | +0.001 | -0.3 |
| klip ikamesi | 20 MB | kilitli | +0.03 | +0.05 | -0.63 | +0.000 | -9.9 |
| klip ikamesi | 20 MB | kilitsiz | -0.01 | -0.06 | +0.20 | +0.004 | +3.7 |
| hdr ikamesi (parca-1) | 40 MB | kilitli | +1.60 | +1.66 | +3.32 | -1.594 | -173.6 |
| hdr ikamesi (parca-2) | 40 MB | kilitli | -0.01 | -0.01 | -0.01 | -0.340 | -10.1 |

#### Kilidin bedeli (aynı kol, kilitli − kilitsiz ölçer)

| kaynak (ikame) | hedef | kol | Δmean | Δharm | Δp10 | plan aynı mı |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| klip ikamesi | 8 MB | eski | +3.69 | +29.47 | +11.78 | evet |
| klip ikamesi | 8 MB | yeni | +3.68 | +29.30 | +10.77 | evet |
| klip ikamesi | 20 MB | eski | +5.53 | +42.52 | +13.22 | evet |
| klip ikamesi | 20 MB | yeni | +5.57 | +42.62 | +12.38 | evet |

#### Izgarada eksik kalan hücreler

Onaltı hücrenin **4'i ölçülmedi**:

- `hdr1-eski-kilitsiz` — hdr ikamesi (parca-1), 40 MB, eski kolu, kilitsiz ölçer: **ölçülmedi**
- `hdr1-yeni-kilitsiz` — hdr ikamesi (parca-1), 40 MB, yeni kolu, kilitsiz ölçer: **ölçülmedi**
- `hdr2-eski-kilitsiz` — hdr ikamesi (parca-2), 40 MB, eski kolu, kilitsiz ölçer: **ölçülmedi**
- `hdr2-yeni-kilitsiz` — hdr ikamesi (parca-2), 40 MB, yeni kolu, kilitsiz ölçer: **ölçülmedi**

### 11.6 K3 kararı — `--measured-quality` kolu açık kalır

**Karar ölçüye bağlıdır ve ölçü şudur: kilitli ölçüde kolun ölçülmüş bir zararı
yok, ölçülmüş bir kazancı var.**

Kilitli ölçerde dört hücrenin dördü de tamamlandı:

| hücre (kaynak ikame) | planı değiştirdi mi | Δmean | Δp10 | Δteslim |
|---|---|---:|---:|---:|
| klip ikamesi 8 MB | **hayır**, plan özdeş | −0,01 | −0,30 | −0,012 MB |
| klip ikamesi 20 MB | **hayır**, plan özdeş | +0,03 | −0,63 | ±0,000 MB |
| hdr ikamesi (parca-1) 40 MB | **evet** — 1458@60 2pass 5389k → 1612@60 crf 21 | **+1,60** | **+3,32** | **−1,594 MB** |
| hdr ikamesi (parca-2) 40 MB | **evet** — 1920@60 2pass 5258k → 1920@60 crf 23 | −0,01 | −0,01 | −0,340 MB |

Üç şey okunuyor:

1. **Kol yalnız planı değiştirdiğinde bir şey yapıyor.** İki SDR hücresinde plan
   alan alan özdeş çıktı; oradaki ±0,03'lük oynama koşumdan koşuma gürültüdür —
   aynı planın iki koşumu §11.4'te 0,0083 MB farklı dosya üretti, yani gürültü
   tabanı bu mertebede.
2. **Planı değiştirdiği yerde kazanç garantili değil.** İki HDR ikamesinin
   **birinde** kazanç var (+1,60 mean, +3,32 p10) ve **üstelik dosya 1,59 MB
   daha küçük**; diğerinde hiçbir şey yok (−0,01). HDR olmak yeterli koşul değil.
3. **Kazancın mekanizması T89'un adlandırdığıyla aynı.** T89 "kazanç durdurma
   kısıtından değil yerleşim seçiminden geliyor" demişti; kazanan hücrede
   yerleşim 1458×820 → 1612×906'ya çıkıyor ve kip iki geçişten crf'ye dönüyor —
   T89'un `hdr` satırındaki 1650×928 → 1842×1036 / crf 22 hareketinin aynısı.

**"+0,95 yalnız HDR'de" yargısı bugün nasıl okunmalı:** yön doğru, ifade fazla
geniş, büyüklük karşılaştırılamaz. Kazanç yine yalnız HDR kaynaklarda göründü,
ama **iki HDR ikamesinin birinde**; "HDR'de kazanır" değil "planı değiştirdiği
bazı HDR kaynaklarda kazanır" denebilir. +1,60 ile +0,95 **karşılaştırılamaz** —
kaynaklar farklı (§11.1), aynı ölçek üzerinde durmuyorlar.

**Karar: kol kapatılmaz.** Gerekçe ölçüdür, tercih değil:

- Ölçülen en kötü sonuç −0,01 mean; bu gürültü tabanının altında. **Ölçülmüş
  zarar yok.**
- Ölçülen en iyi sonuç +1,60 mean / +3,32 p10 ve 1,59 MB daha küçük dosya.
  **Ölçülmüş kazanç var.**
- Kol zaten sevk edilmiş ve varsayılan (§11.3). Kapatmak bir ürün değişikliğidir
  ve onu haklı çıkaracak bir sayı bu ızgarada **yok**.

**Bu kararın dayanmadığı şeyler — ölçülmedi:**

- Kolun **zaman bedeli ölçülmedi.** `ProbeSeconds` farkı (yeni − eski) dört
  hücrede +1,9 / +14,9 / +7,2 / **−7,7** saniye çıktı; işareti bile tutarlı
  değil. Makine dokuz ajanla paylaşımlıydı, bu fark gürültüden ayrılamadı.
- **Deneme sayısı karışık:** klip ikamesi 1/1 ve 1/1, `parca-1` eski 2 → yeni 1,
  `parca-2` eski 1 → yeni **3**. Denemenin bedeli ayrıca ölçülmedi.
- Kazancın **genellenip genellenmediği ölçülmedi**: iki HDR ikamesi var, biri
  kazandı.
- `oyun` sınıfı **hiç ölçülmedi** (§11.1).
- Kolun kaybettiği bir kaynak olup olmadığı **ölçülmedi**; ızgarada böyle bir
  hücre çıkmadı, ama aranmadı da.

**Ölçerin bu karara etkisi.** İki SDR hücresi hem kilitli hem kilitsiz koşuldu ve
kol farkı iki ölçerde de gürültü çıktı (kilitli −0,01 / +0,03, kilitsiz
−0,00 / −0,01). Yani **SDR'de ölçerin kusuru kol yargısını ne yarattı ne gizledi.**
HDR hücrelerinin kilitsiz karşılıkları §11.5'in eksik listesinde; HDR için aynı
şey **ölçülmedi**.

Buna karşılık ölçerin kendi hatası büyük: aynı kolda, aynı dosyada, yalnız ölçer
değişince `klip` ikamesi 20 MB'ta mean **+5,53**, p10 **+13,22** oynuyor
(§11.5, "kilidin bedeli"). Kilitsiz ölçer kaliteyi sistematik olarak **düşük**
gösteriyordu.

### 11.7 Bu turda ne koştu (K7, K8)

**Ürün kodu değişmedi.** Değişen dosyalar: bu belge, `algi-olcusu.md` ve yeni
ölçüm düzeneği `tools/cipa-yeniden/`. Düzenek `src/` altındaki hiçbir sabiti
değiştirmiyor, ürün yoluna girmiyor; **mutasyon kanıtı gerekmedi.** Kilidin
kendi mutasyon bataryası T110'da koşuldu (`algi-olcusu.md` §9.8, M0–M8).

Sözleşmenin `verify` adımı, önce `--no-incremental` derleme ile:

    dotnet build VidShrink.sln -c Release --no-incremental        # rc=0
    dotnet test -c Release --no-build --filter "QualityMeterTests|MeasuredQualityTests"
    # Basarisiz: 0, Basarili: 24, Atlanan: 0, Toplam: 24, Sure: 4 dk 8 sn

**`MeasuredQualityTests` diye bir sınıf yok.** Süzgecin ikinci yarısı hiçbir
şeyle eşleşmiyor; 24 ölçünün 24'ü de `QualityMeterTests`ten geliyor. Ölçülen
kaliteyi gerçekten sınayan üç ölçü `PlanCalculatorTests` içinde ve düz `[Fact]`:

    dotnet test -c Release --no-build --filter "FullyQualifiedName~MeasuredQuality"
    # Basarisiz: 0, Basarili: 3, Atlanan: 0, Toplam: 3, Sure: 3 sn

`MeasuredQualityPointsMoveThePredictionOneForOne`,
`WithoutMeasuredPointsThePredictionFallsBackToThePrior`,
`MeasuredQualityStopLeavesTheRestOfTheBudgetUnspent` — üçü de ffmpeg
gerektirmiyor, **CI'da koşuyor.**

`QualityMeterTests`in 24 ölçüsünün kapı dağılımı sayıldı: **11 düz `[Fact]`,
12 `[FfmpegFact]`, 1 `[TonemapFact]`.** İkinci ve üçüncüsü ffmpeg'e bağlı
(`TonemapFactAttribute`, `FrameGrabberTests.cs:22`, filtre listesi için ffmpeg
koşuyor). Yani ffmpeg'siz koşucuda bu süzgeç **11 geçer, 13 atlar**; yerel
koşumda ffmpeg PATH'te olduğu için 0 atlandı.

**Tam süit koşulmadı** (sözleşme yasakladı), dolayısıyla T116 kendi atlama
sayısını **ölçmedi**. `tools/ci-gibi-kos.sh` ffmpeg'i PATH'ten siliyor; oradaki
yeşil `[FfmpegFact]` ölçüleri hakkında hiçbir şey söylemez. T110 denetiminin
ölçtüğü sayılar: yerel `ci-gibi-kos.sh` **106**, GitHub CI **100** atlıyor.

**Kabul kriteri ölçüleri o atlanan listede mi?** İkiye ayrılıyor:

| ölçü | kapı | ffmpeg'siz CI'da |
|---|---|---|
| `PlanCalculatorTests`in üç `MeasuredQuality*` ölçüsü | `[Fact]` | **koşar** |
| `QualityMeterTests`in beş kare kilidi ölçüsü (§9.8) | `[FfmpegFact]` | **atlanır** |
| `VmafPoolingTests`in iki `OlcumFiltresi_*` ölçüsü | `[Fact]` | koşar, ama **`bench`in grafiğini** pinliyor, ürününkini değil (`algi-olcusu.md` §9.13) |

Izgaranın 16 koşumunun hiçbiri bir test değil; `bench` ikilileri elle koşuldu
(`tools/cipa-yeniden/duzenek/ab-kos.sh`). Süre sayıları için **makine
paylaşımlıydı** — dokuz ajan aynı makinede koştu.
