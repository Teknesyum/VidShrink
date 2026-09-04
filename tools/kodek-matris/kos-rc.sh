#!/bin/bash
O=.calisma/kodek-matris
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"
[ -f "$K3" ] || K3=$(grep -oE '"[^"]*parca-3[^"]*\.mkv"' $O/kos-matris.sh | head -1 | tr -d '"')
echo "KAYNAK=$K3"
HDR="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"

run() {
  local tag=$1 preset=$2 extra=$3 out=$4
  local log="$O/log_rc_${tag}.txt"
  echo "### $tag preset=$preset params=$extra ###" | tee -a "$O/rc-log.txt"
  S=$(date +%s)
  ffmpeg -hide_banner -y -i "$K3" -c:v libsvtav1 -preset $preset -b:v 483k -pass 1 -passlogfile "$O/rc_${tag}" -g 600 -svtav1-params "$extra" $HDR -f null /dev/null >"$log" 2>&1
  ffmpeg -hide_banner -y -i "$K3" -c:v libsvtav1 -preset $preset -b:v 483k -pass 2 -passlogfile "$O/rc_${tag}" -g 600 -svtav1-params "$extra" $HDR -movflags +faststart "$out" >>"$log" 2>&1
  E=$(date +%s)
  BAYT=$(stat -c%s "$out")
  BR=$(ffprobe -v error -show_entries format=bit_rate -of csv=p=0 "$out")
  echo "$tag  saniye=$((E-S))  bayt=$BAYT  bitrate=$BR" | tee -a "$O/rc-log.txt"
}

run p6_vbsiz  6 "keyint=600:scd=1:tune=0"                                          "$O/rc_p6_vbsiz.mp4"
run p4_vb     4 "keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2" "$O/rc_p4_vb.mp4"
run p4_vbsiz  4 "keyint=600:scd=1:tune=0"                                          "$O/rc_p4_vbsiz.mp4"
echo "RC TAMAM" | tee -a "$O/rc-log.txt"
