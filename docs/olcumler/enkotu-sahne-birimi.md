# En kötü sahne birimi — T143

`QualityMeter.WorstScene`'in harita alan aşırı yüklemesinin üretimde sıfır çağıranı vardı.
Kullanıcının algısını ölçtüğü iddia edilen metrik her ölçümde sabit 2 sn ızgarada koşuyordu.
Yanına iki kusur daha çıktı: raporlanan pencere uzunluğu koşulsuz 2.0 basılıyordu ve
`MinimumUnitSeconds` altındaki kısa artık parça ölçünün dışında kalıyordu.

Dal: `T143-enkotu-sahne-birimi` · Uç: `92d671b`

---

## K1 — Kusur önce ölçüldü

### (a) Çağrı yerlerinin ham sayımı

Sözleşme "12 çağrı, dördü haritalı" diyordu. Kendim saydım, ham çıktı:

```
$ grep -rn "WorstScene(" --include=*.cs .
./src/VidShrink.Ffmpeg/QualityMeter.cs:291:            var (worstScene, worstSceneStart) = WorstScene(scores, frameRate, referenceStartSeconds ?? 0);
./src/VidShrink.Ffmpeg/QualityMeter.cs:301:    public static (double Worst, double StartSeconds) WorstScene(
./src/VidShrink.Ffmpeg/QualityMeter.cs:303:        => WorstScene(scores, frameRate, offsetSeconds, null);
./src/VidShrink.Ffmpeg/QualityMeter.cs:305:    public static (double Worst, double StartSeconds) WorstScene(
./tests/VidShrink.Tests/QualityMeterTests.cs:350:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);
./tests/VidShrink.Tests/QualityMeterTests.cs:362:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5);
./tests/VidShrink.Tests/QualityMeterTests.cs:373:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);
./tests/VidShrink.Tests/QualityMeterTests.cs:385:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, MapWithCuts(10.0, 2.5, 5.5));
./tests/VidShrink.Tests/QualityMeterTests.cs:397:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, null);
./tests/VidShrink.Tests/QualityMeterTests.cs:409:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0, MapWithCuts(10.0));
./tests/VidShrink.Tests/QualityMeterTests.cs:421:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5, MapWithCuts(30.0, 15.0, 18.0));
./tests/VidShrink.Tests/QualityMeterTests.cs:433:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);
./tests/VidShrink.Tests/QualityMeterTests.cs:445:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 0);
./tests/VidShrink.Tests/QualityMeterTests.cs:452:    public void SceneShorterThanHalfASecondIsNotTheWorstScene()
./tests/VidShrink.Tests/QualityMeterTests.cs:458:        var (worst, at) = QualityMeter.WorstScene(scores, 60, 12.5, MapWithCuts(30.0, 12.7, 15.0, 18.0));
./tests/VidShrink.Tests/QualityMeterTests.cs:466:        => Assert.Throws<ArgumentException>(() => QualityMeter.WorstScene(Array.Empty<double>(), 60, 0));
```

**Sözleşmedeki sayı yanlıştı.** Test dosyasında 12 *satır* eşleşiyor ama biri çağrı değil:
`:452` bir metot **adı** (`SceneShorterThanHalfASecondIsNotTheWorstScene`). Gerçek sayım:

```
$ grep -rn "QualityMeter\.WorstScene(" --include=*.cs . | wc -l
11
```

| | sayı |
|---|---|
| Testten çağrı | **11** |
| — bunlardan 4 argümanlı | 5 |
| — bunlardan gerçek harita taşıyan | **4** (`:385 :409 :421 :458`; `:397` açıkça `null` geçiyor) |
| Üretimden çağrı | **1** (`QualityMeter.cs:291`, üç argümanlı, `map = null`) |
| Bildirim / devretme | 3 (`:301 :303 :305`) |

