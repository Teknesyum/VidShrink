Tema: transcode sunucu · kaynak: transcode-sunucu.md

# Transcode sunucularında donanım kodlayıcı tespiti (2026-08-22)

## Depo: jellyfin/jellyfin-ffmpeg
NOASSERTION (LGPL/GPL karışık, kökte 4 COPYING) · 849 yıldız · 21 açık issue · push 2026-08-19 ·
sürüm v7.1.4-3 (2026-06-06).
**Ne yapıyor.** FFmpeg'in yamalı çatalı. Tespit yapmaz, gereksizleştirir: Windows için
`Dockerfile.win64.in` ile sabit toolchain'den nvenc/qsv/amf/vaapi aynı bayraklarla açık tek ikili.

**Alınacak fikir.** ffmpeg'i sabitle. PATH'teki rastgele ffmpeg prob sonucunu makineden makineye
değiştirir, hata raporu yeniden üretilemez. Maliyet: ~80 MB dağıtım yükü + GPL uyum metni.
**Alınmayacak.** FFmpeg'i çatallayıp yama taşımak — Jellyfin'in tam zamanlı yayın hattı var,
VidShrink'in yok. Hazır ikiliyi al, kaynağı değil.
**Nereye dokunur.** `src/VidShrink.Ffmpeg/ToolLocator.cs` — "uygulama yanındaki ikili önce".

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest`, dördü için 2026-08-22 · jellyfin master
`EncoderValidator.cs`, `MediaEncoder.cs`, `EncodingHelper.cs` · jellyfin-ffmpeg master kök listesi,
`Dockerfile.win64.make` · Tdarr master `LICENSE.md`, `README.md` · HandBrake master `libhb/*`. **Doğrulanamadı:** Tdarr çalışma zamanı GPU tespiti (kaynak kapalı); "1.000.000 dosyalık
kütüphanede test edildi" (Tdarr README, kendi beyanı).
