# VidShrink Arayüz Denetimi — 8 Eylül 2026

Kapsam: `src/VidShrink.App` tümü (XAML, tema, code-behind, oynatıcı, iş penceresi,
yerelleştirme, ayar kalıcılığı) + çalışan uygulamanın canlı gözlemi.

Yöntem: beş paralel denetçi ajan statik inceleme yaptı (ham çıktılar `ui-denetim-2026-09-08/ajan/`),
ardından uygulama Debug derlenip `.calisma/T171/kaynak/buyuk-1.mp4` ile açıldı ve ekran
görüntüsüyle doğrulandı (`ui-denetim-2026-09-08/ekran/`). **Kod değiştirilmedi.**

> **Satır numaraları uyarısı.** Denetim `44f4593` üzerinde koştu. Rapor yazılırken çalışma
> ağacında başka bir taraf `App.axaml`, `MainWindow.axaml`, `Themes/Theme.axaml`
> (−35 satır), `Themes/Controls.axaml`, `Themes/Playback.axaml` dosyalarını commit'siz
> değiştirmiş durumdaydı. Tema bölümündeki (1.16, 2.16-2.18) satır numaralarını uygulamadan
> önce doğrula.

Her satır: `dosya:satır — kusur — düzeltme` `[kaynak]`. Kaynak `A1..A5` ajan numarası,
`G` benim canlı gözlemim.

---

## 1. Kırık

Kullanıcının gördüğü ya da beklediği şey olmuyor.

### 1.1 Oynatıcı hiç kare çizmiyor

- `Playback/PlayerView.axaml.cs:267` + `Ffmpeg/Playback/DecoderPipe.cs:178-190` — `ContinuousPlayback.Pump`
  kareleri okuyup yalnız **sayıyor**; `Draw` sadece `RunSeekAsync`'ten (`:338`) çağrılıyor. Görüntü seek
  karesinde donuyor, `_seek.Target` ilerlemiyor — kare geri çağrısı ekle, `Draw`'ı oynatma döngüsüne bağla. `[A3]`
- Canlı kanıt: `ekran/01-acilis-dosyali.png` — oynatıcı panosu tamamen siyah, altında
  `position 0 s - paused - zoom 100%`. `[G]`

### 1.2 Oynatıcı sekmesi açılış dosyasını hiç almıyor

- Komut satırından dosya verilerek açıldığında Küçült sekmesi dosyayı yüklüyor
  (`ekran/03-donustur.png`: `buyuk-1.mp4`, 124,6 MB, 1920x1080) ama Oynatıcı sekmesi
  9 saniye sonra hâlâ **"Yüklü Dosya Yok"** diyor (`ekran/20-oynatici-9sn-sonra.png`).
  `MainWindow.axaml.cs:471-473` açılış dosyasını yükler ama `Player`e devretmez — `ApplyLoaded`
  sonunda `Player.Load(path)` çağır. `[G]`

### 1.3 Açılış sekmesi sözleşmeyle çelişiyor

