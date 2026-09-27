# 028 — Düzenleyici Çizelge Belirteçleri

D3 dalgasının (çizelge denetimi) fırça, belirteç, yerleşim ve kabul kararları. Kaynak:
`docs/plan-duzenleyici.md` D3 bölümü, `docs/arastirma/video-duzenleme-cizelge-arayuzu-2026-09-15.md`,
`Themes/Theme.axaml`, `Themes/Playback.axaml`, `Themes/Palette/*/Theme.axaml` (dizin sayımı **36**
palet; istemdeki 37 sayısı dizinde yok), `src/VidShrink.Core/Editing`, `EdlPreviewDriver`.

Kural: **yeni palet rengi yok, yeni ham sayı yok.** Her fırça mevcut bir fırçaya, her ölçü mevcut
bir belirtece bağlanır; `Editor.axaml` Playback.axaml'ın "X <- Y (değer), gerekçe" yorum kalıbını izler.

## 1. Fırçalar

Çizelge üç vurguyu üç role ayırır: **mavi = klip**, **pembe = oynatma başı**, **mor = kesim**.
Oynatıcı şeridi aynı dili konuşuyor (`PlaybackTrack` mavi, `PlaybackEncodeCursor` pembe), o yüzden
kullanıcı sekme değiştirince yeniden öğrenmez.

| Öğe | Fırça | Gerekçe |
|---|---|---|
| Klip gövdesi | `NeonBlueFill` dolgu, `NeonBlueBorder` kenar | `PlaybackTrack` ile birebir; klip, oynatıcının şeridinin parçalanmış hali. |
| Fare üstü klip | `NeonBlueHover` | Denetimlerin `:pointerover` durumuyla aynı. |
| Seçili klip | `NeonBlueActive` dolgu, `NeonBlueBorderStrong` kenar, `GlowBlue` gölge | Seçim = etkin durum; kenar kalınlığı da artar (bkz. belirteçler), renk tek başına taşımaz. |
| Oynatma başı | `NeonPink` çizgi, `GlowPink` gölge; cetveldeki tutamaç `NeonPink` | `PlaybackEncodeCursor` ile aynı: ince pembe dikey çizgi. Mavi klipten ve mavi tutamaçtan ayrışır. |
| İz zemini | Video izi `Surface`, ses izi `PanelSurface`; izler arası ve iz başlığı çizgisi `HeaderRestBorder` | İki iz dolgusuz da ayrışsın; kliplerin mavi dolgusu koyu zemin üstünde kontrastını korur. |
| Cetvel zemini | `AppBg` | Cetvel izlerin altında değil pencerenin parçası; şeritten bir ton geride durur. |
| Cetvel metni | `TextBody`, `FontMono`, `FontSizeSm` | `PlaybackTimeText` ile aynı; 7:1 metin eşiği ölçülü. |
| Cetvel çizgileri | Büyük çizgi `NeonBlueBorderStrong`, küçük çizgi `TextDisabled` | Büyük çizgi etiketle aynı hizada, küçük çizgi arka planda kalır; simge eşiği 3:1 büyük çizgide aranır, küçük çizgi dekoratif. |
| Kesim / yakalama çizgisi | `NeonPurple` çizgi, `GlowPurple` gölge | Üçüncü vurgu şeritte boş; yakalama anında mavi klip ve pembe baştan ayrı görünmesi gerekir. |
| Aralık seçimi (I/O) | `NeonPurpleBorder` kenar, dolgu yok | Silinecek aralık kesim rengiyle çerçevelenir; dolgu klip mavisini ezmez. |
| Hızlı / geri klip işareti | `PlaybackBadge` kalıbı: `PlaybackScrim` zemin, `NeonBlueBorderStrong` çerçeve, `TextBody` mono metin (`2×`, `◀ 1×`) | Dolgu üstüne yazı yasak (`PaletKarsitligiTests.YaziTasimayanDolgular`); perde üstü yazı kalıbı zaten 7:1 ölçülü. Klip `EditorClipMinWidth`'e inince yalnız simge kalır (3:1). |
| Sürüklenen klip gölgesi (hayalet) | `NeonBlueFill` yarı saydam değil: `NeonBlueBorder` kenar, dolgu yok | Saydamlık uydurma renk üretir; yalnız çerçeve çizmek palete dokunmaz. |
| Geçersiz bırakma yeri | `TextDisabled` kenar | Uyarı hue'su yok (018): geçersizlik simge ve soluklukla anlatılır. |

