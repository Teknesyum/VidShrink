# T142 — Kalibrasyon yoklamasında pencere yerleşimi

Dal: `T142-kalibre-pencere` · Ölçümler `dotnet build -c Release --no-incremental`
ile derlenip `dotnet test -c Release` ile koşuldu. Hiçbir ölçüm gerçek ffmpeg
başlatmıyor; hepsi mantık ve yerleşim ölçüsü.

## K1 — Kusur önce ölçüldü, üçü de kırmızı

Ölçüler `tests/VidShrink.Tests/KalibrePencereTests.cs` içinde, kusur commit'i
`b5f1750`. Aşağıdaki ham çıktı, düzeltme öncesi gövdeye karşı koşulan **son**
ölçü kümesinden alındı (21 ölçü: 3 kırmızı, 18 yeşil).

```
  Başarısız VidShrink.Tests.KalibrePencereTests.SahneKesigiPencereMerkezleriniDegistirmeli [10 ms]
   Assert.NotEqual() Failure: Collections are equal
Expected: Not [39,667000000000002, 119, 198,333]
Actual:       [39,667000000000002, 119, 198,333]
  Başarısız VidShrink.Tests.KalibrePencereTests.AyniSureFarkliHeterojenlikFarkliPencereSayisiVermeli [2 ms]
   Assert.NotEqual() Failure: Values are equal
Expected: Not 3
Actual:       3
  Başarısız VidShrink.Tests.KalibrePencereTests.UzunVeCokDegiskenKaynakUcPencereninUstuneCikmali [< 1 ms]
   pencere sayisi 3, tavan hala uc
Başarısız! - Başarısız:     3, Başarılı:    18, Atlanan:     0, Toplam:    21, Süre: 27 ms
```

- **(a) Heterojenlik körlüğü.** 600 sn, aynı süre, iki farklı içerik: düz kaynak
  ve saniyesi değişen kaynak. İkisi de **3** pencere alıyordu.
- **(b) Sahne körlüğü.** İki farklı kesik dizisi, **aynı** saniye profili. Pencere
  merkezleri ikisinde de `[39,667 119 198,333]` — kesiğin konumu yerleşime hiç
  girmiyordu. Ölçü, iki haritanın saniye profilinin birebir aynı olduğunu önce
  doğruluyor; böylece farkın tek olası kaynağı kesik konumu kalıyor.
- **(c) Tavan üç.** 600 sn, çok değişken kaynak: **3**.

`Windows` bunun için `private`ten `internal`a alındı;
`InternalsVisibleTo("VidShrink.Tests")` `src/VidShrink.Ffmpeg/TempCleanup.cs:5`te
zaten vardı, yeni bir şey eklenmedi.

## K2 — Sayım `ComplexityProbe.PlanWindowCount`e devredildi

`WindowSeconds`, `MinWindows`, `MaxWindows` üçü de `CalibrationProbe.cs`ten
**kaldırıldı**. Dosyada kalan sabitlerin tamamı — dört tane, hiçbiri pencere
sabiti değil:

| sabit | değer | neden duruyor |
|---|---|---|
| `CrfGap` | 4.0 | Kalibrasyonun iki ölçüm noktası arasındaki CRF açıklığı. Eğrinin eğimini çözen şey bu; pencere yerleşimiyle ilgisi yok. |
| `SoftwareConcurrency` | 4 | Yazılım kodlayıcıda eşzamanlı ffmpeg kapısı. Kaynak sınırı, içerik kararı değil. |
| `HardwareConcurrency` | 2 | Donanım kodlayıcıda aynı kapı; donanım oturumu daha az paralel kaldırıyor. |
| `SampleTimeout` | 90 sn | Örnek başına ölüm çizgisi. Sözleşme büyütmeyi yasakladı, dokunulmadı. |

Pencereye ait sayısal sabit sayısı: **sıfır**. Sayım `ComplexityProbe.PlanWindowCount`,
pencere uzunluğu `PlanWindows`un döndürdüğü pencerenin kendi `Length`i, yedek
yolun uzunluğu da yine o listeden okunuyor.

