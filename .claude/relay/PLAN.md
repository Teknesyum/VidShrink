# PLAN — Motor: dinamik karar sistematiği

Kullanıcı talebi: motor küçükte de büyükte de "ilmek ilmek düşünen bir video uzmanı" gibi
davranmalı; insan için en gerekli olan neyse onu korumalı. İki somut kusur:

1. 830 MB → 1 MB'da fps kısılmadı, görüntü blok blok oldu. Motorun alt kalite tabanı yok
   ve 20 fps altına sabit 12 puanlık ceza veriyor (`PlanCalculator.cs:322`).
2. Boyut tahmini ±%8. Kullanıcı hedefin **hemen altında** durmak istiyor:
   180 MB dediyse sonuç 175–180 arası, mutlak taban 170.

Test dosyası: `C:\Users\Administrator\Videos\gothic2026-08-15 14-01-29.mp4`
— 830 MB, 52,6 sn, 1920x1080@48, h264 + aac stereo, ~132 Mbps. Çıktılar masaüstüne.

## Görev grafiği

```
T1 (bağımsız) ─────────────────────────┐
T2 → T2b → T2c ─┐
                  T3 → T4 ──────────────────────────┴─► T5
```

| # | İş | Rol / model | Bekliyor |
|---|---|---|---|
| T1 | QualityMeter (VMAF NEG harmonik + p10, XPSNR) ve ölçüm harness'i | builder / sonnet | — |
| T2 | S1 kalibrasyon probu — tahmin ±%8 → ±%3 | builder / opus | — |
| T2b | Pencere sapmasi duzeltmesi + svtav1 -maxrate hatasi | builder / opus | T2 |
| T2c | Sabit maliyetli karmasiklik taramasi | builder / opus | T2b |
| T3 | S2+S3 doluluk bandı ve alt-taşma tekrarı | builder / sonnet | T2b |
| T4 | S4 aşırı sıkıştırma uzmanlığı — bppf tabanı, rejime bağlı ceza | builder / opus | T3 |
| T5 | Gerçek dosyayla A/B ve bant doğrulaması, rapor | builder / sonnet | T1, T4 |

T2c ile T3 paralel: T2c `ComplexityProbe`/`ComplexityProfile`, T3 `PlanCalculator`/`EncodeRunner`
tutuyor, `owns` kesişmiyor. T3 ve T4 `PlanCalculator.cs`'i paylaşır, sıralı.

T3 bandı T2c bitmeden gerçek dosyada tutmaz; T3 mantığı ve testleri kurar, bandın
gerçekten tuttuğu T2c sonrası T5'te ölçülür.

## Kapsam dışı

- Yol haritasındaki P4'ün tam hâli (VMAF turnuvasıyla düzen seçimi). T1 altyapıyı kurar,
  seçimi hâlâ analitik model yapar. Turnuva ayrı bir dalga.
- P5 `IEncoderStrategy`, P6 içerik sınıflandırma ve ses motoru.
- Rakip benchmark (ab-av1, HandBrake).

## Risk

- T3'ün bandı küçük hedeflerde (1–3 MB) tutmayabilir; bant hedef büyüklüğüne göre
  gevşetiliyor, sert taban korunuyor.
- T4'ün bppf tabanları ilk değerlerdir (x264 0,035 · x265 0,025 · AV1 0,020);
  T5 ölçümü bunları oynatabilir.
- T2 her plana ~%5–8 ek süre bindirir. Kapatılabilir olmalı.
