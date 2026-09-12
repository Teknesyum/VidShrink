# Plan — Arayüz kabuğu, simge takımı, oynatıcı, kaydedici

Girdi: `docs/arastirma/ekran-kaydedici-ozellik-karsilastirmasi.md`,
`docs/netlestirme/013-kalan-11-is-icin-kurulus-sirasini-ve-uc-.md`,
arayüz haritası (bu belgenin "Bugünkü hal" bölümü).

Kullanıcının turu 12 işe ayrıldı; 1'i (varsayılan uygulamalar derin bağlantısı) kapandı.
Kalan 11'i dört kesite bölünüyor. Kesitler arası bağımlılık tek yönlü: **A → B → D**,
**C** bağımsız.

## Okumalar (fable'ın beş sorusuna verilen cevap)

| # | Soru | Karar | Dayanak |
|---|---|---|---|
| 1 | "ayarlar sol tarafın en sağında" — yatay mı dikey mi | **Yatay şerit kalıyor**, Ayarlar o şeridin en sağ sekmesi oluyor | Şerit `HorizontalAlignment="Left"` ile sola yaslı (`Controls.axaml:805`); "sol taraf" o sola yaslı öbek, "en sağı" öbeğin son sekmesi. "Ayrı panel olmasın" da başlık çubuğundaki ayrı `BtnSettings` + dişli ikilisini kaldırmak demek. Dikey şeride geçiş `TitleBarHeight`=30 bandını ve `WorkspaceMargin`ı baştan yazmayı gerektirirdi; cümlede bunu isteyen bir iz yok. **Kullanıcıya tek cümleyle doğrulatılıyor.** |
| 2 | Ayarların simgesi | Bugünkü dişli, ama glif değil geometri olarak; başlık çubuğundaki dişli ve `BtnSettings` kalkıyor | "ayarlarınki de aynı simge olsun" — bugünkü ⚙ ile aynı |
| 3 | Simge takımının kaynağı | **Tek `Themes/Icons.axaml`, `StreamGeometry` takımı.** Şekiller Lucide'dan (ISC lisansı, ticari kullanım serbest) alınıyor, yol verisi kopyalanıyor | Font gömmek 42 dilin font yığınına karışır; svg varlığı Avalonia'da ayrı bir çözücü ister. Geometri `Foreground`dan renk, belirteçten ölçü alır — uydurma renk/ölçü yasağıyla tek uyumlu yol. Testle pimlenebilir (anahtar başına geometri var mı). |
| 4 | "uyarı oynatıcının üstünde çıksın" | Oynatıcının **üzerine binen** katman; içeriği aşağı itmiyor | "oynatıcı tüm alanı kapsayacak" cümlesiyle aynı nefeste söyleniyor — iten bant o cümleyi bozar |
| 5 | Otomatik kip gerçekten ölçsün mü | **Ölçüyor.** Kapalı aday kümesi, aday başına kısa kayıt, atlanan kare sayımı | OBS'in kabul ölçütü bu; depoda "rapora giren her sayı ölçümden çıkar" kuralı var. Tablo tahmini kullanıcının makinesini bilmiyor. |

## Kesit A — Sekme şeridi ve simge takımı

Dosyalar: `Themes/Icons.axaml` (yeni), `Themes/Theme.axaml` (simge ölçü belirteci),
`Themes/Controls.axaml` (`NeonTabItem` şablonu simge + yazı), `MainWindow.axaml`
(sekme sırası, başlıkların simgelenmesi, başlık çubuğundaki iki düğmenin kalkması),
`MainWindow.axaml.cs` (`OnOpenSettings` / `MarkSettingsButton` / `SettingsTabIndex` yolu),
`Playback/PlayerView.axaml` (emoji ses simgeleri), `Themes/Playback.axaml`.

1. `Themes/Icons.axaml` — anahtar başına `StreamGeometry`. İlk takım:
   `IconPlayer`, `IconShrink`, `IconConvert`, `IconRecorder`, `IconAdvanced`, `IconAbout`,
   `IconSettings` (dişli), `IconPlay`, `IconPause`, `IconBack10`, `IconForward10`,
   `IconVolume`, `IconVolumeMute`, `IconSpeed`, `IconFullscreen`, `IconMenu`, `IconClose`,
   `IconMaximize`, `IconChevronDown`.
2. `Theme.axaml`'e simge ölçüsü belirteçleri (`IconSizeSm`/`IconSizeMd`) — var olan
   `CheckGlyphSize`/`TargetMinSize` mantığıyla aynı yerde.
3. `NeonTabItem` şablonu: `Path` + `TextBlock` yatay ikili. `Header` düz metin kalıyor,
   simge `TabItem.Tag`'ten ya da attached property'den geliyor — `MainWindow.axaml.cs:2620`
   zaten `StackPanel` başlığı olasılığını tanıyor, o yol bozulmayacak.
4. Ayarlar sekmesi görünür oluyor, XAML'de **en sona** taşınıyor; `BtnSettings` ve başlık
   çubuğundaki dişli kalkıyor. `SettingsTabIndex` `Tabs.Items.IndexOf(TabSettings)` olduğu
   için sıra değişimi kendiliğinden takip ediyor; `OnOpenLanguageSettings` ve oynatıcı
   menüsündeki "Ayarlar" satırı aynı sekmeye gitmeye devam ediyor.
5. Emoji ve metin glifleri geometriye çevriliyor: 🔉/🔊, ⋮, «, », ▶/❚❚, ⚙, □, ×, ▾, `-10`/`+10`.

