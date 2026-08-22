# Dış dünya sınırı incelemesi

Kapsam: `FfprobeClient`, `EncoderCapabilities`, `ToolLocator`, `PlanParser`, `PromptBuilder`. Salt okuma. Yollar `src/` köküne göre.
En kritik üçü: yapıştırılan JSON'la çökme, doğrulanmayan üç plan alanının ffmpeg'e gitmesi, stderr okunmadığı için süreç kilitlenmesi.

## 1. Ne yapıyor

- `ToolLocator` — `tools/ffmpeg` → uygulama klasörü → PATH sırasıyla ikiliyi bulur; tüm süreçlerin `ProcessStartInfo`'sunu üretir (ArgumentList, kabuk yok).
- `FfprobeClient` — `-print_format json -show_format -show_streams` çıktısını `MediaInfo`'ya çevirir: süre, çözünürlük, fps, codec, renk/HDR üstverisi, ses.
- `EncoderCapabilities` — `-encoders`/`-filters`/`-version` ayrıştırır, bir kodlayıcıyı 1 karelik test kodlamasıyla dener, sonucu önbelleğe alır.
- `PromptBuilder` — kaynak + hedef + yerel taban planını tek metin isteme çevirir.
- `PlanParser` — yapıştırılan metinden JSON'u çıkarır, `EncodePlan`'a bağlar, kabul listeleriyle doğrular, `extraArgs`'ı süzer.

## 2. Güvenlik ve girdi doğrulama

**Kritik — yapıştırılan metin uygulamayı çökertiyor.** `try/catch` yalnız `Deserialize` çevresinde (`PlanParser.cs:36-40`); üç yakalanmamış yol:
- `{"mode": null}` → NullReferenceException, `PlanParser.cs:48`
- `{"codec": null}` → `IsValidPreset(null,…)` → `TryGetValue(null)` ArgumentNullException, `PlanParser.cs:117` (`FfmpegArguments.cs:42`)
- `{"extraArgs": [null]}` → `allowed.TryGetValue(null,…)` ArgumentNullException, `PlanParser.cs:159`

Çağıran taraf korumasız: `VidShrink.App/MainWindow.xaml.cs:415` ve `:262` (her seçenek değişiminde kutudaki metni yeniden ayrıştırır).

**Yüksek — kabul listesi dışında üç alan sızıyor.** JSON'a bağlı ama `PlanParser`'da hiç denetlenmeyenler:
- `pixelFormat` (`EncodePlan.cs:63`) → doğrudan `-pix_fmt <değer>` (`FfmpegArguments.cs:84`), serbest metin; `null` verilirse argüman listesine null öğe girer.
- `audioChannels` (`EncodePlan.cs:57`) → `-ac <n>` (`FfmpegArguments.cs:103`), aralık yok.
- `videoBitrateK`/`audioBitrateK`: yalnız 2pass'te `>0` denetimi (`PlanParser.cs:100`), üst sınır yok. Büyük değerde `*2`/`*4`/`*1.5` int taşmasıyla negatif `-maxrate`/`-bufsize` üretir (`FfmpegArguments.cs:67`, `:73`).

Kabuk devrede olmadığı için sonuç enjeksiyon değil, bozuk argüman ve kodlama hatası.

**Komut enjeksiyonu riski yok.** Her çağrı `ArgumentList` + `UseShellExecute=false` (`ToolLocator.cs:65-76`); `cmd.exe` veya birleştirilmiş komut dizesi yok. Tek istisna gösterimlik `ToCommandLine` (`FfmpegArguments.cs:121-129`): sadece boşluklu argümanı tırnaklıyor, tırnak/`^`/`&` kaçışı yok — ekrandaki bu dize kabuğa yapıştırılırsa güvensiz (`MainWindow.xaml.cs:283`, `:526`).

**Dosya yolu.** `File.Exists` denetimi var (`FfprobeClient.cs:12`), yol ayrı argüman (`:20`); ama `-i`/`--` ayırıcısı yok, tire ile başlayan yol ffprobe'a seçenek gibi görünür. ffmpeg tarafında `-i` kullanılıyor (`FfmpegArguments.cs:46`), sorun yok.

