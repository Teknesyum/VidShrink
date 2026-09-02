#!/usr/bin/env bash
# T133 ortak kodlama/olcum parcalari. Butun hucreler ayni fonksiyondan gecer.
ROOT="C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T133"
KAY="$ROOT/.calisma/t133/kaynak"
ISI="$ROOT/.calisma/t133/cikti"
VMD="$ROOT/.calisma/t133/vmaf"
mkdir -p "$ISI" "$VMD" "$ROOT/.calisma/t133/log"

fps_of () { ffprobe -v error -select_streams v:0 -show_entries stream=r_frame_rate -of csv=p=0 "$1" | awk -F/ '{printf "%.6f", $1/$2}'; }

# kodla <ad> <kaynak> <g_kare> <min_kare> <bitrate_k> [kodlayici]
kodla () {
  ad=$1; src=$2; g=$3; kmin=$4; br=$5; enc=${6:-libx264}
  out="$ISI/$ad.mp4"
  [ -f "$out" ] && { echo "atlandi $ad"; return 0; }
  log="$ROOT/.calisma/t133/log/$ad"
  if [ "$enc" = "libsvtav1" ]; then
    ffmpeg -hide_banner -v error -nostdin -y -i "$src" -an -sn -dn \
      -c:v libsvtav1 -preset 4 -b:v "${br}k" -g "$g" -svtav1-params "keyint=$g:scd=1" \
      "$out" || return 1
  else
    ffmpeg -hide_banner -v error -nostdin -y -i "$src" -an -sn -dn \
      -c:v libx264 -preset medium -threads 8 -b:v "${br}k" -g "$g" -keyint_min "$kmin" \
      -pass 1 -passlogfile "$log" -f null - || return 1
    ffmpeg -hide_banner -v error -nostdin -y -i "$src" -an -sn -dn \
      -c:v libx264 -preset medium -threads 8 -b:v "${br}k" -g "$g" -keyint_min "$kmin" \
      -pass 2 -passlogfile "$log" "$out" || return 1
  fi
  echo "kodlandi $ad"
}

# olc <ad> <kaynak>
olc () {
  ad=$1; src=$2
  j="$VMD/$ad.json"
  [ -f "$j" ] && { echo "atlandi-vmaf $ad"; return 0; }
  ( cd "$VMD" && ffmpeg -hide_banner -v error -nostdin -i "$ISI/$ad.mp4" -i "$src"       -lavfi "[0:v]format=yuv420p10le,settb=AVTB,setpts=N[d];[1:v]format=yuv420p10le,settb=AVTB,setpts=N[r];[d][r]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=8:log_fmt=json:log_path=$ad.json"       -fps_mode passthrough -f null - ) >/dev/null 2>&1
  [ -f "$j" ] && echo "olculdu $ad" || { echo "HATA-vmaf $ad"; return 1; }
}

# anahtar_kare <ad>  -> sayi ve gerceklesen ortalama aralik
anahtar_kare () {
  ffprobe -v error -select_streams v:0 -skip_frame nokey -show_entries frame=pts_time -of csv=p=0 "$ISI/$1.mp4" \
    | awk 'NF{n++; t=$1+0; if(n>1) s+=t-p; p=t} END{printf "%d %.3f", n, (n>1? s/(n-1):0)}'
}
