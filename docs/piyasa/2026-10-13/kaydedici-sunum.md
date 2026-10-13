# Kaydedici Sekmesi — Sunum Cilası, Kayıt Sonrası Ve Paylaşım

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b üzerinde. Bugünkü ağaç bir commit ileride (1.2.16 sürüm numarası):
`git diff --stat 9de48d8b HEAD` yalnız `CHANGELOG.md` ve `Directory.Build.props` dosyalarını gösteriyor, `src/` altında
fark yok. Yani aşağıdaki satır numaraları iki ağaçta da aynı.

Kapsam: kaydedicinin yakalama dışındaki yüzü. Kamera katmanı, imleç ve tuş efektleri, yakınlaştırma,
çizim, çerçeve, gizlilik, kayıt bitince olanlar ve paylaşım. Yakalama kaynağı, ses, kodlayıcı,
kısayol ve zamanlama komşu kolun işi; oraya düşenler en sonda tek satırla duruyor.

Eksik tablosu ayrı dosyada: `kaydedici-sunum-eksikler.md`. Betikle sayıldı: 39 satır; öncelik 1 dokuz, öncelik 2
yirmi, öncelik 3 on. Karar sütunu: 20 `gerekmez`, 16 `sahip`, 3 `kapalı`. Önek `EKB`.

## Envanter

Aşağıdaki her satır koddan okundu. Yollar depo köküne göre.

### Kayıt Sırasındaki Efektler

Bizdeki efektlerin hepsi **canlı** çalışır: ekrana gerçek bir pencere çizilir ve kayda öyle girer.
Yani efekt videoya "yakılır", kayıt bittikten sonra kapatılamaz ya da değiştirilemez.

- **İmleci göster ya da gizle.** `ShowCursor = true` (`src/VidShrink.App/Recorder/RecorderSettings.cs:64`),
  kutu `ChkCursor` (`src/VidShrink.App/Recorder/RecorderView.axaml:612`).
- **Tıklama halkası.** `RecorderClickRing` penceresi (`src/VidShrink.App/Recorder/RecorderInputOverlay.cs:47-64`),
  360 ms görünür (`:103-113`), yalnız kadraj içindeki tıklamada (`src/VidShrink.App/Recorder/RecorderView.Girdi.cs:69-74`).
  Ayar `ShowClicks` (`RecorderSettings.cs:70`, `RecorderView.axaml:616`). Renk, boyut, stil ayarı yok;
  sol ve sağ tık ayrılmıyor.
- **Tıklama sesi.** 2000 Hz, 30 ms (`src/VidShrink.App/Recorder/RecorderInput.cs:108-112`), ayar `ClickSound`
  (`RecorderSettings.cs:72`, `RecorderView.axaml:620`).
- **Tuş gösterimi.** `RecorderKeyCaption` (`RecorderInputOverlay.cs:66-90`), yazı alt ortada 1,5 sn durur
  (`:115-126`). `KeyTracker.Feed` değiştirici olmayan **her** tuş basışını, tutulan Ctrl, Alt, Shift, Win ile
  birlikte döndürür (`RecorderInput.cs:87-98`). Ayar `ShowKeys` (`RecorderSettings.cs:74`, `RecorderView.axaml:624`).
  "Yalnız kısayollar" süzgeci yok: bu seçenek açıkken yazılan düz metin de (parola dahil) harf harf ekrana
  ve kayda düşer.
- **İmleç büyüteci.** İmlecin yanında duran mercek (`src/VidShrink.App/Recorder/RecorderMagnifier.cs:29-70`),
  ayar `ShowMagnifier` (`RecorderSettings.cs:76`, `RecorderView.axaml:628`). Kadrajın tamamını yaklaştırmaz;
  otomatik yakınlaştırma değildir.
- **Halka, tuş yazısı ve büyüteç yalnız Windows'ta.** Öbür sistemlerde boş dinleyici kullanılır
  (`RecorderView.Girdi.cs:14-22`).