**Orta — doğrulama ile ffmpeg'in gerçeği ayrışıyor.**
- Codec denetimi harf duyarsız (`PlanParser.cs:45`), ffmpeg duyarlı: `"LIBX264"` geçer, kodlamada patlar. `HasEncoder` de duyarsız (`EncoderCapabilities.cs:25`), "var" der.
- `PlanParser` hiç `WorksAsEncoder` çağırmıyor: makinede olmayan `h264_nvenc` kabul edilir. Otomatik yol bunu yapıyor (`PlanCalculator.cs:488`), AI yolu yapmıyor.
- HDR tamamen atlanıyor: `HdrResolver` çağrılmıyor, `HdrVideoFilter`/`HdrColorArgs` `[JsonIgnore]` olduğu için boş kalır (`EncodePlan.cs:64-65`). HDR kaynak AI planıyla kodlanırsa ne tonemap ne renk üstverisi yazılır (otomatik yol: `PlanCalculator.cs:69`).

**İyi olan taraf.** `SanitizeExtraArgs` (`PlanParser.cs:142-169`) bayrak+değer çiftlerini beş elemanlı izin listesine ve bayrak başına değer doğrulayıcısına bağlıyor; gerisi uyarıyla atılıyor. Sınırın en sıkı yeri burası.

**Düşük.** `ExtractJson` regex'inde zaman aşımı yok, girdi boyutu sınırsız (`PlanParser.cs:135`). CRF aralığı sabit 0–51 (`:106`), libsvtav1'in 0–63'ünü daraltır. Tek sayı yuvarlaması genişliği kaynağın üstüne çıkarıp "upscale" hatası doğurabilir (`:61-62` → `:72`).

**PromptBuilder ↔ parser uyuşmazlığı.** Şablon 3 codec sunuyor (`PromptBuilder.cs:35`), parser 12 kabul ediyor (`PlanParser.cs:13`). `pixelFormat`/`audioChannels` şablonda yok ama deserialize ediliyor. `extraArgs` izin listesi anlatılmıyor → modelin ekleri sessizce atılıyor (`PlanParser.cs:161`). Kurallar satırında (`PromptBuilder.cs:49`) en-boy oranını koruma ve kaynağı aşmama yok, oysa parser ikisini de **hata** sayıyor (`PlanParser.cs:73`, `:78`). Prompt'a yalnız ffprobe'un kendi enum/sayıları giriyor; dosya adı veya etiket metni girmiyor, prompt enjeksiyonu yüzeyi yok (`PromptBuilder.cs:10-29`).

## 3. Dış süreç dayanıklılığı

- **Kritik — `RunCapture` stderr'i hiç okumuyor** (`EncoderCapabilities.cs:127-131`): ffmpeg stderr borusunu doldurursa `ReadToEnd()` asılır ve `WaitForExit(5000)` okumadan *sonra* geldiği için hiç çalışmaz. Zaman aşımında süreç öldürülmüyor, yarım çıktı ayrıştırılıyor.
- **Kritik — ffprobe okumaları sıralı** (`FfprobeClient.cs:25-26`): önce stdout sonuna kadar, sonra stderr. stderr borusu dolarsa stdout hiç EOF görmez → kilitlenme. `Task.WhenAll` şart.
- **Yüksek — zaman aşımı yok.** `ProbeAsync`'in tek koruması token, çağıran token vermiyor (`MainWindow.xaml.cs:170`); iptalde süreç öldürülmüyor (`FfprobeClient.cs:27`). `GetFfmpegVersion` UI iş parçacığında (`MainWindow.xaml.cs:127`) ve `ReadLine()` süresiz bloklar; `WaitForExit(3000)` dolduğunda da süreç öldürülmüyor (`ToolLocator.cs:25-26`).
- **Yüksek — beklenmedik çıktı yakalanmıyor.** `JsonDocument.Parse` (`FfprobeClient.cs:32`) ve `GetProperty("format"/"streams")` (`:34-35`) eski/kısıtlı sürümde JsonException veya KeyNotFoundException atar; sarmalanmadığı için ham .NET istisnası kullanıcıya çıkar.
- **Orta — sürüm kapısı yok.** `EncoderCapabilities.Version` üretiliyor ama projede hiç okunmuyor (`:16`, `:101-105`); asgari ffmpeg sürümü denetlenmiyor.
- **Orta — ikili yoksa hata kayboluyor.** `Locate` anlamlı istisna atıyor (`ToolLocator.cs:47`), ama `Load` her istisnayı yutup boş küme + `"unknown"` döndürüyor (`EncoderCapabilities.cs:117-122`) → "ffmpeg yok" ile "hiç kodlayıcı yok" ayırt edilemez.
- **Orta — çıktı kodlaması.** `StandardOutputEncoding` ayarlanmıyor (`ToolLocator.cs:67-74`); Windows konsol kod sayfası kullanılır, ffprobe'un UTF-8 üstverisi ve yolları bozulur.
- **Düşük.** PATH taraması göreli dizin girdilerini kabul ediyor (`ToolLocator.cs:53-58`); `_ffmpeg`/`_ffprobe` kilitsiz statik alanlar (`:7-11`, yalnızca iş tekrarı).

