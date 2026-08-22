---
name: vidshrink-ffmpeg-process-pattern
description: VidShrink'te ffmpeg/ffprobe Process kullanan kodda tekrar eden iki kusur türü — stdout'un okunmaması ve iptal kaydının eksikliği
metadata:
  type: project
---

VidShrink.Ffmpeg altında `Process` başlatan her yeni kod (QualityMeter, ComplexityProbe, ...)
aynı iki hataya düşme eğiliminde gösterdi, artık standart denetim maddesi:

1. **Pipe kilitlenmesi** — `RedirectStandardOutput` ve `RedirectStandardError` açıkken
   sadece biri okunursa (genelde stderr) öbürünün buffer'ı dolar, süreç asılı kalır.
   Doğru kalıp: `var o = p.StandardOutput.ReadToEndAsync(ct); var e = p.StandardError.ReadToEndAsync(ct);
   await Task.WhenAll(o, e); await p.WaitForExitAsync(ct);`. `-f null -` gibi stdout'a
   neredeyse hiç yazmayan çağrılarda risk düşük ama T2b'de yine de standart kalıp istendi.
2. **Yetim süreç** — `CancellationToken` iptal edildiğinde .NET tarafı `WaitForExitAsync`'ten
   çıkar ama alt `ffmpeg.exe`/`ffprobe.exe` öldürülmezse arkada kalır. Doğru kalıp:
   `using var reg = ct.Register(() => TryKill(process));` ve `TryKill` içinde
   `process.Kill(entireProcessTree: true)`.

**Neden:** Bu proje process yönetimini defalarca yeniden yazıyor (QualityMeter → ComplexityProbe →
T2b'de ffprobe packet çağrısı); her yeni `Process` kullanan builder aynı iki hatayı tekrar
üretme riski taşıyor.

**Nasıl uygula:** Yeni bir Process başlatan dosya denetime geldiğinde önce bu iki noktayı
kontrol et: (a) her iki akış da `Task.WhenAll` ile okunuyor mu, (b) `ct.Register` ile
`Kill(entireProcessTree: true)` var mı. T2b'de her iki nokta da doğru uygulanmış bulundu
(ComplexityProbe.cs SampleAsync + ReadPacketsAsync) — bu artık iyi örnek referansı olarak
kullanılabilir. T3'te de `EncodeRunner.RunCommandAsync` aynı kalıba uyuyor (stdout progress
döngüsü + ayrı stderr `Task.Run` kuyruğu + `ct.Register(() => TryKill(process))`) — üçüncü
doğrulama, artık bu dosyada bu iki madde için ayrıca risk aranmasına gerek yok, yalnızca
yeni bir Process başlatan dosya eklendiğinde kontrol listesi geçerli.

T6'da `EncoderCapabilities.ProbeEncoder` de doğru: `ct.Register` yok ama zaten `CancellationToken`
almıyor, kendi içinde `WaitForExit(4000)` + zaman aşımında `process.Kill(true)` ile kendini
kapatıyor, iki akış da `ReadToEndAsync` ile okunuyor. Kendi kendine sınırlı (bounded, dışarıdan
iptal edilmeyen) tek seferlik çağrılarda bu kalıp `ct.Register` yerine geçerli sayılabilir.

İki yeni Process çağrısı da kalıba uyuyor: `ComplexityProbe.ScanSampleAsync` (T2c) ve
`SplitSampleAsync` (T7). `ScanSampleAsync` ayrıca geçici `-vstats_file`'ı `finally`'de
siliyor — geçici dosya bırakan yeni çağrılarda bunu da kontrol et. Bu dosyalarda artık
kusur değil kalıp var; denetimde yeni eklenen Process bloğunu bul, eskisini tekrar okuma.

Ayrıca genel ders: kabul kriterinde "büyük/uzun kaynakta X saniyeyi aşmamalı, aşarsa Y
mekanizmasına düş" gibi adaptif bir davranış isteniyorsa, sadece verilen test dosyasında
ölçüm yapıp "sınırın altında kaldı" demek yetmez — Y mekanizmasının (örn. `-read_intervals`)
kod içinde gerçekten var olup olmadığı ayrıca aranmalı (T2b'de yoktu, sadece tek test
dosyasıyla "gerekmedi" denilmişti).
