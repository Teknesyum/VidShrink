Tema: sahne ve karmasiklik · kaynak: sahne-karmasiklik.md

# Sahne tespiti ve içerik karmaşıklığı ölçümü — tarama

Tarih: 2026-08-22. Tüm rakamlar `gh api` ve depo kaynağından, aksi belirtilmedikçe doğrulandı.

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
