# B1 — HandBrake 1.11.2 Kıyası, SDR Dört Kesit

Durum: **ölçüldü, kod değişmedi.** Ürün (Bench yolu, otomatik codec) ile HandBrakeCLI 1.11.2 aynı bayta
oturtuldu, dört kesit ve iki bit hızında VMAF-NEG, XPSNR, SSIM, CAMBI ve karanlık PSNR ölçüldü.

## Düzenek

- İş akışı `.github/workflows/handbrake-kiyas.yml`, koşum **35158725446**, etiket
  `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit `02993179`, işler
  `olcum (handbrake, <kesit>)` (windows-latest). Betik `tools/kalite-paketi-3/hb.ps1 -Is handbrake`.
- Kurulum denemesi koşumları **35157040570** ve **35157507195** (duman, tek kesit); sayıları bu belgeye girmedi.
- Kaynaklar: Sintel 1080p `https://download.blender.org/durian/movies/Sintel.2010.1080p.mkv`
  (sha256 `97F1DBC66231DF42AD49BD8C29AA174B8F48933058E47E7157D4BA63D93A8EFA`, CC BY 3.0), kesitler 10 sn
  1920x818@24: `karanlik` 600 sn, `parlak` 370 sn, `hareketli` 325 sn. `ekran`: Netflix "Debugging", xiph AOM CTC
  `b2_scc` (sha256 `8004BE81AEDB40F9C26DB25FF72D8725D832EF3B95747FB4C9FC8A3693EB4E8E`, CC BY 4.0), 1920x1080@30.
- Kollar: `urun-otomatik` (Bench `shrink`), `handbrake` (`-Z 'H.265 MKV 1080p30' -e x265 --encoder-preset slow
  --multi-pass --turbo -a none --crop-mode none`, `-b` ürünün kbps'ine en çok 3 denemede ±%2 içine ayarlanır),
  `negatif-handbrake-yarim-bit` (aynı ayar, yarım bit hızı), `negatif-kaynak-kendisi` (CAMBI tabanı).
- Hüküm kuralı `tools/kalite-paketi-3/hb-ozet.py`: kbps farkı ±%2 dışındaysa "eş bayt değil"; içindeyse VMAF-NEG ort
  ±0,3 ve XPSNR ±0,2 gürültü bandı dışına düşen fark hükmü verir, biri geride ise "geride".

## Sonuç

- Sekiz satırın sekizi **önde**. VMAF-NEG ort farkı +0,35 ile +12,40; XPSNR +0,35 ile +21,14 dB; SSIM her satırda
  yüksek. kbps sapması −%1,73 ile +%0,04.
- Film kesitlerinde fark bit hızı düştükçe büyüyor: 600 kbit'te +2,62 ile +6,22, 2000 kbit'te +0,35 ile +0,83.
- **CAMBI'de ürün geride**: `karanlik` +2,70 ve +2,79 (9,15 ve 9,27'ye karşı 6,45 ve 6,48), eşik +1,0'ın üstünde;
  `hareketli` +0,48 ve +0,66. `ekran`da ürün önde (−5,18 ve −0,84), `parlak` ikisinde de ~0.
- Negatif kontrol her satırda VMAF-NEG'i (−4,98 ile −22,55) ve XPSNR'ı (−1,81 ile −3,61) HandBrake'in altına indirdi.
  CAMBI negatif kontrolde bit hızıyla tekdüze değil: `karanlik` 600'de 5,99 (HandBrake 6,45), `hareketli` 2000'de 0,43.
- Süre: ürünün kodlama saniyesi HandBrake'in 0,30–0,86 katı, `ekran` 2000'de 1,34 katı. Ürünün toplam saniyesi plan
  ve kalibrasyonu da içeriyor (40,2–109,7 sn); HandBrake'in satırı yalnız son denemenin süresi, bu yüzden toplam
  süreler karşılaştırılabilir değil.
