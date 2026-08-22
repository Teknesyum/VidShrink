# Test Kapsamı İncelemesi

16 dosya, 147 geçen + 1 atlanan (`FillBandTests.cs:470`, `VIDSHRINK_LIVE_SOURCE` bağlı).
Test projesi yalnız `Core` ve `Ffmpeg`'e referans veriyor (`VidShrink.Tests.csproj:18-19`); `VidShrink.App` hiç test edilmiyor.

## 1. Ne kapsıyor

- `PlanCalculatorTests` / `EncoderCapabilitiesTests` — kodek düşüşü, gerekçe kodları, ffmpeg `-encoders` ayrıştırma.
- `SpeedModeTests` — Fast kip kodek sırası, donanım preset geçerliliği, 1.06 kör sapma, kalibrasyon, altın plan tablosu.
- `FillBandTests` — bant sınıfları, doldurma politikası, `Correct`/`RetryAimMb`/`MeasuredEncoderEfficiency`, iki canlı `EncodeRunner` turu.
- `HardwareEncoderTests` — NVENC/AMF/QSV argüman biçimi, `NeedsTwoPasses`, kodek katsayıları, MOV/AV1 reddi.
- `HdrArgumentsTests` — HDR10/HLG koruma, 10-bit yokken tonemap, SDR'ın etkilenmemesi.
- `ComplexityScanTests` / `WindowSamplingTests` — tarama noktaları, `ComputeScanBias`/`ComputeWindowBias`, `ParseVstats`/`ParsePackets`, tahmin bandı.
- `CalibrationProbeTests` — iki noktadan halving step çözümü, kelepçe, imza uyuşmazlığı, bozuk ölçümde geri düşme.
- `ConversionArgumentsTests` — GIF palet grafiği, tek boyut / konteyner-kodek / trim doğrulaması.
- `EncodeRunnerAtomicOutputTests` — parçalı dosya adı, başarısızlıkta artık bırakmama, başarıda partial temizliği.
- `DiskSpaceGuardTests` (yalnız aritmetik), `TempCleanupTests` (artık silme), `QualityMeterTests` (VMAF NEG akıl sağlığı).

## 2. Kapsam boşlukları

1. **Yedek dosyaya düşme** — `EncodeRunner.cs:95-100`. Bant altı bir denemeden kalan `fallbackPath`, son denemede tavan
   aşıldığında teslim ediliyor; bu dalı hiçbir test yürütmüyor.
   *Test:* FillTarget'ta önce bant altı, sonra iki kez tavan üstü sonuç veren bir koşuda `Success == true`,
   `UnderBand == true` ve izde "fallback to the last under-band result" beklensin.
2. **İptalde temizlik** — `EncodeRunner.cs:118-122` ve `TryKill` (`:236`) hiç çalıştırılmıyor; testlerin hepsi
   `CancellationToken.None` kullanıyor.
   *Test:* uzun bir lavfi kaynağını yarıda iptal edip `OperationCanceledException` atıldığını ve klasörde
   `vidshrink_partial_*` ile pass log kalmadığını doğrula.
3. **Disk dolu** — guard yalnız aritmetik olarak test ediliyor (`DiskSpaceGuardTests.cs:12-22`); tek çağrı yeri
   `MainWindow.xaml.cs:432` ve test projesi App'e referans vermiyor. Kodlama ortasında disk dolarsa yol
   `EncodeRunner.cs:123`'e düşer, o da doğrulanmamış.
   *Test:* guard kararını App'ten bağımsız çağırıp "yer yok"ta kodlamanın başlamadığını, ayrıca ffmpeg sıfırdan farklı
   çıkışla bittiğinde partial'ın silindiğini doğrula.
4. **Bozuk kaynak** — `EncodeRunnerAtomicOutputTests.cs:42-46` yalnız *var olmayan* dosyayı deniyor; içi bozuk bir dosya
   `FfprobeClient` ve `ComplexityProbe` yollarında hiç denenmiyor.
   *Test:* rastgele baytlardan `.mp4` üretip `FfprobeClient.ProbeAsync`'in anlaşılır hata verdiğini ve `EncodeRunner`'ın
   geriye dosya bırakmadığını doğrula.
5. **Çok kısa video** — en kısa fikstür 2 sn (`FillBandTests.cs:353`). `Math.Max(info.DurationSeconds, 0.1)`
   (`PlanCalculator.cs:173`) ve `MinVideoBitrateK` kelepçesi sınanmıyor.
   *Test:* 0.2 sn / birkaç karelik kaynakta planın NaN veya sonsuz bitrate üretmediğini ve tavanı aşmadığını doğrula.
