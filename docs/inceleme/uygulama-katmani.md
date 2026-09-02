# Uygulama Katmanı İncelemesi

Kapsam: `MainWindow.xaml.cs` (553), `App.xaml.cs` (20), `AssemblyInfo.cs` (10),
`LanguageCatalog.cs` (122). Salt okuma; hiçbir kaynak dosya değiştirilmedi.

## 1. Ne yapıyor

`MainWindow` tek parça code-behind: pencere kromu, dil, kaynak yükleme, plan, kodlama, dönüştürme,
AI JSON köprüsü aynı sınıfta. `App.xaml.cs` açılışta yalnız `TempCleanup` çağırır (17, `catch {}`
ile susturulmuş); `AssemblyInfo.cs` şablon `ThemeInfo`'dan ibaret.

Akış: `Loaded` (48) → `SetLanguage` + `CheckTools` + donanım yoklaması → `OnBrowse`/`OnDrop` →
`LoadAsync` (166) `FfprobeClient.ProbeAsync` ile `_info` doldurur, hedefi 16 MB'a kurar →
`Recalculate` (252) `PlanCalculator.BuildDetailed` ile `_autoPlan`/`_advice` üretir →
`MeasureComplexityAsync` (200) arka planda `ComplexityProbe` + `CalibrationProbe` çalıştırıp planı
yeniden kurar → `OnStart` (427) `EncodeRunner.RunAsync`, `OnConvert` (529) `ConvertAsync`; ilerleme
`Progress<EncodeProgress>` ile. `LanguageCatalog` 112 çift tutar; `SetLanguage` (75) görsel ağacı
gezip metinleri yerinde değiştirir, çalışma zamanı metinleri `T(tr, en)` (70) ile üretilir.

## 2. Arayüz iş parçacığı ve iptal

**Kaynak yükleme hatası hiç görünmüyor — en ağır bulgu.** `TxtStatusBar` XAML'de
`Visibility="Collapsed"` (`MainWindow.xaml:404`). Probe başarısız olduğunda tek geri bildirim
`MainWindow.xaml.cs:176`'ya yazılıyor: bozuk dosya bırakıldığında dosya adı görünür, `BtnStart`
sessizce kapanır (174), sebep hiçbir yerde yazmaz. `CheckTools` (131-133) "ffmpeg bulunamadı"
mesajı da aynı gizli alana gider; yalnız Hakkında sekmesindeki `TxtSystemStatus` (134) kalır.

**UI kilitlenmesi — evet, iki ayrı yolda.**
- `CheckTools:127` → `ToolLocator.GetFfmpegVersion()` (`ToolLocator.cs:21-28`) UI iş parçacığında
  ffmpeg açıp `WaitForExit(3000)` yapar. Açılışta (51) ve **her dil değişiminde** (86) çalışır.
- `Recalculate:255` → `PlanCalculator.BuildDetailed(..., EncoderCapabilities.Instance)`. `Instance`
  bir `Lazy` (`EncoderCapabilities.cs:8`), `Load` üç ffmpeg süreci çalıştırır (111-113, her biri
  `WaitForExit(5000)`). Hızlı Düşür açıkken `PlanCalculator.cs:486-488` `WorksAsEncoder` döngüsüne
  girer; her aday `lock (_probed)` altında 4 sn'ye kadar test kodlaması yapar
  (`EncoderCapabilities.cs:54`). Bu kilit arka plandaki yoklamayla (`MainWindow.xaml.cs:92`)
  çakışırsa hedef kutusuna basılan **her tuş** (400) UI'ı saniyelerce dondurur.

**`async void` ve yutulan istisna.**
- `MainWindow.xaml.cs:52` `_ = ProbeHardwareEncodersAsync();` — beklenmeyen görev. `92`'deki
  `Task.Run` atarsa istisna hiç gözlenmez, `_hardwareEncoderAvailable` `true` kalır (33), `ChkFastGpu`
  yanlışlıkla açık kalır — tam anlamıyla yutulan istisna.
- `OnBrowse` (141) ve `OnDrop` (153) `async void`. `LoadAsync`'in `try` bloğu yalnız probe'u sarar
  (170-178); `ShowInfo` (185), `Recalculate` (195), `RefreshConversion` (196) korumasız —
  `RefreshPlanView:283`'teki `FfmpegArguments.Build` atarsa uygulama düşer, `App.xaml.cs` içinde
  `DispatcherUnhandledException` yok. `OnStart` (466) / `OnConvert` (536) `catch (Exception)`'lı.

**`CancellationTokenSource` atılması.**
- `_cts`: 466 ve 536'daki `finally` her yolda `Dispose()` + `null` yapar — doğru. **Ama kurulum
  `try` dışında** (`439` ve `533`): bu satırlarda istisna olursa `_cts` non-null kalır,
  `SetRunning(false)` çağrılmaz, `BtnStart` (272: `_cts is null` şartı) kalıcı kapanır.
