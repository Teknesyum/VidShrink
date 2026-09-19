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

## Lookahead Turu: Seviye 1 mi 3 mü, Ne Kadar Sürüyor, Kaç I-Kare


Danışma üç koşul koydu (`docs/danisma/2026-09-19-fable-nvenc-kalite-kollari.md`): kodlama
süresi ve gerçekleşen I-kare sayısı tabloya girsin, `-lookahead_level 1` ile 3
karşılaştırılsın, donanım tavanı "eşit" değil "≤" olarak yeniden temellendirilsin. İlk ikisi
burada, üçüncüsü `FfmpegArguments.cs`'de. Her kolda `-rc-lookahead 20` var; ayrışan tek şey
seviye. Betik `tools/nvenc-2/lookahead.sh`, ham tablo `.calisma/nvenc-2/lookahead*.tsv`.

### h264_nvenc

| kesit | kol | saniye | I-kare | bayt | VMAF-neg ort | p10 | XPSNR |
|---|---|---|---|---|---|---|---|
| karanlik | taban | 1.1 | 2 | 2 599 295 | 90.724 | 83.192 | 37.612 |
| karanlik | `-lookahead_level 1` | 0.9 | 2 | 2 576 124 (-0.89 %) | 90.664 | 84.598 | 37.437 |
| karanlik | `-lookahead_level 3` | 1.5 | 2 | 2 486 223 (-4.35 %) | 91.398 | 86.253 | 37.629 |
| parlak | taban | 0.9 | 2 | 3 365 695 | 90.497 | 77.817 | 38.689 |
| parlak | `-lookahead_level 1` | 1.2 | 2 | 3 179 103 (-5.54 %) | 90.023 | 79.238 | 38.225 |
| parlak | `-lookahead_level 3` | 1.6 | 2 | 3 192 538 (-5.14 %) | 90.702 | 81.646 | 38.520 |
| hareketli | taban | 1.0 | 2 | 2 779 921 | 93.111 | 82.865 | 37.478 |
| hareketli | `-lookahead_level 1` | 1.1 | 2 | 2 597 988 (-6.54 %) | 93.094 | 83.616 | 37.113 |
| hareketli | `-lookahead_level 3` | 1.5 | 2 | 2 579 646 (-7.20 %) | 93.421 | 84.098 | 37.165 |

### hevc_nvenc

| kesit | kol | saniye | I-kare | bayt | VMAF-neg ort | p10 | XPSNR |
|---|---|---|---|---|---|---|---|
| karanlik | taban | 1.0 | 2 | 2 656 500 | 94.458 | 89.602 | 39.288 |
| karanlik | `-lookahead_level 1` | 0.9 | 2 | 2 708 349 (+1.95 %) | 94.636 | 90.440 | 39.471 |
| karanlik | `-lookahead_level 3` | 0.9 | 2 | 2 692 564 (+1.36 %) | 94.960 | 90.993 | 39.573 |
| parlak | taban | 0.9 | 2 | 3 382 128 | 92.635 | 87.652 | 39.692 |
| parlak | `-lookahead_level 1` | 1.0 | 2 | 3 326 691 (-1.64 %) | 92.252 | 86.842 | 39.589 |
| parlak | `-lookahead_level 3` | 1.0 | 2 | 3 359 215 (-0.68 %) | 92.730 | 88.004 | 39.653 |
| hareketli | taban | 0.9 | 2 | 2 788 303 | 96.837 | 90.150 | 39.650 |
| hareketli | `-lookahead_level 1` | 1.0 | 2 | 2 768 827 (-0.70 %) | 96.998 | 91.224 | 39.698 |
| hareketli | `-lookahead_level 3` | 1.1 | 2 | 2 763 506 (-0.89 %) | 97.098 | 91.296 | 39.632 |

### av1_nvenc

| kesit | kol | saniye | I-kare | bayt | VMAF-neg ort | p10 | XPSNR |
|---|---|---|---|---|---|---|---|
| karanlik | taban | 1.0 | 2 | 2 546 981 | 95.690 | 91.276 | 39.058 |
| karanlik | `-lookahead_level 1` | 1.1 | 2 | 2 561 317 (+0.56 %) | 95.771 | 91.321 | 39.486 |
| karanlik | `-lookahead_level 3` | 1.3 | 2 | 2 543 921 (-0.12 %) | 95.953 | 91.764 | 39.489 |
| parlak | taban | 1.1 | 2 | 3 200 340 | 93.648 | 89.679 | 39.645 |
| parlak | `-lookahead_level 1` | 1.1 | 2 | 3 178 260 (-0.69 %) | 93.165 | 88.221 | 39.656 |
| parlak | `-lookahead_level 3` | 1.3 | 2 | 3 165 165 (-1.10 %) | 93.363 | 88.604 | 39.670 |
| hareketli | taban | 1.1 | 2 | 2 738 492 | 97.500 | 91.619 | 39.882 |
| hareketli | `-lookahead_level 1` | 1.1 | 2 | 2 705 541 (-1.20 %) | 97.539 | 91.956 | 40.013 |
| hareketli | `-lookahead_level 3` | 1.2 | 2 | 2 693 162 (-1.66 %) | 97.578 | 92.146 | 39.969 |

## Karar

`-rc-lookahead 20 -lookahead_level 3` **alındı**. Dokuz hücrede (3 kesit × 3 NVENC kodeği)
p10'un 9'unda, ortalamanın 8'inde tabanı geçiyor — tek kayıp av1/parlak ortalaması — ve
bunu 9 hücrenin 7'sinde **daha az baytla** yapıyor. En büyük kazanç h264'te: parlak
kesitte p10 77,817 → 81,646.

`-lookahead_level 1` alınmadı: dokuz hücrenin dokuzunda seviye 3'ün altında ve üçünde
tabanın da altında. Seviye boş bir düğme değil, ölçülen seviye 3.

**Kolun bedeli ölçüldü ama kesin değil:** seviye 3'ün kodlama süresi 10 sn'lik kesitlerde
0,9-1,1 sn yerine 1,1-1,6 sn. Mutlak fark 0,5 sn'nin altında ve bu ölçekte zamanlayıcı
gürültüsü büyük; gerçek bir verim ölçümü (uzun kaynak, tekrarlı koşum) yapılmadı.

**Gerçekleşen I-kare sayısı her kolda aynı çıktı (2).** Yani lookahead'in sahne kesimine
I-kare ekleme mekanizması bu üç kesitte hiç tetiklenmedi ve gerçekleşen aralık hâlâ tam
tavana eşit. Buna rağmen `FfmpegArguments.cs`'deki gerekçe "aralık = tavan"dan
"aralık ≤ tavan"a çevrildi: garanti artık malzemeye değil kurala dayanıyor.

`-highbitdepth 1` **alınmadı**. 6/6 kazanıyor ama 8 bit kaynaktan 10 bit çıktı üretiyor;
danışmanın okuması, paylaşım kanallarının (WhatsApp, Telegram, Instagram) yüklenen videoyu
zaten 8 bit'e çevirdiği, yani kazancın kanalda buharlaştığı ve geriye yalnız "eski cihazda
hiç açılmıyor" riskinin kaldığı yönünde. Kol reddedilmedi, açık bir kalite kipine ertelendi.

