kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4
kip         : parca
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-02 09:00:21 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-1.mkv | handbrake | 3.497 | 3531037 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 48.56 | 34.56 | 16.42 | 52.33 | 30.95 | 0.9209 |
| parca-1.mkv | vidshrink | 3.497 | 3531265 | +0.01 | evet | aynı renk uzayında doğrudan karşılaştırma | 45.93 | 37.26 | 12.49 | 48.47 | 30.15 | 0.9331 |
| parca-2-yalniz-video.mkv | handbrake | 3.497 | 3730691 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 93.71 | 93.07 | 89.89 | 93.72 | 43.61 | 0.9921 |
| parca-2-yalniz-video.mkv | vidshrink | 3.497 | 3712523 | -0.49 | evet | aynı renk uzayında doğrudan karşılaştırma | 82.23 | 80.91 | 79.95 | 82.25 | 38.51 | 0.9829 |
| parca-3-yalniz-video.mkv | handbrake | 3.498 | 3680998 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 17.88 | 13.88 | 9.35 | 18.86 | 28.36 | 0.8280 |
| parca-3-yalniz-video.mkv | vidshrink | 3.498 | 3703201 | +0.60 | evet | aynı renk uzayında doğrudan karşılaştırma | 22.29 | 16.18 | 7.52 | 24.15 | 28.91 | 0.8624 |
| parca-1.mkv | handbrake | 34.975 | 35288140 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 83.64 | 79.72 | 66.47 | 83.84 | 38.10 | 0.9752 |
| parca-1.mkv | vidshrink | 34.975 | 35759057 | +1.33 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 72.33 | 66.48 | 44.93 | 72.77 | 34.49 | 0.9575 |
| parca-2-yalniz-video.mkv | handbrake | 34.975 | 36766517 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 95.79 | 95.32 | 91.40 | 95.79 | 47.14 | 0.9952 |
| parca-2-yalniz-video.mkv | vidshrink | 34.975 | 36818689 | +0.14 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 95.80 | 95.33 | 93.76 | 95.80 | 48.77 | 0.9967 |
| parca-3-yalniz-video.mkv | handbrake | 34.984 | 36278925 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 74.96 | 69.90 | 60.02 | 75.24 | 37.26 | 0.9452 |
| parca-3-yalniz-video.mkv | vidshrink | 34.984 | 36280243 | 0.00 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 62.95 | 58.07 | 51.87 | 63.30 | 33.98 | 0.9181 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| handbrake | 60 | 10942726 | evet | aynı renk uzayında doğrudan karşılaştırma | 34.41 | 13.88 | 9.35 | 54.96 | 34.30 | 0.9136 |
| vidshrink | 60 | 10946989 | evet | aynı renk uzayında doğrudan karşılaştırma | 38.08 | 16.18 | 7.52 | 51.62 | 32.52 | 0.9261 |
| handbrake | 600 | 108333582 | evet | aynı renk uzayında doğrudan karşılaştırma | 83.95 | 69.90 | 60.02 | 84.96 | 40.83 | 0.9719 |
| vidshrink | 600 | 108857989 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 74.72 | 58.07 | 44.93 | 77.29 | 39.08 | 0.9574 |

Duyarlılık
- handbrake: AYRIŞIYOR — Hedef boyut 60 MB'den 600 MB'ye çıkarken ölçü 49,54 puan ayrıştı; eşik 1,00.
- vidshrink: AYRIŞIYOR — Hedef boyut 60 MB'den 600 MB'ye çıkarken ölçü 36,65 puan ayrıştı; eşik 1,00.

Komut satırları
- handbrake (H.265 MKV 1080p30 + x265 slow, 483 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-1_handbrake_3.497mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 483 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (882x496@60, libsvtav1/2pass, 464k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -vf scale=882:496:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 464k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-1_vidshrink_3.497mb.mp4
- vidshrink (1650x928@60, libsvtav1/2pass, 539k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-2-yalniz-video.mkv -vf scale=1650:928:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 489k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-2-yalniz-video_vidshrink_3.686mb.mp4
- vidshrink (882x496@60, libsvtav1/2pass, 508k, pix=p010le, hdr=Preserve, deneme=3)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-3-yalniz-video.mkv -vf scale=882:496:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 505k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-3-yalniz-video_vidshrink_3.809mb.mp4
- handbrake (H.265 MKV 1080p30 + x265 slow, 4833 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-1_handbrake_34.975mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 4833 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (1420x798@60, libx264/2pass, 4869k, pix=yuv420p, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -vf scale=1420:798:flags=lanczos,zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p -c:v libx264 -preset slow -b:v 4712k -maxrate 7068k -bufsize 9424k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -keyint_min 60 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-1_vidshrink_34.975mb.mp4
- vidshrink (1920x1080@60, libx264/2pass, 4842k, pix=yuv420p, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-2-yalniz-video.mkv -vf zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p -c:v libx264 -preset slow -b:v 4842k -maxrate 7263k -bufsize 9684k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -keyint_min 60 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-2-yalniz-video_vidshrink_35.934mb.mp4
- vidshrink (1190x670@60, libx264/2pass, 4817k, pix=yuv420p, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-3-yalniz-video.mkv -vf scale=1190:670:flags=lanczos,zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p -c:v libx264 -preset slow -b:v 4817k -maxrate 7225k -bufsize 9634k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\pass -g 600 -keyint_min 60 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\taban\cikti\parca-3-yalniz-video_vidshrink_35.757mb.mp4
