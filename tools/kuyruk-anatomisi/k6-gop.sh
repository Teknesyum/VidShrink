#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t122"
SRC="$BASE/../../../.calisma/kaynak/parca-2.mkv"
REF="$SRC"
PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
TH=4
M="model=version=vmaf_v0.6.1neg:n_threads=4"
LOCK="settb=AVTB,setpts=N"
SCALE="scale=w=1920:h=1080:flags=lanczos"
mkdir -p vmaf-t122

for g in 300 600; do
  n="auto-g$g"
  if [ ! -f "ciktilar/$n.mp4" ]; then
    echo "=== $n $(date +%T)"
    for p in 1 2; do
      if [ "$p" = 1 ]; then extra=(-an -f null /dev/null); else extra=(-c:a aac -b:a 128k -movflags +faststart "ciktilar/.$n.yaziliyor.mp4"); fi
      ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
        -c:v libsvtav1 -preset 6 -b:v 2026k -pass $p -passlogfile "log/$n" \
        -g $g -pix_fmt p010le -svtav1-params "$PSY" $COL "${extra[@]}" || { echo "HATA $n p$p"; exit 1; }
    done
    mv -f "ciktilar/.$n.yaziliyor.mp4" "ciktilar/$n.mp4"
    echo "bitti $n $(stat -c %s ciktilar/$n.mp4) $(date +%T)"
  fi
  if [ ! -f "vmaf-t122/$n-kilitli.json" ]; then
    ffmpeg -hide_banner -loglevel error -nostdin -threads $TH -i "ciktilar/$n.mp4" -i "$REF" \
      -lavfi "[0:v]${SCALE},${LOCK}[t];[1:v]${LOCK}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=vmaf-t122/$n-kilitli.json" \
      -f null - && echo "olculdu $n $(date +%T)" || echo "HATA olcum $n"
  fi
done
echo "K6 BITTI $(date +%T)"
