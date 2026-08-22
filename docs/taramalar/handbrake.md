Tema: hedef boyut GUI · kaynak: hedef-boyut-gui.md

# Hedef dosya boyutuna kodlayan masaüstü araçları

## HandBrake/HandBrake

**Künye** — GPLv2 ("çoğu dosya", LICENSE), son push 2026-08-22, 24.110 yıldız, 295 açık issue, son sürüm 1.11.2 (2026-06-07). Marka politikası depoda yok (doğrulanamadı).

**Ne yapıyor** — Genel amaçlı transcoder. Hedef boyut özelliğini *kaldırdı*: NEWS.markdown 0.9.6 bölümü "Target Size is gone, and isn't coming back / Don't bother complaining on the forums" diyor; daha önceki satırlar özelliğin tekrar tekrar bozulduğunu gösteriyor ("It keeps breaking itself"). Bugün yalnız sabit kalite (RF) veya ortalama bitrate var; boyut hesabını kullanıcı yapıyor. İki geçişte `VideoTwoPass` + `VideoTurboTwoPass` (ilk geçiş hızlandırılmış); turbo yalnız x264/x265'te destekli (`gtk/src/videohandler.c` içindeki `turbo_supported` kontrolü).

**Alınacak fikir** — Turbo ilk geçiş: analiz geçişi son geçişle aynı preset'te koşmak zorunda değil. VidShrink iki geçişli VBR'de ilk geçişi ucuzlatabilir. Ayrıca "hangi kodlayıcıda turbo geçerli" sorusunun kodlayıcı yetenek tablosunda yaşaması gerektiği fikri.

**Alınmayacak** — Hedef boyutu tamamen terk etmek. HandBrake'in gerekçesi kullanıcı beklentisiyle tek geçişlik tahminin uyuşmaması; VidShrink'in cevabı düzeltme turları, özelliği silmek değil. Preset/queue mimarisi de bizim tek işlik akışımıza ağır gelir.

**VidShrink'te nereye dokunur** — `src/VidShrink.Ffmpeg/EncoderCapabilities.cs` (turbo destek bayrağı), `src/VidShrink.Core/ConversionArguments.cs` ve `FfmpegArguments.cs` (ilk geçiş preset'i), `src/VidShrink.Core/CompressionStrategy.cs`.

## Kaynaklar

- `gh api repos/HandBrake/HandBrake` + `/releases/latest`; `NEWS.markdown` (0.9.6 bölümü); `libhb/preset.c`, `gtk/src/videohandler.c`, `macosx/HBVideo.m`
- `gh api repos/Kagami/webm.py` + `/releases/latest` (404); `webm.py` — `_calc_video_bitrate`, `_vorbisq2bitrate`, `_encode`, `print_stats`
- `gh api repos/staxrip/staxrip` + `/releases/latest`; `Source/General/Misc.vb` (`Calc`), `Source/General/Project.vb` (varsayılanlar), `Source/General/GlobalClass.vb` (`GetAutoSize`), `Source/Encoding/VideoEncoder.vb` (`RunCompCheck`, `AutoSetImageSize`)
- VidShrink karşılaştırması: `src/VidShrink.Core/PlanCalculator.cs` (satır 24-43)
