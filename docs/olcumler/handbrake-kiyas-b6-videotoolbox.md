# B6 — VideoToolbox: macOS 15'te HEVC Donanım Kodlayıcı

Durum: **ölçüldü, kod değişmedi.** Ürünün `hevc_videotoolbox` çıktısı ile HandBrake 1.11.2'nin `H.265 Apple
VideoToolbox 1080p` ön ayarı aynı bayta oturtuldu.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, iş `vt` (macos-15). ffmpeg `brew install ffmpeg`; `ffmpeg-encoders.txt` `hevc_videotoolbox`u
  listeliyor. HandBrakeCLI `https://github.com/HandBrake/HandBrake/releases/download/1.11.2/HandBrakeCLI-1.11.2.dmg`
  (sha256 `14463aa81038aaa3ce421dc6cee65fd6c82fdabda040931541ccca38939299fa`).
- Kesitler B1'deki dördü, 2000 ve 5500 kbit. Kollar `urun-vt` (Bench `shrink --force-codec hevc_videotoolbox`),
  `handbrake-vt` (`-Z 'H.265 Apple VideoToolbox 1080p' -a none --crop-mode none -f av_mkv`, bayta ±%2),
  `negatif-handbrake-yarim-bit`.
- Ürünün kurduğu komut: `-c:v hevc_videotoolbox -preset slow -b:v 1956k -maxrate 2934k -bufsize 3912k -pass 2
  -passlogfile …` (`vt-karanlik-2000-urun.log`).

## Sonuç

- **Film kesitlerinde 6 satırın 6'sı geride**, hepsi XPSNR'dan: −0,54 ile −1,17 dB. VMAF-NEG ort'ta aynı satırlar
  karışık: `karanlik` 2000 +3,05, `hareketli` 2000 +2,60, diğerleri −0,40 ile +0,18.
- **CAMBI'de büyük açık**: `karanlik` 9,06 ve 8,17'ye karşı HandBrake 2,84 ve 2,15 (+6,22, +6,02); `hareketli` +0,88,
  +0,66. Negatif kontrol bile (2,93, 2,73) üründen düşük.
- **`ekran` 2 satırda önde**: VMAF-NEG +8,01 / +9,01, XPSNR +8,22 / +9,60, CAMBI −0,80 / −0,74.
- `ekran`da ürün bant altında teslim etti: 2000'de %62,2 ("under band accepted", 3417k istenip 0,658 MB), 5500'de
  %23,9 (5380k istenip 0,695 MB). HandBrake bu baytlara ayarlandı.
- Süre: kodlama saniyesi iki tarafta da 2,5–10,9 sn; ürünün toplamı plan ve kalibrasyonla 26,0–39,8 sn.
- Negatif kontrol film satırlarında VMAF-NEG'i −3,15 ile −17,59, XPSNR'ı −1,89 ile −3,50 düşürdü.

