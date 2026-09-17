# NVENC Yerel Ölçümü: VidShrink ve HandBrake

Makine: RTX 5070 Ti, sürücü 616.56. HandBrakeCLI 1.11.2 (winget). Tarih: 2026-09-17.

## Düzenek

- Kaynak: `sintel.mkv`, 1 180 090 590 bayt, sha256 `97F1DBC66231DF42AD49BD8C29AA174B8F48933058E47E7157D4BA63D93A8EFA`.
- Kesitler 10 sn, FFV1, 1920x818@24: `karanlik` 600 sn, `parlak` 370 sn, `hareketli` 325 sn'den.
- Hedef 2000 kbit/sn = 2,4414 MB. Kodlamalar sırayla, tek tek.
- Ürün kolu: `VidShrink.Bench shrink <kesit> 2.4414 --speed quality --no-measure --lock-codec <kodek>`. `--lock-codec` ürünün `PlanOptions.LockedCodec` yolu. `--force-codec` nvenc'e `-preset slow` ve 2pass yazıyor, ürün yolu değil.
- HandBrake kolu: `-Z "H.265 NVENC 1080p" -e nvenc_h264|nvenc_h265|nvenc_av1 -a none --crop-mode none -f av_mkv -b <k>`. Ürünün çıktı baytına ±%2 içinde en çok 3 denemede oturtulur, tam çözünürlük ve ürünün geometrisi olarak iki kol.
- Negatif kontrol: HandBrake, ürün baytının yarısı (`hareketli`).
- Ölçü: `VidShrink.Bench measure-pair` (VMAF-NEG, libvmaf varsayılan tek iş parçacığı; XPSNR).
- Betikler: `tools/nvenc-yerel/kos.ps1` (ana tablo), `tools/nvenc-yerel/ab.ps1` (A/B).
- Toplam kodlama: 50,9 + 230,9 + 87,9 + 7,6 + 4,7 + 14,3 sn ve teşhis/doğrulama ≈ 40 sn, toplam ≈ 440 sn.

## Ana Tablo

Doluluk / deneme ürün için; HandBrake satırında bayt sapması / deneme. Toplam sn ürün için düzeltmeden önceki süre.

