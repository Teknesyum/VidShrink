#!/usr/bin/env bash
set -eu
POOL="C:/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak"
BIG="$POOL/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"
OUT="${OUT:-C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T133/.calisma/t133/kaynak}"
mkdir -p "$OUT"
TM="zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p"
CHAIN="$TM,scale=1280:720:flags=lanczos,fps=30,setsar=1"

kes720 () {
  ad=$1; bas=$2
  [ -f "$OUT/$ad.mkv" ] && { echo "atlandi $ad"; return; }
  ffmpeg -hide_banner -v error -y -ss "$bas" -t 20 -i "$BIG" \
    -vf "$CHAIN" -an -sn -dn -c:v libx264 -crf 12 -preset veryfast -threads 8 \
    -color_primaries bt709 -color_trc bt709 -colorspace bt709 "$OUT/$ad.mkv"
  echo "bitti $ad"
}

kes720 s1-kesikli   520
kes720 s2-durgun    360
kes720 s3-hareketli 680

if [ ! -f "$OUT/s4-yuksek.mkv" ]; then
  ffmpeg -hide_banner -v error -y -i "$POOL/parca-1.mkv" -t 20 -map 0:v:0 -c copy "$OUT/s4-yuksek.mkv"
  echo "bitti s4-yuksek"
fi

for f in "$OUT"/*.mkv; do
  ffprobe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,pix_fmt,nb_read_packets \
    -count_packets -of csv=p=0 "$f" | sed "s|^|$(basename "$f") |"
done