- `MainWindow.axaml:186` — `SelectedIndex="1"` (Küçült) yazıyor, uygulama **Oynatıcı** (indeks 0)
  ile açılıyor: `ekran/01-acilis-dosyali.png` Oynatıcı sekmesi seçili görünümde ve oynatıcı içeriği
  ekranda. `docs/plan.md` B/T185 "Küçült varsayılan" diyor — koşulan yazılım öyle değil.
  Sebebi bul (muhtemelen `Tabs.SelectedIndex` cs'de eziliyor), sonra sekmeyi sabitle. `[G]`
- Ek belirti: `ekran/03-donustur.png`'de içerik **Küçült** panosu, sekme şeridinde ise **Oynatıcı**
  seçili görünüyor — seçim göstergesi ile içerik ayrı düşüyor. `[G]`

### 1.4 Zoom ölçeği iki kez uygulanıyor, pan hiç uygulanmıyor

- `Playback/PlayerView.axaml.cs:93-96,359-363` + `PlayerView.axaml:27` — tekerlek yalnız `_zoom`'u
  günceller, `Frame` boyutu bir sonraki seek'e kadar değişmez; `Draw` `Scale*PanelScale` ile ölçeği
  çift sayar (`ZoomGesture.Scale` zaten `FitScale*PanelScale`, `ZoomGesture.cs:157`); `OffsetX/Y`
  hiç uygulanmıyor; `Image Stretch="None"` Width/Height'ı yok sayıyor — `RenderTransform` /
  `Stretch=Fill` + offset kullan, zoom sonrası yeniden yerleş. `[A3]`
- Canlı kanıt: `ekran/13-oynatici-dosyali.png` "zoom 100%" derken kaynağın 6 renk çubuğundan yalnız
  4'ü görünüyor, sağda geniş siyah boşluk. Kaynağın gerçek ilk karesi `ekran/kaynak-kare0.png`. `[G]`

### 1.5 Sabit çıktı klasörü ayarı ölü

- `MainWindow.axaml.cs:3157,3163` — `BuildUniqueOutputPath` her zaman `Path.GetDirectoryName(input)`
  kullanıyor; `AppSettings.OutputFolder` / `OutputFolderMode` kaydediliyor, okuyan yok —
  `CmbOutputFolderMode==1` iken dizini `TxtOutputFolder.Text`ten al, klasör yoksa kaynağa geri düş. `[A2,A4]`

### 1.6 Elle ffmpeg yolu ayarı ölü, üstelik ipucu metni yalan söylüyor

- `AppSettings.cs:686-687` + `Ffmpeg/ToolLocator.cs:9-13` — `FfmpegPathMode`/`FfmpegPath` kaydediliyor
  ama `ToolLocator`da setter yok; motor daima `tools/ffmpeg`, exe yanı ve PATH'i kullanıyor. İpucu
  (`settings-tab.ffmpeg-path.hint`) "seçtiğiniz yolu kullanır" diyor — `ToolLocator.Override(path)`
  ekle, `RestoreAppSettings`ten uygula, `_ffmpeg` önbelleğini sıfırla. `[A2,A4]`

### 1.7 `UpdateSettings.Save` ayar dosyasını buduyor

- `UpdateCheck.cs:529-566` — dosyayı `FileMode.Create` ile **yalnız kendi 25 anahtarıyla** yeniden yazıyor;
  `advMode…ffmpegPath` ve `defaultAppSuggestionDismissed` siliniyor. Çağrı yerleri
  `MainWindow.axaml.cs:1653,2198,2217`. `AppSettings.cs:662-665` yorumu tuzağı zaten anlatıyor —
  `UpdateSettings.Save`i oku-birleştir-yaz yap. `[A4]`
- Canlı kanıt: **"Bir Daha Sorma"** şeridi her açılışta yeniden çıkıyor
  (`ekran/01`, `03`, `21` — üçü de aynı oturumun ardışık kareleri). `[G]`
- Aynı kökten: `Integration/DefaultAppSuggestion.cs:28-29` reddi bu dosyaya yazıyor, siliniyor. `[A4]`

### 1.8 Yeni dosya yüklenince eski çıktı ile karşılaştırılıyor

- `MainWindow.axaml.cs:2441-2470,2586-2596` — `_lastOutput` sıfırlanmıyor; `RefreshPreviewSource` yeni
  kaynağı eski çıktıyla eşliyor, `BtnReveal`/`BtnShare` eski dosyayı açıyor — `ApplyLoaded` başında
  `_lastOutput=null; BtnReveal.IsVisible=false; ResetShare(false)`. `[A2]`

### 1.9 Sayı kutuları sessizce sahte değere düşüyor

- `MainWindow.axaml.cs:2244,3232` — `ParseTargetMb`/`ParseQualityTarget` yalnız InvariantCulture;
  TR klavyede yazılan `1,5` ya da `abc` sessizce 16 MB'a düşüyor ve `SaveSettings` (`:865`) bunu
  kalıcılaştırıyor — hata satırı göster, `Replace(',', '.')` ile ondalık virgülü kabul et. `[A2]`
- `MainWindow.axaml.cs:3753-3760` — `ApplyQualityRange` kaydırıcıyı kırpıyor ama kutuyu geri yazmıyor
  (`_syncing` bastırıyor); plan kutudan okuyor (`:3666`) → aralık dışı CRF motora gidiyor,
  `ConversionArguments.Validate` (`:7-30`) CRF aralığını denetlemiyor — kırpılan değeri kutuya geri yaz. `[A2]`

### 1.10 İş penceresi kapanınca kuyruk devam ediyor

- `ShrinkJobWindow.axaml.cs:214-218,334-340` — `OnClosing` yalnız aktif `_cts`i iptal ediyor;
  `PumpAsync` kapalı pencerede sıradaki isteği yeni `EncodeRunner` ile başlatıyor —
  `_pending.Clear()` + kapalı bayrağıyla döngüyü kes. `[A3]`

### 1.11 Ana pencere kapanırken oynatıcı süreçleri öksüz kalıyor

- `MainWindow.axaml.cs:490-498` — `Player.Close()` çağrılmıyor; `DecoderPipe`/`AudioSink`/
  `ContinuousPlayback` ffmpeg süreçleri arkada kalıyor — `OnClosing`e ekle. `[A3]`

### 1.12 Scrub bırakışta yanlış konuma atlıyor

- `Playback/ControlStrip.axaml.cs:150,265,273` — sürükleme sırasında `Position` setter `_position`ı
  ezmeye devam ediyor (`PanelHost.Drain` her karede yazıyor, `PanelHost.cs:756`); bırakışta
  `SeekRequested` scrub noktası yerine oynatma konumunu gönderiyor — ayrı `_scrubPosition` tut. `[A3]`

### 1.13 Karşılaştırma oynatıcısı açılışta başlamıyor

- `Playback/PanelHost.cs:955-960` — `(Durdu && _submitted==0)` dalı; açılışta panoda
  **"Karşılaştırma Oynatıcısı Başlayamadı"** yazıyor (`ekran/03-donustur.png`). ffmpeg 9.0 PATH'te
  mevcut; sebep bu denetimde kazılmadı — ayrı ölçüm gerek. `[G]`

### 1.14 Pencere minimumu ekrandan taşıyor

- `MainWindow.axaml:8` + `MainWindow.axaml.cs:300` — `MinHeight=720`, `Math.Max(MinHeight, …)`;
  1366×768 %125 (çalışma alanı ≈582 mantıksal px) ve 1080p %150 (≈690) ekranlarda pencere ekrandan
  taşıyor — `MinHeight`i çalışma alanına kırp veya 640'a indir. `[A4]`
- Canlı kanıt: 1080p %125 ekranda varsayılan 1560×1060 pencere görev çubuğunun altına taşıyor,
  alt şerit kesik (`ekran/01-acilis-dosyali.png` alt kenar). `[G]`

### 1.15 En küçük pencerede yerleşim kırılıyor

- `ekran/22-kucult-kucuk-pencere.png` (1040×720 mantıksal) — `Çözünürlük` başlığı kelime ortasından
  `Çözünürlü / k` diye bölünüyor (kırpma: `ekran/kanit-kelime-bolunmesi.png`); kaynak bilgi sütun
  başlıkları oluksuz çakışıyor; `HDR Aralığı` değerinin üstüne biniyor —
  `MainWindow.axaml:262-291` sabit `Grid` yerine `MinWidth` + `TextTrimming`, ya da dar genişlikte
  iki sütuna düşen `WrapPanel`. `[G]`

### 1.16 Tema belirteçlerinin dışına çıkan sabit renk ve ölçüler

- `Themes/Controls.axaml:356` — `PinkText` fırçası `#FFFF54EB` sabit renkle Controls içinde tanımlı —
  `Color`+`Brush` çiftini `Theme.axaml`e taşı. `[A1]`
- `MainWindow.axaml:238` — `Effect="drop-shadow(0 0 6 #FF00F3FF)"` sabit renk+ölçü — `Theme.axaml`e
  `DropIconGlow` belirteci ekle. `[A1]`
- `Themes/Controls.axaml:700-703,717` — CheckBox şablonunda `24,8,*` / `Width=24` / `20` sabit;
  Theme'de tanımlı `CheckGlyphSize`(20) **hiç kullanılmıyor** — 20→`CheckGlyphSize`, 24→`TargetMinSize`,
  8→`SpaceSm`. `[A1]`
- `MainWindow.axaml:421-443,537-539,562-564` — 13 `RadioButton` için hiç `ControlTheme` yok
  (`rg RadioButton Themes/` boş); görünüm FluentTheme vurgu renginden geliyor — `CheckStyle` gibi
  bir `RadioStyle` yaz. `[A1]`
- `Themes/Playback.axaml:134-135` — `#CC050507`, `#00050507` literal; `AppBgColor` (`Theme.axaml:10`)
  kopyası, tema değişince kayar — belirteçten türet. `[A3]`
- `Themes/Playback.axaml:87-113` — 20 ölçü `Theme.axaml` değerlerinin elle kopyası —
  `StaticResource` ile bağla. `[A3]`

---

## 2. Tutarsız

Çalışıyor ama kendi kuralıyla ya da kendisiyle çelişiyor.

### 2.1 Ondalık ayracı aynı ekranda iki türlü

- `MainWindow.axaml.cs:568` sözleşmesi "sayı tek yerden geçiyor" diyor; `:2571,2782,2907,2911,2918,2951,2957,2962-2963,3562,3573`
  `$"{x:0.0}"` ile CurrentCulture kullanıyor — hepsini `Num(...)`e çevir. `[A2]`
- Görsel kanıt, tek ekranda: Çıktı panosu **`Tahmini Çıktı 15,6 MB`** (virgül,
  `ekran/kanit-ondalik-cikti.png`), aynı ekranın Yapılacak İşlem panosu **`Tahmini Boyut 15.6 MB`**
  (nokta, `ekran/kanit-ondalik-plan.png`). Aynı karede ayrıca `Öngörülen Kalite 74,4/100` (virgül)
  ile Kalite kutusu `81.6` (nokta). Tam kare: `ekran/03-donustur.png`. `[G]`

### 2.2 Tahmini süre aynı panoda iki biçim

- `ekran/03-donustur.png` — `Tahmini Süre` altında `~50 Sn` ve hemen altında `25 Sn - 1,5 Dk`;
  biri saniye, biri karışık birim — tek biçimlendiriciye (`HumanDuration`) bağla. `[G]`

### 2.3 Süre biçimleri saat düşürüyor

- `MainWindow.axaml.cs:3563` — kalan süre `mm\:ss`; 60 dk üstü `1:05:00`→`05:00` olur. `[A2]`
- `MainWindow.axaml.cs:2570` — `TxtDuration` `hh\:mm\:ss`, 24 sa üstü sarıyor. `[A2]`
- `ShrinkJobWindow.axaml.cs:255` — ETA aynı kusur. `[A3]`
- Hepsi için ortak `HumanDuration` / saat dalı.

### 2.4 Kültür ve parse tutarsızlıkları

- `MainWindow.axaml.cs:876` vs `:3666` — aynı kutu (`TxtQuality`) biri kültürsüz biri Invariant parse —
  876'yı Invariant yap. `[A2]`
- `MainWindow.axaml.cs:3674` — `TxtCustomResolution` `int.TryParse` kültürsüz/`NumberStyles`siz —
  `NumberStyles.Integer, Invariant`. `[A2]`
- `MainWindow.axaml.cs:3720-3723` — `TimeSpan.TryParse` `"1:30"`u 1 sa 30 dk okuyor; kullanıcı
  1 dk 30 sn bekliyor — `mm:ss` / `hh:mm:ss` için özel parser. `[A2]`
- `Playback/PlayerView.axaml.cs:305` dil sınaması `== "tr"`, `ShrinkJobWindow.axaml.cs:360`
  `StartsWith("tr")` — `tr-TR` altında biri İngilizce basıyor; ortak yardımcı. `[A3]`

### 2.5 Oynatıcı durum satırı İngilizce sabit metin

- `Playback/PlayerView.axaml` durum satırı: `position 0 s - paused - zoom 100%`
  (`ekran/01-acilis-dosyali.png` alt sol). Arayüzün geri kalanı Türkçe — sözlüğe anahtar aç. `[G]`

### 2.6 Ayar yolu geçersiz kılması bir yerde yok sayılıyor

- `MainWindow.axaml.cs:1724` — `DismissedNoticePath`, `SettingsPathOverride`ı yok sayıp
  `UpdateSettings.DefaultPath`e yazıyor; ölçümde gerçek `AppData` kirleniyor. `[A4]`
- `ShrinkJobWindow.axaml.cs:95` — `UpdateSettings.Load()` override'sız çağrılıyor,
  `MainWindow.axaml.cs:462` ise `UpdateSettings.Load(SettingsPathOverride)` kullanıyor —
  taşınabilir/ölçüm kipinde iş penceresi başka dosyadan okur. `[G]`

### 2.7 Ayar dosyası atomik yazılmıyor

- `UpdateCheck.cs:529-566` / `AppSettings.cs:774` — kapanışta kesilirse dosya bozulur, `Load`
  sessizce varsayılana döner, tüm ayarlar kaybolur — `.tmp` + `File.Move(overwrite:true)`. `[A4]`

### 2.8 ffmpeg yoksa kullanıcı sebebi bulamıyor

- `MainWindow.axaml.cs:2647` — `BtnStart` sessizce pasif; sebep yalnız Hakkında sekmesinin en
  altında (`MainWindow.axaml:1171`) — düğme yanına `main.about.tool-missing`. `[A4]`
- `MainWindow.axaml.cs:784-788` — `_ffmpegVersion` hata metniyle önbelleğe alınıyor, "Yeniden dene"
  yok; kullanıcı ffmpeg'i kurunca da hata kalıyor — önbelleği hatada sıfırla, yeniden yokla düğmesi. `[A4]`
- `MainWindow.axaml.cs:1065-1075` — elle yol yalnız `File.Exists` ile doğrulanıyor, `-version`
  koşturulmuyor, en düşük sürüm hiçbir yerde tanımlı değil. `[A4]`
- `ShrinkJobWindow.axaml.cs:284-287` — `FileNotFoundException`ın İngilizce `ex.Message`i
  (`ToolLocator.cs:56`) TR arayüzde ham gösteriliyor. `[A3,A4]`

### 2.9 Sözlükte aynı kavram iki adla

- `Locales/tr/main.json:22,34` "Küçült" ↔ `:16,91,351` "Sıkıştırma" — birini seç. `[A4]`
- `Locales/tr/main.json:52,215` "Video kodeği" ↔ `:91` "Sıkıştırma algoritması" — birleştir. `[A4]`
- `Playback/PanelHost.cs:204-205` — rozet `PROCESSED · CRF n`; "yaklaşık" sözcüğü sözlükte yok,
  rozet yan etiketi tekrarlıyor — `playback.badge.approx` anahtarı aç. `[A3]`

### 2.10 Durum satırı sekiz yerden eziliyor

- `MainWindow.axaml.cs:2690-2707` — `TxtSystemStatus` tek yuva; `ReportUnsettledProbe` her
  `Recalculate`te araç/hata metnini eziyor, temizlerken `string.Empty` yazıp `UpdateToolStatus`
  çağırmıyor — probe için ayrı `TextBlock`, ya da temizlerken `UpdateToolStatus()`. `[A2]`

### 2.11 Açılışta gereksiz seri bekleme

- `MainWindow.axaml.cs:471-473` — sıralı `await`: açılış dosyası varken donanım yoklaması
  karmaşıklık ölçümü + kalibrasyon bitene kadar bekliyor, ilk plan donanımsız kuruluyor —
  `LoadStartupFileAsync`i ayır ya da yoklamayı öne al. `[A2]`
- `MainWindow.axaml.cs:3548,2514,2549` — `OnStart` `_probeCts`i iptal ediyor,
  `OperationCanceledException` yutuluyor, `TxtEstimateNote` "ölçülüyor" metninde kalıyor —
  iptal dalında `RefreshEstimateView()`. `[A2]`

### 2.12 Oynatıcı durum bayrakları gerçeği söylemiyor

- `Playback/PanelHost.cs:949-963` — boru dosya sonunda bittiğinde `IsPlaying` true kalıyor,
  şerit `❚❚` gösteriyor — `Durdu`da her durumda `false`. `[A3]`
- `Playback/PlayerView.axaml.cs:261-262` — `_playing` boru yokken de terslenip "oynatılıyor" diyor —
  `_pipe is null` ise erken dön. `[A3]`
- `ShrinkJobWindow.axaml:73-75` — `BtnClose` Bitti/Hata durumunda hâlâ "İptal" yazıyor —
  duruma göre "Kapat". `[A3]`

### 2.13 Klavye odağı

- `Playback/PlayerView.axaml.cs:171-172,185-198` — TopLevel'a tünel `KeyDown`; oynatıcı görünürken
  her Space/Esc pencere genelinde yutuluyor (odaklı TextBox dâhil), üstelik `:43`teki kendi
  handler'ıyla çift kayıt — odağı `PlayerView` içine sınırla. `[A3]`
- `Playback/ComparisonPanel.axaml.cs:504-512,662,870-878` — Space yalnız `Shell` odaklıyken;
  panoya tıklamak `Shell.Focus()` çağırmıyor — `OnStagePressed`de çağır. `[A3]`
- `Playback/ControlStrip.axaml.cs:277-298` — ok tuşları yalnız `Timeline` odaklıyken —
  `OnShellKey`e ilet. `[A3]`

### 2.14 Abonelik sızıntısı

- `Playback/ComparisonPanel.axaml.cs:973-984` — terfi hâlinde ağaçtan kopulursa `TopLevel`
  `KeyDown/PointerPressed` ve `overlay.SizeChanged` abonelikleri kalıyor; yalnız `Settle`da
  (`:832-842`) sökülüyor — `OnDetached`da `Settle()`. `[A3]`

### 2.15 DPI

- `Playback/ComparisonSurface.cs:297` + `PlayerView.axaml.cs:348` — bitmap DPI sabit `96`;
  `Stretch=None` ile %150 ölçekte kare DIP=piksel sayılıp bulanık büyüyor —
  `RenderScaling` ile oluştur ya da hedef dikdörtgenle çiz. `[A3]`

### 2.16 Sabit ölçüler ve genişlikler

- `MainWindow.axaml:309,871` `Width="96"` ↔ `:1044` `Width="120"` — aynı işi gören sayı kutusu iki
  genişlikte; tek `FieldWidthSm` belirtecine bağla. `[A1]`
- `MainWindow.axaml:1052,1069` — `TxtOutputFolder`/`TxtFfmpegPath` `Width="360"` sabit, uzun yol
  kırpılıyor — `Grid ColumnDefinitions="*,Auto"` + `Stretch`. `[A1]`
- `MainWindow.axaml:8` — `Width/Height` Theme'deki `WindowPreferredWidth/Height` kopyası
  (cs zaten `:299`da okuyor); `MinWidth/MinHeight` için belirteç yok — `WindowMinWidth/Height` ekle. `[A1]`
- `MainWindow.axaml:47` — `BorderThickness="0,0,0,1"` sabit — `BorderThinBottom` belirteci. `[A1]`
- `Playback/ComparisonPanel.axaml.cs:76,173,291,333,956`, `ComparisonSurface.cs:387`,
  `ControlStrip.axaml.cs:52-54,96` — belirteç **yedekleri** literal (`4`, `2`, `256`, `24`, `16`,
  `0xFF00F3FF`, `0.25`, `360`, `160`) — yedeği sıfır/istisna yap, sayıyı tekrarlama. `[A3]`

### 2.17 Tek koyu palet, sistem teması takibi yok

- `App.axaml:5` + `Themes/Theme.axaml` — `RequestedThemeVariant="Dark"` sabit, `ThemeDictionaries`
  ve açık palet yok — tasarım kararıysa `AGENTS.md`ye yaz, değilse `Default` + `ThemeDictionaries`. `[A4]`

### 2.18 Kontrast sınırda

- `Themes/Theme.axaml:11-12` — `TextDisabledColor #71717A` / `AppBgColor #050507` ≈ **4.0:1**;
  pasif metin için sınırda — `#8A8A94`e çek. (`Hint` `Body`den türeyip beyaz olduğu için ipuçları
  sorun değil.) `[A4]`

### 2.19 Ekran/konum hafızası yok

- `MainWindow.axaml.cs:291` — açılış boyutu daima `Screens.Primary`; ikincil ekranda konum, boyut ve
  `WindowState` hatırlanmıyor — kaydet, geri yüklerken `Screens.ScreenFromPoint` ile ekran içinde tut. `[A4]`

### 2.20 Bırakma reddi tek metin

- `MainWindow.axaml.cs:2326-2334` — koşum sürerken de (`:2285`) "tek dosya / klasör değil" diyor —
  sebep için üçüncü anahtar (`main.drop.busy`). `[A2]`

### 2.21 Varsayılan hedef kutusu tek yönlü

- `MainWindow.axaml.cs:1030,1040-1044` — varsayılan kutusu hedefi yazıyor, hedef/`ApplyLoaded` (`:2463`)
  varsayılanı güncellemiyor, `AppSettings`te ayrı alan yok — alanı kaldır ya da
  `CaptureAppSettings`e `DefaultTargetMb` ekle. `[A2]`

---

## 3. Kozmetik

- `Gelişmiş` sekmesi: ffmpeg komut kutusu kırpıyor, sarma ve yatay kaydırma yok; sekmenin alt
  yarısı boş (`ekran/21-gelismis.png`) — `TextWrapping="Wrap"` ya da `HorizontalScrollBarVisibility="Auto"`. `[G]`
- `ShrinkJobWindow.axaml` — varsayılan Windows başlık çubuğu kullanıyor; ana pencerede özel neon
  başlık var (`ekran/30-is-penceresi-3.png` ↔ `ekran/01-acilis-dosyali.png`) — aynı kabuğa geç. `[G]`
- `Playback/ComparisonPanel.axaml:100,108,158,159,174` — `BtnPanelMaximize/FullScreen/ZoomIn/ZoomOut`
  ve `SeparatorGrip`te `AutomationProperties.Name` yok. `[A3]`
- `MainWindow.axaml.cs:2266` — dosya seçici tür adı `"Media"` sabit İngilizce — `Say("main.pick.media")`. `[A2]`
- `MainWindow.axaml.cs:216-224` — `TxtTarget` her tuş vuruşunda `SaveSettings` (disk yazımı);
  `:206` her vuruşta `File.Exists` — `_recalculateTimer` gibi geciktir. `[A2]`
- `MainWindow.axaml.cs:3404-3417` — başarım ölçümü `CancellationToken.None`, iptal düğmesi yok. `[A2]`
- `MainWindow.axaml.cs:2259-2261` — koşum sürerken `OnBrowse` sessizce dönüyor, düğme etkin
  kalıyor (`MainWindow.axaml:249,255,801`) — `SetRunning`de kapat. `[A2]`
- `MainWindow.axaml.cs:1611-1632` — `OnShareDelete` iptal/zaman aşımı yok. `[A2]`
- `MainWindow.axaml.cs:4040-4050` — `paylasim-hedefleri.json` bozuksa sessizce `Fallback`. `[A2]`
- `MainWindow.axaml.cs:441` — `AppLogo` `Bitmap` hiç dispose edilmiyor. `[A2]`
- `MainWindow.axaml.cs:566-567` — `Num`un üstünde iki `<summary>`; biri `Speak`in öksüz yorumu. `[A2]`
- `Localization/Strings.cs:252` — değeri `null` olan anahtar `Get`ten `null` dönüyor — `?? key`. `[A4]`
- `ToolLocator.cs:28` — `WaitForExit(3000)` zaman aşımında süreç öldürülmüyor — `Kill()`. `[A4]`
- `Locales/en/main.json`, `tr/main.json` — UTF-8 BOM + CRLF, diğer altı dosya BOM'suz LF — BOM'u at. `[A4]`
- `MainWindow.axaml:179-184,1038` — `LangSwitch` ve `SettingsLangSwitch` iki ayrı boş StackPanel,
  ikisini de cs dolduruyor — tek yer yeter mi, karar senin. `[A1]`
- `MainWindow.axaml:338-341,362-365,372-375` — balon içinde iç içe iki `StackPanel` (aynı Spacing),
  dış katman işlevsiz. `[A1]`
- `MainWindow.axaml:200-201,781-782,1265-1266` çift boş satır; `:311-316,320,387-388` bozuk girinti;
  `:1177` `TabItem` girintisi 2 boşluk fazla. `[A1]`
- `ShrinkJobWindow.axaml.cs:113-114` — `"-"` yer tutucu literal. `[A3]`
- `Playback/ControlStrip.axaml.cs:201,311,334` + `PlayerView.axaml:17` — `|◀`, `❚❚`, `▶`,
  `--:-- / --:--`, `⋮` glif literalleri dağınık — merkezileştir. `[A3]`

---

## 4. Kusur değil — sınandı ve temiz çıktı

Rapora yanlış girmesin diye yazıyorum; ilk üçünü canlı koşumda kendim yanlış teşhis edip düzelttim.

- **Sekme geçişinde hayalet metin** — `12-gelismis.png`de başka sekmenin metni sızıyor sanıldı;
  4 sn beklemeli tekrarda (`ekran/21-gelismis.png`) sekme temiz. Çapraz geçiş animasyonunun
  ortasında yakalanmış kare, kusur değil.
- **Oynatıcının sahte desen gösterdiği** — kaynak klibin kendisi bir ffmpeg sınama deseni
  (`ekran/kaynak-kare0.png`). Kod-çözme doğru; kusur yalnız 1.4'teki kırpma.
- **İş penceresinin İngilizce açılması** — `settings.json`da `"language": "en"` yazıyordu, denetim
  sırasında dili İngilizceye ben çevirmiştim. Pencere kayıtlı dili doğru izliyor.
- **16 MB'ın kabuk menüsünden reddedilmesi** — geçerli boyutlar 100/250/500 MB, 1/2 GB;
  Türkçe uyarı doğru (`ekran/30-is-penceresi-3.png`). Doğrulama çalışıyor.
- Tanımsız `StaticResource`/`DynamicResource` anahtarı yok; EN 475 = TR 475 anahtar, küme farkı yok;
  kullanılan 471 anahtarın hepsi tanımlı; TR'de İngilizce kalıntı, kırık diakritik ya da kesik
  cümle yok. `[A1,A4]`
- UI iş parçacığı ihlali, çift tık, timer/CTS sızıntısı, iptal akışı, çift tamponlama: kanıt yok. `[A2,A3]`

---

## 5. `Danisma 009`un yedi geçiş koşulu — bugünkü durum

| # | Koşul | Durum | Kanıt |
|---|---|---|---|
| 1 | Plan panosu düğmeden bağımsız, mod + tahmini boyut satırı | **YAPILDI** | `MainWindow.axaml:621-632`, `cs:2862,2870,2907-2910` |
| 2 | Seçenek değişince plan 160 ms içinde yenilenir, değişen sayı vurgulanır | **YAPILDI** | `cs:172-176,420-432,2645,2908` |
| 3 | İzinler sıralanabilir tek kontrol | **YAPILMADI** | Hâlâ iki bağımsız `CheckBox`, `MainWindow.axaml:498-499` |
| 4 | Kaynak → plan `eski → yeni` karşılaştırması | **YAPILMADI** | `axaml:262-291` tek durum; `→` yalnız koşum sonucu metninde |
| 5 | Amaç (`Intent`) kontrolü kaldırıldı, etkisi plana yansıyor | **YAPILDI** | `CmbIntent` referansı 0; `cs:1260-1267,1306-1311` |
| 6 | Sol ORİJİNAL / sağ İŞLENMİŞ, yaklaşık parçada CRF | **YAPILDI** | `ComparisonPanel.axaml.cs:262-263`, `PanelHost.cs:203-205` |
| 7 | Tekerlek zoom tek yoldan, her çentik ilerler | **YAPILDI** (kod + birim testi) | `ComparisonPanel.axaml.cs:84,386-416`, `ZoomGestureTests.cs:49-62` |

Kaynak: `ajan/5-onceki-kosullar.md`.

**Belge notu.** `007/008/009` dosyalarının her satırına ` Danisma 00N — …` öneki yapışmış
(`009:1` = `Danisma 002 — UI Bitis Kapisi Denetcisi# Danisma 009`); 009 kendi uyarısıyla tam metin
değil. Denetimden ayrı bir temizlik işi. `[A5]`

---

## 6. `docs/plan.md` ile kodun çeliştiği satırlar

- **B / T185 "acilacak"** — aslında bitti (`git 2661e72 T185 muhurlendi`). Tablo güncellenmemiş.
  Ama bu denetimin 1.3 bulgusu tersini söylüyor: `SelectedIndex="1"` yazılı olmasına rağmen uygulama
  Oynatıcı'da açılıyor. **T185 mühürlü ama koşan yazılımda geçerli değil.** `[A5+G]`
- **C / T188 "acilacak"** — aslında bitti (`git 99b6d33 T188 muhurlendi`); C5 öneri şeridi
  `cs:252-255`, C6 `FileAssociationSetup.Ensure` `App.axaml.cs:78`. Tablo güncellenmemiş. `[A5]`
- **D / T186, E / T187** — git'te sözleşme yok, taşma/kesme panosu kodda yok
  (`overflow|tasma` grep boş). "acilacak" doğru. `[A5]`
- Yol haritası (`docs/tasks/yol-haritasi.md`) motor paketleri sayıyor; arayüz maddesi yalnız
  P1 1.3 HDR seçimi. Bu rapordaki 1.1–1.3 ve 1.15 maddelerinin yol haritasında karşılığı yok. `[A5]`

---

## 7. Önerilen sıra

Sıra senin, ama kırılganlığa göre en pahalıdan ucuza:

1. **1.7** ayar budaması — her açılışta kullanıcının ayarını yiyor, diğer ayar bulgularının hepsi
   bunun ardında bekliyor.
2. **1.1 + 1.2 + 1.4** oynatıcı üçlüsü — oynatıcı sekmesi bugün işlevsiz.
3. **1.3** açılış sekmesi — tek satırlık olması muhtemel, kullanıcının ilk gördüğü şey.
4. **2.1** ondalık ayracı — tek `Num()` süpürmesi, ~10 çağrı yeri, görünürlüğü yüksek.
5. **1.5 + 1.6** ölü ayarlar — ya bağla ya arayüzden kaldır; ipucu metni yalan söylüyor.
6. **1.14 + 1.15** dar ekran yerleşimi.
7. Geri kalanı.

---

## Ekler

- `ui-denetim-2026-09-08/ajan/1-xaml-tema.md` — MainWindow.axaml + tema (152.742 token, 199 sn)
- `ui-denetim-2026-09-08/ajan/2-code-behind.md` — MainWindow.axaml.cs (137.082 token, 283 sn)
- `ui-denetim-2026-09-08/ajan/3-oynatici-is.md` — oynatıcı + ShrinkJobWindow (177.454 token, 197 sn)
- `ui-denetim-2026-09-08/ajan/4-yerellestirme-ayar.md` — yerelleştirme + ayar kalıcılığı (144.419 token, 304 sn)
- `ui-denetim-2026-09-08/ajan/5-onceki-kosullar.md` — 007/008/009 koşullarının durumu (107.910 token, 201 sn)
- `ui-denetim-2026-09-08/ekran/` — 12 ekran görüntüsü ve kırpma

Ajan toplamı: **719.607 token**, en uzun ajan 304 sn (beşi paralel koştu).
