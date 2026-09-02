#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t122"
REF="$BASE/../../../.calisma/kaynak/parca-2.mkv"
TH=4
mkdir -p vmaf-t122
M="model=version=vmaf_v0.6.1neg:n_threads=4"
SCALE="scale=w=1920:h=1080:flags=lanczos"
LOCK="settb=AVTB,setpts=N"

olc () {
  n="$1"; t="ciktilar/$1.mp4"
  [ -f "$t" ] || { echo "YOK $n"; return; }
  [ -f "vmaf-t122/${n}-kilitli.json" ] && { echo "atlandi $n"; return; }
  g="[0:v]${SCALE},${LOCK}[t];[1:v]${LOCK}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=vmaf-t122/${n}-kilitli.json"
  ffmpeg -hide_banner -loglevel error -nostdin -threads $TH -i "$t" -i "$REF" -lavfi "$g" -f null - \
    && echo "olculdu $n $(date +%T)" || echo "HATA $n"
}
olc auto
olc uzman-hb2
echo "OLCUM BITTI $(date +%T)"
