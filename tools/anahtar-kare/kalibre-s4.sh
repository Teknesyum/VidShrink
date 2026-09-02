#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
OZ="$(dirname "$0")/oz.py"
s=s4-yuksek; src="$KAY/$s.mkv"; fps=$(fps_of "$src")
g=$(python -c "print(round($fps*10))"); kmin=$(python -c "print(round($fps*1))")
echo "kaynak bitrate_k kare mean p10 worst boyut_bayt"
for br in 2400 4800 8390 12000 19200; do
  ad="kal-$s-$br"
  kodla "$ad" "$src" "$g" "$kmin" "$br" >/dev/null || { echo "$s $br KODLAMA-HATASI"; continue; }
  olc "$ad" "$src" >/dev/null || { echo "$s $br VMAF-HATASI"; continue; }
  echo "$s $br $(python "$OZ" "$VMD/$ad.json") $(stat -c %s "$ISI/$ad.mp4")"
done
echo KALIBRE-S4-BITTI
