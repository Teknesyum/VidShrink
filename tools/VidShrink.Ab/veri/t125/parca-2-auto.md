kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:20:24 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-2-yalniz-video.mkv | handbrake | 3.5 | 3735428 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 93.73 | 93.09 | 89.90 | 93.73 | 43.61 | 0.9921 |
| parca-2-yalniz-video.mkv | vidshrink | 3.5 | 3806095 | +1.89 | evet | aynı renk uzayında doğrudan karşılaştırma | 82.25 | 80.91 | 79.92 | 82.27 | 38.53 | 0.9830 |
| parca-2-yalniz-video.mkv | handbrake | 34.999 | 36799163 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 95.79 | 95.32 | 91.40 | 95.79 | 47.14 | 0.9952 |
| parca-2-yalniz-video.mkv | vidshrink | 34.999 | 36814928 | +0.04 | evet | aynı renk uzayında doğrudan karşılaştırma | 96.12 | 95.75 | 94.37 | 96.12 | 52.51 | 0.9987 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| handbrake | 3.5 | 3735428 | evet | aynı renk uzayında doğrudan karşılaştırma | 93.73 | 93.09 | 89.90 | 93.73 | 43.61 | 0.9921 |
| vidshrink | 3.5 | 3806095 | evet | aynı renk uzayında doğrudan karşılaştırma | 82.25 | 80.91 | 79.92 | 82.27 | 38.53 | 0.9830 |
| handbrake | 34.999 | 36799163 | evet | aynı renk uzayında doğrudan karşılaştırma | 95.79 | 95.32 | 91.40 | 95.79 | 47.14 | 0.9952 |
| vidshrink | 34.999 | 36814928 | evet | aynı renk uzayında doğrudan karşılaştırma | 96.12 | 95.75 | 94.37 | 96.12 | 52.51 | 0.9987 |

Duyarlılık
- handbrake: AYRIŞIYOR — Hedef boyut 3,5 MB'den 34,999 MB'ye çıkarken ölçü 2,06 puan ayrıştı; eşik 1,00.
- vidshrink: AYRIŞIYOR — Hedef boyut 3,5 MB'den 34,999 MB'ye çıkarken ölçü 13,87 puan ayrıştı; eşik 1,00.

Komut satırları
- handbrake (H.265 MKV 1080p30 + x265 slow, 484 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-2-yalniz-video_handbrake_3.5mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 484 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (1650x928@60, libsvtav1/2pass, 546k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -vf scale=1650:928:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 501k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-2-yalniz-video_vidshrink_3.774mb.mp4
- handbrake (H.265 MKV 1080p30 + x265 slow, 4837 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-2-yalniz-video_handbrake_34.999mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 4837 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (1920x1080@60, libx264/2pass, 4837k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -c:v libx264 -preset slow -b:v 4837k -maxrate 7255k -bufsize 9674k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-2-yalniz-video_vidshrink_35.903mb.mp4
