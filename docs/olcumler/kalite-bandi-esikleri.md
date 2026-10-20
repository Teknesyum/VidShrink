# Kalite Bandı Eşikleri Ölçümü

Tarih: 2026-10-20. Sahibi: later 122, dal `worktree-agent-later-122`.

## Ne Ölçüldü

`Ffmpeg/OutputQualityCheck.BandOf` eşikleri: VMAF 93/85/70, SSIM 0.98/0.95/0.90, PSNR 42/37/32
(saydam / iyi / görünür, altı zayıf). Soru iki tane:

1. VMAF eşikleri bir kodlama merdivenini dört banda anlamlı yayıyor mu?
2. libvmaf yokken devreye giren SSIM ve PSNR bandı, aynı dosyada VMAF bandını tutuyor mu?

Üç sentetik klip vardı: `testsrc2`, `mandelbrot` ve `noise=alls=24` eklenmiş `testsrc2`. Hepsi 640x360, 24 kare/sn, 4 sn.
Kaynaklar kayıpsız (`libx264 -qp 0`) üretildi. Her klip libx264 `medium` ile CRF 14/18/23/28/33/38/43
merdiveninden geçirildi. Her çıktı `OutputQualityCheck.MeasureAsync` ile üç ölçütte ayrı ayrı ölçüldü. Bütün ffmpeg
çağrıları `-threads 2` ile yapıldı.

Komut: `dotnet run --project tools/VidShrink.Bench -c Release -- quality-bands <klasör>`. ffmpeg 9.0 (gyan.dev).

## Sonuç

| Klip | CRF | kbps | VMAF | SSIM | PSNR | Bant VMAF | Bant SSIM | Bant PSNR |
|---|---|---|---|---|---|---|---|---|
| testsrc2 | 14 | 1321 | 99.34 | 0.9992 | 53.50 | Saydam | Saydam | Saydam |
| testsrc2 | 18 | 1045 | 98.72 | 0.9984 | 49.81 | Saydam | Saydam | Saydam |
| testsrc2 | 23 | 723 | 97.01 | 0.9957 | 44.91 | Saydam | Saydam | Saydam |
| testsrc2 | 28 | 416 | 93.69 | 0.9878 | 39.84 | Saydam | Saydam | İyi |
| testsrc2 | 33 | 247 | 87.51 | 0.9769 | 36.51 | İyi | İyi | Görünür |
| testsrc2 | 38 | 195 | 78.07 | 0.9646 | 34.18 | Görünür | İyi | Görünür |
| testsrc2 | 43 | 146 | 62.28 | 0.9460 | 31.77 | Zayıf | Görünür | Zayıf |
| mandelbrot | 14 | 2945 | 95.55 | 0.9958 | 47.52 | Saydam | Saydam | Saydam |
| mandelbrot | 18 | 2075 | 93.38 | 0.9941 | 44.42 | Saydam | Saydam | Saydam |
| mandelbrot | 23 | 1240 | 88.70 | 0.9897 | 40.25 | İyi | Saydam | İyi |
| mandelbrot | 28 | 600 | 80.39 | 0.9806 | 36.12 | Görünür | Saydam | Görünür |
| mandelbrot | 33 | 220 | 67.90 | 0.9646 | 32.89 | Zayıf | İyi | Görünür |
| mandelbrot | 38 | 95 | 49.86 | 0.9424 | 31.05 | Zayıf | Görünür | Zayıf |
| mandelbrot | 43 | 62 | 21.45 | 0.9155 | 29.63 | Zayıf | Görünür | Zayıf |
| gurultu | 14 | 21583 | 99.79 | 0.9825 | 42.67 | Saydam | Saydam | Saydam |
| gurultu | 18 | 15153 | 97.67 | 0.9486 | 38.01 | Saydam | Görünür | İyi |
| gurultu | 23 | 7072 | 93.36 | 0.8303 | 33.33 | Saydam | Zayıf | Görünür |
| gurultu | 28 | 615 | 89.04 | 0.6178 | 30.74 | İyi | Zayıf | Zayıf |
| gurultu | 33 | 257 | 81.75 | 0.6030 | 29.88 | Görünür | Zayıf | Zayıf |
| gurultu | 38 | 185 | 69.73 | 0.5894 | 29.01 | Zayıf | Zayıf | Zayıf |
| gurultu | 43 | 137 | 49.66 | 0.5712 | 27.68 | Zayıf | Zayıf | Zayıf |

Aynı bantta: SSIM–VMAF 10/21 (bir basamak içinde 16/21), PSNR–VMAF 14/21 (bir basamak içinde 19/21).

## Eşik Araması

Bant eşikleri bu 21 satıra ızgarayla uyduruldu. Üç klipten biri dışarıda bırakıldı, eşikler kalan ikisinde seçildi ve
dışarıdaki klipte sayıldı:

| Ölçüt | Dışarıdaki klip | Seçilen eşik | Seçilenle tutan | Mevcutla tutan |
|---|---|---|---|---|
| SSIM | testsrc2 | 0.990/0.985/0.970 | 4/7 | 5/7 |
| SSIM | mandelbrot | 0.977/0.965/0.950 | 4/7 | 2/7 |
| SSIM | gurultu | 0.977/0.965/0.950 | 3/7 | 3/7 |
| PSNR | testsrc2 | 38/37/33 | 6/7 | 5/7 |
| PSNR | mandelbrot | 37/35/32 | 4/7 | 6/7 |
| PSNR | gurultu | 37/35/33 | 4/7 | 3/7 |

Dışarıdaki klipte toplam: SSIM için seçilen 11/21, mevcut 10/21. PSNR için seçilen 14/21, mevcut 14/21.

## Karar

**Eşikler değişmedi.**

- **VMAF 93/85/70.** Merdiven her klipte dört bandın hepsinden sırayla geçiyor; hiçbir klipte bant geri dönmüyor.
  CRF 18 üç klipte de saydam çıktı (98.72 / 93.38 / 97.67). En zor klipte (mandelbrot) CRF 18 ile 23 arasında saydamdan
  iyiye düşüyor. Libx264'ün varsayılanı CRF 23; kolay klipte saydam, zor klipte iyi. Eşiği oynatacak bir bulgu yok.
- **SSIM ve PSNR.** Bu üç klipte uydurulan eşik, dışarıda bırakılan klipte mevcut eşikten anlamlı bir fark göstermiyor
  (SSIM +1 satır, PSNR 0). Eşiği değiştirmek üç sentetik klibe uydurmak olurdu.
  Uyuşmazlığın asıl kaynağı eşik değil ölçütün kendisi:
  - Gürültülü klipte SSIM, CRF 23'te 0.83'e düşüyor. Kodlayıcı tahıl gibi rastgele gürültüyü yeniden üretmiyor, SSIM
    bunu yapı kaybı sayıyor.
  - Mandelbrot'ta SSIM, VMAF'ın "görünür" dediği CRF 28'de hâlâ 0.98.

  Bu yüzden yedek yol bilerek kaba kalıyor: libvmaf varken arayüz VMAF'ı seçiyor (`OutputQualityCheck.Pick`).

## Sınır

Klipler sentetik ve kısa. Gerçek çekim, karanlık sahne ve canlandırma ölçülmedi. VMAF eşiklerinin algıyla ilişkisi
bu ölçümün sorusu değil; ölçüm yalnız eşiklerin bir CRF merdivenini tutarlı ayırıp ayırmadığını sınıyor.
