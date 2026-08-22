Tema: kalite arayuzleri · kaynak: kalite-arayuzleri.md

# Kalite arayüzleri taraması

ffmpeg üstüne kurulu masaüstü arayüzleri. Sorular: kodlayıcı ayarı nasıl gösteriliyor, tahmini boyut
var mı, ilerleme/iptal nasıl kurulmuş, ffmpeg hatası nasıl çevriliyor. Rakamlar `gh api`, 2026-08-22.

**Değişiklik:** `fifonik/FFMetrics` deposunda kaynak kod yok (yalnız README, TODO, ekran görüntüsü),
lisans boş, konusu boyut hedefi değil kalite ölçümü. Yerine `cdgriffith/FastFlix` alındı.

## mifi/lossless-cut
GPL-2.0 · 43.1k yıldız · 295 açık issue · son push 2026-08-21 · son sürüm v3.69.0 (2026-06-04)
- **Ne yapıyor:** Electron/TypeScript, ffmpeg ile kayıpsız kesme-birleştirme. Yeniden kodlama yok,
  dolayısıyla boyut tahmini de yok; değeri süreç ve hata yönetiminde.
- **Alınacak fikir:** stderr ham gösterilmiyor, sınıflandırılıyor ("No space left on device" → disk
  dolu iletisi; başlık yazma hatası → "bu codec bu kapta desteklenmiyor, başka kap dene"). Eşleşme
  yoksa ham metin yerine *denenecek maddeler listesi* çıkıyor. İptal ayrı hata türü olarak yakalanıp
  sessizce yutuluyor — iptalde hata kutusu yok. Süreçler tek kayıtta tutulup toplu iptal ediliyor.
- **Alınmayacak:** Sınıflandırma stderr'de substring/regex arıyor; kırılgan olduğu kaynakta yorumla
  itiraf edilmiş. Kural sayısını az tut, eşleşmeyi çıkış koduyla birlikte ara. 295 açık issue'luk
  özellik yığını da örnek alınacak bir kapsam değil.
- **Nereye dokunur:** `src/VidShrink.Ffmpeg/EncodeRunner.cs` — bugün ham stderr kuyruğu
  `InvalidOperationException` içinde fırlatılıyor. Metinler `src/VidShrink.App/LanguageCatalog.cs`.

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest` (2026-08-22) · lossless-cut `src/main/ffmpeg.ts`,
`src/renderer/src/util.ts`, `src/renderer/src/dialogs/index.tsx` · FastFlix
`encoders/hevc_x265/settings_panel.py`, `widgets/panels/status_panel.py`, `command_runner.py` ·
aviator `src/main.py` ve README "Aviator's Defaults"
