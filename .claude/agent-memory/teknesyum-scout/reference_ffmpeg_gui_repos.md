---
name: ffmpeg-gui-repos
description: Hangi ffmpeg masaüstü arayüzü deposu VidShrink taramaları için işe yarar, hangisi kaynaksız/ilgisiz
metadata:
  type: reference
---

ffmpeg üstü masaüstü arayüz taramalarında kullanılan depolar (2026-08-22 doğrulaması):

- `fifonik/FFMetrics` — **GitHub deposunda kaynak kod yok**, yalnız README/TODO/ekran görüntüsü,
  lisans alanı boş, program kapalı ikili. Kod düzeyinde ders çıkmaz; yeniden açmadan önce bunu hatırla.
- `cdgriffith/FastFlix` (MIT, Python/Qt) — CRF/bit hızı etiketlerini bağlamıyla yazma
  ("22 (1080p)") ve kodlama sırasında canlı boyut tahmini + "N/A" düşüşü örneği.
- `mifi/lossless-cut` (GPL-2.0) — ffmpeg stderr'ini kullanıcı diline çevirme ve iptali hata
  saymama örneği. Boyut tahmini yok (kayıpsız kesme aracı).
- `gianni-rosato/aviator` (GPL-3.0) — dar kapsam örneği; hata ve iş parçacığı yönetimi zayıf.

Rapor: `docs/taramalar/ffmetrics.md`, `docs/taramalar/fastflix.md`, `docs/taramalar/lossless-cut.md`, `docs/taramalar/aviator.md`.
