---
name: avalonia-kok-katman-olcusu
description: OverlayLayer başsız ölçümde 0x0 sınır döndürür; kademe/kaplama ölçüsü katman yöneticisinden okunmalı
metadata:
  type: project
---

`OverlayLayer.GetOverlayLayer(...)` ile alınan katmanın `Bounds` değeri, pencere
gösterilmeden ölçüldüğünde (AppHost + Measure/Arrange/UpdateLayout) 0x0 kalır. Katman bir
`Canvas` ve ölçüsünü çocuklarından almaz; `InvalidateMeasure` + `UpdateLayout` da
boyutlandırmıyor. Üstündeki `VisualLayerManager` ise doğru sınırı taşır (bu makinede
1904x990) ve katman onun başlangıcında durduğu için koordinatlar çakışır.

**Why:** T44'te panelin orta/tam kademesi katmanın sınırından türetiliyordu; başsız ölçümde
üç test birden 0 boyla düştü.

**How to apply:** kök katmana yayılan her ölçüde önce `_overlay.Bounds.Size`, boşsa
`(_overlay.GetVisualParent() as Visual)?.Bounds.Size` oku
(`ComparisonPanel.OverlayArea`). Aynı açık `Fill()` yolunda da vardı.
Bkz. [[vidshrink-pencere-ici-olcum]], [[vidshrink-maximized-olcum]].
