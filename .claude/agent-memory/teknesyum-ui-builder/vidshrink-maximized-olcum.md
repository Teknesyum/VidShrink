---
name: vidshrink-maximized-olcum
description: MainWindow WindowState=Maximized olduğu için başsız Measure/Arrange boyutu yok sayıyor; taşma sayısı pencere boyutundan bağımsız çıkar
metadata:
  type: project
---

`MainWindow` `WindowState="Maximized"` taşıyor. `WindowLayoutTests.LayOut(w, h)` içinde
`window.Measure(new Size(w,h))` verilen boyutu ölçüm geçişine uygular ama **yerleştirme
her zaman headless çalışma alanına** yapılır (bu makinede `1904x990`). Sonuç: görüş alanı
her boyutta aynı, ölçüm geçişi ise verilen boyutla koşar.

**Why:** T14 tur 2'de "taşma dokuz piksel ve pencere boyutundan bağımsız" bulgusu bundan
geliyordu; 1920x1080 denemek sayıyı değiştirmiyor, çünkü görüş alanı zaten sabit.

**How to apply:** Bu testlerde farklı boyutlar bağımsız senaryo değildir — dördü aynı
yerleşimi ölçer. Gerçek boyut duyarlılığı gerekiyorsa `WindowState`'i ölçüm sırasında
`Normal`'a çek. Ayrıca ölçüm ve yerleştirme genişliği ayrıştığı için `WrapPanel`
yükseklikleri ölçümdeki dar genişlikten gelir, yerleştirmedeki geniş halden değil
([[vidshrink-pencere-ici-olcum]]).
