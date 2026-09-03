# T156 — Üç pimsiz borç

Davranış değişmedi. Değişen tek şey, yanlış yapıldığında kırmızıya dönen koruma.
Üç borç da kapandı; kapatırken sözleşmenin iki öncülü ölçüldü ve **ikisi de tutmadı**
(K1'in (a) yolu, K2'nin "sessizce körelir" cümlesi). İkisi de aşağıda kendi bölümünde.

## K1 — `Comparable` kapısı

### Seçilen yol: (b), çünkü (a) mutantı öldüremezdi

Sözleşme iki yol verdi. Yol (a) — `QualityMeterTests`in HDR/tonemap çiftiyle gerçek
ffmpeg koşumu — **mutasyon (a)'yı öldüremez.** Kapının iki kolu var:

```
if (!score.Comparable || score.VmafNegMean is null) return null;
```

Bugün `Comparable: false` üreten her yol `VmafNegMean`i de `null` bırakıyor —
`QualityMeter.cs:220` ve `:252`, ikisi de ilk dört alanı `null` veriyor. Ölçü tarafında
da aynısı yazılı: `QualityMeterTests.HdrAndTonemappedSdrAreNotComparable` hem
`Assert.False(score.Comparable)` hem `Assert.Null(score.VmafNegMean)` diyor. Yani ilk kol
silinse bile ikinci kol aynı girdiyi yakalar ve gerçek koşum yeşil kalır. T154 denetçisinin
"sildim, 45/45 yeşil kaldı" ölçümünün nedeni budur.

Bu yüzden yol (b): karar saf bir yardımcıya çıkarıldı ve doğrudan pimlendi.

### Değişiklik

`src/VidShrink.Ffmpeg/QualityMeter.cs` — `QualityMeasurement.MeasureWindowAsync`in
gövdesindeki altı satır `internal static ToWindowMeasurement(QualityScore, double, long)`a
taşındı; çağıran tek satır kaldı. Koşul, sıra ve üretilen kayıt birebir aynı.

`QualityMeasurement` sınıfının docstring'i **yok**, bu yüzden bayatlayacak cümle çıkmadı;
`IQualityMeasurement.cs`e dokunulmadı.

### Kırmızı (mutasyon (a)) — ham metin

```
  Başarısız VidShrink.Tests.QualityMeterTests.AnIncomparableScoreNeverLeavesTheWindowMeasurement [1 ms]
  Hata İletisi:
   Assert.Null() Failure: Value is not null
Expected: null
Actual:   WindowQualityMeasurement { StartSeconds = 12, VmafNegMean = 94,25, VmafNegHarmonic = 93,5,
          VmafNegP10 = 88, Comparable = True, ElapsedMilliseconds = 340, Message = Renk uzayi uyusmuyor;
          HDR ve SDR/tonemap edilmis goruntu karsilastirilamaz., VmafNegMin = 71, ... }
Başarısız! - Başarısız:     1, Başarılı:    56, Atlanan:     0, Toplam:    57, Süre: 8 s
```

### Geri alınca — ham metin

