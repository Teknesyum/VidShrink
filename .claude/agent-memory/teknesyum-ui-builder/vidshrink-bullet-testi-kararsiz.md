---
name: vidshrink-bullet-testi-kararsiz
description: BulletPaintingTests bazen "Call from invalid thread" ile düşer; aynı ikili ile tekrar koşunca geçer
metadata:
  type: project
---

`VidShrink.Tests.BulletPaintingTests` ara sıra beş ölçümün tamamını
`InvalidOperationException: Call from invalid thread` ile düşürüyor (tip başlatıcı
Avalonia dağıtıcısına erişiyor). Aynı ikili, hiçbir değişiklik olmadan, sonraki koşuda
yeşil dönüyor.

**Why:** 25.08.2026'da T36 tur 2 kapanışında görüldü; değişiklik yalnız metin ve testti,
bu ölçümlerle ilgisi yoktu.

**How to apply:** `dotnet test` bu sınıfta düşerse kodu aramadan bir kez daha koş. İki
koşuda da aynı hata çıkıyorsa o zaman gerçek. Raporda kararsızlığı belirt, sessizce
geçme. Ölçüm deseni için [[vidshrink-tema-olcumu]].