## 4. ffprobe ayrıştırma

- **Alanlar:** `format.duration`, `format.bit_rate`; akıştan `width/height`, `avg_frame_rate`, `r_frame_rate`, `codec_name`, `pix_fmt`, `color_*`, `bits_per_raw_sample`, `field_order`, `tags.rotate`, `side_data_list` (rotation, mastering display, content light level), `disposition.attached_pic`; sesten `codec_name`, `bit_rate`, `channels`.
- **Boşta ne oluyor:** süre `format`→akış→hata (`:50-52`); bit hızı `format.bit_rate`→dosya boyutundan (`:67`); fps `avg`→`r`→**sabit 30** (`:65`); ses bit hızı yoksa **sabit 128 kbps** (`:79`, 640k'lık DTS izinde bütçeyi yanıltır), kanal yoksa 2 (`:80`); bit derinliği `bits_per_raw_sample`→pix_fmt (`:74`).
- **VFR doğru ele alınmıyor.** `avg_frame_rate` doğru tercih, ama `avg`/`r` karşılaştırılmadığı için VFR hiçbir yere işaretlenmiyor; `-fps_mode`/`-vsync` verilmiyor ve `plan.Fps==info.Fps` iken `-r` de eklenmiyor (`FfmpegArguments.cs:56`). Süreye dayalı boyut matematiği sapar. `ParseFraction` 0.1–1000 dışını eliyor (`:182`), `0/0` böylece `r_frame_rate`'e düşüyor.
- **Döndürme doğru:** `tags.rotate` + `side_data.rotation`, ±90/270'te takas (`:136-142`). İki kusur: side_data döngüsü son eşleşmeyle önceki değeri eziyor (`:141`), `DisplayDimensions` iki kez çağrılıyor (`:63-64`). Ayrıca `scale=W:H` bu görüntü boyutlarını kullanıyor (`FfmpegArguments.cs:50`), yani ffmpeg'in varsayılan autorotate davranışına bel bağlanıyor; `-autorotate`/`-noautorotate` hiç açıkça verilmiyor.
- **HDR yanlış pozitifi:** `pix_fmt` içinde `10le` geçen her kaynak HDR sayılıyor (`:69`) → 10 bit SDR dosyalar gereksiz tonemap yoluna girer.
- **Diğer:** ilk video ve ilk ses akışı seçiliyor (`:41-42`), yorum ses izi başta ise o kullanılır; `field_order` yoksa ilerlemeli varsayılıyor (`:77`); `GetInt` alan Object/Array ise `GetString()` üzerinden InvalidOperationException atar (`:157`).

## 5. Önbellek

- `Lazy<EncoderCapabilities>` süreç ömrü boyunca tek kez çalışır (`:8`); hata durumunda boş küme kalıcılaşır (`:117-122`) → **kalıcı yanlış negatif**, oturum sırasında kurulan ffmpeg yeniden başlatmadan görülmez.
- `_probed` sözlüğünde geçersizleştirme veya süre yok (`:12`, `:32-35`).
- 4 sn'lik deneme sınırı (`:54`) soğuk GPU sürücüsünde ilk NVENC/QSV açılışını aşabilir → kalıcı yanlış negatif. Kilit deneme boyunca tutuluyor (`:30`), sorgular serileşir.
- Deneme 256x256/8 bit/tek kare (`:46`): gerçek 4K-10 bit işi kanıtlamaz → **yanlış pozitif**. `lavfi` veya `testsrc2` içermeyen sade bir yapıda her deneme başarısız → topluca yanlış negatif.
- `WorksAsEncoder` yalnız `HasEncoder` geçerse deniyor (`:33`); küme harf duyarsız (`:25`), yanlış yazımlar "var" görünür.
- Ayrıştırıcılar çıktı biçimine sıkı bağlı: `ParseEncoders` bayrak sütununu tam 6 karakter ve nokta/büyük harf şartına bağlıyor (`:80`) — biçim değişirse sessizce boş liste; `ParseFilters`'ta uzunluk denetimi yok (`:95`), yanlış satır kabul edebilir.
- `Task.WaitAll(…,1000)` sonucu yok sayılıyor (`:59`); tek denemede iki ayrı zaman aşımı var.