```
Başarılı!  - Başarısız:     0, Başarılı:    57, Atlanan:     0, Toplam:    57, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

### Sınır

Bugün üretimde `Comparable: false` + dolu `VmafNegMean` üreten yol **yok**. Pim, kaydın
kendi sözleşmesini koruyor: `QualityScore` genel bir kayıt; kimse onu böyle kurmasın diye
değil, kurarsa dışarı sızmasın diye.

## K2 — Tek satıra binen ayrım

### Geri çekilen öncül

Sözleşme "o tek satır silinirse ölçü sessizce körelir" diyordu. Ölçüldü: **körelmiyor,
derleme kırılıyor.** T154 sürümünde `Assert.NotNull(tasinan);` silinip mutasyon (b)
uygulandığında:

```
UretimYoluTests.cs(321,54): error CS8602: Olası bir null başvurunun başvurma işlemi.
```

Çünkü bir sonraki satır `tasinan.Scenes.Count` diyor ve proje nullable uyarılarını hata
sayıyor. Borç gerçekti ama tarifi yanlıştı: asıl zayıflık, ölçünün haritasız kolu **hiç
kurmaması** — `null` sabiti elle yazılıyordu, iki kol yan yana okunmuyordu.

### Eski ölçü (tam metin)

```csharp
    var olcer = MainWindow.ProbeMeter(MainWindow.QualityScenes(Deneme(KesikliHarita(10.0, 2.5, 5.5))));
    var tasinan = Assert.IsType<QualityMeasurement>(olcer).Scenes;
    Assert.NotNull(tasinan);

    var skorlar = DipliSkorlar();
    var haritali = QualityMeter.AggregateVmaf(skorlar, 60, 0, tasinan);
    var sabit = QualityMeter.AggregateVmaf(skorlar, 60, 0, null);

    _cikti.WriteLine($"olcerin tasidigi harita: {tasinan.Scenes.Count} sahne");
    _cikti.WriteLine($"uretim  : enkotu={haritali.WorstScene} at={haritali.WorstSceneStartSeconds}");
    _cikti.WriteLine($"sabit   : enkotu={sabit.WorstScene} at={sabit.WorstSceneStartSeconds}");

    Assert.NotEqual(sabit.WorstScene, haritali.WorstScene);
```

### Yeni ölçü (tam metin)

```csharp
    var haritaliOlcer = MainWindow.ProbeMeter(MainWindow.QualityScenes(Deneme(KesikliHarita(10.0, 2.5, 5.5))));
    var haritasizOlcer = MainWindow.ProbeMeter(MainWindow.QualityScenes(
        new SceneMapAttempt(null, TimeSpan.Zero, SceneMapFallback.NoDuration, "sure yok")));

    var haritali = Assert.IsType<QualityMeasurement>(haritaliOlcer).Scenes;
    var haritasiz = Assert.IsType<QualityMeasurement>(haritasizOlcer).Scenes;

    var skorlar = DipliSkorlar();

    _cikti.WriteLine($"haritali kol : {haritali?.Scenes.Count.ToString() ?? "harita yok"} sahne, enkotu={QualityMeter.AggregateVmaf(skorlar, 60, 0, haritali).WorstScene}");
    _cikti.WriteLine($"haritasiz kol: {haritasiz?.Scenes.Count.ToString() ?? "harita yok"} sahne, enkotu={QualityMeter.AggregateVmaf(skorlar, 60, 0, haritasiz).WorstScene}");

    Assert.NotEqual(
        QualityMeter.AggregateVmaf(skorlar, 60, 0, haritasiz).WorstScene,
        QualityMeter.AggregateVmaf(skorlar, 60, 0, haritali).WorstScene);
```

Haritasız kol artık sabitle değil, üretimin kendi çağrısıyla kuruluyor. İddia tek deyimdir
ve iki kolu birden okur. Koşumun yazdığı satırlar:

```
 haritali kol : 3 sahne, enkotu=40
 haritasiz kol: harita yok sahne, enkotu=55
```

Ölçü gevşemedi: mutasyon (b) bu turdan sonra da ölüyor (aşağıdaki ızgara).

## K3 — Sayı iddiası taraması

`tests/VidShrink.Tests/OluUyeTests.cs`te **bugünkü kaynaktan doğrulanabilir 24 sayı
iddiası** tek tek sayıldı: 17'si tuttu, 7'si sorunluydu. Sorunlu yedinin altısı bayat,
biri yanıltıcı (aşağıdaki tablo, `docs/olcumler/uc-kucuk-borc.md:142-167`; 17 + 7 = 24,
6 + 1 = 7). Ayrıca 3 iddia tarihsel — bugünkü
kaynakta karşılığı yok, onlara dokunulmadı.

Yer gerçeği, ölçünün kendi dökümü
(`dotnet test --filter "OluUyeTests.TheScanDumpsEveryMemberItFound"`):

```
 dosya: 70  uye: 129
 sifir tuketici: 26  hic kullanilmayan: 5
 dizgi/yorum icinde kalan gorunum: 22
