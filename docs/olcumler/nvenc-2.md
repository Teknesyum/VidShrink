# NVENC 2: HandBrake'in Üstüne Çıkma Denemesi

Makine: RTX 5070 Ti. ffmpeg 9.0 (winget), HandBrakeCLI 1.11.2. Tarih: 2026-09-17. Dal: `t0/nvenc-2`.

## Düzenek

- Kesitler 10 sn FFV1, 1920x818@24, Sintel: `karanlik`, `parlak`, `hareketli` (ağırlık uydurma kesitleri) ve `orta` (uydurmaya girmeyen, dışarıda tutulan kesit).
- Hedefler 1000 / 2000 / 3500 kbit; `orta` hevc/av1'de ek olarak 600 kbit.
- Ürün kolu: `VidShrink.Bench shrink <kesit> <mb,...> --speed quality --no-measure --lock-codec <kodek>`, gerçek yeniden deneme döngüsü dahil.
- HandBrake kolu: tam çözünürlük 1920x818, `-b` ürünün teslim ettiği kbps'e ±%2 içinde en çok 3 denemede oturtulur.
- Eski kol: nvenc-2 öncesi argümanlar (`-multipass fullres -g 120 -spatial-aq 1 -temporal-aq 1`, eski tepe eğrisi, tampon 1+2(tepe−1)) eski plan geometrisinde, yeni ürünün teslim ettiği kbps'e oturtuldu.
- Negatif kol: HandBrake ürün baytının yarısında (`hareketli`).
- Ölçü: `VidShrink.Bench measure-pair` (VMAF-NEG ort/p10). Kodlamalar sırayla, tek tek.
- Betikler: `tools/nvenc-2/tara.ps1` (argüman ve geometri taraması), `tools/nvenc-2/kos.ps1` (uçtan uca), `tools/nvenc-2/eski.ps1` (önce kolu).
- Toplam yerel kodlama (yoklamalar ve plan koşumları dahil): 1283,5 sn.

## Kararlar

| Aday | Karar | Dayanak |
|---|---|---|
| maxrate/bufsize | NVENC'te 2,0x / 2,0x sabit (`NvencPeakFactor`, `NvencBufferFactor`); QSV/AMF eğride kaldı | parlak hevc 2000: AQ kapalı tepe 1,094 86,99/71,81, tepesiz 88,82/83,86; g240'ta tepesiz 89,09/85,67, 2x/2x 89,08/85,49. Sınırsız tepe istenmedi: boyut güvencesi ilk denemede daha geniş aşıma açılır. |
| spatial/temporal AQ | Yazılmaz | 9 hücrenin 9'unda AQ açık kol kapalıdan kötü (yeni_aq − yeni ort −1,17). Karar metriğe göre; göz testi yok. |
| multipass qres/fullres | fullres kaldı | qres 88,79/83,76, fullres 88,82/83,86 (tek hücre). |
| b_ref_mode | Değişmedi | middle 88,82/83,86 = temel; each tam karede +0,18 ort ama −%3,6 bayt. |
| tune uhq | Değişmedi | 88,50/82,72 < temel. |
| preset p5-p7 | Değişmedi (p4, av1 p6) | p7 hevc +0,29 ort ama +%2,2 bayt; av1 p5/p7 +%3,8 bayt, ayrışmıyor. |
| -g 120 → 240 | Değişmedi | g240 +0,27 ort / +1,70 p10 (parlak hevc) ama 5 sn anahtar kare tavanı ürünün arama bütçesi sabiti. 42 kodlamalık kapı `nvenc-gop10.md`'de ölçüldü: 8/9 hücre ort ≥ 0, ama hevc adil HB açığı ort'ta kapanmadı (−0,22); kapı kaldı. |
| Küçültme eşiği | NVENC ölçek/fps cezası h264 2,5x, hevc/av1 4,5x; ağırlık tam karedeki taban oranıyla 1,0x→1,86x arasında 1'den tam değere doğrusal | Geometri taraması (aşağıda) ve 800k 1080p60 av1_nvenc ızgarası; rampa fable'ın kararı (`docs/danisma/2026-09-17-nvenc2-fable.md`, ikinci danışma). |

## Önce / Sonra / HandBrake

Yeni sapma: ürün çıktısının hedefe göre bayt sapması (negatif = altında). Eski ve HB sapması: yeni ürünün teslim ettiği kbps'e göre. Farklar VMAF-NEG puanı.

