Tema: .NET ffmpeg sarmalayicilari · kaynak: dotnet-ffmpeg.md

# .NET için ffmpeg sarmalayıcıları — tarama

Net görüş: **VidShrink kendi sarmalayıcısında kalmalı.** EncodeRunner zaten ikisinden daha
doğru iş yapıyor (`-progress pipe:1` stdout'tan, stderr ayrı görevde drenaj,
`ReadLineAsync(ct)`). Geçmek; boyut hedefli çok denemeli döngüyü, ölçülen encoder verimini
ve atomik `.partial` çıkışı yabancı bir API'ye sığdırmak demek. Alınacak olan desen.

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
