Tema: .NET ffmpeg sarmalayicilari · kaynak: dotnet-ffmpeg.md

# .NET için ffmpeg sarmalayıcıları — tarama

Net görüş: **VidShrink kendi sarmalayıcısında kalmalı.** EncodeRunner zaten ikisinden daha
doğru iş yapıyor (`-progress pipe:1` stdout'tan, stderr ayrı görevde drenaj,
`ReadLineAsync(ct)`). Geçmek; boyut hedefli çok denemeli döngüyü, ölçülen encoder verimini
ve atomik `.partial` çıkışı yabancı bir API'ye sığdırmak demek. Alınacak olan desen.

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
