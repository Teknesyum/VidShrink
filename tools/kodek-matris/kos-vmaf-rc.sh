set -e
O=".calisma/kodek-matris"
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"
L="$O/rc-vmaf-genel.txt"
denetle() {
  echo "### VMAF: $1 ###" | tee -a "$L"
  dotnet run --project tools/VidShrink.Ab -c Release -- denetle "$K3" "$1" >"$2" 2>&1
  grep -oE "harm[^ ]*=[0-9.]+" "$2" | tail -1 | tee -a "$L"
}
denetle "$O/rc_p6_vbsiz.mp4" "$O/vmaf_rc_p6_vbsiz.txt"
denetle "$O/rc_p4_vbsiz.mp4" "$O/vmaf_rc_p4_vbsiz.txt"
denetle "$O/rc_p4_vb.mp4"    "$O/vmaf_rc_p4_vb.txt"
echo "RC VMAF TAMAM" | tee -a "$L"