| Kesit | Kodek | Kol | Geometri | Bayt | kbps | Doluluk ya da sapma | VMAF-NEG ort | harm | p10 | XPSNR | Kodlama sn | Toplam sn |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | h264 | ürün | 960x408 | 2415864 | 1932.7 | 94.37% / 1 | 88.47 | 88.06 | 80.53 | 38.26 | 11.3 | 19.9 |
| karanlik | h264 | HB | 1920x818 | 2477123 | 1981.7 | +2.54% / 3 | 88.01 | 87.47 | 79.39 | 36.97 | 2.7 | 8.1 |
| karanlik | h264 | HB ürün geo | 960x408 | 2437481 | 1950 | +0.89% / 2 | 89.86 | 89.48 | 81.58 | 37.82 | 2.7 | 5.4 |
| karanlik | hevc | ürün | 1152x490 | 2500363 | 2000.3 | 97.67% / 1 | 91.56 | 91.31 | 85.15 | 39.14 | 12 | 20.5 |
| karanlik | hevc | HB | 1920x818 | 2502173 | 2001.7 | +0.07% / 2 | 94.22 | 94.09 | 89.77 | 39.18 | 2.7 | 5.4 |
| karanlik | hevc | HB ürün geo | 1152x490 | 2482374 | 1985.9 | -0.72% / 2 | 93.1 | 92.87 | 87.01 | 38.91 | 2.7 | 5.4 |
| karanlik | av1 | ürün | 1382x588 | 2408744 | 1927 | 94.09% / 1 | 94.01 | 93.83 | 88.32 | 39.29 | 12.2 | 22 |
| karanlik | av1 | HB | 1920x818 | 2433345 | 1946.7 | +1.02% / 1 | 95.38 | 95.25 | 90.73 | 38.92 | 2.7 | 2.7 |
| karanlik | av1 | HB ürün geo | 1382x588 | 2430548 | 1944.4 | +0.9% / 1 | 94.82 | 94.65 | 89.26 | 38.93 | 2.7 | 2.7 |
| parlak | h264 | ürün | 1152x490 | 2462912 | 1970.3 | 96.21% / 2 | 82 | 80.44 | 61.46 | 36.72 | 22.5 | 31.4 |
| parlak | h264 | HB | 1920x818 | 2604053 | 2083.2 | +5.73% / 3 | 87.64 | 86.55 | 70.09 | 37.61 | 2.7 | 8.1 |
| parlak | h264 | HB ürün geo | 1152x490 | — | — | HandBrake hatası "VCE: Init Failed" | — | — | — | — | — | — |
| parlak | hevc | ürün | 1420x604 | 2426861 | 1941.5 | 94.8% / 2 | 85.94 | 85.15 | 70.34 | 37.9 | 23.9 | 33 |
| parlak | hevc | HB | 1920x818 | 2474520 | 1979.6 | +1.96% / 3 | 91.03 | 90.85 | 85.27 | 38.93 | 2.9 | 8.7 |
| parlak | hevc | HB ürün geo | 1420x604 | 2441777 | 1953.4 | +0.61% / 2 | 89.08 | 88.99 | 85.59 | 38.6 | 2.9 | 5.6 |
| parlak | av1 | ürün | 1728x736 | 2443138 | 1954.5 | 95.44% / 2 | 89.4 | 88.98 | 78.42 | 38.47 | 23.8 | 33.1 |
| parlak | av1 | HB | 1920x818 | 2396338 | 1917.1 | -1.92% / 3 | 92.21 | 92.05 | 86.09 | 38.98 | 2.9 | 8.3 |
| parlak | av1 | HB ürün geo | 1728x736 | 2405319 | 1924.3 | -1.55% / 2 | 91.4 | 91.28 | 86.56 | 38.92 | 2.7 | 5.4 |
| hareketli | h264 | ürün | 922x392 | 2457985 | 1966.4 | 96.02% / 2 | 89.7 | 89.05 | 79.19 | 37.27 | 24.4 | 32.6 |
| hareketli | h264 | HB | 1920x818 | 2393711 | 1915 | -2.62% / 3 | 89.78 | 89.12 | 79 | 36.4 | 2.7 | 8.1 |
| hareketli | h264 | HB ürün geo | 922x392 | 2442286 | 1953.8 | -0.64% / 2 | 90.77 | 90.14 | 80.04 | 37.02 | 2.7 | 5.4 |
| hareketli | h264 | negatif HB yarım bit | 1920x818 | 1332092 | 1065.7 | — | 75.56 | 73.58 | 58.04 | 32.7 | 2.7 | — |
| hareketli | hevc | ürün | 1152x490 | 2444351 | 1955.5 | 95.48% / 2 | 93.49 | 93.13 | 85.43 | 38.65 | 25.8 | 34.6 |
| hareketli | hevc | HB | 1920x818 | 2388488 | 1910.8 | -2.29% / 3 | 96.16 | 95.96 | 89.29 | 39.2 | 2.9 | 8.3 |
| hareketli | hevc | HB ürün geo | 1152x490 | 2439442 | 1951.6 | -0.2% / 2 | 94.63 | 94.34 | 86.44 | 38.51 | 2.7 | 5.4 |
| hareketli | hevc | negatif HB yarım bit | 1920x818 | 1337244 | 1069.8 | — | 89.04 | 88.52 | 79.39 | 36.82 | 2.7 | — |
| hareketli | av1 | ürün | 1382x588 | 2468419 | 1974.7 | 96.42% / 1 | 95.89 | 95.67 | 88.19 | 39.36 | 13 | 22.8 |
| hareketli | av1 | HB | 1920x818 | 2547860 | 2038.3 | +3.22% / 3 | 97.14 | 96.97 | 91.02 | 39.65 | 2.7 | 8.1 |
| hareketli | av1 | HB ürün geo | 1382x588 | 2486190 | 1989 | +0.72% / 2 | 96.21 | 95.98 | 89.1 | 39.16 | 2.7 | 5.4 |
| hareketli | av1 | negatif HB yarım bit | 1920x818 | 1317142 | 1053.7 | — | 90.76 | 90.24 | 81.37 | 37.26 | 2.7 | — |

- Ürünün dokuz kodlamasının dokuzu bantta, hiçbiri hedefi aşmadı. HandBrake beş hücrede üç denemede ±%2 bandına oturamadı (en çok +%5,73).
- VMAF-NEG ortalamasında ürün dokuz hücrenin sekizinde HandBrake tam çözünürlüğün gerisinde: -0,08 (hareketli h264) ile -5,64 (parlak h264) arası. karanlik h264'te +0,46 önde.
- Negatif kontrol: yarım bitte VMAF-NEG her üç kodekte düştü (hevc 96,16→89,04, h264 89,78→75,56, av1 97,14→90,76).

## Açık 1: Yoğun Kodlanmış Kaynakta `-hwaccel auto` (Düzeltildi)

Ürün kodlaması 11-26 sn, HandBrake'in 2,7-2,9 sn'si. Sebep: FFmpeg `-hwaccel auto`, FFV1 ve ProRes için Vulkan çözücüsünü seçiyor (günlükte "Using auto hwaccel type vulkan").

| Kaynak | hwaccel yok | `-hwaccel auto` |
|---|---|---|
| FFV1 10 sn çözme | 0.67 sn | 6.79 sn |
| ProRes 4 sn çözme | 0.13 sn | 0.99 sn |
| h264 çözme | 0.41 sn | 0.55 sn |

Düzeltme: `FfmpegArguments.HardwareDecodeArgs` donanım çözümü yalnız dağıtım kodeklerine verir (h264, hevc, av1, vp9, vp8, mpeg1/2/4, vc1, wmv3). `CalibrationProbe` hızlı kipte aynı kapıyı kullanır.

| Kesit | Kodek | Toplam sn önce | Toplam sn sonra | Kodlama sn sonra | Bayt | VMAF-NEG ort / p10 / XPSNR |
|---|---|---|---|---|---|---|
| parlak | hevc | 33.0 | 9.8 | 1.7 | 2426861 (aynı) | 85.94 / 70.34 / 37.9 (aynı) |
| hareketli | hevc | 34.6 | 9.4 | 1.8 | 2444351 (aynı) | 93.49 / 85.43 / 38.65 (aynı) |

