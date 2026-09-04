#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
OZ="$(dirname "$0")/oz.py"
echo "kaynak bitrate_k kare mean p10 worst boyut_bayt"
for s in s1-kesikli s2-durgun s3-hareketli; do
  src="$KAY/$s.mkv"; fps=$(fps_of "$src")
  g=$(python -c "print(round($fps*10))"); kmin=$(python -c "print(round($fps*1))")
  for br in 150 300 600 1200 2400 4800 9600 19200; do
    ad="kal-$s-$br"
    kodla "$ad" "$src" "$g" "$kmin" "$br" >/dev/null || { echo "$s $br KODLAMA-HATASI"; continue; }
    olc "$ad" "$src" >/dev/null || { echo "$s $br VMAF-HATASI"; continue; }
    echo "$s $br $(python "$OZ" "$VMD/$ad.json") $(stat -c %s "$ISI/$ad.mp4")"
  done
done
echo KALIBRE-BITTI