| Kesit | Kodek | kbit | Yeni geo | Yeni sapma % / deneme | Yeni ort / p10 | Eski geo | Eski sapma % | Eski ort / p10 | HB sapma % | HB ort / p10 | Yeni−HB ort / p10 | Yeni−Eski ort / p10 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | av1 | 1000 | 1882x802 | -11.07 / 3 | 87.81 / 77.36 | 922x392 | -0.46 | 85.48 / 73.77 | -2.85 | 87.58 / 77.71 | 0.23 / -0.35 | 2.33 / 3.59 |
| hareketli | av1 | 2000 | 1882x802 | -1.37 / 2 | 97.00 / 90.25 | 1382x588 | 0.29 | 96.05 / 88.69 | 0.33 | 97.09 / 91.16 | -0.09 / -0.91 | 0.95 / 1.56 |
| hareketli | av1 | 3500 | 1920x818 | -2.72 / 2 | 98.85 / 95.83 | 1882x802 | 1.14 | 98.71 / 95.26 | 0.74 | 98.87 / 95.73 | -0.02 / 0.10 | 0.14 / 0.57 |
| hareketli | h264 | 1000 | 1382x588 | -4.06 / 2 | 80.67 / 66.77 | 652x278 | -0.25 | 75.97 / 61.77 | -8.73 | 69.31 / 50.03 | 11.36 / 16.74 | 4.70 / 5.00 |
| hareketli | h264 | 2000 | 1382x588 | -5.67 / 2 | 92.67 / 82.86 | 922x392 | -0.03 | 89.47 / 78.91 | -0.88 | 89.78 / 79.00 | 2.89 / 3.86 | 3.20 / 3.95 |
| hareketli | h264 | 3500 | 1804x768 | -5.07 / 2 | 97.26 / 90.12 | 1266x540 | -0.55 | 95.81 / 88.59 | -2.29 | 96.30 / 88.12 | 0.96 / 2.00 | 1.45 / 1.53 |
| hareketli | hevc | 1000 | 1650x702 | -8.68 / 3 | 87.36 / 76.92 | 806x344 | -0.62 | 82.25 / 70.09 | -3.46 | 85.87 / 76.28 | 1.49 / 0.64 | 5.11 / 6.83 |
| hareketli | hevc | 2000 | 1882x802 | -6.43 / 2 | 95.94 / 88.36 | 1152x490 | -1.06 | 93.26 / 84.64 | -0.40 | 96.15 / 89.29 | -0.21 / -0.93 | 2.68 / 3.72 |
| hareketli | hevc | 3500 | 1920x818 | -3.81 / 2 | 98.55 / 94.90 | 1574x670 | -1.20 | 97.93 / 92.90 | -0.49 | 98.73 / 95.26 | -0.18 / -0.36 | 0.62 / 2.00 |
| karanlik | av1 | 1000 | 1882x802 | -3.07 / 1 | 86.84 / 81.00 | 922x392 | -0.06 | 84.87 / 77.13 | -0.22 | 86.89 / 81.23 | -0.05 / -0.23 | 1.97 / 3.87 |
| karanlik | av1 | 2000 | 1882x802 | -6.05 / 1 | 95.16 / 90.67 | 1382x588 | 1.01 | 94.09 / 88.15 | 1.17 | 95.38 / 90.73 | -0.22 / -0.06 | 1.07 / 2.52 |
| karanlik | av1 | 3500 | 1920x818 | -3.37 / 1 | 98.54 / 95.30 | 1882x802 | 0.20 | 97.93 / 94.16 | 1.04 | 98.53 / 95.17 | 0.01 / 0.13 | 0.61 / 1.14 |
| karanlik | h264 | 1000 | 1382x588 | -4.15 / 1 | 79.95 / 69.49 | 652x278 | 0.39 | 76.42 / 65.32 | -0.47 | 72.71 / 59.54 | 7.24 / 9.95 | 3.53 / 4.17 |
| karanlik | h264 | 2000 | 1382x588 | -0.54 / 1 | 92.06 / 85.01 | 960x408 | 0.32 | 89.07 / 81.04 | 5.59 | 89.28 / 81.34 | 2.78 / 3.67 | 2.99 / 3.97 |
| karanlik | h264 | 3500 | 1804x768 | -6.94 / 2 | 96.43 / 92.08 | 1306x556 | 1.42 | 94.85 / 88.81 | 0.31 | 95.05 / 89.38 | 1.38 / 2.70 | 1.58 / 3.27 |
| karanlik | hevc | 1000 | 1650x702 | -2.61 / 1 | 85.35 / 78.67 | 806x344 | -0.49 | 81.36 / 72.71 | 1.99 | 85.18 / 77.95 | 0.17 / 0.72 | 3.99 / 5.96 |
| karanlik | hevc | 2000 | 1882x802 | -5.31 / 2 | 93.74 / 88.61 | 1152x490 | -0.01 | 91.27 / 84.89 | 2.03 | 94.11 / 89.44 | -0.37 / -0.83 | 2.47 / 3.72 |
| karanlik | hevc | 3500 | 1920x818 | -3.51 / 2 | 97.68 / 94.17 | 1574x670 | -0.39 | 96.55 / 91.96 | 1.35 | 97.95 / 94.81 | -0.27 / -0.64 | 1.13 / 2.21 |
| orta | av1 | 600 | 1804x768 | -9.05 / 2 | 92.05 / 89.20 | — | — | — | -1.20 | 92.37 / 88.78 | -0.32 / 0.42 | — |
| orta | av1 | 1000 | 1882x802 | -7.74 / 1 | 93.95 / 91.32 | 1498x638 | -0.61 | 91.70 / 87.69 | 8.86 | 94.62 / 91.59 | -0.67 / -0.27 | 2.25 / 3.63 |
| orta | av1 | 2000 | 1920x818 | -1.68 / 1 | 95.96 / 93.44 | 1920x818 | -1.47 | 95.12 / 92.70 | 0.12 | 95.96 / 93.35 | 0.00 / 0.09 | 0.84 / 0.74 |
| orta | av1 | 3500 | 1920x818 | -0.76 / 1 | 96.69 / 94.86 | 1920x818 | 0.48 | 96.11 / 93.87 | 0.33 | 96.69 / 94.89 | 0.00 / -0.03 | 0.58 / 0.99 |
| orta | h264 | 1000 | 1344x572 | -2.43 / 2 | 90.75 / 87.13 | 1036x442 | -2.40 | 86.58 / 81.90 | -3.40 | 91.55 / 87.44 | -0.80 / -0.31 | 4.17 / 5.23 |
| orta | h264 | 2000 | 1536x654 | -4.12 / 2 | 93.55 / 90.91 | 1498x638 | -1.22 | 91.86 / 88.64 | 3.06 | 94.94 / 92.15 | -1.39 / -1.24 | 1.69 / 2.27 |
| orta | h264 | 3500 | 1920x818 | -4.02 / 2 | 96.27 / 94.15 | 1920x818 | -1.34 | 94.79 / 92.48 | -1.68 | 96.16 / 93.85 | 0.11 / 0.30 | 1.48 / 1.67 |
| orta | hevc | 600 | 1536x654 | -0.62 / 2 | 90.32 / 86.10 | — | — | — | -1.48 | 90.41 / 85.95 | -0.09 / 0.15 | — |
| orta | hevc | 1000 | 1882x802 | -6.03 / 2 | 92.73 / 89.78 | 1266x540 | -0.22 | 89.57 / 84.81 | -0.54 | 92.91 / 89.95 | -0.18 / -0.17 | 3.16 / 4.97 |
| orta | hevc | 2000 | 1882x802 | -6.14 / 2 | 94.85 / 92.21 | 1842x784 | 1.11 | 93.63 / 90.27 | 1.34 | 95.31 / 93.07 | -0.46 / -0.86 | 1.22 / 1.94 |
| orta | hevc | 3500 | 1920x818 | -3.20 / 2 | 96.42 / 94.61 | 1920x818 | -0.60 | 95.48 / 93.14 | 1.94 | 96.44 / 94.68 | -0.02 / -0.07 | 0.94 / 1.47 |
| parlak | av1 | 1000 | 1882x802 | -14.81 / 3 | 85.79 / 72.14 | 1152x490 | 0.77 | 80.09 / 56.55 | 1.84 | 86.03 / 70.44 | -0.24 / 1.70 | 5.70 / 15.59 |
| parlak | av1 | 2000 | 1882x802 | -1.91 / 2 | 91.99 / 86.48 | 1728x736 | 1.56 | 89.43 / 78.64 | -4.98 | 92.17 / 86.06 | -0.18 / 0.42 | 2.56 / 7.84 |
| parlak | av1 | 3500 | 1920x818 | -2.44 / 2 | 94.92 / 92.75 | 1920x818 | -1.08 | 93.62 / 89.27 | -0.36 | 94.95 / 92.58 | -0.03 / 0.17 | 1.30 / 3.48 |
| parlak | h264 | 1000 | 1344x572 | -6.29 / 2 | 79.65 / 56.50 | 806x344 | 0.12 | 71.55 / 42.30 | 4.15 | 77.88 / 50.98 | 1.77 / 5.52 | 8.10 / 14.20 |
| parlak | h264 | 2000 | 1344x572 | -1.29 / 2 | 86.83 / 74.91 | 1152x490 | 0.55 | 82.38 / 62.35 | -7.68 | 86.44 / 68.22 | 0.39 / 6.69 | 4.45 / 12.56 |
| parlak | h264 | 3500 | 1766x752 | -4.19 / 2 | 91.75 / 84.69 | 1574x670 | 0.09 | 88.40 / 74.70 | -1.14 | 91.77 / 82.63 | -0.02 / 2.06 | 3.35 / 9.99 |
| parlak | hevc | 1000 | 1804x768 | -14.59 / 3 | 83.61 / 69.32 | 998x424 | -0.85 | 75.06 / 44.67 | -12.38 | 81.11 / 65.94 | 2.50 / 3.38 | 8.55 / 24.65 |
| parlak | hevc | 2000 | 1882x802 | -3.80 / 2 | 90.48 / 84.19 | 1420x604 | -0.38 | 86.06 / 71.01 | 0.48 | 91.03 / 85.27 | -0.55 / -1.08 | 4.42 / 13.18 |
| parlak | hevc | 3500 | 1920x818 | -2.39 / 2 | 93.97 / 91.14 | 1920x818 | -0.99 | 91.83 / 83.27 | -0.34 | 94.14 / 92.37 | -0.17 / -1.23 | 2.14 / 7.87 |

