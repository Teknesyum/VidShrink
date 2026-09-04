kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\uc-sn.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-02 07:24:34 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| uc-sn.mkv | vidshrink | 0.35 | 355516 | 0.00 | evet | aynı renk uzayında doğrudan karşılaştırma | 27.57 | 17.76 | 9.28 | 30.76 | 30.68 | 0.8373 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 0.35 | 355516 | evet | aynı renk uzayında doğrudan karşılaştırma | 27.57 | 17.76 | 9.28 | 30.76 | 30.68 | 0.8373 |

Komut satırları
- vidshrink (882x496@60, libsvtav1/2pass, 1002k, pix=p010le, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\uc-sn.mkv -vf scale=882:496:flags=lanczos -c:v libsvtav1 -preset 6 -b:v 935k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\serbest2-cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\serbest2-cikti\uc-sn_vidshrink_0.35mb.mp4
