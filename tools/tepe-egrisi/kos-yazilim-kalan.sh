#!/usr/bin/env bash
set -u
kok="$(cd "$(dirname "$0")/../.." && pwd)"
T="$kok/tools/tepe-egrisi/bin/Release/net8.0/tepe-egrisi.exe"
K="$kok/.calisma/t108/kaynak"
is="$kok/.calisma/t108/yazilim"
"$T" izgara --kaynak "$K/durgun-20sn.mkv" --ad durgun --kodlayici libx265 --onayar medium \
  --taban 795 --oranlar 4.636 --tepeler 1.02,1.10,1.50 --is "$is" --threads 4
echo "durgun 4.636 bitti"
for k in hareketli durgun; do
  "$T" izgara --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici libx265 --onayar medium \
    --taban 795 --oranlar 10.236 --tepeler 1.02,1.10,1.50 --is "$is" --threads 4
  echo "$k 10.236 bitti"
done
echo "yazilim kalan bitti"