Sözleşmenin niteliksel iddiası doğruydu: harita alan aşırı yükleme ve `SceneBounds`un
tamamı yalnız testlerden çağrılıyordu. Sayı yanlıştı (12 değil 11), haritalı çağrı sayısı
(4) doğruydu.

### Üç kırmızının ham çıktısı

```
  Başarısız VidShrink.Tests.QualityMeterTests.ACollapseInAShortTrailingUnitStillReachesTheMeasurement [< 1 ms]
  Hata İletisi:
   the last 0.25 s of the clip scored zero and the measurement still reported 100: the trailing remainder is outside the metric
  Başarısız VidShrink.Tests.QualityMeterTests.TheWorstUnitReportsItsOwnLengthNotTheFixedWindow [13 ms]
  Hata İletisi:
   nothing reports how long the winning unit actually was, so SceneWindowSeconds can only be filled from the constant 2.0 whichever arm ran
  Başarısız VidShrink.Tests.QualityMeterTests.TheProductionMeasurementEntryPointCanCarryASceneMap [< 1 ms]
  Hata İletisi:
   no production measurement entry point accepts a SceneMap, so the map-aware arm of WorstScene can only ever be reached from tests
Başarısız! - Başarısız:     3, Başarılı:    24, Atlanan:     0, Toplam:    27, Süre: 26 s - VidShrink.Tests.dll (net8.0)
```

Kusur commit'i ayrı: `29a1ec1`.

(a) ve (b) yansımayla ölçülüyor, çünkü ikisi de **yapısal** kusur: aranan şey bir değer
değil, üretimde var olmayan bir bağlantı. Davranışsal karşılıkları K2 ve K3 ile birlikte
eklendi (`TheProductionAggregationHonoursTheMapItIsGiven`,
`TheReportedUnitLengthSeparatesTheTwoArms`). (c) baştan davranışsal.

---

## K2 — Harita üretim yoluna bağlandı

### Haritanın bugün nasıl ulaştığı — önce ölçüldü

Üretimde gerçek bir `SceneMap` kuruluyor: `MainWindow.axaml.cs:1812`,
`EncodeRunner.TryBuildSceneMapAsync`. Oradan `FfmpegArguments.Build/BuildSegment`'e
(anahtar kare kararları) ve `PreviewSegment`'e gidiyor. **`QualityMeter`'a hiç uğramıyor.**

Uygulama içinden `QualityMeter`'ın tek çağıranı `QualityMeter.cs:59` — yani
`QualityMeasurement.MeasureWindowAsync`, `IQualityMeasurement`'ın gerçeklemesi.

### Yeni imza

```csharp
public static async Task<QualityScore> MeasureAsync(
    string referencePath, string testPath, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct = default)

public static async Task<QualityScore> MeasureWindowAsync(
    string referencePath, string testPath, double referenceStartSeconds, double testStartSeconds,
    double durationSeconds, VidShrink.Core.SceneMap? sceneMap, CancellationToken ct = default)

public static VmafAggregate AggregateVmaf(
    IReadOnlyList<double> scores, double frameRate, double offsetSeconds,
    VidShrink.Core.SceneMap? sceneMap = null)
```

Toplama `AggregateVmaf`'a ayrıldı. Bunun sebebi ölçülebilirlik: üretimin havuzlama yolunun
haritayı gerçekten kullandığı artık ffmpeg koşmadan pimlenebiliyor.

### Bugün hangi kol gerçekten koşuyor

Sınır açıldı, **besleyen taraf hâlâ haritasız.** İki ayrı sebepten:

1. `IQualityMeasurement.MeasureWindowAsync` (`src/VidShrink.Core/IQualityMeasurement.cs:19`)
   harita parametresi taşımıyor. Bu dosya bu sözleşmenin `owns` kümesinin dışında.