**`SpeedMode.Fast` kararı, tek cümle:** hız kipinde yoklama en fazla iki örnek
alır, çünkü Fast doğruluğu süresi için takas eder — bu yüzden Fast, içerik ne
kadar değişken olursa olsun sayımı `ComplexityProbe.MinWindows` ile tavanlar.
Yeni bir sabit uydurulmadı; `MinWindows` paylaşılan `internal` sabit.

## K3 — Yerleşim sahne kesiğine bakıyor, ama kesik bu yola bugün ulaşmıyor

**Karar cümlesi: düzenek bağlı, veri beslenmemiş.**

Ölçüm: `CalibrationProbe.RunAsync`in tek çağıranı
`src/VidShrink.App/MainWindow.axaml.cs:1824`. Sahne haritası **bir satır yukarıda**,
`:1812`de `EncodeRunner.TryBuildSceneMapAsync` ile zaten üretilmiş ve `_sceneMap`te
duruyor — ama çağrıya verilmiyor.

`PlanWindows`un `sceneCuts` argümanını dolduran **dokuz çağrı yeri** var. Sekizi bu
sözleşmenin dışında, dokuzuncusu bu sözleşmenin `CalibrationProbe`a eklediği çağrı:

```
src/VidShrink.Ffmpeg/CalibrationProbe.cs:141        CutTimes(scenes!, duration)   <- bu sözleşme
tests/VidShrink.Tests/ComplexityProbeTests.cs:184   new[] { duration / 2 }
tests/VidShrink.Tests/ComplexityProbeTests.cs:369   cuts = new[] { 60.0, 120.0, 180.0 }
tests/VidShrink.Tests/ComplexityProbeTests.cs:387   new[] { 120.0 }
tests/VidShrink.Tests/ComplexityProbeTests.cs:388   new[] { 30.0, 60.0, 90.0, 120.0, 150.0, 180.0, 210.0 }
tools/ornekleme/Program.cs:214                      cuts
tools/ornekleme/Program.cs:224                      cuts
tools/ornekleme/Program.cs:229                      cuts
tools/ornekleme/Program.cs:232                      cuts
```

Bunların dördü (`tools/ornekleme`) bir ölçü değil, ölçüm aracı: oradaki `cuts`
`Program.cs:159`da `SceneDetector.ScanAsync` + `SceneMap.DerivedCutTimes` ile
**gerçek taramadan** türüyor. Kesikleri sahne koluna süren tek düzenek odur.

Sayım şöyle üretildi. Önce bütün çağrı yerleri — ripgrep, çok satırlı desen
`ComplexityProbe\.PlanWindows\([^;]*;`, `-g '*.cs'`, `src tests tools` altında:

```
tools/ornekleme/Program.cs:198 214 224 229 232 368 479 487                    (8)
tests/VidShrink.Tests/ComplexityProbeTests.cs:183 225 226 240 241 249 250
                                              268 319 369 387 388 413 445 447 (15)
src/VidShrink.Ffmpeg/CalibrationProbe.cs:140 147                              (2)
```

25 nitelenmiş çağrı, artı `ComplexityProbe.cs:79`daki nitelemesiz kendi çağrısı = **26**.
Dördüncü argümanına göre ayrıldıklarında: 9'u doldurulmuş (yukarıdaki liste), 6'sı açıkça
`null` veriyor (`:226 :240 :241 :268 :319 :447`), kalan 11'i üç ya da daha az argümanla
çağırıyor. 9 + 6 + 11 = 26.

Tur 1'in burada yazdığı "dolduran tek yer `ComplexityProbeTests.cs:383`, yani bir ölçü"
cümlesi iki yerden yanlıştı: `:383` bir çağrı değil,
`SceneCutsChangeWhereTheScenePlanLooks` metodunun başlığı; ve sayı bir değil dokuz.

Karar bundan etkilenmiyor. Dokuz çağrı yerinin sekizi test ve ölçüm aracı; üretim yolunda
`MainWindow.axaml.cs:1824` haritayı vermediği için `CalibrationProbe.cs:141` kolu hiç
koşmuyor. `ComplexityProbe`un kendi yoklaması da `ProductionPlan = SamplingPlan.Fixed`
olduğu için kesik dalına girmiyor.

