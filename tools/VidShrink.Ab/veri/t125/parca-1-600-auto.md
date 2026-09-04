kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:08:04 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-1.mkv | handbrake | 34.975 | 35288140 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 83.64 | 79.72 | 66.47 | 83.84 | 38.10 | 0.9752 |
| parca-1.mkv | vidshrink | 34.975 | 35752868 | +1.32 | evet | aynı renk uzayında doğrudan karşılaştırma | 72.80 | 68.44 | 48.57 | 73.10 | 36.14 | 0.9667 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| handbrake | 34.975 | 35288140 | evet | aynı renk uzayında doğrudan karşılaştırma | 83.64 | 79.72 | 66.47 | 83.84 | 38.10 | 0.9752 |
| vidshrink | 34.975 | 35752868 | evet | aynı renk uzayında doğrudan karşılaştırma | 72.80 | 68.44 | 48.57 | 73.10 | 36.14 | 0.9667 |

Komut satırları
- handbrake (H.265 MKV 1080p30 + x265 slow, 4833 kbit/s ABR, çoklu geçiş, turbo ilk geçiş, ses yok, 60 fps CFR)
  HandBrakeCLI -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -o C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-1_handbrake_34.975mb.mkv -Z "H.265 MKV 1080p30" -e x265 -b 4833 --multi-pass --turbo --encoder-preset slow -a none -r 60 --cfr --crop 0:0:0:0 --non-anamorphic
- vidshrink (1382x778@60, libx264/2pass, 4866k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -vf scale=1382:778:flags=lanczos -c:v libx264 -preset slow -b:v 4712k -maxrate 7068k -bufsize 9424k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\auto-cikti\parca-1_vidshrink_34.975mb.mp4
