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

## WyattBlue/auto-editor

**Ne yapıyor** — Sessiz/hareketsiz bölümleri otomatik keser. Unlicense (kamu malı), 5040 yıldız, 0 açık issue, son push 2026-08-20, son sürüm 31.5.0 (2026-08-13). Python'dan **Nim'e yeniden yazılmış** (`src/*.nim`) — eski Python API'sine dair her belge artık geçersiz.
Örnekleme yok, tüm zaman çizgisini tarar; maliyeti ölçünün kendisini ucuzlatarak düşürür.
`--edit motion` şu zinciri libav filtre grafiğine gömer: `scale=<width>:-1,format=gray,gblur=sigma=<blur>`, varsayılan `width=400`, `blur=9`, `threshold=0.02`. Farklı piksel sayımı SIMD ile (NEON/SSE/WASM). Ses yolu varsayılanı `threshold=0.04`, `dB` birimi de kabul ediliyor.
`src/cache.nim`: analiz sonucu `temp/ae-<version>/<tag>.bin` dosyasına yazılır; anahtar = **dosya yolu + mtime (nanosaniye) + time base + yöntem + argümanlar**. Dizin sınırı aşınca en eski dosyalar silinir.

**Alınacak fikir** — (1) *Önbellek anahtarına mtime ve tüm probe argümanlarını koy, sürümü dizin adına yaz.* Sürüm değişince eski önbellek kendiliğinden ölür; kullanıcı elle temizlemez. Boyut sınırı + LRU tahliye şart, yoksa temp şişer.
(2) *Ölçüm çözünürlüğünü sabit küçük genişliğe indir, blur ile gürültüyü sil.* VidShrink zaten yarı çözünürlükte probe yapıyor; sabit hedef genişlik (kaynaktan bağımsız) probe süresini kaynak çözünürlüğünden ayırır.

**Alınmayacak** — Tüm çizgiyi tarama modeli ve Unlicense'ın verdiği rahatlığa güvenip kalıbı bire bir taşıma; auto-editor'ün ölçüsü ikili karar (kes/tut), VidShrink'inki sürekli bir bit hızı tahmini. Yanlış ölçüm burada bir karenin kesilmesi, bizde hedef boyutun kaçırılması demek.

**Nereye dokunur** — `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (probe filtre zinciri), yeni bir probe önbellek dosyası, `src/VidShrink.Ffmpeg/TempCleanup.cs`.

## slhck/ffmpeg-normalize

**Ne yapıyor** — Sesi EBU R128 ile hedef ses düzeyine normalize eder. 1528 yıldız, 0 açık issue, son push 2026-07-10, son sürüm v1.41.1 (2026-07-10). GitHub lisansı `NOASSERTION` gösteriyor ama `LICENSE.md` içeriği düz MIT metni — dosya adı `.md` olduğu için otomatik tanınmıyor.
İki geçişli kalıbın referansı bu: **1. geçiş** `loudnorm=...:print_format=json` filtresini `-f null` çıkışıyla koşar, stderr'den `[Parsed_loudnorm_N]` bloğunu bulup JSON'u ayrıştırır (`input_i`, `input_lra`, `input_tp`, `input_thresh`, `target_offset`).
**2. geçiş** ölçülen değerleri filtreye geri verir ve **yine `print_format=json` ile koşar**; ulaşılan değer `ebu_pass2` olarak saklanır, `--print-stats` ikisini birlikte basar. Yani hedef ile sonucun farkı raporlanabilir bir veri.
Ölçüm sonucu güvenilmezse düzeltilir, sessizce geçilmez: `input_i > 0` ise 0'a kırpılır ve uyarı yazılır; `input_lra` [1,50] dışındaysa `_constrain` ile kırpılır ve neye kırpıldığı log'a düşer. `±inf` ölçümleri -99/0'a çevrilir.
v1.40.0 `--threshold` ile hedefe zaten yakın dosyayı yeniden kodlamadan kopyalar; `--print-stats` çıktısında dosya başına `status` (`normalized`/`skipped`/`error`) döner.

**Alınacak fikir** — (1) *İkinci geçişi de ölç ve hedef–sonuç farkını sakla.* VidShrink kodlama sonrası boyutu biliyor ama ölçülen `ReferenceBppf` ile gerçekleşen bppf farkı kayda geçmiyor. Bu fark biriktikçe probe'un sistematik sapması veriye dayanarak düzeltilir — tahmin motorunun tek geri besleme kanalı budur.
(2) *Her kırpmayı adıyla logla.* `_constrain(değer, min, max, ad)` kalıbı: sınıra çarpan tahmin sessizce geçmiyor, "şu değer şu aralığa kırpıldı" satırı düşüyor. Ölçüm hatası yakalamanın en ucuz yolu.
(3) *Hedefe zaten yakınsa kodlama.* `--threshold` karşılığı: dosya hedef boyutun belli bir yüzdesi içindeyse yeniden kodlamak yerine kopyala ve durumu "atlandı" olarak bildir.

**Alınmayacak** — ffmpeg'in stderr metnini serbest biçimde ayrıştırma refleksi. Burada zorunlu çünkü loudnorm JSON'u başka yere yazmıyor; VidShrink'in ihtiyaç duyduğu boyut/kare verisi `-progress` ve ffprobe JSON'undan yapısal olarak alınabiliyor. Ayrıca EBU R128'in kendisi ses metriği, bit hızına dair hiçbir şey söylemiyor — alınan şey yalnızca iki geçişin kurulum kalıbı.

**Nereye dokunur** — `src/VidShrink.Core/PlanCalculator.cs` (kırpma uyarıları, hedefe yakınsa atla), `src/VidShrink.Ffmpeg/EncodeRunner.cs` (ikinci geçiş ölçümü), `src/VidShrink.Core/ComplexityProfile.cs` (hedef–sonuç farkının saklanması).

## Kaynaklar

- `gh api repos/Breakthrough/PySceneDetect`, `/releases/latest`; `scenedetect/detectors/content_detector.py`, `adaptive_detector.py`, `scene_manager.py`, `stats_manager.py`, `output/image.py` (main dalı, 2026-08-22'de okundu)
- `gh api repos/WyattBlue/auto-editor`, `/releases/latest`; `README.md`, `src/editmethods.nim`, `src/analyze/motion.nim`, `src/cache.nim` (master dalı)
- `gh api repos/slhck/ffmpeg-normalize`, `/releases/latest`; `README.md`, `src/ffmpeg_normalize/_streams.py`, `LICENSE.md` (master dalı)
- VidShrink mevcut durum: `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (2 sn pencere, en fazla 3 pencere, 40 tarama noktası, `ComputeScanBias`)
