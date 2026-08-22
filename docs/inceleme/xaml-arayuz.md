# XAML Arayüz İncelemesi

Kapsam: `src/VidShrink.App/` altında `MainWindow.xaml`, `App.xaml`, `Themes/Theme.xaml`.
Salt okuma; kaynağa dokunulmadı. Satır numaraları o dosyalara aittir.

**En kritik üç bulgu:** `?` bilgi düğmeleri klavyeyle erişilemez ve 12x12 px ·
devre dışı butonlarda yazı okunmuyor (≈2.5:1) · hata mesajları görünmeyen bir `TextBlock`'a yazılıyor.

## 1. Ne yapıyor

Çerçevesiz pencere (`WindowChrome`, MainWindow:2-4); dış `Border` neon mavi 1 px kenar, 6 px köşe (:5).
Üç satırlık `Grid`: özel başlık çubuğu (:12-26), slogan + TR/EN çipleri (:27-32),
`TabControl` (:33-403) — Shrink, Convert, About.

Shrink ve Convert iki sütunlu `Grid` + `ScrollViewer`; her kart `Panel` stilli `Border`.
Sol sütun ayar, sağ sütun çıktı/ilerleme ve eylem butonları.

Tema iki dosyaya bölünmüş: `Theme.xaml` yalnız renk, font ve 8 tipografi/kap stili taşıyor;
kontrol şablonlarının tamamı (`ComboBox`, `TextBox`, `Slider`, `ScrollBar`, `CheckBox`,
`ProgressBar`, `ToolTip`, buton türevleri) `App.xaml` içinde — temanın adı `Theme.xaml`'de,
gövdesi `App.xaml`'de. `Theme.xaml` hiçbir ölçü, boşluk veya köşe yarıçapı belirteci
tanımlamıyor; 2. maddedeki sızıntının kök nedeni bu.

## 2. Sabit kodlanmış değerler

