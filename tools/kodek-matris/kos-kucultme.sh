set -e
O=".calisma/kodek-matris"
K2=".calisma/kaynak/parca-2-yalniz-video.mkv"
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"
HDR="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
P="keyint=600:scd=1:tune=0"
L="$O/kucultme-log.txt"

kodla() {
  local tag=$1 src=$2 br=$3 vf=$4 out=$5
  echo "### $tag  bitrate=$br  vf=${vf:-yok} ###" | tee -a "$L"
  local SC=""; [ -n "$vf" ] && SC="-vf scale=$vf:flags=lanczos"
  S=$(date +%s)
  ffmpeg -hide_banner -y -i "$src" -c:v libsvtav1 -preset 4 -b:v ${br}k -pass 1 \
    -passlogfile "$O/kc_$tag" -g 600 -svtav1-params "$P" $SC $HDR -f null /dev/null >"$O/log_kc_$tag.txt" 2>&1
  ffmpeg -hide_banner -y -i "$src" -c:v libsvtav1 -preset 4 -b:v ${br}k -pass 2 \
    -passlogfile "$O/kc_$tag" -g 600 -svtav1-params "$P" $SC $HDR -movflags +faststart "$out" >>"$O/log_kc_$tag.txt" 2>&1
  E=$(date +%s)
  echo "$tag saniye=$((E-S)) bayt=$(stat -c%s "$out") bitrate=$(ffprobe -v error -show_entries format=bit_rate -of csv=p=0 "$out")" | tee -a "$L"
}

kodla p3_882   "$K3" 483 "882:496" "$O/kc_p3_882.mp4"
kodla p3_1280  "$K3" 483 "1280:720" "$O/kc_p3_1280.mp4"
kodla p2_882   "$K2" 484 "882:496" "$O/kc_p2_882.mp4"
kodla p2_1280  "$K2" 484 "1280:720" "$O/kc_p2_1280.mp4"
kodla p2_1080  "$K2" 484 ""        "$O/kc_p2_1080.mp4"

for t in p3_882 p3_1280 p2_882 p2_1280 p2_1080; do
  case $t in p3_*) R="$K3";; *) R="$K2";; esac
  echo "### VMAF: $t ###" | tee -a "$L"
  dotnet run --project tools/VidShrink.Ab -c Release -- denetle "$R" "$O/kc_$t.mp4" >"$O/vmaf_kc_$t.txt" 2>&1
  grep -oE "harm[^ ]*=[0-9.]+" "$O/vmaf_kc_$t.txt" | tail -1 | tee -a "$L"
done
echo "KUCULTME TAMAM" | tee -a "$L"