2. Daha temeli: **uygulama haritayı ölçümden sonra kuruyor.**
   `MainWindow.axaml.cs:1806` kalite ölçümünü çalıştırıyor, harita `:1812`'de kuruluyor.
   İmza bugün değiştirilse bile o noktada geçirilecek harita henüz yok.

> **Borç.** Kalite ölçümü üretimde bugün de sabit ızgarada koşuyor. Kapatmak için
> `IQualityMeasurement.MeasureWindowAsync` imzasına harita eklenmeli **ve**
> `MainWindow.MeasureComplexityAsync` içinde harita kurulumu kalite ölçümünün önüne
> alınmalı. İkisi de `src/VidShrink.Core` ve `src/VidShrink.App` altında, ayrı sözleşme.

Uydurma harita üretilmedi, varsayılan değiştirilmedi.

---

## K3 — Raporlanan pencere uzunluğu

### Karar

`QualityScore.SceneWindowSeconds` artık sabit `2.0` değil, **puanı raporlanan birimin
kendi uzunluğu.**

**Gerekçe.** Alan aynı kayıtta `VmafNegWorstScene` ve `WorstSceneStartSeconds` ile yan yana
duruyor; o ikisi tek bir birimi anlatıyor — hangi birim, nerede başlıyor. Üçüncü sayı da
aynı birimin süresi olduğunda üçü birlikte tek ve doğru bir cümle kuruyor: "en kötü birim
şu saniyede başladı, şu kadar sürdü, şu puanı aldı." Birimler eşit olmadığında bile bu
cümle yalan olmuyor. `null` döndürmek de meşru bir cevaptı ama bilgi kaybı: haritalı kolda
birim uzunluğu tam da plana taşınmaya değer sayı. Ortalama birim uzunluğu ise yanındaki iki
alanın anlattığı birime ait olmayan bir sayı olurdu.

Alanın **türü ve adı değişmedi** — `IQualityMeasurement.cs` `owns` dışında, dokunulmadı.

Değerin bugüne göre değişmediği yer: tam bölünen sabit ızgara. 8 sn / 30 fps klipte 4 tam
birim var, alan yine `2.0` basıyor.

### İki kolu ayıran ölçü

```csharp
[Fact]
public void TheReportedUnitLengthSeparatesTheTwoArms()
{
    var scores = Enumerable.Repeat(100.0, 600).ToArray();
    for (var i = 150; i < 330; i++) scores[i] = 40.0;

    var fixedArm = QualityMeter.WorstSceneUnit(scores, 60, 0);
    var mapArm = QualityMeter.WorstSceneUnit(scores, 60, 0, MapWithCuts(10.0, 2.5, 5.5));

    Assert.Equal(2.0, fixedArm.UnitSeconds, 6);
    Assert.Equal(3.0, mapArm.UnitSeconds, 6);
    Assert.NotEqual(fixedArm.UnitSeconds, mapArm.UnitSeconds);
}
```

Aynı puan dizisi, aynı çağrı, tek fark harita: sabit kol 2,0 sn diyor, haritalı kol
sahnenin gerçek uzunluğu olan 3,0 sn diyor.

---

## K4 — Kısa birim kararı

### Karar

**Düşürmek yok: eşiğin altındaki birim komşusuna katılıyor.** Önündeki birime, öyle biri
yoksa (baştaki birimse) ardındakine.

**Gerekçe.** Algıyı ölçen bir metriğin en kısa ve en sert kesitleri hiç görmemesi tam da
kaçınılmak istenen şey; videonun son çeyrek saniyesinde çöken bir kodlama ölçünün dışında
kalıyordu. Birleştirme her kareyi tam olarak bir ölçülen birimin içinde tutuyor, buna
karşılık bir avuç kare üzerinden ortalama alma gürültüsüne de girmiyor — eşiğin var olma
sebebi zaten buydu, eşik korundu, sonucu değişti.

