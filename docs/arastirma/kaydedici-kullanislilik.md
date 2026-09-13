# Ekran Kaydedicilerde Kullanışlılık Yüzeyi

Tarih: 13 Eylül 2026
Kapsam: Bandicam, OBS Studio, ShareX, Camtasia, Snagit, NVIDIA ShadowPlay (GeForce
Experience / NVIDIA App), Xbox Game Bar, Screenpresso, Loom.

Bu belge **kullanıcının elini kolaylaştıran yüzeyi** anlatır: bölge seçme, çerçeve, tepsi,
kısayol, imleç vurgusu, geri sayım, otomatik ayar, hedef boyut. Kodek, bit hızı hesabı ve
ffmpeg argüman tarafı ayrı belgededir:
`docs/arastirma/ekran-kaydedici-ozellik-karsilastirmasi.md`. Orası tekrar edilmez.

Her başlık üç parçalı: **Kim** (hangi program ne yapıyor), **Nasıl** (mekanizma, sayı, tuş),
**Biz nasıl yaparız** (VidShrink'te karşılığı). Her başlığın sonunda iki satır var:
*Ücretsiz taklit edilebilir mi* ve *Avalonia + .NET 8'de zorluğu*.

**Doğrulama kuralı:** her iddianın yanında kaynak URL'si var. Belgede bulunamayan şey
"belgede doğrulanamadı" diye yazılmıştır; tahmin yürütülmemiştir. Özellikle ekran görüntüsü
üzerinden okunabilen ama yazılı kaynağı olmayan davranışlar (çerçeve kalınlığı piksel değeri,
yanıp sönme periyodu gibi) bilerek boş bırakılmıştır.

---

## 1. Bölge Seçimi

### Kim ne yapıyor

| Program | Sürükleyerek bölge | Pencere yakalama (vurgulu) | Monitör seçme | Oran/çözünürlük kilidi | Son bölgeyi hatırlama |
|---|---|---|---|---|---|
| Bandicam | Var ("Rectangle on a screen") | Var (Screen Recording > pencere seçimi) | Var (Fullscreen > monitör) | Ön ayar listesi + Shift ile oran koruma | Çerçeve konumu oturumlar arası kalır |
| OBS Studio | Dolaylı: kaynak + kırpma | "Window Capture" kaynağı, listeden seçim | "Display Capture" kaynağı | Canvas çözünürlüğü ayarla belirlenir | Sahne olarak kalır |
| ShareX | Var, ana akış bu | Var, imleç pencerenin üstüne gelince çerçeve | Sayı tuşlarıyla (1, 2, 3...) | Shift oranlı, Alt ön ayara oturtma | "Last region" özelliği var |
| Camtasia | Var (yeşil kutu) | Var (pencere/uygulama) | Var (tam ekran) | Kilit düğmesi + Widescreen/Standard ön ayarları | Son boyut hatırlanır |
| Snagit | Var, imleç artı işaretine döner | Var, otomatik pencere/alan vurgusu | Var | Shift 1:1, Ctrl 16:9, Ctrl+Shift 4:3 | Son bölge yeniden kullanılabilir |
| ShadowPlay | Yok, tam ekran veya oyun penceresi | Oyun süreci otomatik algılanır | Var (masaüstü kaydı) | Yok | Yok |
| Xbox Game Bar | Yok, yalnız odaktaki pencere | Odaktaki pencere zorunlu | Dolaylı | Yok | Yok |
| Screenpresso | Var, artı imleç + kırmızı vurgu | Var, yakalanabilir alan kırmızı çerçeveyle | Var | Belgede doğrulanamadı | Var (çalışma alanı geçmişi) |
| Loom | Var (özel boyut) | Var (pencere/sekme) | Var | Belgede doğrulanamadı | Var |

Kaynaklar:
- Bandicam dikdörtgen penceresi ve ön ayarlar, Shift ile oran koruma:
  https://www.bandicam.com/support/configuration/rectangle_window/
- Bandicam kısmi alan kaydı: https://www.bandicam.com/how-to/screen-recorder/
- ShareX bölge yakalama; büyüteç, ok tuşlarıyla piksel hassasiyeti, `Space` tam ekran,
  `1`/`2`/`3` monitör, "Last region", Shift oranlı, Alt ön ayara oturtma:
  https://getsharex.com/docs/region-capture
- Snagit: ok tuşları imleci bir piksel oynatır; Shift 1:1, Ctrl 16:9, Ctrl+Shift 4:3;
  `M` büyüteç: https://www.techsmith.com/learn/tutorials/snagit/snagit-hotkeys/
- Snagit ekran yakalama akışı ve otomatik alan vurgusu:
  https://www.techsmith.com/learn/tutorials/snagit/how-to-capture-your-screen/
- Camtasia: tam ekran / özel boyut / bölge / pencere / uygulama, kilit düğmesi:
  https://guides.library.ucsc.edu/DS/Resources/Camtasia
- Screenpresso: Print Screen'e basınca imleç artı işaretine döner ve yakalanabilir alan
  kırmızıyla vurgulanır: https://www.screenpresso.com/support/hot-keys/
- Xbox Game Bar yalnız odaktaki pencereyi kaydeder, masaüstü/Gezgin kaydı yoktur:
  https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/
- OBS kaynak tipleri ve pencere yakalamanın üstüne çizilen katmanları almadığı:
  https://obsproject.com/forum/threads/how-to-capture-mouse-highlighter-with-window-capture.182382/

### Nasıl çalışıyor

**Sürükleyerek çizme.** Ortak desen: tüm ekranı kaplayan, yarı saydam, en üstte bir örtü
penceresi açılır; fare basılı sürüklenirken seçim dikdörtgeni çizilir, dışı karartılır.
ShareX ve Snagit bu örtünün üstüne büyüteç ve piksel/koordinat rozeti koyar.

**Pencere yakalama vurgusu.** Örtü açıkken imlecin altındaki pencere `WindowFromPoint` ile
bulunur, sınırları `GetWindowRect` / `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`
ile alınır ve örtüye çerçeve olarak çizilir. Snagit ve Screenpresso bunu tek tıkla seçime
çevirir; ShareX'te aynı davranış var, imleç pencerenin üstüne gelince çerçeve beliriyor
(https://getsharex.com/docs/region-capture).

**Oran/çözünürlük kilidi.** İki ayrı şey var:
- *Anlık kilit*: Snagit'te Ctrl basılıyken 16:9, Ctrl+Shift basılıyken 4:3, Shift basılıyken
  1:1. Bandicam'de Shift basılıyken mevcut oran korunur.
- *Ön ayar listesi*: Bandicam'in dikdörtgen penceresinde hazır boyut listesi var ve
  Shift + sağ tıkla kendi ön ayarını ekleyebiliyorsun
  (https://www.bandicam.com/support/configuration/rectangle_window/). ShareX'te Alt basılı
  tutmak seçimi ön ayar boyutlarına oturtuyor.

**Piksel piksel klavye.** ShareX: ok tuşları imleci/şekli oynatır, `Ctrl`+ok yeniden
boyutlar, `Alt`+ok karşı köşeden boyutlar. Snagit: ok tuşları artı imleci bir piksel
oynatır. Bandicam, Camtasia, Loom, Screenpresso için piksel piksel klavye ayarı **belgede
doğrulanamadı**.

**Kenar tutamaçları.** Bandicam'in dikdörtgen penceresi kenarlarından tutulup yeniden
boyutlanır ve gövdesinden sürüklenip taşınır
(https://www.bandicam.com/support/configuration/rectangle_window/). Camtasia'nın yeşil
kutusunda köşe tutamaçları ve ortada kilit düğmesi vardır
(https://guides.library.ucsc.edu/DS/Resources/Camtasia).

### Biz nasıl yaparız

VidShrink'in kaydedicisi için önerilen sıra:

1. Tek bir **örtü penceresi** (tam sanal masaüstü dikdörtgeni; çok monitörde
   `SystemParameters`/`Screens.All` birleşimi). Avalonia'da `Window` + `SystemDecorations.None`
   + `Topmost=true` + `Background` yarı saydam.
2. Örtü üstünde üç kip: *serbest sürükleme*, *pencere yakala*, *monitör seç*. Kip geçişi
   tek tuş: `W` pencere, `M` monitör, `Esc` iptal — bu bizim seçimimiz, kimseden kopya değil.
3. Oran kilidi: Shift = mevcut oranı koru, Ctrl = 16:9, Ctrl+Shift = 4:3. Snagit ile aynı
   refleks; kullanıcı kas hafızasını taşır.
4. Ön ayar listesi: 1920x1080, 1280x720, 1080x1080, 1080x1920 (dikey), "son bölge".
   Son bölge `%APPDATA%` altındaki ayar dosyasına yazılır.
5. Ok tuşlarıyla 1 piksel, `Shift`+ok ile 10 piksel; `Ctrl`+ok yeniden boyutlama.
6. Seçim kutusunun kenarında canlı rozet: `1280 x 720 · 16:9` — sıkıştırma aracı olduğumuz
   için çift sayıya yuvarlama uyarısını da burada gösteririz (tek sayı genişlik yuv420p'yi
   kırar; bu bizim mevcut Core bilgimiz, dış kaynak gerektirmez).

**Ücretsiz taklit edilebilir mi:** Evet. Bölge seçimi hiçbir programın tekelinde değil;
ShareX (MIT) aynı davranışları açık kaynakla veriyor. Patent engeli görülmedi.
**Avalonia + .NET 8'de zorluğu:** Orta. Tam ekran örtü ve çizim kolay; zor kısımlar çok
monitörde DPI farkı (`Screens.All`, `Screen.Scaling`) ve pencere sınırı için P/Invoke:
`WindowFromPoint`, `GetWindowRect`, `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`,
`EnumWindows`.

---

## 2. Kayıt Çerçevesi

### Kim ne yapıyor

**Bandicam** — Dikdörtgen kipinde ekranda duran bir çerçeve penceresi vardır; üstünde REC
düğmesi, ses seviyesi göstergeleri (hoparlör mavi, mikrofon kırmızı) ve çizim araçları
bulunur. Çerçeve `Ctrl+Alt+H` ile gizlenip gösterilir
(https://www.bandicam.com/support/tips/bandicam-hotkeys/). Bandicam penceresinin "her zaman
üstte" seçeneği genel ayarlardadır
(https://www.bandicam.com/support/settings/general/). Çerçevenin **renk/kalınlık ayarı,
yanıp sönme, tıklamayı geçirme** maddeleri **belgede doğrulanamadı** — resmi yardım
sayfalarında bu ayarlar geçmiyor.

**Camtasia** — Kayıt alanı yeşil kesikli çizgiyle ve köşe tutamaçlarıyla gösterilir; kayıt
başlayınca ekran kararıp 3 saniyelik geri sayım gelir
(https://guides.library.ucsc.edu/DS/Resources/Camtasia).

**ShareX** — Kayıt sırasında seçilen bölgenin kenarında ince bir gösterge ve tepsi üstünden
durdurma vardır; kalıcı kalın çerçeve belgede tarif edilmiyor
(https://getsharex.com/blog/how-to-record-screen-windows/).

**OBS** — Ekran üstü çerçeve yok. Kayıt alanı OBS'in kendi önizleme penceresindedir.

**ShadowPlay / Xbox Game Bar** — Çerçeve yok; onun yerine köşede küçük bir kayıt göstergesi
(Game Bar'da süre sayacı olan mini pencere) vardır
(https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/).

**Loom** — Kayıt sırasında ekranın kenarında kontrol çubuğu durur ve gizlenebilir
(https://support.atlassian.com/loom/docs/hide-the-recording-controls/).

**Snagit / Screenpresso** — Çerçeve yakalama anında vardır; kayıt boyunca kalın çerçeve
davranışı belgede doğrulanamadı.

### Nasıl çalışıyor

Bandicam tarzı "tam ekranda bile üstte kalan" çerçeve Windows'ta şöyle kurulur:

- Çerçeve, içi delik bir katman penceresidir. Kenar şeridi kadar yer kaplar; ortası
  `SetWindowRgn` ile oyulur ya da pencere `WS_EX_LAYERED` + renk anahtarı
  (`SetLayeredWindowAttributes` + `LWA_COLORKEY`) ile saydamlaştırılır.
- Tıklamanın altına geçmesi için `WS_EX_TRANSPARENT` gerekir. Bu bayrak yalnız
  `WS_EX_LAYERED` ile birlikte güvenilir çalışır.
- "Tam ekran oyunun üstünde kalmak" ayrı bir iştir: sıradan `HWND_TOPMOST` özel tam ekran
  DirectX bağlamında görünmeyebilir; Bandicam'in oyun kipi zaten API kancasıyla (hook)
  çalışır. Bizim durumumuzda masaüstü/pencereli senaryo hedeflenmelidir.

### Biz nasıl yaparız

- Çerçeve = ayrı, kenarlıksız, `Topmost`, tıklamayı geçiren bir Avalonia penceresi.
  Renk `teknesyum-ui` belirtecinden gelir; yeni renk uydurulmaz.
- Kalınlık 2 CSS/DIP birimi; köşelerde 12x12 tutamaç (yalnız *ayar kipinde* tıklanabilir).
  Kayıt başlayınca tutamaçlar kaybolur ve pencere `WS_EX_TRANSPARENT` alır.
- Yanıp sönme: kayıt kipinde 1 saniyelik periyotla kenar renginin %60 ↔ %100 opaklığı.
  Kullanıcı kapatabilsin (bazı kullanıcılar için rahatsız edici, ayrıca kayda giriyorsa
  gürültü olur — biz çerçeveyi *kayıt bölgesinin dışına* çizeriz, 2 piksel dışarı taşarak).
- `Ctrl+Alt+H` çerçeveyi gizler/gösterir — Bandicam ile aynı tuş, çünkü aynı işi yapıyor
  ve kullanıcı bunu zaten biliyor.

**Ücretsiz taklit edilebilir mi:** Evet. Katman penceresi ve tıklama geçirme Win32'nin
standart yüzeyi; lisans ya da patent engeli görülmedi.
**Avalonia + .NET 8'de zorluğu:** Orta-zor. Avalonia kendiliğinden tıklama geçiren pencere
vermiyor; `TryGetPlatformHandle()` ile HWND alınıp `GetWindowLongPtr`/`SetWindowLongPtr`
(`GWL_EXSTYLE`) ile `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW |
WS_EX_NOACTIVATE` eklenmeli. Avalonia'nın bunu kutudan vermediği ve native API gerektiği
tartışmada açıkça söyleniyor:
https://github.com/AvaloniaUI/Avalonia/discussions/13827 ,
https://github.com/AvaloniaUI/Avalonia/issues/4956

---

## 3. Tepsi (Tray) Simgesi

### Kim ne yapıyor

**Bandicam** — Tepsi simgesi kayıt durumunu **renkle** söyler: kayıt sırasında kırmızı,
hazır durumda yeşil. Ayrıca "tepsiye küçültülmüş başlat" ve "tepsi balon bildirimlerini
kapat" seçenekleri var (https://www.bandicam.com/support/settings/general/).

**ShareX** — Ana çalışma yüzeyi tepsi menüsüdür: yakalama tipleri, yükleme geçmişi, araçlar
hep sağ tık menüsünden. Kayıt sonrası kodlama simgesi tepside görünür ve sonrası görevleri
oradan tetiklenir (https://getsharex.com/blog/how-to-record-screen-windows/).

**Screenpresso** — Tepsi simgesi + "Workspace" penceresi ikilisiyle çalışır; alınan her
yakalama çalışma alanına düşer (https://www.screenpresso.com/support/hot-keys/).

**Snagit** — Tepside durur, ayrıca ekran kenarında "Capture" düğmesi (OneClick) vardır
(https://www.techsmith.com/learn/tutorials/snagit/how-to-capture-your-screen/).

**OBS** — Tepsiye küçültme ve tepsi menüsünden kayıt başlat/durdur seçenekleri vardır
(Settings > General > System Tray). Menü içeriğinin tam listesi resmi belgede tek sayfada
değil; **kısmen doğrulanamadı**.

**Xbox Game Bar / ShadowPlay** — Klasik tepsi simgesi yerine kaplama (overlay) kullanır.
ShadowPlay'in kaplaması `Alt+Z` ile açılır
(https://www.partitionwizard.com/partitionmagic/nvidia-shadowplay-hotkey.html).

**Loom** — Windows'ta tepside/menü çubuğunda durur, kayıt kontrolleri ekranda ayrı bir
kontrol çubuğundadır (https://support.atlassian.com/loom/docs/hide-the-recording-controls/).

### Nasıl çalışıyor

Tepsi simgesi Win32'de `Shell_NotifyIcon` ile eklenir; durum değişimi simge ikonunun
değiştirilmesiyle (`NIM_MODIFY`) yapılır. Bandicam'in kırmızı/yeşil ayrımı tam olarak budur.
Balon bildirimi `NIF_INFO` ile gider; Windows 10/11'de bu artık kaynak bildirimi (toast)
olarak görünür.

### Biz nasıl yaparız

- Üç durum, üç ikon: **hazır** (VidShrink logosu), **kayıtta** (dolu kırmızı daire),
  **duraklatıldı** (içi boş sarı daire). Renkler `teknesyum-ui` paletinden.
- İpucu metni (tooltip) canlı: `Kayıtta · 00:03:12 · 148 MB`. Hedef boyut aracı olduğumuz
  için **anlık dosya boyutunu** göstermek bizim ayırt edici yanımız; hiçbir kaydedicide
  tepsi ipucunda boyut görülmedi.
- Sağ tık menüsü: Kaydı Başlat/Durdur, Duraklat, Bölge Seç, Son Bölgeyi Kullan, Klasörü Aç,
  Ayarlar, Çıkış. Yedi madde; daha fazlası menüyü okunmaz yapıyor.

**Ücretsiz taklit edilebilir mi:** Evet, tamamen. `Shell_NotifyIcon` işletim sisteminin
kendi yüzeyi.
**Avalonia + .NET 8'de zorluğu:** Kolay. Avalonia 11'in yerleşik `TrayIcon` sınıfı
(`NativeMenu` ile birlikte) yeter; ikonu çalışma zamanında değiştirmek destekleniyor. Daha
zengin menü/bildirim gerekirse `H.NotifyIcon.Avalonia` paketi var. Balon yerine Windows
toast isteniyorsa `Microsoft.Toolkit.Uwp.Notifications` / `CommunityToolkit` gerekir; şart
değil.

---

## 4. Klavye Kısayolları

### Varsayılan tuşlar, program program

**Bandicam** (https://www.bandicam.com/support/tips/bandicam-hotkeys/)

| İş | Tuş |
|---|---|
| Kayıt başlat / durdur | `F12` |
| Duraklat | `Shift+F12` |
| Kaydı iptal et | `Ctrl+Shift+F12` |
| Ekran görüntüsü | `F11` |
| Hoparlörü sustur | `Ctrl+Alt+S` |
| Mikrofonu sustur | `Ctrl+Alt+M` |
| Dikdörtgeni göster/gizle | `Ctrl+Alt+H` |
| Çizim kipi | `Ctrl+Alt+D` |
| Hedef alanı seç | `Ctrl+Shift+F` |
| Web kamerasını büyüt | `Shift+Tab` |
| Web kamerası aç/kapa | `Tab` |
| FPS göster/gizle | `F9` |
| FPS konumu | `Shift+F9` |
| FPS sınırı | `F10` |
| Kalem / fosforlu / çizgi / ok | `Ctrl+Alt+2` / `3` / `4` / `5` |
| Kutu / elips / metin / silgi | `Ctrl+Alt+6` / `Ctrl+Alt+O` / `Ctrl+Alt+8` / `Ctrl+Alt+9` |

**ShareX** (https://getsharex.com/docs/region-capture ,
https://tutorialtactic.com/blog/sharex-shortcuts/)

| İş | Tuş |
|---|---|
| Tam ekran görüntüsü | `Print Screen` |
| Bölge görüntüsü | `Ctrl+Print Screen` |
| Ekran kaydı başlat/durdur | `Shift+Print Screen` |
| Bölge seçerken tam ekran al | `Space` |
| Belirli monitörü al | `1`, `2`, `3`, ... |
| Şekli piksel piksel oynat | ok tuşları |
| Yeniden boyutla | `Ctrl`+ok, `Alt`+ok |
| Oranı koru / ön ayara otur | `Shift` basılı / `Alt` basılı |
| Dikdörtgen / elips / serbest çizim | `R` / `E` / `F` |

**Snagit** (https://www.techsmith.com/learn/tutorials/snagit/snagit-hotkeys/)

| İş | Tuş (Windows) |
|---|---|
| Genel yakalama | `Print Screen` |
| Video: başlat / duraklat | `Shift+F9` |
| Video: durdur | `Shift+F10` |
| Çizim kipine gir/çık | `Ctrl+Shift+D` |
| Büyüteç | `M` |
| 1:1 oran / 16:9 / 4:3 | `Shift` / `Ctrl` / `Ctrl+Shift` basılı |
| İmleci 1 piksel oynat | ok tuşları |
| Dikey / yatay / çapraz kaydırmalı yakalama | `V` / `H` / `B` |

**Camtasia** (https://assets.techsmith.com/docs/pdf-camtasiastudio/camtasia_studio_8_hotkeys.pdf ,
https://www.techsmith.com/learn/tutorials/camtasia/camtasia-shortcuts/)

| İş | Tuş (Windows) |
|---|---|
| Kaydı başlat / sürdür | `F9` |
| Kaydı durdur | `F10` |
| İşaretleyici ekle | `Ctrl+M` |
| ScreenDraw aç/kapa | `Ctrl+Shift+D` |

**NVIDIA ShadowPlay / GeForce Experience**
(https://www.partitionwizard.com/partitionmagic/nvidia-shadowplay-hotkey.html)

| İş | Tuş |
|---|---|
| Kaplamayı aç/kapa | `Alt+Z` |
| Manuel kaydı aç/kapa ve kaydet | `Alt+F9` |
| Son 5 dakikayı kaydet (Instant Replay) | `Alt+F10` |
| Ekran görüntüsü | `Alt+F1` |
| Yayını aç/kapa | `Alt+F8` |
| Yayını duraklat/sürdür | `Alt+F7` |
| Kamerayı aç/kapa | `Alt+F6` |
| Özel kaplamayı aç/kapa | `Alt+F5` |
| FPS sayacı | `Alt+F12` |

**Xbox Game Bar** (https://axeetech.com/xbox-game-bar-keybind-every-shortcut-for-windows/ ,
https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/)

| İş | Tuş |
|---|---|
| Kaplamayı aç | `Win+G` |
| Kaydı başlat/durdur | `Win+Alt+R` |
| Son klibi kaydet | `Win+Alt+G` |
| Ekran görüntüsü | `Win+Alt+PrtScn` |

**Screenpresso** (https://www.screenpresso.com/support/hot-keys/ ,
https://www.screenpresso.com/support/change-hot-keys/)

| İş | Tuş |
|---|---|
| Bölge/ekran görüntüsü | `Print Screen` |
| Video kaydı | `Ctrl+Print Screen` |
| Kısayolları değiştir | Workspace > Settings > Hot keys |

**Loom** (https://support.atlassian.com/loom/docs/use-looms-keyboard-shortcuts/)

| İş | Windows | macOS |
|---|---|---|
| Kaydı başlat/durdur | `Ctrl+Shift+L` | `Cmd+Shift+L` |
| Duraklat/sürdür | `Alt+Shift+P` | `Option+Shift+P` |
| Kaydı iptal et | `Alt+Shift+C` | `Option+Shift+C` |
| Tam ekran görüntüsü | `Ctrl+Shift+1` | `Cmd+Shift+1` |
| Özel boyut görüntüsü | `Ctrl+Shift+2` | `Cmd+Shift+2` |
| Baştan başlat | `Ctrl+Shift+R` | `Cmd+Shift+R` |
| Çizim | `Ctrl+Shift+D` | `Cmd+Shift+D` |
| Konfeti | `Ctrl+Alt+C` | `Ctrl+Cmd+C` |

Loom yalnız QWERTY düzeni destekliyor; Dvorak gibi düzenlerde kısayollar doğru çalışmıyor
(aynı kaynak). Türkçe Q/F düzeni için bu bizim de dikkat etmemiz gereken tuzak.

**OBS Studio** — Varsayılan kısayol **yoktur**; tüm kısayollar Settings > Hotkeys altında
kullanıcı tarafından atanır. Gerekçe olarak klavye düzeni/oyun tuşu çakışması gösteriliyor
(https://obsproject.com/kb/keyboard-shortcuts ,
https://github.com/obsproject/obs-studio/wiki/Keyboard-Shortcuts).

### Nasıl kaydediliyor (Windows)

İki ayrı yol var ve ikisi farklı iş yapar:

1. **`RegisterHotKey(hWnd, id, fsModifiers, vk)`** — İşletim sistemine "bu kombinasyon
   benim" der. Tuş basıldığında pencereye `WM_HOTKEY` gelir. Temiz, ucuz, yönetici hakkı
   istemez. Kısıtı: aynı kombinasyonu başka bir uygulama önce aldıysa çağrı `false` döner —
   **çakışma bildirimi tam olarak buradan çıkar.** `Print Screen` gibi tuşlar Windows 11'de
   Snipping Tool tarafından kapılabiliyor; Screenpresso da, ShareX de bu çakışmayı belge
   düzeyinde uyarı olarak yazmış
   (https://www.screenpresso.com/support/snippingtool/ ,
   https://jeremysawesome.com/2025/04/11/fixing-sharex-hotkeys-the-print-screen-conflict-with-logi-options/).
2. **`SetWindowsHookEx(WH_KEYBOARD_LL, ...)`** — Düşük seviyeli klavye kancası. Her tuşu
   görür, çakışma diye bir şey yoktur, tuşu yutabilir (`return 1`). Bedeli: her tuş basımında
   süreç sınırı geçilir, kanca 300 ms'de cevap vermezse Windows kancayı düşürür
   (`LowLevelHooksTimeout`), antivirüs yazılımları şüpheyle bakar. Tuş gösterimi (bkz. 5.
   başlık) zaten bunu gerektirir.

### Biz nasıl yaparız

- Varsayılanlar: **`F12`** başlat/durdur (Bandicam refleksi), **`Shift+F12`** duraklat,
  **`Ctrl+Shift+F12`** iptal, **`Ctrl+Alt+H`** çerçeveyi gizle, **`Ctrl+Shift+F`** bölge seç.
  Aynı tuşları seçmek bilinçli: kullanıcı kas hafızasını taşısın.
- Kayıt yolu `RegisterHotKey`. Her tuş için dönen `false` toplanır ve ayarlar ekranında
  satırın yanında kırmızı rozet: `F12 — başka bir uygulama kullanıyor`. Sessizce yutmak
  en sık şikâyet edilen davranış.
- `Print Screen` varsayılan olarak **alınmaz**; Windows 11'de Snipping Tool ile çakışıyor.
- Kullanıcı isterse "agresif kip": `WH_KEYBOARD_LL` ile kısayolu kapma. Ayar açıkça
  "antivirüs uyarısı verebilir" notuyla sunulur.

**Ücretsiz taklit edilebilir mi:** Evet. `RegisterHotKey` ve `SetWindowsHookEx` Win32'nin
kendi API'leri; tuş kombinasyonlarının kendisi korunabilir bir şey değil.
**Avalonia + .NET 8'de zorluğu:** Orta. Avalonia global kısayol vermiyor (uygulama içi
`KeyBinding` sadece odakta çalışır). HWND `TopLevel.TryGetPlatformHandle()` ile alınır,
`WM_HOTKEY` için ileti döngüsüne girmek gerekir — pratikte gizli bir ileti penceresi
(`CreateWindowEx` ile message-only, `HWND_MESSAGE`) açmak en temiz yol. Çapraz platform
isteniyorsa `SharpHook` (libuiohook sarmalayıcı) tek pakette Windows/macOS/Linux veriyor;
bedeli düşük seviyeli kanca bedeli.

---

## 5. Tıklama ve Tuş Gösterimi

### Kim ne yapıyor

| Özellik | Bandicam | Camtasia | ShareX | OBS | Loom | Snagit | Game Bar / ShadowPlay |
|---|---|---|---|---|---|---|---|
| İmleci kayda dahil et | Var | Var | Var | Var (kaynak seçeneği) | Var | Var | Var |
| İmleci gizle | Var | Var | Var | Var | Belgede doğrulanamadı | Var | Yok |
| Tıklama halkası/renk | Var | Var | Yok (eklentisiz) | Yok (yerleşik değil) | Var | Yok | Yok |
| Tıklama sesi | Var | Var | Yok | Yok | Belgede doğrulanamadı | Yok | Yok |
| İmleç vurgusu (sarı daire) | Var | Var | Yok | Yok | Var | Yok | Yok |
| İmleci büyütme | Belgede doğrulanamadı | Var (editörde yakınlaştırma) | Yok | Yok | Yok | Yok | Yok |
| Basılan tuşları göster | Yok | Var (keystroke callout) | Yok | Yok | Yok | Yok | Yok |

Kaynaklar:
- Bandicam fare efektleri: "Add mouse highlight effect" seçilince imlecin çevresine **sarı
  daire** ekleniyor; sol/sağ tık sesleri eklenebiliyor ve sesler
  `C:\Program Files\Bandicam\data` altındaki `lclick.wav` / `rclick.wav` dosyaları
  değiştirilerek özelleştiriliyor: https://www.bandicam.com/how-to/mouse-effects/
- Camtasia keystroke callout: kayıt sırasındaki tuşlardan otomatik üretilebiliyor:
  https://techshelps.github.io/CamtasiaStudio/Topics/Edit/Callouts/Keystroke%20Callouts.htm
- Camtasia SmartFocus, imlecin tıkladığı yere otomatik yakınlaşma:
  https://guides.library.ucsc.edu/DS/Resources/Camtasia
- Loom ayarlarında "highlight mouse clicks" açılabiliyor:
  https://www.loom.com/community/d97180be7d674f4fbf57744365457162-pg
- OBS'te yerleşik tıklama vurgusu yok; kullanıcılar yıllardır istiyor:
  https://obsproject.com/forum/threads/option-to-highlight-mouse-cursor-and-mouse-clicks.81966/
- Açık kaynak tuş göstericiler: Carnac, Keyviz, Screenkey, KeyCastr, YAKD:
  https://alternativeto.net/software/keycastow/

### Nasıl çalışıyor

**İmleç.** Windows'ta ekran yakalama API'leri (`BitBlt`, Desktop Duplication) imleci
kendiliğinden vermez; imleç ayrı çekilir: `GetCursorInfo` + `GetIconInfo` ile bitmap,
`GetCursorPos` ile konum, sonra kareye çizilir. ffmpeg tarafında `gdigrab` `draw_mouse=1`
ile aynı işi yapıyor.

**Tıklama halkası.** İki uygulama yolu var:
1. *Kareye çizmek* — tıklama anı `WH_MOUSE_LL` kancasıyla yakalanır, o andan itibaren
   ~400 ms boyunca yakalanan karelerin üstüne genişleyen halka çizilir. Kayıtta görünür,
   ekranda görünmez.
2. *Ekrana çizmek* — tıklama anında tıklamayı geçiren katman pencerede halka animasyonu
   oynatılır; yakalama zaten ekranı aldığı için kayda düşer. Ekranda da görünür (sunum
   için iyi, kullanıcıyı rahatsız edebilir).

**Tuş gösterimi.** `WH_KEYBOARD_LL` kancası, tuş adı çözümü (`MapVirtualKey`,
`GetKeyNameText`), ekranın alt köşesinde 2-3 saniye duran kutu. Carnac'ın yaptığı tam olarak
budur ve açık kaynaktır.

**Tıklama sesi.** Kayıt karışımına tıklama anında kısa bir wav bindirilir. Bandicam bunu
dosya değiştirerek özelleştirilebilir yapmış.

### Biz nasıl yaparız

- Halka: yol 1 (kareye çizme). Sebep: ekranı kirletmez, sonradan kapatılabilir, ffmpeg
  zincirine `drawbox`/overlay yerine kendi kare işlemcimizden girer.
- Varsayılan: halka **açık**, çap 48 px'den 16 px'e 350 ms'de küçülen, sol tık ana renkte,
  sağ tık ikincil renkte — renkler `teknesyum-ui` belirteçlerinden.
- İmleç vurgusu (sürekli sarı daire) **kapalı** gelir; eğitim videosu çeken açar.
- Tuş gösterimi ikinci dalga işi. `WH_KEYBOARD_LL` zaten global kısayol için kurulduysa
  ek maliyet küçük. Türkçe Q klavyede `ğ`, `ü`, `ş` tuş adları `GetKeyNameText` ile doğru
  gelir; `MapVirtualKey` ile elle çözmeye kalkmak yanlış harf üretir.
- Parola kutusuna yazarken tuş gösterimi kendiliğinden susmalı: `GetGUIThreadInfo` ile
  odaktaki denetim `ES_PASSWORD` ise gösterme. Bu, hiçbir programda görülmeyen ama
  gerekli olan bir emniyet.

**Ücretsiz taklit edilebilir mi:** Evet. Halka/vurgu görsel bir fikir, patent görülmedi;
Carnac ve Keyviz açık kaynak olarak aynı işi yapıyor. Bandicam'in `lclick.wav` dosyası
kopyalanamaz — kendi sesimiz üretilir.
**Avalonia + .NET 8'de zorluğu:** Tıklama halkasının kareye çizilmesi **kolay** (SkiaSharp
ya da doğrudan kare tamponuna çizim). Tuş gösterimi **orta-zor**: `SetWindowsHookEx` +
`WH_KEYBOARD_LL` P/Invoke, yönetilen geri çağrının GC tarafından toplanmaması için
`GCHandle` ile sabitlenmesi, 300 ms zaman aşımına takılmamak için kancada iş yapmayıp
kuyruğa atmak gerekir.

---

## 6. Geri Sayım ve Bitiş

### Kim ne yapıyor

**Camtasia** — Kayıt başlarken ekran kararır ve **3 saniyelik** geri sayım gösterilir;
Workflow ayarlarından kapatılabilir (https://guides.library.ucsc.edu/DS/Resources/Camtasia).

**Loom** — Kayda başlarken geri sayım gelir, "toparlanman için"
(https://www.loom.com/community/how-to-record-your-first-video). Kayıt bitince video
doğrudan buluta yüklenir ve **bağlantı panoya kopyalanır** — akışın merkezi paylaşımdır
(https://www.loom.com/community/d97180be7d674f4fbf57744365457162-pg).

**ShareX** — Geri sayım yerine **gecikme** var: Task Settings > Capture > Screen Recorder
altında "start recording after X seconds"
(https://github.com/ShareX/ShareX/issues/4434). Kayıt bitince "After capture tasks" zinciri
çalışır: dosyaya kaydet, yükle, bağlantıyı panoya kopyala
(https://getsharex.com/blog/how-to-record-screen-windows/).

**Bandicam** — Kayıt öncesi görsel geri sayım **belgede doğrulanamadı**. Buna karşılık
zamanlanmış kayıt (belirli saatte başlat) ve otomatik bitirme var
(https://www.bandicam.com/support/settings/general/). Bitiş sonrası "sistemi kapat"
seçeneği var (aynı kaynak).

**Snagit** — Video kaydı bitince dosya doğrudan Snagit Editor'da açılır
(https://www.techsmith.com/learn/tutorials/snagit/how-to-capture-your-screen/).

**Screenpresso** — Yakalanan her şey Workspace'e düşer, oradan düzenlenir/paylaşılır
(https://www.screenpresso.com/support/hot-keys/).

**Xbox Game Bar** — Kayıt bitince köşede "Oyun klibi kaydedildi" bildirimi çıkar, tıklayınca
galeri açılır (https://www.techradar.com/how-to/how-to-use-xbox-game-bar-in-windows-10).

**ShadowPlay** — Klip galeriye düşer, `Alt+Z` kaplamasından paylaşılır
(https://www.partitionwizard.com/partitionmagic/nvidia-shadowplay-hotkey.html).

**OBS** — Geri sayım yok, bitiş ekranı yok. Dosya klasöre yazılır; "Show Recordings"
menüsünden klasör açılır.

### Biz nasıl yaparız

- Geri sayım: **3-2-1**, ekranın ortasında büyük rakam, her rakam 1 saniye, son rakamdan
  sonra 200 ms boşluk. Ayarlanabilir: 0 (kapalı) / 3 / 5 / 10 saniye.
- Geri sayım katmanı tıklamayı geçirir ve **kayıt bölgesinin dışına** konur; içine konursa
  rakamlar kayda girer. Tam ekran kaydında ortaya konmak zorunda olduğu için kayıt, geri
  sayım bittikten sonra başlatılır (ffmpeg süreci önceden ısıtılır, `-ss` ile değil).
- Bitiş: **tek pencere**, üç düğme — *Klasörü Aç*, *Önizle*, *Sıkıştır*. Üçüncüsü bizim
  farkımız: kayıt biter bitmez VidShrink'in hedef boyut motoruna devredilir. Hiçbir
  kaydedicide bu yok; kaydedici ile sıkıştırıcı ayrı programlar.
- Bitiş penceresi 8 saniye sonra kendiliğinden kapanır, ayarla kapatılabilir.

**Ücretsiz taklit edilebilir mi:** Evet. Geri sayım ve bitiş akışı tamamen bizim.
**Avalonia + .NET 8'de zorluğu:** Kolay. Kenarlıksız, `Topmost`, tıklama geçiren pencere +
`DispatcherTimer`. Klasörü açmak: `Process.Start("explorer.exe", $"/select,\"{path}\"")`.

---

## 7. Otomatik En İyi Ayar

### Kim ne yapıyor

**OBS Studio** — Açılışta **Auto-Configuration Wizard** çalışır: "yayın için iyileştir" ya
da "kayıt için iyileştir" seçilir, donanım ve (yayınsa) yükleme hızı sınanır, bir başlangıç
ayar takımı önerilir. Sihirbazın muhafazakâr değerler koyduğu, sonradan elle artırılabildiği
söyleniyor (https://www.dacast.com/blog/best-obs-settings/ ,
https://riverside.com/blog/how-to-record-with-obs). Kodlayıcı seçimi (CPU/GPU) açılır
listeden yapılır (https://alive-project.com/en/streamer-magazine/article/2044/).

**Xbox Game Bar** — Kullanıcıya yalnız üç kaba düğme verir: kare hızı **30 veya 60 fps**
(varsayılan 30), video kalitesi **standart/yüksek**, ses kalitesi **96-192 kbit/s**. Elle
bit hızı kaydırıcısı **yoktur**; standart kalitede oyun/uygulamanın kendi çözünürlüğüne
uyar, 1080p'nin üstüne çıkmaz
(https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/ ,
https://www.elevenforum.com/t/change-video-frame-rate-for-game-recording-in-windows-11.18224/ ,
https://www.elevenforum.com/t/change-video-quality-for-game-recording-in-windows-11.18225/).

**ShadowPlay** — NVIDIA donanım kodlayıcısı (NVENC) zaten verilidir; kullanıcı kalite
ön ayarı seçer. Donanım tespiti gerekmez, sürücü zaten GPU'yu biliyor.

**Bandicam / Camtasia / Snagit / Loom / Screenpresso** — Kutudan makul bir varsayılanla
gelir; kullanıcı hiçbir şey seçmezse çalışır. Hangi donanım kodlayıcısını hangi mantıkla
seçtiklerinin **belgelenmiş bir kararı bulunamadı**; Bandicam ayar sayfasında kodlayıcı
listesi kullanıcıya açıktır (https://www.bandicam.com/guide/settings-video/).

### Nasıl çalışıyor (mekanizma)

Donanım kodlayıcı seçimi pratikte üç adımdır:
1. **Yoklama** — sistemdeki kodlayıcıları listele. ffmpeg tarafında
   `ffmpeg -hide_banner -encoders` çıktısında `h264_nvenc`, `h264_qsv`, `h264_amf`,
   `hevc_nvenc`, `av1_nvenc` aranır.
2. **Gerçek sınama** — listede görünmek yetmez; sürücü yoksa çalışma zamanında patlar.
   Bir saniyelik `testsrc` ile deneme kodlaması yapılır, çıkış kodu bakılır.
3. **Sıralama** — bulunanlar sabit bir tercih sırasına konur ve ilki seçilir; hiçbiri
   yoksa `libx264` yazılım koluna düşülür.

### Biz nasıl yaparız

VidShrink zaten yoklama yapan bir katmana sahip (`src/VidShrink.Ffmpeg`). Kaydedici için:

- İlk çalıştırmada bir kez yoklama, sonuç ayar dosyasına yazılır, ffmpeg sürümü değişince
  yeniden koşar.
- Tercih sırası: `h264_nvenc` → `h264_qsv` → `h264_amf` → `libx264`. Gerekçe: NVENC en yaygın
  ve en kararlı; yazılım kolu her yerde çalışır ama 1080p60 ekran kaydında CPU'yu yer.
- Kullanıcı hiçbir şey seçmezse: bölge çözünürlüğü + 30 fps + "orta kalite" hedefi.
  Kare hızı seçimi ekran içeriğine göre değil, kullanıcı beyanına göre: *Sunum/eğitim* 30 fps,
  *Oyun/animasyon* 60 fps. İki düğme, üç değil.
- OBS'in sihirbazından alınacak ders: **muhafazakâr başla, artırmayı kullanıcıya bırak.**
  Xbox Game Bar'dan alınacak ders: **üç düğmeden fazlası kullanıcıyı kaçırıyor.**

**Ücretsiz taklit edilebilir mi:** Evet, mekanizma tarafında. Uyarı: H.264/HEVC kodlama
patent havuzlarına (MPEG LA / Access Advance) tabi; ffmpeg'i dağıtan her araç bu soruyu
taşır. Bu VidShrink'in mevcut sıkıştırma tarafındaki soruyla aynı sorudur, kaydediciyle
yeni gelen bir yük değil. AV1 ve VP9 bu açıdan daha temiz.
**Avalonia + .NET 8'de zorluğu:** Kolay-orta. Süreç çağırma ve çıktı ayrıştırma zaten var.
Tek tuzak: ffmpeg'in stderr borusu boşaltılmazsa 4096 baytta dolup süreci kilitliyor —
bu projede daha önce yaşandı, yoklama kodunda stderr'i ayrı okumak şart.

---

## 8. Hedef Boyut / Hedef Süre

### Kim ne yapıyor

**Hedef süre (kaydı X sonra durdur):**

- **Bandicam** — "Auto Complete Recording": kayıt **süre sınırı**, kaydedilen **dosya boyutu
  sınırı**, **sessizlik süresi** sınırı ve biter bitmez **sistemi kapatma**
  (https://www.bandicam.com/support/settings/general/ ,
  https://www.bandicam.com/auto-stop-recording/). Ayrıca zamanlanmış kayıt: belirli saatte
  günlük/haftalık başlat-durdur (aynı kaynak).
- **Xbox Game Bar** — Azami kayıt uzunluğu ayarı: **30 dakika ile 4 saat** arası
  (https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/).
- **OBS Studio** — Doğrudan "şu kadar sonra dur" yok; ama **Automatic File Splitting** var:
  Advanced çıkış kipinde kaydı **süreye** ya da **dosya boyutuna** göre otomatik böler
  (https://github.com/obsproject/obs-studio/discussions/5078). Yani sınır dosyaya konuyor,
  kayda değil.
- **ShareX, Camtasia, Snagit, Screenpresso, Loom, ShadowPlay** — Kullanıcının belirlediği
  hedef süre/boyut ayarı **belgelerde doğrulanamadı**. (Loom'un ücretsiz katmanındaki video
  uzunluk sınırı bir *plan kısıtı*, kullanıcı ayarı değil.)

**Hedef boyut (şu kadar MB olsun):**

Dokuz programın hiçbirinde **"kayıt X MB olsun" diye bit hızını önceden hesaplayan** bir
ayar bulunamadı. En yakını Bandicam'in boyut sınırıdır ve o da **hesaplamaz, keser**: dosya
sınıra ulaşınca kaydı bitirir. OBS'in boyuta göre bölmesi de aynı mantık — kesme, hedefleme
değil.

### Nasıl hesaplanır (bizim tarafımız)

Hedef boyut, kayıtta ancak **süre bilindiğinde** önceden hesaplanabilir:

```
toplam_bit = hedef_MB * 8 * 1024 * 1024
ses_bit    = ses_kbps * 1000 * süre_sn
video_bps  = (toplam_bit - ses_bit) / süre_sn * emniyet_payı
```

`emniyet_payı` olarak 0.95 makul (konteyner ek yükü + VBV dalgalanması). Süre bilinmiyorsa
iki seçenek var:

1. **Bütçe kipi** — kullanıcı hem boyut hem süre söyler ("10 dakika, 100 MB"); CBR benzeri
   sıkı bit hızı ve `-maxrate`/`-bufsize` ile sürülür, sapma küçük kalır.
2. **Sonradan sıkıştırma** — kayıt yüksek kalitede alınır, bitince VidShrink'in mevcut iki
   geçişli hedef boyut motoruna devredilir. Doğruluk yüksektir, bedeli ek kodlama süresidir.

Üçüncü bir ara yol da var: **canlı bütçe göstergesi.** Kayıt sürerken tepsi ipucunda ve
çerçevede "148 MB / 200 MB · kalan ~4 dk" yazar; kullanıcı hedefe yaklaşınca kendi karar
verir. Bu, hiçbir kaydedicide görülmedi ve en ucuz özgün fikir.

### Biz nasıl yaparız

- **Otomatik bitirme**: süre sınırı (dakika), boyut sınırı (MB), ikisi de. Bandicam ile aynı
  kabiliyet; bizde ek olarak sınıra 30 saniye kala uyarı.
- **Hedef boyut**: varsayılan yol *sonradan sıkıştırma*. Kaydedici ile sıkıştırıcının aynı
  programda olması VidShrink'in tek gerçek üstünlüğü; bitiş penceresindeki "Sıkıştır"
  düğmesi bu yüzden üçüncü değil, **birinci** düğme olmalı.
- **Bütçe kipi** ikinci dalga. Süreyi kullanıcı beyan ederse tek geçişte hedefe yaklaşmak
  mümkün; beyan yanlışsa dosya hedefi aşar ve bu kullanıcıya önceden söylenir.

**Ücretsiz taklit edilebilir mi:** Evet, hesap tamamen bizim; kimsede taklit edilecek bir
şey de yok. Bu başlık rekabetin en zayıf olduğu yer.
**Avalonia + .NET 8'de zorluğu:** Kolay. Hesap `VidShrink.Core`'un mevcut plan motoruna
oturur. Canlı boyut göstergesi için çıkış dosyasının uzunluğunu saniyede bir okumak yeter
(`FileInfo.Length`, paylaşımlı okuma kipiyle).

---

## 9. Windows API Özeti (tek yerde)

| İş | API | Not |
|---|---|---|
| Tıklamayı geçiren katman | `SetWindowLongPtr(GWL_EXSTYLE, WS_EX_LAYERED \| WS_EX_TRANSPARENT)` | `WS_EX_TRANSPARENT` tek başına yetmez, `WS_EX_LAYERED` ile |
| Katman saydamlığı | `SetLayeredWindowAttributes` (`LWA_COLORKEY`, `LWA_ALPHA`) | Renk anahtarıyla "içi delik" pencere |
| Pencerenin odak çalmaması | `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW` | Alt+Tab listesinde görünmemesi için |
| İçi oyulmuş çerçeve | `CreateRectRgn` + `CombineRgn(RGN_DIFF)` + `SetWindowRgn` | Katman yerine bölge yolu |
| Üstte kalma | `SetWindowPos(HWND_TOPMOST)` | Tam ekran özel DirectX bağlamında garanti değil |
| Global kısayol | `RegisterHotKey` / `UnregisterHotKey`, `WM_HOTKEY` | `false` dönüşü = çakışma; kullanıcıya bildir |
| Düşük seviyeli klavye | `SetWindowsHookEx(WH_KEYBOARD_LL)`, `CallNextHookEx` | 300 ms zaman aşımı; geri çağrıyı `GCHandle` ile sabitle |
| Düşük seviyeli fare | `SetWindowsHookEx(WH_MOUSE_LL)` | Tıklama halkası ve tıklama sesi için |
| Tepsi simgesi | `Shell_NotifyIcon` (`NIM_ADD`, `NIM_MODIFY`, `NIF_INFO`) | Avalonia `TrayIcon` bunu sarmalıyor |
| İmlecin altındaki pencere | `WindowFromPoint`, `ChildWindowFromPointEx` | Bölge seçiminde pencere vurgusu |
| Pencere sınırı (gölgesiz) | `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` | `GetWindowRect` gölgeyi de veriyor, yanlış kırpar |
| İmleç bitmap'i | `GetCursorInfo`, `GetIconInfo`, `DrawIconEx` | Yakalama API'leri imleci vermiyor |
| Tuş adı | `GetKeyNameText`, `MapVirtualKeyEx` | Türkçe Q/F düzeninde `GetKeyNameText` doğru |
| Parola kutusu tespiti | `GetGUIThreadInfo` + `ES_PASSWORD` | Tuş gösterimini susturmak için |
| Ekran yakalama | `gdigrab` (ffmpeg) ya da Desktop Duplication (`IDXGIOutputDuplication`) | DD daha hızlı, DXGI birlikte gelir |

Avalonia'nın bu yüzeyi kutudan vermediği, HWND alıp native API kullanmak gerektiği resmi
tartışmalarda yazılı: https://github.com/AvaloniaUI/Avalonia/discussions/13827 ,
https://github.com/AvaloniaUI/Avalonia/discussions/15836 ,
https://github.com/AvaloniaUI/Avalonia/issues/4956

HWND'ye erişim: `TopLevel.TryGetPlatformHandle()?.Handle`. Bu yalnız Windows kolunda
kullanılır; macOS/Linux kolları `#if`/arayüz ayrımıyla ayrılmalı, yoksa çapraz platform
derlemesi kırılır.

---

## 10. Toplu Karşılaştırma

| Yetenek | Bandicam | OBS | ShareX | Camtasia | Snagit | ShadowPlay | Game Bar | Screenpresso | Loom |
|---|---|---|---|---|---|---|---|---|---|
| Sürükleyerek bölge | + | ~ | + | + | + | - | - | + | + |
| Pencere vurgusu | + | ~ | + | + | + | - | - | + | + |
| Oran kilidi | + | ~ | + | + | + | - | - | ? | ? |
| Piksel klavye ayarı | ? | - | + | ? | + | - | - | ? | ? |
| Üstte kalan çerçeve | + | - | ~ | + | ~ | - | - | ~ | ~ |
| Tepside durum rengi | + | ~ | + | ? | ? | - | - | + | ? |
| Varsayılan kısayol | + | **yok** | + | + | + | + | + | + | + |
| Tıklama halkası | + | - | - | + | - | - | - | ? | + |
| Tıklama sesi | + | - | - | + | - | - | - | ? | ? |
| Tuş gösterimi | - | - | - | + | - | - | - | - | - |
| Geri sayım | ? | - | ~ (gecikme) | + (3 sn) | ? | - | - | ? | + |
| Bitişte önizleme | ? | - | + | + | + | + | + | + | + |
| Otomatik ayar sihirbazı | - | + | - | - | - | + | + | - | + |
| Süre sınırı | + | ~ (bölme) | - | - | - | - | + (30 dk-4 sa) | - | - |
| Boyut sınırı | + | ~ (bölme) | - | - | - | - | - | - | - |
| **Hedef boyut hesabı** | **-** | **-** | **-** | **-** | **-** | **-** | **-** | **-** | **-** |

`+` var, `-` yok, `~` dolaylı/kısmi, `?` belgede doğrulanamadı.

Son satır bu araştırmanın ana bulgusudur: **dokuz programın hiçbiri kayıt öncesi hedef
boyut hesabı yapmıyor.** VidShrink'in kaydedicisi bu boşluğa oturuyor.

---

## VidShrink İçin Öncelik Sırası

1. **Sürükleyerek bölge seçimi + son bölgeyi hatırlama.** Neden: kaydedicinin kapısı bu;
   olmadan hiçbir şey kullanılmıyor. "Son bölge" tek satır ayar, en çok kullanılan özellik.
   Tahmini zorluk: **orta** (çok monitör ve DPI ölçekleme tuzakları).
2. **Global kısayol: `F12` başlat/durdur, `Shift+F12` duraklat, `Ctrl+Shift+F12` iptal.**
   Neden: kayıt sırasında programa dönmek kaydı bozuyor; kısayol olmadan ürün eksik.
   `RegisterHotKey` `false` dönerse çakışma kullanıcıya gösterilir. Zorluk: **orta**
   (gizli ileti penceresi + `WM_HOTKEY` döngüsü).
3. **Tepsi simgesi, üç durumlu renk ve canlı ipucu (süre + anlık MB).** Neden: kayıt
   sürerken tek görünür durum göstergesi; anlık boyut bizim ayırt edici yanımız.
   Zorluk: **kolay** (Avalonia `TrayIcon`).
4. **Bitiş penceresi: Sıkıştır / Klasörü Aç / Önizle.** Neden: kaydedici ile sıkıştırıcının
   aynı programda olması tek gerçek üstünlüğümüz; "Sıkıştır" birinci düğme olmalı.
   Zorluk: **kolay**.
5. **Otomatik bitirme: süre sınırı ve dosya boyutu sınırı.** Neden: uzun kayıtta disk
   doldurmayı önler, Bandicam'in en çok kullanılan ayarı; bizde 30 saniye kala uyarı ile.
   Zorluk: **kolay** (`FileInfo.Length` yoklaması + sayaç).
6. **Kayıt çerçevesi: üstte kalan, tıklamayı geçiren, `Ctrl+Alt+H` ile gizlenen.**
   Neden: kullanıcı neyi kaydettiğini görmeden güvenmiyor. Zorluk: **zor**
   (`WS_EX_LAYERED | WS_EX_TRANSPARENT`, HWND P/Invoke, çok monitör konumlandırma).
7. **Geri sayım 3-2-1 (0/3/5/10 saniye seçenekli).** Neden: kayıt başlarken elin fareden
   çekilmesi için; Camtasia ve Loom bunu standart yapmış. Zorluk: **kolay**.
8. **Oran kilidi ve ön ayarlar (Shift oran, Ctrl 16:9, Ctrl+Shift 4:3; 1920x1080, 1280x720,
   dikey 1080x1920).** Neden: hedef platform çözünürlüğü tutmayan kayıt sonradan yeniden
   ölçekleniyor, kalite gidiyor. Zorluk: **kolay** (seçim matematiği).
9. **Otomatik kodlayıcı seçimi: yokla, sına, sırala (`h264_nvenc` → `qsv` → `amf` →
   `libx264`).** Neden: kullanıcı hiçbir şey seçmeden çalışmalı; yazılım kolu 1080p60'ta
   CPU'yu yiyor. Zorluk: **orta** (stderr'i boşaltmayı unutma — süreç kilitleniyor).
10. **Tıklama halkası ve imleç gizleme.** Neden: eğitim/gösterim videosunun en görünür
    farkı; kareye çizildiği için ekranı kirletmiyor. Zorluk: **orta**
    (`WH_MOUSE_LL` + kare üstü çizim).
11. **Piksel piksel klavye ayarı (ok = 1 px, Shift+ok = 10 px, Ctrl+ok = boyutla) ve
    büyüteç.** Neden: 1920x1080'i piksel piksel tutturmak fareyle mümkün değil; ShareX ve
    Snagit'in sessiz üstünlüğü bu. Zorluk: **kolay** (seçim örtüsü zaten varsa).
12. **Tuş gösterimi (basılan tuşları ekranda gösterme), parola kutusunda otomatik susma.**
    Neden: Camtasia dışında kimsede yok, eğitim videosu çekenler için belirleyici; ama
    kullanıcı kitlesi dar, bu yüzden sonda. Zorluk: **zor** (`WH_KEYBOARD_LL`, 300 ms
    zaman aşımı, `GCHandle` sabitleme, Türkçe klavye tuş adları, antivirüs sürtünmesi).

---

## Kaynakça

Bandicam
- https://www.bandicam.com/support/tips/bandicam-hotkeys/
- https://www.bandicam.com/support/configuration/rectangle_window/
- https://www.bandicam.com/support/settings/general/
- https://www.bandicam.com/how-to/mouse-effects/
- https://www.bandicam.com/how-to/screen-recorder/
- https://www.bandicam.com/auto-stop-recording/
- https://www.bandicam.com/guide/settings-video/

OBS Studio
- https://obsproject.com/kb/keyboard-shortcuts
- https://github.com/obsproject/obs-studio/wiki/Keyboard-Shortcuts
- https://github.com/obsproject/obs-studio/discussions/5078
- https://obsproject.com/forum/threads/option-to-highlight-mouse-cursor-and-mouse-clicks.81966/
- https://obsproject.com/forum/threads/how-to-capture-mouse-highlighter-with-window-capture.182382/
- https://www.dacast.com/blog/best-obs-settings/
- https://riverside.com/blog/how-to-record-with-obs
- https://alive-project.com/en/streamer-magazine/article/2044/

ShareX
- https://getsharex.com/docs/region-capture
- https://getsharex.com/blog/how-to-record-screen-windows/
- https://github.com/ShareX/ShareX/issues/4434
- https://tutorialtactic.com/blog/sharex-shortcuts/
- https://jeremysawesome.com/2025/04/11/fixing-sharex-hotkeys-the-print-screen-conflict-with-logi-options/

TechSmith (Camtasia, Snagit)
- https://www.techsmith.com/learn/tutorials/snagit/snagit-hotkeys/
- https://www.techsmith.com/learn/tutorials/snagit/how-to-capture-your-screen/
- https://www.techsmith.com/learn/tutorials/snagit/scrolling-capture/
- https://www.techsmith.com/learn/tutorials/camtasia/camtasia-shortcuts/
- https://assets.techsmith.com/docs/pdf-camtasiastudio/camtasia_studio_8_hotkeys.pdf
- https://techshelps.github.io/CamtasiaStudio/Topics/Edit/Callouts/Keystroke%20Callouts.htm
- https://guides.library.ucsc.edu/DS/Resources/Camtasia

NVIDIA ShadowPlay
- https://www.partitionwizard.com/partitionmagic/nvidia-shadowplay-hotkey.html

Xbox Game Bar
- https://axeetech.com/xbox-game-bar-keybind-every-shortcut-for-windows/
- https://www.ionos.com/digitalguide/server/configuration/screen-capture-windows-11/
- https://www.elevenforum.com/t/change-video-frame-rate-for-game-recording-in-windows-11.18224/
- https://www.elevenforum.com/t/change-video-quality-for-game-recording-in-windows-11.18225/
- https://www.techradar.com/how-to/how-to-use-xbox-game-bar-in-windows-10

Screenpresso
- https://www.screenpresso.com/support/hot-keys/
- https://www.screenpresso.com/support/change-hot-keys/
- https://www.screenpresso.com/support/snippingtool/
- https://www.screenpresso.com/features/

Loom
- https://support.atlassian.com/loom/docs/use-looms-keyboard-shortcuts/
- https://support.atlassian.com/loom/docs/hide-the-recording-controls/
- https://www.loom.com/community/how-to-record-your-first-video
- https://www.loom.com/community/d97180be7d674f4fbf57744365457162-pg

Avalonia / .NET
- https://github.com/AvaloniaUI/Avalonia/discussions/13827
- https://github.com/AvaloniaUI/Avalonia/discussions/15836
- https://github.com/AvaloniaUI/Avalonia/issues/4956
- https://docs.avaloniaui.net/docs/how-to/window-how-to

Tuş göstericiler (açık kaynak)
- https://alternativeto.net/software/keycastow/
- https://alternativeto.net/software/keyviz
