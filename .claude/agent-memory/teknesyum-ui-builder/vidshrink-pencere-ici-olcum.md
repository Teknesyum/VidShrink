---
name: vidshrink-pencere-ici-olcum
description: VidShrink'te düzen ve tıklama kanıtı için UIA/PrintWindow yerine gerçek pencereyi süreç içinde açan geçici bir ölçüm exe'si kullan
metadata:
  type: project
---

VidShrink'te "şu öğe nerede duruyor, oraya tıklayınca ne oluyor" sorusunu ekran görüntüsü
üzerinden değil, gerçek pencereyi süreç içinde açarak ölç. Depo dışında bir `WinExe`
kur, `VidShrink.App.csproj`'a başvur ve şunu yap:

```csharp
AppBuilder.Configure<VidShrink.App.App>().UsePlatformDetect().SetupWithoutStarting();
var window = new VidShrink.App.MainWindow();
window.Show();
DispatcherTimer.RunOnce(() => Measure(window), TimeSpan.FromSeconds(3));
Dispatcher.UIThread.MainLoop(CancellationToken.None);
```

`SetupWithoutStarting()` `IClassicDesktopStyleApplicationLifetime` kurmaz, bu yüzden
`App.OnFrameworkInitializationCompleted` kendi penceresini açmaz — çakışma olmaz.

Ölçüm araçları:

- **Konum:** `control.TranslatePoint(new Point(w, 0), window)` — pencereye göre kenar.
- **Tıklama kanıtı:** `window.InputHitTest(point)` isabet eden görseli verir; görsel
  ağaçta yukarı yürüyüp `Button`'a çık. Sonra `RaiseEvent(new RoutedEventArgs(Button.ClickEvent))`
  ve `window.Closing`'i geçici dinleyip `e.Cancel = true` yaparak kapatmanın tetiklendiğini
  kanıtla. Bu, "köşeye tıklayınca kapanıyor" iddiasının gerçek kanıtıdır.
- **Yakınlaştırılmış görüntü:** `RenderTargetBitmap` ile denetimi 3 kat DPI'da çiz
  (`new Vector(288, 288)`), `bitmap.Render(control)`, `bitmap.Save(path)`. Pencereyi öne
  almaya, `SetForegroundWindow`'a, `PrintWindow`'a gerek yok — bkz. [[vidshrink-arayuz-dogrulama]].
  Balon `TextBlock`'unu tek başına çizmeden önce `Measure` + `Arrange` çağır, yoksa ölçüsü sıfırdır.

**Why:** T29'da köşe farkının sayıyla 0 olduğunu ve sağ üst pikselin kapatmayı tetiklediğini
göstermek gerekiyordu. UIA `BoundingRectangle` pencere ön planda değilken 0 dönüyor ve öne
alma bu makinede kararsız; süreç içi ölçüm bunların hiçbirine takılmıyor ve ondalık hassasiyet
veriyor.

**How to apply:** `owns` listesi genelde `tests/` içermez, bu araç da zaten depoya girmemeli —
scratchpad'te kur, çıktıyı rapora yaz. `Start-Process -Wait` ile koştur: `WinExe` olduğu için
PowerShell doğrudan çağrıda beklemez ve çıktı dosyası henüz yokken okumaya kalkarsın.

İlgili: [[avalonia-tema-tuzaklari]], [[vidshrink-metin-olcumu]]
