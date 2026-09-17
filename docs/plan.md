# macOS 13-14 — MPVKit Denemesi

Dal `t0/macos-mpvkit`. Karar: fable 2026-09-17 soru 5. Kod (`src/`) değişmez.

1. **Pin.** `tools/mpvkit-macos/mpvkit-1.0.0.lock`: MPVKit 1.0.0 LGPL ürününün 29 zip'i, sha256.
2. **Ölçüm ve bağlama.** `mpvkit-macos.sh`: minos/lipo tablosu, evrensel `libmpv.2.dylib` bağlama.
3. **CI.** `macos-mpvkit.yml`: macos-15, macos-14, macos-15-intel; duman + `OynaticiMotorTests` iki test; negatif kontroller.
4. **Belge.** `docs/olcumler/libmpv-macos-gomme.md` yeni bölüm. Kurucu/release bağlantısı ayrı karar (deps sürümü gerekir).

# HandBrake A1 — Filtre Zinciri

Dal `t0/hb-a1-filtre`. Kaynak: `.calisma/hb3/acik-durumu-2026-09-17.md` satır 31-39, 63; fable K7, K8, B9.

1. **Core.** Yeni `src/VidShrink.Core/VideoFilterChain.cs`: `VideoFilterOptions` (varsayılan: deinterlace koşullu,
   kırpma ve diğer her filtre kapalı), zincir sırası, idet kararı, `PlannedSource` (kırpma/döndürme/detelecine
   boyutu). `FfmpegArguments.Build` filtre yeri tek çağrıya iner. `PlanOptions.Filters` → `EncodePlan.Filters`;
   filtre açıkken passthrough yok. `MediaInfo.FieldOrder` ffprobe'dan.
2. **Ffmpeg.** `InterlaceProbe.cs` (belirsiz field_order + h264/mpeg2/dv → idet), `CropProbe.cs` (10 nokta,
   limit=24, mod birleştirme; `siyah-kenar.md`). `EncodeRunner` Auto kararı koşudan önce çözer.
3. **Bench.** `shrink --filters <tanım>`; CLI başka ajanın alanı, dokunulmaz.
4. **Testler.** `VideoFilterChainTests` (argüman + negatif kontrol), `FiltreYoklamaTests` (gerçek ffmpeg, ≤3 sn).
5. **B9.** `hb.ps1 -Is filtre` + `handbrake-kiyas.yml` haritası; kural önce `docs/olcumler/handbrake-filtre.md`.
6. **K8 bu turda kullanıcıya teslim edilmedi.** `CropProbe`, `EncodePlan.SuggestedCrop` ve `PlanOptions.DetectedCrop`
   çalışır ve testli, ama üretimde tüketicisi yok: kırpma varsayılan kapalı, yoklama sonucu yalnız öneri.
   Öneriyi gösteren ve tek tıkla uygulayan yüzey C1'in işi — **motor hazır, kullanıcı yolu C1'de.** Ölü yüzey
   `OluUyeTests` içindeki `OzellikScan` ölçüsünde gerekçeli borç satırlarıyla pimli.
7. **Kalan tek kol (borç).** B9 doğrulama koşumu 35265321818'de `parlak`/`acik` kolu süre eşiğini 0,05 puan
   aştı (+%5,05); kural gevşetilmedi. Sebep `parlak` kesitinin bütçe döngüsünün 3-4 deneme arası oynaması,
   çözüm ölçüm düzeneğinde (deneme sayısını sabitlemek ya da tekrar sayısını artırıp medyan almak).
   Ayrıntı `docs/olcumler/handbrake-filtre.md`.


# A3 İzle Denetim Düzeltmesi

Dal `t0/hb-a3-izle`. Kaynak: `docs/danisma/2026-09-17-a3-izle-denetim.md` (iki denetim turunun bulguları).

1. `Cli/Locales/en.json`, `tr.json`: yardım tek kez; `CliTests` her satırı tam bir kez sayar. Çıkış kodu 4, `--cikti` klasör, NDJSON.
2. `Core/WatchFolder.cs`: iki ardışık aralıkta sabit damga; kodlama başı/sonu damga kıyası, değişince çıktı silinir;
   durum yeri adayları (izlenen, çıktı, ayar klasörü, yol özeti); yazım hatası izlemeyi durdurmaz; hata kaydı sonraki
   başlatmada bir kez yeniden denenir; `_shrunk` eleme yalnız durumdaki çıktı adlarına; Linux'ta harf duyarlı kıyas; Flush(true).
3. `Cli/CliApp.cs`, `CliRequest.cs`: durum yeri, `--bir-kez` hata kodu 4, yeni olay mesajları, satır başına JSON.
4. `tests/WatchFolderTests.cs`: Ctrl+C 130, salt okunur iki koşu, büyüyen dosya, yeniden deneme, eleme günlüğü.
5. `README.md`: izle belgesi.

# Yol D — Açılış Paneli Hiçbir Yolda Yok

Dal `t0/yol-d-panel`. Kaynak: `.calisma/hb3/yol-haritasi-kalanlar-2026-09-17.md` §7 satır 2, K14.

1. **Panel kalkar.** `Launcher/Splash.cs`, `tools/VidShrink.SplashGen`, `SplashTests.cs` `trash/`'e;
   csproj görüntü hedefi ve sln satırı düşer. Başlatıcının ilerleme parametreleri ve tavanları gider.
2. **Bakım arkada.** Başlatıcı uygulamayı önce doğurur; onarım, sürüm işareti, indirme ve kurulum
   arkasından. Yalnız yarım kalmış kopya günlüğü (çökme artığı) açılıştan önce, sessiz tamamlanır.
