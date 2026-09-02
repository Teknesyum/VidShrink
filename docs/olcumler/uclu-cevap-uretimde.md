# Üçlü cevap üretimde

## K1 — Kusurun ölçümü

Taban: `b88bb66`. Önce `dotnet build VidShrink.sln -c Release --no-incremental`, sonra her ölçü `dotnet test -c Release --filter ...` ile ve `--no-build` kullanılmadan çalıştırıldı. Dört ölçünün dördü de **`d3f8962`** commit'inde kırmızıydı (`T139 K1: dort karar yolunun kirmizi olculeri`).

Tur 2 düzeltmesi: bu paragraf önce `10f9073` diyordu. O commit rebase öncesindendi ve daldan erişilemiyor; daldaki karşılığı `d3f8962`.

| Karar yeri | Ölçü | Eski sonuç |
|---|---|---|
| `PlanCalculator.PickCodec` | `PickCodecOlculmemisTercihiElemedenGeciriyor` | Beklenen `libsvtav1`, gelen `libx265` |
| `PlanCalculator.PickFastCodec` | `PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor` | Beklenen `av1_nvenc`, gelen `libx264` |
| `PerformanceProbe.SelectHardwareCodec` | `PerformanceProbeOlculmemisAdayiCalismiyorSaymiyor` | Beklenen `h264_qsv`, gelen `h264_amf` |
| `HdrResolver` | `SoftwareHdrOlculmemisKodlayiciyiElemedenGeciriyor` | Beklenen HDR koruma, gelen tone-map |

## K2 — Arayüz varsayılanı

`IEncoderAvailability.EncoderState` varsayılanı `Unmeasured` döndürüyor ve süreç doğuran `WorksAsEncoder`ı çağırmıyor. Üçüncü durumu taşıyamayan uygulayıcı için hem `true` hem `false` iki değerli cevap yetersiz kanıttır; varsayılan bu nedenle olumlu veya olumsuz hüküm kurmuyor. `EncoderCapabilities` ile `MainWindow.DeferredEncoderAvailability` üçlü cevabı bildikleri için kendi gerçekleştirmelerini kullanıyor.

Varsayılanı kullanan test sahtesi sayısı **20**. Tur 2 düzeltmesi: bu liste 19 sayıyordu ve `PlanCalculatorProbeTests.SwitchableAvailability` eksikti. Sayı yazıldığı anda taban `b88bb66` idi ve o tabanda gerçekten 19'du; 20'nci sahte `879c88b` (T137 tur 2) ile geldi ve tur 2'nin birleştirme tabanı `f8b33ee` ile bu dala girdi. Geçerli sayı 20'dir.

```
> git grep -c "class .*IEncoderAvailability" b88bb66 -- "tests/VidShrink.Tests/*.cs"   → toplam 19
> git grep -c "class .*IEncoderAvailability" f8b33ee -- "tests/VidShrink.Tests/*.cs"   → toplam 20
> git log --oneline -S SwitchableAvailability f8b33ee -- tests/VidShrink.Tests/PlanCalculatorProbeTests.cs
879c88b T137 tur 2: kusurlar uretildi (T1, T2, T4, T6) — dordu de kirmizi
```

`f8b33ee` tabanında listenin tamamı:

1. `ComplexityScanTests.ColdCapabilities`
2. `EncodeRunnerTests.Encoders`
3. `EncoderAvailabilityTests.Makine`
4. `FfmpegArgumentsTests.OptionAvailability`
5. `FfmpegArgumentsTests.WarmingAvailability`
6. `NoEncoders`
7. `HardwareFlagTests.Availability`
8. `HardwareRateControlTests.FixedAvailability`
9. `HdrArgumentsTests.FakeAvailability`
10. `HdrArgumentsTests.MutatedAvailability`
11. `PerformanceCheckTests.FakeAvailability`
12. `PlanCalculatorProbeTests.RecordingAvailability`
13. `PlanCalculatorProbeTests.UnmeasuredAvailability`
14. `PlanCalculatorProbeTests.ThrowingAvailability`
15. `PlanCalculatorProbeTests.SwitchableAvailability`
16. `PlanCalculatorProbeTests.FlakyThenWorkingAvailability`
17. `PlanCalculatorTests.FakeAvailability`
18. `PlanCalculatorTests.SurucusuzMakine`
19. `PlanCalculatorTests.OlculmemisMakine`
20. `SpeedModeTests.FakeAvailability`

