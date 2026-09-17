# HandBrake Kıyası Hb-2b: Tavan Bekçisi Ve SVT Karanlık Bantlaşma

Dal `t0/hb-2b`. Ağır kodlama yalnız GitHub Actions'ta (`handbrake-kiyas.yml`, `tools/kalite-paketi-3/hb.ps1`).
Danışma kaydı: `docs/danisma/2026-09-17-hb2b-fable.md`.

## Açık 5: Dosyasız Biten Koşum

**Kök neden.** Üç deneme de tavanın üstünde kalınca `EncodeRunner` hiçbir çıktı yazmadan bitiyordu. Düzeltme
adımı ortalama verimle ölçekliyordu; rampa gibi verimi dalgalanan içerikte üçüncü istek de taşıyordu.

**Düzeltme (6e8a8137).** İki tavan üstü denemeden sonra son deneme `CeilingGuard` ile kurulur: tavanın 0,90'ına,
koşumda görülen en kötü verime bölünerek, denenen en küçük isteğin 0,95'i altında. Sormayan yolda hiç sığmayan
koşum en küçük sonucu teslim eder (`Success + OverTarget + CeilingExceeded`, CLI çıkış kodu 3, JSON `overTarget`).

| Hücre | Önce (koşum 35177004568) | Sonra (koşum 35181832415) |
|---|---|---|
| rampa 1200 e0-duzen | 3 deneme tavan üstü, dosya yok | 3/3 tekrar tavan altı: 1,294 / 1,135 / 1,222 MB |
| rampa 1200 e0 | — | 1174k→1,488, 1110k→1,849, bekçi 791k→1,273 (bant altı kabul) |
| karanlik 100 e0 | dosya yok | 0,1335 MB, 3 deneme, bekçi 68k, en küçük (84k) teslim |
| karanlik 100 e0-duzen | 0,1179 MB (ölçüt ≤0,117) | 0,1176 MB, 1036x442, 1 deneme |

Ölçüt değişikliği: karanlik 100 e0-duzen bandı 0,1123–0,1221 MB (hedefin ±%4'ü); rampa hücresinde bekçi şartı yalnız
iki tavan üstü denemeden sonra aranır, çünkü bekçi iki örnek ister ve üç tekrar bant altı yoldan döndü.

SVT-AV1 4.2 bayt garantisi vermiyor (svtbekci, koşum 35179661825); SVT'de bekçi yalnız nişanı kısar.

## Tepe = Tampon = K: Hangi Kodlayıcıda Sınır Tutuyor

Koşum 35183264007 (`tavanbekci--2`). Ham 2 geçiş, sınır = K × (T+1) / 8. Negatif kol tepe serbest.

| Kesit | K | libx264 tepe=K | negatif | libx265 tepe=K | negatif |
|---|---|---|---|---|---|
| rampa | 1200 | 0,4236 ≤ 1,5736 | 1,3643 | **1,6844 > 1,5736** | 1,3854 |
| rampa | 300 | 0,0928 ≤ 0,3934 | 1,7897 taşar | **0,8405 > 0,3934** | 0,3956 taşar |
| rampa | 100 | 0,0326 ≤ 0,1311 | 12,3723 taşar | **0,4865 > 0,1311** | 19,7376 taşar |
| hareketli | 1200 | 1,4313 ≤ 1,5736 | 1,4465 | 1,3903 | 1,4090 |
| hareketli | 300 | 0,3456 ≤ 0,3934 | 0,3443 | 0,3500 | 0,3545 |
| hareketli | 100 | 0,1345 > 0,1311 (taban) | 0,1349 | 0,2841 > 0,1311 | 0,2849 |

libx264'te tepe eşitlemesi sınırı tutuyor (hareketli 100'de iki kol da kodlayıcı tabanında). libx265'te tutmuyor,
rampa 300'de taşmayı 327'den 700 kbit/sn'ye büyütüyor. Bu yüzden `CeilingGuard.CapsPeakAtRate` yalnız libx264.
Üründe x264 bekçisi bu kesitlerde hiç koşmadı (x264 bant altında kaldı).

## Açık 2: SVT Karanlık Bantlaşma

Koşum 35181832415, `svtbant`. Kapı CAMBI(ii) (zscale dither=none, 8 bit) ≤ 7,5. Her anahtarın SVT günlüğünde
satırı var; uydurma anahtar "Error parsing option vidshrinkuydurma: 1." veriyor.

| karanlik | e0 | lqb20 | lqb40 | lqb60 | vb3 | qsc2 | tune0 | fg4 (tahıl kapalı) | ürün x265 | HB x265 |
|---|---|---|---|---|---|---|---|---|---|---|
| 600 CAMBI | 9,28 | 9,18 | 9,22 | 9,02 | 9,01 | 9,34 | 8,87 | 0,98 (9,32) | 6,26 | 6,47 |
| 600 sn | 26,2 | | | | | | | 74,6 | 68,9 | 56,5 |
| 2000 CAMBI | 9,38 | 9,40 | 9,41 | 9,35 | 9,53 | 9,47 | 8,40 | 0,78 (9,37) | 6,67 | 6,50 |
| 2000 sn | 26,3 | | | | | | | 75,0 | 96,1 | 73,4 |

VMAF-NEG(ii) 600: e0 80,45, tune0 79,97, vb3 75,82, ürün x265 76,09, HB x265 77,19.

Hüküm: hiçbir SVT kolu kapıyı geçmiyor; tahıl kolu örtü (tahıl kapalı okuma 9,3). Ölçer kontrolünde (iii)
dither=ordered gürültüsüz gradyanda düşmedi (19,73 → 20,49), o sütun yazılmadı. Ürünün x265 kolu CAMBI'yi ve süre
şartını (≤1,5× HB) geçiyor; karanlık sahnede x265'e geçiş kullanıcının kararı.

Karanlık dışı 2000 (e0 → tune0): parlak CAMBI 0,057 → 0,036, hareketli 1,133 → 0,813, ekran VMAF 92,81 → 92,78.
