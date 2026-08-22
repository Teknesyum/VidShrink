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

## cdgriffith/FastFlix
MIT · 1.557 yıldız · 96 açık issue · son push 2026-05-19 · son sürüm 6.2.1 (2026-03-21)
- **Ne yapıyor:** Python/Qt, H.264/HEVC/AV1 kodlayıcı arayüzü. Hedef boyut girdisi yok; CRF veya bit
  hızı seçtiriyor, kurduğu ffmpeg komutunu ayrı panelde gösteriyor.
- **Alınacak fikir:** (1) Ham sayı da anlaşılır etiket de değil, **bağlamıyla açıklanmış sayı**: CRF
  listesi "22 (1080p)", bit hızı listesi "3000k (1920x1080p @ 30fps)", sonda "Custom". (2) Kodlama
  sırasında ffmpeg'in anlık bit hızından **canlı boyut tahmini** ve kalan süre; hesap tutmazsa uydurma
  sayı yerine "N/A". (3) Her koşum için ayrı stderr günlük dosyası, rapora eklenebilir.
- **Alınmayacak:** Hata tespiti stderr'de sabit İngilizce ifade arıyor ("Error during output"); ffmpeg
  sürümü değişince sessizce kaçırır. Panel başına düzine ayar açması da tek kaydıraçlı sadeliğe ters.
- **Nereye dokunur:** Etiketler `src/VidShrink.App/MainWindow.xaml`; tahmin
  `src/VidShrink.Core/PlanCalculator.cs` ile `EncodeRunner.cs`'deki `EncodeProgress.OutputMb` — canlı
  tahmin ve "hesaplanamıyor" hâli burada birleşir.

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

## fifonik/FFMetrics
Kaynak yok, lisans belirtilmemiş · 1.017 yıldız · 29 açık issue · son push 2026-05-13 · v1.7.0
(2026-05-06). PSNR/SSIM/VMAF/XPSNR grafikleyen kapalı Windows aracı. Alınabilir tek fikir: verilen
ffmpeg komutlarının isteğe bağlı dosyaya günlüklenmesi (`-log-commands`). Bağımlılık olamaz.

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest` (2026-08-22) · lossless-cut `src/main/ffmpeg.ts`,
`src/renderer/src/util.ts`, `src/renderer/src/dialogs/index.tsx` · FastFlix
`encoders/hevc_x265/settings_panel.py`, `widgets/panels/status_panel.py`, `command_runner.py` ·
aviator `src/main.py` ve README "Aviator's Defaults"
