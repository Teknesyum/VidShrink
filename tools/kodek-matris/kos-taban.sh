set -e
O=".calisma/kodek-matris"; L="$O/taban-log.txt"
HDR="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
taban() {
  local tag=$1 src=$2 preset=$3 params=$4
  echo "### CRF63 taban: $tag  preset=$preset  params=$params ###" | tee -a "$L"
  ffmpeg -hide_banner -y -i "$src" -c:v libsvtav1 -preset $preset -crf 63 -g 600 \
    -svtav1-params "$params" $HDR -movflags +faststart "$O/taban_$tag.mp4" >"$O/log_taban_$tag.txt" 2>&1
  echo "$tag bayt=$(stat -c%s "$O/taban_$tag.mp4") bitrate=$(ffprobe -v error -show_entries format=bit_rate -of csv=p=0 "$O/taban_$tag.mp4")" | tee -a "$L"
}
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"
taban p3_p6_vbsiz "$K3" 6 "keyint=600:scd=1:tune=0"
taban p3_p6_vb    "$K3" 6 "keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2"
taban p3_p4_vbsiz "$K3" 4 "keyint=600:scd=1:tune=0"
echo "TABAN TAMAM" | tee -a "$L"
