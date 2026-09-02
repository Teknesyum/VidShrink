#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
REF="gui/parca-2.mkv"
T="gui/parca-2_shrunk.mp4"
M="model=version=vmaf_v0.6.1neg:n_threads=4"
S="scale=w=1920:h=1080:flags=lanczos"
mkdir -p vmaf
echo "kaynak tb: $(ffprobe -v error -select_streams v:0 -show_entries stream=time_base -of csv=p=0 $REF)"
echo "cikti  tb: $(ffprobe -v error -select_streams v:0 -show_entries stream=time_base -of csv=p=0 $T)"
kos () {
  ad="$1"; kilit="$2"
  [ -f "vmaf/settb-$ad.json" ] && { echo "atlandi $ad"; return; }
  g="[0:v]${S},${kilit}[t];[1:v]${kilit}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=vmaf/settb-${ad}.json"
  ffmpeg -hide_banner -loglevel error -nostdin -threads 4 -i "$T" -i "$REF" -lavfi "$g" -f null - \
    && echo "olculdu $ad" || echo "HATA $ad"
}
kos "tam"       "settb=AVTB,setpts=N"
kos "settb-yok" "setpts=N"
kos "startpts"  "settb=AVTB,setpts=PTS-STARTPTS"
echo "SETTB BITTI $(date +%T)"
