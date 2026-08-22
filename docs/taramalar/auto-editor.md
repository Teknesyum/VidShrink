Tema: sahne ve karmasiklik · kaynak: sahne-karmasiklik.md

# Sahne tespiti ve içerik karmaşıklığı ölçümü — tarama

Tarih: 2026-08-22. Tüm rakamlar `gh api` ve depo kaynağından, aksi belirtilmedikçe doğrulandı.

## WyattBlue/auto-editor

**Ne yapıyor** — Sessiz/hareketsiz bölümleri otomatik keser. Unlicense (kamu malı), 5040 yıldız, 0 açık issue, son push 2026-08-20, son sürüm 31.5.0 (2026-08-13). Python'dan **Nim'e yeniden yazılmış** (`src/*.nim`) — eski Python API'sine dair her belge artık geçersiz.
Örnekleme yok, tüm zaman çizgisini tarar; maliyeti ölçünün kendisini ucuzlatarak düşürür.
`--edit motion` şu zinciri libav filtre grafiğine gömer: `scale=<width>:-1,format=gray,gblur=sigma=<blur>`, varsayılan `width=400`, `blur=9`, `threshold=0.02`. Farklı piksel sayımı SIMD ile (NEON/SSE/WASM). Ses yolu varsayılanı `threshold=0.04`, `dB` birimi de kabul ediliyor.
`src/cache.nim`: analiz sonucu `temp/ae-<version>/<tag>.bin` dosyasına yazılır; anahtar = **dosya yolu + mtime (nanosaniye) + time base + yöntem + argümanlar**. Dizin sınırı aşınca en eski dosyalar silinir.

**Alınacak fikir** — (1) *Önbellek anahtarına mtime ve tüm probe argümanlarını koy, sürümü dizin adına yaz.* Sürüm değişince eski önbellek kendiliğinden ölür; kullanıcı elle temizlemez. Boyut sınırı + LRU tahliye şart, yoksa temp şişer.
(2) *Ölçüm çözünürlüğünü sabit küçük genişliğe indir, blur ile gürültüyü sil.* VidShrink zaten yarı çözünürlükte probe yapıyor; sabit hedef genişlik (kaynaktan bağımsız) probe süresini kaynak çözünürlüğünden ayırır.

**Alınmayacak** — Tüm çizgiyi tarama modeli ve Unlicense'ın verdiği rahatlığa güvenip kalıbı bire bir taşıma; auto-editor'ün ölçüsü ikili karar (kes/tut), VidShrink'inki sürekli bir bit hızı tahmini. Yanlış ölçüm burada bir karenin kesilmesi, bizde hedef boyutun kaçırılması demek.

**Nereye dokunur** — `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (probe filtre zinciri), yeni bir probe önbellek dosyası, `src/VidShrink.Ffmpeg/TempCleanup.cs`.

## Kaynaklar

- `gh api repos/Breakthrough/PySceneDetect`, `/releases/latest`; `scenedetect/detectors/content_detector.py`, `adaptive_detector.py`, `scene_manager.py`, `stats_manager.py`, `output/image.py` (main dalı, 2026-08-22'de okundu)
- `gh api repos/WyattBlue/auto-editor`, `/releases/latest`; `README.md`, `src/editmethods.nim`, `src/analyze/motion.nim`, `src/cache.nim` (master dalı)
- `gh api repos/slhck/ffmpeg-normalize`, `/releases/latest`; `README.md`, `src/ffmpeg_normalize/_streams.py`, `LICENSE.md` (master dalı)
- VidShrink mevcut durum: `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (2 sn pencere, en fazla 3 pencere, 40 tarama noktası, `ComputeScanBias`)
