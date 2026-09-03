# T154 — Harita kablosu pimi ve kopya ölçüm gövdesi

T146'nın denetçisi iki borç bıraktı: kalibrasyon yoklamasına giden sahne haritası
kablosunun pimsiz olması, ve ölçüm gövdesinin iki yerde kopya durması. Bu sözleşme
ikisini de kapattı. **Davranış değişmedi** — K2 bir yeniden düzenleme.

Dal: `T154-harita-kablosu-pim`, taban `8798553`.

## K1 — Kablo önce kırmızıya düşürüldü

Var olan ölçü (`KalibrasyonYerlesimiUretimdeSahneHaritasiniGoruyor`) dikiş
fonksiyonunu — `MainWindow.CalibrationScenes` — pimliyordu, çağrı yerini değil.
Denetçi çağrı yerinin son argümanını `(SceneMap?)null` yapıp derledi ve 44/44 yeşil
kaldı.

Yeni ölçü: `UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor`.

**Ne okuyor.** `MainWindow.axaml.cs` kaynağını okur; içindeki `CalibrationProbe.RunAsync(`
çağrısının **bir tane** olduğunu doğrular, o çağrının argüman listesini parantez
derinliği sayarak üst düzey virgüllerden ayırır, altı argüman bulmayı bekler ve
altıncısının `<kapı>(_sceneMap)` şekline uyduğunu arar. Uyuyorsa `<kapı>`yı
`MainWindow` üzerinde yansımayla bulup dolu bir `SceneMapAttempt` ile çağırır, dönen
haritayı `CalibrationProbe.Windows`a verir ve yerleşimin haritasız yerleşimden
gerçekten ayrıldığını ölçer.

**Neden `null`layınca düşüyor.** Argüman `(SceneMap?)null` olduğunda altıncı argüman
metni artık `<kapı>(_sceneMap)` şekline uymaz; regex eşleşmez, ölçü daha yansımaya
gelmeden düşer. Ölçü `CalibrationScenes` adını sabit olarak yazmıyor — kapının adını
çağrı yerinden okuyup çağırıyor, yani pimlediği şey dikişin varlığı değil, çağrı
yerinin `_sceneMap`i o dikişten geçirip yoklamaya vermesi.

### Kusur commit'i

`863c0e3` — "T154 K1 kusur commit'i: kalibrasyon çağrı yerinin harita argümanı null,
yeni ölçü kırmızı". Kusur ve yeni ölçü bu commit'te birlikte duruyor; `d8ffdcd`
argümanı geri koydu.

### Kırmızının ham metni (`863c0e3`)

```
[xUnit.net 00:00:05.40]     VidShrink.Tests.UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor [FAIL]
  Başarısız VidShrink.Tests.UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor [2 ms]
  Hata İletisi:
   kalibrasyon cagri yeri _sceneMap'i gecirmiyor; gecirdigi arguman: (SceneMap?)null
  Yığın İzleme:
     at VidShrink.Tests.UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor() in ...\UretimYoluTests.cs:line 208
  Standart Çıkış İletileri:
 arg[0] = info
 arg[1] = draft
 arg[2] = profile
 arg[3] = speed
 arg[4] = cts.Token
 arg[5] = (SceneMap?)null
Başarısız! - Başarısız:     1, Başarılı:    44, Atlanan:     0, Toplam:    45, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

Kırmızı olan **yalnız yeni ölçü**; var olan 44 ölçünün hepsi mutasyon altında yeşil
kaldı. Denetçinin bulgusu budur.

### Mutasyonun ham metni

Kaynak satır, `MainWindow.axaml.cs` (mutasyon altında):

```csharp
var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, cts.Token, (SceneMap?)null);
```

Üretimdeki hali:

```csharp
var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, cts.Token, CalibrationScenes(_sceneMap));
```

### Geri konduktan sonra (`d8ffdcd`)

```
Başarılı!  - Başarısız:     0, Başarılı:    45, Atlanan:     0, Toplam:    45, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

## K2 — Kopya tek gövdeye indi

`MainWindow.SceneAwareQualityMeasurement` silindi. `QualityMeasurement`
(`src/VidShrink.Ffmpeg/QualityMeter.cs`) haritayı opsiyonel alan olarak kendi taşıyor.

### Seçilen şekil

