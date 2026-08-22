Tema: frameserver ve ffmpeg cekirdegi · kaynak: frameserver.md

# Frameserver ve ffmpeg çekirdeği taraması
Tarih 2026-08-22. Rakamlar `gh api` ile depodan alındı, blogdan değil.

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

## Kaynaklar
`gh api repos/{FFmpeg/FFmpeg, vapoursynth/vapoursynth, AviSynth/AviSynthPlus}` (2026-08-22) · FFmpeg
`doc/ffmpeg.texi` (`-ss`, `-pass`, `-passlogfile`, `-vstats*`, `-stats_enc_*`), `doc/filters.texi`
(`trim`, `select`, `split`, `segment`), `libavcodec/ratecontrol.{h,c}` · VapourSynth `doc/output.rst`,
`doc/functions/video/{trim,splice}.rst` · AviSynth+ `avs_core/include/avisynth.h` lisans başlığı.