3. **Yarış kapısı.** `Launcher/UygulamaKlasoruKapisi.cs` (App'e bağlı): app klasörüne yazan kapıyı
   (klasöre özgü adlı mutex) tutar ve klasörden koşan uygulama süreçleri bitmeden yazmaz; kapı tutulurken
   ya da günlük dururken doğrudan açılan uygulama başlatıcıya devredip çıkar. Otomatik kurulum uygulama
   kapanınca uygulanır.
4. **Hata bildirimi.** Kurulum/taşıma düşerse `app\.bakim-hatasi` yazılır; uygulama açılınca güncelleme
   panelinde `main.update.maintenance-failed` ile söyler, işareti siler. Anahtar 42 dilde.
5. **Kanıt.** `tools/VidShrink.SahteUygulama` (AssemblyName VidShrink.App) ile `.calisma` altında sahte
   kurulum: gecikme kancası `VIDSHRINK_BAKIM_GECIKMESI_MS`, `EnumWindows` ile pencere yok, uygulama
   kanca bitmeden doğdu; iki yarış senaryosu, her biri mutasyonla kırmızı. Negatif kontrol eski kodla.

**Ölçülen (17 Eylül 2026, 4000 ms kanca, 3700 ms boyunca 20 ms'de bir EnumWindows):**

| Başlatıcı | Görünür pencere sınıfı | İlk pencere | Uygulama doğumu |
|---|---|---|---|
| Eski (0bc86188, kanca SplashGate içinde) | 1 — `VidShrinkSplash` | 519 ms | 3700 ms içinde yok |
| Yeni (bu dal) | 0 | — | 107 ms |


# Yol A — Oynatıcı: Döndürme Tuşu, Sürüklerken Mıknatıs, Hız Adımı, Çift Tık Süresi

Dal `t0/yol-a-oynatici`. Kaynak: `.calisma/hb3/yol-haritasi-kalanlar-2026-09-17.md` (1. bölüm, P1, P17, 7/1-7-8).

1. **Döndürme bulunur.** Tuş GOM'daki gibi Ctrl+Shift+S kalır. Kısayolu olan her menü satırının ipucu
   "ad (tuş)". Test ham sağ tık (satır görünür, tuş metni ve ipucu), ham Ctrl+Shift+S, kare pikselleri
   (`OynaticiOdakYoluTests`).
2. **P2 mıknatıs sürüklerken.** Windows'ta yerel `BeginMoveDrag` kalır (Aero Snap), mıknatıs WM_MOVING'de
   dikdörtgene uygulanır; öbür platformlarda kendi taşıma döngüsü, yakalama kaybı ve tuşsuz hareket onu bitirir.
   Konum saf `DragPosition`/`SnapRect`, boyut pencerenin ekranının ölçeğiyle. Test ham fare ve gerçek WM_MOVING.
3. **P17/P1.** `Keymap.SpeedStep` 0,05; `ClickArbiter.DoubleWindowMs` sistemden (Windows `GetDoubleClickTime`,
   öbürlerinde Avalonia platform ayarı), sahte kaynakla gerçek zamanlayıcı ölçülür.

# Ön Ayar Kütüphanesi — HandBrake A2

Dal `t0/hb-a2-onayar`. Kaynak: `.calisma/hb3/acik-durumu-2026-09-17.md` satır 42-45.

1. **Tek tablo.** `src/VidShrink.Core/Presets/platformlar.json` gömülü kaynak; yonga planları (`MainWindow.ChipPlans`)
   buradan okunur. Discord/Telegram/e-posta platformları ve cihaz profilleri aynı tabloya, her değerin kaynağı
   `docs/olcumler/onayar-kaynaklari.md`.
2. **Kullanıcı ön ayarı.** `PresetLibrary` şema sürümlü kaydet/içe/dışa aktar; ayar klasörü `VIDSHRINK_SETTINGS_PATH`
   ile aynı yer. Hata kodu + 42 dilde `main.preset.error.*`.
3. **HandBrake çevirisi.** `HandBrakePresetImport`: taşınan/yaklaşık/düşen alan notları, `main.preset.handbrake.*`.
4. **Testler.** `OnAyarKutuphanesiTests`, `KullaniciOnAyarTests`, `HandBrakeOnAyarCeviriTests`; her kol negatif kontrol
   ve mutasyon.

# VideoToolbox Hızlı Kip — Plan Yolu

Dal `t0/vt-hizli`. Karar: `fable-kararlar-2026-09-17.md` soru 1. Kapı önce `docs/olcumler/videotoolbox-hizli.md`.

1. **Kapı belgesi** ölçümden önce commit'lenir.
2. **Core.** `PlanCalculator`: Hızlı kip aday sırası macOS'ta `hevc_videotoolbox` ile başlar, başka platformda
   listede yok; platform `BuildDetailed`'in açık bir aşırı yüklemesiyle verilir (varsayılan `OperatingSystem.IsMacOS()`).
   VT `-crf` almadığı için planın `crf` kipi VT'de `2pass` (tek geçiş bit hızı) olur. `CodecModel.IsFastHardware`:
   donanım yolu seçimi (IsHardware + hevc_videotoolbox); `IsHardware` VT'yi dışarıda tutmaya devam eder.
   `HardwareVerdict.Decide` ve kodlayıcı yolu sabitlemesi yeni üyeyi okur. `PlanParser` VT'yi yalnız macOS'ta ve
   yoklama `Working` derken kabul eder.
3. **Arayüz.** `MainWindow.HardwareAvailableFrom` tek satır: Hızlı kutusu macOS'ta VT ile açılabilsin.
4. **Testler** `VideoToolboxHizliTests`, `PlanParserTests`; her kol mutasyonla.
5. **Ölçüm** `tools/kalite-paketi-3/hb.ps1` `vthizli` işi, `handbrake-kiyas.yml` macos-15. Kalırsa plan yolu geri alınır.

**Sonuç:** koşum 35249123754 kapıdan kaldı (K2 2/8, K4 5/8); 2-4. adımlar geri alındı, ölçüm düzeneği kaldı.
**Denetim borçları:** kapı karşılaştırması ham değere çekildi (K2 yine 2/8), `KomutSatiri` bench günlüğünü
gerçek komuta bağladı, `VtHizli` kapı kalınca fırlatıyor.

# A4 — arm64 Yayın ve Kurucu Bağımlılıkları

Dal `t0/hb-a45-arm-kiyas`. Kaynak: `.calisma/hb3/acik-durumu-2026-09-17.md` 4. bölüm A4, satır 56.

1. **Yayın matrisi.** `release.yml` `publish` matrisine `win-arm64` ve `linux-arm64`; başlatıcı adımı
   `startsWith(matrix.rid, 'win-')` ile arm64'te de koşar. `UpdateCheck.ReleasedRids` altı hedefe çıkar,
   `KabukAciklariTests` pini birlikte. Kurucu exe ve kabuk uzantısı x64'te kalır (COM DLL arm64 değil).
2. **RID seçimi.** `UpdateCheck.RidFor(platform, arch)` ayrılır, `Rid` onu çağırır; test arm64 makinenin
   `win-arm64`/`linux-arm64` varlığını seçtiğini ve varlığın yayın listesinde olduğunu ölçer.
3. **Kurucu kaynakları.** `SetupModel` pinleri mimari başına: `FfmpegPin.For`, `LibMpvPin.For`.
   ffmpeg win-arm64 BtbN'in ay sonu `autobuild-2026-08-31-13-27` etiketinden (kayan `latest` değil),
   libmpv aarch64 kendi `deps-libmpv-20260903` yayınımızdan, yedek shinchiro. `SetupRunner.RuntimeIdentifier`
   arm64'ü kabul eder.
4. **Betikler.** `Install-VidShrink.ps1` arm64 kolu (pinli ffmpeg zip + aarch64 libmpv, `-DepsOnly` ile
   yalnız bağımlılık kolu), `install-vidshrink.sh` `aarch64` kolu (paket yöneticisi önerisi).
5. **CI.** `release.yml` workflow_dispatch'te `windows-11-arm`: `-DepsOnly` gerçekten koşar, `ffmpeg -version`,
   `NativeLibrary.Load` ile libmpv duman testi, bozuk sha256 negatif kontrolü.
6. **Ölçüm düzeltmesi.** `hb.ps1` SVT kolunun HandBrake preset eşlemesi (x265 adı → SVT sayısı),
   yalnız SVT hücreleri yeniden koşulur, `docs/olcumler/handbrake-kiyas-cli.md` eski satırı geçersiz işaretler.

# Kaydedici C Grubu — R10/R14 Vurgu, R2 macOS/Linux Pencere, R15 Kanıt

Dal `t0/yol-c-kaydedici`. Kaynak: `.calisma/hb3/yol-haritasi-kalanlar-2026-09-17.md` 2., 6., 7. bölüm (9-11).

1. **R10/R14.** `RecorderView.axaml` sonuç paneli: "Küçült'e gönder" ilk sırada `PrimaryButton`, "Klasörü göster"
   ve "Paylaş" `GhostButton`. Test: başsız çizimden düğme pikseli, palet fırçası `NeonBlue` ile kıyas; negatif kontrol eşit tema.
2. **R15.** Paylaş düğmesine ham fare tıkı, sahte sağlayıcıyla `ShareFlow`'dan geçen yol ve ekrandaki bağlantı geri okunur.
3. **R2 Core.** `RecorderRequest.WindowRegion` + `RecorderArguments.WindowCrop`: macOS pencere dikdörtgeni avfoundation
   ekranından `crop`; dikdörtgensiz macOS pencere isteği yine reddedilir.
4. **R2 App.** `RecorderWindowsX11.cs` (`xwininfo -root -tree` ayrıştırıcı, yoksa libX11 `_NET_CLIENT_LIST`; Wayland reddi),
   `RecorderWindowsMac.cs` (CGWindowList). `RecorderView` başlığı kimliğe/dikdörtgene çözer, Linux'ta `DISPLAY`'i geçirir.
   İki yeni anahtar 42 dilde, `BiciminTests` sayımı.
5. **CI.** `ci.yml`'e ubuntu işi: Xvfb + xlogo, `_NET_CLIENT_LIST` elle, filtreli test x11grab 2 sn + ffprobe.
6. **Belge.** `docs/plan-kaydedici-dalgalari.md` durum sütunu kanıt testleriyle.

# Bütçe Doldurma — Yukarı Deneme

Dal `t0/butce-doldur`. Kaynak: `docs/olcumler/nvenc-2.md` (ort %4,8 boş bütçe). Kural önce `docs/olcumler/butce-doldur.md`.

1. **Karar Core'da.** Yeni `src/VidShrink.Core/BudgetFill.cs`: teslim hedefin %97'sinin altındaysa bir yukarı
   deneme planı (ölçülen noktadan ölçek, tavan üstü örnek varsa aradeğerle sınır), yukarı denemenin tutulup
   tutulmayacağı ve deneme bütçesi (sınır + 1). `PlanCalculator`'a boyut↔kbit için iki açık yardımcı.
2. **Koşucu.** `EncodeRunner` üç teslim noktasında (bantta, son denemede tavan aşımı sonrası dolu yedek,
   bant altı kabul) yukarı denemeyi bir kez koşar; tavanı aşarsa önceki sonuç teslim edilir. Bilerek durma,
   doygunluk, ölü verim, kullanıcı kararı ve tavan bekçisi yolları dokunulmaz.
3. **Testler.** `tests/VidShrink.Tests/BudgetFillTests.cs`, her kol için negatif kontrol.
4. **Ölçüm.** `tools/butce-doldur/kos.ps1`: nvenc-2'nin |sapma| > %3 hücreleri + libx264/libx265 6 hücre,
   önce/sonra Bench, VMAF-NEG. Sonuç `docs/olcumler/butce-doldur.md`.

# Karanlık İçerikte x265 Geçişi

Dal `t0/karanlik-x265`. Karar: `docs/danisma/2026-09-17-karanlik-x265-fable.md`. Ölçüm:
`docs/olcumler/karanlik-x265.md`. Ağır kodlama yalnız CI'da (`handbrake-kiyas.yml`).

1. **YAVG ölçümü.** `hb.ps1 -Is yavg`: dört kesitte ürün sondasının penceresiyle YAVG; ayrım <2× ise dur.
2. **Sonda.** `ComplexityProbe` pencerelerde signalstats YAVG okur (`LumaArgs`, `ParseMeanLuma`);
   `ComplexityProfile.MeanLuma`. Ayrıştırıcı testi sabit satırı pimler.
3. **Karar.** `DarkContentSwitch` (Core, saf): Auto + kilitsiz + Aggressive/Extreme + motor libsvtav1
   + MeanLuma < eşik → libx265, turbo ilk geçiş; `ReasonCode.DarkContentHevc`. Beş test kolu, her kola
   mutasyon.
4. **CI kabulü.** `hb.ps1 -Is karanlikgecis`: urun-otomatik vs main bench vs HandBrake; CAMBI(ii) ≤7,5,
   süre ≤1,5× HB, karanlık dışı libsvtav1 ve main ile eş.
5. **Belge.** `docs/kullanim.md` kodek ipucu.
6. **Açıklar (dal `t0/karanlik-acik`).** Strateji önerisinin kodeği geçişi izler; HDR (PQ/HLG)
   kaynakta geçiş yok; luma kolu bölünmüş sonda sürecine katıldı (ayrı üç ffmpeg süreci gitti).

# HandBrake Dalga 2 — Ölçülen Açıklar

Dal `t0/hb-2-aciklar`. Kaynak: dalga 1b ölçümü (koşum 35158725446,
`docs/olcumler/handbrake-kiyas-b1..b6*.md`). Danışma: `.calisma/danisma/hb2-soru.md` / `hb2-yanit.md`.
Ağır kodlama yalnız CI'da; yerelde ≤3 sn klip ve filtreli test.

1. **Bench özeti (açık 6).** `tools/VidShrink.Bench/Program.cs` özet satırı teslim edilen
   denemenin kipini `encodeResult.PlanUsed`'dan yazar.
2. **FPS düşürme otomatik plandan çıkar (açık 3).** `PlanCalculator.SearchLayout`/`FpsCandidates`
   kaynak fps'in altına yalnız kaynak fps'te hiçbir ölçek `RunnableVideoBitrateK`'yı geçmediğinde
   iner. Test: hareketli benzeri Aggressive vaka kaynak fps'te kalır; negatif kontrol: bütçe
   çalışabilir tabanın altındaysa fps yine düşer.
3. **Düşük hedef teslimi (açık 5).** `EncodeRunner` + `PlanCalculator.Correct`: verim < 0.5 →
   yeniden deneme yok, doygun teslim; bitrate düşüşü boyutu küçültmüyorsa sonraki deneme
   çözünürlük basamağı iner; bant doluluğu çöküşü (ör. %3,3) kabul yerine yeniden deneme.
4. **Bütçe doldurma (açık 7).** Verim ölçülmüş 2pass yazılım denemesinde hedef bant merkezi yerine
   üst yarıya kayar; social işiyle ölçülür.
5. **VT yolu (açık 1).** VT yazılım gibi planlanmaz: tek geçiş, `-preset` yok, CI ölçümüyle
   seçilen 10 bit / hız bayrakları. NVENC sabitleri taşınmaz (T149). Testler bilerek güncellenir.
6. **SVT-AV1 karanlık bantlaşma (açık 2) ve x265 turbo ilk geçişi (açık 4).** Önce
   `tools/kalite-paketi-3/hb.ps1` + `handbrake-kiyas.yml`'e eşit baytlı araştırma kolları (svt,
   turbo, vt) ve uydurma anahtar negatif kontrolü; seçilen parametre koda girer, ürün yeniden ölçülür.
7. **Belgeler.** `docs/olcumler/handbrake-kiyas-b7-aciklar.md` önce/sonra; duvar saati iddiası
   sayısı değişirse `duvar-saati-iddialari.md`.

# HandBrake Dalga 1a — Başsız CLI

Dal `t0/hb-1a-cli`. Kaynak: `.calisma/danisma/handbrake-yanit.md` karar 9.

1. **Tek karar kaynağı.** Yeni `src/VidShrink.Ffmpeg/ShrinkEngine.cs`: yoklama + kalibrasyon
   döngüsü (bugün `MainWindow.MeasureComplexityAsync` içinde), karar (`BuildDetailed`),
   gösterilen komut, çıktı adı. `MainWindow` aynı fonksiyonları çağırır; eski statik
   yardımcılar yönlendirici olarak kalır (kaynak pimleri bozulmaz, biri motora taşınır).
2. **`src/VidShrink.Cli`** (yalnız Core + Ffmpeg): `kucult`, `plan`; `--hedef`/`--kalite`,
   `--kodek auto|h264|hevc|av1`, `--cikti`, `--json`, `--olcumsuz`. İlerleme stderr, sonuç
   stdout. Çıkış kodları: 0 bantta, 2 bant altı, 3 tavan aşımı, 1 hata, 64 kullanım, 130 iptal.
   `izle` kaydedilmez (2c).
3. **Dil:** `Locales/en.json` + `tr.json` gömülü, `CurrentUICulture` seçer.
4. **Testler:** `CliTests` — GUI penceresinin komutu ile CLI komutu aynı girdide bayt bayt eşit
   (üç kodek), negatif kontrol (farklı hedef/kodek farklı komut), ayrıştırma, çıkış kodu
   eşlemesi, dil anahtarları; ffmpeg'li 2 sn'lik klip `plan --json` ve `kucult` süreç testi.
5. **Paket:** `release.yml` CLI'ı aynı `publish/<rid>` klasörüne App'ten önce yayınlar
   (macOS'ta `vidshrink-cli`, `VidShrink` ana ikilisiyle büyük/küçük harf çakışmasın);
   `VidShrink.sln`; README'ye kısa CLI bölümü.

# Hipersürüş — çift tıktan ilk kareye

> "ister güncelleme olsun ister olmasın bir videoya tıklandığı oynatmaya geçme
> basamaklarını minimuma indirecek bir hipersürüş tasarısı tıklandığı anda ms ler
> içinde video oynayacak — gerekirse güncelleme ertelenecek bu konu mühim"

## Bugün nerede duruyoruz

Ölçü deponun kendi aracından: `tools/acilis-hizi/olcum.ps1`, enstrüman
`src/VidShrink.App/MainWindow.AcilisIzi.cs`, sonuçlar
[docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md).

| Ölçü | Değer |
| --- | --- |
| `ilk-kare` ortancası, sıcak | 1857,8 ms |
| `ilk-kare` ortancası, soğuk | 1586,0 ms |
| `sekme` → `ilk-kare` arası | ~330 ms |
| libmpv ön yüklemesi (arka planda) | 74,0 / 67,1 ms |

**Bütçenin dağılımı burada saklı.** Motorun kendisi (mpv_create + ilk çözme) ~330 ms.
Geriye kalan ~1250-1500 ms, sekmeye geçilmeden önce, yani **oynatmayla hiç ilgisi
olmayan işlerde** harcanıyor. Hipersürüşün hedefi bu 1250 ms'dir; 330 ms'lik motor
payı ikinci sırada gelir.

Ayrıca ölçünün sıfır noktası app sürecinin `Process.StartTime`'ı
([acilis-hizi.md:10-14](olcumler/acilis-hizi.md)); **başlatıcının harcadığı süre bu
sayılara hiç girmiyor.** Kullanıcının beklediği süre bugün ölçtüğümüzden büyük ve
ne kadar büyük olduğunu bilmiyoruz. İlk iş bunu ölçmek.

## Fable'ın netleştirmesi

[docs/netlestirme/014](netlestirme/014-bir-videoya-cift-tiklandigi-andan-ilk-ka.md).
Beş soru sordu; üçünü burada cevaplıyorum, ikisi kullanıcının kararı:

- **Sıfır noktası** çift tıkın kendisi olacak: başlatıcı dahil. Ölçü bunu göremiyor,
  H0 bunu düzeltiyor.
- **İlk kare** tanımı bugünkü işaret kalıyor: `Frame.Source = _bitmap`
  ([PlayerView.axaml.cs:647](../src/VidShrink.App/Playback/PlayerView.axaml.cs:647)).
  Poster ya da boş pencere sayılmaz — kullanıcı "video oynayacak" dedi, çerçeve değil.
- **Doğrulama zemini** mevcut düzenek: eşleşik (paired) A/B, ortanca fark, ve her
  tekrarın hangi yöne baktığı. Ölçüm bu makinede eşleşmemiş karşılaştırmanın
  geçersiz olduğunu gösterdi ([acilis-hizi.md:22-40](olcumler/acilis-hizi.md)).
- **Hedef eşik** ve **hangi görünür davranışlara dokunulabileceği** kullanıcının kararıydı;
  16 Eylül 2026'da ikisi de cevaplandı, aşağıda.

## H0 — Ölçüyü çift tıka kadar geriye çek

Ölçemediğimiz şeyi iyileştiremeyiz. Başlatıcı, app'i doğurmadan önce sekiz iş
yapıyor ve hiçbiri ölçüde görünmüyor:

| Yer | İş | Neden açılış yolunda değil |
| --- | --- | --- |
| [Launcher/Program.cs:92](../src/VidShrink.Launcher/Program.cs:92) | `SeedVersionMarker` | Her açılışta disk yazımı |
| [:97](../src/VidShrink.Launcher/Program.cs:97) | `UpdateStage.ResumePending` | Bekleyen güncelleme varsa **yüzlerce MB kopyalama** |
| [:115](../src/VidShrink.Launcher/Program.cs:115) | `progress.WriteLog` | `update-log.txt`, her açılışta |
| [:116](../src/VidShrink.Launcher/Program.cs:116) → [Splash.cs:122](../src/VidShrink.Launcher/Splash.cs:122) | Splash join | Panel çizildiyse **2+2 sn tavan** |
| [:121](../src/VidShrink.Launcher/Program.cs:121) | `ToolsPresent` + PATH taraması | **Oynatma ffmpeg kullanmıyor** |

H0: başlatıcıya kendi iz işaretini koy, sıfır noktasını `CreateProcess` anına taşı,
ölçüyü tekrar al. Kazanç hedefi yok; bu **tartıyı kurmak**.

## A dalgası — Oynatma yolunu açılış yolundan ayır

Tek fikir: **argv'de bir video varsa uygulama açılmaz, oynatıcı açılır.** Geri kalan
her şey ilk karenin arkasına düşer.

### A1 — Başlatıcıyı yoldan çıkar (~ölçülecek, tahmin 300-800 ms)

Video yolu argv'deyse başlatıcı **önce** `Process.Start` eder, güncelleme işlerini
**sonra** yapar. Bugün `ResumePending` ve `ToolsPresent` app'in önünde
([Program.cs:97,121](../src/VidShrink.Launcher/Program.cs:97)); ikisi de app doğduktan
sonra koşabilir. `Updater.Run` zaten doğru tarafta ([:145](../src/VidShrink.Launcher/Program.cs:145)) —
onu örnek al.

Kullanıcının cümlesinin karşılığı bu madde: *"ister güncelleme olsun ister olmasın"*.
Bekleyen bir güncelleme varsa **bir sonraki açılışa** ertelenir, video önce oynar.

### A2 — Oynatıcıyı `MainWindow`'dan önce aç (tahmin 400-900 ms)

Bugün [MainWindow.axaml.cs:149](../src/VidShrink.App/MainWindow.axaml.cs:149)
`InitializeComponent()` **dört sekmenin tamamını** kuruyor: küçültme, dönüştürme,
kaydedici, ayarlar, gelişmiş paneller, karşılaştırma paneli. Arkasından ~60 `Watch`
bağlaması, liste kurulumları, `LoadTitleBarLogo`, `SetupShellMenu`
([:169-265](../src/VidShrink.App/MainWindow.axaml.cs:169)) ve `OnWindowLoaded`'daki
ayar yığını ([:500-508](../src/VidShrink.App/MainWindow.axaml.cs:500)) geliyor.
Kullanıcı bunların hiçbirini görmeyecek.

A2: argv'de video varken `PlayerView`'i taşıyan ince bir pencere önce açılır; dört
sekmeli kabuk ilk kareden **sonra**, arka planda kurulur ve oynatıcı oraya taşınır —
ya da kabuk hiç kurulmaz, kullanıcı bir sekmeye basana kadar bekler.

İki seçenek arasındaki fark kullanıcıya görünür (üst şerit ilk anda var mı yok mu),
bu yüzden karar kullanıcının.

### A3 — Motoru arayüzle paralel kur (tahmin 150-300 ms)

`new MpvEngine()` bugün arayüz iş parçacığında, senkron
([PlayerView.axaml.cs:529](../src/VidShrink.App/Playback/PlayerView.axaml.cs:529));
içinde `mpv_create` + `mpv_initialize` + render bağlamı var
([MpvEngine.cs:82,90,96](../src/VidShrink.Player/MpvEngine.cs:82)) ve
`mpv_initialize` soğuk açılışın en pahalı tek çağrısı.

A3: argv'de video varken motor, `libmpv` ön yüklemesinin hemen ardından
([App/Program.cs:182-188](../src/VidShrink.App/Program.cs:182)) arka planda kurulur
ve `loadfile` XAML açılımıyla **aynı anda** koşar. Pencere hazır olduğunda kare zaten
bekliyor olur.

### A4 — `Play()`'in önündeki dört dosya işini arkaya al (tahmin 20-80 ms)

`Play()` çağrısından **önce** koşan senkron disk işleri:

| Yer | İş |
| --- | --- |
| [PlayerView.axaml.cs:549](../src/VidShrink.App/Playback/PlayerView.axaml.cs:549) | `PlaybackHistory.Load` |
| [PlayerView.Window.cs:252-253](../src/VidShrink.App/Playback/PlayerView.Window.cs:252) | `PlayerSettings.Load` + `RecentFiles.Load` |
| [PlayerView.Window.cs:163](../src/VidShrink.App/Playback/PlayerView.Window.cs:163) | **`RecentFiles.Save` — diske yazım** |

Son kullanılanlar listesinin **yazımı** ilk karenin önünde duruyor. Dördü de
`Play()`'den sonra koşabilir; tek istisna `PlaybackHistory.Load`, çünkü kaldığı yere
arama ondan geliyor ([:559-561](../src/VidShrink.App/Playback/PlayerView.axaml.cs:559)).

### A5 — İlk kareyi saatten kopar

`StartRender()` 16 ms'lik bir `DispatcherTimer`
([PlayerView.axaml.cs:557](../src/VidShrink.App/Playback/PlayerView.axaml.cs:557));
ilk kare en kötü halde bir tam tık bekliyor. A5: ilk kare motorun kendi güncelleme
geri çağrısıyla ([MpvEngine.cs:96-104](../src/VidShrink.Player/MpvEngine.cs:96))
saati beklemeden çizilir, saat ikinci kareden itibaren devralır. Kazanç küçük
(0-16 ms) ama bedeli de küçük.

## B dalgası — Motorun kendi payı (~330 ms)

Bu dalga kullanıcıya görünür davranışa dokunuyor; **çatal 2** buraya bakıyor.

| # | Değişiklik | Bugün | Tahmin |
| --- | --- | --- | --- |
| B1 | `hwdec=auto-copy` varsayılan olsun | `no` ([MpvEngine.cs:143](../src/VidShrink.Player/MpvEngine.cs:143)) | çözme ucuzlar, kare kopyası durur |
| B2 | `demuxer-lavf-probe-info` / `demuxer-max-bytes` kısılsın | dokunulmuyor | ilk `loadfile` erken döner |
| B3 | `cache=no` ya da küçük önbellek | libmpv varsayılanı | ilk kare öne çekilir |
| B4 | `vo` SW render yerine donanım yüzeyi | `libmpv` BGRA, CPU kopyası ([:142](../src/VidShrink.Player/MpvEngine.cs:142)) | en büyük kazanç, **en pahalı iş** |

B1-B3 seçenek yazısı; B4 render yolunun yeniden yazımı ve
`ComparisonSurface`/`EngineComparisonFrameSource` de ondan besleniyor. B4 kendi
dalgası olmayı hak ediyor, A ve B1-B3 ölçülmeden başlanmaz.

## Sıra ve ölçü

1. **H0** — tartıyı kur. Bunsuz hiçbir sayı doğrulanamaz.
2. **A1, A4, A5** — görünür davranışa dokunmayan, ucuz, kesin kazançlar.
3. **A3** — paralelleştirme; A2'den önce çünkü A2'nin kazancını o açığa çıkarıyor.
4. **A2** — en büyük tek kalem, en görünür karar.
5. **B1-B3** — mpv seçenekleri, her biri ayrı ölçülür.
6. **B4** — ayrı dalga.

Her adım eşleşik A/B ile ölçülür ve kazancı `docs/olcumler/acilis-hizi.md`'ye yazılır.
Tahminler burada **tahmindir**; hükmü ölçüm verir. `tools/acilis-hizi` `.sln`'e
eklenip CI'da koşan bir eşik pimi kazanır — bugün bu yolun süresini ölçen **hiçbir
test yok** ve kazanılan her ms sessizce geri kaybedilebilir.

## İki çatal — cevaplandı (16 Eylül 2026)

**Çatal 1 — hedef eşik: 100 ms altı, mümkünse.** Mümkün değilse erişilebilenin en iyisi.
Bu eşik B4'ü (render yolunun yeniden yazımı) planın zorunlu bir dalgası yapıyor: A dalgası
tek başına ~300 ms'e iniyor, 100 ms'in altı motorun kendi payına dokunmadan görünmüyor.

**Çatal 2 — hepsi masada.** Görünür davranış değişebilir: A2'de dört sekmeli kabuk ilk
anda görünmeyebilir, B1'de donanım çözme varsayılan olabilir, B4'te render yolu değişebilir.

## Yapıldı

**H0 — tartı kuruldu.** Başlatıcının kendi izi var (`src/VidShrink.Launcher/AcilisIzi.cs`);
sıfır noktası artık başlatıcının doğumu ve bu an `VIDSHRINK_ACILIS_T0` ile app'e geçiyor,
yani iki sürecin satırları aynı eksende okunuyor. `olcum.ps1` iki yeni adım sayıyor:
`baslatici`, `app-dogdu`.

**A1 — başlatıcı yoldan çıktı.** Argümanda açılacak bir dosya varsa uygulama bakım
işlerinin önünde doğuyor; onarım, sürüm işareti ve güncelleme arkasına düşüyor. Bekleyen
dosyaların taşınması (`ResumePending`) o turda hiç koşmuyor, bir sonraki normal açılışa
kalıyor. ffmpeg varlık sınaması da atlanıyor: oynatma libmpv ile.

**A3 — motor arayüz ipliğinden çıktı.** `new MpvEngine()` artık `Task.Run` içinde;
`mpv_initialize` koşarken pencere kendi düzenini kuruyor.

**A4 — ayar yazımları oynatmanın arkasına düştü.** `AfterOpen` (ayar okuması, son
kullanılanlar listesinin diske yazımı) `TogglePlay`'den sonra çağrılıyor.

**A5 — ilk kare saatten koptu.** Çizim saati ilk kare düşene kadar 1 ms adımla koşuyor,
sonra 16 ms'e dönüyor; ayrıca kurulur kurulmaz bir kare deneniyor.

**Ölçüldü (16 Eylül 2026).** Eşleşik sıcak, 14 tekrar, taban `1a1385c1` — yeni `d2ea8bd5`:
dış saatte (`kabuk-ilk-kare`) ortanca fark **−71,0 ms**, 14 çiftin 9'u yeni yapı lehine.
Taban 1796,5 ms, yeni 1747,7 ms. Tam tablo ve okuma tuzakları:
[acilis-hizi.md](olcumler/acilis-hizi.md).

**Hedefe 1,7 saniye var.** Kalanın hepsi uygulamanın içinde: ~700 ms XAML açılımı ve yedi
sekmenin kurulması (**A2**), ~275 ms `mpv_create` ve ilk çözme (**B4**). 100 ms eşiği bu
ikisi yapılmadan görünmüyor; A dalgasının dokunabildiği yer zaten ~70 ms'ti.

Sırada **A2** (oynatıcıyı `MainWindow`'dan önce açmak) ve **B** dalgası var. Her adımın
kazancı eşleşik A/B ile ölçülüp `docs/olcumler/acilis-hizi.md`'ye yazılacak.

## C dalgası — Algı + JIT + palet (16 Eylül 2026)

Fable'ın netleştirmesi: [016](netlestirme/016-a-dalgasi-olculdu-cift-tik-ilk-kare-1796.md).
Beş soruyu burada cevaplıyorum.

**1. Hedefin bittiği işaret — iki saat.** Kullanıcının izin verdiği algı yolu hedefi
ikiye ayırıyor:

| Saat | Ne ölçer | Hedef |
| --- | --- | --- |
| `perde` | Çift tıktan **ekranda bir şey görünene** kadar | ~100 ms |
| `kabuk-ilk-kare` | Çift tıktan **gerçek videonun ilk karesine** kadar | elden geldiğince, üst sınır yok |

Perde bir örtü değil oyalama: arkasında gerçek iş koşuyor ve ilk kare gelir gelmez
kapanıyor. "Bitti" demeden kapanmıyor, donmuş bir panel gecikmeden kötüdür.

**2. İskeletin sahibi — başlatıcı.** Yeni bir çatı kurulmuyor: başlatıcıda **zaten**
çıplak Win32 bir panel var (`src/VidShrink.Launcher/Splash.cs`, 847 satır) ve görüntüsü
her derlemede `App.axaml`'ın paletinden üretiliyor. Bugün yalnız kurulum 400 ms'i geçince
çiziliyor. C2 eşiği sıfırlıyor ve panelin kapanış şartını değiştiriyor. Üçüncü süreç yok.

**3. Başlatıcının sırası — değişmiyor.** A1 bunu zaten yaptı: argümanda dosya varsa
uygulama bakım işlerinin önünde doğuyor.

**4. Teknik sınırlar.**

| Teknik | Karar |
| --- | --- |
| `PublishReadyToRun` | **Bu dalgada.** Yayın boyutu büyür, açılışta JIT'in payı düşer |
| `TieredPGO` | **Bu dalgada.** Bedava anahtar |
| `PublishAot` | **Yasak.** Avalonia XAML ve palet yansımayla kuruluyor |
| Yedi sekmeyi `UserControl`e bölmek | **Bu dalgada değil.** 4601 satırlık kod-arkası sekmelerin içindeki `x:Name`'lere bağlı; ayrı dalga, ayrı ölçüm |
| `hwdec` | **Bu dalgada değil.** B1 olarak duruyor, uyumluluk kolu ayrı ölçülür |
| Yerleşik bekleyen süreç | **Yasak.** Kullanıcının makinesinde boşta duran süreç bırakmıyoruz |

**5. Başarı ölçüsü — aynı protokol.** Eşleşik sıcak, 14 tekrar, ortanca, `olcum.ps1`.
Explorer'ın `CreateProcess` öncesi payı dış saate girmiyor; ölçer `Start-Process`'ten
sayıyor ve bu iki yapı için de aynı.

### C1 — ReadyToRun ve TieredPGO

`VidShrink.App.csproj` ve `VidShrink.Launcher.csproj` bugün hiçbir açılış anahtarı
taşımıyor; her açılışta bütün IL JIT'leniyor. `cerceve` öncesi 303 ms ile `xaml`
adımının 305 ms'i büyük ölçüde bu.

### C2 — Açılış perdesi

Başlatıcı uygulamayı doğurduğu anda paneli açıyor ve uygulamanın "ilk karem ekranda"
işaretini bekliyor. İşaret adlandırılmış bir olay (`VIDSHRINK_ACILIS_PERDESI` ortam
değişkeniyle geçen ad); uygulama ilk kareyi ya da ilk yüklenen pencereyi gördüğü anda
kuruyor. Olay gelmezse tavan süre panelin kendi durma kuralına düşüyor.

Yeni iz adımı: `perde`. `olcum.ps1` onu sayıyor, ölçüm tablosu iki saatli oluyor.

### C3 — Palet kısa devresi

`PaletteCatalog.Use` bugün ayardaki palet **zaten yürürlükteki palet olsa da** tam turu
koşuyor: iki palet dosyasını ayrıştırıyor, renk tablosu çıkarıyor, birleşmiş sözlüğün ilk
sırasını yeni bir `ResourceInclude` ile değiştiriyor ve kurulmuş bütün fırçaları geziyor.
Ölçümde `cerceve` → `palet` arası **194,1 ms**. İstenen palet yürürlüktekiyle aynıysa
yapılacak iş yok.

### Sıra

C1 ve C3 ucuz ve görünür davranışa dokunmuyor, önce onlar. C2 davranışı değiştiriyor,
sonra o. Üçü bittikten sonra tek bir eşleşik ölçüm; hüküm `kabuk-ilk-kare` ve `perde`
sütunlarından okunur.

**C dalgası yapıldı ve ölçüldü (16 Eylül 2026).** Eşleşik sıcak, 14 tekrar: dış saatte
(`kabuk-ilk-kare`) ortanca fark **−2603,3 ms**, **14 çiftin 14'ü** yeni yapı lehine. Yeni
`perde` adımı ortanca **211,4 ms** — bu oturumda tabanın ilk karesi 6506,3 ms'te geliyordu.
Makine bu oturumda ~3,6 kat kaymış durumda; mutlak sayılar oturumlar arası
karşılaştırılmaz, tam tablo ve okuma tuzakları
[acilis-hizi.md](olcumler/acilis-hizi.md).

Sırada **A2** (yedi sekmeyi ayrı `UserControl`lere bölmek) ve **B1-B4** (mpv seçenekleri,
render yolu) var. Perde algı saatini hedefe getirdi; gerçek ilk kareyi 100 ms'e indirmek
hâlâ bu iki dalgadan geçiyor.

## D dalgası — Sondayla ölçülmüş gerçek kalemler (16 Eylül 2026)

Fable'ın beş sorusu ([017](netlestirme/017-c-dalgasindan-sonra-cift-tik-ile-ilk-kar.md)) burada
cevaplanıyor.

1. **Hedef hangi saatte?** `perde` saatinde. Boşta koşan makinede `perde` **78,1 ms**;
   100 ms hedefi orada tutuyor. `kabuk-ilk-kare` için hedef yok, yalnız "her dalgada daha
   az" var: süreç doğumu + CLR + Avalonia çerçevesi tek başına ~320 ms ve bu taban
   `PublishAot` yasakken inmiyor.
2. **Hangi açılış senaryosu?** Çift tık senaryosu. Ölçüm zaten kabuktan dosya vererek
   koşuyor; boş açılış ayrı ölçülmüyor.
3. **Motor anahtarları nereye uygulanır?** Yalnız oynatıcının kendi motoruna
   (`PlayerView.EngineFactory`). Karşılaştırma paneli ve önizleme sesi kendi
   seçenekleriyle kuruluyor, bayt eşleme ölçüsü onlardan okunuyor.
4. **Görünür davranış değişikliği?** Kullanıcı "dalga dalga iznimi isteme" dedi; yapılıyor
   ve turun sonunda adıyla bildiriliyor. Bu dalgada bir tane var: oynatıcı çözmeyi
   donanıma veriyor.
5. **Kabul ölçüsü?** Eşleşik fark ortancası, 14 tekrar, aynı 6,2 MB klip. Mutlak sayı
   oturumlar arası kıyaslanmıyor.

### Sonda: `InitializeComponent`'in içi

`AcilisIsareti` iliştirilmiş özelliği XAML ağacının içine iz noktası koyuyor; derlenmiş
XAML'de öğe kurulurken yazıldığı için iki işaret arasındaki fark aradaki ağacın bedeli.
8 tekrar, boşta makine, ortanca ms:

| Aralık | Pay | Ne kuruluyor |
| --- | --- | --- |
| pencere-yapici → xaml-sekmeler | 50,0 | pencere kabuğu, başlık çubuğu |
| xaml-oynatici → xaml-kucultme | 18,4 | oynatıcı sekmesi (`PlayerView`) |
| xaml-kucultme → xaml-donusturme | 21,5 | küçültme sekmesi, 605 satır |
| xaml-donusturme → xaml-hakkinda | 4,8 | dönüştürme sekmesi |
| xaml-hakkinda → xaml-kaydedici | 1,4 | hakkında sekmesi |
| **xaml-kaydedici → xaml-gelismis** | **89,4** | **kaydedici sekmesi (`RecorderView`)** |
| xaml-gelismis → xaml-ayarlar | 1,2 | gelişmiş sekmesi |
| xaml-ayarlar → xaml-sekmeler-bitti | 3,0 | ayarlar sekmesi |

Yedi sekmeyi `UserControl`'lere bölmek (A2) **gereksiz**: altı sekmenin toplamı 50 ms,
tek başına kaydedici 89,4 ms. Ölçü A2'yi kapattı.

### Boşta makinede bugünkü tablo (8 tekrar, ortanca ms)

| Adım | Birikimli | Pay |
| --- | --- | --- |
| baslatici | 45,2 | 45,2 |
| perde | 78,1 | — |
| main | 100,2 | 44,6 |
| libmpv-hazir | 116,8 | 16,6 |
| cerceve | 319,8 | 203,0 |
| pencere-yapici | 361,4 | 37,5 |
| xaml | 576,5 | 215,1 |
| yapici-bitti | 647,4 | 70,9 |
| pencere-kuruldu | 819,6 | 172,2 |
| pencere-yuklendi | 921,0 | 101,4 |
| sekme | 942,0 | 21,0 |
| kare-kaynagi | 1214,8 | 272,8 |
| ilk-kare | 1216,6 | 1,8 |

### D1 — Kaydedici sekmesi tembel

`RecorderView` XAML'den çıktı; sekme ilk seçildiğinde kuruluyor
(`MainWindow.TembelSekme.cs`). Kenar payı `SectionMargin` belirtecinden okunuyor.

### D2 — Donanım çözme: ölçüldü, geri alındı

`hwdec=auto-copy` denendi. Eşleşik ölçümde motor adımı 272,8 ms yerine 297,0 ms oldu,
yaklaşık +24 ms. Kazanç değil kayıp; `EngineFactory` yazılımsal çözmeye döndürüldü.
Pim: `OynaticiYazilimsalCozuyor_DonanimOlculdu_GeriAlindi`.

### D3 — Motor pencere kurulurken açılıyor

`AcilisMotoru` (App/Playback): kabuktan dosya geldiğinde libmpv ısındıktan hemen sonra
motoru açıp bekletiyor; `PlayerView.OpenAsync` aynı yolu isterse hazır motoru devralıyor,
istemezse kendi motorunu kuruyor ve bekleyen motor `Birak` ile atılıyor.

Ölçü: `sekme → kare-kaynagi` 268,8 ms → 52,7 ms; eşleşik `kabuk-ilk-kare` farkı
−213,7 ms, 14 çiftin 13'ü lehine. Tablo
[docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md) D dalgası.

### D dalgasından sonra kalanlar

`cerceve` ~216 ms (Avalonia çerçeve kurulumu), `Show()` ~173 ms, `Loaded` ~99 ms.
Üçü de çerçeve düzeyinde; kendi kodumuzda kesilecek büyük kalem kalmadı.

## E dalgası — Oynatıcı şeridi ve üst menü (16 Eylül 2026)

Dal `t0/oynatici-serit`. Dokunulan dosyalar: `PlayerView.axaml`, `PlayerView.Serit.cs`,
`Themes/Playback.axaml`, `Themes/Controls.axaml`, `MainWindow.axaml.cs`, iki test.

1. Medya yokken üst şerit de açık: `ChromeHidesItself` oynatıcı sekmesi **ve** yüklü medya ister.
2. Sessiz simgesi `_muted`'ı da okuyor (ham fare ölçüsünde bulundu: simge sessizde değişmiyordu).
3. Ses/hız okuması düz metin değil: mavi çerçeveli değer çipi, `100%` ve `1.00×`.
4. `PlaybackSlider` kendi şablonu: zaman çubuğunun yolu, dolgusu ve tutamacı — pembe dolgu yok.
5. Üst sekmeler sağdaki başlık düğmeleriyle aynı yüz: dolgu yok, mavi yazı, pembe üzerine gelme.
6. Şeridin üst anahattı güçlü mavi (`NeonBlueBorderStrong`), perde üst kenarda soluklaştırmasın.
7. Doğrulama: `OynaticiGercekGirdiTests` ham fareyle mute/ses/hız/oynat; `PencereKabuguTests` pinleri güncellenir.

## Hipersürüş E dalgası — Pencere kurulumunda kalan kendi kalemler (16 Eylül 2026)

D dalgasının "kendi kodumuzda büyük kalem kalmadı" hükmü ölçülmemişti. Sonda (geçici
`AcilisIzi` noktaları) dört hedef aralığa kondu: `pencere-yapici → xaml`,
`yapici-bitti → pencere-kuruldu`, `pencere-kuruldu → pencere-yuklendi`,
`sekme → kare-kaynagi`. Kabul: eşleşik 14 tekrar, `kabuk-ilk-kare` fark ortancası;
kazanç yoksa geri al.

1. **E1** Pencere simgesi Windows'ta ICO'dan. 1254 px PNG'nin HICON'a çevrilmesi 186 ms.
2. **E2** Başlık logosunun çözümü arayüz iş parçacığından çıkıyor. 21 ms.
3. **E3** Dil adları `Strings.PeekIn` ile; 42 kataloğun tamamı açılışta yüklenmiyor. 64 ms.
4. **E4** Kabuktan dosya geldiyse oynatıcı sekmesi yapıcıda seçili; ilk ölçü küçültme
   sekmesini kurmuyor. Pim `KabukYolununActigiSekmeOynaticidir` "sekme değişir" yerine
   "baştan oynatıcı" diyor.

Kesilmeyen: güncelleme paneli ~0,2 ms, `UseLanguage` ~17 ms, `pencere-yapici → xaml`
(çerçeve + XAML ağacı, tek kalem yok).

Sonuç: `kabuk-ilk-kare` 1230,1 → 803,5 ms, eşleşik fark **−455,2 ms**, 14/14. Tablo
[docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md) E dalgası.

## Hipersürüş F dalgası — Perdesiz açılış ve kabuk menüsü (16 Eylül 2026)

Dal `t0/acilis-anlik`. Kullanıcı açılışta "VidShrink açılıyor" panelini görüyor ve
açılışı yavaş buluyor. Kabul: eşleşik A/B, taban `main`, en az 14 tekrar, ayrı Win32
masaüstünde (`WinSta0\vidshrink-olcum`); kullanıcının ekranında pencere açılmaz,
sağ tık menüsünün kayıt değerleri her koşumdan önce ve sonra doğrulanır.

1. **F1** Perde kalkıyor: iki `AcilisPerdesi.cs` silinir, başlatıcı uygulamayı
   beklemeden doğurur. Kurulum paneli 400 ms eşikli bakım kolunda kalır.
2. **F2** `RelabelShellMenu` her açılışta girdileri silip yeniden kuruyordu; silme
   kolu Appx paketi için eşzamanlı PowerShell başlatıyordu. Yenileme yalnız farklı
   etiketi yazar. `InstallOpen` de paketi kaldırmaz; yalnız kutunun boşaltılması kaldırır.
3. **F3** `tools/acilis-hizi/EkranSaati`: ayrı masaüstünde ölçer, kayıt defterine yazmaz.
4. Pinler: `HipersurusTests.OlaganAcilistaPerdeYok`, `SplashTests`,
   `KabukMenusuTests.EtiketYenilemesiGirdiyiYenidenKurmuyor`.

Tablo [docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md) F dalgası.

## Hipersürüş G Dalgası — Başlatıcısız Çift Tık (17 Eylül 2026)

Dal `t0/hipersurus-g`. Kullanıcı: "1 sn'de açılmıyor, panel olmasın". Danışma
`.calisma/danisma/hipersurus-g-yanit.md`. Her ikili ayrı ayar dosyası
(`VIDSHRINK_SETTINGS_PATH`, `.calisma` altı) ve kayıt kalkanıyla (`KayitKalkani`) koşar.

1. **G5** `EkranSaati`: `--klip -` boş açılış, `--bitis` bekleme işaretini gerçekten
   seçer, `--giris-a/--giris-b baslatici|app` tarafı doğrudan `app\VidShrink.App.exe`
   ile açabilir. Uygulamaya izle kapılı `ilk-boya` işareti (ilk çizilen pencere).
2. **G4** Kusur: kaynağa göre önerilen hedef MB ve türetilen kalite ayar dosyasına
   yazılıyordu (eski dosyada `qualityTarget` 60→78,3). Kaydedilen değerler yalnız
   kullanıcının kendi girdisinden gelir.
3. **G2** Dosya ilişkisinin `open` komutu `app\VidShrink.App.exe`'ye gider. Ortak
   karar `ShellIntegration.OpenCommandTarget`; kurucu betiği, `ShellRegistration` ve
   `FileAssociation.Plan` aynı değeri yazar. Simge, `Applications\VidShrink.exe`
   anahtarı ve sağ tık menüleri başlatıcıda kalır. Bakım (onarım, sürüm işareti,
   güncelleme denetimi, işleyici) uygulamanın doğurduğu `VidShrink.exe --bakim`
   ile koşar; `ToolLocator` `app\` altından kökteki `tools\ffmpeg`'i bulur.
4. **G3** Başlatıcı olağan yolda da `SplashGate`'i kurar; 400 ms eşiğini aşmayan
   bakım panelsiz geçer, `ResumePending` de kapsanır. "Yükle"den sonra rozet
   "Başlatıcı açılıyor…" yazmaz.
5. **G1** Yazılım çizimi: oynatıcı ve karşılaştırma paneli için ≤10 sn 1080p
   CPU/kare A/B; kötüleşirse uygulanmaz, sayı raporlanır.
6. **G6** EkranSaati önce/sonra: pencere, ilk boya, ilk kare; ≤10 tekrar, sıralı.
   Tablo [docs/olcumler/hipersurus-g.md](olcumler/hipersurus-g.md).

## Paket 1

Dal `t0/paket-1`. Kaynak: `.calisma/eksikler/rapor.md` satır 1, 2, 3, 6, 7, 12, 13, 14, 15. Her kalem ayrı commit.

1. Güncelleme indirmesi iptal: `MainWindow.Guncelleme.cs` iptal kaynağı, panelin birincil düğmesi inerken "İptal"; `UpdateStaging` yarım dosyayı `.part`tan siler.
2. Oynatma sırasında indirme tavanı: tavansız/tavanlı kare düşümü ölçümü, `docs/olcumler/guncelleme-indirme-tavani.md`.
   **Kapandı, ölçülmedi:** kullanıcı makinesinde ölçülmez, CI/ayrı makine. Ölçüm libmpv'yi yük altında uzun süre koşturuyor; 16 Eylül'de bu makinede yük üreten koşumlar iki kez Kernel-Power 41 kapanmasına denk geldi. 4 MiB/s tavanı ölçülmüş sayı değil, docstring'i öyle kalır.
3. Kurulum çubuğu açılış atağı: `InstallProgress` karelerinden ms ölçümü, `docs/olcumler/kurulum-cubugu-atagi.md`.
4. `player-recent.json` test yolu: kayıt yolu ayar yolu değişkenine bağlanır.
5. libmpv yedeği: `libmpv-mirror` yayın varlığı, CI/release/Install-VidShrink.ps1 yedek kaynak, sha256 aynı.
6. Karşılaştırma paneli perdesi: şekil tema belirteçleriyle, önce/sonra PNG.
   **Zaten yapılmış:** 29 Ağustos 23:51 isteği (`tmp/gecmis-8.md` satır 182) 36 dakika sonra T79 `2b5be0d7` ile karşılandı: şerit paravanı `PlaybackScrimVeil`, alttan yukarı son çeyreğinde (`PlaybackScrimEdge` 0,25) sönüyor. Rapor commit mesajındaki `Paravan sekillendi` satırını kaçırmış. Yeni biçim eklenmedi; Avalonia.Headless ile aynı `PlaybackStrip` teması iki zeminle çizildi: `docs/olcumler/gorseller/paravan-once-duz.png` (`PlaybackScrim`), `paravan-sonra-sonen.png` (`PlaybackScrimVeil`).
7. T194: dar pencerede kaynak bilgi kutuları tek satır, kısaltma + ipucu, `BiciminTests` pinleri.
8. `GlowBlue/Pink/Purple` palet değişiminde canlı.
9. Anahtar kare atlama yarışı: yeniden üret, kök neden, düzelt, pimle.

## Paket 2

Dal `t0/paket-2` (`origin/t0/birlesim`'den). Kaynak: `.calisma/eksikler/rapor.md` satır 10, 11, 18, 20, 21; plan `docs/plan-kaydedici-dalgalari.md`. Her kalem ayrı commit.

1. **9c borçları** (satır 10), dört commit:
   - `PixelFormats` kodek başına daraltılır: kullanıcıya görünen küme yalnız bit akışına giren düzlemsel YUV adları, paketli ad (`nv12`, `p010le`) kodlayıcı istediğinde motorca yazılır. Kaynak `ffmpeg -h encoder=<ad>` (9.0) çıktısı, `docs/olcumler/kaydedici-piksel-bicimleri.md`.
   - `-t` parça başına değil kayıt başına: kalan süre `_capturedBefore`'dan hesaplanır, sıfıra inince yeni parça açılmaz.
   - `SnapshotAsync` şeride bağlanır ("Kare al"), `SnapshotPath` onu kullanır.
   - `MaxKeyframeSeconds`, `MinGainDb`, `MaxGainDb`, `SplitPollMs` docstring'ine "ölçülmüş sayı değil" ve gerekçesi.
2. **GIF çıktısı** (satır 18, B1): `RecorderContainer.Gif`. Canlı yakalama mkv ara dosyaya yazılır, durunca `palettegen/paletteuse` ile GIF'e çevrilir; filtre zinciri oynatıcının klip kolu (`ClipExport`) ile ortak `Core/GifPalette`.
3. **D3 mkv varsayılanı** (satır 20): ayarın varsayılan kabı mkv; sonuç panelinde "MP4 olarak kaydet" (`-c copy` yeniden sarma).
4. **D1/D2 Basit/Gelişmiş** (satır 20): sekmenin üstünde iki kip. Basit: kaynak, ses, başlat. Gelişmiş: otomatik/elle seçimi iki seçenek olarak, bugünkü her şey.
5. **Geri sayım** (satır 21-7): 0/3/5/10 sn, şeritte ve mini kipte sayılır, iptal edilebilir.
6. **Kayıt çerçevesi** (satır 21-6): bölgenin dışına çizilen, üstte kalan, tıklamayı geçiren pencere; kısayolla gizlenir.
7. **Tepsi simgesi** (satır 21-3): üç durum (boşta/kaydediyor/duraklatıldı) palet renginden; ipucunda süre ve dosyanın o anki MB'ı.
8. **Genel kısayol** (satır 21-2): `RegisterHotKey`, `IGlobalHotkeys` arkasında; tanım tek yerde (`RecorderHotkeys`), çakışma kullanıcıya söylenir. Testte sahte kayıtçı.
9. **Gelişmiş panelin on kolu ve çeviri** (satır 11): kap, ölçek, anahtar kare, profil, ayar, hız kontrolü, piksel/renk, süre sınırı, bölme, ses düzeni/kazanç/kapı/bastırma denetimleri; bu paketin bütün yeni anahtarları 42 dilde.

Kurallar: yapay yük yok, `dotnet build -m:2`, test yalnız `--filter`, pencere açan test yok (testte Win32 arka ucu gerçek pencere açar), kayıt defterine ve gerçek `%APPDATA%`'ya yazılmaz — `RecorderSettings` de `VIDSHRINK_SETTINGS_PATH`'e uyar.

## Paket 2b

Dal `t0/paket-2b` (`origin/main` 8c1ef48f). Kaynak: kaydedici tarifinin kalan T7 ve T13 maddeleri, Paket 2'nin ölçemediği
pikseller, `.claude/acik.md` kaydedici borçları. Danışma `docs/danisma/2026-09-17-paket2b-fable.md`. Her kalem ayrı commit.

1. **T7 odak.** Çatal 1 kararı (`plan-duzenleyici.md`): varsayılan düğmeyle (sonuç panelinde bugün var), kendiliğinden
   Ayarlar'da bir seçenek. `AppSettings.FollowRecording` (kapalı): açıkken biten kayıt küçültme sekmesine yüklenir ve
   oynatıcıda açılır, seçili sekme değişmez.
2. **Boşluk kırpma.** `Core/IdleTrim`: `freezedetect` çıktısından donuk aralıklar, her aralık sınıra kısaltılır,
   `select/aselect` ile yeniden kodlanır. Sonuç panelinde "Boşlukları kırp".
3. **Arka plan ayırma.** Fable: modelsiz. `RecorderWebcam.Background` = yok / sabit arka plan (`backgroundkey`) /
   yeşil perde (`chromakey`); webcam grafiğinde `overlay`'den önce.
4. **Canlı önizleme.** Kayıt argümanına ikinci çıktı: yakalama girdisinden saniyede bir küçük JPEG (`-update 1`),
   şeridin altında görüntü her saniye tazelenir.
5. **Kayıt tamponu.** `Core/ReplayBuffer`: yakalama `-f segment -segment_wrap` ile döner parçalara yazılır, F11 son N
   saniyeyi kapsayan parçaları `concat -c copy` ile çıkış klasörüne yazar. Tampon düğmesi şeritte.
6. **Yükleme.** Fable: otomatik yükleme yok, "Paylaş" düğmesi kalır; storage.to varsayılan saklaması 3 günden 1 güne.
7. **Piksel doğrulaması.** gdigrab bu makinede masaüstünü siyah veriyor (YAVG 16), `gfxcapture` desenli pencereyi
   görüyor (YAVG 81). `tools/kaydedici-piksel`: çerçeve, halka ve büyüteç gerçek pencerelerle açılır, `gfxcapture`
   kareleri piksel ölçülür; tıklama sesi `PlaySoundW` dönüşü ve ses çıkış yoklamasıyla. Tablo `docs/olcumler/kaydedici-piksel.md`.
8. **Borçlar.** vp9 `-profile:v 0..3`, renk aralığı `mpeg`/`jpeg` takma adları, `RecorderSettings` JSON gidiş-dönüşü;
   geri kalan satırlar Paket 2'de kapanmış, kanıtı raporda.
9. **Çeviri ve pinler.** Yeni anahtarlar 43 dilde; `BiciminTests` sayım pinleri ve açıklama cümlesi yerel dökümden.

## Paket 3

Dal `t0/paket-3` (`origin/t0/birlesim` üstünden). Kaynak: `.calisma/eksikler/rapor.md` satır 8, 9, 23;
`docs/YOL-HARITASI.md` iki açık kalemi; `trash/sonra-2026-09-16T12-09-24-402Z.md` WhatsApp satırı. Her kalem ayrı commit.

**Ölçüm yeri:** kullanıcının makinesi tam yükte iki kez kapandı. Kalem başına onlarca kodlama ve VMAF geçişi
gerekiyor, o yüzden ölçüm yerelde değil, `workflow_dispatch` iş akışında koşar ve sonuç artifact'tan alınır.
Düzenek `GITHUB_ACTIONS` yokken koşmayı reddeder. Kaynak açık lisanslı Blender filmi, sha256 pinli; 10 sn'lik
kesitler kaynağın kendi parlaklık taramasından seçilir (en karanlık pencere, en parlak/hareketli pencere).

Önce bulunan ölçümler (`docs/olcumler` grep'i):

- `handbrake-acigi.md` — 8,79 VMAF-NEG / 2,60 dB XPSNR farkı `av1_nvenc` eski çıktısıyla ölçüldü; x265'e
  `psy-rd=2:psy-rdoq=1:aq-mode=2`, SVT-AV1'e variance boost sonradan girdi (`tepe-tavani-ve-psy.md`, T87).
  Farkın bugünkü yazılım yolunda kalıp kalmadığı ölçülmedi.
- `yerlesim-skoru.md` §11 — Faz 1'in iki sabiti (`ScalePenaltyScale`, `FpsPenaltyPerHalving`) T107'de
  ölçüldü, değişmedi. Ölçülmeyen kalan: `ScalePenaltyExponent`, `PenaltyWeights(Extreme)` üçlüsü,
  `LowFpsSurcharge`, `LowFpsThreshold`.
- `suit-esszamanli-kosum.md` — çökme kök nedeni açık borç; F1 yük koşumu çökmeyi üretemedi.

1. **WhatsApp karanlık video (satır 8).** WhatsApp çipi `Compatible` → `libx264`; x264'e hiçbir psy/AQ
   argümanı gitmiyor. Düzenek en karanlık 10 sn'lik kesitte eşit bit hızında kolları kıyaslar: ürün,
   `aq-mode=3`, `aq-mode=3:aq-strength=0.8`; negatif kontrol `aq-mode=0` ve ürünün tekrarı. Ölçü VMAF-NEG,
   XPSNR ve karanlık bölge ölçüsü (kaynakta Y<64 piksellerde PSNR ve ayırt edilen ton sayısı). Kazanan kol
   ölçüyle `FfmpegArguments.Psychovisual`'a girer, pimlenir; kazanmazsa kod değişmez. WhatsApp'ın kendi
   yeniden kodlaması CI'da ölçülemez: "ölçülmedi". Tablo `docs/olcumler/whatsapp-karanlik.md`.
2. **HandBrake algı farkı (satır 9).** Aynı kesitlerde gerçek `HandBrakeCLI` (H.265 MKV 1080p30, slow,
   çoklu geçiş) ile ürünün yazılım yolu (`bench shrink --force-codec libx265` ve `libsvtav1`) eş boyutta.
   Donanım yolu (`av1_nvenc`) CI'da yok: "ölçülmedi". Tablo `docs/olcumler/handbrake-acigi-yazilim.md`.
3. **Ceza sabitleri Faz 1 ve çökme düzeneği (satır 23).** Aşırı rejim bit hızlarında ölçek × kare hızı
   ızgarası (`tools/yerlesim-skoru/olc.sh`), ölçülmemiş beş sabitin uyumu; tutulan kesitte doğrulanmayan
   uyum koda girmez. Çökme için `cokme-yeniden-uretim.yml`: iki süit aynı koşucuda eşzamanlı,
   `--blame-crash --blame-hang`, döküm artifact'a. Tablolar `docs/olcumler/ceza-kalibrasyonu.md`,
   `docs/olcumler/cokme-yeniden-uretim.md`.

**Durum (ölçüm sonrası).** Ölçüm yeri sonradan değişti: `workflow_dispatch` varsayılan dalda olmayan iş akışında
404 döndü, tetik etiket oldu (`olcum-kalite-<is+is>__<kesit+kesit>--N`, `olcum-cokme-N`). Kaynak Tears of Steel
URL'si 404, yerine Sintel 1080p (sha256 pinli).

1. WhatsApp: `aq-mode=3` kolları karanlık PSNR'ı 8 satırda −0,01 ile +0,07 dB oynattı; kod değişmedi (koşum 35111531254).
2. HandBrake: yazılım yolunda XPSNR 6/6 önde, VMAF-NEG `karanlik`ta −0,65/−0,61 geride; kod değişmedi (koşum 35112822877).
3. Ceza: sadeleştirilmiş model 9/9 grupta iyimser, tutulan kesit doğrulaması geçmedi; sabitler değişmedi.
   Çökme: 12 süreçte 0 çökme, eşzamanlı 2–3'te 9 kararsız kalış (koşum 35109530525).

## İş 13 — HandBrake Kesit Türüne Göre, AV1 Izgarası

`kalite-olcumu.yml` ikinci iş için genişledi: `handbrake` işi kesit başına kbit listesiyle (`KBITLER`), `av1` işi
preset × CRF/kbit × film-grain × tune × keyint ızgarasıyla (`IZGARA` JSON). Kesit türleri `karanlik`, `hareketli`
(en yüksek YDIF) ve `ekran` (Netflix "Debugging", CC BY 4.0). Ağır ızgara CI'da; yerelde yalnız kısa kontrol.
Çağrı: etiket `olcum-kalite-handbrake+av1__karanlik+hareketli+ekran--N` ya da dispatch girdileri
`isler`, `kesitler`, `kbitler`, `izgara`, `ekran_url`. Tablolar `docs/olcumler/handbrake-kesit-turu.md`,
`docs/olcumler/av1-izgara.md`. Adlı ızgara: etiket `olcum-kalite-av1__<kesitler>__<ad>--N`, dosya
`tools/kalite-paketi-3/izgaralar/<ad>.json`; eş bayt koşumu `docs/olcumler/av1-esbayt.md`.

## İş 14 — Küçült ve Kabuk Açıkları (`t0/kucult-kabuk-aciklari`)

Kaynak: `.calisma/denetim/yol-haritasi-denetimi.md`, "Küçült, karşılaştırma, ayarlar" ve "Kabuk, güncelleme, açılış".

1. Taşma kararı: `EncodeRunner` taşmada her denemede sorar (son deneme dahil). Seçenekler: tekrar dene, bırak,
   büyüğü kabul et, sondan/baştan/ikisinden kes. Kesme yalnız taşma ≤ %3 iken önerilir; `Core/OvershootTrim`
   paket boylarından kesim noktası seçer, `Ffmpeg/TrimRunner` akış kopyasıyla keser, ölçer, hedefe inene dek sıkar.
2. Sıfırla: `App/AppDataReset` ayar klasöründeki on veri dosyasını ve bozuk paylaşım defteri kopyalarını siler;
   güncelleme günlüğü ve yarım güncelleme kaydı kalır.
3. Küçült: Kalite bölümünde `WhatsApp uyumlu (H.264)` kutusu, kodek şeridini kilitler. Kare bölümünde
   "Çözünürlük düşürülebilir" dinamik kutudur; kalkınca Kaynak/1080p/720p/480p şeridi (`PlanOptions.FixedResolution`, kısa kenar).
4. Ayarlar: çıktı klasörü ve ffmpeg yolu radyo şeridi, etiket ile tek satır; dil/tema ve hedef/gelişmiş yan yana.
5. Karşılaştırma rozeti yalnız `CRF n`; ORİJİNAL/İŞLENMİŞ orta panelin üstünde solda/sağda.
6. Opus: MP4'te WhatsApp/iOS uyumu bozuluyor → uygulanmaz, `.calisma/kucult-kabuk/soru-opus.md`.
7. Güncelleme: `AutoUpdate` varsayılanı kapalı; indirme/kurulum sürerken panel kapanmaz; eski Ayarlar
   simgesi (dişli); Hakkında'da platform satırı.

# Dalga 1c — Akış Eşleme

Kaynak: fable kararı 6 (`.calisma/danisma/handbrake-yanit.md`), analiz satırları 17, 20-23, 25-26.
Dal `t0/hb-1c-akis`.

1. `Core/StreamMapping.cs` (yeni): kaynak iz envanteri (`SourceStream`), istek (`StreamRequest`: izleri koru,
   platform, dil), karar (`StreamPlan`: kap, ses/altyazı izleri, notlar) ve `-map` argümanları.
   Varsayılan MP4 + tek ses (kullanıcı dili > varsayılan iz > ilk iz) + metin altyazı `mov_text`, resim
   altyazı düşer. İzleri koru: MKV, tüm ses (Opus) + tüm altyazı + ekler. Passthru: kap taşıyor, iz ≤ hedefin
   %15'i, kaynak bit hızı ≤ yeniden kodlama; TrueHD/DTS varsayılanda hiç. >2 kanal yeniden kodlanırken stereo.
   Platform çipi: MP4, tek ses, altyazısız. `-map_metadata 0 -map_chapters 0` her yolda.
2. `Ffmpeg/FfprobeClient.cs`: `-show_chapters`, iz envanteri, bayt (bit_rate / BPS / NUMBER_OF_BYTES; altyazıda
   yoksa paket toplamı).
3. `Core/MediaInfo.cs`, `Core/EncodePlan.cs`: `Streams`, `ChapterCount`; plana `Streams` ve `NonVideoK`.
4. `Core/PlanCalculator.cs`: ses + altyazı baytı bütçeden düşülür (`SizeMb`, `Correct`, `Estimate` aynı sayıyı
   okur); çok izde ses payı izlere bölünür; `PlanOptions.KeepAllTracks/PlatformDelivery/PreferredLanguage`.
5. `Core/FfmpegArguments.cs`: çıktı kolunda açık `-map`, `faststart` yalnız MP4; birinci geçişte yalnız video.
6. App: gelişmiş ses bölümünde "İzleri koru" kutusu (42 dil), çıktı uzantısı plandan, platform çipi bayrağı.
7. Test `StreamMappingTests.cs`: ffmpeg ile 3 sn girdi (2 ses + srt + PGS + 2 bölüm + başlık/tarih; ayrıca
   dönük MP4), çıktı ffprobe ile okunur; çok izli girdide hedef isabeti; her kolun negatif kontrolü.
