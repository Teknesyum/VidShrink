Tema: kalite arayuzleri · kaynak: kalite-arayuzleri.md

# Kalite arayüzleri taraması

ffmpeg üstüne kurulu masaüstü arayüzleri. Sorular: kodlayıcı ayarı nasıl gösteriliyor, tahmini boyut
var mı, ilerleme/iptal nasıl kurulmuş, ffmpeg hatası nasıl çevriliyor. Rakamlar `gh api`, 2026-08-22.

**Değişiklik:** `fifonik/FFMetrics` deposunda kaynak kod yok (yalnız README, TODO, ekran görüntüsü),
lisans boş, konusu boyut hedefi değil kalite ölçümü. Yerine `cdgriffith/FastFlix` alındı.

## gianni-rosato/aviator
GPL-3.0 · 251 yıldız · 6 açık issue · son push 2026-04-25 · son etiketli sürüm 0.6.0 (2024-03-12)
- **Ne yapıyor:** GTK4/libadwaita, SVT-AV1 + Opus için kasıtlı olarak dar bir arayüz, ~500 satır tek
  dosya. Boyut tahmini **yok**; CRF 0-63 ham kaydıraç, varsayılan 32, açıklama tooltip'te.
- **Alınacak fikir:** Kapsamı küçük tutma disiplini — tek ekran, savunulabilir varsayılanlar (preset 6,
  CRF 32, ses 80 kb/s) ve bunların *neden* öyle olduğunun README'de yazılması. Amaç kipleri için
  aynısı gerekli: seçilen değerin gerekçesi belgede dursun.
- **Alınmayacak:** Kodlama döngüsünde `try/except` yok; ffmpeg hata verirse kullanıcı yalnız duran bir
  çubuk görüyor. Çubuk doğrudan iş parçacığından güncelleniyor. İptal, biten kodlama gibi raporlanıyor
  — "bitti"/"iptal edildi" ayrımı yok; VidShrink'te iptal ayrı sonuç durumu kalmalı.
- **Nereye dokunur:** `src/VidShrink.App/MainWindow.xaml.cs` (iptal ayrı durum, ilerleme dispatcher
  üzerinden) ve amaç kipi varsayılanlarının gerekçesi için `docs/` altındaki karar notu.

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest` (2026-08-22) · lossless-cut `src/main/ffmpeg.ts`,
`src/renderer/src/util.ts`, `src/renderer/src/dialogs/index.tsx` · FastFlix
`encoders/hevc_x265/settings_panel.py`, `widgets/panels/status_panel.py`, `command_runner.py` ·
aviator `src/main.py` ve README "Aviator's Defaults"