T139 ölçüsündeki `LegacyAvailability` da yalnız varsayılan davranışı doğrulamak için eklendi; taban sayımına dahil değildir.

## K3 — Dört tüketim kararı

| Karar yeri | `Unmeasured` kararı | Gerekçe | Ölçü |
|---|---|---|---|
| `PlanCalculator.PickCodec` | Kodeği elemeden geçir, planı geçici işaretle | Bilinmeyen cevap kalıcı yazılım geri düşüşü değildir | `PickCodecOlculmemisTercihiElemedenGeciriyor` |
| `PlanCalculator.PickFastCodec` | İlk ölçülmemiş adayı geçir, planı geçici işaretle | Sıralı tercihte kanıtsız eleme sonraki kodeği haksız öne çıkarır | `PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor` |
| `PerformanceProbe.SelectHardwareCodec` | İlk ölçülmemiş adayı geçir | Performans koşumu adayı fiilen sınayabilir; “çalışmıyor” hükmü kurulmaz | `PerformanceProbeOlculmemisAdayiCalismiyorSaymiyor` |
| `HdrResolver` | HDR korumayı sürdür, kararı geçici işaretle | Ölçülmemişlik HDR desteğinin yokluğu değildir | `SoftwareHdrOlculmemisKodlayiciyiElemedenGeciriyor` |

Düzeltmeden sonra bu dört ölçü ile arayüz ölçüsü birlikte `5/5` yeşil geçti.

## K4 — Mutasyon matrisi

Her mutasyondan önce `dotnet build VidShrink.sln -c Release --no-incremental` başarılı oldu; testlerde `--no-build` kullanılmadı. Hücreler kırmızı ölçü sayısını gösterir.

| Geri alınan düzeltme | Varsayılan | PickCodec | PickFastCodec | PerformanceProbe | HdrResolver | Toplam |
|---|---:|---:|---:|---:|---:|---:|
| Arayüz: `Unmeasured` → `NotWorking` | 1 | 0 | 0 | 0 | 0 | 1 |
| PickCodec: üçlü → iki değerli | 0 | 1 | 0 | 0 | 0 | 1 |
| PickFastCodec: üçlü → iki değerli | 0 | 0 | 1 | 0 | 0 | 1 |
| PerformanceProbe: üçlü → iki değerli | 0 | 0 | 0 | 1 | 0 | 1 |
| HdrResolver: üçlü → iki değerli | 0 | 0 | 0 | 0 | 1 | 1 |

Her mutasyon yalnız kendi ölçüsünü kırdı; diğer dört ölçü yeşil kaldı.

## K5 — Üretim çıktısı değişimi

| Girdi | Eski seçim/karar | Yeni seçim/karar |
|---|---|---|
| `DeferredEncoderAvailability`, kalite kipi, `MaxCompression`, `libsvtav1=Unmeasured` | `libx265` | `libsvtav1`, geçici |
| `DeferredEncoderAvailability`, kalite kipi, `Fast` tercihi, `h264_nvenc=Unmeasured` | `libx264` | `h264_nvenc`, geçici |
| `DeferredEncoderAvailability`, hızlı kip, `av1_nvenc=Unmeasured` | `libx264` | `av1_nvenc`, geçici |
| Üçlü üretim cevabı: `h264_nvenc=NotWorking`, `h264_qsv=Unmeasured`, `h264_amf=Working` | `h264_amf` | `h264_qsv` |
| `DeferredEncoderAvailability`, yazılım HDR, `libsvtav1=Unmeasured` | Kodek `libsvtav1`, SDR tone-map | Kodek `libsvtav1`, HDR korunur ve karar geçici |
| Doğrudan `EncoderCapabilities`, listede ve `Unmeasured` kodek | T137 sonrası `HasEncoder=true`; kodek kabul edilir | Aynı kodek kabul edilir; plan/HDR kararı geçici işaretlenir |
| Her karar yerinde `Working` | Çalışan kodek kabul edilir | Değişmedi |
| Her karar yerinde `NotWorking` | Kodek elenir | Değişmedi |

