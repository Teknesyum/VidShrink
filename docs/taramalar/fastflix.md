Tema: kalite arayuzleri · kaynak: kalite-arayuzleri.md

# Kalite arayüzleri taraması

ffmpeg üstüne kurulu masaüstü arayüzleri. Sorular: kodlayıcı ayarı nasıl gösteriliyor, tahmini boyut
var mı, ilerleme/iptal nasıl kurulmuş, ffmpeg hatası nasıl çevriliyor. Rakamlar `gh api`, 2026-08-22.

**Değişiklik:** `fifonik/FFMetrics` deposunda kaynak kod yok (yalnız README, TODO, ekran görüntüsü),
lisans boş, konusu boyut hedefi değil kalite ölçümü. Yerine `cdgriffith/FastFlix` alındı.

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

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest` (2026-08-22) · lossless-cut `src/main/ffmpeg.ts`,
`src/renderer/src/util.ts`, `src/renderer/src/dialogs/index.tsx` · FastFlix
`encoders/hevc_x265/settings_panel.py`, `widgets/panels/status_panel.py`, `command_runner.py` ·
aviator `src/main.py` ve README "Aviator's Defaults"
