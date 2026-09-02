#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
REF="gui/parca-2.mkv"
M="model=version=vmaf_v0.6.1neg:n_threads=2"
S="scale=w=1920:h=1080:flags=lanczos"
kaydir () {
  n="$1"; t="$2"; r="$3"
  o="vmaf/kaydir-${n}.json"
  [ -f "$o" ] && { echo "atlandi $n"; return; }
  ffmpeg -hide_banner -loglevel error -nostdin -threads 2 -i "$t" -i "$REF" \
    -lavfi "[0:v]${S},settb=AVTB,setpts=N[t];[1:v]settb=AVTB,setpts=${r}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=${o}" \
    -f null - && echo "olculdu $n" || echo "HATA $n"
}
kaydir "auto-ref-artiBir"  "gui/parca-2_shrunk.mp4" "N+1"
kaydir "auto-ref-eksiBir"  "gui/parca-2_shrunk.mp4" "N-1"
echo "KAYDIRMA BITTI $(date +%T)"