- **Geri sayım.** 0, 3, 5, 10 sn (`RecorderSettings.cs:46`, `RecorderView.axaml:513`).
- **Canlı önizleme.** `LivePreview` (`RecorderSettings.cs:78`, `RecorderView.axaml:632`).
- **Kendi pencerelerimizi kayıttan çıkarma.** `src/VidShrink.App/Recorder/CaptureAffinity.cs:12-27`
  (Windows'un "bu pencereyi yakalamaya dahil etme" bayrağı; Windows 10 yapı 19041 ve üstü). Kullanıldığı yerler:
  çerçeve (`RecorderFrame.axaml.cs:124`), bölge düzenleyicisi (`RecorderRegionEditor.axaml.cs:347`),
  mini şerit (`RecorderMini.axaml.cs:71-72`).

### Kamera Katmanı

- **Yalnız Windows.** Panel öbür sistemlerde gizlenir (`src/VidShrink.App/Recorder/RecorderView.Kamera.cs:53`);
  hata metni de bunu söyler (`src/VidShrink.Core/RecorderArguments.cs:894-895`). Kamera DirectShow
  (Windows'un kamera ve ses cihazı arayüzü) ile okunur.
- **Seçenekler.** Cihaz, dört hazır genişlik 160, 240, 320, 480 (`RecorderArguments.cs:889`), dört köşe
  (`:283-289`, konum hesabı `:908-922`), arka plan: olduğu gibi, sabit arka plan, yeşil perde (`:291-296`).
  Arayüz `RecorderView.axaml:801-832`, varsayılanlar `RecorderSettings.cs:112-118`.
- **Arka plan silme iki yolla.** `backgroundkey` (kameranın ilk gördüğü sabit arka planı çıkarır, `:926`) ve
  `chromakey` (yeşil perdeyi çıkarır, `:928`). Zincir `WebcamGraph` (`:937-945`).
- **Yok:** daire ya da yuvarlak köşe, serbest konum, ayna, kenarlık, bulanıklaştırma, modelle kişi ayırma.

### Kayıt Bitince

- **Sonuç paneli** (`RecorderView.axaml:193-314`, doldurma `src/VidShrink.App/Recorder/RecorderView.axaml.cs:175-222`):
  başlıkta boyut ve parça sayısı (`:185-188`), dosya yolu, eksik kalan işler için uyarı satırları (`:195-219`).
  Video önizlemesi ya da küçük resim yok.
- **Düğmeler:** Küçült'e gönder (`RecorderView.axaml:210`), Klasörde göster (`:216`), Oynatıcıda aç (`:222`),
  Düzenle (`:228`, `RecorderView.Duzenle.cs:11-14`), MP4 olarak kaydet (yalnız MKV kayıtta, `:235`),
  Boşlukları kırp (`:242`), Paylaş (`:249`). Dosyayı panoya alan düğme yok; paneldeki tek "Kopyala" bağlantı
  içindir (`:283-288`).
- **Boşlukları kırp.** En az 2 sn donmuş görüntüyü bulur, 0,5 sn pay bırakıp keser, en çok 100 kesim, çıktı
  `-trimmed` ekiyle (`src/VidShrink.Core/IdleTrim.cs:13-21`, `freezedetect` süzgeci `:38`). Keser, hızlandırmaz.
  Sessizliğe göre kesme ayrıca düzenleyicide var (`src/VidShrink.Core/Editing/SilenceCut.cs:51, :90`);
  hız değiştirme de düzenleyicide (`src/VidShrink.Core/Editing/EditExport.cs:139`).
- **Bitiş eylemi** (kayıttan önce seçilir): hiçbiri, dosyayı panoya, küçült ve panoya, yükle ve bağlantıyı panoya
  (`src/VidShrink.App/Recorder/RecorderView.Bitince.cs:16-22`, akış `:132-155`, seçim `RecorderView.axaml:520`).
  "Küçült ve panoya" Küçült sekmesinde o an seçili hedefi kullanır (`src/VidShrink.App/MainWindow.TembelSekme.cs:37, :45-58`).
  "Yükle" ham kaydı gönderir, küçültülmüşü değil (`RecorderView.Bitince.cs:177-187`, `RecorderView.Paylas.cs:69-94`).
- **Kayıt bitince klasörü aç.** `OpenFolderWhenDone` (`RecorderSettings.cs:66`, `RecorderView.axaml:655`).
- **GIF.** Kap olarak kayıttan önce seçilir; kayıt MKV'ye yazılıp sonra çevrilir (`RecorderArguments.cs:468-472`).
  Biten bir MP4 kaydı GIF yapmak için yol Oynatıcı (`src/VidShrink.App/Playback/ClipExport.cs:55, :74`) ya da
  Dönüştür sekmesinden geçer.
- **Kayıt sırasında tek kare PNG.** `SnapshotPath` (`RecorderSettings.cs:442-450`).
- **Dosya adı sabit.** `kayit_` ve tarih damgası, çakışmada `_2`, `_3` (`RecorderSettings.cs:427-436`). Klasör
  varsayılanı `Videolar\VidShrink` (`:413-419`).

### Paylaşım

- **Akış.** Paylaş, iptal, ilerleme, yeniden dene, bağlantı ve Kopyala, QR kod
  (`src/VidShrink.App/Recorder/RecorderView.Paylas.cs:62-132`, `RecorderView.axaml:249-299`). Durum satırı
  bağlantının bitiş tarihini yazar (`src/VidShrink.App/Share/ShareSession.cs:51-61`).
- **Hedefler.** `paylasim-hedefleri.json`: `storage.to` (25 GB, 1–7 gün, varsayılan 1 gün, silinebilir) ve
  `uguu.se` (128 MB, sabit 3 saat). İkisi de hesap ve anahtar istemez.
- **Kaydedicide hedef ve süre seçilemez.** Tablonun varsayılanı geçerlidir (`RecorderView.Paylas.cs:19-21`,
  `ShareSession.cs:39-43`). Tavan gösterimi ve silme düğmesi yalnız Küçült sekmesinde
  (`src/VidShrink.App/MainWindow.axaml.cs:2589, :2613, :2776`). Kaydediciden yüklenen bağlantı kaydediciden silinemez;
  bu bir pazar eksiği değil, iki sekme arasındaki tutarsızlık.
- **Kendi hedefini eklemek bugün elle mümkün.** Tablo önce `%APPDATA%\VidShrink` altındaki kullanıcı kopyasından
  okunur (`src/VidShrink.Core/Share/ShareTargets.cs:188-229`). Sağlayıcı uç nokta adına göre seçilir: `init` ve
  `confirm` varsa üç adımlı imzalı yükleme, `upload` varsa form yüklemesi
  (`src/VidShrink.Core/Share/IShareProvider.cs:66-78`). Arayüzü, anahtar alanı ve yanıt ayrıştırma tarifi yok.

### Kodda Bulunmayanlar

Her biri en az iki adla arandı (`src/` altında, `*.cs` ve `*.axaml`).

- Otomatik yakınlaştırma, imleç yumuşatma, hareket bulanıklığı: `AutoZoom`, `OtoYakin`, `zoompan`, `CursorSmooth`,
  `Yumusat`. Eşleşme yok.
- Çizim ve spot ışığı: `Annotat`, `Spotlight`, `drawbox`, `drawtext`. Yalnız arayüz çizim çağrıları çıktı.
- Çerçeve ve en-boy: `Wallpaper`, `RoundedCorner`, `9:16`, `AspectPreset`, `aspect`. Yalnız düz siyah dolgu var
  (`src/VidShrink.Core/VideoFilterChain.cs:349`, `src/VidShrink.Core/Editing/ClipFilters.cs:61`).
- Gizlilik: `boxblur`, `gblur`, `delogo`, `mosaic`, `Redact`, `Bulanik`, `DoNotDisturb`, `QuietHours`, `FocusAssist`,
  `BildirimGizle`, `DesktopIcons`, `MasaustuSimge`, `Progman`, `SHELLDLL_DefView`. Eşleşme yok.
- Kamera: `hflip`, `mirror`, `WebcamShape`, `KameraBicim`. Eşleşme yok.
- Kayıt sonrası: `DragDrop`, `DoDragDrop`, `DataObject` (sürükle-bırak); `RecordingHistory`, `KayitGecmis`, `Galeri`,
  `Recent`, `SonKayit`, `History` (geçmiş); `FileNameTemplate`, `AdSablon`, `NameTemplate` (ad şablonu);
  `Teleprompter`, `SpeakerNotes` (prompter: yalnız anlatıcının gördüğü akan metin). Kaydedici kodunda eşleşme yok.

### README İle Kod Arasındaki Fark

README kaydedici satırında "webcam" der (`README.md:254`) ama kameranın yalnız Windows'ta çalıştığını söylemez
(`RecorderArguments.cs:894-895`). Halka, tuş gösterimi ve büyüteç için de aynı şey geçerli. Otomatik altyazının
ve filigranın bilerek yapılmadığını README doğru yazıyor (`README.md:264-265`).

### Eski Belgelerde Eskiyen Satırlar

- `docs/envanter/ekran-kaydedici.md` (12 Eylül 2026): "yakalama izi kaynakta sıfır". Bugün `Recorder/` altında 53 dosya var.
- `docs/piyasa/tarama-2026-10-05.md` kaydedici bölümü (satır 153–195):
  - "Arayüzü kayıttan gizleme … mini çubukta görülmedi". Eskidi: `RecorderMini.axaml.cs:71-72`.
  - "Boşta kalan süreyi kırpma … hiçbir rakipte doğrulanmadı". Eskidi: Bandicam 27.0.6 sessizliği, Tella sessizliği,
    Screen Studio duraklamaları kısaltıyor [11][21][1]. Ayrım: bizimki donmuş görüntüye, onlarınki çoğunlukla sese bakıyor.
  - Satır 189, hüküm: "Kayıt sonrası zincir (kırp, küçült, bağlantı al) ve hedef boyutlu kayıt rakipte yok". Yalnız o
    taramanın dört rakibine (OBS, ShareX, Snipping Tool, NVIDIA App)
    karşı doğruydu. Loom, Cap, Zight, ScreenRec ve Canvid durdurunca bağlantıyı panoya koyuyor [37][5][39][41][19].
    macOS aracı ve Snipping Tool da kayıt bitince kırpma sunuyor [44][45].
  - Satır 168, kamera katmanı: "Var, yalnız Windows; yeşil perde". Doğru; ek olarak sabit arka plan silme de var (`RecorderArguments.cs:926`).
  - Satır 180, GIF kaydı: "Var (`RecorderArguments.cs:864`)". Satır kaydı: bugün `:867-880`.
  - Satır 170, çizim ve açıklama: "Kodda bulunamadı". Hâlâ yok (EKB-04).
  - Satır 187, macOS ve Linux: "kamera, kısayol, tıklama ve tuş gösterimi yok". Kamera, tıklama ve tuş gösterimi için
    hâlâ doğru; EKB-14 ve EKB-18 bu satırın açılımı.
- `docs/arastirma/kaydedici-saha-2026-09-15.md:138` "Tuş vuruşu gösterimi. Yalnız Captura ve ScreenToGif." Dar kalmış:
  CleanShot X, Screen Studio, FocuSee, Keyviz ve KeyCastr de gösteriyor [7][1][20][30][31].

### Önceki Taramanın Karar Gerektirmeyenler Tablosu

`docs/piyasa/tarama-2026-10-05.md` satır 227–250. Kaydediciye düşen dört kalemden biri benim alanımda:
mini çubuğu kayıttan gizleme. Yapılmış (`RecorderMini.axaml.cs:71-72`). Öbür üçü komşu kolun alanında;
o taramada "kodda bulunamadı" denen ses seviyesi göstergesi, bölüm işareti ve kısayol atama bugün kodda duruyor
(`RecorderView.SesSeviyesi.cs`, `RecorderView.Bolum.cs`, `RecorderView.Kisayol.cs`).

### Defter Ve Ölçümler

`.claude/acik.md` kapsam sözcükleriyle tarandı (yakınlaştır, imleç, kamera, GIF, galeri, şablon, çizim, bulanık,
bildirim, paylaş, pano, webcam, tuş göster, kaydedici). Bu tablodaki hiçbir eksik defterde yazılı değil; hiçbir satıra
`defterde` eklenmedi. Defterde alana değen iki kayıt var, ikisi de özellik eksiği değil: kararsız tıklama halkası testi
(`.claude/acik.md:21`) ve "iş bitti bildirimi"nin gerçek pencerede hiç denenmemiş olması (`.claude/acik.md:25`).

`docs/olcumler/` altında paylaşım ve yakalama ölçümleri var; sunum efektlerinin (kamera, halka, tuş yazısı,
büyüteç) kayıt akıcılığına etkisini ölçen dosya yok.

## Rakipler

Sürümler `gh api` ve resmi sayfalardan, 13 Ekim 2026'da okundu.

| No | Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|---|
| 01 | Screen Studio | 4.0 | doğrulanmadı (haber Ekim 2026) | ücretli, yalnız macOS | https://screen.studio/ |
| 02 | Cap | 0.6.0 | 2026-09-15 | açık kaynak ve ücretli bulut; macOS, Windows, Linux | https://github.com/CapSoftware/Cap |
| 03 | CleanShot X | 5.0.1 | 2026-09-18 | ücretli, macOS | https://cleanshot.com/ |
| 04 | Bandicam | 27.0.6.2686 | 2026-09-09 | ücretli, Windows (ayrı macOS sürümü var) | https://www.bandicam.com/ |
| 05 | Recordly | 1.4.0 | 2026-09-08 | açık kaynak (AGPL-3.0); Windows, macOS, Linux | https://github.com/webadderall/Recordly |
| 06 | OpenScreen | 1.5.0 (arşivlendi); topluluk kolu 2.0.0 | 2026-06-06; 2026-10-03 | açık kaynak (MIT) | https://github.com/siddharthvaddem/openscreen |
| 07 | ScreenArc | 1.4.1 | 2026-07-22 | açık kaynak (GPL-3.0); Windows, macOS, Linux | https://github.com/tamnguyenvan/screenarc |
| 08 | Screenize | sürüm yok; son itme 2026-05-22 | geliştirme duraklatılmış | açık kaynak (Apache-2.0), macOS | https://github.com/syi0808/screenize |
| 09 | Cursorful | doğrulanmadı | doğrulanmadı | ücretsiz Chrome eklentisi ve ücretli masaüstü (Mac, Windows) | https://cursorful.com/ |
| 10 | Canvid | doğrulanmadı | doğrulanmadı | ücretli; Windows, Mac | https://www.canvid.com/ |
| 11 | FocuSee | doğrulanmadı | doğrulanmadı | ücretli; Windows, macOS | https://focusee.imobie.com/ |
| 12 | Tella | web hizmeti | sürekli | ücretli; web, Mac, Windows | https://www.tella.com/ |
| 13 | Camtasia | 2026.2 | Ağustos 2026 | ücretli; Windows, macOS | https://www.techsmith.com/camtasia/ |
| 14 | Snagit | doğrulanmadı | doğrulanmadı | ücretli; Windows, macOS | https://www.techsmith.com/snagit/features/ |
| 15 | ScreenPal | doğrulanmadı | doğrulanmadı | ücretsiz ve ücretli; Windows, Mac, Chromebook, mobil | https://screenpal.com/screen-recorder |
| 16 | Screenity | 4.6.12 | 2026-09-25 | açık kaynak (GPL-3.0), Chrome eklentisi | https://github.com/alyssaxuu/screenity |
| 17 | ZoomIt | sayfada sürüm yok | sayfa 2026-09-10 | ücretsiz, Microsoft, Windows | https://learn.microsoft.com/en-us/sysinternals/downloads/zoomit |
| 18 | PowerToys (fare araçları) | 0.101.2362.0 | 2026-08-25 | açık kaynak (MIT), Microsoft, Windows | https://github.com/microsoft/PowerToys |
| 19 | Presentify | 8.1.4 (indirme dosya adından) | doğrulanmadı | ücretli, macOS | https://presentifyapp.com/ |
| 20 | Keyviz | 2.1.1 | 2026-04-01 | açık kaynak (GPL-3.0); Windows, macOS, Linux | https://github.com/mulaRahul/keyviz |
| 21 | KeyCastr | 0.11.1 | 2026-09-07 | açık kaynak (BSD-3), macOS | https://github.com/keycastr/keycastr |
| 22 | Carnac | 2.3.13 | 2020-06-20 (bayat) | açık kaynak, Windows | https://github.com/Code52/carnac |
| 23 | ShareX | 21.0.0 | 2026-07-03 | açık kaynak (GPL-3.0), Windows | https://github.com/ShareX/ShareX |
| 24 | ScreenToGif | 2.43.2 | 2026-07-28 | açık kaynak (MS-PL), Windows | https://github.com/NickeManarin/ScreenToGif |
| 25 | Loom | doğrulanmadı | doğrulanmadı | ücretli bulut; Mac, Windows, Chrome | https://www.loom.com/screen-recorder |
| 26 | Zight | doğrulanmadı | doğrulanmadı | ücretli bulut; Mac, Windows, Chrome, iOS | https://zight.com/screen-recorder/ |
| 27 | ScreenRec | doğrulanmadı | doğrulanmadı | ücretsiz bulut; Windows, Mac, Linux | https://screenrec.com/ |
| 28 | Descript | doğrulanmadı | doğrulanmadı | ücretli; Mac, Windows, tarayıcı | https://www.descript.com/screen-recording |
| 29 | Kap | 3.6.0 | 2022-10-27 (bayat) | açık kaynak (MIT), macOS | https://github.com/wulkano/Kap |
| 30 | macOS ekran kaydı aracı | işletim sistemiyle | sürekli | işletim sistemi aracı | https://support.apple.com/guide/mac-help/take-screenshots-or-screen-recordings-mh26782/mac |
| 31 | Windows Snipping Tool | 11.2505.21.0 (GIF dışa aktarma) | 2025-06-19 | işletim sistemi aracı | https://blogs.windows.com/windows-insider/2025/06/19/gif-export-in-snipping-tool-begins-rolling-out-to-windows-insiders/ |
| 32 | OBS Studio ve iki eklentisi | 32.2.2; obs-backgroundremoval 1.4.1; obs-zoom-to-mouse 1.0.1 | 2026-08-14; 2026-07-26; 2023-11-30 (bayat) | açık kaynak | https://github.com/obsproject/obs-studio |
| 33 | Captura | 8.0.0 | 2018-11-10 (bayat) | açık kaynak (MIT), Windows | https://github.com/MathewSachin/Captura |

Adı geçen ama taranmayanlar: Stealthly, My Screen Recorder Pro, Rapidemo, ScreenSnap Pro, Drift. Yalnız üçüncü
taraf sayfalarda görüldüler [52][53][54].

## Özellik Matrisi

"Bizde" sütunu: var, yarım, yok. Son sütun eksik tablosundaki satır.

| Özellik | Kimde Belgeli | Bizde | Tablo |
|---|---|---|---|
| Otomatik yakınlaştırma (kayıt sonrası) | Screen Studio [1][2], Cap [5], CleanShot X [8], Bandicam [11], Recordly [13], OpenScreen [14], ScreenArc [16], Screenize [17], Cursorful [18], Canvid [19], FocuSee [20], Tella [21], Camtasia [22] | yok | EKB-01 |
| Çerçeve: arka plan, dolgu, yuvarlak köşe, gölge | Screen Studio [1], Cap [5], CleanShot X [8], Recordly [13], OpenScreen [14], ScreenArc [16], Cursorful [18], FocuSee [20], Tella [21], Camtasia [22] | yok | EKB-02 |
| İmleç yumuşatma ve sonradan boyut | Screen Studio [1], Cap [5], CleanShot X [8], Camtasia [22], Recordly [13], OpenScreen [14], Cursorful [18] | yok | EKB-03 |
| Hareket bulanıklığı | CleanShot X [8], Recordly [13], Cursorful [18] | yok | EKB-31 |
| En-boy ön ayarı | Recordly [13], OpenScreen [14], ScreenArc [16], Cursorful [18] | yok | EKB-19 |
| Kayıt sırasında çizim | Bandicam [10], ZoomIt [27], Snagit [24], ScreenPal [25], Screenity [26], Zight [39], Loom [37], Presentify [29] | yok | EKB-04 |
| Sürekli imleç vurgusu | Bandicam [12], PowerToys [28], Snagit [24], Loom [37], Presentify [29], Screenity [26] | yarım | EKB-05 |
| Tıklama efekti | Bandicam [12], PowerToys [28], CleanShot X [7], Presentify [29], Screen Studio [1], Cap [5], FocuSee [20], Snagit [24], macOS aracı [44] | var, ayarsız | EKB-10 |
| Tuş gösterimi | CleanShot X [7], Screen Studio [1], FocuSee [20], Keyviz [30], KeyCastr [31] | var, ayarsız | EKB-11 |
| Spot ışığı | FocuSee [20], Screenity [26], Presentify [29], PowerToys [28] | yok | EKB-12 |
| Canlı yakınlaştırma | ZoomIt [27], ScreenPal [25], Presentify [29], obs-zoom-to-mouse [48] | yarım (mercek) | EKB-13 |
| Prompter | Cap [6], FocuSee [20] | yok | EKB-15 |
| Kamera katmanı | Screen Studio [1], Cap [5], CleanShot X [7], Bandicam [9], Recordly [13], OpenScreen [14], ScreenArc [16], Snagit [24], Loom [37] | var, yalnız Windows | EKB-18 |
| Kamera biçimi | CleanShot X [7], Snagit [24], Bandicam [11], Recordly [13], OpenScreen [14], ScreenPal [25] | yok | EKB-06 |
| Kamera serbest konum | CleanShot X [7], Bandicam [9], Recordly [13], OpenScreen [14], Snagit [24] | yarım (dört köşe) | EKB-16 |
| Kamera arka planı (modelle) | Bandicam [11], ScreenPal [25], Canvid [19], Camtasia [22], Loom [37], Screen Studio [1], FocuSee [20], Screenity [26], OBS eklentisi [46] | yarım (yeşil perde, sabit arka plan) | EKB-07 |
| Alan bulanıklaştırma, maske | Screen Studio [1], Cap [6], Bandicam [11], ScreenPal [25], FocuSee [20], OpenScreen [14], Screenity [26], ZoomIt [27], Loom [38] | yok | EKB-08 |
| Bildirim susturma | CleanShot X [7], Zight (yalnız Mac) [40] | yok | EKB-20 |
| Masaüstü simgelerini gizleme | Screen Studio [1], CleanShot X [7] | yok | EKB-21 |
| Kendi arayüzünü kayıttan çıkarma | bu turda rakiplerde aranmadı | var | — |
| Kayıt sonrası hızlı kırpma | macOS aracı [44], Snipping Tool [45], Bandicam [11], ScreenPal [25], Zight [39], Snagit [24], Loom [37] | yarım (ayrı sekme) | EKB-09 |
| Boşluk ve sessizlik kesme | Bandicam [11], Tella [21], Screen Studio [1] | var (donmuş görüntü kaydedicide, sessizlik düzenleyicide) | — |
| Boşluğu kesmek yerine hızlandırma | Screen Studio [1] | yarım (hız yalnız düzenleyicide, elle) | EKB-30 |
| Biten kaydı GIF yapma | Snipping Tool [45], Snagit [24], Screen Studio [1], Recordly [13], OpenScreen [14], ScreenArc [16], FocuSee [20], Screenity [26] | yarım (başka sekmeden) | EKB-23 |
| Panoya kopyalama | CleanShot X [7], Screen Studio [1], Snipping Tool [45], ShareX [35] | yarım (yalnız bitiş eylemi) | EKB-22 |
| Kayan sonuç kartı, sürükle-bırak | macOS aracı [44], CleanShot X [7], Cap [6] | yok | EKB-24 |
| Kayıt geçmişi | ShareX [33], Snagit [24], CleanShot X [7], Cap [6] | yok | EKB-25 |
| Dosya adı şablonu | ShareX [35], OBS [55] | yok (sabit ad) | EKB-26 |
| Durdurunca bağlantı panoda | Loom [37], Cap [5], Zight [39], ScreenRec [41], Canvid [19] | var (bitiş eylemi) | — |
| Hesapsız yükleme | ShareX (özel yükleyici) [34] | var (iki hazır hedef) | — |
| Kendi sunucuna ya da bulutuna yükleme | ShareX [34][35], Cap [5], Screenity [26] | yarım (elle JSON) | EKB-28, EKB-29 |
| Hedef boyuta küçültme (paylaşmadan önce) | rakiplerde doğrulanmadı | var (küçült ve panoya; yüklemeyle zincirlenmiyor) | — |
| Bitiş eylemlerini zincirleme (işle, sonra yükle) | ShareX [35] | yarım (dört eylemden yalnız biri seçilir) | EKB-33 |
| Kaydedicide hedef ve saklama süresi seçimi | ShareX [35], Cap [5] | yarım (yalnız Küçült sekmesinde) | EKB-27 |
| Bağlantı kısaltma | ShareX [35] | yok | EKB-36 |
| Bağlantıda parola, izleyici bilgisi | CleanShot X [7][8], Tella [21], ScreenRec [41] | yok | EKB-34, EKB-35 |
| Otomatik altyazı | Screen Studio [1], Cap [5], Bandicam [11], Camtasia [22] ve altı rakip daha | yok (K5 kapalı) | EKB-37 |
| Metinden kurgu, dolgu sözcük silme | Descript [42], Tella [21], Camtasia [22], Bandicam [11], Screen Studio [3] | yok (K5 kapalı) | EKB-38 |
| Ekran görüntüsü kipi ve güzelleştirici | Cap [5], ShareX [33], CleanShot X [7], Snagit [24] | yarım (kayıt sırasında tek kare PNG; K9 kapalı) | EKB-39 |
| Girdi efektleri macOS ve Linux'ta | KeyCastr [31], Keyviz [30], CleanShot X [7], Presentify [29], Screen Studio [1] | yok (yalnız Windows) | EKB-14 |
| Kamera aynası | Recordly [13], OpenScreen [14] | yok | EKB-17 |
| Kamera düzenleri (yan yana, tam ekran kamera) | Cap [5], Tella [21], Camtasia [22], Screen Studio [3] | yok | EKB-32 |

### Ana Soruların Kısa Yanıtı

**Efektler bizde var mı?** Kamera katmanı, tıklama halkası, tıklama sesi, tuş gösterimi ve büyüteç var; hepsi yalnız
Windows'ta ve ayarsız. Sürekli imleç vurgusu, çizim, spot ışığı yok.

**Otomatik yakınlaştırma pazarı.** Bu turda 13 üründe doğrulandı. 10'u Windows'ta çalışıyor (Cap, Bandicam, Recordly,
OpenScreen, ScreenArc, FocuSee, Cursorful, Canvid, Camtasia, Tella), 5'i açık kaynak (Cap, Recordly, OpenScreen,
ScreenArc, Screenize). Yöntem neredeyse hepsinde aynı: ham video ve yanında ayrı bir imleç ve tıklama kaydı tutulur,
yakınlaştırma kayıt bittikten sonra düzenleyicide üretilir ve elle düzeltilir. Kayıt sırasında canlı yakınlaştıranlar
ayrı bir küme: ZoomIt, ScreenPal, Presentify, obs-zoom-to-mouse. Ürünlerin ilk çıkış tarihlerinin hepsi doğrulanmadı;
tarihi kesin iki ekleme CleanShot X 5.0 (1 Eylül 2026) ve Bandicam 27.0.6 (9 Eylül 2026).

**Bu yöntemin bedeli.** Açık kaynak rakiplerin iş takipçilerinde ortak şikâyet imleç verisinin videoyla
eşleşmemesi: Recordly #34 ve #393, Cap #2144, ScreenArc #160, OpenScreen #604 [13][6][16][14].

**Kayıt bitince.** Rakipler üç şeyden birini yapıyor: köşede küçük resimli kart (macOS, CleanShot X), doğrudan
düzenleyici (Screen Studio, Cap Studio, Recordly), ya da bağlantıyı panoya koyma (Loom, Cap Instant, Zight, ScreenRec).
Bizde kaydedici sekmesinde yedi düğmeli bir panel ve kayıttan önce seçilen tek bir bitiş eylemi var. Düzenleyiciye
ve Küçült'e tek tıkla geçiş var; panelde önizleme ve kırpma yok.

**Paylaşım.** Hesap ve bulut istemeden yapılabilen kısım zaten bizde: anahtarsız iki servise yükleme, bağlantı,
QR kod, saklama süresi. Rakiplerde hesapsız yükleme yalnız ShareX'te görüldü: hazır hedef listesinde bizim de
anahtarsız kullandığımız Uguu var [35], ayrıca özel yükleyici (kullanıcının kendi sunucusuna
yükleme tarifi olan `.sxcu` dosyası) [34]. Loom, Cap Cloud, Zight, ScreenRec, CleanShot Cloud kendi bulutlarını işletiyor.
ShareX 19.0.1 otomatik yüklemeyi varsayılan olarak kapattı [33]. Tek örnek, ama yönü gösteriyor: yükleme kullanıcının
açıkça istediği bir adım oluyor.

**Gizlilik.** Alan bulanıklaştırma dokuz rakipte var. Bildirim susturma ve masaüstü simgelerini gizleme yalnız macOS
araçlarında belgeli (CleanShot X, Screen Studio, Zight); Windows'ta bunu kendiliğinden yapan araç doğrulanmadı.

## Kullanıcı Talepleri

GitHub açık istekleri, başparmak tepkisi sayısıyla, 13 Ekim 2026.

| İstek | Oy | URL |
|---|---|---|
| ShareX: Linux desteği | 68 | https://github.com/ShareX/ShareX/issues/6625 |
| Kap: kayıt hatası | 64 | https://github.com/wulkano/Kap/issues/1059 |
| Kap: son macOS'ta çalışmıyor | 55 | https://github.com/wulkano/Kap/issues/1063 |
| Screenity: Firefox eklentisi | 51 | https://github.com/alyssaxuu/screenity/issues/3 |
| ShareX: WebP, AVIF, JPEG XL | 41 | https://github.com/ShareX/ShareX/issues/6090 |
| Kap: bilgisayar sesi | 38 | https://github.com/wulkano/Kap/issues/145 |
| ShareX: yüksek DPI | 18 | https://github.com/ShareX/ShareX/issues/5312 |
| ShareX: oyundan yakalama | 13 | https://github.com/ShareX/ShareX/issues/848 |
| ShareX: birden çok yükleme hedefi profili | 10 | https://github.com/ShareX/ShareX/issues/3992 |
| Cap: Linux sürümü | 10 | https://github.com/CapSoftware/Cap/issues/609 |
| Recordly: Linux'ta imleç izleme çalışmıyor | 10 | https://github.com/webadderall/Recordly/issues/34 |
| Cap: yalnız kamera kaydı ve tam boy kamera | 4 | https://github.com/CapSoftware/Cap/issues/504 |
| Cap: HEVC dışa aktarma | 4 | https://github.com/CapSoftware/Cap/issues/192 |
| Cap: kayıt profilleri | 2 | https://github.com/CapSoftware/Cap/issues/856 |
| Cap: kamera arka plan bulanıklığı | 2 | https://github.com/CapSoftware/Cap/issues/1365 |
| ScreenArc: Windows'ta fare izleme çalışmıyor | 2 | https://github.com/tamnguyenvan/screenarc/issues/160 |
| OpenScreen: kayıt sırasında canlı açıklama araçları | 1 | https://github.com/siddharthvaddem/openscreen/issues/602 |
| Recordly: Windows'ta imleç kayması | 1 | https://github.com/webadderall/Recordly/issues/393 |
| Cap: video izlenince bildir | 0 (8 yorum) | https://github.com/CapSoftware/Cap/issues/1624 |
| Cap: uzun kayıtta imleç kayması | 0 (6 yorum) | https://github.com/CapSoftware/Cap/issues/2144 |
| OpenScreen: imleç vurgusu | 0 | https://github.com/siddharthvaddem/openscreen/issues/550 |
| Screenity: klavye kısayollarını ekranda göster | 0 | https://github.com/alyssaxuu/screenity/issues/248 |
| Screenity: prompter | 0 | https://github.com/alyssaxuu/screenity/issues/317 |
| OBS forumu: imleç ve tıklama vurgusu | oy sayısı yok | https://obsproject.com/forum/threads/option-to-highlight-mouse-cursor-and-mouse-clicks.81966/ |
| Screen Studio panosu: "Add Multiple Sensitive Data" (birden çok maske) | 3 (etiketsiz sayı) | https://screenstudio.featurebase.app/ |
| Screen Studio panosu: "Independent padding", "Cinematic zoom effect", "Custom Cursor" | 2'şer (etiketsiz sayı) | https://screenstudio.featurebase.app/ |

**Okuma.** En çok oy alanlar platform ve hata istekleri; sunum cilası özelliklerine oy az. Bu yüzden tablodaki
hiçbir satır önceliğini oydan almadı; öncelik 1'lerin hepsi "en az üç yaygın rakipte belgeli" ölçüsünden geldi.

Alana değen iki haneli iki kayıt var. Biri hata: Recordly #34, Linux'ta imleç izleme (10 oy). Öbürü ShareX #3992,
birden çok yükleme hedefi profili (10 oy); EKB-27 ve EKB-28 ile komşu ama aynı istek olduğu doğrulanmadı, o yüzden
onları öncelik 1'e taşımadı.

Bizim için asıl işaret imleç eşleşmesi şikâyetleri: otomatik yakınlaştırma yapılacaksa en riskli parça orası.

## Son İki Yılın Yenilikleri

- **18 Kasım 2024, Bandicam 8.0.0:** kayıt alanında bulanıklık efekti, elips, beyaz tahta [11].
- **2025, Windows Snipping Tool:** kayıt kırpma (11.2501.7.0) ve GIF dışa aktarma (11.2505.21.0, en çok 30 sn) [45].
- **11 Ağustos 2025, Bandicam 8.2.0:** yuvarlak köşeli kamera biçimleri [11].
- **12 Ağustos 2025, ShareX 18.0.0:** geçmiş SQLite'a (tek dosyalık veritabanı) taşındı, "Favorite" işareti, videolara küçük resim [33].
- **15 Ekim 2025, CleanShot X 4.8.4:** paylaşılan medyaya yorum ve ilk görüntüleme bildirimi [8].
- **20 Ocak 2026, ShareX 19.0.1:** otomatik yükleme varsayılan olarak kapalı; "Spotlight" aracı [33].
- **24 Nisan 2026, ShareX 20.0.2:** Avalonia ile yeniden yazılmış görsel düzenleyici, 18 açıklama aracı [33].
- **Nisan–Haziran 2026, Recordly 1.2.0–1.3.4:** yeni imleç türü, hareket sistemi yenilendi, tıklama efektleri,
  altyazı dışa aktarma [13].
- **6 Haziran 2026, OpenScreen 1.5.0;** ardından depo arşivlendi, topluluk kolu 3 Ekim 2026'da 2.0.0 çıkardı [14][15].
- **Haziran–Eylül 2026, Cap 0.5.1–0.6.0:** komut satırı aracı, ekran görüntüsü paylaşımı, Linux desteği, yerleşik
  prompter, gizlilik maskeleri, son kayıtları gösteren yakalama penceresi, yeni düzenleyici, Studio Sound [6].
- **Ağustos 2026, Camtasia 2026.2:** adım adım yakalamadan video, WebM [23].
- **1 Eylül 2026, CleanShot X 5.0:** Studio Mode ve Video Editor: imleci izleyen yakınlaştırma, imleç yumuşatma,
  hareket bulanıklığı, arka planlar, kayıttan sonra imleç boyutu, tuş vuruşu ve kamera ayarı [8].
- **9 Eylül 2026, Bandicam 27.0.6:** imleç hareketine dayalı Smart Zoom, yapay zekâyla kamera arka planı silme,
  bulanıklaştırma ve değiştirme, daire ve yuvarlak kamera biçimleri, ekranda üç bulanıklık alanı, yerleşik oynatıcı
  ve düzenleme, sessizlik ve dolgu sözcük silme, altyazı [11].
- **Ekim 2026, Screen Studio 4.0** (üçüncü taraf haber): metinden kurgu, cihazda çalışan kamera kesimi, bölünmüş ekran [3].

**Yön.** Screen Studio yalnız macOS'ta çalışıyor [1]; aynı işi Windows'ta yapan on ürün doğrulandı. Eylül 2026'da
macOS'ta CleanShot X ve Windows'ta Bandicam aynı ay içinde otomatik yakınlaştırmayı ekledi [8][11]. Açık kaynakta beş
ayrı proje aynı işi yapıyor; biri duraklatılmış, biri arşivlenip toplulukça sürdürülüyor [6][13][14][15][16][17].

On üç ürünün ilk çıkış tarihleri doğrulanmadı; bu yüzden "son iki yılda kaç ürün çıktı" sorusunun kesin sayısı yok.
Kesin olan: özellik 2026'da sürüm geçmişi yıllara yayılan iki ücretli araca girdi; artık yalnız yeni ürünlerin işi değil.

**Kapalı kararlara denk gelenler.**

- **K5 otomatik altyazı (EKB-37, EKB-38):** son iki yılda cihazda çalışan altyazı bu sınıfta standart oldu; Bandicam
  27, Cap, OpenScreen, FocuSee ve Screen Studio 4.0 ekledi, üstüne metinden kurgu ve dolgu sözcük silme geldi.
- **K9 ekran görüntüsü aracı (EKB-39):** Cap 2026'da ekran görüntüsü kipini ve paylaşımını ekledi; ShareX 20 görsel
  düzenleyicisini baştan yazdı.
- **K11 sahne bileşimi (EKB-32):** canlı sahne değil, kayıt sonrası kamera düzeni değiştirme yaygınlaştı (Cap, Tella,
  Camtasia). Bunun K11'e girip girmediği sahibin okuması.
- **K8 filigran:** bu turda rakip sayfalarında aranmadı; tabloya satır yazılmadı.

## Doğrulanamayanlar

- **Screen Studio 4.0'ın tarihi ve içeriği.** Resmi sürüm notları sayfası yalnız gezinti metni döndürdü; bilgi
  üçüncü taraf haberden [3].
- **Bandicam Smart Zoom'un yöntemi.** Canlı mı, kayıt sonrası mı çalıştığı sürüm notundan anlaşılmıyor.
- **Sürüm numaraları:** FocuSee, Snagit, Cursorful, Canvid, ScreenPal, Loom, Zight, ScreenRec, Descript.
- **Rapidemo.** Site adı çözülemedi; yalnız bir rakip blogunda adı geçiyor [54].
- **Loom:** bildirim gizleme ve konuşmacı notları. **ScreenRec:** hesap gerekliliği ve depolama miktarı.
- **Cap:** GIF dışa aktarma; Instant Mode'un kayıt sürerken yükleyip yüklemediği.
- **CleanShot X:** videoyu yerinde kırpma (yalnız Video Editor adı görüldü; EKB-09'da sayılmadı).
- **Bandicam:** tuş gösterimi, yükleme hedefleri, dosya adı şablonu.
- **ScreenToGif** düzenleyici ayrıntısı ve **Kap** eklenti listesi.
- **OBS dosya adı biçimi belirteçleri.** Kaynak kodda `FilenameFormatting` ayarı görüldü, belirteç listesi okunmadı [55].
- **Screen Studio fikir panosundaki sayılar.** Sayfa sayıları etiketlemiyor; oy olduğu varsayım.
- **Windows'ta bildirim susturma.** Kayıt başlarken bildirimleri kendiliğinden susturan bir Windows aracı ve bunun
  belgeli programlama yolu bulunamadı. EKB-20'nin "orta" iş tahmini bu yüzden zayıf.
- **Wayland'da genel tuş dinleme.** EKB-14 için yol araştırılmadı.
- **Tıklama halkası renkleri.** EKB-10 renk seçtirirse, "renk yalnız palet dosyasından" kuralının kayda çizilen
  bir efekt için geçerli olup olmadığı sahibin okuması.
- **Efektlerin kayıt akıcılığına etkisi.** `docs/olcumler/` altında ölçümü yok.
- **Öncelik oylarla desteklenmedi.** Sunum cilası özellik isteklerinin hiçbirinde iki haneli oy görülmedi; iki haneli
  iki kayıt bir hata (Recordly #34) ve bir paylaşım isteği (ShareX #3992).
- **"Yaygın rakip" ölçüsü.** Öncelik 1 kuralındaki "yaygın" kullanıcı ya da satış sayısıyla ölçülmedi; güncel sürümü
  olan ve özelliği resmi sayfasında ya da deposunda yazan rakip sayıldı.

## Kaynaklar

- [1] https://screen.studio/ — Screen Studio özellikleri, fiyat, yalnız macOS
- [2] https://screen.studio/guide/auto-zoom — otomatik yakınlaştırmanın nasıl çalıştığı
- [3] https://alternativeto.net/news/2026/10/screen-studio-4-0-adds-transcript-editing-a-camera-cutout-tool-and-more-mouse-effects/ — Screen Studio 4.0 haberi (üçüncü taraf)
- [4] https://screenstudio.featurebase.app/ — Screen Studio fikir panosu
- [5] https://cap.so/ — Cap kipleri, düzenleyici, depolama seçenekleri, fiyat
- [6] https://github.com/CapSoftware/Cap — Cap sürüm notları (0.4.84–0.6.0) ve açık istekler
- [7] https://cleanshot.com/features — CleanShot X özellikleri
- [8] https://cleanshot.com/changelog — CleanShot X 5.0, 5.0.1, 4.8.4
- [9] https://www.bandicam.com/ — Bandicam sürümü, fiyat, kamera ayarları
- [10] https://www.bandicam.com/guide/draw-on-screen/ — Bandicam kayıt sırasında çizim
- [11] https://www.bandicam.com/downloads/version_history/ — Bandicam 8.0.0, 8.2.0, 27.0.6
- [12] https://www.bandicam.com/how-to/mouse-effects/ — Bandicam fare efektleri
- [13] https://github.com/webadderall/Recordly — Recordly özellikleri, sürümler, açık istekler
- [14] https://github.com/siddharthvaddem/openscreen — OpenScreen özellikleri ve istekleri (arşiv)
- [15] https://github.com/EtienneLescot/openscreen — OpenScreen topluluk kolu
- [16] https://github.com/tamnguyenvan/screenarc — ScreenArc
- [17] https://github.com/syi0808/screenize — Screenize
- [18] https://cursorful.com/ — Cursorful
- [19] https://www.canvid.com/ — Canvid
- [20] https://focusee.imobie.com/ — FocuSee
- [21] https://www.tella.com/ — Tella
- [22] https://www.techsmith.com/camtasia/features/ — Camtasia özellikleri
- [23] https://www.techsmith.com/camtasia/whats-new/ — Camtasia 2026.2
- [24] https://www.techsmith.com/snagit/features/ — Snagit
- [25] https://screenpal.com/screen-recorder — ScreenPal
- [26] https://github.com/alyssaxuu/screenity — Screenity özellikleri ve istekleri
- [27] https://learn.microsoft.com/en-us/sysinternals/downloads/zoomit — ZoomIt
- [28] https://learn.microsoft.com/en-us/windows/powertoys/mouse-utilities — PowerToys fare araçları
- [29] https://presentifyapp.com/ — Presentify
- [30] https://github.com/mulaRahul/keyviz — Keyviz
- [31] https://github.com/keycastr/keycastr — KeyCastr
- [32] https://github.com/Code52/carnac — Carnac (bayat)
- [33] https://getsharex.com/changelog — ShareX 18.0.0–21.0.0 sürüm notları
- [34] https://getsharex.com/docs/custom-uploader — ShareX özel yükleyici
- [35] https://github.com/ShareX/ShareX — kaynak kod: `ShareX/Enums.cs` (kayıt sonrası ve yükleme sonrası görevler), `ShareX.HelpersLib/NameParser/CodeMenuEntryFilename.cs` (ad deseni belirteçleri), `ShareX.UploadersLib/Enums.cs` (28 dosya hedefi); açık istekler
- [36] https://github.com/NickeManarin/ScreenToGif — ScreenToGif
- [37] https://www.loom.com/screen-recorder — Loom
- [38] https://support.atlassian.com/loom/docs/blur-sensitive-information/ — Loom bulanıklık kısıtları
- [39] https://zight.com/screen-recorder/ — Zight
- [40] https://support.zight.com/hc/en-us/articles/5824298961815 — Zight Rahatsız Etme (yalnız Mac)
- [41] https://screenrec.com/ — ScreenRec
- [42] https://www.descript.com/screen-recording — Descript
- [43] https://github.com/wulkano/Kap — Kap sürümü ve istekleri
- [44] https://support.apple.com/guide/mac-help/take-screenshots-or-screen-recordings-mh26782/mac — macOS ekran kaydı aracı
- [45] https://blogs.windows.com/windows-insider/2025/06/19/gif-export-in-snipping-tool-begins-rolling-out-to-windows-insiders/ — Snipping Tool kırpma ve GIF
- [46] https://github.com/royshil/obs-backgroundremoval — OBS arka plan silme eklentisi
- [47] https://obsproject.com/forum/resources/background-removal-virtual-green-screen-low-light-enhance.1260/ — eklentinin indirme sayısı (1.634.010)
- [48] https://github.com/BlankSourceCode/obs-zoom-to-mouse — OBS fareye yakınlaştırma betiği (bayat)
- [49] https://obsproject.com/forum/threads/option-to-highlight-mouse-cursor-and-mouse-clicks.81966/ — OBS forumu, imleç vurgusu isteği
- [50] https://github.com/microsoft/PowerToys — PowerToys sürümü
- [51] https://github.com/MathewSachin/Captura — Captura (bayat)
- [52] https://alternativeto.net/software/stealthly/about — Stealthly (üçüncü taraf; yalnız destek)
- [53] https://deskshare.com/forums/ds_postst3299_Hide-desktop-icons-while-recording-screen.aspx — masaüstü simgesi gizleme (2017 forum; yalnız destek)
- [54] https://www.screensnap.pro/blog/screen-studio-for-windows — Windows'ta Screen Studio benzerleri listesi (rakip blogu)
- [55] https://github.com/obsproject/obs-studio — OBS sürümü ve kaynak kodda `FilenameFormatting` ayarı
- [56] https://www.loom.com/community/d97180be7d674f4fbf57744365457162-pg — Loom tıklama vurgusu (eski belgeden, yeniden okunmadı)

## Komşu Kola Not

- Recordly ve OpenScreen Windows'ta pencereyi WGC ile (Windows'un pencere yakalama arayüzü) yakalıyor; K3 rafta [13][14].
- Cap #192 HEVC dışa aktarma, 4 oy (kodlayıcı) [6].
- Cap #504 yalnız kamera kaydı kipi, 4 oy (kaynak) [6].
- Cap #856 kayıt profilleri, 2 oy (ayar kümelerini adla saklama) [6].
- Kap #145 bilgisayar sesi, 38 oy; ShareX #848 oyundan yakalama, 13 oy [43][35].
- Bandicam 8.2.0 kayıt alanı seçerken web öğesini kendiliğinden algılıyor; Cap 0.5.7 alan seçimlerini yeniden kullanıyor [11][6].
- Cap 0.6.0 ve Descript "Studio Sound", Canvid ses iyileştirme sunuyor (ses) [6][42][19].
- Recordly #662 ses yankısı (8 oy) ve #658 ses kaydı sorunları (5 oy) [13].
- Önceki taramada "kodda bulunamadı" denen ses seviyesi göstergesi, bölüm işareti ve kısayol atama bugün kodda var.
