#!/bin/sh
# T103 korpusu: 17 dk HDR kaynaktan tonemap edilmis 960x540@30 SDR klipler.
# Bolgeler kaynagin saniye basina bit profilinden secildi (bkz. docs/olcumler/ornekleme.md).
set -e
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
SRC=${SRC:-"$ROOT/../../../.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"}
OUT=${OUT:-"$ROOT/.calisma/t103/korpus"}
TM="zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p"
mkdir -p "$OUT"

kes() {
  ad=$1; bas=$2; sure=$3
  [ -f "$OUT/$ad.mkv" ] && { echo "atlandi $ad"; return; }
  ffmpeg -hide_banner -v error -y -ss "$bas" -t "$sure" -i "$SRC" \
    -an -sn -dn -vf "$TM,scale=960:540,fps=30" \
    -c:v libx264 -crf 14 -preset veryfast -threads 4 \
    -color_primaries bt709 -color_trc bt709 -colorspace bt709 \
    "$OUT/$ad.mkv"
  echo "bitti $ad"
}

sentetik() {
  ad=$1; kaynak=$2
  [ -f "$OUT/$ad.mkv" ] && { echo "atlandi $ad"; return; }
  ffmpeg -hide_banner -v error -y -f lavfi -i "$kaynak" \
    -vf "scale=960:540,fps=30,format=yuv420p" \
    -c:v libx264 -crf 14 -preset veryfast -threads 4 \
    -color_primaries bt709 -color_trc bt709 -colorspace bt709 \
    "$OUT/$ad.mkv"
  echo "bitti $ad"
}

kes k1-inisli 300 240 &
kes k2-duz 600 240 &
kes k3-tepeli 420 240 &
wait
kes k4-bas 0 240 &
kes k5-kisa 330 60 &
kes k6-uzun 180 480 &
wait
sentetik s1-mandelbrot "mandelbrot=size=960x540:rate=30:end_pts=240" &
sentetik s2-durgun-hareketli "testsrc2=size=960x540:rate=30:duration=240" &
wait
ls -la "$OUT"
