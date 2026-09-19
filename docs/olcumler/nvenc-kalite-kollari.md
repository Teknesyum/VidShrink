# NVENC Kalite Kolları — Kabul Turu

19 Eylül 2026. ffmpeg 9.0-full_build (gyan.dev), yerel NVIDIA kodlayıcı. Betikler
`tools/nvenc-2/kabul.sh` ve `tools/nvenc-2/kollar.sh`.

`.calisma/nvenc-kollar/rapor.md` dört aday anahtar bıraktı ve dördü için de **beklenen puan
yazmadı**: hiçbiri ölçülmemişti. Ürünün NVENC satırında bugün hâlâ psy/AQ bloğu yok.

## 1. Kabul turu — anahtar gerçekten geçiyor mu

Kalite ölçmeden önce her anahtar 1 sn'lik `testsrc2` üstünde denendi. Ölçü yalnız çıkış kodu
değil: kabul edilen dosyanın `pix_fmt`/`profile`'ı ve baytı okundu, ve iki negatif kontrol kondu —
uydurma anahtar ile sınır dışı değer. SVT-AV1 tanımadığı anahtarı sessizce yutuyordu, NVENC'in
öyle yapmadığı burada ölçüldü.

| kodek | kol | sonuç | çıktı |
|---|---|---|---|
| hevc_nvenc | taban | kabul | Main, yuv420p, 179 086 bayt |
| hevc_nvenc | `-highbitdepth 1` | kabul | **Main 10, yuv420p10le**, 180 598 bayt |
| hevc_nvenc | `-tf_level 4` | **RED** | `InitializeEncoder failed: invalid param (8)` |
| hevc_nvenc | `-rc-lookahead 20 -lookahead_level 3` | kabul | Main, yuv420p, 180 133 bayt |
| hevc_nvenc | `-spatial-aq 1 -aq-strength 4` | kabul | Main, yuv420p, 178 609 bayt |
| hevc_nvenc | negatif: `-zipzop_level 4` | RED | `Unrecognized option 'zipzop_level'` |
| hevc_nvenc | negatif: `-aq-strength 99` | RED | `out of range [1 - 15]` |
| av1_nvenc | taban | kabul | Main, yuv420p, 190 238 bayt |
| av1_nvenc | `-highbitdepth 1` | kabul | **yuv420p10le**, 189 669 bayt |
| av1_nvenc | `-tf_level 4` | kabul | Main, yuv420p, 189 856 bayt |
| av1_nvenc | `-rc-lookahead 20 -lookahead_level 3` | kabul | Main, yuv420p, 181 239 bayt |
| av1_nvenc | `-spatial-aq 1 -aq-strength 4` | kabul | Main, yuv420p, 189 808 bayt |
| av1_nvenc | negatif: `-zipzop_level 4` | RED | `Unrecognized option 'zipzop_level'` |
| av1_nvenc | negatif: `-aq-strength 99` | RED | `out of range [1 - 15]` |

**İlk bulgu:** `-tf_level` `ffmpeg -h encoder=hevc_nvenc` çıktısında **listeleniyor** ama sürücü
HEVC'te reddediyor; yalnız AV1'de kuruluyor. Rapor onu iki kodek için de aday yazmıştı.

Kabul edilen her kolun baytı tabandan farklı — yani hiçbiri sessizce yutulmadı.

## Kalite Kolları: 27 Kol, 3 Kesit, 2 Kodek

Her kol ürünün gerçek satırından yalnız o bayrakla ayrılıyor; bayt, kbps, VMAF-neg
(ortalama / harmonik / p10) ve XPSNR aynı koşumdan okundu. Ham tablo
`tools/nvenc-2/kollar.tsv`, üreten betik aynı klasörde.

### karanlik · hevc_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 2 656 500 | 2125.2 | 94.458 | 94.308 | 89.602 | 39.288 |
| `-highbitdepth 1` | 2 665 099 (+0.32 %) | 2132.1 | 94.822 | 94.683 | 90.290 | 39.795 |
| `-rc-lookahead 20 -lookahead_level 3` | 2 692 564 (+1.36 %) | 2154.1 | 94.960 | 94.845 | 90.993 | 39.573 |
| `-spatial-aq 1 -aq-strength 4` | 2 619 291 (-1.40 %) | 2095.4 | 93.909 | 93.749 | 89.090 | 39.520 |

