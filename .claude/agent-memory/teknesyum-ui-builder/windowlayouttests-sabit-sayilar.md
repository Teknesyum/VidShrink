---
name: windowlayouttests-sabit-sayilar
description: WindowLayoutTests'in yükseklik taraması degenerate — verilen yükseklik yerleşime ulaşmıyor; sayfayı tutan şey sol ayar sütunu
metadata:
  type: project
---

`tests/VidShrink.Tests/WindowLayoutTests.cs` hakkında T46/K7'de ölçülenler:

- **Yükseklik taraması işe yaramıyor.** `Read`/`LayOut` `Measure(size)` + `Arrange(rect)`
  yapsa da `PageShrink`'in görüş alanı her zaman başsız çalışma alanından geliyor.
  200/400/600/900/1060 istendiğinde beşinde de uzam 895, görüş alanı 895.
  `WindowState`'i `Normal`'a çekmek değiştirmiyor ([[vidshrink-maximized-olcum]] burada
  yetmiyor). "Sayfa şu yükseklikte kaymayı keser" cümlesi ölçülemez; ölçülebilen şey
  `PageShrink.Content`'in `DesiredSize` boyu. **Genişlik ekseni gerçek** — ölçüm geçişine
  ulaşıyor, tarama oradan yapılır.
- **Sayfanın boyunu sol ayar sütunu tutuyor**, plan paneli değil. Tasarım boyutunda
  sütun `DesiredSize`: dolu 802/676/473, boş 834/630/437. Sayfa içeriği = en uzun sütun
  + `WorkspaceMargin` (802 + 24 = 826). Sayfayı kısaltmak isteyen iş sol sütuna bakmalı.
- **Katlanan bölümü açıp yeniden ölçmek çalışmıyor.** `BtnPlanReasons`'a `Click` olayı
  atıldığında `PlanReasons.IsVisible` true oluyor ve dokuz çocuk yerinde, ama ikinci bir
  `Measure`/`Arrange`/`UpdateLayout` turundan sonra bile çocukların `DesiredSize` değeri
  sıfır — hiç ölçülmüyorlar. Gizliyken hiç ölçülmemiş bir alt ağacı bu koşumda
  canlandırmanın yolu bulunamadı.

**How to apply:** Orta ya da sol sütunun yüksekliğini değiştiren her işte bu dosyayı
koştur. Ölçemediğin bir iddiayı yeniden pinleme — testi ölçülebilen niceliğe çevir ve
belge yorumuna neden çevirdiğini yaz.

İlgili: [[vidshrink-owns-listesi-daraltiyor]], [[avalonia-bassiz-yerlesim-olcumu]]