Rebase tabanı `e0eee12`; T137 tur 2 bu tabanda `EncoderCapabilities.WorksAsEncoder`ın ölçülemeyen cevabını `HasEncoder(codec)` yapmış durumda. Bu nedenle doğrudan `EncoderCapabilities` kullanan üretim yolunda seçilen kodek değişmiyor; T139 farkı süreç doğurmadan üçlü cevabı tüketmesi ve geçicilik işaretidir. Seçim değişikliği, ölçülmemiş cevabı iki değerli yüzünde `false` taşıyan `DeferredEncoderAvailability` geçidinde görülür.

Regresyon riski ölçülmemiş kodeğin sonraki gerçek yoklamada çalışmadığının anlaşılmasıdır. Plan ve HDR yolları bunu `HardwareNotMeasured` / `NotMeasured` ile geçici işaretleyip ölçüm sonrası yeniden hesaplamaya bırakıyor. Performans yolunda ise ölçülmemiş aday gerçekten başarısız olup daha sonraki aday çalışıyor olabilir; eski iki değerli yol aktif yoklamayla sonraki çalışan adayı seçtiğinde o tek koşum için doğru sonuca ulaşabiliyordu. Yeni yol kanıtsız eleme yapmıyor ve seçilen adayı performans koşumunun sınamasına bırakıyor; başarısızlık `HardwareEncoderFailed` olarak görünür.

## K6 — Verify kolları ve teslim kapısı

Her kol ayrı `dotnet test -c Release --list-tests --filter "FullyQualifiedName~..."` çağrısıyla sayıldı:

| Verify kolu | Test sayısı |
|---|---:|
| `PlanCalculatorTests` | 32 |
| `EncoderStateConsumptionTests` | 4 |
| `HdrResolverTests` | 1 |
| `PerformanceCheckTests` | 21 |
| `EncoderAvailabilityTests` | 12 |
| Toplam | 70 |

Sıfır test bulan kol yok. Genişletilen sahiplik kapsamında yalnız şu üç sahteye `EncoderState` eklendi:

1. `PlanCalculatorTests.FakeAvailability`
2. `PlanCalculatorTests.SurucusuzMakine`
3. `EncoderAvailabilityTests.Makine`

Tam suitin gösterdiği kalan üç eski sahte için genişletilen sahiplikte yine yalnız `EncoderState` eklendi:

4. `SpeedModeTests.FakeAvailability`
5. `HardwareRateControlTests.FixedAvailability`
6. `PlanCalculatorProbeTests.RecordingAvailability`

`OlculmemisMakine` değiştirilmedi. Bu sahte `IEncoderMeasurementState` ile ölçülmüşlük kanıtı taşıdığı için ortak `KnownState` köprüsü, üçlü cevap hâlâ `Unmeasured` ise ve eski arayüz “ölçüldü” diyorsa iki değerli sonucu okuyor. Böylece test iddiası ve K2'nin kanıtsız varsayılanı birlikte korunuyor.

`DonanimYoluKapatilincaKararDegisiyor` tek başına yeniden çalıştırıldığında `14 s` içinde `1/1` yeşil geçti; önceki `30231 ms` sonucu eşzamanlı yük yapıntısıydı. Zaman aşımı sabiti değiştirilmedi.

Son doğrulama:

- `dotnet build VidShrink.sln -c Release --no-incremental`: başarılı, 0 uyarı, 0 hata.
- Birleşik verify: `70/70` başarılı, 0 atlanan, süre `3 dk 50 sn`.
- Test çağrılarında `--no-build` kullanılmadı.
- Tur 1'in son push'ının CI koşumu: **`33680674698`**, `a125667`, `completed success` — `Failed: 0, Passed: 1465, Skipped: 19, Total: 1484`. Bu satır tur 1'de boş bırakılmıştı; tur 2 borcu olarak dolduruldu.

**Tur 2 düzeltmesi — kol sayıları yeniden sayıldı.** Yukarıdaki tablo `b88bb66` tabanına aitti ve `origin/main` birleştirildikten sonra bayatladı. `a125667`/`f8b33ee` sonrası gerçek sayılar:

| Verify kolu | Test sayısı (tur 1 yazılan) | Test sayısı (tur 2, ölçülen) |
|---|---:|---:|
| `PlanCalculatorTests` | 32 | 32 |
| `EncoderStateConsumptionTests` | 4 | **7** |
| `HdrResolverTests` | 1 | 1 |
| `PerformanceCheckTests` | 21 | **22** |
| `EncoderAvailabilityTests` | 12 | 12 |
| Toplam | 70 | **74** |