```csharp
public sealed class QualityMeasurement : VidShrink.Core.IQualityMeasurement
{
    public static QualityMeasurement Instance { get; } = new();

    public QualityMeasurement(VidShrink.Core.SceneMap? scenes = null) => Scenes = scenes;

    internal VidShrink.Core.SceneMap? Scenes { get; }
    ...
            var score = Scenes is null
                ? await QualityMeter.MeasureWindowAsync(
                    referencePath, samplePath, referenceStartSeconds, 0, durationSeconds, ct)
                : await QualityMeter.MeasureWindowAsync(
                    referencePath, samplePath, referenceStartSeconds, 0, durationSeconds, Scenes, ct);
```

`Instance` tekilinin haritası `null`, bu yüzden çağrı `_scenes` almayan aşırı
yüklemede kalıyor — bugünkü davranış aynen korundu. `IQualityMeasurement`
değiştirilmedi; harita gerçeklemenin içinde duruyor, arayüz imzasında değil.

### `ProbeMeter`in yeni gövdesi

```csharp
public static IQualityMeasurement ProbeMeter(SceneMap? scenes)
    => scenes is null ? QualityMeasurement.Instance : new QualityMeasurement(scenes);
```

### `git diff --stat` (K2 adımı, `d8ffdcd` → `d726405`)

```
 src/VidShrink.App/MainWindow.axaml.cs    | 48 ++------------------------------
 src/VidShrink.Ffmpeg/QualityMeter.cs     | 11 ++++++--
 tests/VidShrink.Tests/UretimYoluTests.cs |  8 ++++--
 3 files changed, 17 insertions(+), 50 deletions(-)
```

`--numstat` (eklenen / silinen):

```
3	45	src/VidShrink.App/MainWindow.axaml.cs
9	2	src/VidShrink.Ffmpeg/QualityMeter.cs
5	3	tests/VidShrink.Tests/UretimYoluTests.cs
```

**Silinen satır sayısı: 50.** Bunun 45'i `MainWindow.axaml.cs`ten. Bu 45'in dökümü:

- 43 satır — silinen blok: `SceneAwareQualityMeasurement` sınıfı (36 satır), üstündeki
  `///` docstring'i (6 satır), aralarındaki boş satır (1).
- 2 satır — `ProbeMeter`in yerine yenisi yazılan iki satırı (docstring'in ilk satırı ve
  gövde satırı).

Toplam net değişim: -33 satır. `MainWindow.axaml.cs` 3477 satırdan 3435 satıra indi.

### Geçersiz kalan docstring'ler

Sözleşme iki `///` bloğunun geçersiz kalacağını söyledi; üçüncü bir tane de çıktı.

| Nerede | Ne yapıldı |
|---|---|
| `MainWindow.ProbeMeter` docstring'i | Güncellendi: gövdenin tek yerde, `QualityMeasurement` içinde olduğunu söylüyor. |
| `SceneAwareQualityMeasurement` docstring'i | Sınıfla birlikte silindi. |
| `UretimYoluTests.HaritaGelmedigindeOlcerBugunkuYoldaKaliyor` docstring'i | Cümlesi "`QualityMeasurement` değiştirilmedi" diyordu; K2 onu değiştirdi. Güncellendi. |

## K3 — Davranış değişmedi

### Önce (`8798553`, taban)

```
Başarılı!  - Başarısız:     0, Başarılı:    44, Atlanan:     0, Toplam:    44, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

### Sonra (`d726405`)

```
Başarılı!  - Başarısız:     0, Başarılı:    45, Atlanan:     0, Toplam:    45, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

44 → 45; artan tek şey K1'in yeni ölçüsü.

### Değişen ölçü: bir tane

Taban ile bugün arasındaki `UretimYoluTests.cs` diffinde iddia satırı olarak
değişen tek yer:

```
-        var tasinan = Assert.IsType<MainWindow.SceneAwareQualityMeasurement>(olcer).Scenes;
+        var tasinan = Assert.IsType<QualityMeasurement>(olcer).Scenes;
+        Assert.NotNull(tasinan);
```

**Neden değişti.** `UretimOlceriHaritayiOlcumeTasiyor` adıyla `MainWindow.SceneAwareQualityMeasurement`
tipini soruyordu; K2 o tipi sildi, ölçü derlenmez hale geldi. Sorduğu soru aynı kaldı
— "üretimin kurduğu ölçer haritayı gerçekten taşıyor mu" — yalnız taşıyıcının adı
değişti. `Assert.NotNull(tasinan)` **eklendi**: eski satırda `Scenes` tipi `SceneMap`
(nullable değil) idi, yeni gövdede `SceneMap?`; eksik kalan boşluk kontrolü kapatıldı.
İddia gevşemedi, sıkılaştı.

