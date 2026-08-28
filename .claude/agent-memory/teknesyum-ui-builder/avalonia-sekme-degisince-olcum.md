---
name: avalonia-sekme-degisince-olcum
description: Sekme seçimi değiştikten sonra WindowLayoutTests.LayOutAt yeni sekmeyi ölçmüyor; içerik ağaçta durur ama bounds 0x0 kalır
metadata:
  type: project
---

`WindowLayoutTests.LayOutAt(window, size)` deseni **yalnız açılışta seçili sekme için**
çalışıyor. `tabs.SelectedIndex` değiştirilip aynı çağrı tekrarlandığında yeni sekmenin
içeriği ölçülmüyor: `PageAdvanced` görsel ağaçta bulunuyor ama `Bounds` 0x0, `Viewport`
0x0, görsel çocuğu yok ve alt ağacındaki adlı denetimler
`GetVisualDescendants()` ile hiç bulunamıyor ("Sequence contains no matching element").

**Why:** `LayOutAt` içindeki `window.UpdateLayout()` yeni içeriği pencerenin kendi
`ClientSize`'ı ile (gösterilmeyen pencerede sıfır) ölçüp **temiz** işaretliyor. Sonraki
kök geçişi (`root.Measure(size)`) temiz olan zincire hiç uğramıyor, çünkü aynı kısıtla
ölçüm kısa devre yapıyor.

**How to apply:** Sekme değiştikten sonra pencereyi hiç sürmeyen ikinci bir geçiş yaz —
ağacın tamamını geçersizleştir, sonra yalnız kökü ölç:

```csharp
foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();
var root = (Layoutable)window.GetVisualChildren().Single();
root.InvalidateMeasure();
root.Measure(size); root.Arrange(new Rect(size));
```

Bundan sonra `PageAdvanced` 1510x965 ölçülüyor (bu makinede, tasarım boyutunda).

Aynı sebeple `WindowLayoutTests`'in bütün sekmeleri gezen kırpma ölçümü ilk sekme dışında
boşa dönüyor olabilir — o dosya çoğu sözleşmede `owns` dışında, bulguyu T0'a yaz.

Ayrıca: taşma taraması pencerenin tamamına yapılırsa `PageShrink`'in **kabul edilmiş**
+32 dikey taşması yakalanır ve kendi panelinin suçu gibi görünür. Taramayı ilgilendiğin
sayfanın altına daralt (`GetSelfAndVisualDescendants`).

İlgili: [[avalonia-bassiz-yerlesim-olcumu]], [[vidshrink-maximized-olcum]],
[[windowlayouttests-sabit-sayilar]]