```
> foreach ($k in ...) { dotnet test ... -c Release --list-tests --filter $k }
PlanCalculatorTests = 32
EncoderStateConsumptionTests = 7
HdrResolverTests = 1
PerformanceCheckTests = 22
EncoderAvailabilityTests = 12
```

Sıfır test bulan kol yok; beş kolun toplamı birleşik koşumun toplamına eşit (32+7+1+22+12 = 74).

```
> dotnet build VidShrink.sln -c Release --no-incremental
    0 Uyarı
    0 Hata
> dotnet test -c Release --filter "PlanCalculatorTests|EncoderStateConsumptionTests|HdrResolverTests|PerformanceCheckTests|EncoderAvailabilityTests"
Başarılı!  - Başarısız: 0, Başarılı: 74, Atlanan: 0, Toplam: 74, Süre: 2 m 22 s
```

`EncoderStateConsumptionTests` 4 → 7 farkı K7'nin üç yeni ölçüsüdür. `PerformanceCheckTests` 21 → 22 farkı bu dalın işi değil; birleştirilen `origin/main` ile geldi.

Depo kapısı `tools/kosum-kapisi/kosum-kapisi.ps1 -MinimumTotal 1134` ile filtresiz çalıştırıldı: `1345 başarılı / 0 başarısız / 17 atlanan / toplam 1362`, süre `17 dk 49 sn`. Kapı `başarısız=0 toplam=1362 alt-sınır=1134` sonucu ile geçti. Üç dosyada test iddiası, bandı, eşiği veya test adı değiştirilmedi.

**Tur 2'de bu kapı yerelde tamamlanmadı.** Filtresiz koşum başlatıldı ama makinede eşzamanlı başka ajan koşumları vardı ve süre teslimi bloke ettiği için durduruldu; kısmi çıktı kanıt sayılmaz ve rapora yazılmadı. Tur 2'nin tam suit doğrulaması **CI'a bırakıldı** — yerelde yeşil olduğu varsayılmıyor. Yerelde tamamlanan doğrulama, yukarıdaki `74/74` birleşik verify koşumudur.


## K7 — Geçici cevap arayüze ölçülmüş gibi ulaşmıyor

Tur 1'in kararı doğruydu ve üretimde tüketiliyor; kırılan, kararın arayüze **nasıl** sızdığıydı. `PickFastCodec` ölçülmemiş adayı geçici cevap olarak döndürüyor, `MainWindow.ProbeHardwareEncodersAsync` ise cevabı yalnız kodek adına bakarak okuyordu: `CodecModel.IsHardware(plan.Codec)`. Sürücüsüz makinede aday `av1_nvenc` olduğu için arayüz "donanım var" diyor, hızlı GPU kutusu açılıyordu.

Düzeltme iki parça. `EncodePlan.CodecNotMeasured` işareti geçiciliği plana bindiriyor (`PlanResult.HardwareNotMeasured` HDR yolundaki ölçülmemişliği de topladığı için daha geniş; kodek seçimi için ayrı ve dar bir işaret gerekiyordu — üstelik `ProbeHardwareEncodersAsync` `PlanCalculator.Build` çağırıp `PlanResult`ı atıyor). `MainWindow.HardwareAvailableFrom` ise geçici cevabı, aynı gövdenin zaten çalıştırdığı gerçek yoklamayla doğruluyor:

```csharp
internal static bool HardwareAvailableFrom(EncodePlan plan, EncoderProbeResult probe)
    => CodecModel.IsHardware(plan.Codec)
       && (!plan.CodecNotMeasured || (probe.Measured && probe.Succeeded));
```

Girdi `docs/olcumler/surucu-yoklugu.md` sürücüsüz makinesi: ffmpeg `av1_nvenc`/`hevc_nvenc`/`h264_nvenc` listeliyor, önbellek soğuk, sürücü yok — `HasEncoder=true`, `WorksAsEncoder=false`, `EncoderState=Unmeasured`.

| Ölçülen | `f8b33ee` tabanı | K7 öncesi (`13d5db3`) | K7 sonrası (`931582d`) |
|---|---|---|---|
| `PickFastCodec` seçimi | `libx265`, `IsHardware=False` | `av1_nvenc`, `CodecNotMeasured=True` | `av1_nvenc`, `CodecNotMeasured=True` |
| Arayüze giden `available` | `False` | **`True`** | `False` |
| `ChkFastGpu.IsEnabled` | `False` | **`True`** | `False` |
| `_hardwareEncoderAvailable` | `False` | **`True`** | `False` |

