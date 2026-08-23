---
name: win32-panel-dogrulama
description: VidShrink başlatıcısının çıplak Win32 bekleme panelini görmenin yolu — Splash.cs'i bağlayan geçici bir konsol projesi
metadata:
  type: project
---

Başlatıcının bekleme paneli (`src/VidShrink.Launcher/Splash.cs`) çıplak Win32 ve
`UpdateLayeredWindow` kullanıyor. `VidShrink.exe`'yi doğrudan çalıştırarak göremezsin:
yapı klasöründe `app/VidShrink.App.exe` olmadığı için program panele hiç gelmeden uyarı
kutusuyla çıkıyor.

Çalışan yol: scratchpad'de `net8.0-windows` bir `WinExe` projesi aç, `Splash.cs`'i
`<Compile Include=... Link=...>` ile bağla, `obj/splash.png`'yi
`LogicalName="VidShrink.Launcher.splash.png"` ile göm. Aynı derlemeye girdiği için
`internal` olan `SplashWindow` ve `SplashArt`'a erişebilirsin; `Render(...)` çağırıp
`Graphics.CopyFromScreen` ile pencerenin ekran bölgesini yakala (pencere ekranın
ortasında, boyutu `SplashArt.Instance.Width/Height`).

İki tuzak: `<UseWindowsForms>true</UseWindowsForms>` System.Drawing'i çevrimdışı getirir
ama örtük `System.Windows.Forms` using'i `Timer`'ı belirsizleştirir —
`<Using Remove="System.Windows.Forms" />` ekle. `WinExe` olduğu için konsola bir şey
yazmaz; çıktı dosyalarını ayrı bir çağrıda listele.

**How to apply:** Panelin görünüşünü değiştiren her sözleşmede kanıt bu şekilde alınır.
Bkz. [[vidshrink-arayuz-dogrulama]].
