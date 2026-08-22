# .NET için ffmpeg sarmalayıcıları — tarama

Net görüş: **VidShrink kendi sarmalayıcısında kalmalı.** EncodeRunner zaten ikisinden daha
doğru iş yapıyor (`-progress pipe:1` stdout'tan, stderr ayrı görevde drenaj,
`ReadLineAsync(ct)`). Geçmek; boyut hedefli çok denemeli döngüyü, ölçülen encoder verimini
ve atomik `.partial` çıkışı yabancı bir API'ye sığdırmak demek. Alınacak olan desen.

## rosenbjerg/FFMpegCore
**Ne yapıyor** — akıcı argüman kurucu + süreç çalıştırıcı. MIT, 2090 yıldız, 60 açık issue,
son commit 2025-10-29. GitHub'daki son etiket v1.0.11 (2019); gerçek dağıtım NuGet'te:
5.4.0, 2025-10-27.
**Alınacak fikir** — (a) İptalde önce stdin'e `q` yazıp temiz kapanış bekleme, timeout
dolarsa `Kill()`; böylece moov atom yazılır. (b) `Pre()/During()/Post()` yaşam döngüsü:
geçici kaynağı üreten argümanın kendisi temizler, çağıran değil. (c) GIF paletini geçici
PNG'siz, tek `filter_complex` içinde `split → palettegen → paletteuse` ile üretmesi.
**Alınmayacak** — `Instances` (rosenbjerg/Instances, 28 yıldız, MIT) üzerinden süreç
soyutlaması; tek çağrı için fazladan bağımlılık. İlerlemeyi stderr'den `time=...` regex'iyle
okuması geri adım — stdout `-progress` kanalı daha sağlam. `CancellableThrough(out Action)`
deseni, CancellationToken varken gereksiz ikinci yol.
**Nereye dokunur** — `src/VidShrink.Ffmpeg/EncodeRunner.cs` (`TryKill`, `ct.Register`),
`src/VidShrink.Core/FfmpegArguments.cs` (GIF palet dalı).

## cmxl/FFmpeg.NET
**Ne yapıyor** — olay tabanlı ffmpeg sarmalayıcı. MIT, 681 yıldız, 22 açık issue, son commit
ve son etiket aynı gün: v7.4.0, 2026-03-28.
**Alınacak fikir** — İptalde stdin davranışını girdi türüne göre ayırıyor: veri stdin'den
geliyorsa stdin'i **kapatıyor**, dosyadan geliyorsa `q` **yazıyor**. VidShrink boru hattı
eklerse bu ayrım gerekecek; şimdilik sadece `q` dalı yeterli.
**Alınmayacak** — `BeginErrorReadLine()` + `ErrorDataReceived` olay modeli (ilerlemeyi UI
thread'ine kilitleme riski), `TaskCompletionSource` üzerine elle kurulmuş
`WaitForExitAsync` (.NET 8'de yerleşiği var), hata mesajını stderr'den
`messages[1] + messages[0]` diye seçmesi (VidShrink'in son 15 satır kuyruğu daha dürüst).
**Nereye dokunur** — Sadece `EncodeRunner.cs` iptal yolu.

## Ruslan-B/FFmpeg.AutoGen
**Ne yapıyor** — süreç sarmalayıcı **değil**; CppSharp ile üretilmiş, libav* C API'sine
`unsafe` P/Invoke bağlaması. MIT, 1607 yıldız, 10 açık issue, son commit 2026-08-22,
son etiket v8.0.0.1 (2026-03-14). Lisans LGPL'den MIT'e geçmiş; ffmpeg ikilileri kendi
lisanslarıyla ayrı dağıtılıyor.
**Alınacak fikir** — Sürümü hedeflenen ffmpeg sürümüne birebir eşlemesi (paket `9.0.1` =
ffmpeg 9.0.1). VidShrink `ToolLocator`'da bulduğu ffmpeg sürümünü kaydedip hata metnine
yazabilir; şu an hangi ikiliyle çalıştığı raporda yok.
**Alınmayacak** — Kütüphanenin kendisi. README açıkça "destek çok sınırlı, büyük ölçüde
kendi başınızasınız" diyor. In-process çalışmak ffmpeg çökmesini uygulama çökmesine
çevirir; iki geçişli boyut hedefleme elden yeniden yazılır.
**Nereye dokunur** — `src/VidShrink.Ffmpeg/ToolLocator.cs` (sürüm tespiti),
`EncodeRunner.cs` hata metni.

## Şüpheli yanlar
- FFMpegCore'un GitHub sürüm etiketleri 2019'dan beri güncellenmiyor; release notu ve
  kırıcı değişiklik uyarısı yok, sürüm takibi NuGet'ten yapılmak zorunda.
- cmxl/FFmpeg.NET deposunda `src/FFmpeg.NET/ffmpeg.exe` işlenmiş: **64,6 MB ikili**
  (GitHub contents API `size` alanı). Klon maliyeti ve tedarik zinciri açısından riskli.
- Üçünde de ölçülmüş performans iddiası yok; karşılaştırılabilir tek sayı yıldız.

## Kaynaklar
- `gh api repos/<owner>/<repo>` + `/releases/latest`, üç depo, 2026-08-22.
- NuGet registration API, `ffmpegcore` — 5.4.0 / 2025-10-27.
- Okunanlar: FFMpegCore `FFMpegArgumentProcessor.cs`, `IInputOutputArgument.cs`,
  `GifPaletteArgument.cs`; FFmpeg.NET `FFmpegProcess.cs`, `ProcessExtensions.cs`; AutoGen README.
