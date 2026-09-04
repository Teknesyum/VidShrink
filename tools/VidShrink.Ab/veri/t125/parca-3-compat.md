kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:30:11 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-3-yalniz-video.mkv | vidshrink | 3.499 | 3563483 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 13.75 | 10.59 | 4.25 | 14.59 | 27.12 | 0.8316 |
| parca-3-yalniz-video.mkv | vidshrink | 34.994 | 35546007 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 65.75 | 61.45 | 55.40 | 66.02 | 35.82 | 0.9361 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 3.499 | 3563483 | evet | aynı renk uzayında doğrudan karşılaştırma | 13.75 | 10.59 | 4.25 | 14.59 | 27.12 | 0.8316 |
| vidshrink | 34.994 | 35546007 | evet | aynı renk uzayında doğrudan karşılaştırma | 65.75 | 61.45 | 55.40 | 66.02 | 35.82 | 0.9361 |

Duyarlılık
- vidshrink: AYRIŞIYOR — Hedef boyut 3,499 MB'den 34,994 MB'ye çıkarken ölçü 52,01 puan ayrıştı; eşik 1,00.

Komut satırları
- vidshrink (652x366@60, libx264/2pass, 464k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -vf scale=652:366:flags=lanczos -c:v libx264 -preset slow -b:v 464k -maxrate 696k -bufsize 928k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\parca-3-yalniz-video_vidshrink_3.499mb.mp4
- vidshrink (1190x670@60, libx264/2pass, 4714k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -vf scale=1190:670:flags=lanczos -c:v libx264 -preset slow -b:v 4714k -maxrate 7071k -bufsize 9428k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\parca-3-yalniz-video_vidshrink_34.994mb.mp4
