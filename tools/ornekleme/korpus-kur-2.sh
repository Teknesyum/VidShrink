#!/bin/sh
# Kurgulanmis heterojenlik: gercek goruntuden kesilmis kolay/zor bloklar.
# s1: 180 sn kolay + 60 sn zor (75/25). s2: 20 sn'lik kolay/zor donusumlu 6 blok.
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
SRC=${SRC:-"$ROOT/../../../.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"}
OUT=${OUT:-"$ROOT/.calisma/t103/korpus"}
TM="zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p"
TAIL="$TM,scale=960:540,fps=30,setsar=1"

birlestir() {
  ad=$1; shift
  [ -f "$OUT/$ad.mkv" ] && { echo "atlandi $ad"; return; }
  girdiler=""; zincir=""; n=0
  for parca in "$@"; do
    bas=${parca%:*}; sure=${parca#*:}
    girdiler="$girdiler -ss $bas -t $sure -i $SRC"
    zincir="$zincir[$n:v]$TAIL[v$n];"
    n=$((n+1))
  done
  etiketler=""; i=0
  while [ $i -lt $n ]; do etiketler="$etiketler[v$i]"; i=$((i+1)); done
  ffmpeg -hide_banner -v error -y $girdiler \
    -filter_complex "$zincir$etiketler concat=n=$n:v=1:a=0[out]" -map "[out]" \
    -an -sn -dn -c:v libx264 -crf 14 -preset veryfast -threads 4 \
    -color_primaries bt709 -color_trc bt709 -colorspace bt709 "$OUT/$ad.mkv"
  echo "bitti $ad"
}

birlestir s1-cuval 620:180 520:60
birlestir s2-donusumlu 620:20 520:20 650:20 530:20 680:20 540:20 700:20 550:20 720:20 560:20 740:20 570:20
ls -la "$OUT"
