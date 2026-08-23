---
name: vidshrink-arayuz-dogrulama
description: VidShrink WPF arayüzünü ekranda doğrulamanın işe yarayan yolu — computer-use ve UI Automation bu projede çalışmıyor
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
