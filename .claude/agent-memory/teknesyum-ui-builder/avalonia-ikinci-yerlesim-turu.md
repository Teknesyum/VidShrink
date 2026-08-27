---
name: avalonia-ikinci-yerlesim-turu
description: AppHost kalıbında ilk Measure/Arrange'dan sonra UpdateLayout yeni tur açmaz; ilk turda görünmez olan öğe DesiredSize 0 ile kalır
metadata:
  type: project
---

`tests/VidShrink.Tests/AppHost.cs` kalıbında pencere gösterilmediği için ilk
`Measure`/`Arrange`'dan sonra `window.UpdateLayout()` — ve T44'ün `Settle()` yardımcısı —
yeni bir yerleşim turu **açmaz**. İlk turda `IsVisible=false` olan öğe (rozetler,
`ZoomRow`, boş durum kapatılınca beliren her şey) `DesiredSize` ve `Bounds` 0x0 ile kalır.
Konum doğru görünür (sağa yaslanmış öğe sağ kenarda çıkar), yalnız genişlik sıfırdır —
bu yüzden hata "yerleşim çalışmadı" gibi değil "rozet dar" gibi okunur.

Çalışan yol (`ComparisonPanelTests.Relayout`):

    window.InvalidateMeasure();
    foreach (var part in window.GetVisualDescendants().OfType<Layoutable>()) part.InvalidateMeasure();
    window.Measure(WindowSize);
    window.Arrange(new Rect(WindowSize));
    Dispatcher.UIThread.RunJobs();

Yalnız `window.InvalidateMeasure()` yetmez: çocuklar `IsMeasureValid` olduğu için
`Measure` kısa devre yapar. Bütün alt öğeleri tek tek geçersizleştirmek gerekiyor.

**Why:** T49'da rozet genişliği üç ayrı ölçümde 0 çıktı ve önce metin ölçümü bozuk
sanıldı; serbest bir `TextBlock` 113x16 ölçünce sorunun yerleşim turu olduğu anlaşıldı.

**How to apply:** Panelin durumunu (görünürlük, metin) ölçüm lambdası içinde değiştirip
sonra boyut okuyacaksan `Settle` değil bu tam geçersizleştirme gerekir. `Settle` yalnız
kod tarafında doğrudan yazılan boyutlar için (terfi eden kabuğun `Canvas`/`Width`
değerleri) yeterlidir. Bkz. [[avalonia-bassiz-yerlesim-olcumu]].
