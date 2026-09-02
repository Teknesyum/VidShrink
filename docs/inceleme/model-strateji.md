# Model ve Strateji Katmanı — Kapsam İncelemesi

Kapsam: `CompressionStrategy.cs`, `ComplexityProfile.cs`, `EncodePlan.cs`, `ConversionPlan.cs`,
`MediaInfo.cs`, `IEncoderAvailability.cs`. Salt okuma; kaynağa dokunulmadı.

## 1. Ne yapıyor

`MediaInfo` ffprobe künyesini taşır. `CompressionStrategy` kaynak/hedef oranından bir rejim üretir
(Light/Balanced/Aggressive/Extreme) ve politika anahtarları verir: kodek tercihi, çözünürlük/fps
düşürme izni, ses bütçe payı, amaca göre şeffaflık CRF kayması. `ComplexityProfile` bit ihtiyacını `ReferenceBppf` (bit/piksel/kare) olarak tutar; ölçümle
(`FromProbe`) ya da kaynak bit hızından tahminle (`FromSourceBitrate`) dolar, iki noktalı
`Calibrate` ile kodlayıcıya oturur, `BppfAtCrf`/`CrfForBppf` ile CRF↔boyut çevrimini sağlar.
`EncodePlan` ffmpeg'e giden karar paketi, `ConversionPlan` dönüştürme sekmesinin ayrı paketi,
`IEncoderAvailability` kodek seçimini ffmpeg yapısına bağlayan soyutlama.

## 2. Doğruluk kusurları

### 2.1 MiB→kbit katsayısı hatalı, sistematik %2.9 az bit (yüksek)
`MediaInfo.cs:28` MiB veriyor; `PlanCalculator.cs:92,175,290,313` MiB→kbit için **8192**
kullanıyor. Oysa `FfmpegArguments.cs:71` bitrate'i `-b:v {N}k` yazıyor ve ffmpeg'de `k` = 1000'dir,
doğrusu 8388.608. Motor her hedefte istediğinin %2.4 altını istiyor; `ContainerOverhead = 0.995`
(`PlanCalculator.cs:35`) ikinci gizli payı ekliyor, fill/retry aritmetiğinin tamamı iki kez pay
bırakılmış bir taban üzerinde çalışıyor.

### 2.2 `FromSourceBitrate` yedek tahmini (yüksek)
`ComplexityProfile.cs:79-92`. `:81`'de `TotalBitrateBps - AudioBitrateBps` yapılıyor, ama toplam
kapsayıcı düzeyidir (`FfprobeClient.cs:67`) ve **yalnızca ilk ses akışı** çıkarılıyor (`:79`) —
çok dilli ses veya gömülü altyazıda video bit hızı yukarı sapar. Ses bit hızı okunamazsa 128 kbps
varsayılıyor; `format.bit_rate` yoksa `fileSize*8/duration`'a düşülüyor, kapsayıcı yükü video sayılıyor.

Temel kusur: **bit hızı karmaşıklık değildir.** Zaten sıkıştırılmış kaynak (WhatsApp'tan gelen
video) düşük bppf verir, motor "sade içerik" der, `CrfForBppf` (`:145`) fazla yüksek CRF önerir;
ters yönde intra/ProRes kaynak `:86`'daki 1.5 tavanına doyar. Clamp'lar tutarsız — tahmin
`[0.004, 1.5]` (`:86`), ölçüm `[0.002, 2.0]` (`:105`). Bandı `EstimatedBand = 0.32` (`:48`):
16 MB hedefte kullanıcıya 10.9–21.1 MB gösteriliyor (`MainWindow.xaml.cs:298`). Ölçüm başarısızsa
CRF modunda hedef tutturma garantisi yok, yalnızca retry kurtarıyor.

### 2.3 Rejim eşikleri sınırda sert (yüksek)
`CompressionStrategy.cs:41-43` — 1.5 / 6.0 / 30.0, histerezis yok. Oran 5.999→6.001 geçişinde
**aynı anda** `AutoPreference` Compatible→MaxCompression (`:50-55`, libx264→libx265) ve
`AllowsFpsDrop` false→true (`:59-60`) oluyor: kaynağın birkaç KB büyümesi kodek ve kare hızını
değiştiriyor. 1.5 sınırında `AllowsResolutionDrop` (`:57`) açılıp kapanıyor. Oran ayrıca video
baskısından değil **dosya boyutundan** hesaplanıyor (`PlanCalculator.cs:56`).