Tabandaki 12 ölçünün dökümü: 1'inin iddiası değişti (yukarıdaki), 1'inin yalnız
docstring'i güncellendi (`HaritaGelmedigindeOlcerBugunkuYoldaKaliyor`, iddiası aynı),
10'una hiç dokunulmadı. Hiçbir iddia gevşetilmedi, hiçbir eşik oynatılmadı.

## K4 — Mutasyon ızgarası

Her mutasyondan sonra `dotnet build -c Release --no-incremental`; `--no-build` yalnız
`dotnet test` adımında, derlemenin hemen ardından.

| # | Mutasyon | Sonuç | Kırılan ölçü |
|---|---|---|---|
| a | `CalibrationScenes(_sceneMap)` → `(SceneMap?)null` | **ÖLDÜ** | `UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor` |
| b | `QualityMeasurement(SceneMap? scenes) => Scenes = null` (parametre yoksayıldı) | **ÖLDÜ** | `UretimYoluTests.UretimOlceriHaritayiOlcumeTasiyor` |
| c | `if (!score.Comparable \|\| ...)` → `if (score.VmafNegMean is null ...)` (`Comparable` kapısı kaldırıldı) | **HAYATTA** | yok |

### (a) ham çıktı

```
[xUnit.net 00:00:05.56]     VidShrink.Tests.UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor [FAIL]
Başarısız! - Başarısız:     1, Başarılı:    44, Atlanan:     0, Toplam:    45, Süre: 7 s - VidShrink.Tests.dll (net8.0)
```

### (b) ham çıktı

```
[xUnit.net 00:00:06.03]     VidShrink.Tests.UretimYoluTests.UretimOlceriHaritayiOlcumeTasiyor [FAIL]
Başarısız! - Başarısız:     1, Başarılı:    44, Atlanan:     0, Toplam:    45, Süre: 8 s - VidShrink.Tests.dll (net8.0)
```

### (c) ham çıktı — hayatta kalan mutasyon

Doğrulama kollarında:

```
Başarılı!  - Başarısız:     0, Başarılı:    45, Atlanan:     0, Toplam:    45, Süre: 7 s - VidShrink.Tests.dll (net8.0)
```

`QualityMeasurement` adını geçiren diğer iki ölçü sınıfında da (`ComplexityProbeTests`,
`VmafPoolingTests`) hayatta kaldı:

```
Başarılı!  - Başarısız:     0, Başarılı:    56, Atlanan:     0, Toplam:    56, Süre: 23 s - VidShrink.Tests.dll (net8.0)
```

**Yeni ölçü uydurulmadı.** `QualityMeasurement.MeasureWindowAsync` gövdesi `IsAvailable`
kapısının arkasında ffmpeg süreci doğuruyor; bu makinede süreç doğuran ölçü yasak ve
kapıyı pimleyecek ölçü gerçek bir ölçüm koşumu ister. Kapı T146'dan beri pimsizdi,
T154 onu pimlemedi — **açık borç.** Not: mutasyon kopyayı değil tek gövdeyi vuruyor
artık; yani borç bir yerde duruyor, iki yerde değil.

## K5 — Pim sayıları ve kollar

### `OluUyeTests` pimi: değişmedi — ama sözleşmedeki sayı yanlış

**Sözleşme "pim 26'da" diyor; pim 26'da değil, 31'de.** Sayıyı ben saydım:

```
$ awk '/private static readonly PinnedFinding\[\] Pinned/,/^    };/' tests/VidShrink.Tests/OluUyeTests.cs | grep -c "^        new("
31
```

Ölçünün kendi çıktısı da aynı sayıyı veriyor:

```
 uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31
 mesru: 9  borc: 22
```

Taban commit'i `8798553`te de 31:

```
$ git show 8798553:tests/VidShrink.Tests/OluUyeTests.cs | grep -c "^        new("
31
```

Yani **T154 sayıyı oynatmadı**; sözleşmenin öncülü baştan bayattı. `OluUyeTests.cs:363`
docstring'i hâlâ "Sayı 27'den 26'ya indi (T150 tur 2)" diyor; o cümleden sonra pime beş
kayıt daha eklenmiş ve cümle güncellenmemiş. Bu dosya T154'ün `owns` kümesinde değil,
düzeltilmedi — **T0'a bildirilen bulgu.**

