#!/usr/bin/env bash
# Hizalama kapisi: her kaynak kayipsiz kodlanir, PSNR sonsuz ve VMAF kare
# sayisi kaynakla ayni olmali. Bu kapi acilmadan izgara kosulmaz.
set -u
. "$(dirname "$0")/ortak.sh"
for s in s1-kesikli s2-durgun s3-hareketli s4-yuksek; do
  src="$KAY/$s.mkv"
  n=$(ffprobe -v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets -of csv=p=0 "$src")
  out="$ISI/kayipsiz-$s.mp4"
  [ -f "$out" ] || ffmpeg -hide_banner -v error -nostdin -y -i "$src" -an -c:v libx264 -qp 0 -preset ultrafast "$out"
  p=$(ffmpeg -hide_banner -nostdin -i "$out" -i "$src" -lavfi "[0:v]settb=AVTB,setpts=N[d];[1:v]settb=AVTB,setpts=N[r];[d][r]psnr" -fps_mode passthrough -f null - 2>&1 | sed -n 's/.*average:\([a-z0-9.]*\).*/\1/p' | tail -1)
  rm -f "$VMD/kayipsiz-$s.json"
  olc "kayipsiz-$s" "$src" >/dev/null
  v=$(python "$(dirname "$0")/oz.py" "$VMD/kayipsiz-$s.json")
  echo "$s kaynak_kare=$n psnr=$p vmaf(kare mean p10 worst)=$v"
done
