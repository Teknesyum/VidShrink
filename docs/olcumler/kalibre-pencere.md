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
duruyor — ama çağrıya verilmiyor. Depoda `sceneCuts` argümanını dolduran tek yer
`tests/VidShrink.Tests/ComplexityProbeTests.cs:383`, yani bir ölçü. Üretimde hiçbir
yol doldurmuyor; `ComplexityProbe`un kendi yoklaması da
`ProductionPlan = SamplingPlan.Fixed` olduğu için kesik dalına hiç girmiyor.

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
| 8 | Quality | 2: [1.5 4.5] | 2: [1.5 4.5] | 3: [0.2 2.6 5] | 4: [0 2 4 6] |
| 30 | Quality | 3: [4.7 14 23.3] | 3: [4.7 14 23.3] | 2: [0.5 15.5] | 5: [0.5 6.5 12.5 18.5 24.5] |
| 60 | Quality | 3: [9.7 29 48.3] | 3: [9.7 29 48.3] | 2: [2 32] | 5: [0.5 15.5 24.5 39.5 48.5] |
| 120 | Quality | 3: [19.7 59 98.3] | 3: [19.7 59 98.3] | 2: [5 65] | 5: [2 32 50 80 98] |
| 600 | Quality | 3: [99.7 299 498.3] | 3: [99.7 299 498.3] | 2: [29 329] | 5: [14 164 254 404 494] |
| 3600 | Quality | 3: [599.7 1799 2998.3] | 3: [599.7 1799 2998.3] | 2: [179 1979] | 5: [89 989 1529 2429 2969] |
| 2 | Fast | 1: [0] | 1: [0] | 1: [0] | 1: [0] |
| 8 | Fast | 2: [1.5 4.5] | 2: [1.5 4.5] | 2: [0.2 5] | 2: [4 6] |
| 30 | Fast | 2: [7 21] | 2: [7 21] | 2: [0.5 15.5] | 2: [0.5 15.5] |
| 60 | Fast | 2: [14.5 43.5] | 2: [14.5 43.5] | 2: [2 32] | 2: [0.5 3.5] |
| 120 | Fast | 2: [29.5 88.5] | 2: [29.5 88.5] | 2: [5 65] | 2: [2 8] |
| 600 | Fast | 2: [149.5 448.5] | 2: [149.5 448.5] | 2: [29 329] | 2: [14 44] |
| 3600 | Fast | 2: [899.5 2698.5] | 2: [899.5 2698.5] | 2: [179 1979] | 2: [89 269] |

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
