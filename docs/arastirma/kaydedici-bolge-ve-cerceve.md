# Kaydedici — Bölge Seçimi ve Kayıt Çerçevesi

Tarih: 13 Eylül 2026. Alt ajan raporu, ham hâliyle.

## Ajana verilen istem

```
VidShrink adlı .NET 8 + Avalonia masaüstü programına ekran kaydedici sekmesi yazıyoruz. Piyasadaki ekran kaydedicilerin kullanım kolaylığı özelliklerini araştırıyoruz. Senin konun iki başlık:

1) BÖLGE SEÇİMİ: fareyle bölge çizme, pencere yakalama (window capture), monitör seçme, sabit oranlar (16:9, 1080p, 720p gibi hazır boyutlar), son kullanılan bölgeyi hatırlama, kenar/köşe tutamaçlarıyla yeniden boyutlama, piksel cinsinden boyut göstergesi, büyüteç (magnifier) ile piksel hassas seçim.

2) KAYIT ÇERÇEVESİ (recording border/frame overlay): Bandicam'in tam ekranda bile üstte duran yeşil/kırmızı çerçevesi nasıl çalışıyor? Yanıp sönme (blinking), renk, kalınlık (piksel), köşe tutamaçları, tıklamayı geçirme (click-through / WS_EX_TRANSPARENT + WS_EX_LAYERED), çerçevenin kayda dahil olup olmaması.

İncelenecek programlar: OBS Studio, Bandicam, Camtasia, ShareX, Snagit, Nvidia ShadowPlay, Xbox Game Bar, ScreenRec, Loom, Screenpresso, Action!, FlashBack.

WEB ARAMASI YAP, tahmin etme. Resmi kılavuz sayfaları, kaynak kod (ShareX GitHub, OBS GitHub) ve ürün dokümantasyonu tercih et. Somut sayı topla: piksel kalınlıkları, varsayılan oranlar, hex renkler, ayar adlarının birebir yazımı.

Her bulgu için ürün adı + davranış + URL ver. En az 60 satır, TÜRKÇE yaz. Hangi özelliğin ücretsiz/açık kaynakta taklit edilebilir olduğunu, hangisinin lisans engeli taşıdığını da not et. Kaynağı olmayan şeyi "kaynak bulunamadı" diye işaretle, uydurma.

Cevabını doğrudan metin olarak dön; dosya yazma.
```

## Ajanın dönüşü

# VidShrink Ekran Kaydedici — Bölge Seçimi ve Kayıt Çerçevesi Araştırması

Aşağıdaki her satır web'den doğrulandı. Kaynağı bulunamayan kalem açıkça öyle işaretli.

---

## 1) BÖLGE SEÇİMİ

### ShareX — açık kaynak, sayıları elde (GPL-3.0)