Özet (38 hücre, hiçbir ürün çıktısı hedefi aşmadı; ürün sapması −0,54..−14,81%, ortalama −4,79%):

| Küme | n | Yeni − HB ort / p10 | HB'yi geçen (ort / p10) | Yeni − Eski ort / p10 |
|---|---|---|---|---|
| Tümü | 38 | +0,70 / +1,36 | 14 / 21 | +2,71 / +5,33 |
| Adil (\|HB sapma\| ≤ %2) | 25 | +0,35 / +0,63 | 6 / 13 | +2,03 / +4,21 |
| Adil h264 | 5 | +2,32 / +3,77 | 4 / 5 | +2,63 / +4,61 |
| Adil hevc | 10 | −0,20 / −0,45 | 1 / 2 | +2,26 / +4,81 |
| Adil av1 | 10 | −0,10 / +0,14 | 1 / 6 | +1,46 / +3,38 |
| Adil orta (dışarıda tutulan) | 8 | −0,12 / −0,02 | 1 / 4 | +1,37 / +1,96 |

Negatif kol (HandBrake yarım bayt, hareketli) her hücrede belirgin düşük: h264 1000 48,75/25,77, hevc 2000 88,89/79,32, av1 3500 96,66/89,92. Ölçü bayt farkını görüyor.