### 2.4 Sıfır/eksik girdi (orta)
`CompressionStrategy.cs:48`: `targetMb <= 0` → oran 1.0 → sessizce **Light**;
`PlanCalculator.cs:92`'de `totalK` 0 çıkıp `MinVideoBitrateK` tabanına düşüyor, yani motor "hafif
sıkıştırma" derken en düşük bitrate'i üretiyor. Süre `FfprobeClient.cs:51-52`'de ≤0'da atılıyor
ama `MediaInfo` kendi doğrulamasını içermiyor. `ComplexityProfile.cs:132`'de `sourceFps` 0 ise
`Math.Max(sourceFps, 0.1)` yüzünden `temporal` çöküyor, gereken bppf gerçeğin altına iniyor;
`:81`'deki `Math.Max(1.0, ...)` ses toplamı aşarsa videoyu 1 bps yapıyor ve `:86` clamp'i 0.004'e
çıkarıyor. Üçünde de kelepçe sorunu gizliyor, işaretlemiyor.

### 2.5 Kalibrasyonun sessizce düşmesi (orta)
`Matches` toleransı ölçekte ±0.005, fps'te ±0.01 (`ComplexityProfile.cs:5-6,14-17`). Plan çift
sayıya yuvarlanmış çözünürlük yüzünden ölçeği 0.006 kaydırırsa `AppliesTo` (`:154`) false döner:
`LevelFactor` ve ölçülmüş `HalvingStep` devre dışı kalır, band 0.05'ten 0.14'e sıçrar (`:74,77`),
sebep bildirilmez. `EstimateBandFor` (`:77`) kalibre ama `Measured=false` profil için 0.32
döndürüyor, çünkü `Calibrate` (`:157-183`) `Measured` bayrağını güncellemiyor.

### 2.6 Kalibrasyon matematiği kırılgan (orta)
`FromProbe` `ReferenceBppf`'i bias'a **böler** (`:101`), `Calibrate` modeli `WindowDomainFactor`
ile **çarpar** (`:170`); bu ancak iki prob aynı pencerelerde ölçerse tutar, oysa pencere
üreticileri ayrı fonksiyonlarda (`ComplexityProbe.cs:82`, `CalibrationProbe.cs:101`) ve bağ kodda
ifade edilmemiş. `:98` `DetailExponent`'i `[-0.2, 1.4]`'e kelepçeliyor — negatif üs, yarı ölçek
probunun tam ölçekten **büyük** çıkması demek: gürültü reddedilmiyor, `ScaleFactor` (`:118-127`)
ölçek küçüldükçe bppf'i artırıyor. `LevelFactor` `[0.1, 10.0]` (`:40-41`) için de aynı.

**`EncodePlan` (düşük):** `:69` `ModeEnum` "crf" dışındaki **her** dizeyi TwoPass sayıyor — plan
JSON'undaki yazım hatası sessizce iki geçişe dönüyor. `:54` `Crf` `int?` ama model sürekli CRF
üretiyor; 0.4'lük yuvarlama `CrfHalvingStep`=6 ölçeğinde ~%5 boyut sapmasıdır. `:63-64`
`HdrVideoFilter`/`HdrColorArgs` `[JsonIgnore]`; plan JSON'a yazılıp okunursa HDR kararı kayboluyor.

## 3. MediaInfo birimleri

`MediaInfo.cs:28` `FileSizeBytes / 1024.0 / 1024.0` → **MiB**. Proje içi tutarlı: `EncodeRunner.cs:58,159,207`
çıktıyı MiB ölçüyor, `DiskSpaceGuard.cs:9` MiB ayırıyor, `PlanCalculator` hesabı MiB'de yapıyor.

