---
name: vidshrink-fillband-ceiling-margin
description: Alt-taşma tekrarının nişanı istenen bit hızını kelepçeliyor, teslim edilen boyutu değil; bant ancak (encoder hatası + yayılım) < bant genişliği ise tutar
metadata:
  type: project
---

`PlanCalculator.Correct(fillUnderBand: true)` bitrate'i iki terimin **küçüğüne** kuruyor:
oransal büyütme (`previousVideoK * aim/actual`) ve bütçe kelepçesi (`aim` nominal boyutunu
veren bitrate). Kelepçe **istenen** bit hızını sınırlıyor, encoder'ın teslim ettiğini değil —
encoder'ın sistematik olarak talebin altında kalmasını telafi eden tek terim (oran) böylece
her zaman eziliyor. Sonuç sabit noktaya oturuyor: `aim × encoderVerimi`.

Bu yüzden aynı hedefte ikinci ve üçüncü deneme **bit-bit aynı** bitrate'i ister ve aynı boyutu
verir. "Bir deneme daha olsa tutardı" açıklamasını kabul etme; kelepçeyi elle hesapla.

Bant ancak şu eşitsizlikte tutar: `encoderHatası + yayılım ≲ bantGenişliği`.
≥50 MB sınıfında genişlik %2,8; nişan `hedef/(1+u)` tavan tarafından kuruluyor.
Ölçülen iki geçişli libx264 hatası %1,85, `CalibratedRetrySpread = 0,016` → %3,45 > %2,8,
yani ≥50 MB bandı **deterministik olarak** kaçırılıyor. Küçük hedeflerde bant geniş
(%5 / %8) olduğu için aynı kod tutturuyor — tek hedefte geçen ölçüm sınıfı kanıtlamaz.

Yayılım sabitleri birbirini tutmuyor: `PlanCalculator.CalibratedRetrySpread = 0,016` ile
`ComplexityProfile.CalibratedBand = 0,05` aynı kalibre profil için iki ayrı belirsizlik.
Retry yolu iyimser olanı kullanıyor ve kaynağı kodda yok.

**Nasıl uygula:** `Correct` / `FillBand` / `RetryAimMb` alanına dokunan her sözleşmede
(a) kelepçenin istenen mi teslim edilen mi olduğunu, (b) `u + e` toplamını bant genişliğine
karşı, (c) tetikleyici ile gerekçe metninin aynı eşiği söyleyip söylemediğini kontrol et.
Sert tavan tarafı ayrı ve sağlam: `EncodeRunner` `attempt >= MaxAttempts` + `over` iken
dosyayı siliyor, `CeilingExceeded` dönüyor.
