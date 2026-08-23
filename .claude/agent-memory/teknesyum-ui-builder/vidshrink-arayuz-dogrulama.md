---
name: vidshrink-arayuz-dogrulama
description: VidShrink arayüzünü ekranda doğrulama — Avalonia'da UI Automation çalışıyor, computer-use çalışmıyor, PrintWindow yakalama kalıbı
metadata:
  type: project
---

VidShrink'in WPF penceresini doğrularken iki yol kapalı, bir yol açık.

**Kapalı 1 — computer-use.** `request_access` "VidShrink" ve "VidShrink.App" adlarını
tanımıyor; uygulama Start menüsünde kayıtlı olmadığı için izin diyalogu hiç gösterilmiyor.

**Kapalı 2 — UI Automation.** Pencerenin altında yalnızca 23 öğe görünüyor: başlık çubuğu,
`BtnTr`, `BtnEn`, `BtnMaximize` ve üç `TabItem`. Sekme içeriği (`ChkFastGpu`,
`TxtPlanSummary`, `TxtCommand` ...) ağaca hiç girmiyor; `SelectionItemPattern.Select()`
çağırmak da değiştirmiyor.

**Açık yol.** PowerShell + P/Invoke:

- Yakalama: `PrintWindow(hwnd, hdc, 2)`. `Graphics.CopyFromScreen` kullanma — pencere
  arkada kalırsa üstteki uygulamanın içeriğini yakalar ve fark etmezsin.
- Pencere dikdörtgeni: `DwmGetWindowAttribute(hwnd, 9, ...)`, `GetWindowRect` değil.
- Tıklama: `SetCursorPos` + `mouse_event`. `SetForegroundWindow` sık başarısız oluyor;
  tıklamadan önce `GetForegroundWindow` ile doğrula, eşleşmiyorsa tıklama.
- Dil değiştirme UIA ile yapılabiliyor: `BtnEn` / `BtnTr` üzerinde `InvokePattern`.
- Kaynak dosya vermek için uygulamayı komut satırı argümanıyla başlat; `Loaded` içinde
  ilk var olan dosyayı yüklüyor.

**Why:** T8'de ekran doğrulaması sözleşmenin kabul kriteriydi ve iki araç da sessizce
yanlış sonuç verdi — `CopyFromScreen` bir kez başka uygulamanın penceresini kaydetti.

**How to apply:** Bu depoda arayüz işi teslim ederken doğrudan PrintWindow betiğine geç,
computer-use ve UIA denemesiyle tur harcama.

**Not (2026-08-22):** Arayüz T11'de Avalonia'ya taşındı. Yukarıdaki UIA gözlemi WPF penceresine aitti; Avalonia kendi otomasyon ağacını kuruyor, T12 bittikten sonra yeniden denenmeli. PrintWindow yolu her iki durumda da çalışıyor.

**Not (2026-08-23, T16 — Avalonia yakalama betiğinin üç tuzağı):**
- `Process.MainWindowHandle` boş dönüyor ve exe konsol penceresi de açıyor. `EnumWindows`
  ile pid'e göre ara, **pencere başlığının `VidShrink` olmasını şart koş** — yoksa konsol
  penceresini yakalarsın.
- `PrintWindow` bitmap'i `GetWindowRect` ölçüsünde olmalı, `DwmGetWindowAttribute(9)`
  ölçüsünde değil. Büyütülmüş pencerede ikisi 8 piksel kayıyor; DWM ölçüsüyle bitmap
  açarsan görüntü sola kayar ve sağ kenar kesik görünür — kırpmayı DWM dikdörtgeninin
  pencere dikdörtgenine göre farkıyla yap.
- Uygulama çalışırken `dotnet build` MSB3021 ile düşüyor. Yakalamadan önce
  `Get-Process VidShrink.App | Stop-Process -Force`.

**Not (2026-08-23, T21 — Avalonia'da UIA artık tam çalışıyor):** WPF'teki 23 öğelik
kısır ağaç geçti; Avalonia penceresinde 168 öğe görünüyor ve arayüzün tamamı sürülebilir.
İşleyen kalıp: pid'e göre pencereyi bul, `AutomationIdProperty` ile `x:Name` üzerinden
öğeyi al, `InvokePattern` ile düğmeye bas, `TogglePattern` ile onay kutusu, `ValuePattern`
ile `TxtTarget`, `RangeValuePattern` ile `SliderTarget`. `TextBlock` metni UIA'da `Name`
olarak okunuyor, yani ekrandaki dinamik metni doğrulamak için ekran görüntüsü şart değil.

Üç tuzak:
- `ChkFastGpu` açılışta `IsEnabled=False`; donanım taraması bitene kadar bekle, yoksa
  `Toggle()` özel durum atar. 12 saniye yetmiyor, `IsEnabled` olana kadar dönerek bekle.
- `TxtTarget` üzerinde `ValuePattern.SetValue` metni değiştirmek yerine araya sokuyor
  ("100" → "dü100"). Hedefi hazır çip düğmesiyle (`Name` = "8"/"25"/"100") ya da
  `SliderTarget` üzerinde `RangeValuePattern.SetValue` ile ayarla.
- `GetWindowTextW` P/Invoke bildirimine `CharSet = CharSet.Unicode` yazmazsan başlık tek
  harf ("V") dönüyor ve pencere eşleşmesi sessizce başarısız oluyor.
**Not (2026-08-23, T22 — katmanlı Win32 penceresi):** Başlatıcının bekleme paneli
`UpdateLayeredWindow` ile çiziliyor. `PrintWindow` katmanlı pencerede boş dönüyor;
burada tek çalışan yol tam ekran `CopyFromScreen`. Panel `WS_EX_TOPMOST` olduğu için
üstteki uygulama sorunu da çıkmıyor. Panel Launcher'da yaşadığı için doğrulaması,
`Splash.cs`'i bağlayan ve üretilen PNG'yi gömen küçük bir deneme projesiyle yapıldı —
uygulamayı hiç açmadan.

**Not (2026-08-23, T25 — ipucu balonu yakalama):** Avalonia ipucu balonu ayrı bir üst düzey
pencere. `PrintWindow` ana pencerede balonu göstermez; balonun kendi tutamağını
`EnumWindows` ile pid altında ana pencere dışındaki görünür pencere olarak bul ve onu
`PrintWindow` ile yakala. Tam ekran `CopyFromScreen` kullanma: `SetForegroundWindow` sessizce
başarısız oluyor ve kullanıcının tarayıcısını kaydediyor. Ön plana almak için ALT tuşunu
`keybd_event` ile basıp bırakmak `SetForegroundWindow` kilidini açıyor — bu yol çalışıyor.
Balon hizasını görüntüyle değil sayıyla kanıtla: düğmenin ve balonun `GetWindowRect`
değerlerini yazdır.