`MinimumUnitSeconds` artık `SceneWindowSeconds / 4.0` değil, kendi başına `0.5`. Sayı aynı,
anlamı değişti: dinamik birim uzunluğunda "sabit pencerenin dörtte biri" olmak bir şey
ifade etmiyordu.

Birleştirmeden sonra hiçbir birim eşiğin altında kalmadığı için döngüdeki atlama koruması
ve onun ardından gelen "hepsi atlandı" geri dönüşü erişilemez hale geldi; ikisi de silindi.

### Değişen girdi

975 kare / 60 fps, son 15 kare (0,25 sn) sıfır:

| | en kötü puan | başlangıç | birim |
|---|---|---|---|
| Önce | `100.0` | `0.0 sn` | 2,0 sn |
| Sonra | `88.889` | `14.0 sn` | 2,25 sn |

Önce: çöküş ölçünün dışındaydı, metrik kusursuz bir klip raporluyordu. Sonra: son artık
parça kendinden önceki birime katıldı ve çöküş göründü.

Bu girdiyi eski davranışı **doğru** diye pimleyen bir test vardı —
`TrailingUnitShorterThanHalfASecondIsDropped`. Kaldırılmadı, yeni davranışı pimleyecek
şekilde yeniden yazıldı: `TrailingUnitShorterThanHalfASecondJoinsTheUnitBeforeIt`.

Baştaki kısa birim de artık düşmüyor, ileri katılıyor
(`LeadingUnitShorterThanHalfASecondJoinsTheUnitAfterIt`). Bu durumda raporlanan puan ve
başlangıç değişmiyor, birim yalnızca büyüyor;
`SceneShorterThanHalfASecondIsNotTheWorstSceneOnItsOwn` beklentileri aynı kaldı, adı
davranışı doğru anlatacak şekilde düzeltildi.

Kapsayıcı ölçü: `EveryFrameLandsInExactlyOneMeasuredUnit` — 975 karenin **her birine**
sırayla tek bir sıfır koyup ölçünün düştüğünü doğruluyor. Hiçbir kare ölçünün dışında değil.

---

## K5 — Mutasyon ızgarası

Her mutasyon tek tek uygulandı, her turda `dotnet build -c Release --no-incremental`
koştu, test `--no-build` **olmadan** çalıştırıldı.

| | Mutasyon | Kırılan ölçü | Toplam |
|---|---|---|---|
| **M1** | `MeasureAsync`/`MeasureWindowAsync`'in harita taşıyan aşırı yüklemeleri silindi | `TheProductionMeasurementEntryPointCanCarryASceneMap` | 1 / 53 |
| **M2** | `AggregateVmaf` haritayı yutuyor (`WorstSceneUnit(..., null)`) | `TheProductionAggregationHonoursTheMapItIsGiven` | 1 / 53 |
| **M3** | `WorstUnit` birim uzunluğu yerine sabit `SceneWindowSeconds` döndürüyor | `TheClipShorterThanOneWindowReportsItsOwnLengthNotTwoSeconds`<br>`TheReportedUnitLengthSeparatesTheTwoArms`<br>`TrailingUnitShorterThanHalfASecondJoinsTheUnitBeforeIt` | 3 / 53 |
| **M4** | `MergeShortUnits` çağrısı kaldırıldı, atlama koruması geri kondu | `ACollapseInAShortTrailingUnitStillReachesTheMeasurement`<br>`LeadingUnitShorterThanHalfASecondJoinsTheUnitAfterIt`<br>`TrailingUnitShorterThanHalfASecondJoinsTheUnitBeforeIt`<br>`EveryFrameLandsInExactlyOneMeasuredUnit` | 4 / 53 |

Dört mutasyonun dördü de kırmızıya düşüyor; hiçbiri sessizce geçmiyor.

