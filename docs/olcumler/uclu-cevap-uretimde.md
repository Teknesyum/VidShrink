# Üçlü cevap üretimde

## K1 — Kusurun ölçümü

Taban: `b88bb66`. Önce `dotnet build VidShrink.sln -c Release --no-incremental`, sonra her ölçü `dotnet test -c Release --filter ...` ile ve `--no-build` kullanılmadan çalıştırıldı. `10f9073` commit'inde dört ölçünün dördü de kırmızıydı.

| Karar yeri | Ölçü | Eski sonuç |
|---|---|---|
| `PlanCalculator.PickCodec` | `PickCodecOlculmemisTercihiElemedenGeciriyor` | Beklenen `libsvtav1`, gelen `libx265` |
| `PlanCalculator.PickFastCodec` | `PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor` | Beklenen `av1_nvenc`, gelen `libx264` |
| `PerformanceProbe.SelectHardwareCodec` | `PerformanceProbeOlculmemisAdayiCalismiyorSaymiyor` | Beklenen `h264_qsv`, gelen `h264_amf` |
| `HdrResolver` | `SoftwareHdrOlculmemisKodlayiciyiElemedenGeciriyor` | Beklenen HDR koruma, gelen tone-map |

## K2 — Arayüz varsayılanı

`IEncoderAvailability.EncoderState` varsayılanı `Unmeasured` döndürüyor ve süreç doğuran `WorksAsEncoder`ı çağırmıyor. Üçüncü durumu taşıyamayan uygulayıcı için hem `true` hem `false` iki değerli cevap yetersiz kanıttır; varsayılan bu nedenle olumlu veya olumsuz hüküm kurmuyor. `EncoderCapabilities` ile `MainWindow.DeferredEncoderAvailability` üçlü cevabı bildikleri için kendi gerçekleştirmelerini kullanıyor.

Tabandaki varsayılanı kullanan 19 test sahtesi etkileniyor:

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
15. `PlanCalculatorProbeTests.FlakyThenWorkingAvailability`
16. `PlanCalculatorTests.FakeAvailability`
17. `PlanCalculatorTests.SurucusuzMakine`
18. `PlanCalculatorTests.OlculmemisMakine`
19. `SpeedModeTests.FakeAvailability`

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

## K6 — Verify kolları ve açık engel

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

`OlculmemisMakine` değiştirilmedi. Bu sahte `IEncoderMeasurementState` ile ölçülmüşlük kanıtı taşıdığı için ortak `KnownState` köprüsü, üçlü cevap hâlâ `Unmeasured` ise ve eski arayüz “ölçüldü” diyorsa iki değerli sonucu okuyor. Böylece test iddiası ve K2'nin kanıtsız varsayılanı birlikte korunuyor.

`DonanimYoluKapatilincaKararDegisiyor` tek başına yeniden çalıştırıldığında `14 s` içinde `1/1` yeşil geçti; önceki `30231 ms` sonucu eşzamanlı yük yapıntısıydı. Zaman aşımı sabiti değiştirilmedi.

Son doğrulama:

- `dotnet build VidShrink.sln -c Release --no-incremental`: başarılı, 0 uyarı, 0 hata.
- Birleşik verify: `70/70` başarılı, 0 atlanan, süre `3 dk 50 sn`.
- Test çağrılarında `--no-build` kullanılmadı.
- Rebase sonrası kod ucu CI koşumu `33657686105` (`e768faa`): ilk ve tek kontrolde sürüyordu; sözleşme gereği beklenmedi.

Depo kapısı olan filtresiz `dotnet test VidShrink.sln -c Release` de çalıştırıldı: `1330 başarılı / 14 başarısız / 18 atlanan`, süre `35 dk 43 sn`. On dört kırmızı üç izin dışı eski iki değerli sahteye dağılıyor:

| Sahte | Kırmızı ölçü sayısı |
|---|---:|
| `SpeedModeTests.FakeAvailability` | 7 |
| `HardwareRateControlTests.FixedAvailability` | 1 |
| `PlanCalculatorProbeTests.RecordingAvailability` | 6 |
| Toplam | 14 |

Bu ölçülerin iddiaları değiştirilmedi. Üç sahteye `EncoderState` eklemek ölçülerin temsilini üçlü sözleşmeyle hizalar; ancak dosyaları verilen genişletilmiş sahiplikte değildir ve `PlanCalculatorProbeTests.cs` için ayrıca dokunmama sınırı vardır. Bu nedenle sözleşme filtresi yeşil olsa da depo tam suit kapısı yeşil değildir; T139 teslim edilmiş sayılmaz.
