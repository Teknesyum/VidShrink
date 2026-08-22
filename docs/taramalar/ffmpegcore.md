Tema: .NET ffmpeg sarmalayicilari · kaynak: dotnet-ffmpeg.md

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
