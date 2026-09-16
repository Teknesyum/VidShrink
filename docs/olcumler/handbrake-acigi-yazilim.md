# Paket 3 — HandBrake Açığı, Yazılım Yolu (Kalem 9)

Durum: **ölçüldü, kod değişmedi.** Kayıtlı 8,79 VMAF-NEG açığı (`handbrake-acigi.md`) eski `av1_nvenc` çıktısından,
psy anahtarlarından önce ölçülmüştü. Bu tur yazılım yolunu, psy anahtarları açıkken, aynı bayt bütçesinde ölçtü.

## Düzenek

- İş akışı `.github/workflows/kalite-olcumu.yml`, koşum **35112822877** (etiket `olcum-kalite-handbrake--4`),
  işler `olcum (karanlik|parlak|orta, handbrake)`.
- Betik `tools/kalite-paketi-3/kos.ps1 -Is handbrake`, tablo `tools/kalite-paketi-3/ozet.py handbrake`.
- Kaynak ve kesitler `whatsapp-karanlik.md` ile aynı (Sintel 1080p, 10 sn ffv1 kesitler).
- Ürün kolları `bench shrink --speed quality --no-measure`: `urun-otomatik` (kodlayıcıyı motor seçer; üç kesitte de
  libsvtav1 seçti), `urun-x265` ve `urun-svtav1` (`--force-codec`, çözünürlük ve kare hızı düşürme kapalı).
  Hedef MB = kbit × 10 sn.
- HandBrakeCLI 1.11.2 (sha256 pinli): `H.265 MKV 1080p30`, x265 slow, `--multi-pass --turbo`, kaynak boyutu ve hızı,
  bit hızı = `urun-x265` kolunun ölçülen kbps'i.
- Negatif kontrol: aynı HandBrake ayarı yarım bit hızında.
- Ölçü: `bench measure-pair --fps 24/1`.

## Sonuç

- XPSNR'da `urun-otomatik` altı satırın altısında HandBrake'in önünde: +0,45 ile +1,72 dB.
- VMAF-NEG ortalamasında `urun-otomatik` dört satırda önde (+0,75 ile +2,11), `karanlik` kesitinin iki satırında
  geride (−0,65 ve −0,61), üstelik o iki satırda 11,8 ve 23,6 kbit/sn daha fazla harcayarak.
- Aynı `karanlik` satırlarında `urun-x265` HandBrake'e −0,18 ve +0,50 puan uzakta; karanlık kesitteki geri kalış
  SVT-AV1 seçiminden geliyor, x265 yolundan değil.
- Karanlık PSNR'da `parlak` kesitinde üç ürün kolu da HandBrake'in gerisinde (−0,91 ile −1,40 dB, iki bit hızında).
- Negatif kontrol her satırda üç ölçüyü de düşürdü: VMAF-NEG −1,55 ile −19,31, XPSNR −1,03 ile −2,55,
  karanlık PSNR −1,33 ile −2,89.

