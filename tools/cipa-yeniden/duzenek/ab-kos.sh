#!/usr/bin/env bash
set -u
cd "$(dirname "$0")/../.." || exit 1
W=".calisma/T116"
K="/c/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak"

declare -A SRC=( [sdr8]="$W/sdr-1.mkv" [sdr20]="$W/sdr-1.mkv" [hdr1]="$K/parca-1.mkv" [hdr2]="$K/parca-2.mkv" )
declare -A MB=( [sdr8]=8 [sdr20]=20 [hdr1]=40 [hdr2]=40 )

for meter in kilitli kilitsiz; do
  BIN="$W/bench-$meter/VidShrink.Bench.exe"
  for cfg in sdr8 sdr20 hdr1 hdr2; do
    for arm in eski yeni; do
      tag="$cfg-$arm-$meter"
      out="$W/cikti/$tag"
      log="$W/gunluk/$tag.log"
      if [ -s "$W/gunluk/$tag.json" ]; then echo "atlandi $tag"; continue; fi
      rm -rf "$out"; mkdir -p "$out"
      extra=""
      [ "$arm" = "yeni" ] && extra="--measured-quality"
      echo "=== $tag $(date +%H:%M:%S)"
      "$BIN" shrink "${SRC[$cfg]}" "${MB[$cfg]}" --out "$out" --results "$W/gunluk/$tag.json" $extra >"$log" 2>&1
      echo "   rc=$? $(date +%H:%M:%S)"
      rm -f "$out"/*.mp4 "$out"/pass* 2>/dev/null
    done
  done
done
echo "BITTI $(date +%H:%M:%S)"
