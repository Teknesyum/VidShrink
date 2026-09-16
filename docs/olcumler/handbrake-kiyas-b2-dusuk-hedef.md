# B2 — Düşük Hedef ve Social Ön Ayarları

Durum: **ölçüldü, kod değişmedi.** İki soru: (1) düşük bit hızında ürünün çözünürlük ve kare hızı düşürmesi
HandBrake 1080p'ye karşı ne yapıyor, (2) HandBrake'in Discord için önerdiği Social ön ayarlarının ürettiği boyuta
ürün hedeflenince ne çıkıyor.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, işler `olcum (dusuk, <kesit>)` ve `olcum (social, <kesit>)`, kesitler `karanlik`, `parlak`, `hareketli`
  (kaynak ve sha256 `handbrake-kiyas-b1-sdr.md` ile aynı).
- **dusuk**: 300 ve 600 kbit. Kollar `urun-otomatik`, `urun-dusurme-kapali` (`--no-resolution-drop --no-fps-drop`),
  `handbrake-1080p` (B1 ayarı, `urun-otomatik`in baytına ±%2), `negatif-handbrake-yarim-bit`. 16 fps çıktı ölçücüde kare
  tekrarıyla 24 fps kaynağa hizalanır.
- **social**: HandBrake 1.11.2'de "Discord" adlı ön ayar yok; `handbrake-preset-list.txt` yedi Social ön ayarını
  "online social communities such as Discord" diye tanımlıyor. Her ön ayar `-Z '<ad>' -a none` ile koşuldu, ürün
  HandBrake çıktısının MB'ına hedeflendi (iki kol). Ön ayarların çözünürlük sınırı HandBrake'te uygulanır; ürün
  kendi kararını verir.
- Hüküm kuralı B1 ile aynı; kbps farkı ±%2'yi aşıp ürün **daha az bayt** harcadıysa aynı eşiklerle "(az bayt)"
  etiketli hüküm verilir, ürün daha çok harcayıp gerideyse "geride (çok bayt)".

- Kıyas tabloları sırasıyla: dusuk `urun-otomatik` ve `urun-dusurme-kapali` − `handbrake-1080p`; social aynı iki
  kol − `handbrake`.

## Sonuç

- **dusuk, `urun-otomatik`**: 6 satırın 5'i önde (+2,70 ile +7,04 VMAF-NEG). `hareketli` 300'de **geride**: ürün
  kare hızını 16 fps'e indirdi (`-vf fps=16`), VMAF-NEG ort −5,78, harmonik −49,09 (3,96'ya karşı 53,05), XPSNR
  −9,95. `karanlik` 300'de çözünürlüğü 1842x784'e indirdi ve önde kaldı (+7,04).
- **dusuk, `urun-dusurme-kapali`**: 6 satırın 6'sı önde (+2,72 ile +10,99). `hareketli` 300'de dusurme kapalı kol
  otomatik kolun +16,77 VMAF-NEG ort önünde.
- **social**, iki kolda da 21 satır: 18 önde, 1 bantta, 2 geride. Ürün HandBrake'in MB'ına hedeflenip %0,95–8,52
  daha az bayt teslim etti (çoğu satırda ±%2 dışında, "az bayt").
