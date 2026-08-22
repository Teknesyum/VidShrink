Tema: ffmpeg sarmalayici · kaynak: ffmpeg-sarmalayici.md

# ffmpeg sarmalayıcıları — tarama

Bakılan: argüman kurma sırası, filtre zinciri, ilerleme okuma, iptal/temizlik, hata anlamlandırma.
Künye rakamları `gh api` ile 2026-08-22'de alındı.

## Zulko/moviepy
**Künye:** MIT · 14.860 yıldız · 93 açık issue · son push 2026-08-11 · son sürüm v2.2.1 (2025-05-21).
**Ne yapıyor:** ffmpeg'i hem okuyucu hem yazıcı olarak sarıyor, ham kareleri boruyla geçiriyor.
`FFmpegInfosParser` stderr çıktısını yapılandırılmış meta veriye çeviriyor.
**Alınacak fikir:** (a) **stderr → eyleme dönüşebilir mesaj tablosu.** Hata anında stderr'i tarayıp
"Unknown encoder", "incorrect codec parameters ?", "bitrate not specified", "Invalid encoder type"
kalıplarını yakalıyor, her birine kullanıcının ne yapması gerektiğini ekliyor. VidShrink bugün
15 satırlık ham stderr kuyruğu fırlatıyor — en ucuz kazanç bu.
(b) **`decode_file` seçeneği:** konteyner başlığındaki `Duration:` yalan söyleyebildiği için isteğe
bağlı olarak dosyayı baştan sona çözüp son `time=` değerini gerçek süre sayıyor. VidShrink'te süre
hem hedef bit hızını hem yüzde ilerlemeyi belirliyor; yanlış süre sessizce yanlış boyut üretir.
Maliyet: ek tam çözme geçişi — varsayılan değil, kaçamak yolu olmalı.
**Alınmayacak:** Kareleri süreç dışına boruyla taşıma modeli; VidShrink saf ffmpeg zinciri, araya
bellek kopyası koymanın anlamı yok. `proglog` benzeri ayrı günlük katmanı da gereksiz — VidShrink
zaten `-progress pipe:1` kullanıyor, o daha sağlam.
**Nereye dokunur:** `src/VidShrink.Ffmpeg/EncodeRunner.cs` (224. satırdaki ham stderr
`InvalidOperationException`), `src/VidShrink.Ffmpeg/FfprobeClient.cs` (süre doğrulama),
`src/VidShrink.App/LanguageCatalog.cs` (mesaj çevirisi).

## Kaynaklar
`gh api repos/<owner>/<repo>` ve `.../releases/latest`. Kod iddiaları depoların `master`/`main`
dalındaki `_run.py`, `ffmpeg_writer.py`, `ffmpeg_reader.py`, `classes.ts`, `worker.ts`
dosyalarından okundu. Üçüncü taraf kaynak kullanılmadı, performans/kullanım iddiası aktarılmadı.
