# İş 13 — HandBrake Açığı Kesit Türüne Göre

Durum: **ölçüldü, kod değişmedi.** `handbrake-acigi-yazilim.md` düzeneği üç kesit türünde: karanlık film, hareketli
film, ekran kaydı. Aynı bayt bütçesi hedefi; HandBrake `urun-x265` kolunun ölçülen kbps'ine ayarlı.

## Düzenek

- İş akışı `.github/workflows/kalite-olcumu.yml`, koşum **35114300807** (etiket
  `olcum-kalite-handbrake+av1__karanlik+hareketli+ekran--6`), işler `olcum (<kesit>, handbrake)`.
- `karanlik`, `hareketli`: Sintel 1080p (sha256 `97F1DBC6…`), 1920x818@24, 10 sn. `hareketli` = en yüksek ortalama
  YDIF penceresi (başlangıç 325 sn, YDIF 67,14; `karanlik` 600 sn, YAVG 29,9).
- `ekran`: Netflix "Debugging" (xiph AOM CTC `b2_scc`, CC BY 4.0), 1920x1080@30, 130 kare (4,33 sn).
- Kollar, HandBrake ayarı (HandBrakeCLI 1.11.2, x265 slow, çoklu geçiş) ve negatif kontrol (yarım bit hızı) önceki
  belgeyle aynı. Tablo `python tools/kalite-paketi-3/ozet.py handbrake <dizin> karanlik,hareketli,ekran`.
- Tekrarlanabilirlik: `karanlik` kesiti önceki koşumla (35112822877) aynı; `urun-otomatik − HandBrake` VMAF-NEG farkı
  −0,65 → −0,69 ve −0,61 → −0,63 oldu.

## Sonuç

- XPSNR'da `urun-otomatik` altı satırın altısında önde: +0,69 ile +20,92 dB.
- VMAF-NEG ortalamasında `urun-otomatik` üç satırda önde (`hareketli` 600: +1,18; `ekran` 600: +12,58; `ekran` 2000:
  +5,22), üç satırda geride (`karanlik` 600: −0,69; `karanlik` 2000: −0,63; `hareketli` 2000: −0,28).
- Geride kalınan üç satırda `urun-x265` HandBrake'e −0,24, +0,50 ve +0,35 puan uzakta.
- Ekran kaydında fark büyük: `urun-x265` bile +2,32 ve +4,26 önde; SVT-AV1 kolu +12,58 ve +5,22.
- Negatif kontrol her satırda üç ölçüyü de düşürdü: VMAF-NEG −3,60 ile −22,80, XPSNR −1,98 ile −3,55,
  karanlık PSNR −1,50 ile −4,02.

Karar: HandBrake x265'e karşı kalan VMAF-NEG açığı film içeriğinde ve SVT-AV1 kolunda: `karanlik` iki bit hızında,
`hareketli` yüksek bit hızında. Ekran kaydında açık yok. Kod değişmedi; SVT-AV1 ayarının etkisi `av1-izgara.md`.
**Ölçülmedi:** donanım yolu, HandBrake'in AV1 kodlayıcısı, 10 sn'den uzun ekran kaydı.

Açık kusur: bayt bütçesi birebir değil. `ekran` 2000'de HandBrake istenen 1894,0 kbps yerine 1498,3 kbps üretti;
`urun-otomatik` 133,4, `urun-x265` 395,7 kbit/sn fazla harcadı. O satırın farkı bütçe farkını da taşıyor.
`hareketli` 2000'de otomatik kol 41,1 kbit/sn (%2,2) fazla.