Karar: yazılım yolunda 8,79'luk açık yok; kalan açık `karanlik` kesitinde SVT-AV1'in VMAF-NEG'de 0,61–0,65 puan
geride kalması. Kod değişmedi; kodlayıcı seçimi ve SVT-AV1 ayarı 13. işin (AV1 sistematiği) konusu.
**Ölçülmedi:** donanım yolu (`av1_nvenc`, CI'da GPU yok), HandBrake'in kendi AV1 kodlayıcısı.

Açık kusur: bayt bütçesi birebir değil. HandBrake `urun-x265`'in kbps'ine ayarlandı ama `urun-otomatik` ile fark
−75,4 ile +136,1 kbit/sn arasında (tabloda "Δ kbps"); `orta` 2000 satırında otomatik kol %7 fazla harcadı.

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | MB | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Karanlık PSNR |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | urun-x265 | libx265 | 1920x818@24 | 577,1 | 0,688 | 76,07 | 75,69 | 35,53 | 38,65 |
| karanlik | 600 | urun-svtav1 | libsvtav1 | 1920x818@24 | 587,2 | 0,700 | 75,61 | 75,09 | 36,89 | 39,13 |
| karanlik | 600 | urun-otomatik | libsvtav1 | 1920x818@24 | 588,0 | 0,701 | 75,61 | 75,08 | 36,88 | 39,13 |
| karanlik | 600 | handbrake | HandBrakeCLI x265 slow |  | 576,2 | 0,687 | 76,25 | 75,88 | 35,36 | 38,66 |
| karanlik | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 287,7 | 0,343 | 56,94 | 56,03 | 32,81 | 36,33 |
| karanlik | 2000 | urun-x265 | libx265 | 1920x818@24 | 1936,6 | 2,309 | 95,67 | 95,56 | 39,66 | 42,86 |
| karanlik | 2000 | urun-svtav1 | libsvtav1 | 1920x818@24 | 1943,4 | 2,317 | 94,55 | 94,40 | 40,18 | 42,60 |
| karanlik | 2000 | urun-otomatik | libsvtav1 | 1920x818@24 | 1944,6 | 2,318 | 94,56 | 94,41 | 40,18 | 42,60 |
| karanlik | 2000 | handbrake | HandBrakeCLI x265 slow |  | 1921,0 | 2,290 | 95,17 | 95,03 | 39,48 | 42,69 |
| karanlik | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 959,4 | 1,144 | 86,57 | 86,37 | 37,22 | 40,39 |
| parlak | 600 | urun-x265 | libx265 | 1920x818@24 | 592,6 | 0,706 | 80,70 | 80,41 | 35,50 | 32,49 |
| parlak | 600 | urun-svtav1 | libsvtav1 | 1920x818@24 | 584,5 | 0,697 | 81,91 | 81,30 | 36,92 | 32,97 |
| parlak | 600 | urun-otomatik | libsvtav1 | 1920x818@24 | 584,6 | 0,697 | 81,91 | 81,30 | 36,92 | 32,97 |
| parlak | 600 | handbrake | HandBrakeCLI x265 slow |  | 612,4 | 0,730 | 79,80 | 79,59 | 35,20 | 33,89 |
| parlak | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 306,7 | 0,366 | 66,02 | 65,31 | 32,72 | 31,00 |
| parlak | 2000 | urun-x265 | libx265 | 1920x818@24 | 1966,9 | 2,345 | 92,66 | 92,62 | 39,28 | 37,46 |
| parlak | 2000 | urun-svtav1 | libsvtav1 | 1920x818@24 | 1950,3 | 2,325 | 92,25 | 92,19 | 39,57 | 37,41 |
| parlak | 2000 | urun-otomatik | libsvtav1 | 1920x818@24 | 1939,8 | 2,312 | 92,25 | 92,19 | 39,57 | 37,39 |
| parlak | 2000 | handbrake | HandBrakeCLI x265 slow |  | 2015,2 | 2,402 | 91,26 | 91,16 | 38,80 | 38,64 |
| parlak | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 1009,3 | 1,203 | 86,14 | 86,01 | 36,91 | 35,99 |
| orta | 600 | urun-x265 | libx265 | 1920x818@24 | 579,3 | 0,691 | 92,21 | 92,16 | 39,48 | 45,96 |
| orta | 600 | urun-svtav1 | libsvtav1 | 1920x818@24 | 588,9 | 0,702 | 92,81 | 92,76 | 40,45 | 46,27 |
| orta | 600 | urun-otomatik | libsvtav1 | 1920x818@24 | 589,1 | 0,702 | 92,81 | 92,76 | 40,45 | 46,27 |
| orta | 600 | handbrake | HandBrakeCLI x265 slow |  | 570,6 | 0,680 | 91,66 | 91,59 | 39,24 | 45,80 |
| orta | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 286,7 | 0,342 | 87,04 | 86,89 | 37,55 | 44,34 |
| orta | 2000 | urun-x265 | libx265 | 1920x818@24 | 1911,9 | 2,279 | 95,80 | 95,76 | 41,60 | 48,36 |
| orta | 2000 | urun-svtav1 | libsvtav1 | 1920x818@24 | 1984,5 | 2,366 | 95,71 | 95,68 | 41,60 | 48,07 |
| orta | 2000 | urun-otomatik | libsvtav1 | 1920x818@24 | 1999,0 | 2,383 | 95,73 | 95,69 | 41,60 | 48,06 |
| orta | 2000 | handbrake | HandBrakeCLI x265 slow |  | 1862,9 | 2,221 | 94,97 | 94,91 | 41,15 | 48,15 |
| orta | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 937,3 | 1,117 | 93,43 | 93,36 | 40,12 | 46,82 |

| Kesit | kbit | Kol − HandBrake | Δ kbps | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR |
|---|---|---|---|---|---|---|
| karanlik | 600 | urun-otomatik | 11,8 | -0,65 | 1,52 | 0,47 |
| karanlik | 600 | urun-x265 | 0,9 | -0,18 | 0,17 | -0,01 |
| karanlik | 600 | urun-svtav1 | 11,0 | -0,64 | 1,53 | 0,47 |
| karanlik | 600 | negatif (HB yarım bit − HB) | -288,5 | -19,31 | -2,55 | -2,33 |
| karanlik | 2000 | urun-otomatik | 23,6 | -0,61 | 0,70 | -0,10 |
| karanlik | 2000 | urun-x265 | 15,6 | 0,50 | 0,18 | 0,17 |
| karanlik | 2000 | urun-svtav1 | 22,4 | -0,62 | 0,70 | -0,09 |
| karanlik | 2000 | negatif (HB yarım bit − HB) | -961,6 | -8,59 | -2,25 | -2,30 |
| parlak | 600 | urun-otomatik | -27,8 | 2,11 | 1,72 | -0,91 |
| parlak | 600 | urun-x265 | -19,8 | 0,90 | 0,30 | -1,40 |
| parlak | 600 | urun-svtav1 | -27,9 | 2,11 | 1,72 | -0,91 |
| parlak | 600 | negatif (HB yarım bit − HB) | -305,7 | -13,78 | -2,48 | -2,89 |
| parlak | 2000 | urun-otomatik | -75,4 | 1,00 | 0,77 | -1,25 |
| parlak | 2000 | urun-x265 | -48,3 | 1,40 | 0,48 | -1,17 |
| parlak | 2000 | urun-svtav1 | -64,9 | 0,99 | 0,77 | -1,23 |
| parlak | 2000 | negatif (HB yarım bit − HB) | -1005,9 | -5,11 | -1,89 | -2,65 |
| orta | 600 | urun-otomatik | 18,5 | 1,15 | 1,21 | 0,47 |
| orta | 600 | urun-x265 | 8,7 | 0,55 | 0,24 | 0,16 |
| orta | 600 | urun-svtav1 | 18,3 | 1,15 | 1,21 | 0,47 |
| orta | 600 | negatif (HB yarım bit − HB) | -283,9 | -4,63 | -1,69 | -1,46 |
| orta | 2000 | urun-otomatik | 136,1 | 0,75 | 0,45 | -0,08 |
| orta | 2000 | urun-x265 | 49,0 | 0,82 | 0,45 | 0,21 |
| orta | 2000 | urun-svtav1 | 121,6 | 0,74 | 0,45 | -0,08 |
| orta | 2000 | negatif (HB yarım bit − HB) | -925,6 | -1,55 | -1,03 | -1,33 |
