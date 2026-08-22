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

## ffmpegwasm/ffmpeg.wasm
**Künye:** MIT · 17.745 yıldız · 422 açık issue · son push 2026-02-01 · son sürüm v12.15 (2025-01-07).
Depo canlı ama açık issue sayısı yüksek.
**Ne yapıyor:** ffmpeg'i WebAssembly'ye derleyip web worker içinde çalıştırıyor. Süreç yerine mesaj
kanalı var: `exec(args, timeout)`, `on("log")`, `on("progress")`.
**Alınacak fikir:** **İptali başarısızlıktan ayrı bir yol yap.** `terminate()` bekleyen tüm işleri tek
bir `ERROR_TERMINATED` işaretiyle reddediyor, worker'ı yok ediyor, `loaded` bayrağını düşürüyor —
yeniden kullanmadan önce `load()` şart. Sonuç: iptal, hatayla karışmayan ayrı bir son durum ve nesne
yarı ölü kalmıyor. VidShrink iptalde zaten `Kill(entireProcessTree)` + kısmi dosya silme yapıyor;
eksik olan, iptalin arayüze hata gibi değil iptal gibi ulaşması ve motor durumunun açıkça sıfırlanması.
**Alınmayacak:** wasm/worker mimarisinin tamamı ve tek çıkış kodu (`ret`) ile yetinmek. Kendi belgeleri
ilerlemenin "yalnızca girdi ve çıktı uzunlukları eşitken doğru" olduğunu söylüyor — VidShrink kırpma
yaptığında bu varsayım bozulur; mevcut `EffectiveDuration` hesabı daha doğru, geri adım atma.
**Nereye dokunur:** `src/VidShrink.Ffmpeg/EncodeRunner.cs` (118-126 ve 161-162. satırlardaki `catch`
blokları), `src/VidShrink.Ffmpeg/TempCleanup.cs`, `src/VidShrink.App/MainWindow.xaml.cs`.

## Kaynaklar
`gh api repos/<owner>/<repo>` ve `.../releases/latest`. Kod iddiaları depoların `master`/`main`
dalındaki `_run.py`, `ffmpeg_writer.py`, `ffmpeg_reader.py`, `classes.ts`, `worker.ts`
dosyalarından okundu. Üçüncü taraf kaynak kullanılmadı, performans/kullanım iddiası aktarılmadı.