## Tablo
| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | MB | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Karanlık PSNR |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | urun-x265 | libx265 | 1920x818@24 | 577,7 | 0,689 | 76,06 | 75,68 | 35,49 | 38,63 |
| karanlik | 600 | urun-svtav1 | libsvtav1 | 1920x818@24 | 588,1 | 0,701 | 75,61 | 75,09 | 36,88 | 39,13 |
| karanlik | 600 | urun-otomatik | libsvtav1 | 1920x818@24 | 588,6 | 0,702 | 75,61 | 75,09 | 36,88 | 39,13 |
| karanlik | 600 | handbrake | HandBrakeCLI x265 slow |  | 577,7 | 0,689 | 76,30 | 75,93 | 35,33 | 38,66 |
| karanlik | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 288,7 | 0,344 | 57,08 | 56,15 | 32,77 | 36,36 |
| karanlik | 2000 | urun-x265 | libx265 | 1920x818@24 | 1936,3 | 2,308 | 95,66 | 95,56 | 39,65 | 42,86 |
| karanlik | 2000 | urun-svtav1 | libsvtav1 | 1920x818@24 | 1946,0 | 2,320 | 94,53 | 94,38 | 40,18 | 42,60 |
| karanlik | 2000 | urun-otomatik | libsvtav1 | 1920x818@24 | 1942,6 | 2,316 | 94,53 | 94,38 | 40,18 | 42,60 |
| karanlik | 2000 | handbrake | HandBrakeCLI x265 slow |  | 1920,4 | 2,289 | 95,16 | 95,02 | 39,49 | 42,70 |
| karanlik | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 959,4 | 1,144 | 86,57 | 86,37 | 37,22 | 40,39 |
| hareketli | 600 | urun-x265 | libx265 | 1920x818@24 | 582,2 | 0,694 | 77,21 | 76,39 | 34,66 | 33,04 |
| hareketli | 600 | urun-svtav1 | libsvtav1 | 1920x818@24 | 580,4 | 0,692 | 78,33 | 77,25 | 35,85 | 32,85 |
| hareketli | 600 | urun-otomatik | libsvtav1 | 1920x818@24 | 582,9 | 0,695 | 78,42 | 77,35 | 35,86 | 32,86 |
| hareketli | 600 | handbrake | HandBrakeCLI x265 slow |  | 574,1 | 0,684 | 77,24 | 76,40 | 34,32 | 32,83 |
| hareketli | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 287,7 | 0,343 | 54,44 | 52,49 | 30,78 | 29,93 |
| hareketli | 2000 | urun-x265 | libx265 | 1920x818@24 | 1930,5 | 2,301 | 96,82 | 96,66 | 39,55 | 38,67 |
| hareketli | 2000 | urun-svtav1 | libsvtav1 | 1920x818@24 | 1939,7 | 2,312 | 96,18 | 95,98 | 40,06 | 37,81 |
| hareketli | 2000 | urun-otomatik | libsvtav1 | 1920x818@24 | 1938,6 | 2,311 | 96,18 | 95,98 | 40,05 | 37,81 |
| hareketli | 2000 | handbrake | HandBrakeCLI x265 slow |  | 1897,5 | 2,262 | 96,47 | 96,31 | 39,14 | 37,42 |
| hareketli | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 960,4 | 1,145 | 88,89 | 88,40 | 36,60 | 34,94 |
| ekran | 600 | urun-x265 | libx265 | 1920x1080@30 | 576,0 | 0,297 | 84,86 | 84,84 | 35,06 | 39,03 |
| ekran | 600 | urun-svtav1 | libsvtav1 | 1920x1080@30 | 584,0 | 0,302 | 95,12 | 95,11 | 46,40 | 50,31 |
| ekran | 600 | urun-otomatik | libsvtav1 | 1920x1080@30 | 584,6 | 0,302 | 95,12 | 95,10 | 46,39 | 50,34 |
| ekran | 600 | handbrake | HandBrakeCLI x265 slow |  | 558,1 | 0,288 | 82,54 | 82,51 | 33,16 | 37,53 |
| ekran | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 323,9 | 0,167 | 68,81 | 68,34 | 29,62 | 33,51 |
| ekran | 2000 | urun-x265 | libx265 | 1920x1080@30 | 1894,0 | 0,978 | 96,19 | 96,18 | 50,25 | 54,95 |
| ekran | 2000 | urun-svtav1 | libsvtav1 | 1920x1080@30 | 1631,8 | 0,843 | 97,15 | 97,15 | 58,45 | 65,25 |
| ekran | 2000 | urun-otomatik | libsvtav1 | 1920x1080@30 | 1631,7 | 0,843 | 97,15 | 97,15 | 58,45 | 65,24 |
| ekran | 2000 | handbrake | HandBrakeCLI x265 slow |  | 1498,3 | 0,774 | 91,93 | 91,92 | 37,52 | 41,47 |
| ekran | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI x265 slow |  | 845,2 | 0,437 | 88,33 | 88,31 | 35,54 | 39,97 |

| Kesit | kbit | Kol − HandBrake | Δ kbps | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR |
|---|---|---|---|---|---|---|
| karanlik | 600 | urun-otomatik | 10,9 | -0,69 | 1,55 | 0,47 |
| karanlik | 600 | urun-x265 | 0,0 | -0,24 | 0,16 | -0,03 |
| karanlik | 600 | urun-svtav1 | 10,4 | -0,69 | 1,55 | 0,47 |
| karanlik | 600 | negatif (HB yarım bit − HB) | -289,0 | -19,22 | -2,56 | -2,30 |
| karanlik | 2000 | urun-otomatik | 22,2 | -0,63 | 0,69 | -0,10 |
| karanlik | 2000 | urun-x265 | 15,9 | 0,50 | 0,17 | 0,16 |
| karanlik | 2000 | urun-svtav1 | 25,6 | -0,63 | 0,69 | -0,10 |
| karanlik | 2000 | negatif (HB yarım bit − HB) | -961,0 | -8,58 | -2,26 | -2,30 |
| hareketli | 600 | urun-otomatik | 8,8 | 1,18 | 1,54 | 0,04 |
| hareketli | 600 | urun-x265 | 8,1 | -0,03 | 0,35 | 0,21 |
| hareketli | 600 | urun-svtav1 | 6,3 | 1,08 | 1,53 | 0,02 |
| hareketli | 600 | negatif (HB yarım bit − HB) | -286,4 | -22,80 | -3,54 | -2,90 |
| hareketli | 2000 | urun-otomatik | 41,1 | -0,28 | 0,92 | 0,40 |
| hareketli | 2000 | urun-x265 | 33,0 | 0,35 | 0,42 | 1,25 |
| hareketli | 2000 | urun-svtav1 | 42,2 | -0,28 | 0,92 | 0,40 |
| hareketli | 2000 | negatif (HB yarım bit − HB) | -937,1 | -7,58 | -2,53 | -2,48 |
| ekran | 600 | urun-otomatik | 26,5 | 12,58 | 13,23 | 12,81 |
| ekran | 600 | urun-x265 | 17,9 | 2,32 | 1,89 | 1,50 |
| ekran | 600 | urun-svtav1 | 25,9 | 12,58 | 13,24 | 12,78 |
| ekran | 600 | negatif (HB yarım bit − HB) | -234,2 | -13,73 | -3,55 | -4,02 |
| ekran | 2000 | urun-otomatik | 133,4 | 5,22 | 20,92 | 23,77 |
| ekran | 2000 | urun-x265 | 395,7 | 4,26 | 12,73 | 13,48 |
| ekran | 2000 | urun-svtav1 | 133,5 | 5,22 | 20,92 | 23,79 |
| ekran | 2000 | negatif (HB yarım bit − HB) | -653,1 | -3,60 | -1,98 | -1,50 |

