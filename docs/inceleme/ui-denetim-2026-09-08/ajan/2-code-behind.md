# Ajan 2 — MainWindow.axaml.cs denetimi (ham çıktı, 137.082 token, 283 s)

Kısaltma: `MW` = `src/VidShrink.App/MainWindow.axaml.cs`

**Kırık**

- MW:3157,3163 — `BuildUniqueOutputPath` yalnız kaynak dizinini kullanır; "sabit çıktı klasörü" ayarı (`TxtOutputFolder`, `AppSettings.OutputFolder`) kaydedilir ama hiçbir yol üretimi okumaz (depoda tek tüketici yok) — `CmbOutputFolderMode==1` iken `dir`i `TxtOutputFolder.Text`ten al.
- MW:1026,1072 — "elle ffmpeg yolu" ayarı da yalnız kaydedilip doğrulanır; `ToolLocator`da setter yok (`ToolLocator.cs:9-13`), motor hep PATH'tekini kullanır — `ToolLocator`a `Use(path)` ekle, `RestoreAppSettings`/`OnFfmpegPathTextChanged`ten çağır.
- MW:2441-2470,2586-2596 — yeni dosya yüklenince `_lastOutput` sıfırlanmaz; `RefreshPreviewSource` yeni kaynağı **eski** çıktıyla karşılaştırır, `BtnReveal`/`BtnShare` eski dosyayı gösterir — `ApplyLoaded` başında `_lastOutput=null; BtnReveal.IsVisible=false; ResetShare(false)`.
- MW:2244,3232 — `ParseTargetMb`/`ParseQualityTarget` yalnız InvariantCulture; "1,5" ya da "abc" sessizce 16 MB / kaydırıcı değerine düşer, hata basılmaz, `SaveSettings` (865) bu sahte değeri kalıcılaştırır — parse hatasında `TxtQualityTargetNotice` benzeri satır göster, ondalık virgülü kabul et (`Replace(',', '.')`).
- MW:3753-3760 — `ApplyQualityRange`: kutu değeri aralık dışıysa kaydırıcı kırpılır ama kutu yazılmaz (`_syncing` geri yazımı bastırır); plan kutudan okur (3666) → aralık dışı CRF/50'nin altı kbps motora gider, `Validate` (ConversionArguments.cs:7-30) CRF aralığını denetlemez — kırpılan değeri kutuya geri yaz ya da `RefreshConversion`da hata üret.

**Tutarsız**

