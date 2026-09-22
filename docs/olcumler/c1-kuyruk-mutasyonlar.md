# C1 Kuyruk Mutasyonları (22 Eylül 2026)

## C1-3 — Klasör Bırakma (`KlasorBirakmaTests`)

```
M1 alt klasore iniliyor: Failed!  - Failed:     1, Passed:     6, Skipped:     0, Total:     7, Duration: 802 ms - VidShrink.Tests.dll (net8.0)
M2 sablon tasinmiyor: Failed!  - Failed:     2, Passed:     5, Skipped:     0, Total:     7, Duration: 842 ms - VidShrink.Tests.dll (net8.0)
M3 uzanti sabit mp4: Failed!  - Failed:     1, Passed:     6, Skipped:     0, Total:     7, Duration: 800 ms - VidShrink.Tests.dll (net8.0)
M4 ana pencere secenek gecirmiyor: Failed!  - Failed:     1, Passed:     6, Skipped:     0, Total:     7, Duration: 800 ms - VidShrink.Tests.dll (net8.0)
Geri: Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 793 ms - VidShrink.Tests.dll (net8.0)
```

## C1-2 + C1-4 — Kuyruk Düzenleme ve Bitince Eylemi (`KuyrukDuzenlemeTests`)

M1 taşıma sınırı, M2 geri sayımın bir saniye erken bitmesi, M3 duraklatma kontrolünün düşmesi, M4 vazgeçin sayacı sıfırlamaması, M5 çıkarmanın kabul sayısını düşürmemesi.

```
== M1-sinir
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.BekleyenlerTasinirVeCikarilir [16 ms]
Failed!  - Failed:     1, Passed:     5, Skipped:     0, Total:     6, Duration: 561 ms - VidShrink.Tests.dll (net8.0)
== M2-erken
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.KapatGeriSayimdanSonraBirKezCalisir [22 ms]
Failed!  - Failed:     1, Passed:     5, Skipped:     0, Total:     6, Duration: 563 ms - VidShrink.Tests.dll (net8.0)
== M3-duraklatma
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.GeriSayimYalnizBosVeAkanSiradaBaslar [30 ms]
Failed!  - Failed:     1, Passed:     5, Skipped:     0, Total:     6, Duration: 574 ms - VidShrink.Tests.dll (net8.0)
== M4-vazgec
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.KapatGeriSayimdanSonraBirKezCalisir [25 ms]
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.VazgecilenGeriSayimCalismaz [12 ms]
Failed!  - Failed:     2, Passed:     4, Skipped:     0, Total:     6, Duration: 573 ms - VidShrink.Tests.dll (net8.0)
== M5-sayim
  Failed VidShrink.Tests.KuyrukDuzenlemeTests.BekleyenlerTasinirVeCikarilir [17 ms]
Failed!  - Failed:     1, Passed:     5, Skipped:     0, Total:     6, Duration: 574 ms - VidShrink.Tests.dll (net8.0)
== taban
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 568 ms - VidShrink.Tests.dll (net8.0)
```