Bu sözleşmede yapılan: `RunAsync` ve `Windows` opsiyonel bir `SceneMap` alıyor;
harita geldiğinde saniye profili sahnelerin ölçülmüş `BitsPerSecond` değerinden
kuruluyor, kesikler sahne başlangıçlarından çıkıyor ve yerleşim
`ComplexityProbe.PlanWindows(SamplingPlan.Scene, …)`e devrediliyor. Harita
gelmediğinde bugünkü eşit aralıklı yerleşim `SignallessWindows` adıyla yedek yol
olarak duruyor — silinmedi.

Hangi kol bugün gerçekten koşuyor: **yedek kol, her girdide.** K5 tablosunun
"harita yok" sütunu bunu gösteriyor; o sütun "bugünkü" sütununun satır satır
aynısı. Çağrıyı düzeltmek bu sözleşmenin işi değil; açık borç olarak aşağıda.

## K4 — `SampleWindow.Length` gerçekten kullanılıyor

`-t` artık sabit `WindowSeconds` değil, pencerenin kendi `Length`i. Argüman
üretimi `TrimArgs(SampleWindow)` içine ayrıldı, ffmpeg olmadan ölçülebiliyor.

Bugünkü ham uzunluk listesi (değişken haritayla, `SpeedMode.Quality`):

```
sure 8:    -t = [2 2 2 2]
sure 60:   -t = [2 2 2 2 2]
sure 600:  -t = [2 2 2 2 2]
sure 3600: -t = [2 2 2 2 2]
```

Yani **bugün tüm uzunluklar 2.0**. `PlanWindows`un üç kolu da (`FixedWindows`,
`ProfileWindows`, `SceneWindows`) pencereyi tek bir `windowSeconds` ile kuruyor.
Bu değişiklik davranışı değiştirmiyor; dinamik uzunluğa giden yolu açıyor.
İddia bundan büyük değil.

## K5 — Davranış nerede değişti

Sol iki sütun bugünkü kod ile yeni kodun **harita verilmediği** hali; sağ iki
sütun harita verildiğinde. Biçim `sayı: [başlangıçlar]`.

| süre | hız | bugünkü | yeni, harita yok | yeni, düz harita | yeni, değişken harita |
|---|---|---|---|---|---|
| 2 | Quality | 1: [0] | 1: [0] | 1: [0] | 1: [0] |
| 8 | Quality | 2: [1.5 4.5] | 2: [1.5 4.5] | 3: [0.2 2.6 5] | 2: [2.2 4.4] |
| 30 | Quality | 3: [4.7 14 23.3] | 3: [4.7 14 23.3] | 2: [0.5 15.5] | 5: [0.5 6.5 12.5 18.5 24.5] |
| 60 | Quality | 3: [9.7 29 48.3] | 3: [9.7 29 48.3] | 2: [2 32] | 5: [0.5 15.5 24.5 39.5 48.5] |
| 120 | Quality | 3: [19.7 59 98.3] | 3: [19.7 59 98.3] | 2: [5 65] | 5: [2 32 50 80 98] |
| 600 | Quality | 3: [99.7 299 498.3] | 3: [99.7 299 498.3] | 2: [29 329] | 5: [14 164 254 404 494] |
| 3600 | Quality | 3: [599.7 1799 2998.3] | 3: [599.7 1799 2998.3] | 2: [179 1979] | 5: [89 989 1529 2429 2969] |
| 2 | Fast | 1: [0] | 1: [0] | 1: [0] | 1: [0] |
| 8 | Fast | 2: [1.5 4.5] | 2: [1.5 4.5] | 2: [2.6 5] | 2: [2.2 4.4] |
| 30 | Fast | 2: [7 21] | 2: [7 21] | 2: [0.5 15.5] | 2: [0.5 15.5] |
| 60 | Fast | 2: [14.5 43.5] | 2: [14.5 43.5] | 2: [2 32] | 2: [0.5 3.5] |
| 120 | Fast | 2: [29.5 88.5] | 2: [29.5 88.5] | 2: [5 65] | 2: [2 8] |
| 600 | Fast | 2: [149.5 448.5] | 2: [149.5 448.5] | 2: [29 329] | 2: [14 44] |
| 3600 | Fast | 2: [899.5 2698.5] | 2: [899.5 2698.5] | 2: [179 1979] | 2: [89 269] |

### Tablonun fixture'ı

