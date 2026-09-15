# Kaydedici Küçük Denetim Şeridi — Saha Ölçüleri, 15 Eylül 2026

Üçüncü tarama. Soru tek: kayıt sürerken ekranda kalan küçük denetim şeridi kaç piksel,
hangi düğmeleri taşıyor, sürüklenebiliyor mu, hep üstte mi. Ölçülemeyen her alan
"bilinmiyor" yazıyor; uydurulan sayı yok.

## 1. LICEcap (justinfrankel/licecap)

C, 5595 yıldız, repoda LICENSE yok — <https://github.com/justinfrankel/licecap>

- Diyalog: `IDD_DIALOG1 DIALOG 0,0,551,335` — `licecap/licecap.rc:29`
- Görüntü alanı `IDC_VIEWRECT` 4,4 → 543×314 DLU; alt şerit ~17 DLU (~30 px)
- Düğmeler: Insert (41×12 DLU), Record (41×12), Stop (30×12) — tek satır
- Şeritte kalanlar: Max FPS, Size (X×Y), durum metni (`640x480 x fps 3:21`)
- Ayarlar ayrı `IDD_OPTIONS` diyaloğunda — `licecap_ui.cpp:768-810`
- Sürükleme: `WM_LBUTTONDOWN` ile pencere taşınıyor — `licecap_ui.cpp:1775-1811`
- Hep üstte: `HWND_TOPMOST` yalnız kayıt sırasında (`:1680`), durunca `HWND_NOTOPMOST` (`:666`)
- Pencere çerçevesi = kayıt alanı; şerit alanın dışına taşmıyor

## 2. Captura (MathewSachin/Captura)

C#/WPF, 10832 yıldız, MIT — <https://github.com/MathewSachin/Captura>

- Ayrı mini kip **yok**. Tek pencere: `MaxWidth="440"`, `SizeToContent="Height"`,
  `CaptionHeight="0"`, `Topmost` ayardan bağlı — `src/Captura/Windows/MainWindow.xaml:1-20`
- Sabit piksel yükseklik yok; içerik boyuna göre otomatik

## 3. Kooha (SeaDve/Kooha)

Rust/GTK4-libadwaita, 3510 yıldız, GPL-3.0 — <https://github.com/SeaDve/Kooha>

- `default-width=220`, `default-height=230`, `resizable=False` — `data/resources/ui/window.ui:6-9`
- Ana sayfa: pencere/monitör seç, bölge, masaüstü sesi, mikrofon, imleç, Record
- Kayıt sırasında **aynı pencere** `recording_page`'e geçiyor: süre etiketi, Stop, Pause/Resume
- Ayrı yüzen çubuk yok; `keep-above` çağrısı kaynakta bulunamadı → bilinmiyor

## 4. Blue Recorder (xlmnxp/blue-recorder)

Rust, 594 yıldız — <https://github.com/xlmnxp/blue-recorder>

- `GtkHeaderBar` içine gömülü Record/Stop, süre etiketi, dosya adı, klasör seçici,
  alan/ekran/pencere toggle'ları — `gui/interfaces/main.ui`
- `resizable=False`, sabit piksel ölçü **yok** (GTK içerik boyuna göre)

## 5. Green Recorder (mhsabbagh/green-recorder — arşiv)

Python/GTK3, 626 yıldız, GPL-3.0 — <https://github.com/mhsabbagh/green-recorder>

- Aynı headerbar modeli, `resizable=False`, sabit piksel yok — `ui/ui.glade:76-90`

## 6. Screenity (alyssaxuu/screenity)

JavaScript, 18692 yıldız, GPL-3.0 — <https://github.com/alyssaxuu/screenity>

- Yüzen çubuk `src/pages/Content/toolbar/layout/ToolbarWrap.jsx`, `react-rnd` ile sürüklenebilir
- **Ölçü:** `height: 48px`, varsayılan `position:absolute; bottom:20px; left:20px`,
  genişlik `fit-content` — `src/pages/Content/toolbar/styles/layout/_Toolbar.scss:194-211`
- Süre etiketi koşullu genişlik: saat varsa 58 px, yoksa 42 px
- Düğmeler: Stop, Draw, Pause/Resume, imleç kipleri (target/highlight/spotlight),
  Restart, Discard, Camera, Blur, Mic
- `z-index: 99999999999999` — sayfa içinde her şeyin üstünde; OS seviyesinde topmost değil

## 7. PowerToys Video Conference Mute (microsoft/PowerToys)

**Özellik kaldırıldı** — v0.88.0'da deprecate edildi, commit `12bb5c2` (16 Ocak 2025).
Silinmeden önceki haliyle okundu.

- Boyut sabit değil: sprite görselinin dörtte biri DPI ile ölçekleniyor —
  `scaledOverlayWidth = image->GetWidth()/4 * dpi/96`, `Toolbar.cpp`
- Köşe payı `BORDER_OFFSET = 12px`, sağ üst köşe `TOP_RIGHT_BORDER_OFFSET = 40px`
- `SetWindowPos(hwnd, HWND_TOPMOST, ...)`, dört köşeye yapışma
- Düğmeler: mikrofon aç/kapa, kamera aç/kapa
- Kullanıcı raporu (ölçü değil): 4K'da 322×44 px — issue #17457

## 8. GPU Screen Recorder

Asıl proje CLI. Bulunan GUI `runlevel5/gpu-screen-recorder-adwaita` (resmi değil,
11 yıldız, GPL-3.0) tam ayarlar penceresi; küçük denetim şeridi yok. Kapsam dışı.

## 9. Kapalı kaynak (ScreenRec, Gyazo GIF, Recordit)

Piksel ölçüsü **bilinmiyor** — kaynak yok. Gyazo GIF'in kayıt sırasında stop/pause/
complete düğmeli küçük çubuk gösterdiği belgeli (<https://help.gyazo.com/>); ölçü ve
renk uydurulmadı.

## 10. Çıkan üç kalıp

- **Pencere = çerçeve** (LICEcap, peek): denetim kayıt alanının kendisine yapışık,
  ayrı yüzen çubuk yok.
- **Headerbar** (Blue/Green Recorder, Kooha): düğmeler başlık çubuğuna gömülü,
  sabit piksel yok, içeriğe göre.
- **Gerçek yüzen çubuk** (Screenity, PowerToys VCM): ayrı, sürüklenebilir,
  `HWND_TOPMOST` / yüksek z-index, köşeye yapışma mantığı taşıyor.

Yükseklik kümesi üç taramanın hepsinde aynı yere düşüyor: **30-64 px**.
