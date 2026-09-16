# SVT-AV1 e0 Dizgesi: Ürün Yolunda Hedef Bant ve RelativeBitrateNeed

Durum: **ölçüldü, kod değişmedi.** Dal `t0/av1-vb` ürün dizgesini `tune=1:enable-variance-boost=0` (e0) yaptı
(`src/VidShrink.Core/FfmpegArguments.cs:532`). Eski dizge `tune=0:enable-variance-boost=1:variance-boost-strength=2`
(e1). Danışman yanıtı iki ölçüm istedi: (1) ≥50 MB hedefte ürünün 2 geçişli yolu ilk denemede `FillBand`
(0,972–1,0) içine düşüyor mu; (2) `CodecModel.RelativeBitrateNeed["libsvtav1"] = 0.55` variance boost kapanınca
hâlâ doğru mu.

## Düzenek

- İş akışı `.github/workflows/kalite-olcumu.yml`, iki yeni iş: `hedefbant` ve `oran` (`tools/kalite-paketi-3/kos.ps1`).
  e1 kolu koşucuda ayrı bir `git worktree` içinde dizge geri yazılarak derlenen ikinci Bench'tir; başka fark yok.
  Her satırdaki `svtav1-params` sütunu ürünün `komut:` satırından okundu, iki kolun gerçekten farklı dizgeyle koştuğunu gösterir.
- Koşum **35131959230** (etiket `olcum-kalite-hedefbant+oran__karanlik+hareketli+ekran--1`): `hedefbant` 120 sn,
  hedef 50 ve 100 MB; `oran` 10 sn kesitler.
- Koşum **35134885578** (`workflow_dispatch`, dal `t0/av1-vb-olcum`, `isler=hedefbant`, `uzun_sure=360`, `hedefler=50`):
  aynı hedefin düşük bit hızı (1143k).