Bütün satırlar tek bir üreteçten çıktı. Kaynak `MediaInfo` her satırda aynı —
1920x1080, 30 fps, h264, toplam 8 Mbps, 400 MB (`KalibrePencereTests.cs:9-19`daki
`Kaynak`) — yalnız `DurationSeconds` değişiyor. Harita sütunları aynı dosyanın
`:42-56` satırlarındaki iki yardımcıyla kuruluyor:

- **harita yok** = `CalibrationProbe.Windows(Kaynak(süre), hız, null)`.
- **düz harita** = `DuzHarita(süre, 10)` — 10 eşit uzunlukta sahne, hepsi 4,0 Mbps.
- **değişken harita** = `DegiskenHarita(süre, 20)` — 20 eşit uzunlukta sahne, bit hızı
  sırayla 0,6 Mbps ve 18,0 Mbps.

Başlangıçlar tabloda 1 ondalığa yuvarlandı; ham çıktı 3 ondalık veriyor
(`3: [4.667 14 23.333]` → `3: [4.7 14 23.3]`).

Ham çıktı — `.calisma/T142/uretici`, `dotnet run -c Release`, üç koşumda birebir aynı
(sütunlar: harita yok · düz harita · değişken harita):

```
| 2 | Quality | 1: [0] | 1: [0] | n20 1: [0]
| 8 | Quality | 2: [1.5 4.5] | 3: [0.2 2.6 5] | n20 2: [2.2 4.4]
| 30 | Quality | 3: [4.667 14 23.333] | 2: [0.5 15.5] | n20 5: [0.5 6.5 12.5 18.5 24.5]
| 60 | Quality | 3: [9.667 29 48.333] | 2: [2 32] | n20 5: [0.5 15.5 24.5 39.5 48.5]
| 120 | Quality | 3: [19.667 59 98.333] | 2: [5 65] | n20 5: [2 32 50 80 98]
| 600 | Quality | 3: [99.667 299 498.333] | 2: [29 329] | n20 5: [14 164 254 404 494]
| 3600 | Quality | 3: [599.667 1799 2998.333] | 2: [179 1979] | n20 5: [89 989 1529 2429 2969]
| 2 | Fast | 1: [0] | 1: [0] | n20 1: [0]
| 8 | Fast | 2: [1.5 4.5] | 2: [2.6 5] | n20 2: [2.2 4.4]
| 30 | Fast | 2: [7 21] | 2: [0.5 15.5] | n20 2: [0.5 15.5]
| 60 | Fast | 2: [14.5 43.5] | 2: [2 32] | n20 2: [0.5 3.5]
| 120 | Fast | 2: [29.5 88.5] | 2: [5 65] | n20 2: [2 8]
| 600 | Fast | 2: [149.5 448.5] | 2: [29 329] | n20 2: [14 44]
| 3600 | Fast | 2: [899.5 2698.5] | 2: [179 1979] | n20 2: [89 269]
```

"Bugünkü" sütunu bu üreteçten çıkmaz — o, değişiklikten önceki koddur. Yeni kodun
"harita yok" sütunuyla satır satır aynı olması `SahneHaritasiYokkenYerlesimBugunkuYolunAynisi`
ölçüsüyle bağlanmıştır.

### Tur 2'de düzelen üç hücre

Tablonun 8 sn satırları sessizce başka bir fixture kullanıyordu; üreteçle yeniden
üretildiler:

| hücre | eski | üreteçten çıkan |
|---|---|---|
| 8 sn / Quality / değişken harita | `4: [0 2 4 6]` | `2: [2.2 4.4]` |
| 8 sn / Fast / düz harita | `2: [0.2 5]` | `2: [2.6 5]` |
| 8 sn / Fast / değişken harita | `2: [4 6]` | `2: [2.2 4.4]` |

`4: [0 2 4 6]` n=20 ile çıkmıyor; n=4 ve n=8 ile çıkıyor. `2: [4 6]` ise
**hiçbir n ile çıkmıyor**: 8 sn'de n=2..24 taradım, `DegiskenHarita`nın kendi bit
hızlarıyla. n=4 ve n=8'de Fast şunu veriyor:

```
n=4  Quality 4: [0 2 4 6]   Fast 2: [0 2]
n=8  Quality 4: [0 2 4 6]   Fast 2: [0 4]
```