1. ShareX — `RegionCaptureOptions.cs` varsayılanları: `MinimumSize = 5`, `SnapDistance = 30`, `BackgroundDimStrength = 20`, `FPSLimit = 100`, `InputDelay = 500` — https://raw.githubusercontent.com/ShareX/ShareX/master/ShareX.ScreenCaptureLib/RegionCaptureOptions.cs
2. ShareX — büyüteç: `ShowMagnifier = true`, `MagnifierPixelCount = 15` (tek sayı zorunlu), `MagnifierPixelSize = 10`, `UseSquareMagnifier = false` — aynı dosya
3. ShareX — büyüteç sınırları: `MagnifierPixelCountMinimum = 3`, `…Maximum = 35`, `MagnifierPixelSizeMinimum = 3`, `…Maximum = 30` — aynı dosya
4. ShareX — hazır boyut listesi `SnapSizes` tam beş öğe, hepsi 16:9: **426x240, 640x360, 854x480, 1280x720, 1920x1080** — aynı dosya
5. ShareX — sabit boyut kipi: `IsFixedSize = false`, `FixedSize = (250, 250)` — aynı dosya
6. ShareX — pencere yakalama otomatik algılama: `DetectWindows = true`, `DetectControls = true`; fare hangi pencerenin üstündeyse dikdörtgen ona oturur — aynı dosya
7. ShareX — `QuickCrop = true`, `ShowInfo = true` (konum+boyut yazısı), `ShowCrosshair = false`, `ShowCenterCrosshair = false`, `EnableAnimations = true` — aynı dosya
8. ShareX — nişangâh rengi kodda `Color.FromArgb(125, Color.LightBlue)`, bilgi yazısı Verdana 9pt, metin arka planı 200 alfa, imleç kaçıklığı 10 px — https://raw.githubusercontent.com/ShareX/ShareX/master/ShareX.ScreenCaptureLib/Forms/RegionCaptureForm.cs
9. ShareX — klavye birebir: `Shift` "Proportional resizing", `Alt` "Snap selection to preset sizes", `Space` tam ekran, `1-0` belirli monitör, `~` etkin monitör, fare tekerleği büyüteç boyutu — https://github.com/ShareX/sharex.github.io/blob/master/docs/region-capture.md
10. ShareX — menü adları birebir: "Show position and size info", "Show magnifier", "Square shape magnifier", "Magnifier pixel count", "Magnifier pixel size", "Show screen-wide crosshair", "Fixed size region mode", "Multi region mode" — https://deepwiki.com/ShareX/ShareX/3.1-region-capture
11. ShareX — son bölge kısayol türü olarak var: `HotkeyType.LastRegion`; yanında `CustomRegion`, `ActiveWindow`, `ActiveMonitor`, `RectangleRegion` — https://raw.githubusercontent.com/ShareX/ShareX/master/ShareX/Enums.cs
12. ShareX — kayıt tarafında `ScreenRecorderCustomRegion` ve `ScreenRecorderActiveWindow` var ama **video kaydı için "last region" yok**, açık istek — https://github.com/sharex/sharex/issues/8038
13. ShareX — yedi kip: Default, Annotation, Editor, TaskEditor, OneClick, ScreenColorPicker, **Ruler** (cetvel bindirmesi) — https://deepwiki.com/ShareX/ShareX/3.1-region-capture
14. ShareX — tutamaç (resize node) piksel ölçüsü: **kaynak bulunamadı**; yalnız `UseLightResizeNodes = false` ayarı görünüyor

### Bandicam — tescilli, davranış belgelenmiş

