Tema: kalite arayuzleri · kaynak: kalite-arayuzleri.md

# Kalite arayüzleri taraması

ffmpeg üstüne kurulu masaüstü arayüzleri. Sorular: kodlayıcı ayarı nasıl gösteriliyor, tahmini boyut
var mı, ilerleme/iptal nasıl kurulmuş, ffmpeg hatası nasıl çevriliyor. Rakamlar `gh api`, 2026-08-22.

**Değişiklik:** `fifonik/FFMetrics` deposunda kaynak kod yok (yalnız README, TODO, ekran görüntüsü),
lisans boş, konusu boyut hedefi değil kalite ölçümü. Yerine `cdgriffith/FastFlix` alındı.

## fifonik/FFMetrics
Kaynak yok, lisans belirtilmemiş · 1.017 yıldız · 29 açık issue · son push 2026-05-13 · v1.7.0
(2026-05-06). PSNR/SSIM/VMAF/XPSNR grafikleyen kapalı Windows aracı. Alınabilir tek fikir: verilen
ffmpeg komutlarının isteğe bağlı dosyaya günlüklenmesi (`-log-commands`). Bağımlılık olamaz.

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest` (2026-08-22) · lossless-cut `src/main/ffmpeg.ts`,
`src/renderer/src/util.ts`, `src/renderer/src/dialogs/index.tsx` · FastFlix
`encoders/hevc_x265/settings_panel.py`, `widgets/panels/status_panel.py`, `command_runner.py` ·
aviator `src/main.py` ve README "Aviator's Defaults"