## Açık 2: Tepe Hızı ve Tampon (Ölçüldü, Sabitler Değişmedi)

`ab.ps1`, ürünün komutu elle yeniden kuruldu (`-rc vbr -multipass fullres -g 120 -spatial-aq 1 -temporal-aq 1`, maxrate 1,1x, bufsize 1,2x).

| Kesit | Kodek | Kol | -b:v k | kbps | VMAF-NEG ort | harm | p10 | XPSNR |
|---|---|---|---|---|---|---|---|---|
| parlak | hevc | ürün | 1721 | 1979.9 | 86.15 | 85.43 | 71.48 | 37.96 |
| parlak | hevc | +rc-lookahead 10 | 1721 | 1953.7 | 86.04 | 85.28 | 71.19 | 37.92 |
| parlak | hevc | AQ yok | 1721 | 1887.3 | 86.75 | 85.91 | 69.92 | 37.78 |
| parlak | hevc | temporal AQ yok | 1721 | 1979.3 | 86.16 | 85.43 | 71.26 | 37.96 |
| parlak | hevc | tepesiz, eş bayt | 1549 | 1991.8 | 87.59 | 87.4 | 80.71 | 38.53 |
| parlak | hevc | tepesiz | 1721 | 2200.1 | 88.23 | 88.09 | 82 | 38.74 |
| parlak | hevc | bufsize 2x | 1721 | 2058.8 | 87.16 | 86.8 | 77.09 | 38.37 |
| parlak | hevc | bufsize 4x | 1721 | 2082.9 | 87.3 | 86.98 | 77.41 | 38.42 |
| hareketli | hevc | ürün | 1831 | 1955.5 | 93.49 | 93.13 | 85.43 | 38.65 |
| hareketli | hevc | +rc-lookahead 10 | 1831 | 1984.8 | 93.05 | 92.56 | 82.7 | 38.53 |
| hareketli | hevc | tepesiz | 1831 | 2037.8 | 93.9 | 93.57 | 85.72 | 38.81 |
| hareketli | hevc | bufsize 2x | 1831 | 1968.4 | 93.56 | 93.21 | 85.3 | 38.68 |
| hareketli | hevc | bufsize 4x | 1831 | 1999.1 | 93.74 | 93.4 | 85.68 | 38.74 |
| karanlik | hevc | ürün | 1945 | 2000.3 | 91.56 | 91.31 | 85.15 | 39.14 |
| karanlik | hevc | bufsize 2x | 1945 | 2011.2 | 91.56 | 91.3 | 85.14 | 39.18 |
| karanlik | hevc | bufsize 4x | 1945 | 2020.4 | 91.55 | 91.29 | 85.16 | 39.2 |
| parlak | av1 | ürün | 1827 | 2037.9 | 89.89 | 89.56 | 80.17 | 38.64 |
| parlak | av1 | bufsize 2x | 1827 | 2056.2 | 89.94 | 89.61 | 80.47 | 38.66 |
| parlak | av1 | bufsize 4x | 1827 | 2086.1 | 90.1 | 89.81 | 81.01 | 38.72 |
| parlak | h264 | ürün | 1688 | 1971.2 | 82.02 | 80.47 | 61.85 | 36.72 |
| parlak | h264 | bufsize 2x | 1688 | 2027.5 | 83.06 | 82.14 | 67.26 | 37.07 |
| parlak | h264 | bufsize 4x | 1688 | 2022.7 | 82.98 | 82 | 66.45 | 37.06 |

- Lookahead kazandırmıyor; hareketli'de p10 85,43→82,70 kaybettiriyor.
- Eş baytta tepesiz kol parlak hevc'de +1,44 ortalama, +9,23 p10. Tampon 2x parlak'ta p10'u hevc +5,61, h264 +5,41 artırıyor, bayt +%3-4; hareketli ve karanlik'ta etkisiz.
- `PeakRateFactor` eğrisi `HardwareRateControlTests` ile pimli, `BufferFactor` hiç ayrı ölçülmedi. Üç kesitlik 10 sn'lik ölçüm sabiti değiştirmeye yetmiyor; ayrı sözleşmenin konusu.

## Açık 3: Küçültme Geometrisi (Yalnız Bulgu)

Aynı baytta HandBrake tam çözünürlük, HandBrake ürün geometrisinden hevc/av1'de +0,56 ile +1,95 önde (VMAF-NEG ort); h264'te ürün geometrisi daha iyi (karanlik -1,85, hareketli -0,99). Kod değişmedi.

## Ölçülmeyenler

- 5500 kbit ve üstü hedefler.
- parlak h264 HandBrake ürün geometrisi (HandBrake hatası).
- `ComplexityProbe` hızlı kipindeki sabit `-hwaccel auto` (yol alıyor, kodek bilgisi yok).
- Tepe/tampon sabitlerinin değiştirilmiş ürünle uçtan uca ölçümü.