`2: [0 2]` iyi huylu bir yerleşim değil: 8 saniyelik klipte iki örnek de ilk dört
saniyeye yığılıyor. Aşağıdaki açık borç 2'nin tarif ettiği durum tam budur; yanlış
yazılmış `2: [4 6]` hücresi onun bir örneğini gizliyordu.

Ölçülen heterojenlik:

```
sure 60:  duz h=0 sayim=2   degisken h=0,9355 sayim=5
sure 600: duz h=0 sayim=2   degisken h=0,9355 sayim=5
```

**Bugün hiçbir girdide örnek sayısı azalmadı** — çünkü bugün harita hiçbir
çağrıda verilmiyor ve "harita yok" sütunu "bugünkü" sütununun birebir aynısı.
Beş ölçü bunu cividiyor (`SahneHaritasiYokkenYerlesimBugunkuYolunAynisi`).

Harita beslenmeye başladığında azalan tek girdi sınıfı **düz içerik**: 30 sn ve
üzeri Quality kaynakta 3 → 2. Gerekçe: bunun adı kusur değil, ürünün iddiası —
baştan sona aynı olan bir klipte üçüncü örnek yeni bilgi taşımıyor,
`PlanWindowCount` heterojenlik sıfırken `MinWindows`a iniyor. Taban
`MinWindows = 2`nin altına hiçbir yerde inilmiyor; 8 sn Quality'de 2 → 3,
değişken kaynakta 3 → 5 ile artıyor.

## K6 — Mutasyon ızgarası

Her mutasyon tek başına uygulandı, `dotnet build -c Release --no-incremental` ile
tam derlendi, `--no-build` kullanılmadı.

| mutasyon | ne bozuldu | kırılan ölçü | sonuç |
|---|---|---|---|
| M1 — `Heterogeneity(secondBits)` yerine sabit `1.0` | sayım içeriğe bakmaz | `AyniSureFarkliHeterojenlikFarkliPencereSayisiVermeli` | 1 kırmızı / 21 yeşil |
| M2 — kesikler `Array.Empty<double>()` | yerleşim sahneye bakmaz | `SahneKesigiPencereMerkezleriniDegistirmeli` | 1 kırmızı / 21 yeşil |
| M3 — `-t` yine sabit `2.0` | uzunluk pencereden gelmez | `KesimArgumaniAyriUzunlugunuTasiyabiliyor` | 1 kırmızı / 21 yeşil |
| M4 — `count = Math.Min(count, 3)` | tavan yine üç | `UzunVeCokDegiskenKaynakUcPencereninUstuneCikmali` | 1 kırmızı / 21 yeşil |
| mutasyonsuz | — | — | 0 kırmızı / 22 yeşil |

Her mutasyon yalnız kendi ölçüsünü kırdı; çapraz kırılma yok.

M3'ün ölçüsünün `KesimArgumaniPencereninKendiUzunlugunuTasiyor` değil de sentetik
pencereli ikizi olması bilinçli: bugün planlanan her pencerenin uzunluğu 2.0
olduğu için sabit `2.0` ile `window.Length` üretimde ayırt edilemiyor. Bağlayıcı
olan ölçü, uzunluğu 3.25 olan bir `SampleWindow` veriyor.

## K7 — Verify kollarının her biri gerçekten test buluyor

`dotnet test -c Release --list-tests` ile sayıldı:

| kol | bulunan test |
|---|---|
| `CalibrationProbeTests` | 46 |
| `KalibrePencereTests` | 22 |

Sıfır bulan kol yok. Yoklama çevresindeki geniş koşum
(`KalibrePencereTests|CalibrationProbeTests|ComplexityProbeTests|PlanCalculatorProbeTests|PlanCalculatorTests`):
**151 geçti, 3 atlandı, 0 kaldı.** Atlanan üçü `LiveSourceFact` ile korunan gerçek
ffmpeg ölçüleri; `VIDSHRINK_LIVE_SOURCE` verilmediği için koşmadı, sözleşme ağır
ölçüm başlatmayı yasakladığı için verilmedi.

CI: koşum `33656169898`, rapor yazıldığı anda **`in_progress`**. Sözleşme gereği
beklenmedi, bir kez bakıldı.

## Tur 2 — belge düzeltmesi, kod değişmedi