**Dışa dönük tutarsızlık var.** Kullanıcıya her yerde "MB" yazılıyor
(`MainWindow.xaml.cs:235, 297, 298, 447, 449-450, 456-457, 535`) ama sayı MiB; 25 MiB = 26.21
ondalık MB, fark %4.86. Bu doğrudan `:456-457`'deki "hedeften büyük dosya asla verilmez" sözünü
ilgilendiriyor: hedef MiB olarak zorlanıyor, platform sınırı ondalık MB ise söz ihlal ediliyor.
Etkilenen ön ayarlar Gmail 25 (`MainWindow.xaml:111`), Discord 8 (`:110`), WhatsApp 16
(`MainWindow.xaml.cs:29`). §2.1'deki 8192 hatası kısmen dengeliyor — iki geçişte çıktı hedefin
~%97'sine oturuyor, 25 hedefte ~24.3 MiB ≈ 25.4 ondalık MB, hâlâ %1.9 aşım ve CRF modunda daha
fazla. Ters yönlü iki hata güvenlik sağlamıyor, asıl kusuru gizliyor: **hedefin hangi birimde
olduğu hiçbir yerde tanımlı değil.**

## 4. Sabitler

Hiçbiri ölçüme bağlanmamış, hiçbirinin yanında kaynak yok: rejim eşikleri 1.5 / 6.0 / 30.0
(`CompressionStrategy.cs:41-43`), ses bütçe payları 0.30 / 0.25 / 0.18 / 0.12 (`:64-67`),
şeffaflık kayması -6.0 / -3.0 (`:72-73`), `SourceSlackFactor = 0.82` (`ComplexityProfile.cs:34`
— `FromSourceBitrate`'in tek ayar vidası), `DefaultDetailExponent = 0.55` ve `LowScaleDamping =
0.3` (`:35-36`), belirsizlik bantları 0.05 / 0.08 / 0.14 / 0.32 (`:45-48`), kelepçeler `[-0.2,1.4]`
`[3,12]` `[0.1,10]` `[0.5,2.0]` (`:36-43`), kodek tabloları (`CodecModel.cs:20-32, 63-74`).
Bant değerleri testlerde sabitlenmiş (`CalibrationProbeTests.cs:116-120`,
`ComplexityScanTests.cs:166-175`, `WindowSamplingTests.cs:127-129`) — değerin **doğru** olduğu
değil **değişmediği** doğrulanıyor.

## 5. Sadeleştirme ve ölü kod

**Gerçek hata:** `ConversionPlan.HdrPolicy` (`ConversionPlan.cs:19`) üretimde hiç atanmıyor —
`MainWindow.xaml.cs:480` alanı boş bırakıyor, yalnızca testler set ediyor
(`HdrArgumentsTests.cs:135,147`). `ConversionArguments.cs:75` bu değeri kullandığından
**dönüştürme sekmesindeki HDR seçimi ne olursa olsun daima `Preserve` uygulanıyor.**

Hiç okunmayanlar: `MediaInfo.Pixels` (`MediaInfo.cs:29`), `SizeEstimate.SpreadRatio`
(`ComplexityProfile.cs:193`), `StrategyAdvice.SuggestedCodec` / `SuggestedPreference` (`CompressionStrategy.cs:31-32`,
`PlanCalculator.cs:220`'de doldurulup okunmuyor), `SampledSeconds` / `SampledFrames` (`:53-54`). Üretilip yutulan tavsiyeler:
`BudgetIsGenerous`, `ResolutionReduced`, `FrameRateReduced`, `AudioReduced`, `TargetEnforcedTwoPass`
(`CompressionStrategy.cs:9,14-16,18`) `PlanCalculator.cs:159,121,127,450,202`'de üretiliyor ama
`MainWindow.xaml.cs:375`'teki `_ => null` ile sessizce düşüyor.

Ölü dal: `CompressionStrategy.cs:48` — `targetMb <= 0` zaten yakalandığı için `Math.Max(targetMb,
0.001)` ulaşılamaz; `RegimeFor` (`:38-45`) ile `Ratio` (`:47-48`) aynı oranı iki farklı formülle
hesaplıyor. Tuzak varsayılan: `ComplexityProfile.WindowBias` varsayılanı 1.0
(`:58`) ve bu `WindowBiasKnown`'ı (`:63`) doğru yapıyor; `FromSourceBitrate` bilerek 0.0'a çekiyor
(`:89`), ama `with` ile üretilen profil korumasız kalıp 0.14 bandını hak etmeden alır.
`IEncoderAvailability.cs` temiz: iki üye, tek uygulayıcı (`EncoderCapabilities.cs:6`).