## Aşırı Uyum

Ağırlıklar `karanlik` ve `hareketli` üzerinde plan-only koşumlarla uyduruldu. `orta` uydurmaya girmedi: orada yeni ürün eskiden +1,37/+1,96 iyi, HandBrake ile başa baş (−0,12/−0,02). En büyük tek uyum şüphesi `orta` h264 2000: −1,39/−1,24 (HB +%3,06 bayt, sınırda adil). 600 kbit satırı (taban oranı 1,86) rampanın üst ucunda: hevc −0,09/+0,15, av1 −0,32/+0,42. Tek kaynak (Sintel, 24 fps, 1920x818) ölçüldü; 60 fps ya da 1080p kaynakta ağırlıklar ölçülmedi.

## Rampa Doğrulaması

Fable'ın önerdiği oran ≈1,4 hücresi: `hareketli`, 440 kbit, yeni argümanlar. Plan: h264 922x392, hevc 1074x458, av1 1306x556 (450 kbit); 600 kbit'te `orta` hevc 1536x654 ve av1 1804x768 rampadan önceyle aynı.

| Kodek | Geometri | Teslim kbps | Sapma % | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|
| hevc | 1074x458 | 421 | -4.32 | 69.18 | 54.27 |
| hevc | 1382x588 | 489.8 | 11.33 | 72.6 | 59.42 |
| hevc | 1920x818 | 385.7 | -12.35 | 61.07 | 47.29 |
| av1 | 1306x556 | 435 | -1.14 | 71.94 | 57.99 |
| av1 | 1920x818 | 387.6 | -11.9 | 63.34 | 49.28 |

