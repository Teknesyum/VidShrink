# Plan — HDR10+ Köprüsü (x265 dhdr10-info)

Karar: `docs/handbrake/fable-karar-hdr10plus-2026-09-23.md`. Ölçüm: `docs/olcumler/hdr10plus-tasima.md`.

- **Core `Hdr10PlusJson`**: ffprobe `-show_frames` JSON metninden x265 `dhdr10-info` JSON'u üreten saf
  işlev (alan değerleri paydaya ölçeklenir, sıra gösterim sırası); yol kaçışı (`\` → `/`, `:` → `\:`).
- **Core `HdrResolver`**: `Hdr10PlusBridge` (HDR10+ kaynak, korunan HDR, libx265) ve
  `Hdr10PlusOnSvtAv1`; köprü varken `DynamicMetadataDropped` kurulmaz. Yönlendirme işlevi:
  kilitsiz, korunan HDR10+ kaynakta x265 kullanılabilirse kodek x265 olur.
- **Core `PlanCalculator`**: yönlendirme ve iki yeni gerekçe — `Hdr10PlusNotCarriedOnSvtAv1`
  (kilitli SVT-AV1), `Hdr10PlusDroppedInCut` (trim, detelecine ya da fps düşüşü köprüyü kapatır).
- **Core `EncodePlan` / `FfmpegArguments`**: `Hdr10PlusBridge`, `Hdr10PlusMetadataPath`; yol varken
  x265'in iki geçişine de aynı `dhdr10-info` gider.
- **Ffmpeg**: `FfprobeClient` kare başına yan veriyi okur ve çıkışta HDR10+ kare sayar;
  `EncodeRunner` köprüde önce ayrı ilerleme adımıyla çözme geçişini koşar, JSON'u işin geçici
  önekine (`vidshrink_<guid>_hdr10plus.json`) yazar, iş sonunda önekle birlikte silinir;
  kaynak/çıkış sayısı `EncodeResult`'a girer.
- **App**: iki gerekçe ve bir aşama sözcüğü 42 dilde; sayılar eşit değilse sonuç `StatusWarning`.
- **Test**: dönüştürücü alan değerleri, kaçış, yönlendirme, SVT-AV1, kesit, argüman, canlı
  `[FfmpegFact]` (12 kare, `-threads 2`); her davranışa elle mutasyon.