Karar: VideoToolbox'ta ürün film içeriğinde HandBrake'in XPSNR'da ve bantlaşmada gerisinde, ekran kaydında önünde.
**Ölçülmedi:** h264_videotoolbox, Apple Silicon dışı makine, ürünün otomatik codec seçiminin VT'yi seçmesi
(`PlanParser.AllowedCodecs` VT'yi içermiyor, kol `--force-codec` ile zorlandı).

Açık kusur (kod değişikliği ister, yalnız raporlandı): `CodecModel.IsHardware` yalnız NVENC/QSV/AMF'yi donanım sayıyor;
VideoToolbox yazılım gibi planlanıp `-pass 2 -passlogfile` ve `-preset slow` alıyor. ffmpeg'in `hevc_videotoolbox`unda bunların
karşılığı olduğu doğrulanmadı; iki geçişin VT'de ne yaptığı ve toplam sürenin 26–40 sn olmasındaki payı ölçülmedi.

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Toplam sn | HB deneme | Ek hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 2000 | urun-vt | hevc_videotoolbox | 1920x818 | 1956,5 | 2445589 | 91,68 | 91,37 | 37,87 | 0,9894 | 9,055 | 41,63 | 5,4 | — | 37,5 | — | — |
| karanlik | 2000 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 1940,6 | 2425711 | 88,62 | 88,12 | 38,55 | 0,9885 | 2,836 | 40,20 | 5,4 | — | 5,4 | 1 | — |
| karanlik | 2000 | negatif-handbrake-yarim-bit | — | 1920x818 | 981,9 | 1227361 | 71,03 | 68,99 | 35,54 | 0,9723 | 2,927 | 37,78 | 5,0 | — | — | — | — |
| karanlik | 5500 | urun-vt | hevc_videotoolbox | 1920x818 | 5221,1 | 6526364 | 98,82 | 98,78 | 41,39 | 0,9965 | 8,174 | 45,07 | 5,2 | — | 33,9 | — | — |
| karanlik | 5500 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 5132,1 | 6415185 | 98,64 | 98,60 | 41,95 | 0,9969 | 2,151 | 42,54 | 4,8 | — | 4,8 | 2 | — |
| karanlik | 5500 | negatif-handbrake-yarim-bit | — | 1920x818 | 2581,9 | 3227397 | 93,14 | 92,93 | 39,69 | 0,9922 | 2,726 | 41,06 | 5,3 | — | — | — | — |
| parlak | 2000 | urun-vt | hevc_videotoolbox | 1920x818 | 1900,0 | 2374979 | 85,75 | 83,75 | 36,80 | 0,9869 | 0,029 | 31,75 | 5,4 | — | 34,5 | — | — |
| parlak | 2000 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 1902,6 | 2378275 | 85,70 | 83,61 | 37,97 | 0,9879 | 0,003 | 32,17 | 5,3 | — | 5,3 | 2 | — |
| parlak | 2000 | negatif-handbrake-yarim-bit | — | 1920x818 | 992,4 | 1240476 | 75,77 | 68,26 | 35,39 | 0,9736 | 0,003 | 29,43 | 5,7 | — | — | — | — |
| parlak | 5500 | urun-vt | hevc_videotoolbox | 1920x818 | 5080,3 | 6350331 | 94,03 | 93,94 | 39,92 | 0,9965 | 0,024 | 37,71 | 10,9 | — | 39,8 | — | — |
| parlak | 5500 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 5098,9 | 6373629 | 94,43 | 94,40 | 41,05 | 0,9974 | 0,001 | 37,82 | 5,5 | — | 5,5 | 2 | — |
| parlak | 5500 | negatif-handbrake-yarim-bit | — | 1920x818 | 2630,9 | 3288571 | 89,55 | 88,99 | 39,16 | 0,9929 | 0,003 | 33,74 | 5,4 | — | — | — | — |
| hareketli | 2000 | urun-vt | hevc_videotoolbox | 1920x818 | 1948,9 | 2436071 | 94,23 | 93,86 | 38,05 | 0,9903 | 1,032 | 37,09 | 5,2 | — | 34,3 | — | — |
| hareketli | 2000 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 1955,1 | 2443932 | 91,62 | 91,10 | 38,59 | 0,9890 | 0,148 | 36,23 | 5,3 | — | 5,3 | 1 | — |
| hareketli | 2000 | negatif-handbrake-yarim-bit | — | 1920x818 | 999,4 | 1249257 | 76,78 | 74,95 | 35,09 | 0,9731 | 0,182 | 33,00 | 5,5 | — | — | — | — |
| hareketli | 5500 | urun-vt | hevc_videotoolbox | 1920x818 | 5360,1 | 6700076 | 99,10 | 99,06 | 41,43 | 0,9967 | 0,784 | 41,46 | 5,5 | — | 34,0 | — | — |
| hareketli | 5500 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x818 | 5300,1 | 6625076 | 99,16 | 99,12 | 42,40 | 0,9974 | 0,123 | 40,30 | 5,4 | — | 5,4 | 1 | — |
| hareketli | 5500 | negatif-handbrake-yarim-bit | — | 1920x818 | 2675,4 | 3344276 | 96,01 | 95,77 | 40,01 | 0,9930 | 0,150 | 37,69 | 5,0 | — | — | — | — |
| ekran | 2000 | urun-vt | hevc_videotoolbox | 1920x1080 | 1272,9 | 689490 | 92,95 | 92,92 | 42,67 | 0,9995 | 0,603 | 46,43 | 3,8 | — | 26,2 | — | — |
| ekran | 2000 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x1080 | 1294,2 | 700997 | 84,94 | 84,76 | 34,44 | 0,9954 | 1,399 | 37,71 | 2,5 | — | 2,5 | 2 | — |
| ekran | 2000 | negatif-handbrake-yarim-bit | — | 1920x1080 | 727,9 | 394226 | 73,02 | 72,56 | 31,24 | 0,9869 | 1,520 | 34,29 | 2,7 | — | — | — | — |
| ekran | 5500 | urun-vt | hevc_videotoolbox | 1920x1080 | 1345,0 | 728532 | 93,91 | 93,90 | 44,01 | 0,9997 | 0,457 | 47,94 | 3,9 | — | 26,0 | — | — |
| ekran | 5500 | handbrake-vt | HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p | 1920x1080 | 1351,4 | 731944 | 84,90 | 84,73 | 34,41 | 0,9955 | 1,200 | 37,75 | 2,5 | — | 2,5 | 2 | — |
| ekran | 5500 | negatif-handbrake-yarim-bit | — | 1920x1080 | 766,7 | 415256 | 75,17 | 74,77 | 31,70 | 0,9891 | 1,429 | 34,76 | 2,8 | — | — | — | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 2000 | 3,05 | 3,25 | -0,67 | 0,0009 | 6,219 | 1,43 | 1,00 | -0,81 | geride |
| karanlik | 5500 | 0,18 | 0,18 | -0,56 | -0,0004 | 6,023 | 2,53 | 1,08 | -1,70 | geride |
| parlak | 2000 | 0,06 | 0,13 | -1,17 | -0,0010 | 0,027 | -0,42 | 1,02 | 0,14 | geride |
| parlak | 5500 | -0,40 | -0,47 | -1,13 | -0,0009 | 0,022 | -0,11 | 1,98 | 0,37 | geride |
| hareketli | 2000 | 2,60 | 2,76 | -0,54 | 0,0013 | 0,883 | 0,86 | 0,98 | 0,32 | geride |
| hareketli | 5500 | -0,05 | -0,06 | -0,97 | -0,0007 | 0,661 | 1,16 | 1,02 | -1,12 | geride |
| ekran | 2000 | 8,01 | 8,17 | 8,22 | 0,0041 | -0,796 | 8,72 | 1,52 | 1,67 | önde |
| ekran | 5500 | 9,01 | 9,17 | 9,60 | 0,0042 | -0,743 | 10,18 | 1,56 | 0,48 | önde |