6. **Sessiz video** — README (`README.md:90`) açıkça vaat ediyor; `AudioCodec = null` yalnız `FillBandTests.cs:453`'te
   plan alanı olarak var, ses akışı olmayan `MediaInfo` ile plan üretimi test edilmiyor.
   *Test:* `AudioCodec = null, AudioChannels = 0` bir `MediaInfo` ile `AudioBitrateK = 0` çıktığını ve
   `FfmpegArguments.Build` sonucunda `-c:a` yerine `-an` bulunduğunu doğrula.
7. **Sert tavan sözü kısmen korunuyor** — `FillBandTests.cs:405-468` yalnız tek yolu kanıtlıyor (üç deneme, yedek yok,
   dosya yazılmaz). `ToleranceOver = 1.0` sınırı, yani çıktının tam hedefe eşit olduğu durum, test edilmiyor.
   *Test:* çıktı tam `targetMb` iken `over` sayılmadığını ve dosyanın teslim edildiğini doğrula.
8. **Üç denemenin ara adımları** — "tavan üstü → düzelt → yine tavan üstü → düzelt" zinciri yalnız canlı ffmpeg testinde
   dolaylı geçiyor; `Correct`'in ardışık çağrılarda yakınsadığını gösteren saf test yok.
   *Test:* sahte ölçümlerle `Correct`'i üç kez zincirleyip her adımda tahminin tavanın altına indiğini doğrula.
9. **Doğrudan testi hiç olmayan birimler:** `PromptBuilder`, `HdrResolver`, `LanguageCatalog`, `ToolLocator`,
   `CompressionStrategy`; `FfprobeClient` yalnız atlanan canlı testte geçiyor.

## 3. Kırılgan testler

- `FillBandTests.cs:335`, `:407`, `EncodeRunnerAtomicOutputTests.cs:11`, `:62`, `QualityMeterTests.cs:11-12` —
  `if (!ToolLocator.IsAvailable(out _)) return;`. ffmpeg yoksa bu testler **atlanmış değil, yeşil** görünür.
  147 sayısı ffmpeg'siz makinede de aynı kalır ama beş test hiçbir şey doğrulamamış olur.
- `FillBandTests.cs:337`, `:409`, `EncodeRunnerAtomicOutputTests.cs:64`, `TempCleanupTests.cs:10`,
  `QualityMeterTests.cs:14` — gerçek `Path.GetTempPath()` klasörleri. `TempCleanupTests.cs:30` temizliği
  `try/catch`'siz `Directory.Delete` ile yapıyor; dosya kilitliyse test kırılır.
- `FillBandTests.cs:391-399` — libx264'ün gerçek çıktısına bağlı. 320x240 / CRF 40 kaynağın 5 MB hedefte bant altı
  kalması bir ffmpeg sürümü varsayımı; `result.Attempts > 1` ve `underBandRetries.Count <= 2` başka sürümde tutmayabilir.
- `FillBandTests.cs:193` `MeasuredLibx264Yield = 0.9815` — tek makinede ölçülmüş sabit. `:279` bant kontrolü bu sayı
  değişirse kırılır.
- `QualityMeterTests.cs:24` (`VMAF >= 95`) ve `:49` (fark > 20) — libvmaf model sürümüne bağlı eşikler; `:12` ayrıca
  `EncoderCapabilities.Instance` üzerinden makinedeki gerçek ffmpeg'e bakıyor.
- `FillBandTests.cs:477-480` — canlı test çıktıyı `Desktop\vidshrink_live` altına yazar; yönlendirilmiş Desktop'ta yol
  çözülmeyebilir.
- Zamana bağlı test yok; `Stopwatch` yalnız bench'te.

## 4. Altın anlık görüntüler

Beklenen çıktıyı birebir tutanlar:

- `SpeedModeTests.cs:275-295` — 18 satırlık plan tablosu (kodek / mod / CRF / bitrate / çözünürlük / preset).
- `FillBandTests.cs:41-55` — bant sınıfı sabitleri (180/50/25/10/8/1).
- `HardwareEncoderTests.cs:144-168` — `QualityLimit`, `RelativeBitrateNeed`, `ReferenceCrf` sabitleri.
- `CalibrationProbeTests.cs:116-120`, `ComplexityScanTests.cs:166-210`, `WindowSamplingTests.cs:127-130` —
  tahmin bandı sabitleri (0.32 / 0.14 / 0.08 / 0.05).
- `DiskSpaceGuardTests.cs:8-21` — `hedef*3 + 200 MB` formülü.