- `ekran` 2000'de ürün bant altında teslim etti (%82,3, 3 deneme, "under band accepted"); HandBrake o bayta ayarlandı.

- İş 13 (`handbrake-kesit-turu.md`, koşum 35114300807) `karanlik`ta ürünü −0,69 geride ölçmüştü. Arada ürün 0.8.4
  (SVT-AV1 variance boost kapalı) ve HandBrake koluna `--turbo` geldi; iki koşum ayrı düzenek, farkı karşılaştırılmaz.

Karar: SDR'de aynı baytta VMAF-NEG ve XPSNR'da ürün HandBrake'in önünde. Açık yalnız bantlaşmada: karanlık film
kesitinde ürünün CAMBI'si HandBrake x265'inkinden ~2,7 yüksek.
**Ölçülmedi:** NVENC/QSV/AMF yolu (koşucuda donanım yok), CLI yolu (1a CLI main'de yok; `urun_yolu=cli` girdisi hazır).

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Toplam sn | HB deneme | Ek hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ekran | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,016 | — | — | — | — | — | — |
| ekran | 600 | urun-otomatik | libsvtav1 | 1920x1080 | 570,9 | 309251 | 95,14 | 95,13 | 46,08 | 0,9997 | 0,365 | 50,70 | 9,7 | — | 40,2 | — | — |
| ekran | 600 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x1080 | 571,1 | 309328 | 82,74 | 82,71 | 33,24 | 0,9925 | 5,540 | 37,44 | 16,5 | — | 16,5 | 2 | — |
| ekran | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x1080 | 320,7 | 173685 | 68,46 | 67,93 | 29,63 | 0,9775 | 5,099 | 33,44 | 16,0 | — | — | — | — |
| ekran | 2000 | urun-otomatik | libsvtav1 | 1920x1080 | 1685,9 | 913217 | 97,20 | 97,20 | 58,92 | 1,0000 | 0,039 | 65,43 | 26,0 | — | 54,6 | — | — |
| ekran | 2000 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x1080 | 1670,5 | 904759 | 92,26 | 92,26 | 37,78 | 0,9972 | 0,883 | 41,59 | 19,4 | — | 19,4 | 3 | — |
| ekran | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x1080 | 762,8 | 413145 | 86,99 | 86,98 | 34,99 | 0,9955 | 4,046 | 39,47 | 16,7 | — | — | — | — |
| hareketli | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,066 | — | — | — | — | — | — |
| hareketli | 600 | urun-otomatik | libsvtav1 | 1920x818 | 585,3 | 731670 | 83,69 | 83,13 | 36,10 | 0,9823 | 0,976 | 34,40 | 27,5 | — | 73,1 | — | — |
| hareketli | 600 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 577,1 | 721360 | 77,47 | 76,65 | 34,32 | 0,9740 | 0,492 | 32,85 | 61,9 | — | 61,9 | 1 | — |
| hareketli | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 289,3 | 361575 | 54,92 | 53,05 | 30,85 | 0,9454 | 0,606 | 29,96 | 51,4 | — | — | — | — |
| hareketli | 2000 | urun-otomatik | libsvtav1 | 1920x818 | 1936,2 | 2420218 | 97,35 | 97,22 | 39,87 | 0,9941 | 1,121 | 38,91 | 26,6 | — | 69,1 | — | — |
| hareketli | 2000 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 1902,7 | 2378412 | 96,51 | 96,36 | 39,14 | 0,9926 | 0,464 | 37,42 | 88,7 | — | 88,7 | 1 | — |
| hareketli | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 961,7 | 1202066 | 88,94 | 88,47 | 36,60 | 0,9853 | 0,434 | 34,95 | 67,7 | — | — | — | — |
| karanlik | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,404 | — | — | — | — | — | — |
| karanlik | 600 | urun-otomatik | libsvtav1 | 1920x818 | 603,9 | 754864 | 81,21 | 80,86 | 36,66 | 0,9816 | 9,150 | 39,93 | 25,2 | — | 68,9 | — | — |
| karanlik | 600 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 604,0 | 755017 | 77,33 | 76,98 | 35,53 | 0,9757 | 6,448 | 38,81 | 55,6 | — | 55,6 | 1 | — |
| karanlik | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 304,1 | 380111 | 58,71 | 57,86 | 33,03 | 0,9555 | 5,987 | 36,51 | 47,4 | — | — | — | — |
| karanlik | 2000 | urun-otomatik | libsvtav1 | 1920x818 | 1982,6 | 2478227 | 96,05 | 95,95 | 39,89 | 0,9932 | 9,265 | 43,30 | 25,8 | — | 67,1 | — | — |
| karanlik | 2000 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 1958,7 | 2448360 | 95,31 | 95,18 | 39,55 | 0,9924 | 6,479 | 42,76 | 72,6 | — | 72,6 | 1 | — |
| karanlik | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 986,9 | 1233685 | 87,04 | 86,84 | 37,32 | 0,9849 | 6,618 | 40,48 | 62,1 | — | — | — | — |
| parlak | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,001 | — | — | — | — | — | — |
| parlak | 600 | urun-otomatik | libsvtav1 | 1920x818 | 596,2 | 745287 | 82,02 | 81,47 | 36,51 | 0,9890 | 0,045 | 35,42 | 23,6 | — | 65,7 | — | — |
| parlak | 600 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 595,3 | 744173 | 79,39 | 79,17 | 35,09 | 0,9856 | 0,010 | 33,79 | 59,1 | — | 59,1 | 2 | — |
| parlak | 600 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 309,8 | 387271 | 66,15 | 65,47 | 32,76 | 0,9725 | 0,006 | 30,98 | 54,0 | — | — | — | — |
| parlak | 2000 | urun-otomatik | libsvtav1 | 1920x818 | 1958,7 | 2448408 | 91,44 | 91,32 | 39,17 | 0,9955 | 0,058 | 39,74 | 68,7 | — | 109,7 | — | — |
| parlak | 2000 | handbrake | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 1956,6 | 2445720 | 91,10 | 91,00 | 38,72 | 0,9949 | 0,015 | 38,54 | 80,0 | — | 80,0 | 2 | — |
| parlak | 2000 | negatif-handbrake-yarim-bit | HandBrakeCLI 1.11.2 x265 slow | 1920x818 | 1004,2 | 1255238 | 86,12 | 85,99 | 36,91 | 0,9912 | 0,013 | 35,97 | 69,7 | — | — | — | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| ekran | 600 | 12,40 | 12,42 | 12,84 | 0,0072 | -5,175 | 13,26 | 0,59 | 0,04 | önde |
| ekran | 2000 | 4,94 | 4,94 | 21,14 | 0,0028 | -0,844 | 23,83 | 1,34 | -0,91 | önde |
| hareketli | 600 | 6,22 | 6,48 | 1,78 | 0,0083 | 0,484 | 1,55 | 0,44 | -1,40 | önde |
| hareketli | 2000 | 0,83 | 0,86 | 0,73 | 0,0015 | 0,657 | 1,49 | 0,30 | -1,73 | önde |
| karanlik | 600 | 3,88 | 3,87 | 1,14 | 0,0060 | 2,702 | 1,12 | 0,45 | 0,02 | önde |
| karanlik | 2000 | 0,74 | 0,78 | 0,35 | 0,0009 | 2,787 | 0,54 | 0,36 | -1,21 | önde |
| parlak | 600 | 2,62 | 2,30 | 1,41 | 0,0034 | 0,035 | 1,63 | 0,40 | -0,15 | önde |
| parlak | 2000 | 0,35 | 0,32 | 0,44 | 0,0006 | 0,043 | 1,20 | 0,86 | -0,11 | önde |
