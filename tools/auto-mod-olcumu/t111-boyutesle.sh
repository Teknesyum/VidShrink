#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
SRC="gui/parca-2.mkv"
PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
TH=4
enc () {
  name="$1"; preset="$2"; gop="$3"; br="$4"
  out="ciktilar/${name}.mp4"
  [ -f "$out" ] && { echo "atlandi $name"; return; }
  echo "=== $name preset=$preset gop=$gop br=${br}k $(date +%T)"
  for p in 1 2; do
    if [ "$p" = 1 ]; then extra=(-an -f null NUL); else extra=(-c:a aac -b:a 128k -movflags +faststart "$out"); fi
    ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
      -c:v libsvtav1 -preset "$preset" -b:v "${br}k" -pass $p -passlogfile "log/${name}" \
      -g "$gop" -pix_fmt p010le -svtav1-params "$PSY" $COL "${extra[@]}" || { echo "HATA $name pass$p"; return; }
  done
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}
hb () {
  name="$1"; br="$2"
  out="ciktilar/${name}.mp4"
  [ -f "$out" ] && { echo "atlandi $name"; return; }
  echo "=== $name HandBrake b=${br} $(date +%T)"
  HandBrakeCLI -i "$SRC" -o "$out" \
    -e x265_10bit --encoder-preset slow --encopts "pools=4" \
    -b "$br" --multi-pass --turbo \
    -E ca_aac -B 128 --mixdown stereo \
    -w 1920 -l 1080 --crop-mode none -r 60 --cfr \
    -f av_mp4 -O >"log/${name}.log" 2>&1
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}
enc "uzman-biz4" 4 300 "${1:-2557}"
hb  "uzman-hb3" "${2:-1867}"
echo "ESLEME BITTI $(date +%T)"