**Tek kesişim, kasıtlı.** `TrailingUnitShorterThanHalfASecondJoinsTheUnitBeforeIt` hem
M3'te hem M4'te kırılıyor, çünkü pimlediği olgu — *birleşmiş* birimin 2,25 sn olarak
raporlanması — iki düzeltmeyi birden gerektiriyor: birleştirme olmadan öyle bir birim yok,
gerçek uzunluk raporlanmadan da o sayı görünmüyor. Ayrıştırılabilir olan tek kesişim
ayrıştırıldı: `LeadingUnit...` başta hem birleştirmeyi hem uzunluğu iddia ediyordu ve iki
mutasyonda birden kırılıyordu; ileri birleştirme zaten `Score` ve `StartSeconds` ile
kanıtlandığı için uzunluk iddiası K3'un kendi ölçülerine bırakıldı (`92d671b`).

İki sabiti karşılaştıran ölçü yazılmadı. Ölçülerin pimlediği her sayı — `55.0`, `40.0`,
`2.5`, `3.0`, `88.889`, `2.25`, `92.0`, `0.05` — çağrının ürettiği değer.

---

## K6 — Verify kollarının test sayısı

`dotnet test -c Release --list-tests`, kol başına:

```
QualityMeterTests: 32
VmafPoolingTests:  21
combined:          53
```

Sıfır bulan kol yok. vstest'in varsayılan operatörü substring olduğu için sessizce exit 0
veren boş kol riski vardı; iki kol da gerçekten test buluyor. Sözleşme öncesi taban 24 + 21
= 45'ti, `QualityMeterTests`'e 8 ölçü eklendi.

### Yeşil koşum

```
Başarılı!  - Başarısız:     0, Başarılı:    53, Atlanan:     0, Toplam:    53, Süre: 22 s - VidShrink.Tests.dll (net8.0)
```

`Atlanan: 0` — ffmpeg bu makinede mevcut, `FfmpegFact` ölçüleri de koştu.

### CI

| koşum | commit | sonuç |
|---|---|---|
| `33659543712` | `cc6c69c` (K4) | `cancelled` — ardılı tarafından iptal edildi |
| `33659565512` | `92d671b` (K5) | **`completed success`** |

```
$ gh run view 33659565512 --json status,conclusion,headSha
{"conclusion":"success","status":"completed",
 "headSha":"92d671be982b6f2172819cbb7fadf6b9b387d7ed"}
```

Yeşil koşum bütün kod commit'lerini kapsıyor: `92d671b` K1-K5'in tamamının ucu. Ondan
sonraki tek commit rapor (`de106cf`), yalnız `docs/` altında; CI iş akışı `docs/**` ve
`**/*.md` yollarını görmezden geldiği için yeni koşum başlatmıyor.

CI kapısı yerel verify'dan geniş: `dotnet build -warnaserror` ve
`kosum-kapisi.ps1 -MinimumTotal 1134 -MaximumSkipped 30` — yani bütün süit, atlanan test
tavanıyla birlikte. İkisi de geçti.

---

## Sınırlar

`owns` dışına yazılmadı. `IQualityMeasurement.cs`, `SceneMap.cs`, `VidShrink.App/**` ve
`PlanCalculator.cs` değişmedi — doğrulaması:

```
$ git diff --name-only 1e546f1..92d671b
docs/olcumler/enkotu-sahne-birimi.md
src/VidShrink.Ffmpeg/QualityMeter.cs
tests/VidShrink.Tests/QualityMeterTests.cs
```

Gerçek ffmpeg gerektiren ağır ölçüm başlatılmadı. `WorstScene` saf bir fonksiyon; bütün
kararlar birim testiyle ölçüldü. Geçici dosyalar `.calisma/T143/` altında;
`.calisma/kaynak/` ve `.calisma/ab/` ellenmedi. Kod yorumu yazılmadı.

Geri çekilen iddia yok: hiçbir eşik gevşetilmedi, hiçbir bant genişletilmedi. Değişen tek
beklenti K4'ün değiştirdiği davranışın kendisi ve o da yukarıda girdisiyle yazılı.