Kodlayıcı tabana yakın bit hızını izlemiyor (1920x818'de 440k→394, 490k→554), eş bayt sağlanamadı. Bayt farkının etkisi (PerHalving ≈10 ile %10 ≈ 1,4 puan) sıra farkından küçük: tam kare küçük kareden hevc'de 8,1, av1'de 8,6 puan düşük. Rampanın tam kareden uzak durma seçimi uyuşuyor; ara ölçek 1382x588'in +%11 baytla 72,6 okuması, rampanın bu oranda biraz fazla küçülttüğünü düşündürüyor (açık).

## Açık Hücreler

- hevc adil hücrelerde HandBrake'in 0,2/0,45 altında. Aday neden `-g 120` (HB 10 sn; tek hücrede g240 +0,27/+1,70). Anahtar kare tavanı arama bütçesi sabiti. `nvenc-gop10.md`: g240 bu açığı −0,335/−0,37'den −0,222/+0,057'ye daraltıyor, ort'ta kapatmıyor; tavan 5 sn'de kaldı.
- parlak hevc/av1 1000 ve hareketli hevc/av1 1000: ilk deneme +%27..+42 aşıyor, üç denemede %8,7-14,8 altında bitiyor. 2x tepenin ilk deneme aşımı; önden küçültme katsayısı 400 sn dosyada ölçülmedi.
- Ürün ortalama %4,8 bütçe bırakıyor; HB ürünün baytına oturtulduğu için kıyas eş baytta, ama bırakılan bayt kaliteye dönmedi.
- \|HB sapma\| > %2 olan 13 hücre adil değil; özet tabloda ayrı.
- AQ kararı yalnız VMAF-NEG'e dayanıyor.
- Rampanın ortası (oran 1,4) eş baytta ölçülemedi.

## Negatif Kontrol (ffmpeg)

Beş karelik kodlama, her kodek; uydurma anahtar ve uydurma değer reddedilmeli, gerçek argümanlar kabul edilmeli.

```
ffmpeg: winget ffmpeg.exe
[h264_nvenc gercek] cikis=0 :: frame=    5 fps=0.0 q=29.0 Lsize=N/A time=00:00:00.08 bitrate=N/A speed=0.298x elapsed=0:00:00.27
[h264_nvenc uydurma-anahtar] cikis=-1414549496 :: Unrecognized option 'uydurma_anahtar'. || Error splitting the argument list: Option not found
[h264_nvenc uydurma-deger] cikis=-22 :: [h264_nvenc @ 000001b3f63c79c0] Unable to parse "multipass" option value "uydurma" || [h264_nvenc @ 000001b3f63c79c0] Error setting option multipass to value uydurma. || [vost#0:0/h264_nvenc @ 000001b3f4607700] Error applying encoder options: Invalid argument
[h264_nvenc aq-yok-gercek] cikis=0 :: frame=    5 fps=0.0 q=27.0 Lsize=N/A time=00:00:00.08 bitrate=N/A speed=0.36x elapsed=0:00:00.23
[hevc_nvenc gercek] cikis=0 :: frame=    5 fps=0.0 q=27.0 Lsize=N/A time=00:00:00.08 bitrate=N/A speed=0.349x elapsed=0:00:00.23
[hevc_nvenc uydurma-anahtar] cikis=-1414549496 :: Unrecognized option 'uydurma_anahtar'. || Error splitting the argument list: Option not found
[hevc_nvenc uydurma-deger] cikis=-22 :: [hevc_nvenc @ 00000277d4920300] Unable to parse "multipass" option value "uydurma" || [hevc_nvenc @ 00000277d4920300] Error setting option multipass to value uydurma. || [vost#0:0/hevc_nvenc @ 00000277d49200c0] Error applying encoder options: Invalid argument
[hevc_nvenc aq-yok-gercek] cikis=0 :: frame=    5 fps=0.0 q=27.0 Lsize=N/A time=00:00:00.08 bitrate=N/A speed=0.368x elapsed=0:00:00.22
[av1_nvenc gercek] cikis=0 :: frame=    5 fps=0.0 q=35.0 Lsize=N/A time=00:00:00.20 bitrate=N/A speed=0.867x elapsed=0:00:00.24
[av1_nvenc uydurma-anahtar] cikis=-1414549496 :: Unrecognized option 'uydurma_anahtar'. || Error splitting the argument list: Option not found
[av1_nvenc uydurma-deger] cikis=-22 :: [av1_nvenc @ 00000281cfb863c0] Unable to parse "multipass" option value "uydurma" || [av1_nvenc @ 00000281cfb863c0] Error setting option multipass to value uydurma. || [vost#0:0/av1_nvenc @ 00000281cfb86180] Error applying encoder options: Invalid argument
[av1_nvenc aq-yok-gercek] cikis=0 :: frame=    5 fps=0.0 q=29.0 Lsize=N/A time=00:00:00.20 bitrate=N/A speed=0.848x elapsed=0:00:00.24
sure 2.1817872
```

## Ekler

### Geometri Taraması (g240, tepesiz, AQ kapalı)

| Kesit | Kodek | Geometri | İstenen kbps | Teslim kbps | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|
| karanlik | h264 | 652x278 | 950 | 946.4 | 78.36 | 66.99 |
| karanlik | h264 | 1286x548 | 950 | 977.6 | 80.46 | 70.69 |
| karanlik | h264 | 1920x818 | 950 | 1017.4 | 76.04 | 64.35 |
| karanlik | hevc | 806x344 | 950 | 948.9 | 82.45 | 73.49 |
| karanlik | hevc | 1362x580 | 950 | 954.9 | 84.78 | 77.99 |
| karanlik | hevc | 1920x818 | 950 | 1024.3 | 85.4 | 78.77 |
| karanlik | av1 | 922x392 | 950 | 952.5 | 85.55 | 77.25 |
| karanlik | av1 | 1418x604 | 950 | 939.1 | 86.75 | 80.56 |
| karanlik | av1 | 1920x818 | 950 | 849.1 | 84.46 | 78.56 |
| parlak | h264 | 806x344 | 950 | 932.3 | 75.67 | 59.94 |
| parlak | h264 | 1362x580 | 950 | 932.4 | 79.33 | 56.92 |
| parlak | h264 | 1920x818 | 950 | 939.7 | 79.13 | 55.31 |
| parlak | hevc | 998x424 | 950 | 977.1 | 80.57 | 69.04 |
| parlak | hevc | 1456x620 | 950 | 953.2 | 83.52 | 69.3 |
| parlak | hevc | 1920x818 | 950 | 967.2 | 84.46 | 69.96 |
| parlak | av1 | 1152x490 | 950 | 935.3 | 83.56 | 72.58 |
| parlak | av1 | 1536x654 | 950 | 877.6 | 85.41 | 73.01 |
| parlak | av1 | 1920x818 | 950 | 1043.6 | 88 | 76.35 |
| hareketli | h264 | 652x278 | 950 | 956.1 | 78.44 | 64.98 |
| hareketli | h264 | 1286x548 | 950 | 961.6 | 81.15 | 68.56 |
| hareketli | h264 | 1920x818 | 950 | 915.7 | 71.86 | 55.07 |
| hareketli | hevc | 806x344 | 950 | 941.1 | 84.21 | 72.25 |
| hareketli | hevc | 1362x580 | 950 | 938.9 | 87.4 | 77.33 |
| hareketli | hevc | 1920x818 | 950 | 921 | 86.16 | 75.59 |
| hareketli | av1 | 922x392 | 950 | 942.8 | 87.82 | 77.3 |
| hareketli | av1 | 1418x604 | 950 | 917.7 | 89.44 | 79.18 |
| hareketli | av1 | 1920x818 | 950 | 1035.1 | 90.62 | 81.03 |
| karanlik | h264 | 1306x556 | 3325 | 3285.4 | 96.06 | 90.4 |
| karanlik | h264 | 1614x688 | 3325 | 3320.1 | 96.6 | 92.17 |
| karanlik | h264 | 1920x818 | 3325 | 3327 | 96.21 | 91.29 |
| karanlik | hevc | 1574x670 | 3325 | 3349.6 | 97.28 | 92.96 |
| karanlik | hevc | 1746x744 | 3325 | 3366.2 | 97.47 | 93.61 |
| karanlik | hevc | 1920x818 | 3325 | 3338.6 | 97.62 | 94.28 |
| karanlik | av1 | 1882x802 | 3325 | 3301.3 | 98.23 | 94.57 |
| karanlik | av1 | 1920x818 | 3325 | 3307.7 | 98.43 | 95.01 |
| parlak | h264 | 1574x670 | 3325 | 3360.9 | 91.37 | 86.41 |
| parlak | h264 | 1746x744 | 3325 | 3290.2 | 91.6 | 85.28 |
| parlak | h264 | 1920x818 | 3325 | 3298.6 | 92.33 | 84.29 |
| parlak | hevc | 1920x818 | 3325 | 3296.1 | 93.88 | 91.07 |
| parlak | av1 | 1920x818 | 3325 | 3411.3 | 94.92 | 92.89 |
| hareketli | h264 | 1266x540 | 3325 | 3346.8 | 96.61 | 89.71 |
| hareketli | h264 | 1596x680 | 3325 | 3357.4 | 97.27 | 91.04 |
| hareketli | h264 | 1920x818 | 3325 | 3319.8 | 97.01 | 89.85 |
| hareketli | hevc | 1574x670 | 3325 | 3303.5 | 98.16 | 93.64 |
| hareketli | hevc | 1746x744 | 3325 | 3389.2 | 98.41 | 94.45 |
| hareketli | hevc | 1920x818 | 3325 | 3286.8 | 98.51 | 94.63 |
| hareketli | av1 | 1882x802 | 3325 | 3314 | 98.7 | 95.29 |
| hareketli | av1 | 1920x818 | 3325 | 3350.5 | 98.8 | 95.61 |

### Argüman Taraması (2000 kbit)
| Kesit | Kodek | Kol | Preset | Geometri | İstenen kbps | Teslim kbps | Sapma % | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|---|---|
| parlak | hevc | urun | p4 | 1420x604 | 1943 | 1947.5 | 0.23 | 85.95 | 70.14 |
| parlak | hevc | hb | p4 | 1420x604 | 1943 | 1949 | 0.31 | 89.03 | 85.4 |
| parlak | hevc | tepesiz | p4 | 1420x604 | 1943 | 1968.2 | 1.3 | 87.5 | 79.4 |
| parlak | hevc | tepesiz_aqyok | p4 | 1420x604 | 1943 | 1965.1 | 1.14 | 88.82 | 83.86 |
| parlak | hevc | tepesiz_saq | p4 | 1420x604 | 1943 | 1958.8 | 0.81 | 87.45 | 79.47 |
| parlak | hevc | buf2 | p4 | 1420x604 | 1943 | 1957.1 | 0.72 | 86.63 | 74.36 |
| parlak | hevc | hb_mp | p4 | 1420x604 | 1943 | 1955.4 | 0.64 | 89.06 | 85.33 |
| parlak | hevc | aqyok_tepe | p4 | 1420x604 | 1943 | 1931.6 | -0.59 | 86.99 | 71.81 |
| parlak | hevc | aqyok_g240 | p4 | 1420x604 | 1943 | 1968.4 | 1.31 | 89.09 | 85.67 |
| parlak | hevc | aqyok_la10 | p4 | 1420x604 | 1943 | 1958 | 0.77 | 88.8 | 83.57 |
| parlak | hevc | aqyok_uhq | p4 | 1420x604 | 1943 | 1968.7 | 1.32 | 88.5 | 82.72 |
| parlak | hevc | aqyok_p7 | p4 | 1420x604 | 1943 | 1962.2 | 0.99 | 89.03 | 83.75 |
| parlak | hevc | aqyok_bref | p4 | 1420x604 | 1943 | 1965.1 | 1.14 | 88.82 | 83.86 |
| parlak | hevc | aqyok_qres | p4 | 1420x604 | 1943 | 1962.1 | 0.98 | 88.79 | 83.76 |
| parlak | hevc | aqyok_buf2tepe15 | p4 | 1420x604 | 1943 | 1957 | 0.72 | 88.72 | 82.85 |
| parlak | h264 | urun | p4 | 1152x490 | 1970 | 1978.5 | 0.43 | 82.18 | 62.56 |
| parlak | h264 | hb | p4 | 1152x490 | 1970 | 1952.4 | -0.89 | 85.94 | 78.2 |
| parlak | h264 | yeni | p4 | 1152x490 | 1970 | 1988.9 | 0.96 | 85.98 | 77.42 |
| parlak | h264 | yeni_aq | p4 | 1152x490 | 1970 | 1946.6 | -1.19 | 83.73 | 72.03 |
| parlak | h264 | yeni_tepe15 | p4 | 1152x490 | 1970 | 1998.8 | 1.46 | 85.6 | 75.38 |
| parlak | av1 | urun | p6 | 1728x736 | 1955 | 1969.1 | 0.72 | 89.55 | 78.97 |
| parlak | av1 | hb | p6 | 1728x736 | 1955 | 1886.1 | -3.52 | 91.25 | 86.26 |
| parlak | av1 | yeni | p6 | 1728x736 | 1955 | 1875.2 | -4.08 | 91.45 | 87.28 |
| parlak | av1 | yeni_aq | p6 | 1728x736 | 1955 | 1981.3 | 1.35 | 90.82 | 85.88 |
| parlak | av1 | yeni_tepe15 | p6 | 1728x736 | 1955 | 1945.7 | -0.47 | 91.26 | 85.55 |
| parlak | hevc | yeni | p4 | 1420x604 | 1943 | 1968.4 | 1.31 | 89.09 | 85.67 |
| parlak | hevc | yeni_aq | p4 | 1420x604 | 1943 | 1950.9 | 0.41 | 87.74 | 81.61 |
| parlak | hevc | yeni_tepe15 | p4 | 1420x604 | 1943 | 1951.6 | 0.44 | 88.46 | 80.71 |
| karanlik | h264 | urun | p4 | 960x408 | 1933 | 1925.6 | -0.38 | 88.41 | 80.27 |
| karanlik | h264 | hb | p4 | 960x408 | 1933 | 1942 | 0.46 | 90.67 | 81.58 |
| karanlik | h264 | yeni | p4 | 960x408 | 1933 | 1935.1 | 0.11 | 90.54 | 81.95 |
| karanlik | h264 | yeni_aq | p4 | 960x408 | 1933 | 1937.2 | 0.22 | 88.62 | 80.49 |
| karanlik | h264 | yeni_tepe15 | p4 | 960x408 | 1933 | 1937.3 | 0.22 | 90.53 | 81.84 |
| karanlik | hevc | urun | p4 | 1152x490 | 2000 | 2003.2 | 0.16 | 91.57 | 85.07 |
| karanlik | hevc | hb | p4 | 1152x490 | 2000 | 1983 | -0.85 | 92.9 | 86.22 |
| karanlik | hevc | yeni | p4 | 1152x490 | 2000 | 2003.4 | 0.17 | 92.87 | 86.43 |
| karanlik | hevc | yeni_aq | p4 | 1152x490 | 2000 | 2011.7 | 0.59 | 91.7 | 85.76 |
| karanlik | hevc | yeni_tepe15 | p4 | 1152x490 | 2000 | 2000.2 | 0.01 | 92.84 | 86.38 |
| karanlik | av1 | urun | p6 | 1382x588 | 1927 | 1945.2 | 0.95 | 94.08 | 88.24 |
| karanlik | av1 | hb | p6 | 1382x588 | 1927 | 1940.8 | 0.71 | 94.99 | 88.78 |
| karanlik | av1 | yeni | p6 | 1382x588 | 1927 | 1935.1 | 0.42 | 94.94 | 89.46 |
| karanlik | av1 | yeni_aq | p6 | 1382x588 | 1927 | 1909.8 | -0.89 | 94.12 | 88.39 |
| karanlik | av1 | yeni_tepe15 | p6 | 1382x588 | 1927 | 1936.9 | 0.51 | 94.91 | 89.36 |
| hareketli | h264 | urun | p4 | 922x392 | 1966 | 1966.9 | 0.04 | 89.71 | 79.1 |
| hareketli | h264 | hb | p4 | 922x392 | 1966 | 1950.9 | -0.77 | 90.86 | 79.09 |
| hareketli | h264 | yeni | p4 | 922x392 | 1966 | 1945.4 | -1.05 | 91.23 | 80.87 |
| hareketli | h264 | yeni_aq | p4 | 922x392 | 1966 | 1946.9 | -0.97 | 89.87 | 79.63 |
| hareketli | h264 | yeni_tepe15 | p4 | 922x392 | 1966 | 1943.2 | -1.16 | 91.21 | 80.82 |
| hareketli | hevc | urun | p4 | 1152x490 | 1956 | 1953.1 | -0.15 | 93.47 | 85.31 |
| hareketli | hevc | hb | p4 | 1152x490 | 1956 | 1935.8 | -1.03 | 93.94 | 85.06 |
| hareketli | hevc | yeni | p4 | 1152x490 | 1956 | 1946.1 | -0.51 | 94.4 | 86.17 |
| hareketli | hevc | yeni_aq | p4 | 1152x490 | 1956 | 1961.8 | 0.3 | 93.78 | 85.52 |
| hareketli | hevc | yeni_tepe15 | p4 | 1152x490 | 1956 | 1951 | -0.26 | 94.42 | 86.18 |
| hareketli | av1 | urun | p6 | 1382x588 | 1975 | 1990.5 | 0.78 | 95.93 | 88.54 |
| hareketli | av1 | hb | p6 | 1382x588 | 1975 | 1970.6 | -0.22 | 95.97 | 88.54 |
| hareketli | av1 | yeni | p6 | 1382x588 | 1975 | 1966.7 | -0.42 | 96.26 | 89.34 |
| hareketli | av1 | yeni_aq | p6 | 1382x588 | 1975 | 1956.6 | -0.93 | 95.91 | 88.93 |
| hareketli | av1 | yeni_tepe15 | p6 | 1382x588 | 1975 | 1965.4 | -0.49 | 96.29 | 89.38 |
| parlak | hevc | yeni_tepe2 | p4 | 1420x604 | 1943 | 1959.2 | 0.84 | 89.08 | 85.49 |
| parlak | hevc | yeni_tepe2_g120 | p4 | 1420x604 | 1943 | 1968.7 | 1.32 | 88.81 | 83.79 |
### İnce Tarama (tam çözünürlük, 2000 kbit)
| Kesit | Kodek | Kol | Preset | Geometri | İstenen kbps | Teslim kbps | Sapma % | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|---|---|
| parlak | hevc | p7 | p4 | 1920x818 | 1943 | 1986.5 | 2.24 | 91.28 | 85.48 |
| parlak | hevc | p5 | p4 | 1920x818 | 1943 | 1872.6 | -3.62 | 90.48 | 83.37 |
| parlak | hevc | la20 | p4 | 1920x818 | 1943 | 1973.6 | 1.57 | 91.2 | 84.6 |
| parlak | hevc | brefeach | p4 | 1920x818 | 1943 | 1873.2 | -3.59 | 90.61 | 83.37 |
| parlak | hevc | bf4 | p4 | 1920x818 | 1943 | 1977.6 | 1.78 | 90.91 | 84.06 |
| parlak | hevc | uhqp7 | p4 | 1920x818 | 1943 | 1944.9 | 0.1 | 91.17 | 84.59 |
| parlak | hevc | tepe2 | p4 | 1920x818 | 1943 | 1850.4 | -4.77 | 90.51 | 84.15 |
| parlak | av1 | p7 | p6 | 1920x818 | 1955 | 2031.2 | 3.9 | 92.69 | 87.71 |
| parlak | av1 | p5 | p6 | 1920x818 | 1955 | 2029.7 | 3.82 | 92.61 | 87.61 |
| parlak | av1 | la20 | p6 | 1920x818 | 1955 | 2014 | 3.02 | 92.4 | 86.85 |
| parlak | av1 | brefeach | p6 | 1920x818 | 1955 | 2016.4 | 3.14 | 92.79 | 88.72 |
| parlak | av1 | bf4 | p6 | 1920x818 | 1955 | 2017.7 | 3.21 | 92.75 | 88.29 |
| parlak | av1 | uhqp7 | p6 | 1920x818 | 1955 | 2013.9 | 3.01 | 92.25 | 87.49 |
| parlak | av1 | tepe2 | p6 | 1920x818 | 1955 | 2025.8 | 3.62 | 92.65 | 87.7 |
### Doğrulama (karanlik hevc 1000)
| Kesit | Kodek | Kol | Preset | Geometri | İstenen kbps | Teslim kbps | Sapma % | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|---|---|
| karanlik | hevc | urunarg | p4 | 1690x720 | 967 | 941.3 | -2.66 | 84.23 | 77.21 |
| karanlik | hevc | tepesiz | p4 | 1690x720 | 967 | 953.7 | -1.38 | 84.6 | 77.92 |