Tur 2 denetimin iki KRİTİK'i için açıldı; ikisi de bu belgenin içindeydi. **Kod
değişmedi:** turun tek dosya değişikliği `docs/olcumler/kalibre-pencere.md`.

```
$ git status --porcelain
 M docs/olcumler/kalibre-pencere.md
```

Yapılanlar:

- **U1/U2** — K5 tablosunun 8 sn satırları üreteçten yeniden üretildi (üç hücre
  değişti), tablonun altına fixture tanımı ve ham çıktı yazıldı. Üreteç
  `.calisma/T142/uretici`: `CalibrationProbe.Windows`ı `KalibrePencereTests`in kendi
  `Kaynak` / `DuzHarita` / `DegiskenHarita` yardımcılarıyla çağıran saf bir konsol
  programı; ffmpeg çağırmıyor, üç koşumda birebir aynı çıktıyı verdi.
- **U3** — K3'ün envanter cümlesi dokuz çağrı yerini sayan listeyle değiştirildi.
- Denetimin bulmadığı bir hücre daha düzeldi: 8 sn / Fast / **düz harita** yazılı
  `2: [0.2 5]` idi, üreteç `2: [2.6 5]` veriyor. Fast, Quality'nin üç penceresinden
  (`[0.2 2.6 5]`) ilk ikisini değil, ağırlıkça öndeki ikisini alıyor.
- Denetimin bulmadığı bir çağrı yeri daha var: `ComplexityProbeTests.cs:184`
  `sceneCuts`a `new[] { duration / 2 }` veriyor. Denetimin yedi yerlik listesinde yok;
  bu yüzden sayı dokuz, yedi değil.

Verify kolları bir kez koşuldu, ham çıktı:

```
$ dotnet test -c Release --filter "CalibrationProbeTests|KalibrePencereTests"     tests/VidShrink.Tests/VidShrink.Tests.csproj

Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:09.36]     VidShrink.Tests.CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt [SKIP]
  Atlandı VidShrink.Tests.CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt [1 ms]
[xUnit.net 00:00:09.36]     VidShrink.Tests.CalibrationProbeTests.LiveEncodeTimeMatchesTheMeasuredEstimate [SKIP]
[xUnit.net 00:00:09.36]     VidShrink.Tests.CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape [SKIP]
  Atlandı VidShrink.Tests.CalibrationProbeTests.LiveEncodeTimeMatchesTheMeasuredEstimate [1 ms]
  Atlandı VidShrink.Tests.CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape [1 ms]

Başarılı!  - Başarısız:     0, Başarılı:    65, Atlanan:     3, Toplam:    68, Süre: 46 ms
```

Atlanan üçü `LiveSourceFact` ile korunan gerçek ffmpeg ölçüleri; `VIDSHRINK_LIVE_SOURCE`
verilmedi.

## Açık borç

1. **Sahne haritası kalibrasyona beslenmiyor.** `MainWindow.axaml.cs:1824`,
   `:1812`de üretilmiş `_sceneMap`i `CalibrationProbe.RunAsync`e vermiyor.
   Parametre hazır (`SceneMap? scenes`), tek eksik o çağrı. Dosya bu sözleşmenin
   `owns`u dışında.
2. **Fast + harita, pencereleri klibin başına yığabiliyor.** Tabloda 60 sn Fast +
   değişken harita satırı `[0.5 3.5]` veriyor: iki örnek de ilk 5,5 saniyede.
   Kaynağı `ComplexityProbe.SceneWindows`un eşit oranlı sahnelerde beraberliği en
   erken sahne lehine bozması. O dosya T141'in; bugün harita beslenmediği için
   canlı etkisi yok, ama besleme açıldığında önce buraya bakılmalı.
3. **`SampleWindow.Weight` kalibrasyonda kullanılmıyor.** `PlanWindows` pencere
   başına ağırlık döndürüyor, `RunAsync` bayt ve kareleri hâlâ ağırlıksız
   topluyor. `ComplexityProbe.WeightedBppf` bu işi yapan yordamı taşıyor.
   K1–K7'nin hiçbiri bunu istemedi, kapsam büyütülmedi.
4. **Dinamik pencere uzunluğu hâlâ ölü yol.** `-t` artık `Length`ten geliyor ama
   `PlanWindows`un üç kolu da tek uzunluk üretiyor (K4).
