#!/usr/bin/env bash
# T108 tepe egrisi izgarasi. Iki akis: donanim (GPU) ve yazilim (CPU).
# Kullanim: kos.sh donanim|yazilim <is-dizini>
set -u
kok="$(cd "$(dirname "$0")/../.." && pwd)"
T="$kok/tools/tepe-egrisi/bin/Release/net8.0/tepe-egrisi.exe"
K="$kok/.calisma/t108/kaynak"
akis="${1:?donanim|yazilim}"
is="${2:?is dizini}"

ORANLAR=2.6,4.636,7.5,10.236,16.0
TEPELER=1.02,1.10,1.50

if [ "$akis" = "donanim" ]; then
  for c in av1_nvenc hevc_nvenc; do
    for k in hareketli durgun; do
      "$T" izgara --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici "$c" \
        --oranlar "$ORANLAR" --tepeler "$TEPELER" --is "$is" --threads 4 || exit 1
    done
  done
else
  for k in hareketli durgun; do
    "$T" izgara --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici libx265 --onayar medium \
      --oranlar 4.636,10.236 --tepeler "$TEPELER" --is "$is" --threads 4 --taban 795 || exit 1
  done
  for k in hareketli durgun; do
    "$T" vbv --kaynak "$K/$k-20sn.mkv" --ad "$k" --kodlayici libx265 --onayar medium \
      --crf 23 --bitrate 8138 --tepeler yok,1.10,1.25,1.50,2.00 --is "$is" --threads 4 || exit 1
  done
fi
echo "$akis bitti"