**Önce okundu, sonra koşuldu.** `OluUyeTests.Members()` yansımayla yalnız
`type.Namespace.StartsWith("VidShrink.Core")` olan türleri geziyor; bunların enum
üyelerini ve `public static readonly` alanlarını sayıyor. Silinen sınıf
`SceneAwareQualityMeasurement` `VidShrink.App` içindeydi (`MainWindow`ın iç sınıfı),
büyüyen sınıf `QualityMeasurement` ise `VidShrink.Ffmpeg` içinde. İkisi de taramanın
dışında. T154 `VidShrink.Core`a ne enum üyesi ekledi ne de `public static readonly`
alan. Bu yüzden sayının değişmesi beklenmiyordu.

Okuma böyle dedi, koşum da onayladı (`OluUyeTests` 11 ölçü, hepsi yeşil):

```
Başarılı!  - Başarısız:     0, Başarılı:    11, Atlanan:     0, Toplam:    11, Süre: 670 ms - VidShrink.Tests.dll (net8.0)
```

Pim listesine dokunulmadı; sayı ne 26 ne de başka bir yere kaydı, 31'de duruyor.

### Doğrulama kollarının test sayıları

`dotnet test -c Release --no-build --filter "<kol>" --list-tests` ile sayıldı; sıfır
bulan kol yok.

| Kol | Test sayısı |
|---|---|
| `UretimYoluTests` | 13 |
| `QualityMeterTests` | 32 |
| **toplam** | **45** |

`UretimYoluTests`in 13 ölçüsü:

```
VidShrink.Tests.UretimYoluTests.HaritaGelmedigindeKalibrasyonSabitIzgaradaKaliyor
VidShrink.Tests.UretimYoluTests.HaritaGelmedigindeOlcerBugunkuYoldaKaliyor
VidShrink.Tests.UretimYoluTests.HaritaKaliteOlcumundenOnceKuruluyor
VidShrink.Tests.UretimYoluTests.HizliKipteIlkGecisSonGecistenHizliKosuyor
VidShrink.Tests.UretimYoluTests.KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor
VidShrink.Tests.UretimYoluTests.KalibrasyonYerlesimiUretimdeSahneHaritasiniGoruyor
VidShrink.Tests.UretimYoluTests.KaliteKipindeIlkGecisSonGecisinOnAyarindaKaliyor
VidShrink.Tests.UretimYoluTests.KaliteOlcumuUretimdeSahneHaritasiniGoruyor
VidShrink.Tests.UretimYoluTests.Libx264HizliKiptedeTurboyaAcilmiyor
VidShrink.Tests.UretimYoluTests.TabloKalibrasyonYerlesimi
VidShrink.Tests.UretimYoluTests.TabloKaliteEnKotuBirim
VidShrink.Tests.UretimYoluTests.TabloTurboIlkGecis
VidShrink.Tests.UretimYoluTests.UretimOlceriHaritayiOlcumeTasiyor
```

`QualityMeterTests`in 32 ölçüsü:

