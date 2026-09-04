kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:21:09 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-3-yalniz-video.mkv | handbrake | 3.499 | 3680998 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 17.88 | 13.88 | 9.35 | 18.86 | 28.36 | 0.8280 |
| parca-3-yalniz-video.mkv | vidshrink | 3.499 | 3715009 | +0.92 | evet | aynı renk uzayında doğrudan karşılaştırma | 22.29 | 16.09 | 7.51 | 24.14 | 28.92 | 0.8625 |
| parca-3-yalniz-video.mkv | handbrake | 34.994 | 36282675 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 74.96 | 69.89 | 60.81 | 75.24 | 37.26 | 0.9452 |
| parca-3-yalniz-video.mkv | vidshrink | 34.994 | 36276324 | -0.02 | evet | aynı renk uzayında doğrudan karşılaştırma | 66.11 | 61.80 | 55.54 | 66.38 | 35.88 | 0.9368 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| handbrake | 3.499 | 3680998 | evet | aynı renk uzayında doğrudan karşılaştırma | 17.88 | 13.88 | 9.35 | 18.86 | 28.36 | 0.8280 |
| vidshrink | 3.499 | 3715009 | evet | aynı renk uzayında doğrudan karşılaştırma | 22.29 | 16.09 | 7.51 | 24.14 | 28.92 | 0.8625 |
| handbrake | 34.994 | 36282675 | evet | aynı renk uzayında doğrudan karşılaştırma | 74.96 | 69.89 | 60.81 | 75.24 | 37.26 | 0.9452 |
| vidshrink | 34.994 | 36276324 | evet | aynı renk uzayında doğrudan karşılaştırma | 66.11 | 61.80 | 55.54 | 66.38 | 35.88 | 0.9368 |

Duyarlılık
- handbrake: AYRIŞIYOR — Hedef boyut 3,499 MB'den 34,994 MB'ye çıkarken ölçü 57,08 puan ayrıştı; eşik 1,00.
- vidshrink: AYRIŞIYOR — Hedef boyut 3,499 MB'den 34,994 MB'ye çıkarken ölçü 43,82 puan ayrıştı; eşik 1,00.

Komut satırları
- handbrake (H.265 MKV 1080p30 + x265 slow, 483 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-3-yalniz-video_handbrake_3.499mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 483 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (882x496@60, libsvtav1/2pass, 515k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -vf scale=882:496:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 489k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-3-yalniz-video_vidshrink_3.687mb.mp4
- handbrake (H.265 MKV 1080p30 + x265 slow, 4835 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-3-yalniz-video_handbrake_34.994mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 4835 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (1190x670@60, libx264/2pass, 4811k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-3-yalniz-video.mkv -vf scale=1190:670:flags=lanczos -c:v libx264 -preset slow -b:v 4811k -maxrate 7216k -bufsize 9622k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-3-yalniz-video_vidshrink_35.717mb.mp4