- Kaynak Sintel 1080p, sha256 `97F1DBC66231DF42AD49BD8C29AA174B8F48933058E47E7157D4BA63D93A8EFA`; ekran
  `Debugging_1920x1080_30fps_8bit_420.y4m`, sha256 `8004BE81AEDB40F9C26DB25FF72D8725D832EF3B95747FB4C9FC8A3693EB4E8E`.
  Kesit seçimi `kesitler.json`: `karanlik` 600, `hareketli` 325, `ekran` 0 (kaynak 15 sn'den kısa).
- `hedefbant`: kesitin başlangıcından uzun parça x264 `-crf 4 veryfast` ara dosyaya kesildi (kaynak MB: 120 sn
  `karanlik` 578,195, `hareketli` 704,598, `ekran` 57,955; 360 sn `karanlik` 1929,671, `hareketli` 1977,548,
  `ekran` 174,13). 360 sn film parçası kaynağın sonunu aştığı için pencere geri kaydı: `karanlik` 348–708,
  `hareketli` 325–685 (10 sn'lik kesit pencerenin içinde; pencerenin geri kalanı o kesit türünden seçilmedi). Ekran klibi döngüyle uzatıldı.
  Komut: `bench shrink <parça> <hedef> --speed quality --no-measure --force-codec libsvtav1 --no-resolution-drop --no-fps-drop`
  (ürünün planı, kalibrasyonu ve `EncodeRunner` döngüsü; preset 6, 2 geçiş). Deneme satırları Bench günlüğündeki
  `deneme N:` izinden, teslim baytı çıkan mp4'ten.
- `oran`: 10 sn kesitte iki geçişli ABR, `-g 240`, `yuv420p`; libx264 slow, libx265 slow, SVT-AV1 p6 e0 ve e1, her biri
  `bench psy-args` ekleriyle; 400/800/1600/3200/6400 kbit. Ölçü `bench measure-pair --fps`. Eş kalite oranı:
  kbps ekseninde log-doğrusal eğri, iki eğrinin ortak kalite aralığında 41 noktada log kbps farkının ortalaması
  (`python tools/kalite-paketi-3/ozet.py oran <dizin> karanlik,hareketli,ekran`).

## Sonuç 1 — Hedef Bant

- Film kesitleri, ≥50 MB, kol başına 6 hücre: **e0 ilk denemede 6/6 bantta**, doluluk %98,19–%98,41.
  e1 5/6: `hareketli` 50 MB 120 sn'de ilk deneme 48,592 MB (bant alt 48,600) → `RetryScaled`, ikinci deneme
  49,300 MB. e0 aynı hücrede 49,150 MB ile ilk denemede bantta.
- Ekran (döngülü): iki kol da 3/3 hücrede ilk denemede bant altında (e0 %68,29 / %36,86 / %88,74; e1 %68,75 /
  %36,35 / %81,08). 120 sn'de ikinci deneme de bant altında kabul edildi. 360 sn'de ikinci deneme tavanı aştı
  (e0 50,18 MB, e1 52,669 MB) ve **teslim edilmedi**; üçüncü deneme e0 48,725 MB bantta, e1 47,226 MB bant altı kabul.
  Ekran 100 MB hücresinde kaynak 57,955 MB olduğundan hedef %95 kaynak sınırına (55,06 MB) indi; ilk deneme
  4051k bu yüzden.
- **Teslim taşması: 18 hücrenin 0'ında.** KRİTİK yok.

## Sonuç 2 — RelativeBitrateNeed

- Film, eş kalitede SVT-AV1 / libx264 kbps oranı: e0 VMAF-NEG `karanlik` 0,492, `hareketli` 0,430; XPSNR 0,683, 0,572.
  e1 VMAF-NEG 0,590, 0,519; XPSNR 0,620, 0,566. Dört film değerinin ortalaması e0 0,544, e1 0,574.
- Variance boost kapanınca VMAF-NEG'de gereken bit düşüyor (0,590 → 0,492, 0,519 → 0,430), XPSNR'da
  `karanlik`ta artıyor (0,620 → 0,683), `hareketli`de yerinde (0,566 → 0,572).
- Aynı ölçekte libx265 / libx264 (modelde 0,68): VMAF-NEG 0,548, 0,513; XPSNR 0,728, 0,638. e0 / libx265:
  VMAF-NEG 0,885, 0,815; XPSNR 0,906, 0,857 (modelde 0,55 / 0,68 = 0,809).
- Ekran oranları (e0 0,350 / 0,430) ortak aralığı dar (VMAF-NEG 93,29 … 97,33) ve doygun; karara girmedi.

Öneri (kod değişmedi): **0,55 kalsın.** e0'ın film ortalaması 0,544 ve iki ölçünün aralığı (0,430 … 0,683) 0,55'i
içeriyor; variance boost'un kapanması katsayıyı yükseltmiyor, e1'e göre düşürüyor. Modelin x265'e göre oranı
(0,809) ölçülen e0 / libx265 aralığının (0,815 … 0,906) biraz altında; bu x265 tarafının VMAF-NEG'de modelden
ucuz ölçülmesinden, ayrı bir kalibrasyon konusu.

**Ölçülmedi:** 10 sn dışındaki kesitlerde oran, preset 6 dışı, HDR, donanım kodlayıcılar, 50 MB altı hedefler
(bant 0,95 / 0,92), öznel izleme. Her hücre tek koşum; iki geçişli ABR bu koşucuda bayt düzeyinde tekrarlanmıyor
(`av1-esbayt.md`), `hareketli` e1 48,592 MB bant altından 0,008 MB uzakta ve tekrarda yön değiştirebilir.

## Hedef Bant — Koşum 35131959230 (120 sn)

| Kesit | Kol | svtav1-params | Kaynak | Hedef MB | Bant alt MB | 1. deneme kbit | 1. hedeflenen MB | 1. çıkan MB | 1. doluluk % | 1. bantta | 1. dal | Deneme | Teslim MB | Teslim taşma | Kodlama sn |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 600+120,0 sn | 50 | 48,600 | 3429 | 49,300 | 49,117 | 98,23 | evet | in band | 1 | 49,117 | yok | 244,0 |
| karanlik | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 600+120,0 sn | 50 | 48,600 | 3429 | 49,300 | 49,013 | 98,03 | evet | in band | 1 | 49,013 | yok | 278,5 |
| karanlik | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 600+120,0 sn | 100 | 97,200 | 6858 | 98,600 | 98,194 | 98,19 | evet | in band | 1 | 98,194 | yok | 230,2 |
| karanlik | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 600+120,0 sn | 100 | 97,200 | 6858 | 98,600 | 98,081 | 98,08 | evet | in band | 1 | 98,081 | yok | 248,7 |
| hareketli | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 325+120,0 sn | 50 | 48,600 | 3429 | 49,300 | 49,150 | 98,30 | evet | in band | 1 | 49,150 | yok | 370,0 |
| hareketli | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 325+120,0 sn | 50 | 48,600 | 3429 | 49,300 | 48,592 | 97,18 | hayır | under band | 2 | 49,300 | yok | 723,0 |
| hareketli | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 325+120,0 sn | 100 | 97,200 | 6858 | 98,600 | 98,342 | 98,34 | evet | in band | 1 | 98,342 | yok | 328,3 |
| hareketli | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 325+120,0 sn | 100 | 97,200 | 6858 | 98,600 | 98,063 | 98,06 | evet | in band | 1 | 98,063 | yok | 368,8 |
| ekran | e0 | `keyint=300:scd=1:tune=1:enable-variance-boost=0` | 0+120,0 sn döngü | 50 | 48,600 | 3429 | 49,300 | 34,146 | 68,29 | hayır | under band | 2 | 38,707 | yok | 317,1 |
| ekran | e1 | `keyint=300:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 0+120,0 sn döngü | 50 | 48,600 | 3429 | 49,300 | 34,377 | 68,75 | hayır | under band | 2 | 37,904 | yok | 324,1 |
| ekran | e0 | `keyint=300:scd=1:tune=1:enable-variance-boost=0` | 0+120,0 sn döngü | 100 | 97,200 | 4051 | 98,600 | 36,857 | 36,86 | hayır | under band | 2 | 44,263 | yok | 303,5 |
| ekran | e1 | `keyint=300:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 0+120,0 sn döngü | 100 | 97,200 | 4051 | 98,600 | 36,354 | 36,35 | hayır | under band | 2 | 43,053 | yok | 314,0 |

| Kesit | Kol | Hedef MB | Denemeler (no:dal:kbit:çıkan MB) |
|---|---|---|---|
| karanlik | e0 | 50 | 1:in band:3429k:49.117 |
| karanlik | e1 | 50 | 1:in band:3429k:49.013 |
| karanlik | e0 | 100 | 1:in band:6858k:98.194 |
| karanlik | e1 | 100 | 1:in band:6858k:98.081 |
| hareketli | e0 | 50 | 1:in band:3429k:49.15 |
| hareketli | e1 | 50 | 1:under band:3429k:48.592 | 2:in band:3479k:49.3 |
| hareketli | e0 | 100 | 1:in band:6858k:98.342 |
| hareketli | e1 | 100 | 1:in band:6858k:98.063 |
| ekran | e0 | 50 | 1:under band:3429k:34.146 | 2:under band accepted:4951k:38.707 |
| ekran | e1 | 50 | 1:under band:3429k:34.377 | 2:under band accepted:4917k:37.904 |
| ekran | e0 | 100 | 1:under band:4051k:36.857 | 2:under band accepted:10837k:44.262 |
| ekran | e1 | 100 | 1:under band:4051k:36.354 | 2:under band accepted:10987k:43.053 |

## Hedef Bant — Koşum 35134885578 (360 sn)

| Kesit | Kol | svtav1-params | Kaynak | Hedef MB | Bant alt MB | 1. deneme kbit | 1. hedeflenen MB | 1. çıkan MB | 1. doluluk % | 1. bantta | 1. dal | Deneme | Teslim MB | Teslim taşma | Kodlama sn |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 348+360,0 sn | 50 | 48,600 | 1143 | 49,300 | 49,123 | 98,25 | evet | in band | 1 | 49,123 | yok | 653,1 |
| karanlik | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 348+360,0 sn | 50 | 48,600 | 1143 | 49,300 | 49,096 | 98,19 | evet | in band | 1 | 49,096 | yok | 691,2 |
| hareketli | e0 | `keyint=240:scd=1:tune=1:enable-variance-boost=0` | 325+360,0 sn | 50 | 48,600 | 1143 | 49,300 | 49,203 | 98,41 | evet | in band | 1 | 49,203 | yok | 932,7 |
| hareketli | e1 | `keyint=240:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 325+360,0 sn | 50 | 48,600 | 1143 | 49,300 | 49,170 | 98,34 | evet | in band | 1 | 49,170 | yok | 1033,3 |
| ekran | e0 | `keyint=300:scd=1:tune=1:enable-variance-boost=0` | 0+360,0 sn döngü | 50 | 48,600 | 1143 | 49,300 | 44,372 | 88,74 | hayır | under band | 3 | 48,725 | yok | 2293,8 |
| ekran | e1 | `keyint=300:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2` | 0+360,0 sn döngü | 50 | 48,600 | 1143 | 49,300 | 40,539 | 81,08 | hayır | under band | 3 | 47,226 | yok | 2476,5 |

| Kesit | Kol | Hedef MB | Denemeler (no:dal:kbit:çıkan MB) |
|---|---|---|---|
| karanlik | e0 | 50 | 1:in band:1143k:49.123 |
| karanlik | e1 | 50 | 1:in band:1143k:49.096 |
| hareketli | e0 | 50 | 1:in band:1143k:49.203 |
| hareketli | e1 | 50 | 1:in band:1143k:49.17 |
| ekran | e0 | 50 | 1:under band:1143k:44.372 | 2:over ceiling:1270k:50.18 | 3:in band:1248k:48.725 |
| ekran | e1 | 50 | 1:under band:1143k:40.539 | 2:over ceiling:1390k:52.669 | 3:under band accepted:1301k:47.226 |

## Eş Kalite Oranı — Koşum 35131959230

| Kesit | Kol | İstenen kbit | kbps | VMAF-NEG ort | XPSNR | Karanlık PSNR | Kodlama sn |
|---|---|---|---|---|---|---|---|
| karanlik | libx264 | 400 | 422,0 | 41,45 | 30,10 | 34,24 | 15,9 |
| karanlik | libx265 | 400 | 398,7 | 66,56 | 34,11 | 37,40 | 61,5 |
| karanlik | svtav1-e0 | 400 | 411,5 | 73,08 | 35,43 | 38,74 | 20,9 |
| karanlik | svtav1-e1 | 400 | 392,4 | 64,86 | 35,36 | 37,70 | 20,6 |
| karanlik | libx264 | 800 | 824,9 | 67,23 | 34,53 | 37,64 | 14,9 |
| karanlik | libx265 | 800 | 789,6 | 83,07 | 36,68 | 39,73 | 72,2 |
| karanlik | svtav1-e0 | 800 | 813,3 | 86,23 | 37,53 | 40,79 | 22,7 |
| karanlik | svtav1-e1 | 800 | 803,2 | 82,21 | 37,86 | 40,08 | 23,5 |
| karanlik | libx264 | 1600 | 1617,6 | 85,81 | 38,07 | 40,56 | 15,9 |
| karanlik | libx265 | 1600 | 1562,0 | 93,60 | 39,00 | 42,13 | 104,3 |
| karanlik | svtav1-e0 | 1600 | 1619,9 | 94,42 | 39,39 | 42,71 | 21,8 |
| karanlik | svtav1-e1 | 1600 | 1591,9 | 92,39 | 39,69 | 41,97 | 22,9 |
| karanlik | libx264 | 3200 | 3203,6 | 95,58 | 40,69 | 43,24 | 18,9 |
| karanlik | libx265 | 3200 | 3154,0 | 98,30 | 41,09 | 44,53 | 106,9 |
| karanlik | svtav1-e0 | 3200 | 3243,5 | 98,35 | 41,06 | 44,65 | 23,0 |
| karanlik | svtav1-e1 | 3200 | 3176,0 | 97,71 | 41,23 | 44,08 | 24,6 |
| karanlik | libx264 | 6400 | 6386,2 | 98,79 | 42,93 | 45,87 | 24,0 |
| karanlik | libx265 | 6400 | 6298,2 | 99,47 | 42,91 | 46,83 | 127,4 |
| karanlik | svtav1-e0 | 6400 | 6510,4 | 99,43 | 42,45 | 46,47 | 22,2 |
| karanlik | svtav1-e1 | 6400 | 6339,7 | 99,23 | 42,55 | 45,94 | 24,1 |
| hareketli | libx264 | 400 | 407,4 | 30,40 | 26,61 | 26,48 | 17,3 |
| hareketli | libx265 | 400 | 395,7 | 65,35 | 32,81 | 31,36 | 71,8 |
| hareketli | svtav1-e0 | 400 | 397,8 | 75,01 | 34,60 | 32,86 | 23,2 |
| hareketli | svtav1-e1 | 400 | 420,9 | 68,94 | 34,30 | 31,24 | 23,4 |
| hareketli | libx264 | 800 | 822,6 | 63,47 | 32,71 | 30,95 | 14,1 |
| hareketli | libx265 | 800 | 790,7 | 84,98 | 36,02 | 34,47 | 79,9 |
| hareketli | svtav1-e0 | 800 | 794,4 | 89,12 | 37,22 | 35,54 | 26,1 |
| hareketli | svtav1-e1 | 800 | 796,7 | 85,49 | 37,20 | 34,22 | 26,7 |
| hareketli | libx264 | 1600 | 1602,9 | 86,56 | 37,01 | 34,80 | 24,3 |
| hareketli | libx265 | 1600 | 1584,5 | 95,36 | 38,88 | 37,77 | 99,3 |
| hareketli | svtav1-e0 | 1600 | 1584,9 | 96,27 | 39,33 | 38,14 | 25,3 |
| hareketli | svtav1-e1 | 1600 | 1585,5 | 94,67 | 39,44 | 36,93 | 26,9 |
| hareketli | libx264 | 3200 | 3175,9 | 96,23 | 40,23 | 38,53 | 19,5 |
| hareketli | libx265 | 3200 | 3143,5 | 98,78 | 41,11 | 40,90 | 120,1 |
| hareketli | svtav1-e0 | 3200 | 3191,1 | 98,80 | 41,08 | 40,75 | 26,4 |
| hareketli | svtav1-e1 | 3200 | 3156,9 | 98,39 | 41,26 | 39,90 | 29,0 |
| hareketli | libx264 | 6400 | 6362,5 | 99,25 | 42,63 | 42,17 | 24,0 |
| hareketli | libx265 | 6400 | 6289,0 | 99,64 | 42,96 | 43,99 | 145,2 |
| hareketli | svtav1-e0 | 6400 | 6379,1 | 99,56 | 42,34 | 43,05 | 24,7 |
| hareketli | svtav1-e1 | 6400 | 6348,6 | 99,50 | 42,49 | 42,50 | 27,0 |
| ekran | libx264 | 400 | 390,8 | 67,93 | 31,04 | 32,96 | 5,3 |
| ekran | libx265 | 400 | 415,6 | 77,09 | 32,34 | 35,82 | 18,5 |
| ekran | svtav1-e0 | 400 | 405,3 | 93,29 | 42,02 | 46,01 | 10,5 |
| ekran | svtav1-e1 | 400 | 402,9 | 92,71 | 42,00 | 44,96 | 10,7 |
| ekran | libx264 | 800 | 763,0 | 86,85 | 37,34 | 39,13 | 4,2 |
| ekran | libx265 | 800 | 765,6 | 89,87 | 38,47 | 42,83 | 18,9 |
| ekran | svtav1-e0 | 800 | 686,7 | 95,78 | 48,32 | 53,57 | 10,6 |
| ekran | svtav1-e1 | 800 | 670,9 | 95,58 | 48,04 | 52,82 | 10,8 |
| ekran | libx264 | 1600 | 1464,9 | 94,31 | 45,20 | 47,07 | 4,6 |
| ekran | libx265 | 1600 | 1412,2 | 95,10 | 46,16 | 50,97 | 19,6 |
| ekran | svtav1-e0 | 1600 | 1346,5 | 96,94 | 55,35 | 61,98 | 12,5 |
| ekran | svtav1-e1 | 1600 | 1277,2 | 96,91 | 55,09 | 61,69 | 12,0 |
| ekran | libx264 | 3200 | 2746,9 | 96,66 | 54,54 | 55,89 | 4,8 |
| ekran | libx265 | 3200 | 2340,0 | 96,75 | 54,46 | 58,75 | 21,1 |
| ekran | svtav1-e0 | 3200 | 1866,2 | 97,28 | 60,56 | 67,25 | 11,5 |
| ekran | svtav1-e1 | 3200 | 1739,9 | 97,21 | 59,41 | 66,07 | 11,5 |
| ekran | libx264 | 6400 | 4551,7 | 97,39 | 66,62 | 66,80 | 5,6 |
| ekran | libx265 | 6400 | 3880,9 | 97,35 | 64,25 | 67,44 | 22,0 |
| ekran | svtav1-e0 | 6400 | 2147,6 | 97,33 | 62,13 | 69,19 | 13,9 |
| ekran | svtav1-e1 | 6400 | 2111,3 | 97,30 | 61,54 | 68,67 | 16,5 |

| Kesit | Ölçü | Kol | Eş kalitede kbps oranı (kol / libx264) | Ortak aralık | Kol / libx265 |
|---|---|---|---|---|---|
| karanlik | VMAF-NEG | libx265 | 0,548 | 66,56 … 98,79 | — |
| karanlik | VMAF-NEG | svtav1-e0 | 0,492 | 73,08 … 98,79 | 0,885 |
| karanlik | VMAF-NEG | svtav1-e1 | 0,590 | 64,86 … 98,79 | 1,088 |
| karanlik | XPSNR | libx265 | 0,728 | 34,11 … 42,91 | — |
| karanlik | XPSNR | svtav1-e0 | 0,683 | 35,43 … 42,45 | 0,906 |
| karanlik | XPSNR | svtav1-e1 | 0,620 | 35,36 … 42,55 | 0,822 |
| hareketli | VMAF-NEG | libx265 | 0,513 | 65,35 … 99,25 | — |
| hareketli | VMAF-NEG | svtav1-e0 | 0,430 | 75,01 … 99,25 | 0,815 |
| hareketli | VMAF-NEG | svtav1-e1 | 0,519 | 68,94 … 99,25 | 1,003 |
| hareketli | XPSNR | libx265 | 0,638 | 32,81 … 42,63 | — |
| hareketli | XPSNR | svtav1-e0 | 0,572 | 34,60 … 42,34 | 0,857 |
| hareketli | XPSNR | svtav1-e1 | 0,566 | 34,30 … 42,49 | 0,851 |
| ekran | VMAF-NEG | libx265 | 0,819 | 77,09 … 97,35 | — |
| ekran | VMAF-NEG | svtav1-e0 | 0,350 | 93,29 … 97,33 | 0,422 |
| ekran | VMAF-NEG | svtav1-e1 | 0,358 | 92,71 … 97,30 | 0,431 |
| ekran | XPSNR | libx265 | 0,897 | 32,34 … 64,25 | — |
| ekran | XPSNR | svtav1-e0 | 0,430 | 42,02 … 62,13 | 0,485 |
| ekran | XPSNR | svtav1-e1 | 0,424 | 42,00 … 61,54 | 0,479 |
