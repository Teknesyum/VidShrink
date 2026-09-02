#!/usr/bin/env bash
# T116 K2 ikame kaynagi: sdr-1.mkv
#
# T89'un uc kaynagi (klip / oyun / hdr) uretildikleri turda silindi
# (docs/olcumler/olculen-kaliteyle-plan.md:22). Ayni kaynaklar yeniden
# olculemez. Elde yalniz .calisma/kaynak/parca-{1,2,3}.mkv var; ucu de
# 1080p60 HDR hevc, yani T89'un "hdr" satirinin ikamesi dogrudan var,
# "klip" (1080p60 SDR) satirinin ikamesi uretilmek zorunda.
#
# "oyun" (1080p48 av1 oyun kaydi) icin ikame URETILMEDI ve o satir OLCULMEDI:
# elde 48 fps av1 oyun kaydi yok, sentetik taklit oyun kaydinin hareket
# istatistigini vermez.
#
# Kodlama parametreleri x264'un iki gecis gunlugunden (.calisma/T116/
# x264pass-0.log ilk satiri) geri okundu ve dogrulandi:
#   1920x1080 fps=60/1 bitdepth=8 rc=abr bitrate=3400 rc_lookahead=30
#   b_adapt=1 bframes=3 threads=4
# rc_lookahead=30 + b_adapt=1 x264'te tam olarak "preset fast"tir.
#
# TONEMAP OPERATORU OLCULDU. Filtre zinciri kapta saklanmiyor, o yuzden geri
# okunamadi; onun yerine ilk kare dort operatorle yeniden uretilip elde duran
# sdr-1.mkv'nin ilk karesiyle PSNR'landi (duzenek: tonemap-dogrulama/):
#   hable 39,03 dB · reinhard 21,28 · mobius 20,08 · clip 19,33
# Yani operator hable. Kalan 39 dB'lik fark x264'un kendi kaybi (a.png
# sikistirilmis akistan cikiyor). desat degeri ve zscale parametreleri
# ayrica OLCULMEDI; dosyanin bayt bayt yeniden uretildigi de OLCULMEDI.
set -eu
cd "$(dirname "$0")/../../.." || exit 1
K=".calisma/kaynak/parca-1.mkv"
W=".calisma/T116"
OUT="${1:-$W/sdr-1.mkv}"
VF="zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p"

ffmpeg -hide_banner -y -i "$K" -vf "$VF" \
  -c:v libx264 -preset fast -b:v 3400k -pass 1 -passlogfile "$W/x264pass" \
  -threads 4 -an -f null /dev/null

ffmpeg -hide_banner -y -i "$K" -vf "$VF" \
  -c:v libx264 -preset fast -b:v 3400k -pass 2 -passlogfile "$W/x264pass" \
  -threads 4 -an "$OUT"

# Olculen sonuc (elde duran sdr-1.mkv): 1920x1080@60, h264, yuv420p, bt709,
# 60,40 s, 24 211 675 bayt = 23,1 MiB. T89'un "klip"i 24,8 MB idi.
