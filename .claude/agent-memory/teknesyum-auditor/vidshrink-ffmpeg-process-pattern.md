---
name: vidshrink-ffmpeg-process-pattern
description: vidshrink projesinde ffmpeg Process kullanımlarında tekrar eden stdout okunmaması / iptalde süreç öldürülmemesi kusuru
metadata:
  type: project
---

vidshrink'te `ToolLocator.StartInfo` her zaman hem `RedirectStandardOutput` hem
`RedirectStandardError` açar. T1'de `QualityMeter.cs RunFilterAsync` yalnızca
stderr okuyup stdout'u hiç tüketmeden `WaitForExitAsync` çağırıyordu — 25 dakika
kaybettirdi. O dosya sonradan düzeltildi: şimdi `RunFilterAsync` (satır 110-118)
hem `StandardOutput.ReadToEndAsync(ct)` hem `StandardError.ReadToEndAsync(ct)`'i
`Task.WhenAll` ile okuyor, AYRICA `ct.Register(() => TryKill(process))` ile iptalde
alt süreci öldürüyor.

**T2 denetiminde (2026-08-19) çıkan nüans:** `-f null -` (null muxer) hedefine
yazan probe çağrılarında (`ComplexityProbe.cs`, `CalibrationProbe.cs`) ffmpeg
stdout'a fiilen 0 bayt yazıyor — null muxer veriyi atıyor, `video:`/`frame=`
raporu stderr'e gidiyor. Yani bu özel durumda gerçek pipe-buffer deadlock riski
neredeyse yok; "sadece stderr okunuyor" tek başına KRİTİK sayılmamalı.

Asıl tekrar eden gerçek kusur şu: `ct.Register(() => TryKill(process))` YOKSA,
kullanıcı iptal ettiğinde `ReadToEndAsync(ct)`/`WaitForExitAsync(ct)` sadece
.NET tarafındaki beklemeyi iptal eder, alt ffmpeg.exe süreci öldürülmez —
arka planda yetim süreç olarak çalışmaya devam eder. `CalibrationProbe.SampleAsync`
(T2, `src/VidShrink.Ffmpeg/CalibrationProbe.cs`) bunu QualityMeter'ın düzeltilmiş
kalıbını değil, ComplexityProbe.cs'in eski/düzeltilmemiş kalıbını kopyalayarak
tekrarladı (sözleşme T2'de açıkça "ComplexityProbe kalıbını kullan" dediği için).

**Why:** Bu, ekibin kendi T1 düzeltmesiyle çelişen, tespit edilmesi kolay ama
sözleşmelerde K-maddesi olarak yazılmayan bir tutarlılık kusuru — build/test
yeşil geçer, çökme olmaz, ama denetimde gözden kaçırılırsa birikir.

**How to apply:** Her yeni ffmpeg `Process` sarmalayıcısında iki ayrı şeyi kontrol et:
(1) her iki stream okunuyor mu — `-f null -` ise KRİTİK değil ama tutarsızlık notu;
gerçek çıktı üreten formatlarda (mp4/dosyaya yazan) hâlâ KRİTİK.
(2) `CancellationToken` alan her Process çağrısında `ct.Register(() => Kill(process))`
(veya eşdeğeri) var mı — yoksa ÖNEMLİ bulgu: iptalde yetim süreç kalır. `QualityMeter.TryKill`
örnek/referans kalıp.
