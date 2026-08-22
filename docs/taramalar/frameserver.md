# Frameserver ve ffmpeg çekirdeği taraması
Tarih 2026-08-22. Rakamlar `gh api` ile depodan alındı, blogdan değil.

## Depo: FFmpeg/FFmpeg
63.531 yıldız · push 2026-08-21 · açık issue 3 (asıl izleyici Trac) · çekirdek LGPL-2.1+, `--enable-gpl`
ile GPL-2+ (x264/x265 orada) · GitHub'da release yok, mirror etiketi `v0.6.1` yanıltıcı — sürüm ffmpeg.org'da.
- **Ne yapıyor** — Girdiyi bir kez çözüp filtre grafiğinden çok çıkışa dağıtır: `select=n=K:e=<ifade>`
  kareyi `ceil(val)-1` çıkışına yollar, `segment=timestamps=a|b` tek akışı N akışa böler,
  `trim=start:end` (+`setpts`) aralık keser, `split` kopyalar. `-ss` girdi tarafında en yakın önceki
  seek noktasına atlar; `-accurate_seek` (varsayılan) farkı çözüp atar, `-noaccurate_seek` saklar —
  hız kazancı doğrudan kare kayması demek. Çıkıştaki `-ss` sıfırdan çözüp atar: tam ama yavaş.
  Ölçüm: `-vstats` / `-vstats_file` / `-vstats_version` (v2: out, st, frame, q, PSNR, f_size, s_size,
  time, br, avg_br) ve `-stats_enc_pre` / `-stats_enc_post` + `..._fmt` (`{n} {ni} {t} {pts} {size}
  {br} {abr} {key}`). İki geçiş `-pass 1|2`, `-passlogfile PREFIX` → `PREFIX-N.log`; libavcodec
  mpegvideo stat satırı kare başına `type q itex ptex mv misc mc-var var hbits` tutar
  (`ratecontrol.h::RateControlEntry`).
- **Alınacak fikir** — (1) K pencereyi tek süreçte tek çözmeyle kodla: `select=n=K` kareyi pencere
  indeksine yollar, K çıkış `-f null` ya da ayrı dosya; bugünkü N süreç + N `-ss` yerine tek geçiş,
  maliyet grafik kurulumu ve pencere→çıkış eşlemesi. (2) Pencere boyutunu dosyadan değil
  `-stats_enc_post` `{size}` toplamından oku: kare başına bayt profili bedava, geçici dosya yok.
- **Alınmayacak** — Ölçümde `-noaccurate_seek` (pencere kayar, ölçtüğün yer istediğin yer değil) ve
  `-vstats` PSNR alanı (referans çözme maliyeti). `ff_write_pass1_stats` biçimini karmaşıklık kaynağı
  sanmak: mpeg4/mpegvideo ailesine ait, libx264/libx265 kendi stat biçimini farklı alanlarla yazar.
- **Nereye dokunur** — `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (pencere başına süreç → tek grafik),
  `src/VidShrink.Core/FfmpegArguments.cs`, `src/VidShrink.Core/ComplexityProfile.cs`,
  `tests/VidShrink.Tests/WindowSamplingTests.cs`.

## Depo: vapoursynth/vapoursynth
2.077 yıldız · push 2026-08-21 · açık issue 10 · LGPL-2.1 · son kararlı R79 (2026-08-07), R80A1/A2 ön sürüm.
- **Ne yapıyor** — Kare indeksli rastgele erişimli frameserver. `std.Trim(clip, first, last)` aralık
  keser, `std.Splice([...])` birleştirir: dağınık pencereler tek klip olur. `vspipe` bunu tek seferde
  Y4M olarak ffmpeg'e borular; `-r/--requests` eşzamanlı kare isteği, `-s/-e` kare aralığı,
  `--filter-time` filtre başına süre, `-j` kare özellikleri JSON.
- **Alınacak fikir** — Pencereleri ayrı ölçüm birimi değil *tek sanal klip* say. Trim+Splice mantığı
  ffmpeg'de `trim`+`concat` ile birebir kurulur: tek kodlama, tek sayı, pencereler arası
  normalizasyon derdi biter. Maliyet: pencere sınırına düşen sahne kesmeleri I-kare şişmesi yaratır.
- **Alınmayacak** — VapourSynth'i bağımlılık yapmak. WPF kurulumuna Python çalışma zamanı ve yerel
  eklenti yüzeyi eklemenin bedeli kazanılan hızın çok üstünde.
- **Nereye dokunur** — `src/VidShrink.Ffmpeg/ComplexityProbe.cs`,
  `src/VidShrink.Ffmpeg/CalibrationProbe.cs` (birleşik klip sapması).

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
