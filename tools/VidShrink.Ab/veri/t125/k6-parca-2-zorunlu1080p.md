kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:26:52 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-2-yalniz-video.mkv | vidshrink | 3.5 | 3488830 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 89.67 | 84.80 | 47.68 | 89.89 | 41.10 | 0.9867 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 3.5 | 3488830 | evet | aynı renk uzayında doğrudan karşılaştırma | 89.67 | 84.80 | 47.68 | 89.89 | 41.10 | 0.9867 |

Komut satırları
- vidshrink (1920x1080@60, libx264/2pass, 464k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-2-yalniz-video.mkv -c:v libx264 -preset slow -b:v 464k -maxrate 696k -bufsize 928k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\k6-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\k6-cikti\parca-2-yalniz-video_vidshrink_3.5mb.mp4
