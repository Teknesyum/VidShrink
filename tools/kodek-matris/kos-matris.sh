set -e
O=".calisma/kodek-matris"
HDR="-pix_fmt p010le -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -an"
K2=".calisma/kaynak/parca-2-yalniz-video.mkv"
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"

run_av1() {
  local tag=$1 src=$2 preset=$3 bitrate=$4 out=$5 log=$6
  echo "### AV1 preset $preset $tag @ ${bitrate} ###" | tee -a "$log"
  S=$(date +%s)
  ffmpeg -hide_banner -y -i "$src" -c:v libsvtav1 -preset $preset -b:v ${bitrate}k -pass 1 -passlogfile "$O/${tag}_av1pass" -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 $HDR -f null /dev/null >>"$log" 2>&1
  ffmpeg -hide_banner -y -i "$src" -c:v libsvtav1 -preset $preset -b:v ${bitrate}k -pass 2 -passlogfile "$O/${tag}_av1pass" -g 600 -svtav1-params keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2 $HDR -movflags +faststart "$out" >>"$log" 2>&1
  E=$(date +%s)
  echo "SANIYE=$(( E - S ))" | tee -a "$log"
}

run_x265() {
  local tag=$1 src=$2 bitrate=$3 out=$4 log=$5
  echo "### x265 slow $tag @ ${bitrate} ###" | tee -a "$log"
  S=$(date +%s)
  ffmpeg -hide_banner -y -i "$src" -c:v libx265 -preset slow -b:v ${bitrate}k -g 600 -keyint_min 60 $HDR -x265-params pass=1:stats="$O/${tag}_x265pass" -f null /dev/null >>"$log" 2>&1
  ffmpeg -hide_banner -y -i "$src" -c:v libx265 -preset slow -b:v ${bitrate}k -g 600 -keyint_min 60 $HDR -x265-params pass=2:stats="$O/${tag}_x265pass" -movflags +faststart "$out" >>"$log" 2>&1
  E=$(date +%s)
  echo "SANIYE=$(( E - S ))" | tee -a "$log"
}

run_x264() {
  local tag=$1 src=$2 bitrate=$3 out=$4 log=$5
  echo "### x264 slow $tag @ ${bitrate} ###" | tee -a "$log"
  S=$(date +%s)
  ffmpeg -hide_banner -y -i "$src" -c:v libx264 -preset slow -b:v ${bitrate}k -pass 1 -passlogfile "$O/${tag}_x264pass" -g 600 -keyint_min 60 $HDR -f null /dev/null >>"$log" 2>&1
  ffmpeg -hide_banner -y -i "$src" -c:v libx264 -preset slow -b:v ${bitrate}k -pass 2 -passlogfile "$O/${tag}_x264pass" -g 600 -keyint_min 60 $HDR -movflags +faststart "$out" >>"$log" 2>&1
  E=$(date +%s)
  echo "SANIYE=$(( E - S ))" | tee -a "$log"
}

# A grubu: parca-3, 4811k, sure kiyasi
run_av1 p3_av1p4_A "$K3" 4 4811 "$O/p3_av1_p4_4811.mp4" "$O/log_p3_av1_p4.txt"
run_av1 p3_av1p2_A "$K3" 2 4811 "$O/p3_av1_p2_4811.mp4" "$O/log_p3_av1_p2.txt"

# B grubu: dusuk hedef
run_av1  p2_av1p6_B "$K2" 6 484 "$O/p2_av1_p6_484.mp4"  "$O/log_p2_av1_p6.txt"
run_x265 p2_x265_B  "$K2"    484 "$O/p2_x265_484.mp4"    "$O/log_p2_x265.txt"
run_x264 p2_x264_B  "$K2"    484 "$O/p2_x264_484.mp4"    "$O/log_p2_x264.txt"

run_av1  p3_av1p6_B "$K3" 6 483 "$O/p3_av1_p6_483.mp4"  "$O/log_p3_av1_p6.txt"
run_x265 p3_x265_B  "$K3"    483 "$O/p3_x265_483.mp4"    "$O/log_p3_x265.txt"
run_x264 p3_x264_B  "$K3"    483 "$O/p3_x264_483.mp4"    "$O/log_p3_x264.txt"

# C grubu: parca-2, 4837k
run_av1 p2_av1p6_C "$K2" 6 4837 "$O/p2_av1_p6_4837.mp4" "$O/log_p2_av1_p6_C.txt"

echo "TUM KOSUMLAR TAMAM"
