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
