---
name: karmasiklik-olcum-hizi
description: VidShrink'in tekrar eden darboğazı — karmaşıklık ölçümü için pencere kodlama hızı; 2026-08-22 frameserver taramasının vardığı yön
metadata:
  type: project
---

VidShrink hedef boyuta sıkıştırmak için videodan pencereler kesip kodluyor ve bu ölçümün
hızı sürekli gündemde. 2026-08-22'de frameserver teması tarandı (FFmpeg, VapourSynth,
AviSynth+); rapor `docs/taramalar/ffmpeg.md`, `docs/taramalar/vapoursynth.md`, `docs/taramalar/avisynthplus.md`.

**Why:** Bugünkü tasarım pencere başına ayrı ffmpeg süreci + ayrı `-ss` kullanıyor, yani
girdi N kez çözülüyor. Tarama, tek çözme geçişiyle çok çıkış üretmenin (`select=n=K`,
`trim`+`concat`) ve pencere boyutunu geçici dosya yerine `-stats_enc_post` `{size}`
toplamından okumanın mümkün olduğunu gösterdi.

**How to apply:** Ölçüm hızı konusu tekrar açıldığında sıfırdan araştırma yapma, önce o
raporu oku. VapourSynth/AviSynth'i bağımlılık olarak önerme — WPF kurulumuna Python ya da
DLL kaydı yükü kabul edilebilir değil, alınan şey desen. Ölçümde `-noaccurate_seek`
önerme: pencereyi kaydırır, ölçüm yanlılaşır.
