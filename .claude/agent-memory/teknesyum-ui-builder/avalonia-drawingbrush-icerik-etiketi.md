---
name: avalonia-drawingbrush-icerik-etiketi
description: Avalonia XAML'de DrawingBrush ve DrawingGroup içeriğini örtük yazma; derleyici AVLN2000 ile düşer
metadata:
  type: feedback
---

Avalonia 11'de `DrawingBrush` ve `DrawingGroup` içeriğini örtük çocuk olarak yazma;
`<DrawingBrush.Drawing>` ve `<DrawingGroup.Children>` etiketlerini açıkça yaz.

**Why:** Örtük yazımda derleme `AVLN2000: Internal compiler error: Index was out of
range ... (ResolveContentPropertyTransformer)` ile düşüyor. Hata satırı fırçanın
açılış satırını gösteriyor, içeriği değil — sebep gizli kalıyor.

**How to apply:** Tema dosyasına vektör arka plan, ikon fırçası ya da desen eklerken.
Aynı dosyada birden çok geometri üst üste binecekse her birini ayrı
`GeometryDrawing` yap ve opaklığı ortak `DrawingGroup`'a ver: sarma kuralı
(nonzero) kaynaklı delikler böyle oluşmaz, çakışan alanlar iki kez boyanmaz.
Ölçüm tarafında `Descendants` kullan — açık özellik etiketi araya girdiği için
`Elements` boş döner. Bkz. [[vidshrink-tema-olcumu]].