`Danger`, `Warning`, `Success` çizelgede kullanılmaz; silme geri alınabilir, kırmızı çağrısı yok.

## 2. Belirteçler

Yeni dosya `src/VidShrink.App/Themes/Editor.axaml`; `App.axaml` merge listesine `Playback.axaml`'ın
yanına girer. Her satır "belirteç <- kaynak (değer), gerekçe".

**İz ve cetvel**

- `EditorVideoTrackHeight <- DropIconSize (48)`: iki `TargetMinSize` üst üste; klip içinde rozet + tutamak sığar.
- `EditorAudioTrackHeight <- TargetMinSize (24)`: D3'te ses izi aynadır, dalga formu yok; dokunma ölçüsü yeter.
- `EditorTrackGap <- SpaceXs (4)`: izler arası tek çizgi payı.
- `EditorTrackHeaderWidth <- FieldWidthSm (120)`: iz adı ve sessize alma simgesi için etiket sütunu.
- `EditorRulerHeight <- TargetMinSize (24)`: cetvel tıklanabilir (oynatma başını taşır), dokunma ölçüsünde olmalı.
- `EditorRulerMajorTick <- SpaceSm (8)`, `EditorRulerMinorTick <- SpaceXs (4)`: iki kademe, cetvelin üçte biri ve altıda biri.
- `EditorRulerLabelMinSpacing <- NumberBoxWidth (96)`: `00:00:00.000` mono metnin genişliği; etiket aralığı bunun altına inince 1-2-5-10 merdiveninde bir üst basamağa çıkılır.

**Klip**

- `EditorClipRadius <- RadiusChip (4)`: klip şerit parçasıdır, `PlaybackTrack` ile aynı köşe.
- `EditorClipBorder <- BorderThin (1)`, seçili `EditorClipSelectedBorder <- FocusRingThickness (2)`: seçim kalınlıkla da okunur.
- `EditorClipPadding <- SpaceXs (4)`: rozet ve ad kenara yapışmaz.
- `EditorClipMinWidth <- TargetMinSize (24)`: iki kenar tutamağı (2×8) + orta tutuş (8). Bunun altındaki klip çizilir ama tutamaksızdır; yakınlaştırınca açılır.
- `EditorEdgeGripWidth <- SpaceSm (8)`: FramePFX 8 px ile aynı; D3'te tutamak yalnız imleç biçimi değiştirir, trim komutu yok.

**Oynatma başı ve etkileşim**

- `EditorPlayheadWidth <- PlaybackCursorWidth (2)`: oynatıcının pembe imleciyle aynı kalınlık.
- `EditorPlayheadHandle <- SliderThumbSize (20)`: cetveldeki tutamaç, Slider tutamacıyla aynı hedef.
- `EditorSnapThresholdPx <- SpaceSm (8)`: Pitivi 5 ile FramePFX 8 arasında; **piksel tanımlanır, tick saklanır**: eşik tick = 8 / (px/tick), her zoom değişiminde yeniden hesaplanır.
- `EditorDragThresholdPx <- SpaceXs (4)`: bunun altındaki hareket tıklamadır, `Move` başlamaz.
- `EditorAutoScrollMargin <- PlaybackThumbnailWidth (128)`: sürüklerken kenara bu kadar yaklaşınca görünüm kayar.

**Yakınlaştırma**

- `EditorZoomMax <- TargetMinSize (24)`: bir kaynak karesi 24 px olunca durur (tek kare tıklanabilir).
- `EditorZoomMin`: sayı değil kural — çizelgenin tamamı görünüm genişliğine sığar (`Duration` / görünüm px). Pencere büyüyünce değişir, belirteç değil hesap.
- `EditorZoomStep <- PlaybackHoverZoom (2)`: Ctrl+tekerlek her adımda ×2 / ÷2, merkez imleç altındaki tick.
- Geçiş süreleri: seçim ve fare üstü `MotionFast`, kaydırma anında (`MotionInstant`); çizelgede yumuşak kaydırma yok, kare doğruluğu önce.