### karanlik · av1_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 2 546 981 | 2037.6 | 95.690 | 95.576 | 91.276 | 39.058 |
| `-highbitdepth 1` | 2 550 424 (+0.14 %) | 2040.3 | 96.011 | 95.904 | 91.697 | 39.778 |
| `-tf_level 4` | 2 545 442 (-0.06 %) | 2036.4 | 95.747 | 95.639 | 91.442 | 39.021 |
| `-rc-lookahead 20 -lookahead_level 3` | 2 543 921 (-0.12 %) | 2035.1 | 95.953 | 95.847 | 91.764 | 39.489 |
| `-spatial-aq 1 -aq-strength 4` | 2 518 664 (-1.11 %) | 2014.9 | 95.392 | 95.272 | 90.958 | 39.262 |

### parlak · hevc_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 3 382 128 | 2705.7 | 92.635 | 92.504 | 87.652 | 39.692 |
| `-highbitdepth 1` | 3 399 845 (+0.52 %) | 2719.9 | 93.072 | 92.957 | 88.382 | 40.253 |
| `-rc-lookahead 20 -lookahead_level 3` | 3 359 215 (-0.68 %) | 2687.4 | 92.730 | 92.588 | 88.004 | 39.653 |
| `-spatial-aq 1 -aq-strength 4` | 3 318 232 (-1.89 %) | 2654.6 | 91.980 | 91.823 | 85.790 | 39.657 |

### parlak · av1_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 3 200 340 | 2560.3 | 93.648 | 93.552 | 89.679 | 39.645 |
| `-highbitdepth 1` | 3 204 359 (+0.13 %) | 2563.5 | 94.164 | 94.077 | 89.954 | 40.508 |
| `-tf_level 4` | 3 203 306 (+0.09 %) | 2562.6 | 93.455 | 93.373 | 89.380 | 39.637 |
| `-rc-lookahead 20 -lookahead_level 3` | 3 165 165 (-1.10 %) | 2532.1 | 93.363 | 93.241 | 88.604 | 39.670 |
| `-spatial-aq 1 -aq-strength 4` | 3 176 172 (-0.76 %) | 2540.9 | 93.250 | 93.144 | 88.951 | 39.619 |

### hareketli · hevc_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 2 788 303 | 2230.6 | 96.837 | 96.663 | 90.150 | 39.650 |
| `-highbitdepth 1` | 2 790 405 (+0.08 %) | 2232.3 | 97.000 | 96.830 | 90.511 | 40.325 |
| `-rc-lookahead 20 -lookahead_level 3` | 2 763 506 (-0.89 %) | 2210.8 | 97.098 | 96.948 | 91.296 | 39.632 |
| `-spatial-aq 1 -aq-strength 4` | 2 765 839 (-0.81 %) | 2212.7 | 96.676 | 96.500 | 90.066 | 39.966 |

### hareketli · av1_nvenc

| kol | bayt | kbps | VMAF-neg ort | harmonik | p10 | XPSNR |
|---|---|---|---|---|---|---|
| taban | 2 738 492 | 2190.8 | 97.500 | 97.347 | 91.619 | 39.882 |
| `-highbitdepth 1` | 2 723 576 (-0.54 %) | 2178.9 | 97.644 | 97.497 | 91.954 | 40.705 |
| `-tf_level 4` | 2 733 502 (-0.18 %) | 2186.8 | 97.547 | 97.398 | 91.679 | 39.878 |
| `-rc-lookahead 20 -lookahead_level 3` | 2 693 162 (-1.66 %) | 2154.5 | 97.578 | 97.436 | 92.146 | 39.969 |
| `-spatial-aq 1 -aq-strength 4` | 2 711 728 (-0.98 %) | 2169.4 | 97.358 | 97.200 | 91.280 | 40.073 |

## Okuma

- **`-highbitdepth 1` altı hücrenin altısında kazanıyor** — ortalamada, p10'da ve XPSNR'de,
  bayt farkı ±%0,5 içinde. En büyük kazanç XPSNR'de (hevc/parlak 39,69 → 40,25).
- **`-rc-lookahead 20 -lookahead_level 3` altıda beşte kazanıyor**, av1/parlak'ta kaybediyor;
  çoğu hücrede daha az baytla (av1/hareketli 2 738 492 → 2 693 162).
- **`-spatial-aq 1 -aq-strength 4` altıda altısında kaybediyor.** Reddedildi.
- **`-tf_level 4`** hevc'te sürücü tarafından reddediliyor, av1'de fark gürültü düzeyinde.
  Reddedildi.

İki kazanan kolun ürüne alınması ayrı bir karar: `-highbitdepth 1` 8 bit kaynaktan 10 bit
çıktı üretiyor (oynatma uyumluluğu), lookahead ise `FfmpegArguments.cs:262`'de yazılı
anahtar kare yerleşim garantisini değiştiriyor. İkisi de danışmaya verildi.