**≥50 MB hedeflerde ilk planı iki geçişe çevirme — doğrulandı, şunları kırar:**

- `SpeedModeTests.cs:277-278, 283-284, 289-290` — tablodaki altı `180` satırı bugün `crf` modunda
  (`"Compatible|180|FillTarget|libx264|crf|19|11927|128|1152x648@30|slow"`). Bu satırlar `PlanCalculator.cs:164-166`
  yolundan geliyor: bütçe şeffaflık tavanını aştığı için `plan.Mode = "crf"` ve `plan.Crf` atanıyor. Kural değişirse
  aynı satırların üç kolonu birden değişir — `mode` `crf`→`2pass`, `crf` kolonu `19/20/31/32`→boş, `videoBitrateK`
  bütçeden yeniden hesaplandığı için `11927/10992/11021` kayar. 25 ve 8 MB satırları zaten `2pass`, etkilenmez.
- `FillBandTests.cs:87-107` (hedef 120) — `:106` satırı `ReasonCode.FillCrfLowered` bekliyor. Bu kod yalnız CRF dalında
  üretiliyor (`PlanCalculator.cs:185`); plan baştan iki geçiş olursa hiç üretilmez. `:96-97`'deki fikstür varsayımı
  ("tavan bandın altında kalır") da anlamını yitirir.
- `FillBandTests.cs:110-126` (hedef 5000) — `ReasonCode.FillTwoPassBandCenter` ve "CRF floor" metni bekliyor
  (`PlanCalculator.cs:194`). Yeni kural bu dala girmeden iki geçişe geçerse hem kod hem metin kaybolur.
- `FillBandTests.cs:57-68` ve `:70-85` (hedef 60) — eşiğin hemen üstünde. `:66`'daki `target + 0.5` sınırının
  `TwoPassUncertainty` ile birlikte hâlâ tuttuğu yeniden doğrulanmalı.
- Etkilenmeyenler: `HdrArgumentsTests` (40 MB), `PlanCalculatorTests` / `EncoderCapabilitiesTests` (25 MB),
  `SpeedModeTests.cs:131-143` preset geçerliliği (mod bağımsız).

## 5. Bench aracı

`tools/VidShrink.Bench/Program.cs` üç komut veriyor: `measure` (VMAF/XPSNR), `shrink` (hedef listesini kodlar, JSON yazar),
`compare` (iki JSON'u yan yana basar). README'deki ölçüm iddialarını bugün **üretemez**:

1. `Program.cs:95` — `RunAsync` `fillPolicy` verilmeden çağrılıyor, yani varsayılan `QualityCeiling`. Uygulamanın
   varsayılanı `FillTarget` (`MainWindow.xaml.cs:138`). "**Budget fill 92–99%**" (`README.md:86`) doldurma politikasının
   ölçüsüdür; bench onu ölçmüyor.
2. `Program.cs:84` — `ComplexityProbe` var, `CalibrationProbe` yok; uygulama (`MainWindow.xaml.cs:217`) ve canlı test
   (`FillBandTests.cs:487`) kalibre edip profili `EncodeRunner`'a geçiriyor. Bench profil de geçirmiyor (`Program.cs:95`).
3. `BenchResult` (`Program.cs:168-181`) — `Attempts` alanı ve tahmini boyut alanı yok. "**8 of 8 runs landed under target
   on the first attempt**" ve "**Size estimate accurate to within 8%**" (`README.md:85`) için gereken veri hiç
   kaydedilmiyor; `planResult.Estimate` (`Program.cs:90`) okunup atılıyor.
4. `Program.cs:98` — `encodeResult.Success` kontrol edilmiyor. Tavan aşıldığında `EncodeRunner.cs:102` dosya yazmaz;
   `QualityMeter.MeasureAsync` olmayan dosyaya bakar ve tüm koşu düşer.
5. `Program.cs:139-149` — `compare` kayıtları hedefe göre değil **sıraya göre** eşliyor; farklı hedef listeleriyle alınan
   iki koşu sessizce yanlış eşlenir.
6. `Program.cs:94-96` — süre tek atış, ısınma turu ve tekrar yok; `EncodeSeconds` makineler arası kıyas için güvenilmez.
   `Program.cs:122` dosya adında yerel saat kullanıyor.

Kalite sayıları (`VmafNegHarmonic`, `VmafNegP10`, `Xpsnr`) güvenilir: `QualityMeter.cs:106` referansı test çözünürlüğüne
`zscale` ile hizaladığı için ölçek düşüren planlar doğru kıyaslanıyor.
