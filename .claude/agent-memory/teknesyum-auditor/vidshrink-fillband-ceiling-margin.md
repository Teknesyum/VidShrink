---
name: vidshrink-fillband-ceiling-margin
description: FillBand marjları birbirine geçmiş — Correct() düzeltme hedefleri bandın altına düşüyor; her Correct/FillBand dokunuşunda tekrar kontrol et
metadata:
  type: project
---

`PlanCalculator.Correct()` iki yönlü düzeltme yapıyor ve her iki yön de sabit bir marjla
hedefin altına nişan alıyor. Marjlar `FillBand` sınırlarıyla uyumsuz:

- Alt-taşma dalı (`fillUnderBand: true`) tavanı `UnderBandRetryCapMargin = 0.98`.
  ≥50 MB sınıfında bant alt sınırı hedefin %97,2'si → nişan ile bant alt sınırı arasında
  yalnız **%0,8** boşluk var. Aynı dosyadaki `TwoPassUncertainty = 0.04` iki geçişli VBR'ın
  ±%4 ıskaladığını söylüyor. Yani düzeltme denemesi banda ancak dar bir aralıkta oturur.
- Üst-taşma dalı `CrfFitMargin = 0.94` kullanıyor; ≥50 MB sınıfının sert tabanı %94,4.
  **Üst-taşma düzeltmesi tasarımı gereği sert tabanın altına nişan alıyor** — bir kez
  tavan düzeltmesi çalıştıysa sonuç bandın içine düşemez.

**Neden önemli:** Bant kaçırıldığında builder'lar bunu kalibrasyona (`ComplexityProfile`,
T2c) bağlama eğiliminde. `Correct()` profil almıyor ve döndürdüğü plan 2-pass bitrate
hedefli — kalibrasyon o denemenin isabetini etkilemiyor. Düzeltme denemesinden çıkan
bant kaçırması **marjdan** gelir, kalibrasyondan değil.

T7 üçüncü örnek: donanım yolunda kalibrasyon düşerse iki geçişli bitrate `1.06` ile
çarpılıyor (`PlanCalculator.HardwareUncalibratedBias`). Bu, planı bilerek hedefin ~%6
üstüne nişanlıyor; sert tavan yalnızca `EncodeRunner`'ın `over` kontrolüyle korunuyor,
plan tarafında değil. Yani "plan hedefi aşıyor" ile "program hedefi aşıyor" ayrı sorular —
tavan sözünü denetlerken hep runner'daki `over` + `MaxAttempts` dalına bak.

**Nasıl uygula:** `Correct` / `FillBand` / `CrfFitMargin` alanına dokunan her sözleşmede
iki marjı bant sınıflarına karşı elle hesapla. Bant kaçıran gerçek ölçüm raporlanmışsa
"başka T'nin işi" açıklamasını deneme-deneme izine bakmadan kabul etme; iz verilmemişse
`? kanıtsız` işaretle. Sert tavan tarafı ayrı: `EncodeRunner.cs` artık `attempt >= MaxAttempts`
ve `over` iken dosyayı silip `CeilingExceeded` dönüyor, tavan sözü kodda tutuluyor.
