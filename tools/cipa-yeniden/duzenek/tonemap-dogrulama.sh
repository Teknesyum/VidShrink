#!/usr/bin/env bash
# sdr-1.mkv'nin hangi tonemap operatoruyle uretildigini kaptan degil
# GORUNTUDEN okur: ilk kare dort operatorle yeniden uretilir ve elde duran
# dosyanin ilk karesiyle PSNR'lanir. En yuksek PSNR kazanir.
set -eu
cd "$(dirname "$0")/../../.." || exit 1
K=".calisma/kaynak/parca-1.mkv"
D=".calisma/T116/tonemap-dogrulama"
mkdir -p "$D"
ffmpeg -v error -y -i .calisma/T116/sdr-1.mkv -frames:v 1 -pix_fmt yuv420p "$D/a.png"
for op in hable mobius reinhard clip; do
  ffmpeg -v error -y -i "$K" -vf "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=$op:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p" -frames:v 1 "$D/b-$op.png"
  v=$(ffmpeg -hide_banner -i "$D/a.png" -i "$D/b-$op.png" -lavfi "[0:v]format=yuv420p[a];[1:v]format=yuv420p[b];[a][b]psnr" -f null - 2>&1 | grep -o "average:[0-9.]*")
  echo "$op $v"
done
# Olculen: hable average:39.025317 · reinhard 21.278990 · mobius 20.075628 · clip 19.330704
