---
name: vidshrink-probe-bias
description: VidShrink'in bit hızı tahmininde bilinen açık sorun — ComplexityProbe pencere seçimi bias'ı ve eksik geri besleme
metadata:
  type: project
---

VidShrink'in hedef boyut tahmini `ComplexityProbe` ile videodan kısa pencereler kodlayıp `ReferenceBppf`/`DetailExponent` çıkarmaya dayanıyor. Pencere seçimi bias sorunları yaşandı ve düzeltme global bir katsayıyla (`ComputeScanBias`) yapılıyor.

**Why:** Yanlış ölçüm doğrudan hedef boyutun kaçırılması demek; kullanıcının gördüğü tek sonuç bu. Tahmin motorunda hedef ile gerçekleşen arasındaki farkı saklayan bir kanal yok, yani sistematik sapma birikirse fark edilmiyor.

**How to apply:** Probe, plan hesabı veya kodlama akışına dokunan her öneride iki soruyu sor: (1) ölçüm kendi komşuluğuna göre mi normalize ediliyor yoksa global katsayıyla mı düzeltiliyor, (2) sonuç ölçülüp hedefle farkı kaydediliyor mu. Sınıra kırpılan her tahmin sessizce geçmemeli, adıyla loglanmalı.

Ayrıntılı gerekçe ve dış örnekler: [[reference-taramalar]] → `docs/taramalar/pyscenedetect.md`, `docs/taramalar/auto-editor.md`, `docs/taramalar/ffmpeg-normalize.md`