Pim: `tests/VidShrink.Tests` içinde yeni `SimgeTakimiTests` — her `Icon*` anahtarının
`Icons.axaml`'de var olduğu, hiçbir `Button.Content`'in emoji aralığında karakter taşımadığı,
Ayarlar sekmesinin son sırada ve görünür olduğu.

## Kesit B — Oynatıcı

Dosyalar: `Playback/PlayerView.axaml`, `Playback/PlayerView.Serit.cs`,
`Playback/TrackButtons.axaml` (+`.cs`) kaldırılıyor (`trash/`e), `Themes/Playback.axaml`,
`Themes/Controls.axaml` (uyarı yığınının yeri), `MainWindow.axaml`.

Yeni kod olan / var olanın sadeleşmesi olan ayrımı:

| İş | Durum |
|---|---|
| Başlık, altyazı/ses parçası, üç nokta kalksın | **Sadeleşme** — üst satır tümüyle siliniyor, `Grid RowDefinitions="Auto,*"` → tek satır. Altyazı ve ses parçası zaten sağ klik menüsünde (`AddTrackMenus`). |
| Sağ klik menüsü tüm ayarları taşısın | **Zaten var** — `Keymap.cs:104`. Üç nokta gidince tek yol kalıyor; `MenuAtPointer` kolu sadeleşiyor. |
| Alt panel gizli, fare aşağı gelince görünsün | **Zaten var** — `PlayerView.Serit.cs:82-130`. Kullanıcı görmüyorsa sebebi ayrı: şerit bugün sahnenin değil sekmenin altında duruyor ve `WorkspaceMargin` boşluğu var. Sahne tam alanı kaplayınca kendiliğinden doğru yere oturuyor. |
| Oynatıcı tüm alanı kapsasın | **Yeni** — oynatıcı sekmesi için `WorkspaceMargin` sıfırlanıyor (şablonda sekmeye özel sınıf), `Stage` `Padding=0` zaten. |
| Ses ve hız progressbar gibi, sayı görünsün | **Yeni** — düğme çiftleri gidiyor, yerine `Slider` + değer yazısı. `SliderThumbSize` belirteci var. |
| −10/+10 simge, play ortada, daha büyük/geniş | **Yeni yerleşim** — 10 sütunlu ızgara yeniden bölünüyor: play ortada, `PlaybackBarButtonSize`den büyük yeni bir belirteç (`PlaybackPlayButtonSize`). |
| Uyarı oynatıcının üstünde | **Yeni** — uyarı yığını `TabControl` şablonundan çıkıp `MainWindow` kökünde içeriğin üstüne binen bir katmana taşınıyor; böylece her sekmede aynı davranıyor ve hiçbir sekmeyi itmiyor. |

Pim: `OynaticiGorunumTests`'e ekler — başlık/parça düğmelerinin yokluğu, play düğmesinin
ızgaradaki sütunu ve ölçüsü, ses/hız `Slider` türü, uyarının oynatıcı yüksekliğini
düşürmediği (yerleşim ölçüsü).

## Kesit C — Kaydedici, ffmpeg kolu (bağımsız)

Araştırmanın (a) kümesinden, otomatik kipin **girdisi olanlar önce**:

1. Kayıt kabı seçimi (mp4/mkv/mov) — yarım dosya sorununu kapatıyor.
2. Çıktı çözünürlüğü ölçekleme (`-vf scale`).
3. Keyframe/profil/tune (`-g`, `-profile:v`, `-tune`).
4. Hedef bit hızı / CBR kolu — `CodecModel.BitrateRateControlArgs` bağlanıyor.
5. Piksel biçimi / renk uzayı.
6. Süre sınırı ve otomatik dosya bölme.
7. Ayrı ses izleri, ses filtreleri (volume/agate/afftdn).
8. Ekran görüntüsü düğmesi (`FrameGrabber` bağlanıyor).
9. Windows çoklu monitör seçimi.

## Kesit D — Otomatik kip (A ve C'den sonra)

C'nin 1-5'i olmadan anlamsız: sihirbazın kararı **kap + çözünürlük + kodlayıcı + hız
denetimi** dörtlüsünü yazacak yer ister; bugün kayıt kolunda yalnız kalite tabanlı tek kol var.

Model (OBS'in kayıt kolu, yayın kolu alınmıyor):
- Aday kümesi kapalı: 2160p, 1440p, 1080p, 720p, 480p × {30, 60}.
- Donanım envanteri `EncoderAvailability`den; yeğleme NVENC > QSV > Apple VT > AMF > x264.
- Her aday için kısa gerçek kayıt, `RecorderSession`ın saydığı atlanan kare okunuyor.
- Kabul ölçütü ve eşikler **ölçümden** yazılıyor (`tools/VidShrink.Bench`), OBS'in 10 karesi
  varsayılan değil başlangıç noktası.
- En çok 3 kazanan; aralarından CPU sınıfı tavanını aşmayan en yükseği seçiliyor.

Pim: `KayitOtomatikKipTests` — aday kümesinin kapalılığı, elenen adayın seçilmemesi,
donanım yokken x264'e düşme, uydurma kodlayıcının negatif kontrolü.

## Sıra

A → B → C → D. A ve C aynı dosyalara dokunmuyor, paralel alt ajana verilebilir
(A: `Themes/`, `MainWindow.axaml`; C: `src/VidShrink.Core` kayıt kolu, `Recorder*`).
B, A'nın simge takımını bekliyor. D, C'yi bekliyor.
