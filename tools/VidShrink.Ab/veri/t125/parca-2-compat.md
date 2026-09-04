kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:30:14 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-2-yalniz-video.mkv | vidshrink | 3.5 | 3542454 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 71.01 | 68.45 | 54.62 | 71.13 | 34.14 | 0.9624 |
| parca-2-yalniz-video.mkv | vidshrink | 34.999 | 35862641 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 96.10 | 95.74 | 94.25 | 96.10 | 52.43 | 0.9987 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 3.5 | 3542454 | evet | aynı renk uzayında doğrudan karşılaştırma | 71.01 | 68.45 | 54.62 | 71.13 | 34.14 | 0.9624 |
| vidshrink | 34.999 | 35862641 | evet | aynı renk uzayında doğrudan karşılaştırma | 96.10 | 95.74 | 94.25 | 96.10 | 52.43 | 0.9987 |

Duyarlılık
- vidshrink: AYRIŞIYOR — Hedef boyut 3,5 MB'den 34,999 MB'ye çıkarken ölçü 25,09 puan ayrıştı; eşik 1,00.

Komut satırları
- vidshrink (1152x648@60, libx264/2pass, 464k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -vf scale=1152:648:flags=lanczos -c:v libx264 -preset slow -b:v 464k -maxrate 696k -bufsize 928k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\parca-2-yalniz-video_vidshrink_3.5mb.mp4
- vidshrink (1920x1080@60, libx264/2pass, 4716k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -c:v libx264 -preset slow -b:v 4716k -maxrate 7074k -bufsize 9432k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\parca-2-yalniz-video_vidshrink_34.999mb.mp4
