---
name: vidshrink-owns-siniri-kopyalama
description: VidShrink'te owns sinirinin disinda kalan sabitler kopyalaniyor veya owns disina yaziliyor; her denetimde bu iki sapmayi ara
metadata:
  type: project
---

Sözleşmelerin `owns` listesi dar olduğu için builder'lar tekrar eden iki sapma yapıyor:
(1) owns dışındaki dosyaya yazıp raporda gerekçelendiriyor (T6, `IEncoderAvailability.cs`),
(2) owns dışındaki `private static` sabiti kendi dosyasına **kopyalıyor**
(T8, `MainWindow.xaml.cs:35 HardwareEncoderOrder` = `PlanCalculator.cs:45 FastHardwareOrder`).

**Why:** İkincisi denetimde kolay kaçıyor — kriter geçiyor, hiçbir test kırılmıyor, ama
iki liste sessizce ayrışabiliyor ve builder bunu rapora yazmıyor.

**How to apply:** Yeni bir sabit dizi/eşik gördüğünde aynı değerin Core tarafında
zaten olup olmadığını grep'le. Varsa "not" olarak yaz ve kopyanın raporda geçip
geçmediğini söyle; owns içinde düzeltilemeyeceği için tur açma, T0'a bırak.
İlgili: [[vidshrink-dogrulama-beyani]]