**Renk.** `#3300F3FF` beş yerde (App:240, 251, 310, 438 · Theme:89),
`#6600F3FF` dört yerde (MainWindow:12 · App:97, 399 · Theme:145),
`#1400F3FF` bir (Theme:145), `#80B026FF` bir (App:21 — `NeonPurple`'ın yarı alfası).
Hepsi `NeonBlue`/`NeonPurple`'ın alfa varyantı; iki fırça kaynağı ile kapanır.
Adlandırılmış renk: `Black` beş kez (App:113, 129, 239, 248 · Theme:98),
`White` bir kez (App:151 — `TextBody` yerine ToolTip yazısı).

**Yazı boyutu (satır içi).** MainWindow:16 `18`, :213 `24`, :220 `24`, :225 `14`, :295 `18`.
:213 ile :220 aynı rolü oynayan, ölçekte karşılığı olmayan tek kullanımlık 24 px.
:225 `Hint`'in 14'ünü stile bağlanmadan tekrar ediyor.

**Köşe yarıçapı.** 20 bildirim, üç dosyaya dağılmış ve tema tarafında karşılığı yok:
`6` on üç kez (MainWindow:3, 5, 12 · App:22, 106, 163, 199, 213, 317 · Theme:107, 145),
`5` dört (App:264, 348, 357, 399), `4` dört (App:235, 273, 401, 429), `8` bir (App:77).

**Kenar boşluğu.** `MainWindow.xaml` içinde 59 satır içi `Margin`/`Padding` (`App.xaml` 3,
`Theme.xaml` 1). Dikey ritim elle kuruluyor: `0,12,0,0` 10 kez, `0,16,0,0` 8 kez,
`0,20,0,8` 7 kez (About başlıkları :385-397), `0,14,0,0` 4 kez. Negatif düzeltmeler ayrıca
var: :146, :152, :156, :168 `6,-2,...` ve :251 `-8,16,-8,0`.

## 3. Erişilebilirlik

1. **`?` düğmeleri klavyeyle açılamıyor.** `InfoButton` stilinde `Focusable="False"`
   (App:69), içerik yalnız `ToolTip` ile veriliyor. Odak alamayan öğede
   `ToolTipService.ShowOnKeyboardFocus` çalışmaz. 27 bilgi düğmesinin tamamı klavye ve
   ekran okuyucu için yok hükmünde.
2. **Hedef boyutu 12x12 px** (App:57-58) — WCAG 2.2 SC 2.5.8 asgarisi 24x24.
   Uygulamanın birincil yardım aracı en küçük tıklama hedefi.
3. **Devre dışı kontrastı yetersiz.** `Opacity 0.3` (App:35 GhostButton, Theme:119
   PrimaryButton). Devre dışı `PrimaryButton`'da siyah yazı / sönük neon mavi ≈ **2.5:1**;
   `BtnStart` (MainWindow:232) ve `BtnConvert` (:373) açılışta devre dışı — kullanıcının
   ilk gördüğü iki buton okunmuyor. `0.45` kullananlar (App:219, 328, 444) ≈4.5:1 ile sınırda.
4. **Odak göstergesi yok.** Üç dosyada tek `FocusVisualStyle` yok, hiçbir şablonda
   `IsKeyboardFocused` tetikleyicisi yok. WPF varsayılanı siyah kesikli çizgidir ve `#121216`
   zeminde görünmez; `CheckBox` (App:421), `TabItem` (Theme:145), `Slider` (App:341)
   ve buton şablonlarının hepsi etkileniyor.
5. **Otomasyon adı yok.** `AutomationProperties` hiç geçmiyor. Küçültme butonunun içeriği
   çıplak bir `Rectangle` (MainWindow:21) — ekran okuyucuya isim vermiyor; büyütme ve kapatma
   `□` / `×` glifiyle okunuyor (:22, :23).
6. **Etiket–kontrol bağı yok.** Tüm etiketler `TextBlock`; `Label`+`Target` ve `AccessKey` yok.
   `CheckBox` şablonu `RecognizesAccessKey="True"` diyor (App:432) ama hiçbir içerikte
   alt çizgi kısayolu tanımlı değil.
7. **Görünmeyen hata kanalı.** `TxtStatusBar` `Visibility="Collapsed"` (MainWindow:404);
   `MainWindow.xaml.cs:176` dosya çözümleme hatasını yalnız buraya yazıyor, kullanıcı hiçbir
   geri bildirim görmüyor. FFmpeg durumu ise sadece About sekmesine yansıyor (aynı dosya :134).
8. Sekme sırası mantıklı: bağlantılar → pencere butonları → TR/EN → sekmeler → içerik.
9. Metin kontrastı iyi tarafta: beyaz/`#121216` ≈17:1, `NeonBlue` ≈13.7:1, `NeonPink` ≈5.8:1.
   Sorun ikincil metnin az kontrastlı olması değil, hiç ayrışmaması — 5.2'ye bakın.

## 4. Düzen dayanıklılığı

1. **Asgari pencere 1040x720 DIP** (MainWindow:1). %150 DPI'de 1560x1080 fiziksel piksel;
   1920x1080 ekranda %150 ölçekte kullanılabilir alan ~1280x700 DIP olduğundan pencere sığmaz.
   %125'te 1300x900 fiziksel — 1366x768 dizüstülerde de sığmaz. WPF Windows metin ölçeğini
   uygulamadığı için risk yazı ölçeğinde değil, DPI'de.
2. **Yatay `StackPanel`'ler kaydırılamıyor.** Üç `ScrollViewer` de yalnız dikey (:35, :239, :380).
   :144-149'daki iki onay kutusu + iki `?` tek satırda duruyor; TR metinleri
   ("Çözünürlüğü Düşürebilir", "Kare Hızını Düşürebilir") EN'den uzun, dar sütunda kırpılır.
   :108-114'teki beş çip için de aynısı geçerli.
3. **Yerelleştirilen `ComboBox`'larda sabit genişlik.** `CmbFillPolicy Width="190"` (:157),
   `CmbHdrPolicy Width="170"` (:164). `MainWindow.xaml.cs:118` görsel ağacı gezip metinleri
   çalışma anında değiştiriyor; "Stay At Quality Ceiling" → "Kalite Tavanında Kal" sığmazsa kesilir.
4. **Slider yanındaki sayı kutusu dar.** :292 sütunu `72` px, içindeki `TxtQuality`
   `FontSize="18"` kalın + `Padding="8,4"` (:295) → yazıya ≈56 px kalıyor. Fixed Bitrate
   modunda beş haneli kbps kırpılır. Shrink tarafındaki eşi `82` px ve 16 px (:100, :105) —
   aynı iş, iki ölçü.
5. **Sütun hizası sihirli sayıyla:** `TargetPanel MinHeight="260"` (:90), Output `254` (:196).
6. Diğer sabitler: başlık yüksekliği `38` iki yerde (:3 `CaptionHeight`, :12 `Height`),
   `Height="150"` (:191, :358), `MaxHeight="100"` (:178), `Grid Height="42"` (:288).

## 5. Tutarsızlık

1. **İki ayrı ipucu biçimi.** 18 ipucu `TipText` stilini kullanıyor (MaxWidth 420,
   LineHeight 21 — App:50-54); 9 tanesi stilsiz ham `TextBlock`: MainWindow:280, 287, 299,
   311, 315, 325, 340, 344, 348. İkinci grup `ToolTip`'in `MaxWidth="460"` sınırına düşüyor
   (App:157), satır yüksekliği de farklı.
2. **Dört renk anahtarı, tek renk.** `TextBody`, `TextDim`, `TextLabel`, `TextHint`
   hepsi `#FFFFFFFF` (Theme:43-46). `Hint` stili (App:177) yalnız punto ile ayrışıyor.
3. **Kullanılmayan kaynak:** `NeonSuccess` (Theme:13), `TextLabel` (:45), `H3` stili (:58) —
   üçü de projede hiç referans almıyor.
4. **Gereksiz `Foreground` tekrarı.** `Label` ve `H2` zaten `NeonBlue` (Theme:55, 69);
   MainWindow:198, 202, 206, 212, 217 aynı fırçayı yeniden atıyor.
5. **`ComboBoxItem` tetikleyicileri iki kez.** Aynı `IsHighlighted`/`IsSelected` kuralları hem
   `ControlTemplate.Triggers` (App:238-241) hem `Style.Triggers` (:245-254) içinde.
6. **Örtük stil sızıntısı.** MainWindow:252-254'teki `UniformGrid.Resources` içindeki
   `TargetType="StackPanel"` örtük stili ızgara hücrelerine değil, o alt ağaçtaki **her**
   `StackPanel`'e uygulanır — :256, :270, :280 gibi iç içe etiket satırları da `8,0,8,18` alıyor.
7. **Kopyalanmış eylem satırı:** MainWindow:230-233 ile :371-374 aynı şablon (Cancel + Primary,
   `Margin="12,0,0,0"`), :227 ile :368 aynı "Show In Folder" butonu. Ortak stil yok.
8. **`ProgressBar` yüksekliği iki değer** — Shrink `5` (:199), Convert `8` (:365).
9. **Stil yerine satır içi tipografi:** :388 `MonoValue` varken elle font veriyor, :225 stilsiz.
10. **Üç katmanlı arka plan:** `Window.Background` (:1) ve iç `Border.Background` (:5) `AppBg`,
    üstüne `WorkspaceBackground` (:6) — ilk iki dolgu yalnız 1 px kenar kadar görünür.
    Başlık çubuğu `Border`'ı da dört köşeden yuvarlatılmış (:12) ama üste yaslı; alt iki köşe
    içerikle arasında çentik bırakıyor.
