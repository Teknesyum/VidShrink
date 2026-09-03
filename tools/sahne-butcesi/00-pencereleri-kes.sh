#!/usr/bin/env bash
# Uc olcum penceresini kaynak havuzundan keser. Havuz paylasimlidir, salt okunur.
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HAVUZ="${VIDSHRINK_KAYNAK:-C:/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak}"
S="$HAVUZ/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"
D="$KOK/.calisma/T114/kaynak"
mkdir -p "$D"
[ -s "$S" ] || { echo "kaynak yok: $S" >&2; exit 1; }

kes() {
  local ad="$1" bas="$2" sure="$3"
  if [ -s "$D/$ad.mkv" ]; then echo "$ad zaten var"; return 0; fi
  ffmpeg -y -v error -ss "$bas" -t "$sure" -i "$S" -an \
    -c:v libx265 -preset veryfast -crf 12 -pix_fmt yuv420p10le \
    -x265-params pools=8:log-level=error -threads 8 "$D/$ad.mkv"
  echo "$ad hazir"
}

kes p1-karisik   144.117 189.183 &
kes p2-durgun    333.300 186.366 &
wait
kes p3-hareketli 600.000 189.000
ls -l "$D"
