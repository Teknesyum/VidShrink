---
name: avalonia-gecis-anli-ekran-goruntusu
description: Avalonia'da geçişli bir özelliği değiştirdikten hemen sonra ekran görüntüsü alma ve okuma — ikisi de geçişin başlangıç değerini verir
metadata:
  type: feedback
---

Bir denetimin `Opacity`, `Width` gibi geçişli (`Transitions`) bir özelliğini değiştirdikten
**sonra aynı turda** ne ekran görüntüsü al ne de o özelliği oku. İkisi de geçişin başlangıç
değerini verir: `Bar.Opacity = 1` yazdıktan hemen sonra `Bar.Opacity` hâlâ 0 döner ve
`RenderTargetBitmap` şeridi görünmez çizer.

Aynı sorun `IsVisible` ile açılan bir öğede de var: görünür yapıldıktan hemen sonra çizersen
öğe henüz ölçülmediği için hiç görünmez.

**How to apply:** Ölçüm exe'sinde durumu değiştiren adım ile ekran görüntüsü alan adımı ayrı
zamanlayıcı adımlarına böl (geçiş süresinden uzun bir aralıkla). T40'ta dört ayrı görüntü
sessizce yanlış çıktı ve sebebi ancak adımlar bölününce anlaşıldı. Ölçüm iskeleti için
[[vidshrink-pencere-ici-olcum]].

**Görünmez ama odaklanabilir denetim.** Gizli bir şerit klavyeyle erişilebilir kalacaksa
`IsVisible=false` kullanma — `Opacity=0` + `IsHitTestVisible=false` kullan. `IsVisible=false`
denetimi sekme sırasından da çıkarır; ayrıca `UserControl`'ün içeriği hiç ölçülmediği için
`GetVisualDescendants()` boş döner ve ölçüm aracı öğeleri bulamaz (mantıksal ağaca düş).