15. Bandicam — "Rectangle on a screen" kipi; `F12` kayıt, `F11` görüntü — https://www.bandicam.com/support/configuration/rectangle_window/
16. Bandicam — varsayılan dikdörtgen **3.4.1'de 640x480 → 1280x720** oldu — https://www.bandicam.com/downloads/version_history/
17. Bandicam — hazır boyut listesi sürümlerle oynadı: 3.4.0'da 1920x1080 eklendi, 4.1.4'te 854x480 → **848x480** ve 480x270 → **480x268** düzeltildi, 4.6.2'de 480x268 ve 480x360 silindi — aynı changelog
18. Bandicam — **dikey oranlar 5.1.1'de** geldi: 9:16 için **608x1080** ve **720x1280** — aynı changelog
19. Bandicam — son bölgeyi hatırlama **5.2.1**'de: "The recently used size and position will be stored in the 'Recently used size' menu." — aynı changelog
20. Bandicam — kullanıcı kendi hazır boyutunu ekler: `Shift` basılıyken kayıt penceresine sağ tık, o anki boyut+konum listeye yazılır — https://www.bandicam.com/support/configuration/rectangle_window/
21. Bandicam — "Find window" düğmesi hedef pencereyi otomatik seçer (1.8.0'da eklendi, 4.1.6'da `ESC` iptali, 4.2.0'da "recording area selection" olarak geliştirildi) — changelog
22. Bandicam — **büyüteç 4.2.0'da** eklendi ("Added a magnifier function when adjusting the size of the rectangle window"), **6.2.1'de boyutu ve ölçeği 1.6x büyütüldü** — changelog
23. Bandicam — `Shift` basılı yeniden boyutlama en-boy oranını korur — https://www.bandicam.com/support/configuration/rectangle_window/

### TechSmith (Snagit / Camtasia) — tescilli

24. Snagit — seçim özelliklerinde "Fixed region" seçeneği + piksel "Width"/"Height" alanları — https://support.techsmith.com/hc/en-us/articles/360031128032-Capture-the-Same-Part-of-the-Screen-Using-Fixed-Capture-Video
25. Snagit — "Repeat last capture" varsayılan kısayolu **Ctrl+Shift+R**; son bölgeyi hatırlamanın tescilli karşılığı bu + Preset ikilisi — https://www.techsmith.com/learn/tutorials/snagit/change-global-hotkey/
26. Snagit — büyüteç piksel hassasiyeti için; **`M` tuşu** büyüteci gizler/gösterir — https://www.techsmith.com/learn/tutorials/snagit/image-capture/
27. Snagit — `Ctrl` basılı sürükleme seçimi 16:9'a kilitler; videoda özel ölçü / 4:3 / 16:9 seçilebilir — https://www.techsmith.com/learn/tutorials/snagit/common-captures/
28. Camtasia — "Custom" açılırında Standard veya Widescreen hazır ölçü listesi; Width/Height yanındaki **kilit simgesi** oranı sabitler — https://techshelps.github.io/CamtasiaStudio/Topics/Record/Record%20at%20Standard%20or%20Widescreen%20Dimensions.htm
29. Camtasia — resmi olarak önerilen ölçüler **1920x1080 (1080pHD)** ve **1280x720 (720pHD)** — https://www.techsmith.com/learn/tutorials/camtasia/crisp-clear-screen-video/
30. Camtasia — "Lock to Application": kayıt alanı pencereye kilitlenir, alan değişince pencere de yeniden boyutlanır — https://techshelps.github.io/CamtasiaStudio/Topics/Record/Lock%20Recording%20Area%20to%20Window%20or%20Application.htm
31. Camtasia — açılır listedeki **birebir preset değerleri: kaynak bulunamadı** (yardım metni değerleri yazmıyor, ekran görüntüsüne gömülü)

### OBS Studio — bölge seçimi yok, kırpma var (GPL-2.0)

32. OBS — pencere yakalama yöntemi enum'u `METHOD_AUTO = 0`, `METHOD_BITBLT = 1`, `METHOD_WGC = 2`; arayüzde "Automatic", "BitBlt", **"Windows 10 (1903 and up)"** — https://github.com/obsproject/obs-studio/blob/master/plugins/win-capture/window-capture.c
33. OBS — özellik anahtarları birebir: `"window"`, `"method"`, `"priority"`, `"client_area"`, `"cursor"`; eşleme önceliği `WINDOW_PRIORITY_TITLE / CLASS / EXE` — aynı dosya
34. OBS — bazı pencere sınıfları zorla WGC'ye düşer: `Chrome`, `Mozilla`, `ApplicationFrameWindow`, `Windows.UI.Core.CoreWindow`, `WinUIDesktopWin32WindowClass`, `XLMAIN`, `PPTFrameClass`, `OpusApp`, `rctrl_renwnd32`, `SDL_app` — aynı dosya
35. OBS — **fareyle bölge çizme yok**; bölge `Alt` + tutamaç sürüklemesiyle kırpılarak elde edilir, kırpılan kenar yeşile döner; sayısal giriş `Ctrl+E` Edit Transform'daki Crop alanları — https://obsproject.com/forum/threads/how-to-record-only-a-specific-part-of-the-screen-instead-of-the-whole-screen.167264/

### Diğerleri

36. Loom — "Custom Size" kipi; kutu ayarlanırken **sağ alt köşede birebir piksel ölçüsü** gösterilir — https://support.atlassian.com/loom/docs/record-a-custom-size-video/
37. Loom — en küçük özel boyut **251 x 251 piksel**; alan 4K'yı taşımıyorsa çözünürlük otomatik düşer. Özellik **yalnız Business / Business+ AI / Education / Enterprise planlarında** — aynı URL → **lisans engeli**
38. Screenpresso — fare yavaşlayınca büyüteç belirir, ölçüleri gösterir; **`M` tuşuna üst üste basmak zum katsayısını değiştirir**; tekerlekle de zum — https://www.screenpresso.com/docs/ScreenpressoHelp.pdf
39. Screenpresso — "Capture last region" ayrı bir yakalama kipi olarak var — https://www.screenpresso.com/releases/screenpresso-2-0-0/
40. Action! (Mirillis) — "Active desktop region/area" kipi; ok tuşları bölgeyi taşır, **`Shift`+ok yeniden boyutlar**, `0` sıfırlar — https://mirillis.com/active-screen-region-recording-action-tutorial
41. FlashBack — üç kip: Fullscreen / Region / Window; region'da kenar tutamaklarından çekilerek boyutlanır. **Pencere kipinde kayıt sırasında pencere yeniden boyutlanırsa video boyutu değişmez** — https://www.techradar.com/reviews/flashback-express
42. FlashBack — hazır boyut listesi, son bölge hatırlama, büyüteç: **kaynak bulunamadı**
43. ScreenRec — **Alt + S** ile alan seçimi, sonra fotoğraf/video düğmesi — https://screenrec.com/screenrec-quickstart-guide-for-windows-xp-vista-7-8-10/
44. ScreenRec — sabit oran / hazır boyut / tutamaç davranışı: **kaynak bulunamadı**
45. Nvidia ShadowPlay — **bölge seçimi yok**; tam ekranda NVFBC, tek pencerede NVIFR; "Allow Desktop capture" gizlilik tercihi var — https://en.wikipedia.org/wiki/Nvidia_ShadowPlay
46. Xbox Game Bar — **bölge seçimi yok**, sürükle-seç arayüzü yok; sadece etkin pencere. Masaüstü ve Dosya Gezgini kaydedilemez ("Gaming features aren't available for the Windows desktop or File Explorer") — https://learn.microsoft.com/en-us/answers/questions/5495177/gamebar-gaming-features-arent-availiable-for-windo

---

## 2) KAYIT ÇERÇEVESİ (border / overlay)

### Bandicam — asıl sorunun cevabı

47. Bandicam — çerçeve `Ctrl+Alt+H` ile gizlenir ("Show/Hide" menüsü) — https://www.bandicam.com/how-to-hide-bandicam-interface/
48. Bandicam — kayda dahil olmama, birebir: **"If you check the 'Exclude Bandicam windows from recording' option, the Bandicam windows will be visible during recording, but not in the recorded video."** — aynı URL. Bu seçenek **7.1.0 (19.02.2024)** ile geldi — https://www.bandicam.com/forum/viewtopic.php?f=8&p=29520
49. Bandicam — daha eski çözüm: 4.5.6 changelog'u birebir **"Less of the red-colored rectangle window will be recorded than before when using the enhanced capture method."** → yani kırmızı çerçeve tarihsel olarak **kayda karışıyordu**; 'Use enhanced capture method' kapatılarak azaltılıyordu — https://www.bandicam.com/downloads/version_history/
50. Bandicam — **yeşil→kırmızı ikilisi çerçevenin değil FPS bindirmesinin rengi**: "The green number (FPS) indicates the frame rate of the targeted screen", kayıt başlayınca kırmızıya döner — https://www.bandicam.com/faqs/no_fps_frames_per_second_number/. Aynı yeşil→kırmızı tepsi ikonunda da var (v1.7.0 changelog).
51. Bandicam — **"Show the blinking rectangle" adında bir ayar: kaynak bulunamadı.** Resmi yardım, FPS ve General ayar sayfaları ile forum aramalarında böyle bir dize geçmiyor. Yanıp sönme Bandicam'de belgelenmiş bir özellik değil.
52. Bandicam — çerçevenin **hex rengi ve piksel kalınlığı: kaynak bulunamadı**
53. Bandicam — FPS sekmesi ayar adları birebir: "Show FPS Overlay", "Show/Hide Hotkey", "Position Hotkey", "Only when capturing" — https://www.bandicam.com/support/settings/fps/
54. Bandicam — General sekmesinde "Bandicam window always on top", "Show FPS in captured video/image", "Show tip/information bar" — https://www.bandicam.com/guide/settings-general/
55. Bandicam — v1.7.2 changelog: "Added 'Always on top' option in the rectangle window" — changelog
56. Bandicam — tam ekran oyunda üstte durması **Win32 topmost değil, DirectX/OpenGL/Vulkan kancası**; bindirme oyunun kendi sunum zincirine çizilir, bu yüzden exclusive fullscreen'de de görünür — https://www.bandicam.com/guide/record-game/. Bağımsız kanıt: kancaya çizim yapan tersine mühendislik örneği — https://github.com/altoid29/bandicam-overlay-hook

### ShareX — çerçevenin gerçek sayıları (iki dal farklı)

57. ShareX `master` (WinForms `ScreenRecordForm.cs`) — çerçeve rengi **`Color.Red` (bekleme) → `Color.Lime` (#00FF00, kayıt) → `Color.FromArgb(241,196,27)` (#F1C41B, duraklatma)**; `new Pen(borderColor)` yani **1 px** — https://raw.githubusercontent.com/ShareX/ShareX/master/ShareX.ScreenCaptureLib/Forms/ScreenRecordForm.cs
58. ShareX `master` — pencere stili `WS_EX_TOPMOST | WS_EX_TOOLWINDOW`; **click-through uygulanmamış**; çerçeve kayda girmesin diye kayıt dikdörtgeni **her yönden 1 px içeri** alınıyor: `new Rectangle(windowLocation.X + 1, windowLocation.Y + 1, borderRectangle.Width - 2, borderRectangle.Height - 2)`; `panelOffset = 3` — aynı dosya
59. ShareX `develop` (Avalonia `RecordingRegionBorder.cs`) — kesikli "marching ants": arka kalem `new Pen(new SolidColorBrush(Color.FromArgb(220,20,20,20)), 1, new DashStyle([5,5], 0))`, vurgu kalemi `new DashStyle([5,5], 5)`. **Yanıp sönme zamanlayıcısı yok, köşe tutamağı çizmiyor** — https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/Presentation/ScreenRecording/RecordingRegionBorder.cs
60. ShareX `develop` — sabitler birebir: `private const int BorderPixels = 1;`, `private const int ToolbarGapPixels = 3;`; vurgu rengi `AccentBrush="Lime"`, sınıf varsayılanı `Brushes.Goldenrod` — https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/Presentation/ScreenRecording/ScreenRecordWindow.axaml.cs
61. ShareX `develop` — ortayı **gerçekten deliyor**: `CreateRectRgn` + `CombineRgn` + `SetWindowRgn` + `DeleteObject`; ayrıca `info.ExStyle |= WindowStyles.WS_EX_TOOLWINDOW` — aynı dosya
62. ShareX `develop` — tam tıklama geçirgenliği ayrı bir yerde: `ApplyClickThroughToolWindowStyle()` içinde `info.ExStyle |= WindowStyles.WS_EX_TRANSPARENT | WindowStyles.WS_EX_TOOLWINDOW;` — https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/Presentation/ScrollingCapture/ScrollingCaptureRegionWindow.axaml.cs

### Win32 mekaniği (VidShrink için doğrudan reçete)

63. `WS_EX_LAYERED = 0x00080000`, `WS_EX_TRANSPARENT = 0x00000020`, `WS_EX_TOOLWINDOW = 0x00000080`, `WS_EX_NOACTIVATE = 0x08000000`, `WS_EX_TOPMOST = 0x00000008` — https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
64. Tıklama geçirme kuralı birebir: "if the layered window has the **WS_EX_TRANSPARENT** extended window style, the shape of the layered window will be ignored and the mouse events will be passed to other windows underneath" — https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
65. `SetLayeredWindowAttributes(hwnd, crKey, bAlpha, dwFlags)` — `LWA_COLORKEY = 0x1`, `LWA_ALPHA = 0x2` (0 tam saydam, 255 opak) — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes
66. `SetWindowPos` ile topmost: `HWND_TOPMOST = (HWND)-1`; stil değişiminden sonra `SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_FRAMECHANGED` şart — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
67. Alternatif: `WM_NCHITTEST` (0x0084) → `HTTRANSPARENT (-1)`. Ama belge sınırı net: yalnız **aynı iş parçacığındaki** pencereye zincirlenir; süreçler arası geçirgenlik için `WS_EX_TRANSPARENT` gerekir — https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest
68. **Çerçeveyi kayıttan hariç tutma**: `SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE = 0x00000011)`. Belge birebir: "One use for this affinity is for windows that show video recording controls, so that the controls are not included in the capture." **Windows 10 sürüm 2004'te geldi**; daha eskisinde `WDA_MONITOR (0x1)` gibi davranır (pencere kaybolmaz, içeriği boşalır). `WDA_NONE = 0x0` — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
69. Ortayı delme: `CreateRectRgn` + `CombineRgn (RGN_DIFF = 4)` + `SetWindowRgn` — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn

### Diğer ürünlerde çerçeve

70. OBS — ayar adı birebir `Basic.Settings.General.HideOBSWindowsFromCapture` = **"Hide OBS windows from screen capture"**, Settings → General — https://github.com/obsproject/obs-studio/blob/master/frontend/data/locale/en-US.ini; uygulaması `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` — https://github.com/obsproject/obs-studio/pull/5698
71. OBS — projektör pencerelerine uygulanmaz (kullanıcılar onları başka uygulamayla yakalıyor); bağlam menüleri ve combo açılırları gizlenemiyor — aynı PR
72. OBS — **kayıt alanı çerçevesi yok**; kullanıcılar forumda istiyor, yerleşik çözüm sunulmuyor — https://obsproject.com/forum/threads/how-to-show-a-border-around-the-desktop-recording-area.139286/
73. OBS / WGC — yakalanan pencerenin etrafındaki **sarı kenarlığı Windows çiziyor**. Kapatmak için `GraphicsCaptureSession.IsBorderRequired = false`, ama önce `GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)` ile kullanıcı onayı ve manifest'te **`graphicsCaptureWithoutBorder`** yeteneği şart; kullanıcı reddederse atama başarılı görünür ama yok sayılır. Win10 build **10.0.20348** (sürüm 2104) — https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.isborderrequired
74. OBS — bu kenarlığı gizleme isteği **"Closed as not planned"** ile kapandı — https://github.com/obsproject/obs-studio/issues/8159. OBS 32.1 sürüm notlarında "Capture Border" geçmiyor (**doğrulanamadı**) — https://obsproject.com/blog/obs-studio-32-1-release-notes
75. Camtasia — kayıt alanının çevresinde **yeşil kesikli çizgi**; içteki pusula simgesiyle taşınır, köşe tutamağıyla boyutlanır — https://www.techsmith.com/learn/tutorials/camtasia/camtasia-recorder/. Çerçevenin kayda girip girmediğine dair birebir cümle: **kaynak bulunamadı**
76. Snagit — fare pencere üstüne gelince **turuncu kesikli çerçeve** belirir; seçim nişangâhları da turuncu — https://www.techsmith.com/learn/tutorials/snagit/image-capture/. Video araç çubuğu için birebir: "The Video Recording toolbar will be hidden in the final video if the capture preference to hide Snagit is selected." — https://www.techsmith.com/learn/tutorials/snagit/how-to-capture-video/
77. Action! — üç durumlu çerçeve, birebir: **yeşil** "region is accepted and Action! is ready to start recording", **gri** "current region cannot be recorded" (iki masaüstüne taşan bölge), **kırmızı** "recording is in progress". HUD varsayılan olarak kayda **dahil edilmez**, ayarla açılabilir — https://mirillis.com/active-screen-region-recording-action-tutorial. Piksel kalınlığı/hex: **kaynak bulunamadı**
78. Screenpresso — çerçeve yerine **alan dışını karartma**; ayardan kapatılabilir — https://www.screenpresso.com/support/dimmed-screen/
79. FlashBack — pencere kipinde seçilen pencerenin etrafında **kırmızı çerçeve**; renk/kalınlık belgelenmemiş — https://www.techradar.com/reviews/flashback-express
80. Xbox Game Bar — kayıt alanı çerçevesi çizmez; süre+mikrofon gösteren küçük kayıt çubuğu durur, `Win+Alt+B` ile gizlenir — https://learn.microsoft.com/en-us/answers/questions/4229393/disable-game-recording-icon-in-game-bar. Windows 11'de göstergenin kayda karışması sık bildirilen sorun — https://learn.microsoft.com/en-us/answers/questions/5655019/how-to-fix-the-recording-on-xbox-game-bar
81. ScreenRec ve Loom — ekran üstü kayıt alanı çerçevesi için resmî belge: **kaynak bulunamadı** (Loom'da belgelenen göstergeler mavi ses göstergesi ve iOS kırmızı üst çubuğu)

---

## Avalonia / .NET 8 ile taklit edilebilirlik

82. **Kanıt ShareX'in kendisi**: `develop` dalı Avalonia kullanıyor, çerçeveyi `Control.Render` içinde `Pen` + `DashStyle` ile çiziyor, HWND'yi `TryGetPlatformHandle()?.Handle` ile alıp Win32 stillerini elle basıyor. VidShrink'te birebir aynı yol izlenebilir.
83. Pencere kurulumu: `SystemDecorations=None`, `Background=Transparent`, `TransparencyLevelHint=Transparent`, `Topmost=true`, `ShowInTaskbar=false`; sonra P/Invoke.
84. Gereken `user32.dll` çağrıları: `GetWindowLongPtr`/`SetWindowLongPtr` (`GWL_EXSTYLE = -20`) → `WS_EX_LAYERED|WS_EX_TRANSPARENT|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`; `SetLayeredWindowAttributes`; `SetWindowPos(..., HWND_TOPMOST, ... SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE|SWP_FRAMECHANGED)`; `SetWindowDisplayAffinity(hwnd, 0x11)`; `SetWindowRgn`. `gdi32.dll`: `CreateRectRgn`, `CombineRgn`, `DeleteObject`.
85. .NET 8'de hepsi `[LibraryImport("user32.dll", SetLastError = true)]` ile kaynak üreteçli (AOT uyumlu) yazılabilir; `SetWindowLongPtr` için 32/64 bit ayrımı gerekir.
86. **Yanıp sönmenin Win32 karşılığı yok** — tamamen çizim işi. `DispatcherTimer` ile `DashStyle.Offset` artarsa "marching ants", renk/opaklık değişirse blink. ShareX statik çiziyor.
87. **Uyarı — WDA_EXCLUDEFROMCAPTURE'ın bedeli**: pencere hiçbir yakalayıcıya görünmez (Teams, uzak masaüstü, ekran görüntüsü dahil) ve Win10 2004 altında `WDA_MONITOR` gibi davranır → sürüm kontrolü şart.
88. **Uyarı — exclusive fullscreen**: katmanlı bir Win32 penceresi exclusive fullscreen D3D uygulamasının üstünde görünmez. Bandicam'inki gibi bir bindirme ancak hedef sürecin sunum zincirine kancalanarak çizilir; Avalonia/.NET ile makul yol bunu denemek değil, kullanıcıyı borderless windowed'a yönlendirmektir.

### Lisans tablosu

89. **Açık kaynak, okunabilir ama bulaşıcı**: ShareX **GPL-3.0**, OBS **GPL-2.0**. Kod kopyalanırsa VidShrink da aynı lisansa geçer. Davranışı okuyup kendi kodunu yazmak (clean-room) engelli değil — bu araştırmadaki sayılar (SnapSizes beşlisi, 30 px yapışma, 5 px asgari, `BorderPixels = 1`, 1 px içeri alma hilesi) bu şekilde kullanılabilir.
90. **Yalnız gözlemle taklit**: Bandicam'ın hazır boyut menüsü ve "Recently used size", Snagit'in "Fixed region" + Preset kısayolu, Camtasia'nın "Lock to Application", Screenpresso'nun `M` ile kademeli zum. Arayüz davranışı serbest; ikon/metin/ekran görüntüsü kopyalamak değil.
91. **Lisans engelli tek somut kalem**: Loom'un "Custom Size" bölge kaydı ücretli plana kilitli — özellik fikri değil ama ürünün kendisi taklit edilemez referans.
92. **Win32 API'lerinin hepsi ücretsiz ve belgelenmiş**; tek istisna `graphicsCaptureWithoutBorder` yeteneği — **paketli (MSIX) uygulama manifesti gerektirir**, paketsiz bir Avalonia exe'si bu yeteneği bildiremez, dolayısıyla WGC'nin sarı kenarlığı VidShrink'te kapatılamaz.
