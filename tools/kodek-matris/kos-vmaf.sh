set -e
O=".calisma/kodek-matris"
K2=".calisma/kaynak/parca-2-yalniz-video.mkv"
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"

denetle() {
  local src=$1 out=$2 log=$3
  echo "### VMAF: $out ###" | tee -a "$log"
  dotnet run --project tools/VidShrink.Ab -c Release --no-build -- denetle "$src" "$out" >>"$log" 2>&1
}

denetle "$K3" "$O/p3_av1_p4_4811.mp4" "$O/vmaf_p3_av1_p4_4811.txt"
denetle "$K3" "$O/p3_av1_p2_4811.mp4" "$O/vmaf_p3_av1_p2_4811.txt"
denetle "$K2" "$O/p2_av1_p6_484.mp4"  "$O/vmaf_p2_av1_p6_484.txt"
denetle "$K2" "$O/p2_x265_484.mp4"    "$O/vmaf_p2_x265_484.txt"
denetle "$K2" "$O/p2_x264_484.mp4"    "$O/vmaf_p2_x264_484.txt"
denetle "$K3" "$O/p3_av1_p6_483.mp4"  "$O/vmaf_p3_av1_p6_483.txt"
denetle "$K3" "$O/p3_x265_483.mp4"    "$O/vmaf_p3_x265_483.txt"
denetle "$K3" "$O/p3_x264_483.mp4"    "$O/vmaf_p3_x264_483.txt"
denetle "$K2" "$O/p2_av1_p6_4837.mp4" "$O/vmaf_p2_av1_p6_4837.txt"

echo "VMAF TAMAM"
