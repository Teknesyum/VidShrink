---
name: vidshrink-dogrulama
description: VidShrink'te tahmin doğruluğunu ölçmenin tek yolu scratchpad'e ayrı bir harness yazmak — bench aracı kalibrasyon adımını çalıştırmıyor
metadata:
  type: project
---

VidShrink'te "tahmin/gerçek farkı" ölçmek gerektiğinde `tools/VidShrink.Bench` yeterli
değil: `bench shrink` yalnızca `ComplexityProbe` çalıştırıyor, uygulamanın gerçek yolundaki
`CalibrationProbe` adımını atlıyor ve her hedef için VMAF ölçüyor (yavaş). Scratchpad'e
`VidShrink.Core` + `VidShrink.Ffmpeg` projelerine referans veren küçük bir konsol uygulaması
yazıp `ComplexityProbe → PlanCalculator.BuildDetailed → CalibrationProbe → BuildDetailed →
FfmpegArguments.Build` zincirini elle koşturmak hem hızlı hem de MainWindow'daki akışın
aynısı.

**Why:** T2b'de ölçüm bu yüzden iki kez yapıldı; bench sayıları uygulamanınkiyle
uyuşmuyordu.

**How to apply:** Kalibrasyon/tahmin doğruluğu iddia eden her sözleşmede doğrulamayı bu
harness ile yap, `bench` çıktısına dayanma. Build/test komutları sorunsuz:
`dotnet build VidShrink.sln -c Release`, `dotnet test VidShrink.sln`.