**Zaman**

- Kaydırma/yakalama sonunda oynatma başı kare sınırına yuvarlanır (kaynak fps'ten tick); sürükleme sırasında `SeekPrecision.Keyframe`, bırakınca `Exact` — `EdlPreviewDriver.SeekAsync` bugün yalnız Exact atıyor, sürükleme için Keyframe kolu eklenir.

## 3. Yerleşim ve Kapsam

**Ayrı sekme `TabEditor`**, `TabPlayer`'dan hemen sonra (`MainWindow.axaml` `Tabs` sırasında ikinci);
anahtar `main.tab.editor`, 42 dile girer. Kaynak D0'ın `CurrentMedia` odağından gelir; sekme açılınca
odaktaki dosya yoksa oynatıcıdaki boş durum metni gösterilir.

**Üstte önizleme: mevcut `PlayerView` sınıfının ikinci örneği, kendi motoru.** `PlayerView.EngineFactory`
ile yeni `IPlaybackEngine` kurulur, `EdlPreviewDriver` ona bağlanır; oynatıcı sekmesinin motoru
**paylaşılmaz**. Gerekçe: `edl://` yüklemesi motorun açık dosyasını değiştirir; paylaşılsa oynatıcı
sekmesine dönen kullanıcı konumunu, döngüsünü ve geçmişini kaybeder. İkinci motor kalıbı zaten var
(`EngineComparisonFrameSource`, `PreviewAudio`). Sekme arka plana düşünce önizleme motoru `Pause`,
kapatılmaz; bellek maliyeti 4. bölümde risk.

**Altta `EditorTimeline` denetimi**, arada `GridSplitter`. Denetim FramePFX melez yolunu izler: klipler
`Control` (odak, otomasyon, tıklama), cetvel ve iz zemini `DrawingContext` ile çizilir; yalnız görünür
aralık çizilir, görünüm dışı klip için `Control` yaratılmaz. Oynatma başı ayrı üst katmandır, klip
düzeni değişince yeniden çizilmez. Araç çubuğu tek satır: böl, sil, hız kutusu (`NumberBoxWidth`),
geri/ileri al, yakınlaştır, "Dışa aktar" düğmesi D4'e kadar devre dışı.

**D3 kapsamı: tek kaynak, tek video izi + tek ses izi.** Ses izi görüntü izinin aynasıdır: aynı klip
sınırlarını çizer, ayrı düzenlenmez, dalga formu yok. Model tek liste taşıdığı için ikinci bağımsız iz
model değişikliği ister; D3 modele dokunmaz.

D3'e giren işlemler (hepsi mevcut model API'si): tıklayarak seçim; oynatma başında böl (`Split`);
seçiliyi sil (`Delete`); I/O işaretleriyle aralık sil (`DeleteRange`); sürükleyerek sıra değiştir
(`Move`, bırakış klip sınırına yakalanır); hız ve geri (`SetSpeed`, sağ tık menüsü ve araç çubuğu
kutusu, eksi değer geri); geri al / ileri al (`Undo`/`Redo`); yakınlaştır ve kaydır; yakalama
(klip sınırları, oynatma başı, I/O işaretleri, 0 ve `Duration`).