Birinci satır tur 1'in kasıtlı kararıdır ve K7 onu değiştirmiyor: seçim aynı kalıyor, değişen yalnız o seçimin arayüze ölçülmüş bir evet gibi ulaşması. Taban sütunu `.claude/worktrees/T139-taban` (`f8b33ee`) üzerinde `T139TabanOlcusu.TabanSatir6_DogrudanEncoderCapabilitiesHizliKip` koşumundan:

```
TABAN satir6 codec=libx265 IsHardware=False
```

`ChkFastGpu.IsEnabled` pimleyen ölçü kalıcıdır — `EncoderStateConsumptionTests.OlculmemisDonanimAdayiHizliKipKutusunuAcmiyor`. Gerçek pencereyi `AppHost.Run` içinde kurup `ApplyHardwareVerdict`i sürücüsüz cevapla çağırıyor ve hem kutunun hem alanın kapalı olduğunu iddia ediyor. Yanına iki ölçü daha kondu: `OlculmemisDonanimAdayiArayuzeDonanimVarDemiyor` (geçici cevap + başarısız yoklama → donanım yok) ve `OlculmemisAdayiDogrulayanYoklamaGecerseDonanimVarDiyor` (geçici cevap + **başarılı** yoklama → donanım var). İkincisi olmadan düzeltmenin ucuz hali —`&& !plan.CodecNotMeasured`— de yeşil geçerdi; o hal çalışan GPU'lu makinede de donanımı kapatırdı, çünkü `PlanCalculator.Build` koştuğunda `EncoderCapabilities` yoklama önbelleği henüz soğuk.

## K8 — Mutasyon

Her mutasyondan önce `dotnet build VidShrink.sln -c Release --no-incremental`; testlerde `--no-build` yok. Her mutasyon uygulandıktan sonra `git diff -U0` ile dosyada gerçekten değiştiği doğrulandı (bu depoda satır sonu yüzünden sessizce uygulanmayan ikame sahte yeşil üretmişti). Her kolun kaç test bulduğu `--list-tests` ile ayrıca sayıldı; sıfır test bulan kol yok.

**Mutasyon A** — `MainWindow.HardwareAvailableFrom` içinde doğrulama şartı düşürülüyor, yani kusur birebir geri geliyor:

```
-       && (!plan.CodecNotMeasured || (probe.Measured && probe.Succeeded));
+       && true;
```

**Mutasyon B** — işaret plana hiç basılmıyor, `BuildDetailed` içindeki satır siliniyor:

```
-        result.Plan.CodecNotMeasured = probe.CodecNotMeasured;
```

| Verify kolu | Test | Mutasyon A | Mutasyon B |
|---|---:|---|---|
| `EncoderStateConsumptionTests` | 7 | **Başarısız 3, Başarılı 4** | **Başarısız 3, Başarılı 4** |
| `PlanCalculatorTests` | 32 | 32/32 yeşil | 32/32 yeşil |
| `PerformanceCheckTests` | 22 | 22/22 yeşil | 22/22 yeşil |
| `HdrResolverTests` | 1 | 1/1 yeşil | 1/1 yeşil |
| `EncoderAvailabilityTests` | 12 | 12/12 yeşil | 12/12 yeşil |

Her iki mutasyon da yalnız K7'nin kolunu kırıyor ve üçünü birden kırıyor: kutu ölçüsü, negatif yön ölçüsü ve pozitif yön ölçüsü. Mutasyon B'nin de aynı üçünü kırması, işaretin `EncodePlan` üzerinden taşınmasının ölçüldüğünü gösteriyor — `MainWindow` tarafı tek başına pimlenmiş olsaydı B yeşil kalırdı.

## K9 — K5 paragrafı yeniden yazılıyor

K5'in altındaki gerekçe paragrafı **yanlış**. Yanlış olan cümle şuydu:

> Seçim değişikliği, ölçülmemiş cevabı iki değerli yüzünde `false` taşıyan `DeferredEncoderAvailability` geçidinde görülür.

