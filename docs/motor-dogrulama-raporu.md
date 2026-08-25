# Motor doğrulama raporu

T2-T4'te yapılan değişikliklerin gerçek dosyalar üzerinde ölçülmüş karşılaştırması.
Bu rapor kod değiştirmez; bulguları yazar.

## Kurulum

| | |
|---|---|
| Makine | DESKTOP-0J80KVV · AMD Ryzen 7 9700X (8 çekirdek) · 64 GB |
| ffmpeg | 9.0-full_build-www.gyan.dev |
| Eski motor | `d900042` — T4 birleştirmesi `ab08dde`'nin doğrudan ebeveyni |
| Yeni motor | bugünkü `main` |
| Fill kipi | `FillTarget` (ana koşu) |

T0 referans olarak `a2c87f1` demişti. `ab08dde`'nin doğrudan ebeveyni `d900042`; ikisi
arasındaki tek fark `c8cbb72` "Drop the status mirror nobody read" ve bu commit motora
dokunmuyor. Ölçüm `d900042` ile yapıldı.

Eski sürüm `git archive` ile ayrı bir klasöre çıkarıldı ve orada ayrı derlendi; `git stash`
kullanılmadı. İki motor **aynı** bench kaynağıyla derlendi, yani ölçüm farkı yalnız
`src/` farkından geliyor.

## Ölçüm yöntemi

- **VMAF NEG**: `libvmaf=model=version=vmaf_v0.6.1neg`, test akışı `zscale` ile kaynak
  çözünürlüğüne çıkarılıyor. Kare başına skorlardan harmonik ortalama ve p10 alınıyor.
- **XPSNR**: ffmpeg'in `xpsnr` filtresi tek toplam skor basmıyor, yalnız y/u/v veriyor.
  Tek skor `(4y + u + v) / 6` ağırlıklı ortalamasıyla türetiliyor — 4:2:0'da y düzleminin
  piksel sayısı u ve v'nin dört katı olduğu için. **Bu bir tercihtir**; XPSNR
  spesifikasyonunda birebir böyle tanımlandığı doğrulanamadı. Tablolarda y/u/v ayrı ayrı da
  veriliyor ki okuyan kendi ağırlığını uygulayabilsin.
- Ölçüm kodu bench içine alındı. `QualityMeter` ile aynı sayıyı verdiği doğrulandı:
  k4-ekrankaydi / 8 MB vakasında iki yol da VMAF-NEG harmonik 96,75 ve XPSNR 60,02 dedi.

## Harness'ta kapatılan boşluklar

Ölçüme başlamadan önce `tools/VidShrink.Bench` üç yerde gerçek uygulamadan farklı
davranıyordu. Üçü de kapatıldı, yoksa rapor yanlış motoru ölçmüş olurdu.

1. **`fillPolicy` geçirilmiyordu.** T3 fark etmişti. `bench shrink` artık
   `--fill filltarget|qualityceiling` alıyor ve bunu hem `PlanOptions.FillPolicy`'ye hem
   `EncodeRunner.RunAsync`'e veriyor. Her sonuç satırı hangi kipte ölçüldüğünü taşıyor.
2. **`CalibrationProbe` hiç çağrılmıyordu.** Kalibrasyonu yalnız uygulama yapıyordu; bench
   kalibrasyonsuz motoru ölçüyordu. Kalibrasyonsuz profilde `EstimateBand` 0,32, kalibre
   profilde 0,05 — yani bant tutturma ölçümü anlamsız çıkardı. Uygulamanın iki turlu
   döngüsü bench'e taşındı, `--no-calibrate` ile kapatılabiliyor.
3. **Taşınan döngüde kendi hatam vardı.** Uygulama kalibre profili planlamada tutuyor,
   `WithoutCalibration()`'ı yalnız bir sonraki turun *girdisi* olarak kullanıyor. İlk
   taşımada ikisini tek değişkende birleştirmiştim; tur sayısı dolunca kalibrasyon
   atılıyordu ve her satır `kalibre=hayır` görünüyordu.

### Kalibrasyon neden düşük görünüyordu

Sentetik kliplerde kalibrasyonun içerik yüzünden düştüğü **doğru değil**. Üçüncü maddedeki
port hatasıydı; düzeltildikten sonra aynı vaka `kalibre=hayır` → `kalibre=evet` oldu.

İçerik ihtimali ölçerek elendi. `ComplexityProfile.Calibrate`'in tek içerik bağımlı reddi
`lowBppf <= highBppf`, yani iki CRF noktası arasında dosya boyutunun düşmemesi.
`CalibrationProbe`'un örnekleme adımı birebir taklit edildi (2 saniyelik üç pencere,
plan çözünürlüğü, libx264/slow) ve k4-ekrankaydi'nde altı CRF çiftinde de tepki tekdüze:

| crf çifti | düşük CRF bayt | yüksek CRF bayt | düşüyor mu |
|---|---|---|---|
| 20 / 24 | 659 456 | 441 344 | evet |
| 28 / 32 | 301 056 | 229 376 | evet |
| 36 / 40 | 189 440 | 169 984 | evet |
| 38 / 42 | 179 200 | 128 000 | evet |
| 42 / 46 | 128 000 | 71 680 | evet |
| 46 / 50 | 71 680 | 54 272 | evet |

`bench panel --only o3` örneklerin çalıştığını ayrıca doğruladı: k4 için 576 kare / 219 fps,
k1 için 576 kare / 78 fps. Yani örnekler ne başarısız oluyordu ne de tepki düzdü.
