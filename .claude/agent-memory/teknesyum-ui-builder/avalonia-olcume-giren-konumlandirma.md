---
name: avalonia-olcume-giren-konumlandirma
description: Avalonia'da bir öğeyi Margin ile kaydırmak ölçüme girer ve kabı şişirir; kaydırma çubuğu çıkarır — RenderTransform kullan
metadata:
  type: feedback
---

Bir öğeyi kabının içinde kaydırıyorsan (ayırıcı, imleç, tutamaç) `Margin` kullanma,
`RenderTransform` içinde `TranslateTransform` kullan ve `X`'i güncelle.

**Why:** `Margin` Measure aşamasına girer. `HorizontalAlignment="Left"` bir çocuğa büyük
bir sol kenar boşluğu verdiğinde ızgaranın istediği genişlik o kadar büyür. Kap
`ScrollViewer` ya da `StackPanel` gibi sonsuz genişlik veren bir yerdeyse pencere aşılır,
alt kenarda yatay kaydırma çubuğu belirir ve sağa hizalı öğeler ekran dışına itilir.
T39'da ayırıcı `Split=1`'e getirildiğinde tam bunu yaptı; ekran görüntüsü olmasa
sayılar yeşil görünüyordu çünkü ayırıcının kendi konumu doğruydu.

**How to apply:**

- Sürükleme sırasında her karede yeni `TranslateTransform` üretme; bir alan tut, `X`'ini yaz.
- Uçlarda öğe kabın dışına taşarsa kabında `ClipToBounds="True"` yeterlidir.
- Doğrulamada yalnız öğenin konumunu değil, **kabın kaydırma durumunu da** ölç:
  `scroll.Extent.Width` ile `scroll.Viewport.Width` eşit kalmalı.
