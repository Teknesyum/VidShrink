#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
SRC="gui/parca-2.mkv"
NAME="${1:-uzman-biz}"; PRESET="${2:-4}"; GOP="${3:-300}"; BR="${4:-2700}"; VF="${5:-}"
PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
vfarg=(); if [ -n "$VF" ]; then vfarg=(-vf "$VF"); fi
mkdir -p ciktilar log
echo "=== $NAME preset=$PRESET gop=$GOP br=${BR}k vf=${VF:-yok} $(date +%T)"
ffmpeg -hide_banner -loglevel error -y -hwaccel auto -i "$SRC" "${vfarg[@]}" \
  -c:v libsvtav1 -preset "$PRESET" -b:v "${BR}k" -pass 1 -passlogfile "log/${NAME}" \
  -g "$GOP" -pix_fmt p010le -svtav1-params "$PSY" $COL -an -f null NUL
ffmpeg -hide_banner -loglevel error -y -hwaccel auto -i "$SRC" "${vfarg[@]}" \
  -c:v libsvtav1 -preset "$PRESET" -b:v "${BR}k" -pass 2 -passlogfile "log/${NAME}" \
  -g "$GOP" -pix_fmt p010le -svtav1-params "$PSY" $COL \
  -c:a aac -b:a 128k -movflags +faststart "ciktilar/${NAME}.mp4"
echo "bitti $NAME $(stat -c %s ciktilar/${NAME}.mp4) $(date +%T)"