Doğrusu bunun tersi. `DeferredEncoderAvailability` `IEncoderMeasurementState` uyguluyor ve `IsMeasured(codec)` ile `EncoderState(codec) != Unmeasured` bu sınıfta aynı şeyi söylüyor; ortak `KnownState` köprüsü de zaten "eski yüz ölçüldü diyorsa iki değerli sonucu oku" kuralını işletiyor. Bu nedenle T139'un üçlü cevabı bu geçitte **hiçbir seçimi değiştirmiyor**: K5 tablosunun 1, 2, 3 ve 5. satırları bu girdide eski ile aynı çıkıyor. Değişen taraf, ölçülmüşlük yüzünü hiç taşımayan **doğrudan `EncoderCapabilities`** yolu (6. satır) ile üçlü cevabı doğrudan veren performans yolu (4. satır).

Tablo aşağıda düzeltilmiş hâliyle. Karşılaştırma tabanı `f8b33ee`; taban sütunundaki her satırın altında onu üreten koşum var.

| # | Girdi | `f8b33ee` | Dal | Değişti mi |
|---:|---|---|---|---|
| 1 | Geçit, kalite kipi, `MaxCompression`, `libsvtav1=Unmeasured` | `libsvtav1`, `notMeasured=True` | aynı | **hayır** |
| 2 | Geçit, kalite kipi, `Fast` tercihi, `h264_nvenc=Unmeasured` | `h264_nvenc`, `notMeasured=True` | aynı | **hayır** |
| 3 | Geçit, hızlı kip, `av1_nvenc=Unmeasured` | `av1_nvenc`, `notMeasured=True` | aynı | **hayır** |
| 4 | Üçlü cevap: `h264_nvenc=NotWorking`, `h264_qsv=Unmeasured`, `h264_amf=Working` | `h264_amf` | `h264_qsv` | **evet** |
| 5 | Geçit, yazılım HDR, `libsvtav1=Unmeasured` | `policyChanged=False notMeasured=True pix=yuv420p10le` | aynı | **hayır** |
| 6 | Doğrudan `EncoderCapabilities`, hızlı kip, sürücüsüz makine | `libx265`, `IsHardware=False` | `av1_nvenc`, `IsHardware=True` | **evet** |

Taban koşumu — `.claude/worktrees/T139-taban` (`f8b33ee`), `dotnet build -c Release --no-incremental` ardından `dotnet test -c Release --filter "FullyQualifiedName~T139TabanOlcusu"`:

```
TABAN satir1 codec=libsvtav1 notMeasured=True
TABAN satir2 codec=h264_nvenc notMeasured=True
TABAN satir3 codec=av1_nvenc notMeasured=True
TABAN satir4 selected=h264_amf
TABAN satir5 policyChanged=False notMeasured=True pix=yuv420p10le
TABAN satir6 codec=libx265 IsHardware=False
```

6. satır T139 tur 2'nin KRİTİK'ini üreten satırdır: sürücüsüz makinede seçim yazılımdan donanıma geçiyor. Tur 1 bu geçişi kasten yapmıştı; kusur geçişin kendisinde değil, K7'de yazıldığı gibi geçici seçimin arayüze ölçülmüş gibi ulaşmasındaydı. K7'den sonra 6. satırın seçimi hâlâ `av1_nvenc` — değişen, o seçimin arayüzde donanım varlığına çevrilmemesi.

K5 tablosu ve paragrafı yukarıda olduğu gibi duruyor; bu bölüm onu iptal eder.

## Kapanmayanlar

Tur 2 kapsamında **kapatılmayan**, ölçülmüş iki nokta:

1. **`PickFastCodec` ilk ölçülmemiş adayda duruyor.** `av1_nvenc`in çalışmadığı ama `hevc_nvenc`in çalıştığı makinede taban kod aday listesinde ilerleyip `hevc_nvenc`i buluyordu; tur 1'den beri yeni kod ilk adayı geçici cevap olarak döndürüyor, `ProbeHardwareEncodersAsync` o tek adayı yokluyor ve başarısız olunca donanım yok görünüyor. Bu yanlış-negatif K7'nin ürünü değil, tur 1 kararının ürünü; yeniden deneme döngüsünü yazmak tur 2'nin sahipliğinde değil. Ayrı sözleşme gerekiyor.
2. **Sözleşme metnindeki gerekçe yanlış.** Tur 2 metni "`EncoderCapabilities` `EncoderState`i uygulamıyor" diyor; `src/VidShrink.Ffmpeg/EncoderCapabilities.cs:108` uyguluyor ve listede olup yoklanmamış kodek için `Unmeasured` döndürüyor. Sonuç aynı, gerekçe farklı.
