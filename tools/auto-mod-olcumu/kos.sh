#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
SRC="gui/parca-2.mkv"
mkdir -p ciktilar log

PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"

enc () {
  name="$1"; preset="$2"; gop="$3"; vf="$4"
  out="ciktilar/${name}.mp4"
  if [ -f "$out" ]; then echo "atlandi $name"; return; fi
  vfarg=()
  if [ -n "$vf" ]; then vfarg=(-vf "$vf"); fi
  echo "=== $name preset=$preset gop=$gop vf=${vf:-yok} $(date +%T)"
  ffmpeg -hide_banner -loglevel error -y -hwaccel auto -i "$SRC" "${vfarg[@]}" \
    -c:v libsvtav1 -preset "$preset" -b:v 2026k -pass 1 -passlogfile "log/${name}" \
    -g "$gop" -pix_fmt p010le -svtav1-params "$PSY" $COL -an -f null NUL
  ffmpeg -hide_banner -loglevel error -y -hwaccel auto -i "$SRC" "${vfarg[@]}" \
    -c:v libsvtav1 -preset "$preset" -b:v 2026k -pass 2 -passlogfile "log/${name}" \
    -g "$gop" -pix_fmt p010le -svtav1-params "$PSY" $COL \
    -c:a aac -b:a 128k -movflags +faststart "$out"
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}

enc "e1-preset4"  4 120 ""
enc "e2-gop300"   6 300 ""
enc "e3-olcek810" 6 120 "scale=1440:810:flags=lanczos"
echo "HEPSI BITTI $(date +%T)"
