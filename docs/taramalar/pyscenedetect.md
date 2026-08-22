Tema: sahne ve karmasiklik · kaynak: sahne-karmasiklik.md

# Sahne tespiti ve içerik karmaşıklığı ölçümü — tarama

Tarih: 2026-08-22. Tüm rakamlar `gh api` ve depo kaynağından, aksi belirtilmedikçe doğrulandı.

## Breakthrough/PySceneDetect

**Ne yapıyor** — Videoyu sahnelere böler. BSD-3-Clause, 5116 yıldız, 67 açık issue, son push 2026-08-19, son sürüm v0.7.1 (2026-07-22).
Örnekleme yok: **her kareyi** okur, ama ucuza. `ContentDetector` HSV farkı alır, eşik 27.0, `min_scene_len` 15 kare.
Ucuzlatma iki yoldan: `auto_downscale` kareyi en az 256 px genişliğe indirir (`DEFAULT_MIN_WIDTH`), `frame_skip` N kareden birini işler.
`StatsManager` kare başına metrikleri CSV'ye yazar — eşik değiştirip yeniden çalışırken video ikinci kez çözülmez.
`AdaptiveDetector` mutlak eşik yerine **komşuluk oranı** kullanır: her karenin skoru önündeki/arkasındaki `window_width=2` karenin ortalamasına bölünür, oran 3.0'ı aşarsa kesme; `min_content_val=15.0` gürültü tabanı.
Temsili kare seçimi `output/image.py`'de: sahne süresi `num_images` eşit dilime bölünür, her dilimden bir kare, uçlarda `frame_margin` payı bırakılır.

**Alınacak fikir** — (1) *Yerel ortalamaya göre normalize etme.* VidShrink'in `ComputeScanBias` düzeltmesi global; AdaptiveDetector'ın kalıbı ise her ölçümü kendi komşuluğuna oranlıyor. Pencere bias'ı böyle kaynağında sönümlenir, sonradan katsayıyla düzeltilmez.
(2) *Ölçüm sonucunu diske yaz.* StatsManager kalıbı: aynı dosya + aynı parametre = ikinci kez kodlama yok.
(3) *Sahne sınırında değil, sahne içinde eşit dilimde örnekle, uçlara pay bırak.* Sahne geçişi kaçınılması gereken yer; geçiş karesi karmaşıklığı yukarı çeker.

**Alınmayacak** — Tüm kareyi tarama. VidShrink'in ölçüsü piksel farkı değil, gerçek kodlama çıktısı; her kareyi kodlamak hedef süreyle bağdaşmaz. Ayrıca OpenCV/numpy bağımlılığı .NET tarafında karşılığı olmayan bir yüzey.

**Nereye dokunur** — `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (`ComputeScanBias`, `WindowScanPoints`), `src/VidShrink.Core/ComplexityProfile.cs`.

## Kaynaklar

- `gh api repos/Breakthrough/PySceneDetect`, `/releases/latest`; `scenedetect/detectors/content_detector.py`, `adaptive_detector.py`, `scene_manager.py`, `stats_manager.py`, `output/image.py` (main dalı, 2026-08-22'de okundu)
- `gh api repos/WyattBlue/auto-editor`, `/releases/latest`; `README.md`, `src/editmethods.nim`, `src/analyze/motion.nim`, `src/cache.nim` (master dalı)
- `gh api repos/slhck/ffmpeg-normalize`, `/releases/latest`; `README.md`, `src/ffmpeg_normalize/_streams.py`, `LICENSE.md` (master dalı)
- VidShrink mevcut durum: `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (2 sn pencere, en fazla 3 pencere, 40 tarama noktası, `ComputeScanBias`)