```

| # | Satır | İddia | Bugünkü ölçüm | Durum |
| --- | --- | --- | --- | --- |
| 1 | 363 | "Sayi 27'den 26'ya indi" | sıfır tüketici gerçekten **26**; ama cümle 31 satırlık dizinin başında duruyor | yanıltıcı |
| 2 | 364 | `PerformanceProbe.cs:97` | :97 doğru satır | tuttu |
| 3 | 368 | "sekiz yerde uretilip bir yerde tuketiliyor" | `uretim=9 tuketim=1` | **bayat** |
| 4 | 375 | "Iki uyeli tur" + `UpdateCheck.cs:34` | Assumed+Read = 2; :34 doğru | tuttu |
| 5 | 381 | "Tek okuyan `ConversionArguments.cs:86`" | Crf `tuketim=1`; :86 doğru | tuttu |
| 6 | 383 | "uc satici" + "Vendor()'in son satiri" | Nvenc/Qsv/Amf = 3; `CodecModel.cs:134` son satır | tuttu |
| 7 | 385 | "Iki okuyan da (`PlanCalculator.cs:321`, `MainWindow.axaml.cs:2247`)" | **üç** okuyan: `PlanCalculator.cs:342`, `MainWindow.axaml.cs:2284`, `EncodeRunner.cs:140` | **bayat** |
| 8 | 387 | "Alti gerekceden biri" | HardwareVerdictReason 6 üye | tuttu |
| 9 | 389 | `PreviewSegment.cs:103` | doğru satır | tuttu |
| 10 | 393 | "uc durumu" | Olculemedi / OrnekKodlaniyor / TamKodlama = 3 | tuttu |
| 11 | 399 | `MainWindow.axaml.cs:2494-2496` | gerçek **2517-2519** | **bayat** |
| 12 | 401 | `PerformanceReportText.cs:22-26` | doğru aralık | tuttu |
| 13 | 407 | "on bir hatadan sekizini uretip" | sınıflandırıcı **on birin hepsini** üretiyor; sıfır tüketicili olan sekiz | **bayat** |
| 14 | 407 | "arayuz yalniz Cancelled, None, TokenExpired ve Unknown soruyor" | dördü de okunuyor, ama ikisi arayüzde değil (`ShareResult.cs:103`, `ShareErrorClassifier.cs:227`) | **bayat** |
| 15 | 409, 411 | "(disarida=0)" | FileUnreadable 0, LocalDiskFull 0 | tuttu |
| 16 | 413 | "dort yerde uretiliyor" | NetworkFailure `uretim=4` | tuttu |
| 17 | 415 | "uc yerde uretiliyor" | NotAuthorized `uretim=3` | tuttu |
| 18 | 421 | "uc yerde uretiliyor" | ServiceError `uretim=3` | tuttu |
| 19 | 423 | "Sekiz okuma yerinin hepsi `speed == SpeedMode.Fast`" | **on** okuma yeri; dokuzu `==`, `CalibrationProbe.cs:148` `!=` | **bayat** |
| 20 | 425 | `ComplexityProfile.cs:128-132` | doğru aralık | tuttu |
| 21 | 427 | "testlerde ve araclarda bes" | `disarida=5` | tuttu |
| 22 | 429 | "Hicbir yerde gorunmuyor" | `uretim=0 tuketim=0 disarida=0` | tuttu |
| 23 | 435 | "Uretimde sifir, testlerde bir gorunum" | `uretim=0 disarida=1` | tuttu |
| 24 | 484 | "pimlenen 31 satir 129 uyelik kumenin bir parcasi" | `Pinned.Length=31`, `uye: 129` | tuttu |

Doğrulanamayan (tarihsel): `:209` "on gorunum yanlislikla uretim sayildi", `:501` "63 olu
uye bildirmisti", `:49` / `:125` "yedi olayinda" — sonuncusu hata günlüğüne bakıyor, bu
depoya değil. Üçü de olduğu gibi bırakıldı.

### 1 numaralı iddianın kökü

Ham sayım:

```
$ grep -c '^        new(' tests/VidShrink.Tests/OluUyeTests.cs
31
```

26 sayısı **yanlış değil** — `ZeroConsumer` sayısı gerçekten 26. Yanıltıcı olan yeri:
cümle 31 satırlık `Pinned` dizisinin docstring'inde duruyor, okuyan onu dizinin boyu
sanıyor. Fark `Flagged = ZeroConsumer || Unused` = 26 + 5 = 31. Pim oynatılmadı,
31 satır 31 kaldı; cümle ikisini de adıyla söyleyecek şekilde yeniden yazıldı.

Sözleşmenin verdiği `awk 'NR>=372 && NR<=442' | grep -c` penceresi de aynı sayıyı veriyor
(31), çünkü dizi 372-436 arasında ve pencerede başka `new(` yok.

### Düzeltilen cümleler

| Satır | Eski | Yeni |
| --- | --- | --- |
| 363 | "Sayi 27'den 26'ya indi (T150 tur 2)." | "Kume bugun 31 satir: 26 sifir uretim tuketicili uye + 5 hic kullanilmayan uye (`Flagged = ZeroConsumer \|\| Unused`). T150 tur 2'de sifir tuketici sayisi 27'den 26'ya, kumenin tamami 32 satirdan 31'e indi." |
| 368 | "uye artik **sekiz** yerde uretilip bir yerde tuketiliyor" | "uye artik **dokuz** yerde uretilip bir yerde tuketiliyor" |
| 385 | "**Iki okuyan da** (PlanCalculator.cs:321, MainWindow.axaml.cs:2247)" | "**Uc okuyan da** (PlanCalculator.cs:342, MainWindow.axaml.cs:2284, EncodeRunner.cs:140)" |
| 399 | "MainWindow.axaml.cs:**2494-2496**" | "MainWindow.axaml.cs:**2517-2519**" |
| 407 | "on bir hatadan sekizini uretip hicbirini ayirmiyor; arayuz yalniz Cancelled, None, TokenExpired ve Unknown soruyor." | "on bir hatanin hepsini uretiyor, sekizini hicbir kol ayirmiyor. Okunan dort uye: Cancelled ve TokenExpired arayuzde (MainWindow.axaml.cs:1033, :3415), None ShareResult.cs:103'te, Unknown siniflandiricinin kendi sayacinda (ShareErrorClassifier.cs:227)." |
| 423 | "**Sekiz** okuma yerinin hepsi 'speed == SpeedMode.Fast' soruyor" | "**On** okuma yerinin hepsi 'speed == SpeedMode.Fast' kalibinda soruyor (dokuzu ==, CalibrationProbe.cs:148 !=)" |

"32 satırdan 31'e" iddiasının kaynağı:

```
$ git show 008033a:tests/VidShrink.Tests/OluUyeTests.cs | grep -c '^        new('
32
$ git show 89b00dd:tests/VidShrink.Tests/OluUyeTests.cs | grep -c '^        new('
31
$ diff <(008033a uyeleri) <(89b00dd uyeleri)
5d4
<         new("EncoderProbeState.NotWorking"
```

### Bu turda kapanmayan yan

Bu satır numaraları hâlâ **pimsiz**: bayatladıklarında hiçbir ölçü kırmızıya dönmez.
`MainWindow.axaml.cs` ve `PlanCalculator.cs` şu anda T155'in elinde; o birleşince
7 ve 11 numaralı satırlar yeniden kayabilir.

## K4 — Mutasyon ızgarası

Her mutasyondan önce `dotnet build -c Release --no-incremental`. `--no-build` yalnızca
derlemenin hemen ardından, aynı ikiliyi koşturmak için kullanıldı.

| Mutasyon | Ne yapıldı | Kırılan ölçü | Ham çıktı |
| --- | --- | --- | --- |
| (a) | `!score.Comparable \|\|` silindi | `QualityMeterTests.AnIncomparableScoreNeverLeavesTheWindowMeasurement` | `Assert.Null() Failure: Value is not null` — `Başarısız: 1, Başarılı: 56, Toplam: 57` |
| (b) | ctor'un harita parametresi yok sayıldı (`Scenes = null`) | `UretimYoluTests.UretimOlceriHaritayiOlcumeTasiyor` | `Assert.NotEqual() Failure: Values are equal / Expected: Not 55 / Actual: 55` — `Başarısız: 1, Başarılı: 56, Toplam: 57` |
| (c) | K2'nin iddiasından tek satır silindi — üç satırın her biri ayrı denendi | derleme kırıldı; ölçü sessizce geçmedi | 1. satır: `CS1002 ; bekleniyor` · 2. satır: `CS1501: 'NotEqual' yöntemi için hiçbir tekrar yükleme 1 bağımsız değişken almaz` · 3. satır: `CS1002 ; bekleniyor` + `CS1026 ) bekleniyor` |

(c)'de "kırılma" derleme hatasıdır, test kırmızısı değil. Sözleşmenin istediği ayrım budur:
bir satırı silmek ölçüyü susturmuyor. **Bütün deyimi** silmek hâlâ sessizce geçer — hiçbir
test kendi iddiasının yokluğunu ölçemez.

## K5 — Kol başına test sayısı

`--list-tests`, her kol ayrı, `grep -cE '^    VidShrink\.'`:

| Kol | T0'ın dağıtımdan önceki sayısı | Bu dalın başlangıcı | Teslim | Eşleşen sınıf |
| --- | --- | --- | --- | --- |
| `QualityMeterTests` | 32 | 32 | **33** (K1 +1) | yalnız `VidShrink.Tests.QualityMeterTests` |
| `UretimYoluTests` | 13 | 13 | **13** | yalnız `VidShrink.Tests.UretimYoluTests` |
| `OluUyeTests` | 11 | 11 | **11** | yalnız `VidShrink.Tests.OluUyeTests` |

T0'ın üç sayısı da dalın başlangıcında birebir doğrulandı. Sıfır bulan kol yok; hiçbir kol
başka bir sınıfa taşmıyor. Üçü birlikte:

```
Başarılı!  - Başarısız:     0, Başarılı:    57, Atlanan:     0, Toplam:    57, Süre: 9 s - VidShrink.Tests.dll (net8.0)
```

33 + 13 + 11 = 57; toplam koşum sayısıyla tutuyor, yani kollar çakışmıyor.

### ffmpeg süreçleri

`QualityMeterTests` tam koşumu (33 test, 9 s) sırasında, komut satırında `vidshrink_qm_`
geçen süreçler 150 ms aralıkla örneklendi:

```
benzersiz PID (vidshrink_qm_): 27
ffmpeg.exe = 19
ffprobe.exe = 8
```

Bu bir **alt sınırdır**: 150 ms'den kısa yaşayan süreç örneklemeye düşmemiş olabilir.
Süre ölçümü yapılmadı — koşum sırasında makinede paralel bir başka ajan da ffmpeg
koşturuyordu.

### CI

| Alan | Değer |
| --- | --- |
| Koşum kimliği | `33752378032` |
| İş akışı | `ci` |
| Commit | `ab2557ab628c718c1aa24f78562a8c8f73abe7d0` |
| Sonuç | `completed` / `success` |

Koşum kapısının ham satırı:

```
Passed!  - Failed:     0, Passed:  1533, Skipped:    18, Total:  1551, Duration: 18 m 14 s - VidShrink.Tests.dll (net8.0)
KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=1551 alt-sınır=1134 atlanan=18 ust-sinir=30
```
