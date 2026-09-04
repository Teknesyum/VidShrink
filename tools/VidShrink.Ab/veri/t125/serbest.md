kaynak      : C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\uc-sn.mkv
kip         : tam
tolerans    : ±%2
ffmpeg      : 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
başlangıç   : 2026-09-02 07:19:06 UTC

Ölçüm satırları
| girdi | yarışmacı | hedef MB | bayt | fark % | eş boyut | renk kapısı | harm | p10 | min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| uc-sn.mkv | vidshrink | 0.35 | 362102 | 0.00 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 25.85 | 16.49 | 10.42 | 28.48 | 28.86 | 0.7954 |

Özet
| yarışmacı | hedef MB | toplam bayt | eş boyut | renk kapısı | harm | en kötü p10 | kare min | ort | XPSNR | SSIM |
|---|---|---|---|---|---|---|---|---|---|---|
| vidshrink | 0.35 | 362102 | evet | SDR uzayında karşılaştırma — HDR kaybı hariç | 25.85 | 16.49 | 10.42 | 28.48 | 28.86 | 0.7954 |

Komut satırları
- vidshrink (882x496@60, libsvtav1/2pass, 1063k, pix=yuv420p, hdr=Preserve, deneme=2)
  ffmpeg -hide_banner -y -hwaccel auto -i C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\uc-sn.mkv -vf scale=882:496:flags=lanczos,zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p -c:v libsvtav1 -preset 6 -b:v 935k -pass 2 -passlogfile C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\serbest-cikti\pass -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -an -movflags +faststart C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\ab\t125\k6-deneme\serbest-cikti\uc-sn_vidshrink_0.35mb.mp4
