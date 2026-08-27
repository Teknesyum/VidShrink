---
name: no-build-bayat-derleme
description: dotnet test --no-build diskteki eski test derlemesini kosturur; VidShrink'te bu yuzden uc push kirmizi mühürlendi. Avalonia penceresinde Measure argumani da benzer sekilde sessizce yutulur.
metadata:
  type: feedback
---

**Kabul kriterini `dotnet test --no-build` ile dogrulama.** Kod degistikten sonra
`--no-build` diskteki *onceki* test derlemesini kosturur; yeni ya da degistirilmis
testler hic calismaz ve "0 basarisiz" okunur.

**Why:** T46 boyle muhurlendi. Testler yazildiklari anda 7 kirmiziydi, ama `bin/`
icindeki T46 oncesi derleme yesil testleri tasidigi icin uc kez "0 basarisiz" okundu
ve CI uc push boyunca kirmizi kaldi. T51'de T46'nin kendi commit'i sifirdan bir
worktree'de temiz derlenip kosturulunca ayni 7 kirmizi ayni sayilarla cikti.

**How to apply:** Kabul kriteri dogrulamasi her zaman tam derlemeyle. `--no-build`
yalnizca ayni turda az once derlenmis bir agacta, hiz icin. Supheliysen
`git worktree add` ile sifirdan bir agacta kostur — hem bayat derlemeyi hem de
agactaki artik dosyalari eler.

**Ayni sinifin ikinci yuzu — Avalonia pencere olcumu.** `window.Measure(size)`
cagrisindan once `window.Width`/`Height` `double.NaN` yapilmazsa `ApplyLayoutConstraints`
pencerenin kendi `Height` degerini argumanin yerine koyar ve **her istenen yukseklik
ayni yerlesimi** olcer. Imzasi: `ScrollViewer.Extent == Viewport` ve deger istekten
bagimsiz sabit. Olculen kural:
`viewport = clamp(istenen yukseklik, MinHeight, sonsuz) - pencere susu`, icerik boyunda
tavanlanir. Bir sonda yazarken bu sifirlamayi unutma; unutulursa sonda tutarli ama
tamamen yanlis sayilar uretir.

Bkz. [[vidshrink-dogrulama]] — olcum duzeneginin kendisi yanlissa ondan cikan her sayi
yanlistir ve raporlar tutarli gorunur.
