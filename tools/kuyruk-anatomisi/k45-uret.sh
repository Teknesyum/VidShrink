#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t122"
SRC="$BASE/../../../.calisma/kaynak/parca-2.mkv"
PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
TH=4

if [ ! -f ciktilar/auto.mp4 ]; then
  echo "=== auto preset=6 g=120 br=2026k $(date +%T)"
  for p in 1 2; do
    if [ "$p" = 1 ]; then extra=(-an -f null /dev/null); else extra=(-c:a aac -b:a 128k -movflags +faststart ciktilar/.auto.yaziliyor.mp4); fi
    ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
      -c:v libsvtav1 -preset 6 -b:v 2026k -pass $p -passlogfile log/auto \
      -g 120 -pix_fmt p010le -svtav1-params "$PSY" $COL "${extra[@]}" || { echo "HATA auto pass$p"; exit 1; }
  done
  mv -f ciktilar/.auto.yaziliyor.mp4 ciktilar/auto.mp4
  echo "bitti auto $(stat -c %s ciktilar/auto.mp4) $(date +%T)"
fi

if [ ! -f ciktilar/uzman-hb2.mp4 ]; then
  echo "=== uzman-hb2 HandBrake b=1900 $(date +%T)"
  HandBrakeCLI -i "$SRC" -o ciktilar/.uzman-hb2.yaziliyor.mp4 \
    -e x265_10bit --encoder-preset slow --encopts "pools=4" \
    -b 1900 --multi-pass --turbo \
    -E ca_aac -B 128 --mixdown stereo \
    -w 1920 -l 1080 --crop-mode none -r 60 --cfr \
    -f av_mp4 -O >log/uzman-hb2.log 2>&1 || { echo "HATA hb2"; exit 1; }
  mv -f ciktilar/.uzman-hb2.yaziliyor.mp4 ciktilar/uzman-hb2.mp4
  echo "bitti uzman-hb2 $(stat -c %s ciktilar/uzman-hb2.mp4) $(date +%T)"
fi
echo "URETIM BITTI $(date +%T)"