```
VidShrink.Tests.QualityMeterTests.ACollapseInAShortTrailingUnitStillReachesTheMeasurement
VidShrink.Tests.QualityMeterTests.Bt709MetadataOnlyRemuxMatchesTheIdenticalCopyScore
VidShrink.Tests.QualityMeterTests.CollapseInTheTrailingHalfSecondIsNotDropped
VidShrink.Tests.QualityMeterTests.EveryFrameLandsInExactlyOneMeasuredUnit
VidShrink.Tests.QualityMeterTests.FixedWindowsDiluteTheSceneTheMapWouldIsolate
VidShrink.Tests.QualityMeterTests.HdrAndTonemappedSdrAreNotComparable
VidShrink.Tests.QualityMeterTests.HeavilyDegradedCopyScoresClearlyLowerThanTheOriginal
VidShrink.Tests.QualityMeterTests.IdenticalClipReportsTheModelCeilingInsteadOfAForcedHundred
VidShrink.Tests.QualityMeterTests.LeadingUnitShorterThanHalfASecondJoinsTheUnitAfterIt
VidShrink.Tests.QualityMeterTests.MapWithASingleSceneFallsBackToTheFixedWindow
VidShrink.Tests.QualityMeterTests.OneFrameOfSlipIsWorthTensOfVmafPointsOnThisFixture
VidShrink.Tests.QualityMeterTests.ReferenceAndSampleMayUseDifferentWindowOffsets
VidShrink.Tests.QualityMeterTests.SceneBoundariesAreReadOnTheReferenceTimelineNotFromZero
VidShrink.Tests.QualityMeterTests.SceneShorterThanHalfASecondIsNotTheWorstSceneOnItsOwn
VidShrink.Tests.QualityMeterTests.ShiftedSourceIsReportedNotSilentlyRepaired
VidShrink.Tests.QualityMeterTests.SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame
VidShrink.Tests.QualityMeterTests.TheClipShorterThanOneWindowReportsItsOwnLengthNotTwoSeconds
VidShrink.Tests.QualityMeterTests.TheProductionAggregationHonoursTheMapItIsGiven
VidShrink.Tests.QualityMeterTests.TheProductionMeasurementEntryPointCanCarryASceneMap
VidShrink.Tests.QualityMeterTests.TheReportedUnitLengthSeparatesTheTwoArms
VidShrink.Tests.QualityMeterTests.TheWorstUnitReportsItsOwnLengthNotTheFixedWindow
VidShrink.Tests.QualityMeterTests.TonemappedReferenceSeparatesTwoSdrQualities
VidShrink.Tests.QualityMeterTests.TrailingUnitShorterThanHalfASecondJoinsTheUnitBeforeIt
VidShrink.Tests.QualityMeterTests.TwoNearLosslessRivalsKeepTheirOrderAboveTheCeilingBand
VidShrink.Tests.QualityMeterTests.UntaggedSourceAgainstANonBt709TagIsRefusedInsteadOfAssumed
VidShrink.Tests.QualityMeterTests.VideoStartAheadOfTheContainerIsTheOffsetThatReachesTheFilterGraph
VidShrink.Tests.QualityMeterTests.WorstSceneAveragesOverTwoSecondBuckets
VidShrink.Tests.QualityMeterTests.WorstSceneFallsBackToTheWholeClipWhenItIsShorterThanOneWindow
VidShrink.Tests.QualityMeterTests.WorstSceneFindsTheDamagedSectionTheMeanHides
VidShrink.Tests.QualityMeterTests.WorstSceneRejectsAnEmptyScoreList
VidShrink.Tests.QualityMeterTests.WorstSceneReportsTheWindowStartOnTheReferenceTimeline
VidShrink.Tests.QualityMeterTests.WorstSceneUsesSceneBoundariesWhenTheMapIsPresent
```

### CI koşumu

Koşum kimliği **33746293827**, commit `a999645`, dal `T154-harita-kablosu-pim`,
iş akışı `ci`.

```
$ gh run view 33746293827 --json status,conclusion,databaseId,headSha
33746293827 a999645111fe0f263df1d4372620049d1e56e507 completed success
```

`-warnaserror` derlemesi geçti; filtresiz tam süit CI'da yeşil:

```
Passed!  - Failed:     0, Passed:  1525, Skipped:    18, Total:  1543, Duration: 18 m 42 s - VidShrink.Tests.dll (net8.0)
KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=1543 alt-sınır=1134 atlanan=18 ust-sinir=30
```

## Kalan borç

1. **`Comparable` kapısı pimsiz** (K4-c). Mutasyon hayatta kaldı. Kapıyı pimlemek
   gerçek bir ffmpeg ölçüm koşumu ister; bu makinede süreç doğuran ölçü yasak. Borç
   T146'dan devralındı, T154 onu iki kopyadan tek gövdeye indirdi ama kapatmadı.
2. **K1'in ölçüsü kaynak okuyor.** Çağrı yeri `MeasureComplexityAsync` içinde, özel
   ve `async`; Avalonia penceresi ve ffmpeg olmadan koşturulamıyor. Ölçü çağrı yerinin
   argüman metnini okuyup oradan yansımaya geçiyor — davranışın yarısını gerçekten
   ölçüyor (geçen haritanın yerleşimi değiştirdiğini), diğer yarısını metinden
   çıkarıyor. Çağrı yerini gerçekten koşturan bir ölçü, `MeasureComplexityAsync`in
   ayrılabilir bir parçaya bölünmesini ister; bu T154'ün kapsamı değildi.
3. **`OluUyeTests` docstring'i bayat.** `tests/VidShrink.Tests/OluUyeTests.cs:363`
   "Sayı 27'den 26'ya indi" diyor, pim listesi 31 kayıt taşıyor. Sözleşmenin K5 öncülü
   bu bayat cümleden gelmiş. Dosya T154'ün `owns` kümesinde değil; düzeltilmedi.
