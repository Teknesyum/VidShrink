# Ajan 4 — Yerelleştirme ve ayar kalıcılığı denetimi (ham çıktı, 144.419 token, 304 s)

## Kırık

- `src/VidShrink.App/AppSettings.cs:686-687` + `MainWindow.axaml.cs:3157` — `FfmpegPathMode=1` (Elle) ve `FfmpegPath` kaydediliyor ama hiçbir tüketicisi yok; `ToolLocator` (`src/VidShrink.Ffmpeg/ToolLocator.cs:12`) yalnız `tools/ffmpeg`, exe yanı ve PATH'e bakar, App'ten sadece `Ffmpeg/IsAvailable/GetFfmpegVersion` çağrılıyor. İpucu metni (`settings-tab.ffmpeg-path.hint`) "seçtiğiniz yolu kullanır" diyor — yalan — `ToolLocator`'a `Override(string? path)` ekle, `RestoreAppSettings`'te uygula, `_ffmpeg` önbelleğini sıfırla.
- `AppSettings.cs:680-681` + `MainWindow.axaml.cs:3157-3164` — `OutputFolderMode=1` / `OutputFolder` kaydediliyor ama çıktı yolu her zaman `Path.GetDirectoryName(inputPath)`; sabit klasör ayarı ölü — 3157'de mod 1 ise `TxtOutputFolder.Text` kullan (var olmayan klasörde geri düş).
- `MainWindow.axaml.cs:1653, 2198, 2217` — `UpdateSettings.Save` tek başına çağrılıyor; `UpdateCheck.cs:529-566` dosyayı `FileMode.Create` ile yalnız kendi 25 anahtarıyla yeniden yazar → `advMode…ffmpegPath` ve `defaultAppSuggestionDismissed` silinir (AutoUpdate kutusu, FastGpu kutusu, ilk açılıştaki donanım kararı). `AppSettings.cs:662-665` yorumu bu tuzağı zaten söylüyor — üç yerde ardına `CaptureAppSettings().Save(...)` ekle ya da `UpdateSettings.Save`'i oku-birleştir-yaz yap.
- `Integration/DefaultAppSuggestion.cs:28-29` — "varsayılan uygulama" reddi `settings.json`'a yazılıyor; üstteki silme yüzünden dil/ayar değiştirince şerit yeniden çıkar — aynı düzeltme.
- `MainWindow.axaml:8` + `MainWindow.axaml.cs:300` — `MinHeight=720`, `Math.Max(MinHeight, …)`; 1366×768 %125 (çalışma alanı ≈582 mantıksal px) ve 1080p %150 (≈690) ekranlarda pencere ekrandan taşar — `MinHeight`'ı çalışma alanına kırp veya 640'a indir.

## Tutarsız

