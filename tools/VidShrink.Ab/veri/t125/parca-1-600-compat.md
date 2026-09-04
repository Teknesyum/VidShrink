kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-03 20:27:40 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| parca-1.mkv | vidshrink | 34.975 | 35763101 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 72.82 | 68.47 | 49.10 | 73.12 | 36.14 | 0.9667 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 34.975 | 35763101 | evet | aynı renk uzayında doğrudan karşılaştırma | 72.82 | 68.47 | 49.10 | 73.12 | 36.14 | 0.9667 |

Komut satırları
- vidshrink (1382x778@60, libx264/2pass, 4865k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\parca-1.mkv -vf scale=1382:778:flags=lanczos -c:v libx264 -preset slow -b:v 4712k -maxrate 7068k -bufsize 9424k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\pass -g 600 -keyint_min 60 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\tur2\compat-cikti\parca-1_vidshrink_34.975mb.mp4