**D3 dışında:** kenar tutamağıyla trim (modelde komut yok; `Trim(index, start, end)` komutuyla D3b ya da
D4 öncesi ayrı iş), küçük resim ve dalga formu şeridi, çoklu iz, geçişler, kırpma kesmesi (ripple
dışı), kısayol haritası (D5; D3'te yalnız Delete, Ctrl+Z/Y ve Space bağlanır, S/X/Z/A/B/M/C çakışması
D5'te sekmeye bağlı kapsamla çözülür).

## 4. Riskler

1. **Her düzenleme edl yeniden yüklemesi.** `EdlPreview.Uri` değişince motor dosyayı yeniden açar; uzun
   kaynakta ilk kare gecikir. Ölçülmeden kabul edilmez (kabul 5).
2. **Geri parçada seek titremesi.** 50 ms'de bir Exact seek; çizelge oynatma başı bunu izlerken zıplar.
   Oynatma başı sürüş sırasında motor değil sürücü saati (`TimelinePosition`) ile çizilir.
3. **Uzun kaynakta çizim maliyeti.** Yüzlerce kesimde her klip için `Control` yaratmak düzeni boğar;
   görünür aralık kuralı şart (kabul 1-2).
4. **Ekran KontrastTests'e girmezse ölçüsüz kalır.** Yeni sekme `Ekranlar()` listesine eklenmeden mor
   çizgi ve rozetler 36 palette ölçülmemiş olur (kabul 7).
5. **İkinci motor belleği.** İki libmpv örneği; 4K kaynakta iki çözücü. Önizleme motoru sekme arka
   plandayken `Pause`, uygulama düşük bellekte ikinci motoru kapatıp yeniden açar — D3'te ölçüm alınır,
   karar D4'te.
6. **Dil bağımlı ölçü.** Başsız ölçüm İngilizce pencere görüyor; cetvel etiket genişliği mono ve
   sayısal olduğundan dilden bağımsız, ama araç çubuğu düğme metinleri dil değiştirir. Ölçüm Türkçe ve
   İngilizce iki kez alınır.
7. **İki yeşil dal birleşince derlenmiyor.** `PlayerView.OpenAsync`/`EngineFactory` internal imzaları
   D3 dalıyla oynatıcı dalları arasında paylaşılır; birleşmeden sonra main koşumu beklenir.
8. **Yakalama eşiği zoomla sabit kalmazsa** kullanıcı yakınlaştırınca yakalama "yapışkan" ya da "kaçak"
   olur; tick değeri her zoom değişiminde yeniden hesaplanmalı (kabul 3).

## 5. Kabul Şartları

1. **Çizim maliyeti kaynağa bağlı değil:** 1 dk ve 60 dk kaynak, aynı görünüm genişliği ve aynı görünür
   klip sayısında (10) çizim süresi ±%20 içinde; ölçüm `tools/VidShrink.Bench`, sayı `docs/olcumler`e.
2. **Görünüm dışı klip için `Control` yok:** 200 kesimli çizelgede görünüm 10 klip gösterirken görsel
   ağaçtaki klip denetimi sayısı ≤ 12 (görünür + iki kenar payı), test pimler.
3. **Yakalama eşiği zoomdan bağımsız:** üç zoom kademesinde (min, orta, max) klip sınırına 8 px ±1 px
   yaklaşınca yakalanır, 10 px'te yakalanmaz; tick eşiği her kademede farklıdır ve test bunu doğrular.
4. **Bırakıştan sonra oynatma başı motorla aynı karede:** cetvelde sürükleyip bırakınca
   `EdlPreviewDriver.TimelinePosition` ile çizilen baş arasındaki fark ≤ 1 kaynak karesi (`DuzenleyiciEdlCanliTests` düzeni, gerçek libmpv).
5. **Düzenleme sonrası ilk kare ≤ 1 sn:** böl, sil, taşı, hız işlemlerinin her birinden sonra yeni
   `edl://` yüklenip ilk kare çizilene kadar geçen süre 10 dk'lık 1080p kaynakta ≤ 1 sn, beş tekrar medyanı.
6. **Sabit yok:** `Editor.axaml` ve `Editing/*.cs` (App) içinde ham renk, ham piksel ve ham süre yok;
   kaynak okuyan test her `DynamicResource`/`StaticResource` anahtarının `Theme.axaml`,
   `Playback.axaml` ya da `Editor.axaml` içinde tanımlı olduğunu ve `Editor.axaml`'daki her belirtecin
   yorumunda `<-` türetim satırı bulunduğunu pimler.
7. **Kontrast ölçülü:** düzenleyici sekmesi `KontrastTests.Ekranlar()`'a girer; 36 palet × altı durum
   (`:pointerover :pressed :focus-visible :selected :checked :open`) yeşil; cetvel metni ve rozet metni
   7:1, büyük cetvel çizgisi, oynatma başı ve kesim çizgisi 3:1.
8. **Erişilebilirlik ve geri alma:** `PlayheadAutomationPeer` Slider rolü, `Minimum`/`Maximum`/`Value`
   ve biçimli zaman metni döndürür; on rastgele işlemden (böl/sil/taşı/hız karışık) sonra on Ctrl+Z ile
   `Clips` başlangıç listesine eşit, on Ctrl+Y ile işlem sonrası listeye eşit.
