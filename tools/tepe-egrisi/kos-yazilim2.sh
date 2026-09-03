#!/usr/bin/env bash
set -u
kok="$(cd "$(dirname "$0")/../.." && pwd)"
T="$kok/tools/tepe-egrisi/bin/Release/net8.0/tepe-egrisi.exe"
K="$kok/.calisma/t108/kaynak"
is="$kok/.calisma/t108/yazilim"
for k in hareketli durgun; do
  "$T" vbv --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici libx265 --onayar medium \
    --crf 23 --bitrate 8138 --tepeler yok,1.10,1.25,1.50,2.00 --is "$is" --threads 4 || exit 1
done
echo "vbv bitti"
"$T" izgara --kaynak "$K/durgun-20sn.mkv" --ad durgun --kodlayici libx265 --onayar medium \
  --oranlar 4.636 --tepeler 1.02,1.10,1.50 --is "$is" --threads 4 || exit 1
for k in hareketli durgun; do
  "$T" izgara --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici libx265 --onayar medium \
    --oranlar 10.236 --tepeler 1.02,1.10,1.50 --is "$is" --threads 4 || exit 1
done
echo "yazilim bitti"
