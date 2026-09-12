[[netlestirme:013]]

# Netleştirme: Kalan 11 iş için kuruluş sırasını ve üç tasarım kararını netleştir. (1) Sekme şe

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Kalan 11 iş için kuruluş sırasını ve üç tasarım kararını netleştir. (1) Sekme şeridi bugün yatay ve üstte; kullanıcı 'ayarlar sol tarafın en sağında olsun' diyor. Bu cümle mevcut yatay şeridin sağ ucu mu, yoksa sol dikey bir şeride geçiş mi? Hangi okuma kullanıcının 'ayrı panel olmasın, hem teker hem yazı' cümlesiyle ve 30 piksellik TitleBarHeight şeridiyle tutarlı? (2) Simge takımı: bugün Unicode glifi + emoji karışık, altyapı yok. Üç yol var — (a) tek bir Icons.axaml içinde StreamGeometry takımı (kendi çizdiğimiz, renk Foreground'dan, ölçü belirteçten), (b) gömülü bir simge fontu, (c) svg varlıkları. Hangisi bu depoda (Avalonia 12, uydurma renk/ölçü yasağı, 42 dil, testle pimleme alışkanlığı) doğru ve neden? Ses simgesinin emoji olması hangi somut kusuru üretiyor? (3) Oynatıcı: alt şerit göster/gizle zaten kurulu, sağ klik menüsü zaten üç noktayla aynı menü. Kullanıcının istediği 6 oynatıcı maddesi içinde hangisi gerçekten yeni kod, hangisi var olanın sadeleştirilmesi? Uyarı şeritleri bugün TabControl şablonunda sayfanın üstüne basılıyor ve oynatıcıyı aşağı itiyor; 'uyarı oynatıcının üstünde çıksın' için şablonu mu değiştirmeli yoksa oynatıcı sekmesine kendi katmanını mı koymalı? (4) Kaydedici: ffmpeg ile bugün yapılabilir 12 kalem var. Otomatik kip (OBS modeli: aday kümesini kısa kayıtlarla koşturup atlanan kare saymak) bu 12 kalemin öncesine mi sonrasına mı konmalı — otomatik kip hangi kalemler olmadan anlamsız kalır? (5) Bu 11 iş tek turda mı kurulmalı yoksa hangi doğal kesitlere bölünmeli; hangileri paralel alt ajana verilebilir (aynı dosyaya dokunmadan)?

## Elde olan olgular

# Gerçekler — arayüz kabuğu, simge takımı, oynatıcı, kaydedici otomatik kip

## Kullanıcının cümlesi (harfiyen)

> şu üstte varsayılan program soruyor varsayılan uygulamalara tıklıyınca video formatlarını otomatik filtrelesin kullanıcı minimum efor yapmalı
> ayarlar sol tarafın en sağında olsun hem teker hem yazı ayrı panel olmasın her bir sekmenin iconu olsun ayarlarınki de aynı simge olsun
> çok daha göze hoş gelen simgeler istiyorum tüm programda ses simgemiz bile kötü
> bandicam gibi kullanışlı olmalıyız çok kötü durumdayız recorderde obs vb programları araştır onlardaki tüm özellikleri istiyorum yine recorder içinde otomatik modumuz olacak en iyi ayarları program seçicek
> oynatıcı sekmesinde oynatıcı başlığı kalkıcak altyazı ses parçası butonları kalkacak üç nokta da aynı şekilde ekrana sağ klik menüyü açacak bütün ayarlar orda olacak ses ve hız progressbar gibi ayarlanacak rakamı görebilecez -10 +10 simge olacak play ortalarında olacak ve daha büyük daha geniş olacak tüm o alt panel gizlenecek sadece fare aşağı gelince gözükecek oynatıcı tüm alanı kapsayacak uyarı çıkacaksa oynatıcının üstünde çıksın

İlk madde (varsayılan uygulamalar) bitti: `DefaultApp.SettingsPageForThisApp()` artık
`ms-settings:defaultapps?registeredAppUser=VidShrink` üretiyor; Windows o sayfada yalnız
kurucunun kaydettiği 24 uzantıyı listeliyor ve hepsi video. Aşağıdaki sorular kalan 11 iş için.

## Arayüzün bugünkü hali (kaynaktan okundu)

- Sekme şeridi tek bir `TabControl`: `src/VidShrink.App/MainWindow.axaml:108`. Şablon
  `Themes/Controls.axaml:797-811` — `StackPanel Orientation="Horizontal"`, `HorizontalAlignment="Left"`,
  `Height="{StaticResource TitleBarHeight}"` (30). **Sol dikey şerit (rail) yok, yatay üst şerit var.**
- Sekme sırası (XAML bildirim sırası): 0 Oynatıcı (`:195`), 1 Küçült (`:200`, açılışta seçili),
  2 Dönüştür (`:807`), 3 Ayarlar (`:1054`, **`IsVisible="False"`**), 4 Hakkında (`:1194`),
  5 Kaydedici (`:1219`), 6 Gelişmiş (`:1225`).
- Ayarlar sekmesi şeritte görünmüyor; girişi başlık çubuğundaki düğme
  (`MainWindow.axaml:71-74` `BtnSettings` → `MainWindow.axaml.cs:727` `Tabs.SelectedIndex = SettingsTabIndex`)
  ve dişli (`MainWindow.axaml:64-69`, `Content="⚙"` → `OnOpenLanguageSettings`).
- Sekme başlıkları düz metin: `Header="{loc:Text main.tab.*}"`, şablon yalnız
  `ContentPresenter Content="{TemplateBinding Header}"` (`Controls.axaml:861-867`).
  Ama kod `StackPanel` başlığı olasılığını zaten tanıyor (`MainWindow.axaml.cs:2620`).
- **Simge altyapısı yok**: `Icons.axaml` yok, simge fontu yok, gömülü svg/png yok
  (Assets yalnız uygulama logosu). İki mekanizma var:
  (a) Unicode glifi `Button.Content` olarak — `⚙`, `□`, `×`, `▾`, `⋮`, `«`, `»`, `▶`/`❚❚`,
      ses için **emoji** `🔉` / `🔊` (`Playback/PlayerView.axaml:143,149`) — çizimi sistem emoji fontuna bırakılmış;
  (b) Avalonia `Path` geometrisi, toplam 6 yer (`MainWindow.axaml:81-84` kahve fincanı,
      `:226-236` `DropIcon`, `Themes/Playback.axaml:161-162` iki `StreamGeometry`,
      `Controls.axaml:730` CheckMark, `:977` Arrow). `PathIcon` hiç kullanılmıyor.
- Ölçü belirteçleri `Themes/Theme.axaml`: `TitleBarHeight` 30 (`:261`), `TargetMinSize` 24 (`:251`),
  `WindowButtonWidth/Height` 42/30 (`:254-255`), `CheckGlyphSize` 20 (`:257`), `DropIconSize` 48 (`:268`),
  `SliderThumbSize` 20 (`:259`), `WorkspaceMargin` 24,12,24,12 (`:227`).
  Oynatıcı ölçüleri `Themes/Playback.axaml`: `PlaybackBarButtonSize` 40 (`:130`),
  `PlaybackSeekBarHeight` 40 (`:128`), `PlaybackSeekTrackHeight` 10 (`:129`),
  `PlaybackStripHideDelay` 360 ms, `PlaybackStripShowDelay` 0, `PlaybackHoverZoneShare` 0.25 (`:120-122`).
- Renk belirteçleri `Theme.axaml`: `NeonBlue`, `NeonPink`, `NeonPurple`, `NeonSuccess`, `NeonEmber`,
  `TextBody`, `TextDisabled`, `OnNeon`, `NeonBlueBorder(Strong)`, `HeaderRestBorder`.

## Oynatıcının bugünkü hali

- Görünüm `src/VidShrink.App/Playback/PlayerView.axaml` (166 satır) + 7 kısmi sınıf dosyası.
- Kök `Grid RowDefinitions="Auto,*"` (`:15`): üst satır **başlık** (`:18-21`, `main.player.title`, `H2`),
  **altyazı/ses parçası** düğmeleri (`Playback/TrackButtons.axaml:7-16`), **üç nokta** (`:23-30`, `Content="⋮"`);
  alt satır sahne (`Stage` → `Surface` → `Image Frame`).
- Alt şerit `PlayerView.axaml:103-162`: `Border StripBar`, `Opacity="0"`, `IsHitTestVisible="False"`,
  içinde zaman çubuğu + 10 sütunlu düğme ızgarası. Göster/gizle **zaten kurulu**:
  `PlayerView.Serit.cs:82-130` — panonun alt %25'inde fare, `RevealSerit` opaklık + hit-test,
  `HoldSerit()` fare şeritteyken veya duraklatılmışken gizlemiyor.
- Ses: `BtnSeritVolumeDown` 🔉 / `TxtSeritVolume` / `BtnSeritVolumeUp` 🔊 (`:143-150`).
  Hız: `BtnSeritSlower` « / `TxtSeritSpeed` / `BtnSeritFaster` » (`:152-159`). **İkisi de düğme çifti, slider değil.**
- `-10`/`+10`: `BtnSeritBack` `Content="-10"`, `BtnSeritForward` `Content="+10"` (`:132-135`) — **düz metin**.
- Sağ klik menüsü **zaten var ve üç noktayla aynı menü**: `Keymap.cs:104` sağ tuş → `OpenMenu`,
  `PlayerView.axaml.cs:402-414` `MenuAtPointer ? flyout.ShowAt(Surface, true) : flyout.ShowAt(BtnPlayerMenu)`.
  İçerik `Keymap.cs:152-162` + `AddTrackMenus`/`AppendWindowMenu`/`AppendToolsMenu`/`AppendAdvancedMenu`.
  Yani altyazı ve ses parçası **menüde de var**; üstteki düğmeler ikinci bir yol.
- Sekme içeriğinin kenar boşluğu `PlayerView`de değil, `TabControl` şablonunda:
  `Controls.axaml:813-814` `SelectedContentHost Margin="{StaticResource WorkspaceMargin}"` (24,12,24,12).
- Uyarı şeritleri (`AppliedNotice` `MainWindow.axaml:111`, `UpdateNotice` `:142`,
  `DefaultAppSuggestionBar` kodla eklenen) `TabControl.Tag` içindeki `StackPanel`e giriyor ve
  `Controls.axaml:812`'deki `ContentPresenter Grid.Row="1"` ile **sekme şeridinin altına, sayfa içeriğinin üstüne**
  basılıyor — yani bugün oynatıcıyı aşağı itiyorlar, üstüne binmiyorlar.

## Kaydedici araştırmasının sonucu

Belge: `docs/arastirma/ekran-kaydedici-ozellik-karsilastirmasi.md` (42 satırlık karşılaştırma tablosu).

OBS Auto-Configuration Wizard'ın kayıt kolu:
- Kullanıcıya üç şey soruyor: amaç (yayın/kayıt), temel çözünürlük, FPS tercihi.
- **Ölçüyor**: aday çözünürlük/FPS basamaklarını (2160p…240p × 30/60) gerçekten kodluyor;
  kabul ölçütü 5 saniyede **en çok 10 atlanan kare**; en çok 3 kazanan aday.
  Ayrıca donanım envanteri (NVENC/QSV/VT/AMF).
- **Karar veriyor**: temel çözünürlük 1920x1200'de tavan, gerekirse 1920x1080;
  en küçük aday alanı ≥518.400 piksel ise 60 fps; CPU sınıfına bağlı piksel/saniye tavanı
  (superb 2.304.060 … toaster 518.560); kodlayıcı yeğleme sırası NVENC > QSV > Apple > AMD,
  donanım yoksa x264.
- Bant genişliği testi yalnız yayın kolunda; bizde karşılığı yok (yayın yapmıyoruz).

Bizde eksik olup **ffmpeg CLI ile bugün yapılabilir** olanlar (12 kalem): kayıt kabı seçimi
(mkv yarım dosya sorununu kökten kapatıyor), çıktı ölçekleme `-vf scale`, keyframe/profil/tune,
hedef bit hızı/CBR kolu (`CodecModel.BitrateRateControlArgs` var ama kayıt kolunda kullanılmıyor),
ayrı ses izleri (`amix` yerine iki `-map`), ses filtreleri (volume/agate/afftdn), otomatik dosya
bölme, süre sınırı, ekran görüntüsü (`FrameGrabber` var), Windows çoklu monitör, piksel biçimi/renk
uzayı, ve otomatik kip.

**Ciddi iş isteyenler** (9 kalem): webcam bindirmesi, global kısayol tuşu (üç platformda üç yerel
çağrı), tekrar arabelleği, tıklama efekti, gerçek zamanlı çizim, bölgeyi fareyle seçtirme,
zamanlanmış kayıt, chroma key, Apple VideoToolbox kolu (engel teknik değil, ölçüm).

**Makul olmayanlar** (5 kalem): oyun yakalama (grafik API kancası), OBS'in sahne/kaynak mimarisi,
tarayıcı kaynağı, AI tarafı, ağ bant genişliği testi.

## Depo kuralları (kararı bağlayan)

- Renk yalnız `Themes/Palette/<Ad>/Theme.axaml`, ölçü yalnız `Themes/Theme.axaml` belirteçlerinden.
  Uydurma renk/ölçü yasak.
- Rapora giren her sayı `tools/VidShrink.Bench`ten çıkar.
- `main`e yalnız T0 birleştirir; alt ajanlar kendi dalında.
- Beş dosya ve üstü iş → önce `docs/plan.md`.
- Kod içine yorum yazılmaz.
