Tema: frameserver ve ffmpeg cekirdegi · kaynak: frameserver.md

# Frameserver ve ffmpeg çekirdeği taraması
Tarih 2026-08-22. Rakamlar `gh api` ile depodan alındı, blogdan değil.

## Depo: AviSynth/AviSynthPlus
1.199 yıldız · push 2026-08-21 · açık issue 89 · GitHub lisans alanı boş; `avisynth.h` GPL-2+ **artı
bağlama istisnası** · son etiket v3.7.5 (2025-04-21), 16 aydır etiket yok ama ana dal canlı.
- **Ne yapıyor** — Windows'ta DLL olarak yüklenen, betikle tanımlanan tembel frameserver. Sunucu
  filtreye iş parçacığı sayısını `SetCacheHints` / `CACHE_INFORM_NUM_THREADS` ile bildirir, önbellek
  boyutu `AEP_CACHESIZE_L2` gibi ortam özelliklerinden okunur.
- **Alınacak fikir** — Paralelliği filtreye *söylemek*. VidShrink pencereleri paralel işliyor ama her
  sürece kaç çekirdek düştüğünü ffmpeg'e bildirmiyor; tek grafiğe geçilirse `-threads` /
  `-filter_threads` bütçesi pencere sayısına göre açıkça dağıtılmalı. Maliyet düşük.
- **Alınmayacak** — AviSynth'i kurulum zincirine sokmak: 32/64-bit DLL kaydı, eklenti klasörü, sürüm
  çakışması. Lisans istisnası kapalı kaynak barındırmayı mümkün kılıyor, teknik yük ayrı mesele.
- **Nereye dokunur** — `src/VidShrink.Core/FfmpegArguments.cs` (thread bütçesi),
  `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (paralel pencere ↔ thread eşlemesi).

## Kaynaklar
`gh api repos/{FFmpeg/FFmpeg, vapoursynth/vapoursynth, AviSynth/AviSynthPlus}` (2026-08-22) · FFmpeg
`doc/ffmpeg.texi` (`-ss`, `-pass`, `-passlogfile`, `-vstats*`, `-stats_enc_*`), `doc/filters.texi`
(`trim`, `select`, `split`, `segment`), `libavcodec/ratecontrol.{h,c}` · VapourSynth `doc/output.rst`,
`doc/functions/video/{trim,splice}.rst` · AviSynth+ `avs_core/include/avisynth.h` lisans başlığı.
