---
name: ref-tdarr-kaynak-kapali
description: HaveAGitGat/Tdarr deposu kaynak kod içermez (tescilli EULA, sadece paketleme/issue tracker) — tarama işlerinde HandBrake ile değiştir
metadata:
  type: reference
---

`github.com/HaveAGitGat/Tdarr` deposunda uygulama kaynağı yok: kökte yalnız `docker/`,
`flatpak/`, `updater/`, `assets/`, `LICENSE.md`, `README.md`. `LICENSE.md` OSI onaylı değil,
tescilli EULA (Personal Free / Subscription / Business). GitHub sürümü yayınlanmıyor, yalnız
`v1.xxx-Beta` biçimli etiketler var. 2026-08-22'de doğrulandı.

**Why:** Tdarr transcode/donanım tespiti temalı taramalarda sık aday olarak çıkıyor, ama
mekanizması incelenemiyor — depoyu açıp boş dönmek zaman kaybı.

**How to apply:** Tdarr aday olarak verilirse doğrudan "kaynak kapalı" diye işaretle ve aynı
temada okunabilir kaynaklı `HandBrake/HandBrake` (GPL-2.0, `libhb/nvenc_common.c`,
`libhb/qsv_common.c` içinde gerçek SDK yetenek sorgusu) ile değiştir; değiştirdiğini rapora yaz.
Eşdeğer açık kaynak olarak `Tdarr_Plugins` deposu ayrıdır ve açıktır, ama tespit çekirdeği orada
değil.
