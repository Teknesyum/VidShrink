# Ölçerek Arama: Hedef Sapması

Tarih: 2026-10-20. Düzenek: `tools/VidShrink.Ab`, `ab ara --kaynak <yol> --kalite <puan> --threads 2`.
Süreç iki çekirdeğe bağlı (affinity), kodlayıcı `-threads 2`. Ölçek VMAF-NEG ortalaması.

Klip: sentetik, `testsrc2=size=1280x720:rate=30:duration=8,noise=alls=12:allf=t`, x264 crf 12 (103 MB).
Başlangıç hedefi modelin `TargetMbForQuality` sonucudur; birinci tur, kutu kapalıyken kullanılan hedefin ölçümüdür.

## İstenen 80

| Tur | MB | Ölçülen | Sapma | Geçen |
|---|---|---|---|---|
| 1 (model) | 9.673 | 87.17 | +7.17 | 21.5 sn |
| 2 | 4.228 | 79.42 | -0.58 | 43.7 sn |

Sonuç: 2 turda yakınsadı, sapma +7.17'den -0.58'e indi, dosya 9.67 MB yerine 4.23 MB.

## İstenen 70

| Tur | MB | Ölçülen | Sapma | Geçen |
|---|---|---|---|---|
| 1 (model) | 3.046 | 77.96 | +7.96 | 18.2 sn |
| 2 | 1.214 | 84.76 | +14.76 | 32.4 sn |
| 3 | 0.330 | 59.06 | -10.94 | 45.2 sn |
| 4 | 0.772 | 75.73 | +5.73 | 60.4 sn |

Sonuç: 4 tur sınırında durdu, yakınsamadı; istenen puana ulaşan en küçük hedef seçildi (0.772 MB, +5.73).
Bu bölgede puan MB ile tekdüze değil (1.214 MB, 3.046 MB'tan yüksek ölçüldü); plan küçük hedefte
başka bir kola geçiyor olabilir, ölçülmedi. Kıskaç araması tekdüzelik varsayar.

## Sınırlar

- Örnek kodlama tek geçişlidir (`-b:v` ya da CRF); gerçek kodlama iki geçişli olabilir.
- Tek sentetik klip, iki hedef puan. Sabit dosya kümesiyle yan yana ölçüm yapılmadı.