- `_probeCts`: `202-205` bir öncekini iptal edip atıyor, fakat **son örnek hiç atılmıyor** —
  dosya başına bir CTS sızıntısı. `438` iptal ediyor ama atmıyor/null'lamıyor. `Closing` işleyicisi
  de yok: kodlama sürerken uygulama kapatılırsa `_cts` iptal edilmez,
  `EncodeRunner.cs:269`'daki `ct.Register(() => TryKill(process))` tetiklenmez, ffmpeg süreci öksüz kalır.

**İki kere Başlat.** Güvenli: `429`–`439` arası tamamen eşzamanlı, ilk `await` `443`'te; WPF ikinci
tıklamayı araya sokamaz, `SetRunning(true)` (539) düğmeyi kapatır. **Fakat üç gerçek boşluk var:**
1. `SetRunning(false)` (539) `BtnConvert.IsEnabled = !running && _info is not null` diyor; doğrulama
   şartını (`525`: `errors.Count == 0 && _cts is null`) atlıyor ve `OnStart`'ın `finally`'si (466)
   `RefreshConversion()` çağırmıyor (`OnConvert`'inki 536 çağırıyor). Geçersiz kırpma zamanıyla
   Küçült bitirildiğinde Dönüştür düğmesi geçersiz planla açılıyor.
2. `Gözat`/`Bırak` kodlama sırasında kapatılmıyor (141, 153); yeni dosya bırakılırsa `_info` (170)
   değişir ve `449`'daki sonuç mesajı **yeni** dosyanın boyutuna göre yüzde hesaplar.
3. `_cts` tek alan, hem Küçült hem Dönüştür kullanıyor (439, 533); ikinci atama olursa ilk CTS
   öksüz kalır, `OnCancel` (550) yalnız yenisini iptal eder. Bugün yalnız düğme durumu koruyor.

## 3. Kullanıcıya gösterilen hata

Üç ayrı yol var, ikisi ham İngilizce metin döküyor.

- **Ham ffmpeg stderr.** `EncodeRunner.cs:224` çıkış kodu sıfır değilse
  `InvalidOperationException($"ffmpeg failed ({exit}):\n{son 15 stderr satırı}")` atıyor (kuyruk
  `182-193`). `MainWindow.xaml.cs:466` `catch (Exception ex) { TxtResult.Text = ex.Message; }` ile
  doğrudan panele basıyor. Türkçe arayüzde görülen tipik metin:
  `ffmpeg failed (1):` + `[libx264 @ ...] height not divisible by 2 (1280x721)` +
  `Error initializing output stream 0:0 -- Error while opening encoder...`. Anlamlı mesaj yok,
  çeviri yok. Aynısı `536` (Dönüştür) ve `526` (`TxtConvertValidation.Text = ex.Message`) için de.
- **Anlamlı mesaj (tek iyi örnek).** Hedefin altına inilemediğinde `452-457` `CeilingExceeded`
  bayrağını yakalayıp iki dilde açıklayıcı metin veriyor, Core'un İngilizce `result.Error` metnini
  (`EncodeRunner.cs:103`) kullanmıyor. Disk alanı kontrolü (432-436) de aynı kalitede.
- **Çevrilmemiş `Error`.** `462` `TxtResult.Text = result.Error;` — bu dala yalnız
  `EncodeRunner.cs:116` düşüyor, metni `"Encoding loop ended unexpectedly."`. Türkçe arayüzde de
  İngilizce; ayrıca `Error` `string?` olduğu için nullable uyarısı taşıyor.

## 4. Çeviri bütünlüğü

- **Katalog dengeli, XAML tarafı tam:** 112 anahtar, 112 farklı değer — `TurkishToEnglish` ters
  sözlüğü (`LanguageCatalog.cs:121`) çakışma vermiyor. XAML'deki 152 metin sabitinin katalog dışında
  kalanların hepsi marka/kısaltma/sayı (`VidShrink`, `FPS`, `MP4`, `1080`); çevrilmemiş prose yok.
- **Düzeltme notu (T126):** Bu madde eskiden `LanguageCatalog.cs:7`'yi bir çeviri sözlüğündeki ölü
  bileşik anahtar (`"Target Size Media Compression & Media Converter"`) olarak gösteriyordu — o dizge
  kaynakta hiç yok, `LanguageCatalog.cs:7` bir `// T27:` yorum satırı. **`LanguageCatalog.cs` hiçbir
  zaman çeviri sözlüğü olmadı**, yalnız başlık büyütme yardımcısı (`Title(text, turkish)`, sabit-yazım
  tablosu `Names`); çeviriler gerçekte `src/VidShrink.App/Locales/{en,tr}/*.json` altındaki JSON
  dosyalarında yaşıyor. O dosyalarda gerçekten ölü olan 3 anahtar var:
  `main.plan.fact.estimated-size`, `main.plan.reasons-count`, `main.quality.loss-points`
  (`Locales/en/main.json:126,117,70`) — hiçbir `.axaml`/`.cs` dosyasında çağrılmıyor, kod yerine
  benzer adlı aktif anahtarları kullanıyor: `main.plan.fact.estimate`, `main.plan.reasons`,
  `main.quality.loss`+`main.quality.points` (`MainWindow.xaml.cs:1788,1796,1704`).
- **Kod içinde gömülü İngilizce (4 yer):** `MainWindow.xaml.cs:522` `"Trim times must use HH:MM:SS
  format."`; `524` `ConversionArguments.Validate` çıktısı (`ConversionArguments.cs:10-31`, hepsi
  yalnız İngilizce); `312` `plan.Reason` (`PlanCalculator.cs:216`); `462`/`466`/`526`/`536` istisna.
- **Kapsam boşlukları.** `DescribeStrategy` (358-375) 17 `AdviceCode`'un 12'sini karşılıyor;
  `BudgetIsGenerous` (`PlanCalculator.cs:159`), `ResolutionReduced` (121), `FrameRateReduced` (127),
  `AudioReduced` (450), `TargetEnforcedTwoPass` (202) `_ => null` ile sessizce düşüyor.
  `DescribeReason` (317-331) 13 `ReasonCode`'un 12'sini karşılıyor; `HardwareBitrateBias`
  (`PlanCalculator.cs:506`) düşüyor, yalnız o kod varsa gerekçe paneli boş kalıyor.
- **`T(...)` tutarlılığı.** `T(türkçe, ingilizce)` sırası her yerde aynı, hata yok. İki yerde
  `_turkish ? ... : ...` üçlüsü doğrudan kullanılmış (`131-133`, `448-450`), kalıp bölünmüş. Ayrıca
  `T($"...", $"...")` her çağrıda iki dizeyi de biçimlendiriyor (319-330, 353).
- **Dil değişiminde tazelenmeyenler.** `SetLanguage:87` yalnız `ShowInfo`/`Recalculate`/
  `RefreshConversion` çağırıyor; `TxtResult` (449), `TxtConvertResult` (535), `TxtAiStatus` (420) ve
  `TxtStage` (440) eski dilde kalıyor.

## 5. Kaçak kopya ve sabitler

| App | Core karşılığı | Durum |
| --- | --- | --- |
| `MainWindow.xaml.cs:35` `HardwareEncoderOrder` | `PlanCalculator.cs:45-48` `FastHardwareOrder` | 7 eleman **birebir aynı**, iki ayrı yerde |
| `MainWindow.xaml.cs:508-509` `CrfMinimum`/`CrfMaximum` (vp9 4-63, av1 1-63, diğer 0-51) | `CodecModel.cs:56-60` `CrfRange` (av1 18-55, diğer 10-45) | Aynı kavram, **iki farklı tablo**; App ham ffmpeg aralığını, Core planlayıcı aralığını tutuyor |
| `MainWindow.xaml.cs:472` kodek indeksi→adı eşlemesi | `PlanParser.cs:13` `AllowedCodecs`, `ConversionArguments.cs:131-133` | Kodek adı sözlüğü üçe bölünmüş |
| `MainWindow.xaml.cs:478` ses kodeki eşlemesi | `PlanParser.cs:14` `AllowedAudioCodecs` | `pcm_s16le` yalnız App'te |
| `MainWindow.xaml.cs:479` `mp3`/`m4a`/`wav` kapsayıcı listesi | `ConversionPlan.cs:20` `AudioOnly` | Aynı liste iki yerde |
| `MainWindow.xaml.cs:480` `Crf 23`, `VideoBitrateK 2500`, `AudioBitrateK 128` | `ConversionPlan.cs:10,11,16` aynı varsayılanlar | Core varsayılanları App her zaman ezdiği için ölü |
| `MainWindow.xaml.cs:475` `{2160,1440,1080,720,480}` | Core'da yok; `MainWindow.xaml:302-306` ComboBoxItem'ları | Merdiven XAML ile koda kopyalanmış, indeksle bağlı |
| `MainWindow.xaml.cs:29` `WhatsAppTargetMb = 16` | Core'da karşılığı yok | Sabitin kendisi `139`'da tekrar `16` olarak yazılmış (kendi sabitini kullanmıyor) |
| `MainWindow.xaml.cs:471` `((ComboBoxItem)CmbContainer.SelectedItem).Content` | `TranslateTree:113` `ContentControl.Content`'i çeviriyor | Bugün çakışma yok; katalog anahtarıyla eşleşen bir kapsayıcı adı eklenirse Türkçe ad ffmpeg'e gider |