- MW:568 sözleşme "sayı tek yerden geçiyor" der; MW:2571,2907,2911,2918,2951,2957,2962-2963,2782,3562,3573 `$"{x:0.0}"` ile CurrentCulture kullanır (tr-TR'de "12,3 MB", `Num` "12.3") — handoff notu doğru; hepsini `Num(...)`e çevir.
- MW:566-567 — `Num`un üstünde iki `<summary>` (biri `Speak`in öksüz yorumu) — yorumu `Speak`e taşı.
- MW:1040-1044,1030 — `TxtDefaultTargetMb` ↔ `TxtTarget` tek yönlü: varsayılan kutusu hedefi yazar, hedef/`ApplyLoaded`(2463) varsayılanı güncellemez; `AppSettings`te ayrı alan yok — ya alanı kaldır ya `CaptureAppSettings`e `DefaultTargetMb` ekle ve `ApplyLoaded` onu okusun.
- MW:2690-2707 — `TxtSystemStatus` sekiz yerden yazılan tek yuva: `ReportUnsettledProbe` her `Recalculate`te hata/araç metnini ezer, probe metni kalkınca `string.Empty` yazar (`UpdateToolStatus` çağırmaz) — probe durumu için ayrı `TextBlock` ya da temizlerken `UpdateToolStatus()`.
- MW:471-473 — `OnWindowLoaded` sıralı `await`: açılış dosyası varken donanım yoklaması karmaşıklık ölçümü + kalibrasyon bitene dek beklenir; ilk plan donanımsız kurulur — `LoadStartupFileAsync`i `_ =` ile ayır ya da yoklamayı öne al.
- MW:3548,2514,2549 — `OnStart` `_probeCts`i iptal eder, `OperationCanceledException` boş yutulur, `TxtEstimateNote` "ölçülüyor/kalibre" metninde kalır — iptal dalında `RefreshEstimateView()` çağır.
- MW:3563 — kalan süre `mm\:ss`; 60 dk üstü saat düşer ("1:05:00" → "05:00") — `HumanDuration` ya da `hh\:mm\:ss`.
- MW:2326-2334 — bırakma reddi (`Reject`) tek metin: koşum sürerken (2285 `_cts is not null`) de "tek dosya / klasör değil" der — sebep için üçüncü anahtar (`main.drop.busy`).
- MW:3720-3723 — `TimeSpan.TryParse` "1:30"u 1 sa 30 dk okur; kullanıcı 1 dk 30 sn bekler, hata "başlangıç kaynak sonundan sonra" olur — `mm:ss` / `hh:mm:ss` için özel parser.
- MW:876 vs 3666 — aynı kutu (`TxtQuality`) biri kültürsüz, biri Invariant parse — 876'yı Invariant yap.
- MW:3674 — `TxtCustomResolution` `int.TryParse` kültürsüz/`NumberStyles`siz, dosyanın geri kalanıyla çelişir — `NumberStyles.Integer, Invariant`.

**Kozmetik**

- MW:3404-3417 — performans ölçümü `CancellationToken.None`, iptal düğmesi yok; uzun sürer — `_cts` benzeri CTS ve iptal düğmesi.
- MW:2259-2261 — koşum sürerken `OnBrowse` sessizce döner, düğme (`BtnBrowseEmpty`, 249/255/801) etkin kalır — `SetRunning`de kapat.
- MW:1611-1632 — `OnShareDelete` iptal/zaman aşımı yok (`DeleteAsync` `default` CT) — `UpdateProbeTimeout` benzeri CTS ver. (default, unmeasured)
- MW:4040-4050 — `paylasim-hedefleri.json` bozuksa sessizce `Fallback`; kullanıcı öğrenmez — `TxtShareStatus`a tek satır.
- MW:2266 — dosya seçici türü `"Media"` sabit İngilizce — `Say("main.pick.media")`.
- MW:441 — `AppLogo` `Bitmap` hiç dispose edilmez (pencere ömrü, sızıntı değil) — `OnClosing`de dispose.
- MW:216-224 — `TxtTarget` vb. her tuş vuruşunda `SaveSettings` (disk yazımı); 206 her vuruşta `File.Exists` — `_recalculateTimer` gibi geciktir.
- MW:2570 — `TxtDuration` `hh\:mm\:ss`, 24 sa üstü sarar — `HumanDuration`.

**Bulunamayanlar (kanıt yok)** — UI iş parçacığı ihlali: `Task.Run` sonuçları hep `await`/`Dispatcher.UIThread.Post|InvokeAsync` üzerinden (648, 1692, 2172, 3617); `Progress<T>` UI bağlamında kurulu. `async void` işleyicilerden `OnShare`/`OnShareDelete`/`OnDrop` try'sız ama çağırdıkları (`UploadAsync` MultipartUploadProvider.cs:82, `LoadAsync` 2368) tüm istisnaları yakalar. Çift tık: `_cts` ilk `await`ten önce atanır (3550, 3801), `SetRunning` düğmeleri kapatır. Timer/CTS: `_recalculateTimer` tik'te durur (431), CTS'ler `finally`de dispose (2559, 3603, 3830, 4169); `Strings.Changed` 492'de bırakılır.

**Boyut — ayrılabilir sorumluluklar**

1. `DeferredEncoderAvailability` (1780-2050, iç sınıf) + `HardwareAvailableFrom`/`ProbeHardwareEncodersAsync` → `Encoding/EncoderProbeGate.cs`.
2. `ShareTarget`, `ShareTargetTable`, `ShareFlow`, `DescribeBytes`, `OnShare*` (1426-1643, 3982-4206) → `Share/ShareTargetTable.cs` + `Share/ShareFlow.cs` + `MainWindow.Share.cs` (partial).
3. Ayar geri yükleme/yakalama (819-1075, 2107-2150 splitter) → `MainWindow.Settings.cs` (partial).
4. Dönüştürme sekmesi (`ReadConversionPlan`, `ParseTime`, `ApplyQualityRange`, `RefreshConversion`, `OnConvert`, 3662-3834) → `MainWindow.Convert.cs` (partial).
5. Görsel yardımcılar (`Fade`, `Pulse`, `Motion`, `Scalar`, `Paint`, `Look`, panel girişi, drop görseli, 255-450, 2306-2347) → `Ui/MotionHelpers.cs` (static, `Control` uzantıları).
