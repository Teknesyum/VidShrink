kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-02 06:47:22 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-1.mkv | handbrake | 3.497 | 3531037 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 48.56 | 34.56 | 16.42 | 52.33 | 30.95 | 0.9209 |
| parca-1.mkv | vidshrink | 3.497 | 3525089 | -0.17 | evet | aynı renk uzayında doğrudan karşılaştırma | 33.44 | 18.57 | 5.53 | 40.35 | 29.06 | 0.9159 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| handbrake | 3.497 | 3531037 | evet | aynı renk uzayında doğrudan karşılaştırma | 48.56 | 34.56 | 16.42 | 52.33 | 30.95 | 0.9209 |
| vidshrink | 3.497 | 3525089 | evet | aynı renk uzayında doğrudan karşılaştırma | 33.44 | 18.57 | 5.53 | 40.35 | 29.06 | 0.9159 |

Komut satırları
- handbrake (H.265 MKV 1080p30 + x265 slow, 483 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\cikti\parca-1_handbrake_3.497mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 483 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (768x432@60, libx264/2pass, 481k, pix=p010le, hdr=Preserve, deneme=1)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -vf scale=768:432:flags=lanczos -c:v libx264 -preset slow -b:v 481k -maxrate 721k -bufsize 962k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\cikti\parca-1_vidshrink_3.628mb.mp4
