#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
mkdir -p "$CIKTI" "$IS/gecis"

KOD="-c:v libx264 -preset slow -pix_fmt yuv420p10le -an"

kodla () {
  ad="$1"; kol="$2"; hiz="$3"; vf="$4"
  cik="$CIKTI/${ad}-${kol}-${hiz}.mp4"
  if [ -f "$cik" ]; then echo "atlandi ${ad}-${kol}-${hiz}"; return; fi
  gl="$IS/gecis/${ad}-${kol}-${hiz}"
  ffmpeg -hide_banner -loglevel error -nostdin -y -i "$KAYNAK/$ad.mkv" -vf "$vf" \
    $KOD -b:v "${hiz}k" -pass 1 -passlogfile "$gl" $RENK -f mp4 NUL 2>/dev/null
  ffmpeg -hide_banner -loglevel error -nostdin -y -i "$KAYNAK/$ad.mkv" -vf "$vf" \
    $KOD -b:v "${hiz}k" -pass 2 -passlogfile "$gl" $RENK -movflags +faststart "$cik"
  if [ -f "$cik" ]; then echo "kodlandi ${ad}-${kol}-${hiz}"; else echo "HATA ${ad}-${kol}-${hiz}"; fi
}

kodla "NA" "duz"    "2000" "null"
kodla "NA" "yanlis" "2000" "crop=1920:1042:0:4"
kodla "NB" "duz"    "2000" "null"
kodla "NB" "yanlis" "2000" "crop=1920:1072:0:6"
echo "ZARAR BITTI"
