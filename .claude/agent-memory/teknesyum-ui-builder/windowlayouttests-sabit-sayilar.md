---
name: windowlayouttests-sabit-sayilar
description: Yerleşimi küçülten her değişiklik WindowLayoutTests'teki sabitlenmiş eşikleri kırar; dosya çoğu sözleşmenin owns listesi dışında
metadata:
  type: project
---

`tests/VidShrink.Tests/WindowLayoutTests.cs` sayfanın kaymayı kestiği pencere yüksekliğini,
taban boyuttaki dış kaydırma sayısını ve "sayfayı tutan şey plan paneli tavanıdır" iddiasını
**sabit sayılarla** pinliyor.

**Why:** T46'da yonga şeridi üç satırdan ikiye indi ve plan gerekçeleri katlandı; sayfa
600 px'te kaymayı kesmeye başladı, plan içeriği 278 px'e düştü. Beş ölçüm birden kırmızı
oldu — hepsi iyileşme bildiriyordu, gerileme değil.

**How to apply:** Orta sütunun ya da sol sütunun yüksekliğini değiştiren her sözleşmede,
işi bitirmeden önce `dotnet test --filter WindowLayoutTests` koş. Dosya genellikle `owns`
dışında kalıyor; öyleyse dokunma, ham sayıları rapora yaz ve T0'a bırak. Bir de içerik
küçüldüğünde `ThePlanPanelCeilingIsWhatHoldsTheLoadedPage` yalnız yeniden sabitleme değil,
iddiasının gözden geçirilmesini ister.

İlgili: [[vidshrink-owns-listesi-daraltiyor]], [[vidshrink-makine-ekrani]]
