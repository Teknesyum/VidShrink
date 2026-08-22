Tema: ffmpeg sarmalayici · kaynak: ffmpeg-sarmalayici.md

# ffmpeg sarmalayıcıları — tarama

Bakılan: argüman kurma sırası, filtre zinciri, ilerleme okuma, iptal/temizlik, hata anlamlandırma.
Künye rakamları `gh api` ile 2026-08-22'de alındı.

## kkroening/ffmpeg-python
**Künye:** Apache-2.0 · 11.007 yıldız · 525 açık issue · master son commit 2022-07-11 ·
GitHub sürümü yok, en yeni etiket v0.1.9 (2017-11-20). Fiilen bakımsız.
**Ne yapıyor:** Filtreleri yönlü çevrimsiz grafik olarak modelliyor; `topo_sort` ile düğümleri
sıralayıp `-filter_complex` metnini üretiyor. Argümanlar dört kovaya ayrılmış: global, girdi,
filtre, çıktı — sıra elde değil, kovadan geliyor. Ara akışlara `s0`, `s1` etiketi otomatik dağıtılıyor.
**Alınacak fikir:** (a) Argümanı kova kova üret, sırayı kovaların sırası belirlesin; "şu bayrak
`-i`'den önce mi sonra mı" hatası tanım gereği kalkar. (b) Geçersiz grafiği çalıştırmadan reddet —
aynı çıkıştan iki tüketici varsa `split` gerektiğini söyleyip hata atıyor. VidShrink karşılığı:
uyumsuz filtre/kodlayıcı bileşimini ffmpeg'e göndermeden yakalamak.
**Alınmayacak:** Tam DAG makinesi. VidShrink tek girdi, tek çıktı, en fazla iki filtre (scale +
HDR tonemap) kuruyor. İlerleme okuma da yok; `run_async` çıplak `Popen` döndürüp çağırana bırakıyor.
**Nereye dokunur:** `src/VidShrink.Core/FfmpegArguments.cs` (48-54. satır, `-vf` metnini düz
`string.Join(',')` ile kuruyor), `src/VidShrink.Core/ConversionArguments.cs`.

## Kaynaklar
`gh api repos/<owner>/<repo>` ve `.../releases/latest`. Kod iddiaları depoların `master`/`main`
dalındaki `_run.py`, `ffmpeg_writer.py`, `ffmpeg_reader.py`, `classes.ts`, `worker.ts`
dosyalarından okundu. Üçüncü taraf kaynak kullanılmadı, performans/kullanım iddiası aktarılmadı.