- Geride ve bantta kalanların hepsi HandBrake'in çözünürlük düşürmediği tek ön ayar, `Social 25 MB 30 Seconds
  1080p60`: `parlak` −0,64 / −0,45 dB (az bayt) ve dusurme kapalı kolda −0,53 / −0,38 (eş bayt), `karanlik` XPSNR
  −0,25 (az bayt), `hareketli` bantta (az bayt).
- HandBrake'in 720p/540p/360p'ye indirdiği altı ön ayarda ürün 1920x818'de kaldı ve +1,69 ile +13,85 VMAF-NEG önde.
- CAMBI social'da da ürünün aleyhine: `karanlik` +1,04 ile +3,49, diğer kesitler +0,03 ile +0,69.

Karar: Düşük hedefte açık, hareketli içerikte kare hızını 16'ya indirmek; aynı baytta düşürmeyen kol HandBrake'in
+10,99 önünde. Social ön ayarlarında HandBrake'in çözünürlük indirmesi ürüne 1,7–13,9 puan bırakıyor.
**Ölçülmedi:** 30 sn'den uzun kaynakla ön ayarın gerçek süre sınırı (kesitler 10 sn), ses bütçesi (`-a none`).

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Toplam sn | HB deneme | Ek hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 300 | urun-otomatik | libsvtav1 | 1920x818 | 292,8 | 365988 | 49,14 | 3,96 | 20,89 | 0,9042 | 1,037 | 17,57 | 17,7 | — | 62,2 | — | — |
| hareketli | 300 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 289,8 | 362230 | 65,91 | 63,13 | 33,19 | 0,9656 | 0,995 | 31,48 | 20,5 | — | 63,5 | — | — |
| hareketli | 300 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 289,3 | 361575 | 54,92 | 53,05 | 30,85 | 0,9454 | 0,606 | 29,96 | 53,1 | — | 53,1 | 1 | — |
| hareketli | 300 | negatif-handbrake-yarim-bit | — | 1920x818 | 229,3 | 286669 | 30,33 | 25,04 | 27,59 | 0,9077 | 0,595 | 27,26 | 48,9 | — | — | — | — |
| hareketli | 600 | urun-otomatik | libsvtav1 | 1920x818 | 585,7 | 732080 | 83,70 | 83,15 | 36,11 | 0,9824 | 0,977 | 34,40 | 22,6 | — | 64,9 | — | — |
| hareketli | 600 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 583,8 | 729751 | 83,67 | 83,11 | 36,10 | 0,9823 | 0,971 | 34,40 | 22,6 | — | 64,6 | — | — |
| hareketli | 600 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 577,7 | 722086 | 77,53 | 76,69 | 34,35 | 0,9740 | 0,466 | 32,87 | 60,5 | — | 60,5 | 1 | — |
| hareketli | 600 | negatif-handbrake-yarim-bit | — | 1920x818 | 289,3 | 361575 | 54,92 | 53,05 | 30,85 | 0,9454 | 0,606 | 29,96 | 51,6 | — | — | — | — |
| karanlik | 300 | urun-otomatik | libsvtav1 | 1842x784 | 302,2 | 377720 | 65,75 | 64,68 | 34,37 | 0,9680 | 8,920 | 37,83 | 17,1 | — | 52,8 | — | — |
| karanlik | 300 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 299,6 | 374487 | 64,93 | 63,78 | 34,27 | 0,9673 | 8,969 | 37,72 | 15,9 | — | 48,3 | — | — |
| karanlik | 300 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 304,1 | 380111 | 58,71 | 57,86 | 33,03 | 0,9555 | 5,987 | 36,51 | 38,9 | — | 38,9 | 1 | — |
| karanlik | 300 | negatif-handbrake-yarim-bit | — | 1920x818 | 151,0 | 188781 | 34,00 | 32,23 | 29,54 | 0,9207 | 6,843 | 33,84 | 31,2 | — | — | — | — |
| karanlik | 600 | urun-otomatik | libsvtav1 | 1920x818 | 603,3 | 754149 | 81,20 | 80,85 | 36,68 | 0,9816 | 9,214 | 39,92 | 18,8 | — | 50,8 | — | — |
| karanlik | 600 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 603,2 | 753989 | 81,20 | 80,84 | 36,68 | 0,9816 | 9,214 | 39,92 | 18,9 | — | 51,2 | — | — |
| karanlik | 600 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 602,9 | 753566 | 77,26 | 76,90 | 35,48 | 0,9756 | 6,440 | 38,80 | 53,7 | — | 53,7 | 1 | — |
| karanlik | 600 | negatif-handbrake-yarim-bit | — | 1920x818 | 304,1 | 380111 | 58,71 | 57,86 | 33,03 | 0,9555 | 5,987 | 36,51 | 37,0 | — | — | — | — |
| parlak | 300 | urun-otomatik | libsvtav1 | 1920x818 | 298,0 | 372480 | 71,12 | 69,56 | 34,39 | 0,9801 | 0,033 | 32,79 | 22,7 | — | 72,9 | — | — |
| parlak | 300 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 297,8 | 372225 | 71,11 | 69,54 | 34,39 | 0,9801 | 0,033 | 32,79 | 21,4 | — | 63,0 | — | — |
| parlak | 300 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 298,9 | 373603 | 65,37 | 64,64 | 32,64 | 0,9716 | 0,004 | 30,84 | 49,2 | — | 49,2 | 2 | — |
| parlak | 300 | negatif-handbrake-yarim-bit | — | 1920x818 | 154,6 | 193283 | 46,14 | 44,28 | 29,99 | 0,9479 | 0,003 | 28,30 | 43,4 | — | — | — | — |
| parlak | 600 | urun-otomatik | libsvtav1 | 1920x818 | 595,8 | 744756 | 82,00 | 81,45 | 36,50 | 0,9890 | 0,045 | 35,43 | 32,8 | — | 82,5 | — | — |
| parlak | 600 | urun-dusurme-kapali | libsvtav1 | 1920x818 | 599,4 | 749307 | 82,03 | 81,47 | 36,51 | 0,9890 | 0,045 | 35,44 | 24,6 | — | 65,8 | — | — |
| parlak | 600 | handbrake-1080p | HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo | 1920x818 | 594,0 | 742533 | 79,30 | 79,08 | 35,09 | 0,9856 | 0,011 | 33,76 | 59,7 | — | 59,7 | 2 | — |
| parlak | 600 | negatif-handbrake-yarim-bit | — | 1920x818 | 309,8 | 387271 | 66,15 | 65,47 | 32,76 | 0,9725 | 0,006 | 30,98 | 49,6 | — | — | — | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 300 | -5,78 | -49,09 | -9,95 | -0,0413 | 0,430 | -12,38 | 0,33 | -1,20 | geride |
| hareketli | 600 | 6,18 | 6,46 | 1,76 | 0,0083 | 0,511 | 1,54 | 0,37 | -1,37 | önde |
| karanlik | 300 | 7,04 | 6,83 | 1,35 | 0,0125 | 2,934 | 1,32 | 0,44 | 0,63 | önde |
| karanlik | 600 | 3,94 | 3,95 | 1,20 | 0,0060 | 2,774 | 1,12 | 0,35 | -0,07 | önde |
| parlak | 300 | 5,75 | 4,92 | 1,75 | 0,0085 | 0,029 | 1,95 | 0,46 | 0,30 | önde |
| parlak | 600 | 2,70 | 2,37 | 1,42 | 0,0034 | 0,034 | 1,67 | 0,55 | -0,30 | önde |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 300 | 10,99 | 10,08 | 2,35 | 0,0202 | 0,389 | 1,52 | 0,39 | -0,17 | önde |
| hareketli | 600 | 6,14 | 6,42 | 1,76 | 0,0083 | 0,505 | 1,53 | 0,37 | -1,04 | önde |
| karanlik | 300 | 6,22 | 5,92 | 1,25 | 0,0117 | 2,982 | 1,22 | 0,41 | 1,50 | önde |
| karanlik | 600 | 3,93 | 3,94 | 1,20 | 0,0060 | 2,774 | 1,12 | 0,35 | -0,05 | önde |
| parlak | 300 | 5,74 | 4,91 | 1,75 | 0,0085 | 0,029 | 1,95 | 0,43 | 0,37 | önde |
| parlak | 600 | 2,72 | 2,39 | 1,42 | 0,0034 | 0,034 | 1,68 | 0,41 | -0,90 | önde |

| Kesit | Ön ayar | Kol | Hedef MB | MB | Geometri | kbps | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Kodlama sn | Hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | Social 25 MB 30 Seconds 1080p60 | handbrake | — | 7,092 | 1920x818 | 5949,1 | 99,35 | 99,31 | 42,24 | 0,9971 | 0,847 | 23,8 | — |
| hareketli | Social 25 MB 30 Seconds 1080p60 | urun-otomatik | 7,092 | 6,811 | 1920x818 | 5713,1 | 99,50 | 99,45 | 42,10 | 0,9974 | 1,076 | 62,8 | — |
| hareketli | Social 25 MB 30 Seconds 1080p60 | urun-dusurme-kapali | 7,092 | 6,788 | 1920x818 | 5693,9 | 99,49 | 99,45 | 42,09 | 0,9974 | 1,082 | 62,5 | — |
| hareketli | Social 25 MB 1 Minute 720p60 | handbrake | — | 3,538 | 1280x720 | 2967,7 | 96,56 | 96,38 | 39,71 | 0,9948 | 0,760 | 18,3 | — |
| hareketli | Social 25 MB 1 Minute 720p60 | urun-otomatik | 3,538 | 3,404 | 1920x818 | 2855,7 | 98,59 | 98,51 | 40,81 | 0,9958 | 1,134 | 82,6 | — |
| hareketli | Social 25 MB 1 Minute 720p60 | urun-dusurme-kapali | 3,538 | 3,408 | 1920x818 | 2859,2 | 98,58 | 98,50 | 40,81 | 0,9958 | 1,132 | 67,5 | — |
| hareketli | Social 25 MB 2 Minutes 540p60 | handbrake | — | 1,767 | 960x540 | 1482,1 | 89,69 | 89,13 | 37,27 | 0,9902 | 0,539 | 12,5 | — |
| hareketli | Social 25 MB 2 Minutes 540p60 | urun-otomatik | 1,767 | 1,681 | 1920x818 | 1410,0 | 95,37 | 95,16 | 38,96 | 0,9921 | 1,084 | 24,8 | — |
| hareketli | Social 25 MB 2 Minutes 540p60 | urun-dusurme-kapali | 1,767 | 1,676 | 1920x818 | 1405,7 | 95,41 | 95,21 | 38,96 | 0,9921 | 1,062 | 24,8 | — |
| hareketli | Social 25 MB 5 Minutes 360p60 | handbrake | — | 0,591 | 640x360 | 495,5 | 65,22 | 63,98 | 33,33 | 0,9718 | 0,368 | 10,6 | — |
| hareketli | Social 25 MB 5 Minutes 360p60 | urun-otomatik | 0,591 | 0,561 | 1920x818 | 470,7 | 79,04 | 78,28 | 35,28 | 0,9784 | 0,992 | 23,9 | — |
| hareketli | Social 25 MB 5 Minutes 360p60 | urun-dusurme-kapali | 0,591 | 0,561 | 1920x818 | 470,8 | 79,07 | 78,34 | 35,28 | 0,9784 | 0,984 | 23,7 | — |
| hareketli | Social 10 MB 30 Seconds 720p60 | handbrake | — | 2,651 | 1280x720 | 2223,6 | 94,84 | 94,57 | 38,86 | 0,9929 | 0,647 | 18,2 | — |
| hareketli | Social 10 MB 30 Seconds 720p60 | urun-otomatik | 2,651 | 2,509 | 1920x818 | 2105,1 | 97,70 | 97,58 | 40,10 | 0,9946 | 1,133 | 26,4 | — |
| hareketli | Social 10 MB 30 Seconds 720p60 | urun-dusurme-kapali | 2,651 | 2,520 | 1920x818 | 2113,6 | 97,70 | 97,58 | 40,10 | 0,9946 | 1,132 | 26,4 | — |
| hareketli | Social 10 MB 1 Minute 540p60 | handbrake | — | 1,325 | 960x540 | 1111,1 | 85,70 | 85,05 | 36,35 | 0,9867 | 0,499 | 13,7 | — |
| hareketli | Social 10 MB 1 Minute 540p60 | urun-otomatik | 1,325 | 1,256 | 1920x818 | 1053,5 | 93,08 | 92,79 | 38,22 | 0,9900 | 0,978 | 26,7 | — |
| hareketli | Social 10 MB 1 Minute 540p60 | urun-dusurme-kapali | 1,325 | 1,250 | 1920x818 | 1048,3 | 93,06 | 92,78 | 38,21 | 0,9899 | 0,978 | 26,8 | — |
| hareketli | Social 10 MB 2 Minutes 360p60 | handbrake | — | 0,591 | 640x360 | 495,3 | 66,43 | 65,21 | 33,48 | 0,9724 | 0,306 | 11,2 | — |
| hareketli | Social 10 MB 2 Minutes 360p60 | urun-otomatik | 0,591 | 0,563 | 1920x818 | 472,4 | 79,08 | 78,34 | 35,28 | 0,9784 | 0,987 | 23,6 | — |
| hareketli | Social 10 MB 2 Minutes 360p60 | urun-dusurme-kapali | 0,591 | 0,560 | 1920x818 | 469,7 | 78,99 | 78,24 | 35,28 | 0,9784 | 0,993 | 23,7 | — |
| karanlik | Social 25 MB 30 Seconds 1080p60 | handbrake | — | 7,167 | 1920x818 | 6011,9 | 99,15 | 99,13 | 42,47 | 0,9970 | 7,249 | 19,9 | — |
| karanlik | Social 25 MB 30 Seconds 1080p60 | urun-otomatik | 7,167 | 6,932 | 1920x818 | 5815,0 | 99,32 | 99,30 | 42,22 | 0,9971 | 8,291 | 18,3 | — |
| karanlik | Social 25 MB 30 Seconds 1080p60 | urun-dusurme-kapali | 7,167 | 6,938 | 1920x818 | 5819,7 | 99,33 | 99,31 | 42,22 | 0,9971 | 8,301 | 18,1 | — |
| karanlik | Social 25 MB 1 Minute 720p60 | handbrake | — | 3,579 | 1280x720 | 3002,0 | 96,13 | 95,99 | 40,20 | 0,9946 | 7,054 | 12,8 | — |
| karanlik | Social 25 MB 1 Minute 720p60 | urun-otomatik | 3,579 | 3,297 | 1920x818 | 2766,2 | 97,82 | 97,75 | 40,68 | 0,9949 | 9,340 | 29,3 | — |
| karanlik | Social 25 MB 1 Minute 720p60 | urun-dusurme-kapali | 3,579 | 3,440 | 1920x818 | 2885,9 | 97,98 | 97,92 | 40,80 | 0,9950 | 9,247 | 48,5 | — |
| karanlik | Social 25 MB 2 Minutes 540p60 | handbrake | — | 1,788 | 960x540 | 1499,9 | 88,85 | 88,60 | 38,25 | 0,9902 | 6,802 | 12,7 | — |
| karanlik | Social 25 MB 2 Minutes 540p60 | urun-otomatik | 1,788 | 1,729 | 1920x818 | 1450,6 | 93,35 | 93,22 | 39,09 | 0,9911 | 9,294 | 17,9 | — |
| karanlik | Social 25 MB 2 Minutes 540p60 | urun-dusurme-kapali | 1,788 | 1,729 | 1920x818 | 1450,6 | 93,36 | 93,22 | 39,09 | 0,9911 | 9,294 | 18,1 | — |
| karanlik | Social 25 MB 5 Minutes 360p60 | handbrake | — | 0,597 | 640x360 | 500,6 | 67,43 | 66,79 | 34,97 | 0,9745 | 6,352 | 8,8 | — |
| karanlik | Social 25 MB 5 Minutes 360p60 | urun-otomatik | 0,597 | 0,587 | 1920x818 | 492,0 | 77,06 | 76,56 | 36,00 | 0,9781 | 9,058 | 17,6 | — |
| karanlik | Social 25 MB 5 Minutes 360p60 | urun-dusurme-kapali | 0,597 | 0,587 | 1920x818 | 492,0 | 77,05 | 76,56 | 36,00 | 0,9781 | 9,055 | 17,6 | — |
| karanlik | Social 10 MB 30 Seconds 720p60 | handbrake | — | 2,675 | 1280x720 | 2244,2 | 94,19 | 94,02 | 39,48 | 0,9926 | 6,438 | 14,8 | — |
| karanlik | Social 10 MB 30 Seconds 720p60 | urun-otomatik | 2,675 | 2,595 | 1920x818 | 2176,9 | 96,70 | 96,61 | 40,14 | 0,9938 | 9,338 | 19,2 | — |
| karanlik | Social 10 MB 30 Seconds 720p60 | urun-dusurme-kapali | 2,675 | 2,593 | 1920x818 | 2175,5 | 96,68 | 96,59 | 40,13 | 0,9938 | 9,362 | 19,1 | — |
| karanlik | Social 10 MB 1 Minute 540p60 | handbrake | — | 1,337 | 960x540 | 1121,6 | 85,19 | 84,93 | 37,49 | 0,9868 | 6,290 | 11,3 | — |
| karanlik | Social 10 MB 1 Minute 540p60 | urun-otomatik | 1,337 | 1,295 | 1920x818 | 1086,0 | 90,36 | 90,19 | 38,41 | 0,9889 | 9,193 | 19,5 | — |
| karanlik | Social 10 MB 1 Minute 540p60 | urun-dusurme-kapali | 1,337 | 1,296 | 1920x818 | 1087,5 | 90,41 | 90,24 | 38,41 | 0,9889 | 9,166 | 19,6 | — |
| karanlik | Social 10 MB 2 Minutes 360p60 | handbrake | — | 0,594 | 640x360 | 498,0 | 68,17 | 67,58 | 35,04 | 0,9746 | 5,682 | 9,4 | — |
| karanlik | Social 10 MB 2 Minutes 360p60 | urun-otomatik | 0,594 | 0,584 | 1920x818 | 489,9 | 76,95 | 76,45 | 35,98 | 0,9780 | 9,117 | 17,5 | — |
| karanlik | Social 10 MB 2 Minutes 360p60 | urun-dusurme-kapali | 0,594 | 0,584 | 1920x818 | 490,2 | 77,01 | 76,50 | 35,98 | 0,9780 | 9,168 | 17,7 | — |
| parlak | Social 25 MB 30 Seconds 1080p60 | handbrake | — | 7,334 | 1920x818 | 6152,4 | 95,82 | 95,79 | 41,45 | 0,9978 | 0,020 | 26,0 | — |
| parlak | Social 25 MB 30 Seconds 1080p60 | urun-otomatik | 7,334 | 7,066 | 1920x818 | 5927,3 | 95,18 | 95,13 | 41,00 | 0,9977 | 0,048 | 50,1 | — |
| parlak | Social 25 MB 30 Seconds 1080p60 | urun-dusurme-kapali | 7,334 | 7,266 | 1920x818 | 6094,8 | 95,28 | 95,24 | 41,06 | 0,9978 | 0,048 | 25,0 | — |
| parlak | Social 25 MB 1 Minute 720p60 | handbrake | — | 3,646 | 1280x720 | 3058,1 | 90,97 | 90,88 | 39,34 | 0,9960 | 0,011 | 16,1 | — |
| parlak | Social 25 MB 1 Minute 720p60 | urun-otomatik | 3,646 | 3,552 | 1920x818 | 2979,3 | 93,10 | 93,02 | 39,90 | 0,9966 | 0,056 | 26,6 | — |
| parlak | Social 25 MB 1 Minute 720p60 | urun-dusurme-kapali | 3,646 | 3,610 | 1920x818 | 3028,3 | 93,08 | 93,00 | 39,90 | 0,9966 | 0,055 | 26,6 | — |
| parlak | Social 25 MB 2 Minutes 540p60 | handbrake | — | 1,830 | 960x540 | 1535,0 | 83,90 | 83,79 | 37,24 | 0,9928 | 0,009 | 13,1 | — |
| parlak | Social 25 MB 2 Minutes 540p60 | urun-otomatik | 1,830 | 1,762 | 1920x818 | 1478,1 | 89,93 | 89,76 | 38,57 | 0,9945 | 0,057 | 65,1 | — |
| parlak | Social 25 MB 2 Minutes 540p60 | urun-dusurme-kapali | 1,830 | 1,772 | 1920x818 | 1486,2 | 89,97 | 89,80 | 38,58 | 0,9946 | 0,054 | 65,2 | — |
| parlak | Social 25 MB 5 Minutes 360p60 | handbrake | — | 0,611 | 640x360 | 512,4 | 66,32 | 65,98 | 33,90 | 0,9808 | 0,003 | 10,9 | — |
| parlak | Social 25 MB 5 Minutes 360p60 | urun-otomatik | 0,611 | 0,593 | 1920x818 | 497,0 | 79,60 | 78,87 | 35,96 | 0,9871 | 0,054 | 23,9 | — |
| parlak | Social 25 MB 5 Minutes 360p60 | urun-dusurme-kapali | 0,611 | 0,592 | 1920x818 | 496,2 | 79,61 | 78,89 | 35,95 | 0,9871 | 0,055 | 24,2 | — |
| parlak | Social 10 MB 30 Seconds 720p60 | handbrake | — | 2,748 | 1280x720 | 2305,6 | 89,71 | 89,63 | 38,79 | 0,9951 | 0,009 | 18,9 | — |
| parlak | Social 10 MB 30 Seconds 720p60 | urun-otomatik | 2,748 | 2,683 | 1920x818 | 2250,3 | 92,08 | 91,97 | 39,43 | 0,9959 | 0,057 | 27,2 | — |
| parlak | Social 10 MB 30 Seconds 720p60 | urun-dusurme-kapali | 2,748 | 2,694 | 1920x818 | 2259,9 | 92,07 | 91,97 | 39,43 | 0,9959 | 0,057 | 27,2 | — |
| parlak | Social 10 MB 1 Minute 540p60 | handbrake | — | 1,383 | 960x540 | 1159,7 | 81,89 | 81,79 | 36,65 | 0,9910 | 0,006 | 14,3 | — |
| parlak | Social 10 MB 1 Minute 540p60 | urun-otomatik | 1,383 | 1,337 | 1920x818 | 1121,2 | 88,12 | 87,86 | 38,06 | 0,9933 | 0,057 | 72,1 | — |
| parlak | Social 10 MB 1 Minute 540p60 | urun-dusurme-kapali | 1,383 | 1,331 | 1920x818 | 1116,3 | 88,02 | 87,77 | 38,05 | 0,9933 | 0,057 | 70,7 | — |
| parlak | Social 10 MB 2 Minutes 360p60 | handbrake | — | 0,615 | 640x360 | 515,9 | 67,83 | 67,62 | 34,12 | 0,9820 | 0,003 | 11,7 | — |
| parlak | Social 10 MB 2 Minutes 360p60 | urun-otomatik | 0,615 | 0,599 | 1920x818 | 502,7 | 79,70 | 78,99 | 35,99 | 0,9873 | 0,049 | 24,1 | — |
| parlak | Social 10 MB 2 Minutes 360p60 | urun-dusurme-kapali | 0,615 | 0,597 | 1920x818 | 500,7 | 79,68 | 78,97 | 35,98 | 0,9873 | 0,049 | 24,0 | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | Social 25 MB 30 Seconds 1080p60 | 0,14 | 0,14 | -0,14 | 0,0002 | 0,229 | 0,28 | 2,64 | 4,13 | bantta (az bayt) |
| hareketli | Social 25 MB 1 Minute 720p60 | 2,02 | 2,13 | 1,11 | 0,0010 | 0,373 | 2,49 | 4,51 | 3,92 | önde (az bayt) |
| hareketli | Social 25 MB 2 Minutes 540p60 | 5,68 | 6,03 | 1,69 | 0,0020 | 0,545 | 2,80 | 1,98 | 5,11 | önde (az bayt) |
| hareketli | Social 25 MB 5 Minutes 360p60 | 13,82 | 14,30 | 1,95 | 0,0066 | 0,623 | 2,37 | 2,25 | 5,27 | önde (az bayt) |
| hareketli | Social 10 MB 30 Seconds 720p60 | 2,86 | 3,01 | 1,24 | 0,0017 | 0,486 | 2,28 | 1,45 | 5,63 | önde (az bayt) |
| hareketli | Social 10 MB 1 Minute 540p60 | 7,38 | 7,74 | 1,86 | 0,0032 | 0,479 | 2,60 | 1,95 | 5,47 | önde (az bayt) |
| hareketli | Social 10 MB 2 Minutes 360p60 | 12,65 | 13,13 | 1,80 | 0,0060 | 0,680 | 2,30 | 2,11 | 4,85 | önde (az bayt) |
| karanlik | Social 25 MB 30 Seconds 1080p60 | 0,17 | 0,17 | -0,25 | 0,0001 | 1,042 | 0,12 | 0,92 | 3,39 | geride (az bayt) |
| karanlik | Social 25 MB 1 Minute 720p60 | 1,69 | 1,77 | 0,48 | 0,0002 | 2,286 | 1,23 | 2,29 | 8,52 | önde (az bayt) |
| karanlik | Social 25 MB 2 Minutes 540p60 | 4,50 | 4,62 | 0,85 | 0,0009 | 2,492 | 1,51 | 1,41 | 3,40 | önde (az bayt) |
| karanlik | Social 25 MB 5 Minutes 360p60 | 9,63 | 9,77 | 1,04 | 0,0037 | 2,706 | 1,29 | 2,00 | 1,75 | önde |
| karanlik | Social 10 MB 30 Seconds 720p60 | 2,51 | 2,59 | 0,66 | 0,0012 | 2,900 | 1,39 | 1,30 | 3,09 | önde (az bayt) |
| karanlik | Social 10 MB 1 Minute 540p60 | 5,17 | 5,26 | 0,92 | 0,0021 | 2,903 | 1,53 | 1,73 | 3,28 | önde (az bayt) |
| karanlik | Social 10 MB 2 Minutes 360p60 | 8,78 | 8,86 | 0,93 | 0,0035 | 3,434 | 1,29 | 1,86 | 1,65 | önde |
| parlak | Social 25 MB 30 Seconds 1080p60 | -0,64 | -0,66 | -0,45 | -0,0000 | 0,028 | 2,00 | 1,93 | 3,80 | geride (az bayt) |
| parlak | Social 25 MB 1 Minute 720p60 | 2,13 | 2,14 | 0,55 | 0,0005 | 0,045 | 2,84 | 1,65 | 2,64 | önde (az bayt) |
| parlak | Social 25 MB 2 Minutes 540p60 | 6,03 | 5,97 | 1,33 | 0,0017 | 0,048 | 3,78 | 4,97 | 3,85 | önde (az bayt) |
| parlak | Social 25 MB 5 Minutes 360p60 | 13,29 | 12,89 | 2,05 | 0,0063 | 0,052 | 3,54 | 2,19 | 3,10 | önde (az bayt) |
| parlak | Social 10 MB 30 Seconds 720p60 | 2,36 | 2,34 | 0,64 | 0,0008 | 0,048 | 3,15 | 1,44 | 2,46 | önde (az bayt) |
| parlak | Social 10 MB 1 Minute 540p60 | 6,23 | 6,08 | 1,41 | 0,0023 | 0,050 | 3,66 | 5,04 | 3,43 | önde (az bayt) |
| parlak | Social 10 MB 2 Minutes 360p60 | 11,87 | 11,37 | 1,87 | 0,0053 | 0,046 | 3,56 | 2,06 | 2,63 | önde (az bayt) |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | Social 25 MB 30 Seconds 1080p60 | 0,14 | 0,14 | -0,14 | 0,0002 | 0,234 | 0,29 | 2,63 | 4,48 | bantta (az bayt) |
| hareketli | Social 25 MB 1 Minute 720p60 | 2,02 | 2,12 | 1,11 | 0,0010 | 0,372 | 2,47 | 3,69 | 3,79 | önde (az bayt) |
| hareketli | Social 25 MB 2 Minutes 540p60 | 5,73 | 6,08 | 1,69 | 0,0020 | 0,523 | 2,81 | 1,98 | 5,44 | önde (az bayt) |
| hareketli | Social 25 MB 5 Minutes 360p60 | 13,85 | 14,36 | 1,94 | 0,0066 | 0,616 | 2,36 | 2,24 | 5,25 | önde (az bayt) |
| hareketli | Social 10 MB 30 Seconds 720p60 | 2,85 | 3,01 | 1,24 | 0,0017 | 0,486 | 2,28 | 1,45 | 5,20 | önde (az bayt) |
| hareketli | Social 10 MB 1 Minute 540p60 | 7,36 | 7,73 | 1,85 | 0,0032 | 0,480 | 2,58 | 1,96 | 5,99 | önde (az bayt) |
| hareketli | Social 10 MB 2 Minutes 360p60 | 12,57 | 13,02 | 1,79 | 0,0060 | 0,687 | 2,31 | 2,12 | 5,45 | önde (az bayt) |
| karanlik | Social 25 MB 30 Seconds 1080p60 | 0,18 | 0,18 | -0,25 | 0,0001 | 1,051 | 0,12 | 0,91 | 3,30 | geride (az bayt) |
| karanlik | Social 25 MB 1 Minute 720p60 | 1,85 | 1,93 | 0,59 | 0,0004 | 2,193 | 1,35 | 3,79 | 4,02 | önde (az bayt) |
| karanlik | Social 25 MB 2 Minutes 540p60 | 4,50 | 4,62 | 0,85 | 0,0009 | 2,492 | 1,51 | 1,43 | 3,40 | önde (az bayt) |
| karanlik | Social 25 MB 5 Minutes 360p60 | 9,63 | 9,77 | 1,04 | 0,0037 | 2,704 | 1,29 | 2,00 | 1,75 | önde |
| karanlik | Social 10 MB 30 Seconds 720p60 | 2,49 | 2,57 | 0,65 | 0,0011 | 2,924 | 1,39 | 1,29 | 3,16 | önde (az bayt) |
| karanlik | Social 10 MB 1 Minute 540p60 | 5,22 | 5,31 | 0,93 | 0,0021 | 2,876 | 1,53 | 1,73 | 3,14 | önde (az bayt) |
| karanlik | Social 10 MB 2 Minutes 360p60 | 8,84 | 8,92 | 0,94 | 0,0035 | 3,486 | 1,30 | 1,88 | 1,59 | önde |
| parlak | Social 25 MB 30 Seconds 1080p60 | -0,53 | -0,55 | -0,38 | 0,0000 | 0,028 | 1,92 | 0,96 | 0,95 | geride |
| parlak | Social 25 MB 1 Minute 720p60 | 2,11 | 2,12 | 0,56 | 0,0005 | 0,044 | 2,96 | 1,65 | 0,98 | önde |
| parlak | Social 25 MB 2 Minutes 540p60 | 6,07 | 6,01 | 1,34 | 0,0017 | 0,045 | 3,65 | 4,98 | 3,28 | önde (az bayt) |
| parlak | Social 25 MB 5 Minutes 360p60 | 13,29 | 12,91 | 2,05 | 0,0063 | 0,052 | 3,52 | 2,22 | 3,26 | önde (az bayt) |
| parlak | Social 10 MB 30 Seconds 720p60 | 2,36 | 2,34 | 0,64 | 0,0008 | 0,048 | 3,20 | 1,44 | 2,02 | önde (az bayt) |
| parlak | Social 10 MB 1 Minute 540p60 | 6,13 | 5,98 | 1,40 | 0,0023 | 0,051 | 3,71 | 4,94 | 3,89 | önde (az bayt) |
| parlak | Social 10 MB 2 Minutes 360p60 | 11,85 | 11,35 | 1,87 | 0,0053 | 0,045 | 3,56 | 2,05 | 3,04 | önde (az bayt) |