- `MainWindow.axaml.cs:784-788` — ffmpeg yokken `_ffmpegVersion` hata metniyle önbelleğe alınır, `UpdateToolStatus` yalnız açılışta ve dil değişince koşar; kullanıcı ffmpeg'i kurduktan sonra "Yeniden dene" düğmesi yok, durum satırı `Hakkında` sekmesinin en altında (`MainWindow.axaml:1171`) — bırakma alanına uyarı + yeniden yokla düğmesi, hata durumunda önbelleği sıfırla.
- `MainWindow.axaml.cs:2647` — ffmpeg yokken `BtnStart` sessizce pasif; sebep ancak Hakkında sekmesinde — düğme yanına `main.about.tool-missing` göster.
- `MainWindow.axaml.cs:1065-1075` — Elle yol doğrulaması yalnız `File.Exists`; `-version` koşturulmuyor, sürüm alt sınırı hiçbir yerde yok (`src/VidShrink.Ffmpeg` içinde min sürüm karşılaştırması bulunmadı) — `GetFfmpegVersion` ile yokla, eski sürümü uyar.
- `ShrinkJobWindow.axaml.cs:284-287` — ffmpeg yoksa `FileNotFoundException`'ın İngilizce `ex.Message`'ı (`ToolLocator.cs:56`) TR arayüzde ham gösterilir — `ToolLocator.IsAvailable` ile önden kontrol edip `main.about.tool-missing` kullan.
- `MainWindow.axaml.cs:291` — açılış boyutu her zaman `Screens.Primary`; ikincil ekranda açılış/konum/boyut/`WindowState` hiç hatırlanmıyor (`Closing`'de yazım yok, ayar alanı yok) — konum+boyut+durumu kaydet, geri yüklerken `Screens.ScreenFromPoint` ile ekran içinde tut.
- `App.axaml:5` + `Themes/Theme.axaml` — `RequestedThemeVariant="Dark"` sabit, `ThemeDictionaries`/açık palet yok, sistem teması takibi yok; tek palet olduğu için açık/koyu çift karşılaştırması yapılamadı — tasarım kararıysa AGENTS.md'ye yaz, değilse `Default` + `ThemeDictionaries`. (default, unmeasured)
- `Themes/Theme.axaml:11-12` — `TextDisabledColor #71717A` / `AppBgColor #050507` kontrastı ≈4.0:1; pasif metin için sınırda, `Hint` (Controls.axaml:25) `Body`'den türeyip beyaz olduğundan ipuçları sorun değil — pasif rengi `#8A8A94`'e çek.
- `Locales/tr/main.json:22,34` vs `:16,91,351` — aynı işlem iki adla: sekme/düğme "Küçült", tagline ve kodek etiketi "Sıkıştırma" ("Sıkıştırma algoritması") — birini seç.
- `Locales/tr/main.json:52,215` vs `:91` — aynı kavram "Video kodeği" ve "Sıkıştırma algoritması" — "Video kodeği"ne birle.
- `MainWindow.axaml.cs:1724` — `DismissedNoticePath` `SettingsPathOverride`'ı yok sayıp `UpdateSettings.DefaultPath`'e yazar; ölçümde gerçek AppData kirlenir — override'ı kullan.
- `UpdateCheck.cs:529-566` / `AppSettings.cs:774` — atomik yazım yok; kapanışta kesilirse dosya bozulur, `Load` sessizce varsayılana döner ve tüm ayarlar kaybolur — `.tmp` + `File.Move(overwrite:true)`.

## Kozmetik

- `Locales/en/main.json`, `tr/main.json` — UTF-8 BOM + CRLF, diğer altı dosya BOM'suz LF (`file` çıktısı); `JsonSerializer` BOM'u yutuyor ama gereksiz fark — BOM'u at.
- `Localization/Strings.cs:252` — değeri `null` olan anahtar `Get`'ten `null` döner (`Deserialize<Dictionary<string,string>>` null kabul eder) — `?? key` ile koru.
- `ToolLocator.cs:28` — `WaitForExit(3000)` zaman aşımında süreç öldürülmüyor — `Kill()`.

## Kanıtsız / temiz çıkanlar

- Anahtar kümeleri: EN 475 = TR 475, iki dilde küme farkı yok; kod tarafında kullanılan 471 anahtarın tamamı tanımlı; tanımlı-kullanılmayan yok (`settings-tab.*` beş anahtar `Say()` ile 688-697'de kullanılıyor). Yer tutucu `{n}` kümeleri tüm çiftlerde eşit.
- TR'de İngilizce kalmış metin, kırık diakritik (i/ı, ş/s) veya kesik cümle bulunamadı; sesli tarama ve noktalama karşılaştırması sıfır sonuç verdi.
- Bozuk JSON: `UpdateSettings.Load` ve `AppSettings.Load` kök-nesne kontrolü + `JsonException` yakalıyor, çökmez. Ayar yolu `ApplicationData/VidShrink/settings.json`, platform bağımsız.
- Dil: `ResolveLanguage` kayıt → sistem → `en`; dil değişince `SaveSettings` koşuyor (`:670`), canlı yenileme var.
